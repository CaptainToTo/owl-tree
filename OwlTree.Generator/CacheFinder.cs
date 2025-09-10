using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace OwlTree.Generator
{
    public static class CacheFinder
    {
        public static void GetCache(SourceProductionContext context, Compilation compilation)
        {
            var proj = GetProjectPath(compilation);
            var isLib = IsLibraryProject(proj);

            if (proj != GeneratorState.CurProjectPath)
            {
                GeneratorState.IsLibraryProject = isLib;
                GeneratorState.CurProjectPath = proj.ToLower();
            }

            var foundPath = FindGeneratorPath(proj);

            if (foundPath == null)
                return;

            if (string.IsNullOrEmpty(GeneratorState.CachePath))
            {
                GeneratorState.CachePath = foundPath;
                GeneratorState.LoadCache();
            }

            if (!GeneratorState.HasProject(proj))
                GeneratorState.AddProject(proj);
            GeneratorState.CurProjectId = GeneratorState.GetProjectId(proj);

            LoadLibraryReferences(context, GeneratorState.CurProjectPath);
        }

        private static string GetProjectPath(Compilation compilation)
        {
            var firstFile = compilation.SyntaxTrees.FirstOrDefault()?.FilePath;
            if (string.IsNullOrEmpty(firstFile))
                return null;

            var dir = Path.GetDirectoryName(firstFile);
            while (dir != null)
            {
                var csproj = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
                if (csproj != null)
                    return csproj;

                dir = Directory.GetParent(dir)?.FullName;
            }

            return null;
        }

        private static bool IsLibraryProject(string csprojPath)
        {
            if (string.IsNullOrWhiteSpace(csprojPath))
                throw new ArgumentException("Path cannot be null or empty.", nameof(csprojPath));

            if (!File.Exists(csprojPath))
                throw new FileNotFoundException("Project file not found.", csprojPath);

            Regex OwlTreeTagRegex = new Regex(
                @$"<\s*{Helpers.Tk_LibProject}\s*/\s*>",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            string text = File.ReadAllText(csprojPath);
            return OwlTreeTagRegex.IsMatch(text);
        }

        public static string FindGeneratorPath(string csprojPath)
        {
            if (string.IsNullOrWhiteSpace(csprojPath))
                throw new ArgumentException("Project path cannot be null or empty.", nameof(csprojPath));

            if (!File.Exists(csprojPath))
                throw new FileNotFoundException("Project file not found.", csprojPath);

            var baseDir = Path.GetDirectoryName(csprojPath)
                ?? throw new InvalidOperationException("Cannot determine project directory.");

            var doc = XDocument.Load(csprojPath);

            // Look for <ProjectReference Include="..." OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
            var projectRef = doc.Descendants("ProjectReference")
                .FirstOrDefault(e =>
                    string.Equals(e.Attribute("OutputItemType")?.Value, "Analyzer", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(e.Attribute("ReferenceOutputAssembly")?.Value, "false", StringComparison.OrdinalIgnoreCase) &&
                    e.Attribute("Include") != null &&
                    Path.GetFileName(e.Attribute("Include")!.Value)
                        .Equals("OwlTree.Generator.csproj", StringComparison.OrdinalIgnoreCase));

            if (projectRef != null)
            {
                var include = projectRef.Attribute("Include")?.Value;
                if (!string.IsNullOrWhiteSpace(include))
                {
                    string absPath = Path.GetFullPath(Path.Combine(baseDir, include));
                    return Path.GetDirectoryName(absPath);
                }
            }

            // Look for <Analyzer Include="...">
            var analyzerRef = doc.Descendants("Analyzer")
                .FirstOrDefault(e =>
                    e.Attribute("Include") != null &&
                    Path.GetFileName(e.Attribute("Include")!.Value)
                        .Equals("OwlTree.Generator.dll", StringComparison.OrdinalIgnoreCase));

            if (analyzerRef != null)
            {
                var include = analyzerRef.Attribute("Include")?.Value;
                if (!string.IsNullOrWhiteSpace(include))
                {
                    string absPath = Path.GetFullPath(Path.Combine(baseDir, include));
                    return Path.GetDirectoryName(absPath);
                }
            }

            return null;
        }

        public static void LoadLibraryReferences(SourceProductionContext context, string mainProjectPath)
        {
            if (string.IsNullOrWhiteSpace(mainProjectPath))
                throw new ArgumentException("Project path cannot be null or empty.", nameof(mainProjectPath));

            if (!File.Exists(mainProjectPath))
                throw new FileNotFoundException("Project file not found.", mainProjectPath);

            var baseDir = Path.GetDirectoryName(mainProjectPath)
                ?? throw new InvalidOperationException("Cannot determine project directory.");

            var doc = XDocument.Load(mainProjectPath);

            var refs = doc.Descendants("Reference");
            var paths = refs.Select(e => e.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v) && v.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Concat(
                    refs.Select(e => e.Attribute("HintPath")?.Value)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                )
                .Select(p => Path.GetFullPath(Path.Combine(baseDir, p)).ToLower());

            foreach (var p in paths)
            {
                var cacheFile = Directory.GetFiles(Path.GetDirectoryName(p))
                    .Where(f => f.EndsWith(Helpers.CacheFileType)).FirstOrDefault();

                if (string.IsNullOrEmpty(cacheFile))
                {
                    Diagnostics.LibraryCacheFileNotFound(context, p);
                    continue;
                }
                GeneratorState.LoadLibrary(p, cacheFile);
            }
        }

        public static int[] GetIncludedProjects(string mainProjectPath)
        {
            var knownProjects = GeneratorState.GetProjects().Select(p => p.path);

            if (string.IsNullOrWhiteSpace(mainProjectPath))
                throw new ArgumentException("Project path cannot be null or empty.", nameof(mainProjectPath));

            if (!File.Exists(mainProjectPath))
                throw new FileNotFoundException("Project file not found.", mainProjectPath);

            var knownSet = new HashSet<string>(
                knownProjects.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);

            var baseDir = Path.GetDirectoryName(mainProjectPath)
                ?? throw new InvalidOperationException("Cannot determine project directory.");

            var doc = XDocument.Load(mainProjectPath);

            var results = new List<string>();

            // Collect <ProjectReference Include="...">
            var projRefs = doc.Descendants("ProjectReference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v));

            // Collect <Reference Include="..."> with path
            var dllRefsFromInclude = doc.Descendants("Reference")
                .Select(e => e.Attribute("Include")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v) && v.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

            // Collect <Reference><HintPath>...</HintPath>
            var dllRefsFromHintPath = doc.Descendants("Reference")
                .Select(e => e.Element("HintPath")?.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v));

            var dllRefs = dllRefsFromInclude.Concat(dllRefsFromHintPath);

            foreach (var include in projRefs.Concat(dllRefs))
            {
                string absPath = Path.GetFullPath(Path.Combine(baseDir, include!)).ToLower();

                if (knownSet.Contains(absPath) && !results.Contains(absPath))
                    results.Add(absPath);
            }
            results.Add(mainProjectPath);

            return results.Select(p => GeneratorState.GetProjectId(p)).ToArray();
        }
    }
}