using System;

using Mystia;
using Mystia.Listeners;
using Mystia.Scenes;

namespace MetaMystia.Listeners;

/// <summary>
/// 存档/会话级通知与宿主状态读取。承接原 <c>SaveManagementPatch</c>（读档、回主菜单与回退一天前
/// 一律先清理联机状态）与 <c>DesktopPlatformProfilePatch</c>（平台解析到的 DLC keys 交给资源包依赖检查）。
/// </summary>
[AutoLog]
public sealed partial class SessionSync : ISessionListener, IGlobalGameLoop
{
    private static bool s_platformRead;

    /// <summary>
    /// 邀请记录（作用域无关）。取代原 <c>StatusTrackerPatch.RecordInvitedGuest_ReversePatch</c> 的调用点，
    /// 由全局循环在 <see cref="Setup"/> 时取一次。
    /// </summary>
    internal static IGuestRecords Records { get; private set; }

    public void Setup(IGlobalServices services)
    {
        Records = services.Common.Records;
        ReadPlatform();
    }

    /// <summary>入口绑定晚于全局循环首帧时，补读一次平台信息。</summary>
    public void Update(IGlobalServices services, float delta)
    {
        if (!s_platformRead) ReadPlatform();
    }

    /// <summary>原 <c>SaveManagement.LoadPlayerData</c> 前缀。</summary>
    public void OnPlayerDataLoading() => GameFlow.BeforeStateReset();

    /// <summary>原 <c>SaveManagement.DisposeGameStatusAndBackToMainMenu</c> / <c>DisposeGameStatusAndRewindDay</c> 前缀。</summary>
    public void OnGameStatusResetting(bool rewindingDay) => GameFlow.BeforeStateReset();

    /// <summary>
    /// 原 <c>SteamPlatformProfilePatch.GetActiveKeys_Postfix</c>：把平台解析到的 DLC keys 写进资源包依赖检查。
    /// 平台未解析出 keys（非 Steam 等）时不写标签，按 <c>ResourceExManager</c> 的兜底路径加载。
    /// </summary>
    private static void ReadPlatform()
    {
        var platform = ModRuntime.Context?.Platform;
        if (platform is null) return;

        s_platformRead = true;
        if (!platform.KeysResolved) return;

        ResourceExManager.SetActiveDlcTags(platform.ActiveDlcKeys);
        Log.Info($"Active DLC keys: {string.Join(", ", platform.ActiveDlcKeys)}");
    }
}
