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

## `Utils/Il2CppOutDelegate.cs`

`Il2CppOutDelegate` 用于构造 `DaySceneChatSelectionPannel.GetSelectionConfigurationCallback`。该委托包含 `string`、`bool` 和 `Il2CppSystem.Action` 三个 `out` 参数，普通 `DelegateSupport.ConvertDelegate` 无法正确表达其原生写回布局。

该实现执行以下操作：

1. 创建与原生调用约定一致的 `NativeGetSelectionConfigurationInvoker`。
2. 手工创建 `Il2CppMethodInfo` 和 Il2Cpp 委托对象，并设置方法指针与目标对象。
3. 以 `methodInfo.Pointer` 关联托管 Handler，在原生回调中恢复输入对象并写回三个 `out` 指针。
4. 持有原生 Invoker 和生成委托的托管引用，防止被 GC 回收。
5. 在原生回调边界捕获异常、记录日志并清空输出，禁止托管异常越过原生边界。

该实现仅支持 `GetSelectionConfigurationCallback`，不得扩展为未经验证的通用 `out/ref` 委托转换器。当前由 `Managers/StoryReplayManager.cs` 用于构建对话回放菜单选项。

## 维护规则

- 优先使用正常的强类型 Interop API，不得预先采用指针绕过。
- 不得把上述实现复制到其他类型；先复现并确认相同的底层缺陷。
- 升级 BepInEx、Il2CppInterop、游戏版本或项目引用 DLL 后，必须重新验证触发条件和内存布局。
- 缺陷消失后应删除绕过，恢复普通 Interop 调用。
