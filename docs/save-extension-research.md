# 原版存档中的模组数据区调研

## 结论

目标是把模组数据放进游戏原有存档：安装模组时能够读写；卸载模组后原版仍能读档，且再次保存后尽量保留数据，供重新安装模组后恢复。

**直接增加普通 JSON 字段，不能满足完整目标。推荐继续验证“未加载 DLC 的 Scheduler 数据”作为承载位置。**

| 方案 | 无模组读档 | 无模组再次保存 | 判断 |
| --- | --- | --- | --- |
| 新增普通字段，例如根节点 `metaMystia` | 默认跳过未知字段 | 不会自动写回 | 不满足保留要求 |
| 模组拦截读写，另外保存未知字段 | 有模组时可实现 | 卸载后拦截失效 | 仍不能解决原版重存丢失 |
| 在 `schedulerPartialDLC` 增加专用条目，使用已有字符串字段装数据 | 未启用的键进入未加载数据区 | 保存入口会把该数据区带回新存档 | 最值得验证的候选 |
| 独立附属文件 | 不影响原版解析 | 需另管存档对应、复制和同步 | 不符合“数据在原存档内部”的首选目标 |

以上是静态分析结论，不是游戏内实测。普通未知字段的结论限于合法、类型正确、合理大小的 JSON；损坏文件、特殊 JSON 元数据和异常深度不在此保证内。

## 版本与证据范围

- 主要依据：用户指定的 **13337 IDA 数据库，Release4.4.0e**，以及对应目录中的 `dump.cs`、`script.json`、`stringliteral.json`。
- IDA 记录的输入 MD5：`6c5ab054446859f0beb87e1baa0ae744`，已与配套目录 `origin/GameAssembly.dll` 核对一致。目录根部的同名 DLL 哈希不同，本报告不把它当作同一二进制。
- 当前存档版本字符串为 `BetaV102`，由新版 `StringLiteral_15677` 确认；它与游戏发行版本是两回事。
- 旧版逆向仓库仅用于定位类型和入口，不将其地址、函数体或异常处理移植为新版事实。本文下列地址全部来自新版。
- 调研未修改 IDA 数据库或玩家存档，也未启动游戏。后续实现及格式检查见 [MetaLib 存储与按键](metalib-storage.md)。

本机文件位置与原始分析结果索引见不提交的配套文件 `save-extension-research.local.md`。

## 为什么未知字段读得进，却留不住

新版读取入口 `SaveManagement.GenerateSaveData`（`0x1805956E0`）先执行预处理正则，再调用：

```csharp
JsonConvert.DeserializeObject<PlayerSaveFile>(text, m_JsonSerializerSettings)
```

这是行为摘要，不是可直接使用的还原源码。

`SaveManagement..cctor`（`0x1805DC700`）只给这组设置指定 `NullValueHandling.Ignore`，没有启用未知成员报错。游戏所带 `JsonSerializer` 的默认 `MissingMemberHandling` 为 `Ignore`。

`JsonSerializerInternalReader.PopulateObject`（`0x1822D6C50`）找不到对应成员时，只有 `MissingMemberHandling.Error` 才进入报错分支；默认转入 `SetExtensionData`（`0x1822D9C80`）。后者没有 `ExtensionDataSetter` 时直接 `reader.Skip()`。

配套 `PlayerSaveFile` 定义没有 `JsonExtensionData` 成员或任意 JSON 容器。写入端 `GenerateSaveString(PlayerSaveFile, Formatting)`（`0x180595D20`）序列化固定结构，不合并原文件的未知内容。

因此，普通额外字段在读取时通常不会阻止解析，但也没有进入游戏存档对象。**单纯读档不会据此改写磁盘文件；下一次覆盖保存时，额外字段才从新文件中消失。**

## 候选位置：未加载的 Scheduler 条目

存档已有以下字段：

```csharp
Dictionary<string, PlayerSaveFile.DLCSchedulerSaveData> schedulerPartialDLC;
```

这意味着可以增加一个字典键，但键下面的值仍须符合游戏认识的 `DLCSchedulerSaveData` 结构。不能在值里面再随意新增一个 `payload` 字段，并期待原版保留它。

### 读取路径

`LoadPlayerData`（`0x180597F00`）从当前游戏资料的 `ActiveDLCLabel` 建立集合，筛选 `schedulerPartialDLC`：

```text
当前启用的键 → RunTimeScheduler.Initialize
其余条目     → RunTimePlayerData.NotLoadedDLCSchedulerSaveData
```

