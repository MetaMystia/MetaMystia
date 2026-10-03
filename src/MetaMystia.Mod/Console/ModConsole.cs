using System.Runtime.InteropServices;

namespace MetaMystia.ConsoleSystem;

/// <summary>
/// 给进程分配一个 Win32 调试控制台，取代原先 BepInEx 控制台创建接口。
/// 游戏进程默认没有控制台；只在用户显式执行控制台命令时调用。
/// </summary>
internal static partial class ModConsole
{
    /// <summary>分配新的控制台窗口；进程已挂控制台时返回 false 且不改变现状。</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    public static void Create() => AllocConsole();
}
