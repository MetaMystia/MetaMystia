using System;
using HarmonyLib;

namespace MetaMystia.Patch;

[AutoLog]
public static partial class PatchRegistry
{
    public static readonly Type[] Patches = [
        // SceneManager Patches
        typeof(MainSceneManagerPatch),
        typeof(DaySceneManagerPatch),
        typeof(NightSceneManagerPatch),
        typeof(PrepNightSceneManagerPatch),
        typeof(ResultSceneManagerPatch),
        typeof(StaffSceneManagerPatch),
        typeof(UniversalGameManagerPatch),

        // DayScene Patches
        typeof(StatusTrackerPatch),
        typeof(CharacterControllerUnitPatch),
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
