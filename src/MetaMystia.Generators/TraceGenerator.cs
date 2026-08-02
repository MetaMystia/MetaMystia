using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MetaMystia.Generators
{
    [Generator]
    public sealed class TraceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classProvider = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    "MetaMystia.TracePatchAttribute",
                    predicate: (node, _) =>
                        node is ClassDeclarationSyntax cls &&
                        cls.Modifiers.Any(SyntaxKind.PartialKeyword),
                    transform: (ctx, _) => GetTraceInfo(ctx)
                )
                .Where(info => info.HasValue)
                .Select((info, _) => info!.Value);

            context.RegisterSourceOutput(classProvider, GenerateTracePatches);
        }

        private static TraceClassInfo? GetTraceInfo(GeneratorAttributeSyntaxContext context)
        {
            var symbol = context.TargetSymbol as INamedTypeSymbol;
            if (symbol == null) return null;

            var className = symbol.Name;
            var ns = symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : symbol.ContainingNamespace.ToString();

            // Extract target type name from [HarmonyPatch(typeof(X))]
            string? targetTypeName = null;
            foreach (var attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "HarmonyPatchAttribute" &&
                    attr.ConstructorArguments.Length > 0 &&
                    attr.ConstructorArguments[0].Value is INamedTypeSymbol targetType)
                {
                    targetTypeName = targetType.Name;
                    break;
                }
            }

            // Collect [TracePatch(...)] entries
            var methods = new List<TraceMethodEntry>();
            foreach (var attr in context.Attributes)
            {
                if (attr.ConstructorArguments.Length < 1 ||
                    attr.ConstructorArguments[0].Value is not string methodName)
                    continue;

                // Read Type[] parameterTypes from second constructor arg (if present)
                string[]? paramTypeNames = null;
                if (attr.ConstructorArguments.Length >= 2 &&
                    attr.ConstructorArguments[1].Kind == TypedConstantKind.Array)
                {
                    var typeConstants = attr.ConstructorArguments[1].Values;
                    paramTypeNames = typeConstants
                        .Where(tc => tc.Value is ITypeSymbol)
                        .Select(tc => ((ITypeSymbol)tc.Value!)
                            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .ToArray();
                }

                // Read DisplayName from named arguments
                string? displayName = null;
                foreach (var named in attr.NamedArguments)
                {
                    if (named.Key == "DisplayName" && named.Value.Value is string dn)
                        displayName = dn;
                }

                methods.Add(new TraceMethodEntry(methodName, displayName, paramTypeNames));
            }

            if (methods.Count == 0) return null;

            return new TraceClassInfo(className, ns, targetTypeName, methods.ToArray());
        }

        private static void GenerateTracePatches(SourceProductionContext spc, TraceClassInfo info)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using HarmonyLib;");
            sb.AppendLine();

            if (info.Namespace != null)
            {
                sb.AppendLine($"namespace {info.Namespace};");
                sb.AppendLine();
            }

            sb.AppendLine($"partial class {info.ClassName}");
            sb.AppendLine("{");
            sb.AppendLine("    public static MetaMystia.TraceLog tl => MetaMystia.Plugin.tl;");

            for (int i = 0; i < info.Methods.Length; i++)
            {
                var entry = info.Methods[i];
                var display = entry.DisplayName
                    ?? (info.TargetTypeName != null
                        ? $"{info.TargetTypeName}.{entry.MethodName}"
                        : entry.MethodName);

                var safeName = SanitizeIdentifier(entry.MethodName);

                // Build [HarmonyPatch(...)] attribute string
                string patchAttr;
                if (entry.ParameterTypeNames != null)
                {
                    if (entry.ParameterTypeNames.Length > 0)
                    {
                        var typeList = string.Join(", ",
                            entry.ParameterTypeNames.Select(t => $"typeof({t})"));
                        patchAttr = $"[HarmonyPatch(\"{entry.MethodName}\", new System.Type[] {{ {typeList} }})]";
                    }
                    else
                    {
                        patchAttr = $"[HarmonyPatch(\"{entry.MethodName}\", new System.Type[0])]";
                    }
                }
                else
                {
                    patchAttr = $"[HarmonyPatch(\"{entry.MethodName}\")]";
                }

                sb.AppendLine();
                sb.AppendLine($"    {patchAttr}");
                sb.AppendLine($"    [HarmonyPrefix]");
                sb.AppendLine($"    public static void __Trace_{safeName}_{i}_Prefix() => tl.OnPrefix(\"{display}\");");
                sb.AppendLine();
                sb.AppendLine($"    {patchAttr}");
                sb.AppendLine($"    [HarmonyPostfix]");
                sb.AppendLine($"    public static void __Trace_{safeName}_{i}_Postfix() => tl.OnPostfix(\"{display}\");");
                sb.AppendLine();
                sb.AppendLine($"    {patchAttr}");
                sb.AppendLine($"    [HarmonyFinalizer]");
                sb.AppendLine($"    public static void __Trace_{safeName}_{i}_Finalizer(System.Exception __exception) => tl.OnFinalizer(\"{display}\", __exception);");
            }

            sb.AppendLine("}");

            spc.AddSource(
                $"{info.ClassName}.trace.g.cs",
                SourceText.From(sb.ToString(), Encoding.UTF8)
            );
        }

        private static string SanitizeIdentifier(string name)
        {
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                    chars[i] = '_';
            }
            return new string(chars);
        }

        private readonly struct TraceMethodEntry
        {
            public readonly string MethodName;
            public readonly string? DisplayName;
            public readonly string[]? ParameterTypeNames;

            public TraceMethodEntry(string methodName, string? displayName, string[]? parameterTypeNames)
            {
                MethodName = methodName;
                DisplayName = displayName;
                ParameterTypeNames = parameterTypeNames;
            }
        }

        private readonly struct TraceClassInfo
        {
            public readonly string ClassName;
            public readonly string? Namespace;
            public readonly string? TargetTypeName;
            public readonly TraceMethodEntry[] Methods;

            public TraceClassInfo(string className, string? ns, string? targetTypeName, TraceMethodEntry[] methods)
            {
                ClassName = className;
                Namespace = ns;
                TargetTypeName = targetTypeName;
                Methods = methods;
            }
        }
    }
}
