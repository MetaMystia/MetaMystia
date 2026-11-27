using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using Common.CharacterUtility;
using Common.UI;
using DEYU.AdpUISystem.Managers;

using MetaMystia.Multiplayer.Messages;

namespace MetaMystia.UI;

/// <summary>
/// 表情轮盘：按住快捷键时在角色处展开，松开时播放指针方向上的表情；指针停在中心时不播放。
/// </summary>
public static class EmoteWheel
{
    private const float Radius = 190f;
    private const float SlotSize = 80f;
    private const float IconSize = 56f;
    private const float SelectedScale = 1.15f;
    private const float DeadZone = 70f;
    private const float TitleSize = 20f;

    private static readonly Color SlotText = new(0.29f, 0.18f, 0.12f);
    private static readonly Color Unavailable = new(1f, 1f, 1f, 0.35f);

    // 从正上方起顺时针排列。
    private static readonly EmoteType[] Order =
    [
        EmoteType.ExGood, EmoteType.Good, EmoteType.Normal, EmoteType.Bad, EmoteType.ExBad,
        EmoteType.Heart, EmoteType.Heartbreak, EmoteType.Exclaim,
        EmoteType.Jump, EmoteType.Shake, EmoteType.Sparkle, EmoteType.Henshin, EmoteType.Fire, EmoteType.Smoke,
    ];

    private static readonly Image[] _slots = new Image[Order.Length];
    private static readonly TextMeshProUGUI[] _labels = new TextMeshProUGUI[Order.Length];
    private static readonly bool[] _available = new bool[Order.Length];
    private static RectTransform _root;
    private static Image _plate;
    private static TextMeshProUGUI _title;
    private static Sprite _slotSprite;
    private static Sprite _selectedSprite;
    private static CharacterControllerUnit _unit;
    private static int _selected = -1;

    private static bool IsOpen => _root != null && _root.gameObject.activeSelf;

    public static void Update()
    {
        if (!IsOpen)
        {
            if (Hotkeys.EmoteWheel.WasPressedThisFrame() && CanShow() && OverlayUi.EnsureCreated(loadFont: true))
                Open(PlayerManager.Local.GetCharacterUnit());
            return;
        }
        if (_unit == null || !CanShow())
        {
            Close();
            return;
        }
        if (Hotkeys.EmoteWheel.IsPressed())
        {
            Select();
            return;
        }
        var unit = _unit;
        int selected = _selected;
        Close();
        if (selected >= 0 && Emote.Play(unit, Order[selected])) EmoteMessage.Send(Order[selected]);
    }

    // 只有常驻面板（白天、夜间的 HUD）请求 Player 输入模式，其他面板打开时为 UI。
    private static bool CanShow() =>
        !InGameConsole.HasKeyboardFocus
        && GameFlow.LocalScene is Scene.DayScene or Scene.WorkScene
        && !GameFlow.InStory
        && UniversalGameManager.IsInputEnabled
        && AdpUIPanelManager.CurrentInputMode == AdpUIPanelManager.RequestedInputMode.Player;

    private static void Open(CharacterControllerUnit unit)
    {
        if (unit == null) return;
        if (_root == null) Build();
        _unit = unit;
        float extent = 2f * Radius + SelectedScale * SlotSize;
        var size = new Vector2(extent, extent);
        var center = Vector2.zero;
        if (Camera.main is { } camera)
            center = OverlayUi.Root.InverseTransformPoint(camera.WorldToScreenPoint(unit.mainRenderer.bounds.center));
        OverlayUi.Place(_root, new Vector2(0.5f, 0.5f), OverlayUi.Clamp(new Vector2(0.5f, 0.5f), center, size), size);
        for (int i = 0; i < Order.Length; i++)
        {
            _available[i] = Emote.IsAvailable(Order[i]);
            _slots[i].color = _available[i] ? Color.white : Unavailable;
            if (_labels[i] != null) _labels[i].text = Title(Order[i]);
        }
        Highlight(-1);
        _root.gameObject.SetActive(true);
    }

    private static void Close()
    {
        Highlight(-1);
        _root.gameObject.SetActive(false);
        _unit = null;
    }

