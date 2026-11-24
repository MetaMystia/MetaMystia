# 原生按键设置接入

基线：游戏 RELEASE 4.4.0e、Input System 1.3.0、模组 0.31.4。目标是一个独立插件，供 MetaMystia 和其他模组向原版按键设置面板注册按键。尚未实现。

## 原版结构

- `UIPanelManager`（`DontDestroyOnLoad`）上的 `PlayerInput` 持有资源 `UnifiedGameInputActions`，与 `UIInputModule.actionsAsset` 是同一对象，开场时已存在。映射只有 `UI`、`DayScene_Player`、`NightScene_Player`，无控制方案，不限制设备。运行时实测。
- 输入模式切换调用 `PlayerInput.SwitchCurrentActionMap`，只停用旧的当前映射、启用新的，其他映射的启用状态不变。运行时实测。
- 面板 `EscBindingPannel` 的行来自 `inputBindingDatas`（标题键 + 若干 `InputActionReference` 与绑定索引），冲突范围来自 `groups`，原版 35 行、3 组。面板在启动加载时预缓冲并执行一次 `OnPanelInitialize`，其中调用 `DrawBindingElements` 生成行；之后随场景迁移，不再初始化。`OnPanelOpen` 时按标题键重新取文本。运行时实测。
- 改键子面板 `EscBindingSubPannel` 只接受键盘，排除 `escape`、鼠标右键和 `InputHelperManager.UnSupportedKeyboardPath`（`OEM1`–`OEM5`、`IMESelected`、`leftMeta`、`rightMeta`、`contextMenu`、`pageUp`、`pageDown`、`home`、`end`、`insert`、`delete`、`numLock`、`printScreen`、`scrollLock`、`pause`、`numpadEquals`、`anyKey`）。捕获期间按下的键被抑制，不触发其他动作。完成后，同组内绑定到同一按键的其他行被清空，组外不处理。运行时实测。
- 保存：`AdpPlayerInputWrapper.SaveBinding()` 把整个资源的覆盖序列化后交给 `SaveManagement.SaveBindingData`，写入存档目录的 `Mystia.binding`；没有覆盖时为空字符串。读取：`UpdateBindings()` 调用 `LoadBindingOverridesFromJson(json, removeExisting: true)`，在 `UniversalGameManager.OnAwake` 和 `DrawBindingElements` 中执行。「重置键位」清空整个资源的覆盖后保存并重新读取。运行时实测。

## 不能写入原版绑定文件

`LoadBindingOverridesFromJson` 遇到资源中不存在的绑定 ID 时抛 `NotImplementedException`（Input System 1.3.0 的未实现分支）。`Mystia.binding` 含模组条目、而启动读取时模组映射不存在（卸载模组、改名或注册晚于读取）时，`UniversalGameManager.OnAwake` 抛出，游戏停在加载画面，按键面板初始化也会抛出；删除该文件后恢复。运行时实测。

因此模组绑定的覆盖与原版文件分开保存，原版文件只保留原版条目。

## 方案

各环节均在游戏内用调试器挂补丁验证，运行时实测，除注明外。

| 环节 | 做法 |
| --- | --- |
| 映射 | 每个模组一个映射，名称使用保留前缀加模组 ID。用 `InputActionMap.FromJson` 创建，动作和绑定 ID 由模组 ID 与动作名确定性生成。 |
| 加入资源 | 记录启用的映射，`asset.Disable()`，`AddActionMap`，任一映射 `ResolveBindings()`，恢复原有启用状态，再启用模组映射。原版动作解析不变；模组映射在白天、备菜、营业和 UI 模式下保持启用并响应按键。 |
| 面板行 | `EscBindingPannel.OnPanelInitialize` 前缀：向 `inputBindingDatas` 追加行（动作引用指向资源中的模组动作，绑定索引 `-1`），向 `groups` 追加一组，由原版一并生成，按键图标正常（含 `/`）。前缀只在启动时执行一次，此后注册的按键改为向已初始化面板追加：以独立列表调用 `DrawBindingElements`，再合入 `AllGameObjectInstances`、`inputBindingDatas`、`groups`。 |
| 标题 | `MultiLanguageTextMeshTitle.SetTitlePhrases(键, 文本)`，格式同原版「类别\n - 名称」。语言切换会重建标题表，在 `OnPanelOpen` 前缀中按当前语言重新设置（语言切换未实测）。 |
| 保存 | `SaveManagement.SaveBindingData` 前缀：按动作名前缀拆分 JSON，模组条目写入插件自己的文件，原版条目传给原方法；没有模组条目时不改动参数。 |
| 读取 | `AdpPlayerInputWrapper.UpdateBindings` 后缀：读取插件文件，按绑定 ID 逐条 `ApplyBindingOverride`，跳过不存在的 ID。映射在启动读取之后注册时，注册时应用一次。 |
| 重置 | 原版「重置键位」同时清空模组覆盖，插件文件随保存清空。 |

已验证：开场注册映射和补丁后，启动时面板生成模组行，插件文件中的覆盖得到恢复；拆分保存后重启，原版覆盖正常读取，未注册模组映射时启动无异常。上述挂点在 ICF 折叠表中均无折叠。

Patch 抛出的异常由 Il2CppInterop 捕获并记录（`During invoking native->managed trampoline`），原方法被跳过、调用方继续。保存前缀抛出即丢失本次保存，不得抛出。

## 使用方的约束

- 模组映射始终启用，在面板、对话和文本输入期间都会触发，上下文由使用方判断。
- 面板只编辑绑定索引 0 的单个键盘按键，不支持组合键和手柄。其他绑定（如小键盘回车）只能作为不可编辑的附加绑定。
- 默认键避开上述不支持的按键。
- 与原版动作同键时两者同时触发，面板不清除任一方。
- 现有快捷键用旧版 `Input.GetKeyDown`，迁移后改读 `InputAction`。
