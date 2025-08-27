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
        public static void GetCache(Compilation compilation)
        {
            var proj = GetProjectPath(compilation);
            var isLib = IsLibraryProject(proj);

            if (proj != GeneratorState.CurProjectPath)
            {
                GeneratorState.IsLibraryProject = isLib;
                GeneratorState.CurProjectPath = proj.ToLower();
            }

            var foundPath = FindGeneratorPath(proj, "OwlTree.Generator");

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

        private static string FindGeneratorPath(string startPath, string generatorName)
        {
            if (string.IsNullOrWhiteSpace(startPath))
                throw new ArgumentException("Start path cannot be null or empty.", nameof(startPath));

            startPath = Path.GetFullPath(startPath);

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();
            queue.Enqueue(startPath);
            visited.Add(startPath);

            var dll = $"{generatorName}.dll";
            var csproj = $"{generatorName}.csproj";

            while (queue.Count > 0)
            {
                var currentDir = queue.Dequeue();

                // Check for DLL or csproj match
                string dllPath = Path.Combine(currentDir, dll);
                string csprojPath = Path.Combine(currentDir, csproj);

                if (File.Exists(dllPath) || File.Exists(csprojPath))
                    return currentDir;

                try
                {
                    // Enqueue neighbors: parent + subdirectories
                    var parent = Directory.GetParent(currentDir)?.FullName;
                    if (parent != null && visited.Add(parent))
                        queue.Enqueue(parent);

                    foreach (var dir in Directory.GetDirectories(currentDir))
                    {
                        if (visited.Add(dir))
                            queue.Enqueue(dir);
                    }
                }
                catch
                {
                    // Ignore directories we can't read
                }
            }

            return null; // Not found
        }

        public static int[] GetIncludedProjects(string mainProjectPath)
        {
            var knownProjects = GeneratorState.GetProjects().Select(p => p.project);

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