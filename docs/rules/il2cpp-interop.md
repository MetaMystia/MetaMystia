# Il2CppInterop 开发规范

## 事实来源

- 逆向源码由 Il2CppDumper 导出，并经人工与 AI 还原，用于理解游戏逻辑和调用链。还原结果可能有误，关键结论结合上下文和运行时数据交叉验证；标注 `mimo` 或只有空方法体的代码只作参考。
- 项目实际引用的 Interop DLL 是编译、成员访问和 Hook 签名的依据。
- DummyDll 用于辅助确认原始类型、字段、方法、枚举和元数据，不能作为 Mod 访问权限的依据。

逆向源码保留 `private`、`protected` 等访问级别和完整的协程、异步、匿名函数结构；Interop 壳代码中的成员均以 `public` 形式暴露，协程和异步逻辑被拆分，编译器生成的结构使用特殊名称。禁止因为逆向源码中存在 `private` 或 `protected` 而使用反射。

## 类型边界

必须按 Interop 签名区分：

- `System.*` 与 `Il2CppSystem.*`；
- `object` 与 `Il2CppSystem.Object`；
- 托管集合与 Il2Cpp 集合；
- 托管委托与 `Il2CppSystem.Action`、`Il2CppSystem.Func`；
- `System.Nullable<T>` 与 `Il2CppSystem.Nullable<T>`。

不得根据普通 C# 经验替换 Interop 签名中的类型。

## 集合与数组

复杂处理应遵循以下顺序：

1. 从游戏 API 读取 Il2Cpp 集合。
2. 转换或复制为托管数组、`List<T>` 或 `Dictionary<TKey, TValue>`。
3. 在托管侧进行筛选、排序、分组和 LINQ。
4. 仅在调用游戏 API 前转换回目标 Il2Cpp 类型。

常见类型包括：

- `Il2CppSystem.Collections.Generic.List<T>`；
- `Il2CppSystem.Collections.Generic.Dictionary<TKey, TValue>`；
- `Il2CppReferenceArray<T>`；
- `Il2CppStructArray<T>`；
- `Il2CppStringArray`。

Il2Cpp Dictionary 不得视为普通托管 Dictionary。优先使用已经验证的 `ContainsKey`、`Add` 和索引器。不得默认认为 `TryAdd` 或复杂泛型重载可用。

不要为一次转换新增通用包装。只有转换逻辑重复且语义稳定时，才考虑补充现有扩展方法。

## 委托

普通委托转换使用 `DelegateSupport.ConvertDelegate<TIl2Cpp>()`。生成的委托被原生侧持有时，必须保证托管引用不会被 GC 回收。

带 `ref`、`out`、复杂泛型或原生指针布局的委托不能凭签名猜测。应先确认 Interop 壳代码、逆向源码和实际参数布局。项目中的 `Il2CppOutDelegate` 只用于已经确认的特殊签名，不应扩展为通用入口。

## 类型转换

Il2Cpp 对象向子类转换使用 `TryCast<T>()` 或 `Cast<T>()`。Interop 按声明类型创建托管包装，C# 的 `as`、`is` 只检查包装类型：从基类引用转子类时，即使实际对象是子类也会失败。

## 协程与异步结构

- 托管协程与 Il2Cpp 协程接口的区别见 [coroutine](coroutine.md)。
- 逆向源码中的完整 async、协程、匿名函数和局部函数，在 Interop DLL 中可能被拆成状态机、闭包类型和特殊化方法名。
- `__c__DisplayClass*`、`_Method_b__*`、`Method_Internal_*` 等名称必须结合类型、签名、声明顺序和逆向代码确认，不能只按名称猜测。

## 异常排查顺序

普通强类型调用失败时，按以下顺序检查：

1. 游戏版本、Interop DLL 和逆向资料是否匹配。
2. 类型、重载、虚方法实际实现和 Hook 目标是否正确。
3. 调用时机、场景、对象生命周期和资源初始化是否正确。
4. 是否混用了托管与 Il2Cpp 类型。
5. 是否涉及泛型实例、值类型装箱、Dictionary、Nullable、委托或 `ref/out`。

只有前四项得到排除并出现明确的异常、崩溃或数据损坏时，才考虑 Il2CppInterop 生成或封送缺陷。

禁止默认使用反射、Unsafe、手动 `il2cpp_runtime_invoke` 或指针操作。确需绕过 Interop 时，必须限定到具体类型和签名，并记录触发条件、原因和验证方式。

项目已确认的缺陷及局部绕过见 [il2cppinterop-defects](../reference/il2cppinterop-defects.md)。
