using System.Collections;

using Mystia;
using Mystia.Imgui;
using Mystia.Scenes;

using Common.UI;

using MetaMystia.Multiplayer;
using MetaMystia.UI;
using SgrYuki;

namespace MetaMystia;

/// <summary>
/// 模组全局循环与 IMGUI 宿主（取代原先的 <c>PluginHost</c> MonoBehaviour）：
/// 由框架的常驻泵驱动，跨场景有效。
/// </summary>
[AutoLog]
public sealed partial class ModLoop : IGlobalGameLoop, IIMGUIProvider
{
    public void Setup(IGlobalServices services)
    {
        ModRuntime.MainThread = services.Common.MainThread;
        ModRuntime.Coroutines = services.Common.Coroutines;
        ModRuntime.Assets = services.Common.Assets;
        ModRuntime.Locator = services.Common.Locator;
        ModRuntime.MapBuilder = services.Common.MapBuilder;
        ModRuntime.Dialogs = services.Common.Dialogs;

        InGameConsole.Initialize();
        ResourceExManager.FlushPendingConsoleLogs();
    }

    public void Update(IGlobalServices services, float delta)
    {
        ConfigManager.Flush();

        GameFlow.RefreshInStoryCache();
        GameSession.Tick();

        InGameConsole.Update();
        PlayerListPanel.Update();

        PluginManager.HandleShortcuts();
    }

    public void FixedUpdate(IGlobalServices services, float delta)
    {
        CommandScheduler.Tick();

        switch (GameFlow.LocalScene)
        {
            case Scene.DayScene:
            case Scene.WorkScene:
                PlayerManager.OnFixedUpdate();
                break;
        }
    }

    public void OnGui(IIMGUIDrawer drawer)
    {
        InGameConsole.OnGui(drawer);
        PlayerListPanel.OnGui(drawer);
        PluginManager.DrawStatusOverlay(drawer);
    }

    public void Shutdown(IGlobalServices services)
    {
        GameSession.Stop();
        ModRuntime.Coroutines?.StopAll();
        ModRuntime.Coroutines = null;
    }

    /// <summary>启动一个托管协程，跨场景存活（原先的 <c>PluginHost.StartManagedCoroutine</c>）。</summary>
    public static CoroutineHandle StartManagedCoroutine(IEnumerator routine) =>
        ModRuntime.Coroutines?.Start(_ => routine) ?? default;
}
