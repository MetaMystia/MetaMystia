namespace SgrYuki.Utils;

public class TestClass
{
    public static GameData.Profile.DLC1_YuumaBossData Test()
    {
        return NightScene.SceneManager.Instance.m_LoadedBossData as GameData.Profile.DLC1_YuumaBossData;
    }
}
