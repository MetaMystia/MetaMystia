# Harmony Hook 风格

## 组织方式

- 一个游戏目标类对应一个 Patch 类和一个主要文件。
- Patch 类使用 `[HarmonyPatch(typeof(...))]` 声明目标类型，并使用 `[AutoLog]` 接入项目日志。类级 `typeof` 必须写完整的命名空间和类名。
- Patch 统一注册到 `src/MetaMystia.Mod/Patches/PatchRegistry.cs`。
- 方法级 `[HarmonyPatch]` 必须使用 `nameof(短类名.方法名)`，并在文件顶部添加对应命名空间的 `using`。不得在 `nameof` 中重复完整命名空间，也不得使用字符串硬编码方法名。重载方法必须明确参数类型。
- Hook 方法按 `目标方法_Prefix`、`目标方法_Postfix`、`目标方法_ReversePatch` 命名。

```csharp
using Common.UI;

[HarmonyPatch(typeof(Common.UI.UniversalGameManager))]
[AutoLog]
public partial class UniversalGameManagerPatch
{
    [HarmonyPatch(nameof(UniversalGameManager.OpenDialogMenu))]
    [HarmonyPrefix]
    public static bool OpenDialogMenu_Prefix()
    {
        return RunOriginal;
    }
}
```

## Hook 选择

- 必须先审计目标方法及其上下游调用链，再根据实际执行流选择 Hook 位置。不得仅凭方法名或一般用途决定使用 `Prefix` 或 `Postfix`。
- 必须确认目标状态在何时产生、何时被后续逻辑读取，以及原方法内部和返回后分别发生哪些副作用。
- `Prefix` 可读取执行前状态、修改参数、替代结果或跳过原方法；只有所需时点位于原方法之前时才使用。
- `Postfix` 可读取原方法结果或执行后的状态；只有所需数据或副作用在原方法执行后才成立时才使用。
- 同时使用 `Prefix` 和 `Postfix` 时，必须明确原方法被跳过、提前返回或抛出异常时的执行关系及状态一致性。
- 需要延迟或受控重放原方法时使用 `ReversePatch`，但必须先确认重放不会破坏原调用链、上下文或时序。
- 能使用 `Prefix` 或 `Postfix` 完成时，不使用 Transpiler 或 Finalizer。
- Hook 签名只声明实际需要的 `__instance`、`__result`、`ref` 参数等内容。

返回 `bool` 的 Prefix 必须使用 `HarmonyPrefixFlow` 中的 `RunOriginal` 和 `SkipOriginal`，不得直接返回含义不明确的 `true` 或 `false`。

```csharp
if (MpManager.ShouldSkipAction || !MpManager.IsConnected)
    return RunOriginal;

if (MpManager.IsRoomHost)
{
    // 捕获或广播行为
    return RunOriginal;
}

if (MpManager.IsRoomClient)
{
    // 重放主机权威状态
    return SkipOriginal;
}
```

## Il2CppInterop 目标

匿名函数、局部函数和闭包在 Interop DLL 中可能表现为 `__c__DisplayClass*`、`Method_Internal_*` 等特殊名称。选择此类目标时，必须对照逆向代码和 Interop DLL 确认类型、签名和声明顺序，并在附近简要记录原始方法映射依据。

不得因逆向代码中的 `private` 或 `protected` 修饰符使用反射。Hook 目标和参数类型以项目实际引用的 Interop DLL 为准。

## 日志

Patch 类使用 `[AutoLog]`，通过项目提供的 `Log` 记录信息。禁止使用 `UnityEngine.Debug`。

日志应说明 Hook、关键条件和结果，不记录没有诊断价值的固定流程信息。
