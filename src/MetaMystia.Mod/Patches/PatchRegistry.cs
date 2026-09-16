using System;
using HarmonyLib;

namespace MetaMystia.Patch;

[AutoLog]
public static partial class PatchRegistry
{
    public static readonly Type[] Patches = [
        // Launch
        typeof(SteamPlatformProfilePatch),

        // SceneManager Patches
        typeof(MainSceneManagerPatch),
        typeof(DaySceneManagerPatch),
        typeof(NightSceneManagerPatch),
        typeof(PrepNightSceneManagerPatch),
        typeof(ResultSceneManagerPatch),
        typeof(StaffSceneManagerPatch),
        typeof(UniversalGameManagerPatch),

        // DayScene Patches
        typeof(DaySceneSustainedPannelPatch),
        typeof(YuyukoExtraDialogData__c__DisplayClass4_0Patch),
        typeof(StatusTrackerPatch),
        typeof(CharacterControllerInputGeneratorComponentPatch),
        typeof(DayScenePlayerInputPatch),
        typeof(DaySceneMapPatch),
        typeof(NoteBookProfilePannelPatch),
        typeof(DaySceneShopPannelPatch),

        // PrepScene Patches
        typeof(IzakayaConfigPannelPatch),
        typeof(IzakayaConfigurePatch),
        typeof(IzakayaSelectorPanelPatch),

        // WorkScene Patches
        typeof(CookControllerPatch),
        typeof(SellablePatch),
        typeof(GuestsManagerPatch),
        typeof(GuestGroupControllerPatch),
        typeof(WorkSceneServePannelPatch),
        typeof(WorkSceneStoragePannelPatch),
        typeof(QTERewardManagerPatch),
        typeof(NightSceneEventManagerPatch),

        // 幽幽子挑战专用补丁
        typeof(YuyukoTimedNegativeSpellPatch),
        typeof(YuyukoPhase2GuestSpawnPatch),
        typeof(YuyukoChallengeContextPatch),
        typeof(YuyukoBossDataPatch),
        typeof(YuyukoMainLoopPatch),
        typeof(YuyukoTimingPatch),
        typeof(YuyukoPhase3GuestSpawnPatch),
        typeof(YuyukoLockCookersPatch),
        typeof(YuyukoRetakeContextPatch),
        typeof(NightSceneDirectorPatch),
        typeof(YuyukoOnFailPatch),
        typeof(IncomeControllerYuyukoPatch),

        typeof(WorkSceneSustainedPannelPatch),
        typeof(MystiaQTEBuffRewardPatch),
        typeof(GameTimeManagerPatch),
        typeof(WorkSceneCookingSelectionPannel__c__DisplayClass79_0Patch),
        typeof(UIManagerPatch),
        typeof(GuestsManager__c__DisplayClass174_0Patch),
        typeof(SpecialGuestsControllerPatch),
        typeof(NormalGuestsControllerPatch),

        typeof(RunTimeAlbumPatch),
        typeof(RunTimeSchedulerPatch),

        // ResourceEx Patches
        typeof(DataBaseCharacterPatch),
        typeof(DataBaseDayPatch),
        typeof(DataBaseCorePatch),
        typeof(DataBaseLanguagePatch),
        typeof(DaySceneLanguagePatch),
        typeof(NightSceneLanguagePatch),
        typeof(SpecialGuestDescriberPatch),
        typeof(DaySceneMapProfilePatch),
        typeof(DialogPannelPatch),
        typeof(DataBaseSchedulerPatch),
        typeof(RunTimeDayScenePatch),
        typeof(DaySceneChatSelectionPannel__c__DisplayClass17_0Patch),
        typeof(CollabBehaviourComponentPatch),
        typeof(DaySceneUIManagerPatch),
        typeof(TrackedMissionDataPatch),
    ];

    public static bool AllPatched => PatchedException == null;
    public static Exception PatchedException { get; set; }

    public static void ApplyAll(Harmony harmony)
    {
        Log.LogInfo($"Patching {Patches.Length} modules...");
        for (int i = 0; i < Patches.Length; i++)
        {
            var patch = Patches[i];
            try
            {
                harmony.PatchAll(patch);
                Log.LogInfo($"  [{i + 1}/{Patches.Length}] {patch.Name} OK");
            }
            catch (Exception ex)
            {
                Log.LogFatal($"  [{i + 1}/{Patches.Length}] {patch.Name} FAILED: {ex.Message}");
                PatchedException = ex;
            }
        }
    }
}
