using UnityEngine.InputSystem;

using MetaMystia.MystiaLib.Keybinding;

namespace MetaMystia.UI;

/// <summary>模组快捷键，在原版按键设置中修改。动作名决定保存的绑定 ID，不得修改。</summary>
public static class Hotkeys
{
    public static InputAction OpenChat { get; private set; }
    public static InputAction OpenCommand { get; private set; }
    public static InputAction PlayerList { get; private set; }
    public static InputAction ToggleStatus { get; private set; }
    public static InputAction LogBreak { get; private set; }

    public static void Register()
    {
        var map = Keybinds.Register("MetaMystia", MyPluginInfo.PLUGIN_NAME,
            new Keybind("OpenChat", "<Keyboard>/t", () => TextId.HotkeyOpenChat.Get()),
            new Keybind("OpenCommand", "<Keyboard>/slash", () => TextId.HotkeyOpenCommand.Get()),
            new Keybind("PlayerList", "<Keyboard>/enter", () => TextId.HotkeyPlayerList.Get()),
            new Keybind("ToggleStatus", "<Keyboard>/backslash", () => TextId.HotkeyToggleStatus.Get()),
            new Keybind("LogBreak", "<Keyboard>/rightShift", () => TextId.HotkeyLogBreak.Get()));
        OpenChat = map["OpenChat"];
        OpenCommand = map["OpenCommand"];
        PlayerList = map["PlayerList"];
        ToggleStatus = map["ToggleStatus"];
        LogBreak = map["LogBreak"];
    }
}
