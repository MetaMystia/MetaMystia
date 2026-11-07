# 原生按键设置接入说明

适用于游戏 RELEASE 4.4.0e、Input System 1.3.0。接入时复用原版按键设置、输入捕获和绑定保存流程。

## 输入映射

将模组动作放入独立的 `InputActionMap`，加入游戏的 `PlayerInput.actions`。为动作和绑定分配固定 ID，使保存的绑定覆盖能够在重启后匹配。

优先在输入系统初始化期间注册映射。需要在运行中追加时，按以下顺序处理：

1. 记录原有映射的启用状态，停用资源中的全部映射。
2. 创建尚未启用的新映射，调用 `AddActionMap` 加入资源。
3. 从原有映射调用 `ResolveBindings()`，重新解析整个资源的共享绑定状态。
4. 恢复原有映射的启用状态，再启用模组映射。

Input System 1.3.0 的 `AddActionMap` 只追加映射、标记资源并设置归属，不负责刷新已有的共享输入状态。不得仅追加映射后直接启用，也不要先独立启用新映射再将其迁入游戏资源。

参考：[添加映射实现](https://github.com/Unity-Technologies/InputSystem/blob/1.3.0/Packages/com.unity.inputsystem/InputSystem/Actions/InputActionSetupExtensions.cs)、[绑定解析实现](https://github.com/Unity-Technologies/InputSystem/blob/1.3.0/Packages/com.unity.inputsystem/InputSystem/Actions/InputActionMap.cs)。

## 设置面板

| 入口 | 接入方式 |
| --- | --- |
| `EscBindingPannel.inputBindingDatas` | 追加设置行，配置标题、动作引用和绑定索引。 |
| `EscBindingPannel.groups` | 按动作能否同时生效划分冲突检查范围。独立模组组只检查组内冲突，不覆盖原版动作。 |
| `EscBindingPannel.DrawBindingElements` | 创建控件，并将动作引用归一到游戏的 `PlayerInput.actions`。 |
| `EscBindingSubPannel` | 复用原版按键捕获、取消和冲突处理流程。 |
| `MultiLanguageTextMeshTitle.SetTitlePhrases` | 注册设置行标题，并在语言数据重建后重新注册。 |

`DrawBindingElements` 使用 `ObjectSpawnManager.SpawnPooled`。向已初始化的面板追加少量设置行时，应传入独立的控件列表，生成后再合入面板的 `AllGameObjectInstances`。直接传入原有控件池会复用前面的控件，并隐藏未使用的剩余控件。

同步维护 `inputBindingDatas`、`groups`、`instances` 和逻辑导航组，避免重复注册。控件生成后，调用 `DynamicDeviceActionProvider.Instance.UpdateInputDevice()` 刷新按键显示。

## 模组快捷键

在 `PlayerListPanel.Update` 与 `InGameConsole.Update` 中读取对应输入动作，替换硬编码的快捷键判断。保留控制台内部发送、关闭等输入处理。

- 玩家列表默认使用回车；需明确数字键盘回车是否作为额外绑定，以及改键时是否同步调整。
- 命令输入默认使用斜杠；读取已有配置时，应明确旧配置迁移与原版绑定覆盖的优先级。
- 按键设置及改键子面板打开时，屏蔽模组快捷键，避免捕获新键时同时触发功能。
- 保留玩家列表的联机显示条件，并处理控制台打开时的输入互斥。
- 原版输入模式切换会切换动作映射；需维护模组映射的启用状态，避免切图后失效或在不适用的界面触发。

## 保存与恢复

复用 `AdpPlayerInputWrapper.SaveBinding()` 和 `UpdateBindings()`，通过 `UniversalGameManager` 初始化的游戏回调保存、加载绑定 JSON。加载覆盖前应先注册具有固定 ID 的模组动作与绑定。

将模组绑定纳入原版恢复默认值流程，并分别检查改键、取消、冲突、恢复默认值、跨场景和重启后的行为。存在 JSON 保存接口不能代替完整的保存与恢复检查。

## 动态调试

持续使用的类型、委托和 Harmony 补丁应放在 `/script` 会话中，不使用可卸载的 `/exec` 承载长期回调。通过 `DelegateSupport.ConvertDelegate` 转换原生回调，并保留托管委托引用。

每次只执行一个可核验动作，执行后读取映射、绑定及面板状态。调试 Token 仅作为请求参数，不写入脚本、文档或输出记录。