    private static void Select()
    {
        int selected = -1;
        var pointer = Mouse.current is { } mouse ? (Vector2)_root.InverseTransformPoint(mouse.position.ReadValue()) : Vector2.zero;
        if (pointer.magnitude >= DeadZone)
        {
            float angle = (Mathf.Atan2(pointer.x, pointer.y) * Mathf.Rad2Deg + 360f) % 360f;
            int index = Mathf.RoundToInt(angle * Order.Length / 360f) % Order.Length;
            if (_available[index]) selected = index;
        }
        if (selected != _selected) Highlight(selected);
    }

    private static void Highlight(int selected)
    {
        if (_selected >= 0)
        {
            _slots[_selected].sprite = _slotSprite;
            _slots[_selected].rectTransform.localScale = Vector3.one;
        }
        _selected = selected;
        _plate.gameObject.SetActive(selected >= 0);
        if (selected < 0) return;
        _slots[selected].sprite = _selectedSprite;
        _slots[selected].rectTransform.localScale = Vector3.one * SelectedScale;
        _title.text = Title(Order[selected]);
        var measured = OverlayUi.Measure(_title.text, TitleSize, 1000f, wrap: false);
        _plate.rectTransform.sizeDelta = new Vector2(measured.x + 28f, measured.y + 12f);
    }

    private static void Build()
    {
        _slotSprite = OverlayUi.AtlasSprite("Common_Controls_Atlas", "Button_ItemBG");
        _selectedSprite = OverlayUi.AtlasSprite("Common_Controls_Atlas", "Button_ItemBG_Selected");
        _root = OverlayUi.Node("EmoteWheel", OverlayUi.Root);
        var center = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < Order.Length; i++)
        {
            float angle = i * 2f * Mathf.PI / Order.Length;
            var slot = OverlayUi.Node($"Slot_{Order[i]}", _root).gameObject.AddComponent<Image>();
            slot.sprite = _slotSprite;
            slot.raycastTarget = false;
            OverlayUi.Place(slot.rectTransform, center, new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * Radius, new Vector2(SlotSize, SlotSize));
            _slots[i] = slot;
            if (Emote.GetIcon(Order[i]) is { } sprite)
            {
                var icon = OverlayUi.Node("Icon", slot.rectTransform).gameObject.AddComponent<Image>();
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                OverlayUi.Place(icon.rectTransform, center, Vector2.zero, new Vector2(IconSize, IconSize));
                continue;
            }
            var label = OverlayUi.Label("Label", slot.rectTransform, 17f, SlotText);
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            OverlayUi.Fill(label.rectTransform, 0, 0, 0, 0);
            _labels[i] = label;
        }
        _plate = OverlayUi.Box("Plate", _root, OverlayUi.Surface, 8f);
        OverlayUi.Place(_plate.rectTransform, center, Vector2.zero, Vector2.zero);
        _title = OverlayUi.Label("Title", _plate.rectTransform, TitleSize, OverlayUi.Text);
        _title.alignment = TextAlignmentOptions.Center;
        _title.enableWordWrapping = false;
        OverlayUi.Fill(_title.rectTransform, 0, 0, 0, 0);
        _root.gameObject.SetActive(false);
    }

    private static string Title(EmoteType emote) => (emote switch
    {
        EmoteType.Fire => TextId.EmoteNameFire,
        EmoteType.Shake => TextId.EmoteNameShake,
        EmoteType.Jump => TextId.EmoteNameJump,
        EmoteType.Henshin => TextId.EmoteNameHenshin,
        EmoteType.Sparkle => TextId.EmoteNameSparkle,
        EmoteType.Smoke => TextId.EmoteNameSmoke,
        EmoteType.ExGood => TextId.EmoteNameExGood,
        EmoteType.Good => TextId.EmoteNameGood,
        EmoteType.Normal => TextId.EmoteNameNormal,
        EmoteType.Bad => TextId.EmoteNameBad,
        EmoteType.ExBad => TextId.EmoteNameExBad,
        EmoteType.Heart => TextId.EmoteNameHeart,
        EmoteType.Heartbreak => TextId.EmoteNameHeartbreak,
        _ => TextId.EmoteNameExclaim,
    }).Get();
}
