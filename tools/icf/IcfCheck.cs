using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Xml.Linq;

/// <summary>
/// 按 HarmonyX 的规则解析模组中每个 Hook 的目标，经 Il2CppInterop 包装方法找到 il2cpp token，再查折叠表。
/// 只读取类型、特性与 IL，不运行游戏代码。
/// </summary>
static class IcfCheck
{
    /// <summary>Hook 方法上带此特性表示已按规则处理折叠。</summary>
    const string FoldedAttribute = "IcfFoldedAttribute";

    static readonly bool GitHub = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
    static int errors;

    public static int Run(string modPath, string depsDir)
    {
        var root = FindRoot();
        var game = XDocument.Load(Path.Combine(root, "Versions.props")).Descendants("TargetGameVersion").Single().Value;
        var tablePath = Path.Combine(root, "tools", "icf", game.Replace(" ", "") + ".tsv");
        if (!File.Exists(tablePath))
        {
            Error($"缺少 {game} 的折叠表 {Path.GetRelativePath(root, tablePath)}，用 tools/icf index 生成");
            return 1;
        }
        var index = IcfIndex.Load(tablePath);

        var interopDir = Path.GetFullPath(Path.Combine(depsDir, "interop"));
        string[] probe = [Path.GetFullPath(Path.Combine(depsDir, "core")), interopDir];
        var mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(modPath));
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name.Name);
            if (loaded != null)
                return loaded;
            // Costura 嵌入的程序集
            using var embedded = mod.GetManifestResourceStream($"costura.{name.Name.ToLowerInvariant()}.dll.compressed");
            if (embedded != null)
            {
                using var deflate = new DeflateStream(embedded, CompressionMode.Decompress);
                using var buffer = new MemoryStream();
                deflate.CopyTo(buffer);
                buffer.Position = 0;
                return context.LoadFromStream(buffer);
            }
            var path = probe.Select(dir => Path.Combine(dir, name.Name + ".dll")).FirstOrDefault(File.Exists);
            return path == null ? null : context.LoadFromAssemblyPath(path);
        };
        var harmony = new HarmonyApi(AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(probe[0], "0Harmony.dll")));

        Type[] types;
        try
        {
            types = mod.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            foreach (var message in e.LoaderExceptions.Select(x => x.Message).Distinct())
                Error($"加载模组类型失败：{message}");
            types = e.Types.Where(t => t != null).ToArray();
        }

        int hooks = 0, managed = 0, folded = 0;
        foreach (var type in types)
        {
            CheckManualPatch(type);
            foreach (var (method, kind, info) in harmony.PatchMethods(type))
            {
                if (kind == "ReversePatch")
                    continue;
                var hook = $"{type.FullName}.{method.Name}";
                MethodBase target;
                try
                {
                    target = harmony.Original(info);
                }
                catch (Exception e)
                {
                    Error($"{hook}：解析 Hook 目标失败：{e.GetBaseException().Message}");
                    continue;
                }
                if (target == null)
                {
                    Error($"{hook}：特性中没有完整的 Hook 目标");
                    continue;
                }
                hooks++;
                var targetName = $"{target.DeclaringType.FullName}::{target.Name}";
                if (!Path.GetFullPath(target.DeclaringType.Assembly.Location).StartsWith(interopDir, StringComparison.OrdinalIgnoreCase))
                {
                    managed++;
                    continue;
                }
                var failure = NativeMethod(target, out var image, out var token);
                if (failure != null)
                {
                    Error($"{hook}：无法定位 {targetName} 的原生方法（{failure}）");
                    continue;
                }
                var entry = index.Find(image, token);
                if (entry == null)
                    continue;

                folded++;
                var others = index.At(entry.Rva).Where(e => e != entry).Select(e => e.Method).Distinct().ToList();
                var listed = others.Count == 0 ? "均为泛型实例" : string.Join("、", others.Take(5)) + (others.Count > 5 || entry.Shared - 1 > others.Count ? " 等" : "");
                var text = $"{hook}：{targetName} 与另外 {entry.Shared - 1} 个方法共享 RVA 0x{entry.Rva:x}（{listed}）";
                if (method.GetCustomAttributesData().Any(a => a.AttributeType.Name == FoldedAttribute))
                    Notice(text + "，已标注 [IcfFolded]");
                else
                    Error(text + "。换用未折叠的目标，或按 docs/rules/harmony-hook.md 处理后标注 [IcfFolded]");
            }
        }

        Console.WriteLine($"Hook {hooks} 个（托管方法 {managed} 个），目标已折叠 {folded} 个，问题 {errors} 个；折叠表 {Path.GetFileName(tablePath)}");
        return errors == 0 ? 0 : 1;
    }

    /// <summary>手动调用 Harmony.Patch 的目标无法静态解析，要求改用特性声明。</summary>
    static void CheckManualPatch(Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null)
                continue;
            foreach (var ins in IL.Read(il))
            {
                if (ins.Code != OpCodes.Call && ins.Code != OpCodes.Callvirt)
                    continue;
                var callee = Resolve(() => method.Module.ResolveMethod((int)ins.Operand));
                if (callee?.Name == "Patch" && callee.DeclaringType?.FullName is "HarmonyLib.Harmony" or "HarmonyLib.PatchProcessor")
                    Error($"{type.FullName}.{method.Name}：手动调用 Harmony.Patch 无法检查 ICF，改用 [HarmonyPatch] 特性声明目标");
            }
        }
    }

    /// <summary>
    /// 包装方法体读取 NativeMethodInfoPtr_* 字段，类型初始化器以
    /// <c>ldc.i4 token; call GetIl2CppMethodByToken; stsfld 该字段</c> 赋值；映像名是最外层类型初始化器中的第一个字符串。
    /// </summary>
    static string NativeMethod(MethodBase method, out string image, out uint token)
    {
        image = null;
        token = 0;
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il == null)
            return "没有方法体";
        var field = Fields(method, il).FirstOrDefault(f => f.Name.StartsWith("NativeMethodInfoPtr_"));
        if (field == null)
            return "方法体中没有 NativeMethodInfoPtr 字段";

        var owner = field.DeclaringType;
        var init = owner.TypeInitializer;
        if (init == null)
            return $"{owner.FullName} 没有类型初始化器";
        var args = owner.IsGenericType ? owner.GetGenericArguments() : null;
        long constant = -1, pending = -1;
        foreach (var ins in IL.Read(init.GetMethodBody().GetILAsByteArray()))
        {
            if (ins.Code == OpCodes.Ldc_I4 || ins.Code == OpCodes.Ldc_I8)
                constant = ins.Operand;
            else if (ins.Code == OpCodes.Call)
                pending = Resolve(() => owner.Module.ResolveMethod((int)ins.Operand, args, null))?.Name == "GetIl2CppMethodByToken" ? constant : -1;
            else if (ins.Code == OpCodes.Stsfld && pending >= 0)
            {
                var stored = Resolve(() => owner.Module.ResolveField((int)ins.Operand, args, null));
                if (stored?.Name == field.Name)
                {
                    token = (uint)pending;
                    break;
                }
            }
        }
        if (token == 0)
            return $"{owner.FullName} 的类型初始化器中没有 {field.Name} 的 token";

        var outer = owner;
        while (outer.DeclaringType != null)
            outer = outer.DeclaringType;
        var outerInit = outer.TypeInitializer?.GetMethodBody()?.GetILAsByteArray();
        var ldstr = outerInit == null ? default : IL.Read(outerInit).FirstOrDefault(i => i.Code == OpCodes.Ldstr);
        if (ldstr.Code != OpCodes.Ldstr)
            return $"{outer.FullName} 的类型初始化器中没有映像名";
        image = outer.Module.ResolveString((int)ldstr.Operand);
        return null;
    }

    static IEnumerable<FieldInfo> Fields(MethodBase method, byte[] il)
    {
        var typeArgs = method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
        var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;
        foreach (var ins in IL.Read(il))
            if (ins.Code == OpCodes.Ldsfld && Resolve(() => method.Module.ResolveField((int)ins.Operand, typeArgs, methodArgs)) is { } field)
                yield return field;
    }

    static T Resolve<T>(Func<T> resolve) where T : class
    {
        try
        {
            return resolve();
        }
        catch (Exception e) when (e is ArgumentException or TypeLoadException or IOException or MissingMemberException)
        {
            return null;
        }
    }

    static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Versions.props")))
                return dir.FullName;
        throw new DirectoryNotFoundException("找不到 Versions.props");
    }

    static void Error(string message)
    {
        errors++;
        Console.WriteLine(GitHub ? $"::error::{message}" : $"错误：{message}");
    }

    static void Notice(string message) => Console.WriteLine(GitHub ? $"::notice::{message}" : $"提示：{message}");

    /// <summary>通过反射调用游戏所用 HarmonyX 的解析逻辑，与 PatchClassProcessor 一致。</summary>
    sealed class HarmonyApi(Assembly harmony)
    {
        const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        readonly Type extensions = harmony.GetType("HarmonyLib.HarmonyMethodExtensions", true);
        readonly Type harmonyMethod = harmony.GetType("HarmonyLib.HarmonyMethod", true);
        readonly Type patchTools = harmony.GetType("HarmonyLib.PatchTools", true);
        readonly Type attributePatch = harmony.GetType("HarmonyLib.AttributePatch", true);
        readonly Type methodType = harmony.GetType("HarmonyLib.MethodType", true);

        public IEnumerable<(MethodInfo Method, string Kind, object Info)> PatchMethods(Type type)
        {
            var patches = (System.Collections.IList)patchTools.GetMethod("GetPatchMethods", Static).Invoke(null, [type, true]);
            if (patches.Count == 0)
                yield break;

            var container = harmonyMethod.GetMethod("Merge", Static, [typeof(List<>).MakeGenericType(harmonyMethod)]).Invoke(null, [extensions.GetMethod("GetFromType", Static).Invoke(null, [type])]);
            var methodTypeField = harmonyMethod.GetField("methodType");
            methodTypeField.SetValue(container, methodTypeField.GetValue(container) ?? Enum.Parse(methodType, "Normal"));
            var merge = extensions.GetMethod("Merge", Static, [harmonyMethod, harmonyMethod]);
            var infoField = attributePatch.GetField("info", Instance);
            var methodField = harmonyMethod.GetField("method");
            foreach (var patch in patches)
            {
                var info = infoField.GetValue(patch);
                var method = (MethodInfo)methodField.GetValue(info);
                var merged = merge.Invoke(null, [container, info]);
                methodField.SetValue(merged, method);
                yield return (method, attributePatch.GetField("type", Instance).GetValue(patch)?.ToString(), merged);
            }
        }

        public MethodBase Original(object info)
        {
            try
            {
                return (MethodBase)patchTools.GetMethod("GetOriginalMethod", Static).Invoke(null, [info]);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }
    }
}
