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

## 存档数据

基线：游戏 RELEASE 4.4.0e。

```csharp
sealed class Progress
{
    public int Served { get; set; }
    public HashSet<string> Claimed { get; set; } = [];
}

static readonly SaveEntry<Progress> Data = Saves.Register<Progress>("MetaMystia", "progress");

if (!Data.Value.Claimed.Contains("letter"))
    Data.Update(p => p.Claimed.Add("letter"));
```

- 数据跟随当前存档：换存档、开新游戏后 `Value` 换成新对象，不要留存旧对象；回溯日期时保留（源码推断）。回到主菜单后仍是上一个存档的数据。
- 修改须经 `Update`，直接改 `Value` 的内容不写入。`Update` 只写入当前存档的内存数据，落盘由原版存档完成，不存档就重读则丢弃。每次 `Update` 序列化整项数据，不要每帧调用。
- `Update` 返回是否写入。还没读过档、记录损坏或重复、版本高于注册版本时 `Value` 为默认值，`Update` 只改内存并返回 `false`，原记录保留。
- 修改函数、迁移函数抛出的异常交给调用方，不写入。
- 只在主线程使用。只存 KB 级数据：启动时原版解析全部存档，每次存档复制整个目录（源码推断）。
- 不含联机同步与肉鸽模式的独立存档。

### 数据结构

数据类型须序列化为 JSON 对象，属性名以 camelCase 写入。引用游戏内容用标签或 ID，日期用游戏内天数。

- 新增字段不升版本，旧存档中取初始值。旧版模组修改同版本的数据时会丢掉它不认识的字段；不能接受则升版本。
- 改名、改含义、删字段时递增 `version` 并提供 `migrate`。迁移结果在下一次 `Update` 时写入；没有 `migrate` 时按当前结构直接读取。

```csharp
Saves.Register<Progress>("MetaMystia", "progress", 2, (from, data) =>
{
    var old = data["served"];
    data.Remove("served");
    data["totalServed"] = old;
    return data;
});
```

### 存档格式

```json
"schedulerPartialDLC": {
  "MystiaLib": {
    "dlcSaveDate": 0,
    "scheduledEvents": {},
    "scheduledNews": {},
    "scheduledNewsReplaceContents": {},
    "allTrackingMissions": {},
    "finishedEvents": ["{\"id\":\"MetaMystia/progress\",\"version\":1,\"data\":{\"served\":3,\"claimed\":[]}}"],
    "finishedMissions": []
  }
}
```

- 借用未启用 DLC 的存档数据：读档时不在 `ActiveDLCLabel` 中的键进入 `RunTimePlayerData.NotLoadedDLCSchedulerSaveData`，存档时原样写回。该键不得启用。普通的新增 JSON 字段会在下一次存档时丢失，所以没有采用。
- `finishedEvents` 每个元素一条记录，`id` 为 `<owner>/<name>`。只替换目标记录，其他记录逐字保留，包括无法识别的。
- 原版每次读档都换成新字典，据此判断换了存档，不需要 Hook。写入时复制为新字典再替换：原版在线程池中生成存档。

运行时实测：含中文、转义、64 KB 字符串和非 JSON 文本的记录，经存档、移除模组后原版读档、推进一天、存档、重启再读存、装回模组，逐字一致，键没有重复。