筛选函数 `<LoadPlayerData>b__0`（`0x1805F6890`）只执行 `currentActiveDLCs.Contains(x.Key)`。未选中条目通过 `Except` 和 `ToDictionary` 整体进入未加载数据区。

这里没有对未启用 Scheduler 条目的字符串逐个检查事件是否存在。物品、图鉴、白天数据走另一套 `ExtractValid` 流程，不能将 Scheduler 的结论推广到它们。

### 再次保存路径

`GenerateCurrentPlayerSaveData`（`0x180593580`）把 `NotLoadedDLCSchedulerSaveData` 传给 `PlayerSaveFile` 构造函数（`0x1806087C0`）。构造函数先调用 `DeepCloneS` 保存这些条目，再执行当前运行数据的 DLC 分类。

`RunTimeSchedulerSaveDataPartial.Classify`（`0x1806373D0`）把当前运行中的事件、任务等加入字典。如果新分类的键已存在，会给新条目加数字后缀；末尾只把 `CORE` 条目移回主结构并移除 `CORE` 键。没有发现该函数遍历删除其他未加载条目的逻辑。

`DLCSchedulerSaveData.Clone`（`0x1805FC760`）把各字段交给构造函数。字符串数组由 `RegularClone` 的共享体（`0x180D36710`）调用 `ToArray` 复制，没有按事件名称过滤。

保存工作函数（`0x1805F27C0`）执行生成存档、序列化、`FileSystemHandle.WriteFile`，之后复制文件到备份目录。候选数据随这份存档进入原有流程，不需要写完文件后再追加。

### 字典克隆：汇编补充证据

按用户要求，直接审计 `0x180D36170` 的完整汇编。IDA 函数范围为 `0x180D36170–0x180D36450`（末端不含，含对齐，共 `0x2E0` 字节），没有其他函数块。结合新版元数据，其在 Scheduler 调用处的行为可概括为：

```csharp
// 行为摘要，不是恢复的原始源码。
return source == null
    ? null
    : source.ToDictionary(x => x.Key, x => x.Value.Clone());
```

| 汇编位置 | 核实的行为 |
| --- | --- |
| `0x180D36181`、`0x180D36433` | 输入为 null 时返回 null |
| `0x180D361D0–0x180D362C8` | 获取或创建、缓存键选择委托 |
| `0x180D3630E–0x180D363FC` | 获取或创建、缓存值选择委托 |
| `0x180D36401–0x180D3642F` | 将原字典及两个委托传入泛型上下文的第 7 项方法，并尾调用；配套 RGCTX 定义对应 `Enumerable.ToDictionary` |

该地址被多个泛型方法共用，IDA 显示的 `DeepClone<object, object, object>` 名称不能直接代表当前调用。`PlayerSaveFile` 构造函数传入的 MethodInfo 明确是 `DeepCloneS<string, DLCSchedulerSaveData>`；`dump.cs` 也把该实例映射到此地址。

针对这个实例继续核对两个选择器：

- 键选择器 `<DeepCloneS>b__19_0` 在 `0x180417E50`，只有 `mov rax, [rdx]; ret`，取键值对的键，不检查内容。其 IDA 名称同样因代码共用而显示为其他业务函数。
- 值选择器 `<DeepCloneS>b__19_1` 在 `0x180E38820`，取得 `x.Value` 后调用 `DeepCloneS<DLCSchedulerSaveData>`（`0x180D36120`），后者直接调用 `DLCSchedulerSaveData.Clone()`。

因此，**字典克隆这一环没有未知键过滤，也没有字符串内容过滤**。此前该共享体无法反编译造成的静态证据缺口已补齐；这不替代完整存档往返实测。

反编译失败位置 `0x180D362C8` 调用的 `sub_18035CAB0` 被 IDA 标成了九参数业务构造函数，但其实际沿两个跳转到 `0x180397AF0`：按传入地址计算页索引并原子设置位图，符合 GC 写屏障行为，不读取业务构造参数。错误函数原型很可能干扰反编译；本次没有修改 IDA 类型，也未通过修复后重试确认因果。

### 建议的数据形态

专用键为 `MetaLib`，借用其 `finishedEvents` 字符串数组，每个字符串保存一个模块的 `module`、`version` 和 `data`。使用直接 JSON，由外层序列化负责转义，不加 Base64。例如下面是**结构示意，不能拿它替换整份存档**：

