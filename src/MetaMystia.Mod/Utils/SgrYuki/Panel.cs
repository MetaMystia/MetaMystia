using System.Linq;

namespace SgrYuki.Utils;

[MetaMystia.AutoLog]
public static partial class Panel
{
    public static Il2CppSystem.Collections.Generic.Stack<Il2CppSystem.Collections.Generic.Stack<DEYU.AdpUISystem.PanelCollection.UIPanelImpl>> PanelStack => DEYU.AdpUISystem.Managers.AdpUIPanelManager.Instance?.m_PanelStack;
    public static Il2CppSystem.Collections.Generic.Stack<DEYU.AdpUISystem.PanelCollection.UIPanelImpl> TopPanelStack => PanelStack != null && PanelStack.Count > 0 ? PanelStack.Peek() : null;
    public static DEYU.AdpUISystem.PanelCollection.UIPanelImpl TopPanel => TopPanelStack != null && TopPanelStack.Count > 0 ? TopPanelStack.Peek() : null;
    public static string TopPanelName => TopPanel?.ControlledPanelName;
    public static Common.DialogUtility.DialogPannel TopPanelAsDialog => TopPanel?.ControlledPanel as Common.DialogUtility.DialogPannel;
    public static void CloseTopDialogPanel() => TopPanelAsDialog?.InterruptDialog(new UnityEngine.InputSystem.InputAction.CallbackContext());

    private static Il2CppSystem.Action<DEYU.AdpUISystem.PanelCollection.FadeType> _dialog_panel_close_callback;


    public static void CloseTopPanel()
    {
        void OnDialogPanelFadeFinished(DEYU.AdpUISystem.PanelCollection.FadeType _, Common.DialogUtility.DialogPannel currentPanel)
        {
            if (TopPanelAsDialog != null)
            {
                CloseTopDialogPanel();
                return;
            }
            currentPanel.remove_OnPanelOpenCloseFadeFinishCallback(_dialog_panel_close_callback);
            Log.Warning("All dialog panels closed, callback removed.");
            CloseActivePanelsBeforeSceneTransit();
        }
        ;

        void RegisterDialogPanelCloseCallback(Common.DialogUtility.DialogPannel currentPanel)
        {
            System.Action<DEYU.AdpUISystem.PanelCollection.FadeType> managed = _ => OnDialogPanelFadeFinished(_, currentPanel);
            _dialog_panel_close_callback = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action<DEYU.AdpUISystem.PanelCollection.FadeType>>(managed);
            TopPanelAsDialog.add_OnPanelOpenCloseFadeFinishCallback(_dialog_panel_close_callback);
        }
        ;

        if (TopPanelAsDialog != null)
        {
            Log.Warning($"Closed dialog panel {TopPanelName}");
            var currentPanel = TopPanelAsDialog;
            CloseTopDialogPanel();
            RegisterDialogPanelCloseCallback(currentPanel);     // Interrupt all dialog panels
        }
        else
        {
            if ("DialogPannel(Clone)".Equals(TopPanelName))
            {
                Log.Error($"TopPanelName is {TopPanelName}, will try convert then close");
                CloseTopDialogPanel();
            }
            else if ("WorkSceneSustainedPannel(Clone)".Equals(TopPanelName))
            {
                Log.Warning($"Will not close {TopPanelName}");
            }
            else
            {
                if (TopPanel?.ControlledPanel?.IsPanelOpened is true)
                {
                    Log.Warning($"Closed regular panel {TopPanelName}");
                    TopPanel?.ControlledPanel?.ClosePanel();
                }
                else
                {
                    Log.Error($"regular panel {TopPanelName} is not opened!");
                }
            }
        }
    }



