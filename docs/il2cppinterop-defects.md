# Il2CppInterop 缺陷

Il2CppInterop 生成的壳代码可能因类型转换、封送或原生内存布局处理不完整而产生运行时缺陷。相关调用可能正常编译，但在执行时返回错误数据、破坏内存或崩溃。

不得将普通逻辑错误默认归因于 Il2CppInterop。必须先按 [`il2cpp-interop-guide.md`](il2cpp-interop-guide.md) 的排查顺序确认版本、签名、时机、生命周期和类型边界。只有缺陷可复现且原因明确时，才允许增加低层绕过；绕过必须限定到具体类型和签名。

## `Utils/MetaMikuUtils.cs`

`ForceAddOrUpdateValueTuple<TKey, TValue>` 用于修复 Il2Cpp `Dictionary` 写入已装箱值类型时的数据偏移。

已确认场景中，`Il2CppSystem.ValueTuple` 在托管侧持有已装箱对象指针，而原生 `Dictionary` 的值槽需要未装箱结构体数据。直接调用 `Add` 或索引器会把对象头当作字段数据写入，造成字段错位、无效指针和崩溃。

该方法执行以下操作：

1. 使用 `IL2CPP.il2cpp_object_unbox` 取得值的原始数据指针。
2. 从目标 Dictionary 的 IL2CPP 元数据中定位双参数 `set_Item`。
3. 固定值类型 Key，通过 `il2cpp_runtime_invoke` 传入 Key 指针和未装箱 Value 指针。

此方法仅适用于已确认存在该缺陷的 `Dictionary<TKey, TValue>` 写入，不得作为通用 Dictionary API。当前调用位于 `ResourceEx/SpecialGuest.cs`，用于写入 `DataBaseLanguage.SpecialGuest`。

## `Utils.ForceAddOrUpdateBoxedValue<TValue>`

`DaySceneMapProfile.MapNode` 是包含引用字段的原生结构体。直接写入 `DataBaseDay.mapData` 的索引器时，同样会把装箱对象头复制进值槽，造成字段偏移。地图无需配置食堂，也会在原游戏遍历地图查询食堂等级时触发异常。

2026-09-14 在 RELEASE 4.4.0e、BepInEx 6 be.785 环境实测：临时字典写入前的三级食堂数组均为 `Int32[]`，写入后变为 `String[] / String[] / Int32[]`；指针对比分别对应原采集点标签、出生点标签、一级食堂数组。

该方法位于 `Utils/MetaMikuUtils.cs`，用于字符串键与装箱值类型，当前调用为 `DayMapRegistry` 的 `Dictionary<string, DaySceneMapProfile.MapNode>`。调用前检查字典声明的 Value 是原生值类型，禁止对普通引用类型拆箱；其他类型需先确认同类缺陷再使用。

从字典类型获取双参数 `set_Item`，字符串 Key 传对象指针，Value 使用 `il2cpp_object_unbox` 取得结构体数据。调用期间保留对象引用，原生异常照常抛出，不吞掉失败。原有值类型 Key 的 `ForceAddOrUpdateValueTuple` 保持不变。

验证包括临时字典新增与覆盖、字符串及数组回读、数组指针一致性；修复后重启，两张扩展地图的三个食堂数组均为空 `Int32[]`，55 个原版食堂编号的等级查询及不存在编号查询通过，实际日终进入开店准备成功。验证范围为单机，不代表联机或所有版本均已验证。

## `Utils/Il2CppOutDelegate.cs`

`Il2CppOutDelegate` 用于构造 `DaySceneChatSelectionPannel.GetSelectionConfigurationCallback`。该委托包含 `string`、`bool` 和 `Il2CppSystem.Action` 三个 `out` 参数，普通 `DelegateSupport.ConvertDelegate` 无法正确表达其原生写回布局。

该实现执行以下操作：

1. 创建与原生调用约定一致的 `NativeGetSelectionConfigurationInvoker`。
2. 手工创建 `Il2CppMethodInfo` 和 Il2Cpp 委托对象，并设置方法指针与目标对象。
3. 以 `methodInfo.Pointer` 关联托管 Handler，在原生回调中恢复输入对象并写回三个 `out` 指针。
4. 持有原生 Invoker 和生成委托的托管引用，防止被 GC 回收。
5. 在原生回调边界捕获异常、记录日志并清空输出，禁止托管异常越过原生边界。

该实现仅支持 `GetSelectionConfigurationCallback`，不得扩展为未经验证的通用 `out/ref` 委托转换器。当前由 `Managers/StoryReplayManager.cs` 用于构建对话回放菜单选项。

## 注入抽象类时的虚表越界写

`ClassInjector.RegisterTypeInIl2Cpp` 注入抽象类时：
- 为类结构分配的虚表空间只有"基类虚表 + 接口方法"；
- 却把 `VtableCount` 设为"基类虚表 + 接口方法 + 本类声明的抽象方法数"，并逐个写入这些抽象方法的虚表项。

每个可注入的抽象方法会越界写入一个 `VirtualInvokeData`（16 字节）。该结构由 `Marshal.AllocHGlobal` 分配，与 coreclr 共用进程堆，所以越界写会损坏进程堆，之后在无关位置随机崩溃（`RtlReportCriticalFailure ← RtlFreeHeap`）。

2026-09-26 在 RELEASE 4.4.0e、BepInEx 6 be.785 环境下确认：
- 开启 PageHeap 后首次启动即在 `ClassInjector.RegisterTypeInIl2Cpp` 递归注入 `SpellBaseEx` 时崩溃；
- 对照本机 `Il2CppInterop.Runtime.dll` 的反编译代码，确认了上述分配与写入的不一致。

规避方法：被注入的抽象类，其抽象成员（方法、属性）全部标注 `[HideFromIl2Cpp]`，使其不参与注入。当前涉及 `ResourceEx/SpellCollection/SpellBaseEx.cs`。抽象成员如需被游戏调用，应改为带默认实现的虚成员。

## `BeverageOut` 原生跳板的短跳转重定位

2026-10-09 在 RELEASE 4.4.0e 的首次机会异常转储中确认：

- 托管栈为 `DMD<RunTimeStorage::BeverageOut>` → `il2cpp_runtime_invoke`，入参酒水 ID 为 0（绿茶）。
- 原文件 `GameAssembly+0x664250` 的开头为 `85 C9 74 5E`，即检查 ID 为 0 后跳到 `+0x6642B2` 的 `ret`。
- Hook 跳板把两字节条件跳转展开为六字节后，目标变成 `+0x6642B6`，多了四字节，落入 `int 3` 填充区。系统故障记录与转储地址一致。

局部规避：在现有 `RunTimeStoragePatch.BeverageOut_Prefix` 中对绿茶直接返回 `SkipOriginal`，等价于原版不扣库存，并避免进入错误跳板。不增加 Hook，不修改游戏二进制。该证据确认跳板重定位错误，尚未定位具体依赖库中的实现；也不能据此解释所有 `Il2CppExceptionWrapper`。编译和模拟测试不替代游戏复测。

## 维护规则

- 优先使用正常的强类型 Interop API，不得预先采用指针绕过。
- 不得把上述实现复制到其他类型；先复现并确认相同的底层缺陷。
- 升级 BepInEx、Il2CppInterop、游戏版本或项目引用 DLL 后，必须重新验证触发条件和内存布局。
- 缺陷消失后应删除绕过，恢复普通 Interop 调用。