```json
{
  "schedulerPartialDLC": {
    "MetaLib": {
      "dlcSaveDate": 123,
      "scheduledEvents": {},
      "scheduledNews": {},
      "scheduledNewsReplaceContents": {},
      "allTrackingMissions": {},
      "finishedEvents": ["{\"module\":\"example.mod\",\"version\":1,\"data\":{\"count\":3}}"],
      "finishedMissions": []
    }
  }
}
```

`123` 仅示意存档日期，不能把这个字段用作模组版本。多个模块共用这一个游戏键，每条记录独立管理版本，不设额外容器层。数组复制没有只保留首项的限制。外层 JSON 的转义使内部引号、换行不以原始 JSON 字段出现；托管检查已覆盖当前预处理正则，游戏内完整往返仍需验证。此格式不提供加密，接口与用例见 [MetaLib 存储与按键](metalib-storage.md)。

这不是游戏官方提供的模组接口，而是利用现有保留行为。字段名称虽叫 `finishedEvents`，但专用键始终未启用时，其内容不会送入当前事件列表。不要把这段字符串直接放进主 `schedulerPartial.finishedEvents` 或运行中的 `RunTimeScheduler.finishedEvents`。

## 其他承载位置的比较

本节仍以 Release4.4.0e 为准。除 Scheduler 外，下面多数位置只完成候选筛选，不能视为已经证明卸载模组后完整往返安全。

| 位置 | 能存什么 | 评价 |
| --- | --- | --- |
| 未启用 Scheduler 条目的 `finishedMissions` | 编码字符串 | 与 `finishedEvents` 一样通过 `RegularClone<string[]>` 复制，是同一方案中的等价备选，没有明显性能优势 |
| `dayScenePartial.trackedSwitch` | 字符串键、布尔值 | 可考虑少量模组开关；进入实际运行状态，隔离性弱于未加载 Scheduler |
| `dayScenePartial.trackedCount` | 字符串键、整数值 | 可考虑少量模组计数；不能直接存任意对象，运行中的清理与修改范围仍需专项审计 |
| `playerPartial.musicChapterStatus` | 字符串键、字符串列表 | 类型适合承载字符串，但属于实际音乐章节状态，业务遍历未完整核实，暂不推荐 |
| `dayScenePartialDLC`、`albumPartialDLC`、`storagePartialDLC` | 地图、NPC、物品等既有结构 | 缺少简单的字符串值容器，且读取有额外处理，不优于 Scheduler |

`RunTimeDayScene.Initialize`（`0x18060FDA0`）把主存档的 `trackedSwitch`、`trackedCount` 带入运行状态；`GenerateSaveData`（`0x18060D7D0`）把这两份字典交给存档构造函数。构造函数（`0x18060CCC0`）使用 `RegularClone`，`Classify`（`0x18060B480`）末尾把它们保留在主存档，未将这两个字段按 DLC 拆分。这里核对的是相关字段路径，不代表完成了两个集合全部业务访问和生命周期的审计。

它们也不是纯数据存放区：`ToggleTrackedSwitch`（`0x180612C20`）写入后会刷新条件组件并触发回调；`trackedCount` 对应运行字段 `trackedSpecialDayNPCInteractCount`。因此，若目的只是保存任意模组对象，仍优先使用未加载 Scheduler 条目，不建议把编码内容塞进布尔或整数字典的键中。

其他 DLC 字典也不能仅凭“未启用”判断完全不参与业务。以白天数据为例，`SaveManagement.ExtractValid<DLCDaySceneSaveData>`（`0x180A54440`）对未启用条目调用元素的 `ExtractValid`（`0x1805FB880`），将有效部分送入已加载集合，其余部分保留。元素函数会检查地图、NPC、家具等资源；它与 Scheduler 按 DLC 键整体分流的行为不同。这不等于无效部分必然删除，但增加了利用和验证成本。

本次 `CheckCoreAllMusicChapterFinished`（`0x18062DA40`）两次反编译失败，未继续还原或据此保证音乐章节容器安全。`SetTrackedCount`、`ToggleTrackedSwitch` 重试后仍带局部变量分配警告，只使用明确可辨识的字典操作和回调调用作为候选评价依据。

## 实现时必须保持的条件

