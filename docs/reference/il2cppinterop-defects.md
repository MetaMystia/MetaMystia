# Il2CppInterop 缺陷

Il2CppInterop 生成的壳代码可能因类型转换、封送或原生内存布局处理不完整而产生运行时缺陷。相关调用可能正常编译，但在执行时返回错误数据、破坏内存或崩溃。

不得将普通逻辑错误默认归因于 Il2CppInterop。必须先按 [`il2cpp-interop`](../rules/il2cpp-interop.md) 的排查顺序确认版本、签名、时机、生命周期和类型边界。只有缺陷可复现且原因明确时，才允许增加低层绕过；绕过必须限定到具体类型和签名。

绕过用的代码放在 `src/MetaMystia.Mod/Interop/`，一个缺陷一个文件；与业务绑定的单行规避留在调用处。

## `Interop/BoxedValueDictionary.cs`

`SetBoxedValue` 用于修复 Il2Cpp `Dictionary` 写入已装箱值类型时的数据偏移。

Interop 把 `Il2CppSystem.ValueTuple`、`DaySceneMapProfile.MapNode` 等非 blittable 结构体包装为类，托管侧持有已装箱对象指针，而原生 `Dictionary` 的值槽需要未装箱结构体数据。直接调用 `Add` 或索引器会把对象头当作字段数据写入，造成字段错位、无效指针和崩溃。

该方法执行以下操作：

1. 检查字典声明的 Value 是原生值类型，禁止对普通引用类型拆箱。
2. 从目标 Dictionary 的 IL2CPP 元数据中定位双参数 `set_Item`。
3. 通过 `il2cpp_runtime_invoke` 调用：字符串 Key 传对象指针，`int` Key 传数据指针；Value 传 `il2cpp_object_unbox` 取得的结构体数据。

调用期间保留对象引用，原生异常照常抛出。此方法仅适用于已确认存在该缺陷的 Dictionary 写入，不得作为通用 Dictionary API；其他类型需先确认同类缺陷再使用。当前调用：

- `SpecialGuestRegistry`：`DataBaseLanguage.SpecialGuest`
- `SpellRegistry`：`DataBaseNight.SpecialGuestSpellPortrayal`
- `DayMapRegistry`：`DataBaseDay.mapData`

### `MapNode` 实测

`MapNode` 是包含引用字段的原生结构体。直接写入 `DataBaseDay.mapData` 的索引器后，即使地图不配置食堂，原游戏遍历地图查询食堂等级时也会触发异常。

在 RELEASE 4.4.0e、BepInEx 6 be.785 环境实测：临时字典写入前的三级食堂数组均为 `Int32[]`，写入后变为 `String[] / String[] / Int32[]`；指针对比分别对应原采集点标签、出生点标签、一级食堂数组。

验证包括临时字典新增与覆盖、字符串及数组回读、数组指针一致性；修复后重启，两张扩展地图的三个食堂数组均为空 `Int32[]`，55 个原版食堂编号的等级查询及不存在编号查询通过，实际日终进入开店准备成功。验证范围为单机，不代表联机或所有版本均已验证。以上针对合并前的 `ForceAddOrUpdateBoxedValue`；合并为 `SetBoxedValue` 后单机复测一张扩展地图，三个食堂数组长度均为 0，食堂等级查询、日终至次日白天正常。

## `Interop/SelectionCallback.cs`

`SelectionCallback` 用于构造 `DaySceneChatSelectionPannel.GetSelectionConfigurationCallback`。该委托包含 `string`、`bool` 和 `Il2CppSystem.Action` 三个 `out` 参数，普通 `DelegateSupport.ConvertDelegate` 无法正确表达其原生写回布局。

该实现执行以下操作：

1. 创建与原生调用约定一致的 `NativeGetSelectionConfigurationInvoker`。
2. 手工创建 `Il2CppMethodInfo` 和 Il2Cpp 委托对象，并设置方法指针与目标对象。
3. 以 `methodInfo.Pointer` 关联托管 Handler，在原生回调中恢复输入对象并写回三个 `out` 指针。
4. 持有原生 Invoker 和生成委托的托管引用，防止被 GC 回收。
5. 在原生回调边界捕获异常、记录日志并清空输出，禁止托管异常越过原生边界。

该实现仅支持 `GetSelectionConfigurationCallback`，不得扩展为未经验证的通用 `out/ref` 委托转换器。生成的委托不释放，只由 `UI/DaySceneSelectionItem.cs` 在构造时调用，菜单项重复使用。

## `ConvertDelegate` 的委托不释放

`DelegateSupport.ConvertDelegate` 生成的 `Il2CppToMonoDelegateReference` 由 `GCHandle`（`Normal`）保活，直到对应的 Il2Cpp 对象终结；而它自身又以强句柄持有该 Il2Cpp 对象，两者都不会被回收（Il2CppInterop 1.5.3 源码推断）。每次转换遗留一个委托、闭包和一块方法信息内存。

