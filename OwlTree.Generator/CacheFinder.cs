using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace OwlTree.Generator
{
    public static class CacheFinder
    {
        public static void GetCache(Compilation compilation)
        {
            if (!string.IsNullOrEmpty(GeneratorState.CachePath))
                return;

            var proj = GetProjectPath(compilation);
            var foundPath = FindGeneratorPath(proj, "OwlTree.Generator");

            if (foundPath == null)
                return;

            GeneratorState.CachePath = foundPath + "/" + Helpers.CacheFile;

            if (File.Exists(GeneratorState.CachePath))
                LoadCache(proj, GeneratorState.CachePath);
            else
                StartCache(proj, GeneratorState.CachePath);
        }

        private static void StartCache(string proj, string cachePath)
        {
            throw new NotImplementedException();
        }

        private static void LoadCache(string proj, string cachePath)
        {
            throw new NotImplementedException();
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
    }
}