1. 专用键在有模组、无模组时都保持“未启用”。不要注册成实际 DLC，不要加入当前资料的 `ActiveDLCLabel`，也不要为承载数据修改 `allActivatedDLC`。
2. 使用独立命名，避开 `CORE`、官方 DLC 名、`UNDEFINED*`、`ResourceEx*`。不要让事件或任务分类产生这个专用键。
3. 保留原版外层结构和字段类型，未使用的集合写成空集合。任意模组结构放到编码字符串内部。
4. 只替换自己的字典条目，保留其他 DLC 和其他模组的数据。识别不了内部格式时先保留原数据，不能直接覆盖为空。
5. 跟随游戏当前运行数据，避免模组静态缓存串档；加载期间及主菜单拒绝操作。新版日期回溯保留运行数据，加载历史备份则使用备份中的数据。无模组推进剧情期间，模组数据只会被保留，不会跟着剧情自动更新。
6. 保存前固定本次要写的数据。新版保存工作通过 `RunOnThreadPool` 执行，不应在后台序列化期间并发修改同一集合，也不能在那里读取 Unity 对象。

当前项目的 [SchedulerDataRecovery](../src/MetaMystia.Mod/ResourceEx/Registries/SchedulerDataRecovery.cs) 会在特定条件下清理 `UNDEFINED*` / `ResourceEx*`，并恢复、移除精确的 `ResourceEx` 条目。候选键必须与这套流程分开。存储实现位于独立的 [MetaMystia.MetaLib](../src/MetaMystia.MetaLib/Storage/ModSaveData.cs)，主模组尚未接入其业务数据。

### 自动回调时点

新版 `WriteCurrentPlayerDataToSlotAsync`（`0x1805DC330`）初始化并同步启动状态机；其 `MoveNext`（`0x1805F8130`）随后调用 `RunOnThreadPool`。生成存档、JSON 序列化和写文件都位于该后台工作函数（`0x1805F27C0`）。当前数据库对保存入口找到一个直接业务调用点：`ResultSceneSavePannel.<SavePlayerDataImpl>d__18.MoveNext`（`0x180570ED0`），在等待保存结果前调用它；不能据此排除委托或其他模组直接调用。

MetaLib 在保存入口的 Prefix 中检查线程，并在主线程收集模块回调、一次性发布记录；加载回调在 `LoadPlayerData` 的 Postfix 分发，不将解析文件等同于加载游戏。直接调用 `GenerateCurrentPlayerSaveData` 不触发自动回调，F9 会显式先收集。入口反编译重试后仍有局部变量分配警告，只使用明确的状态机调用和匹配元数据；回调 Hook 尚待运行时验证。

## 仍需补齐的验证

- **字典克隆已补查**：`0x180D36170` 仍无法直接反编译，但已按用户要求审计完整汇编，并核对泛型元数据及两个选择器。此环节支持保留候选条目；完整往返尚未实测。
- `GenerateSaveString` 和 `DLCSchedulerSaveData` 构造函数带有 IDA 局部变量分配警告；报告仅使用可辨识调用及配套类型信息，没有据此还原完整 C#。
- 本次未穷尽历史存档升级函数。推荐先针对该版本产生的 `BetaV102` 存档验证，不保证历史迁移、降级或未来游戏更新都保持同样行为。
- 项目引用的 Interop 接口已核对并通过编译，托管格式检查通过。原生字典桥接、游戏内操作及卸载模组后的完整往返仍未实测。

最小验收流程：使用测试存档，带模组写入含中文、数组和嵌套对象的数据；完全卸载模组后启动原版、读档、推进一天并保存；重启原版再读存一次；重新安装模组，检查解码后的数据与最初一致，并检查专用键没有消失或产生后缀副本。之后再覆盖不同槽位、新游戏和回溯场景。

## 关键证据索引

| 新版位置 | 用途 |
| --- | --- |
| `dump.cs:77440`，`PlayerSaveFile` | 根存档结构，无扩展 JSON 字段 |
| `dump.cs:76944`，`DLCSchedulerSaveData` | 可承载的原有字段及类型 |
| `0x1805956E0` / `0x1805DC700` | 读取入口与 JSON 设置 |
| `0x182283E50` / `0x1822D6C50` / `0x1822D9C80` | 默认行为、未知成员分支与跳过逻辑 |
| `0x180597F00` / `0x1805F6890` | 区分启用与未启用 Scheduler 条目 |
| `0x180593580` / `0x1806087C0` | 未加载数据带入下一份存档 |
| `0x1806373D0` | 保存分类、键冲突与 CORE 处理 |
| `0x180D36170` / `0x180417E50` / `0x180E38820` / `0x180D36120` | 字典克隆完整汇编、键和值选择器、值克隆入口 |
| `0x1805FC760` / `0x1805FC7E0` / `0x180D36710` | 元素克隆、字段复制与数组复制 |
| `0x1805F8130` / `0x1805F27C0` | 后台保存、写入及备份流程 |

