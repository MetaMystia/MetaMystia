using System;
using System.Reflection;

namespace SgrYuki;

public static class NativeDllExtractor
{
    public static string Extract(string resourceName, string fileName)
    {
        string targetPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            fileName
        );

        if (System.IO.File.Exists(targetPath))
            return targetPath;

        using var stream =
            Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(resourceName)
            ?? throw new Exception("Resource not found: " + resourceName);

        using var file = System.IO.File.Create(targetPath);
        stream.CopyTo(file);

        return targetPath;
    }
}