高频或按条目生成委托的位置应复用已转换的委托。`DaySceneChatSelectionPannel` 每次打开对每个可用选项取两次配置（判断可用性、取标题），因此 `DaySceneSelectionItem` 只在构造时转换一次，之后替换内容。

## 注入抽象类时的虚表越界写

`ClassInjector.RegisterTypeInIl2Cpp` 注入抽象类时：
- 为类结构分配的虚表空间只有"基类虚表 + 接口方法"；
- 却把 `VtableCount` 设为"基类虚表 + 接口方法 + 本类声明的抽象方法数"，并逐个写入这些抽象方法的虚表项。

每个可注入的抽象方法会越界写入一个 `VirtualInvokeData`（16 字节）。该结构由 `Marshal.AllocHGlobal` 分配，与 coreclr 共用进程堆，所以越界写会损坏进程堆，之后在无关位置随机崩溃（`RtlReportCriticalFailure ← RtlFreeHeap`）。

在 RELEASE 4.4.0e、BepInEx 6 be.785 环境下确认：
- 开启 PageHeap 后首次启动即在 `ClassInjector.RegisterTypeInIl2Cpp` 递归注入 `SpellBaseEx` 时崩溃；
- 对照本机 `Il2CppInterop.Runtime.dll` 的反编译代码，确认了上述分配与写入的不一致。

规避方法：被注入的抽象类，其抽象成员（方法、属性）全部标注 `[HideFromIl2Cpp]`，使其不参与注入。当前涉及 `ResourceEx/SpellCollection/SpellBaseEx.cs`。抽象成员如需被游戏调用，应改为带默认实现的虚成员。`tools/check.py` 检查继承游戏类型的抽象类是否漏标。

## `BeverageOut` 原生跳板的短跳转重定位

在 RELEASE 4.4.0e 的首次机会异常转储中确认：

- 托管栈为 `DMD<RunTimeStorage::BeverageOut>` → `il2cpp_runtime_invoke`，入参酒水 ID 为 0（绿茶）。
- 原文件 `GameAssembly+0x664250` 的开头为 `85 C9 74 5E`，即检查 ID 为 0 后跳到 `+0x6642B2` 的 `ret`。
- Hook 跳板把两字节条件跳转展开为六字节后，目标变成 `+0x6642B6`，多了四字节，落入 `int 3` 填充区。系统故障记录与转储地址一致。

局部规避：在现有 `RunTimeStoragePatch.BeverageOut_Prefix` 中对绿茶直接返回 `SkipOriginal`，等价于原版不扣库存，并避免进入错误跳板。不增加 Hook，不修改游戏二进制。不能据此解释所有 `Il2CppExceptionWrapper`。编译和模拟测试不替代游戏复测。

来源（源码推断）：BepInEx 默认的 Dobby。BepInEx/Dobby 的 `X64InstructionRelocation.cc` 把 `jcc rel8`（2 字节）改写为 `jcc rel32`（6 字节）时，位移未计入两者的长度差，目标多 4 字节，与转储一致；`jmp rel8` 的位移还被截断为 8 位。上游修复为 BepInEx/Dobby#13，未合并。

## 空 `Nullable<T>` 返回值

值类型返回值由 `il2cpp_runtime_invoke` 装箱，空 `Nullable<T>` 装箱后为空指针，Interop 以空指针构造 `Il2CppSystem.Nullable<T>` 时抛 `NullReferenceException`（Il2CppInterop#182）。

规避方法：不调用返回 `Nullable<T>` 的原生方法，改用其判断条件。当前涉及 `LocalPlayer.InitSkin`。

## 上游状态

核对 Il2CppInterop `81a6f78`（1.5.3 之后）与 BepInEx/Dobby `9d27180`：

| 缺陷 | 上游 | 待合并 |
|---|---|---|
| Dictionary 写入装箱值类型 | 未修复 | Il2CppInterop#246、#238 |
| `out` 参数委托 | 未修复，转换时抛异常 | #238，参数改用 `ByReference` 类型 |
| `ConvertDelegate` 不释放 | 未修复 | 未核对 |
| 抽象类虚表越界写 | 未修复 | #238 |
| 短跳转重定位 | 未修复 | BepInEx/Dobby#13 |
| 空 `Nullable<T>` 返回值 | 未修复 | #238 |

#238 为 V2 重写草稿，生成的程序集与现有代码不兼容。

## 维护规则

- 优先使用正常的强类型 Interop API，不得预先采用指针绕过。
- 不得把上述实现复制到其他类型；先复现并确认相同的底层缺陷。
- 升级 BepInEx、Il2CppInterop、游戏版本或项目引用 DLL 后，必须重新验证触发条件和内存布局。
- 缺陷消失后应删除绕过，恢复普通 Interop 调用。
