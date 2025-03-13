using System;
using System.IO;
using System.Reflection;

// simple helpers for setting up test logs

namespace FileInitializer;

public class Logs
{
    public static void InitPath(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public static void InitFiles(params string[] paths)
    {
        foreach (var path in paths)
            File.WriteAllText(path, $"New test at {DateTimeOffset.UtcNow}:\n\n");
    }
}
