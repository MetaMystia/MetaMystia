namespace MetaMystia.Network;

/// <summary>
/// 移动同步总入口：按当前场景分流到白天 / 夜间移动同步。
/// </summary>
internal static class MoveSyncBehavior
{
    public static void Send()
    {
        switch (MpManager.LocalScene)
        {
            case Common.UI.Scene.DayScene:
                DayMoveSyncBehavior.Send();
                break;
            case Common.UI.Scene.WorkScene:
                NightMoveSyncBehavior.Send();
                break;
        }
    }
}
