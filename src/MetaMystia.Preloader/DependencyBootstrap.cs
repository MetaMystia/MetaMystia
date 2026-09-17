using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

using BepInEx;
using BepInEx.Preloader.Core.Patching;

namespace MetaMystia.Preloader;

[PatcherPluginInfo("MetaMystia.Preloader", "MetaMystia Preloader", BuildVersion.Value)]
public sealed class DependencyBootstrap : BasePatcher
{
    public override void Initialize()
    {
        string fileName = $"MetaMystia-v{BuildVersion.Value}.dll";
        var paths = Directory.GetFiles(Paths.PluginPath, fileName, SearchOption.AllDirectories);
        if (paths.Length != 1)
        {
            Log.LogError($"Expected one {fileName} in plugins, found {paths.Length}.");
            return;
        }

        var assembly = Assembly.LoadFrom(paths[0]);
        RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        Log.LogInfo("MetaMystia embedded dependency resolver initialized before plugin discovery.");
    }
}