    /// <summary>
    /// API Common.UI.GeneralSustainedPannel.CurrentActiveSustainedPannel.CloseActivePannel()
    /// Can close:
    /// DayScene_FastTravelPannel(Clone)
    /// NoteBook_ProfilePannel(Clone)
    /// NoteBook_ProfilePannel_Page#1(Clone)
    /// NoteBook_ProfilePannel_Page#2(Clone)
    /// NoteBook_ProfilePannel_Page#3(Clone)
    /// NoteBook_RecipePannel(Clone)
    /// NoteBook_NewsPannel(Clone)
    /// NoteBook_MissionPannel(Clone)
    /// NoteBook_NewsPanelSelector(Clone)
    /// NoteBook_AlbumPannel(Clone)
    /// NoteBook_AlbumGuestSubSubPannel(Clone)
    /// NoteBook_AlbumItemSubPannel(Clone)
    /// Storage_MainPannel(Clone)
    /// Storage_OtherPannel(Clone)
    ///
    /// Cannot close and will throw exception, then game will stuck:
    /// DayScene_FastTravelConfirmPannel(Clone)
    /// FlattenedFilterPanel(Clone)
    /// EscMenu_LoadSubPannel(Clone)
    /// IzakayaDetailPanel(Clone) (not try yet)
    /// IzakayaConfigRemindSubPanel(Clone)
    /// IzakayaPresetPannel(Clone)
    /// IzakayaConfigPannelSubPannel(Clone)
    /// </summary>
    public static void CloseActivePanelsBeforeSceneTransit()
    {
        string[] PanelToBeClosed = ["NoteBook", "Storage", "DayScene_FastTravelPannel", "EscMenu"];
        string[] PanelMustBeClosedFirst = ["DayScene_FastTravelConfirmPannel(Clone)", "FlattenedFilterPanel(Clone)", "EscMenu_LoadSubPannel(Clone)",
            "DialogPannel(Clone)",
            "IzakayaDetailPanel(Clone)", "IzakayaConfigRemindSubPanel(Clone)", "IzakayaPresetPannel(Clone), IzakayaConfigPannelSubPannel(Clone)",
            "WorkSceneQTEPannel(Clone)",
            // "WorkSceneServePannel(Clone)", "WorkSceneTrayPannel(Clone)"
            ];

        if (TopPanel == null) return;
        var count = TopPanelStack.Count;
        for (int i = 0; i < 2; i++)
        {
            if (PanelMustBeClosedFirst.Any((panel) => panel.Equals(TopPanelName)))
            {
                CloseTopPanel();
            }
            else
            {
                break;
            }
        }

        if (TopPanel == null) return;
        if (PanelToBeClosed.Any((panel) => TopPanelName.Contains(panel)))
        {
            Log.Warning($"Closed panel {TopPanelName} in SustainedPannel");
            Common.UI.GeneralSustainedPannel.CurrentActiveSustainedPannel.CloseActivePannel();
        }
    }

    public static void CloseTargetPannelIfOnTop(string name)
    {
        if (name.Equals(TopPanelName))   // "IzakayaConfigRemindSubPanel(Clone)"
        {
            CloseTopPanel();
        }
    }

    public static void ClosePanelUntil(string name, string[] exclude)
    {
        while (true)
        {
            if (TopPanel == null) return;
            if (!name.Equals(TopPanelName) && !exclude.Any((ex) => ex.Equals(name)))
            {
                CloseTopPanel();
            }
            else
            {
                return;
            }
        }
        // GameData.Core.Collections.CharacterUtility.DataBaseCharacter.port.
        // GameData.Core.Collections.CharacterUtility.DataBaseCharacter.m_PlayerActiveSkinPortrayal;
        // GameData.RunTime.Common.RunTimeAlbum.ChangePlayerSkin(1);   // changed skin, but not album. Parameter: skin id
        // GameData.Core.Collections.CharacterUtility.DataBaseCharacter.LoadActivePlayerSkinAsync(GameData.RunTime.Common.RunTimeStorage.GetAllClothes()[1]);  // changed album, not skin
        // GameData.Core.Collections.DaySceneUtility.DataBaseDay.allNPCs["Kosuzu"].GetVisual();
        // MetaMystia.MystiaManager.Instance.GetCharacterUnit(false).UpdateCharacterSprite(GameData.Core.Collections.DaySceneUtility.DataBaseDay.allNPCs["Kosuzu"].GetVisual());
    }
}
