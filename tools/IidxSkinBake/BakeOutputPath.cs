// Copyright (c) OMS contributors. Licensed under the MIT Licence.
using System;
using System.IO;

namespace osu.Tools.IidxSkinBake;

/// <summary>Validates a new bake directory without creating or relocating anything.</summary>
internal static class BakeOutputPath
{
    public static string Validate(string output, string? explicitRoot, params string[] sourceRoots)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) || File.Exists(output))
            throw new IOException("Output must be a new directory.");

        string systemDrive = Path.GetPathRoot(Environment.SystemDirectory)!;
        if (string.Equals(Path.GetPathRoot(output), systemDrive, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetPathRoot(output), @"C:\", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Bake output may not use the system drive.");

        if (explicitRoot != null)
        {
            if (!Path.IsPathFullyQualified(explicitRoot))
                throw new ArgumentException("An explicit output root must be an absolute directory.");
            string root = Path.GetFullPath(explicitRoot);
            if (!Directory.Exists(root)
                || string.Equals(Path.TrimEndingDirectorySeparator(root), Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root)!), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("An explicit output root must be an existing dedicated directory, not a volume root.");
            if (!BakeInput.IsBelow(output, root))
                throw new ArgumentException("Output escapes its explicit output root.");
        }
        else
        {
            string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            if (!BakeInput.IsBelow(output, Path.Combine(repository, ".dev-cache", "temp", "iidx-bake-dev"))
                && !BakeInput.IsBelow(output, Path.Combine(repository, "artifacts")))
                throw new ArgumentException("Output must be beneath this checkout's bake temp or artifacts, unless --output-root is supplied.");
        }

        foreach (string sourceRoot in sourceRoots)
        {
            string source = Path.GetFullPath(sourceRoot);
            if (string.Equals(output, source, StringComparison.OrdinalIgnoreCase)
                || BakeInput.IsBelow(output, source) || BakeInput.IsBelow(source, output))
                throw new ArgumentException("Output and source may not overlap.");
        }
        for (string? parent = output; parent != null; parent = Path.GetDirectoryName(parent))
        {
            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(parent);
            }
            catch (FileNotFoundException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Output ancestors may not contain reparse points.");
        }

        return output;
    }
}
