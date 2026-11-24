# MystiaLib

与 MetaMystia 业务无关的基础能力，供本模组及其他模组使用。现阶段是模组代码的一部分，源码在 `src/MetaMystia.Mod/MystiaLib`，由 `Plugin.Load` 调用 `Bootstrap.Initialize()`；之后拆分为独立插件。

- 不依赖 MetaMystia 的其他代码，日志用自己的 `Bootstrap.Log`，补丁登记在 `Bootstrap.Patches`（`tools/check.py` 检查）并使用独立的 Harmony 实例。
- 命名空间现为 `MetaMystia.MystiaLib`，拆分时改为 `MystiaLib`。写入磁盘的标识统一用 `Bootstrap.Id`（`MystiaLib`），不随命名空间变化，不得修改。
- 拆分后，独立版须在检测到仍包含 MystiaLib 的旧版 MetaMystia 时停用自身，避免两份同时挂补丁、写同一文件。

## 按键

基线：游戏 RELEASE 4.4.0e、Input System 1.3.0。

```csharp
var map = Keybinds.Register("MetaMystia", "MetaMystia",
    new Keybind("OpenChat", "<Keyboard>/t", () => TextId.HotkeyOpenChat.Get()));
if (map["OpenChat"].WasPressedThisFrame()) { }
```

- 在插件 `Load` 中注册。每个模组一个映射，名为 `MystiaLib.<owner>`；动作和绑定的 ID 由模组 ID 与动作名生成，动作名改动后已保存的绑定失效。
- 映射加入游戏的 `PlayerInput.actions` 后始终启用：原版切换输入模式只开关当前映射，模组键在面板、对话和文本输入期间同样触发，使用方自行判断上下文。
- 面板在启动加载时初始化一次，`EscBindingPannel.OnPanelInitialize` 前缀追加模组行；之后注册的按键可用，但不出现在面板上。所有模组的行同属一个冲突组：改键时清除组内同键的行，不处理原版行，与原版同键时两者同时触发。
- 面板只编辑一个键盘按键，不支持组合键和手柄，不接受 `escape` 及 `InputHelperManager.UnSupportedKeyboardPath` 中的按键（`pageUp`、`home`、`delete` 等）。
- 标题为「类别\n - 名称」，打开面板时按当前语言重新设置。
- 原版「重置键位」同时重置模组按键。

### 保存

模组按键的覆盖保存在 `BepInEx/config/MystiaLib.bindings.json`，原版 `Mystia.binding` 只保留原版条目：`SaveManagement.SaveBindingData` 前缀拆出模组条目，`AdpPlayerInputWrapper.UpdateBindings` 后缀按绑定 ID 补回。本次未加载的模组条目原样保留。

原因：`LoadBindingOverridesFromJson` 遇到资源中不存在的绑定 ID 时抛 `NotImplementedException`。原版文件含模组条目、而启动读取时模组映射不存在（卸载模组、改名），`UniversalGameManager.OnAwake` 抛出，游戏停在加载画面（运行时实测）。

Patch 抛出的异常由 Il2CppInterop 捕获并跳过原方法，保存前缀抛出即丢失本次保存。
