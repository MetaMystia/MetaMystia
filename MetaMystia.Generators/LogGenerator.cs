using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace MetaMystia.Generators
{
    [Generator]
    public sealed class LogGenerator : IIncrementalGenerator
    {
        private const string LogSource = "MetaMystia.Plugin.Instance.Log";
        private const string LogSourceType = "BepInEx.Logging.ManualLogSource";
        private const string TargetNamespace = "MetaMystia";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 调试用
            // System.Diagnostics.Debugger.Launch();

            var classProvider = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    "MetaMystia.AutoLogAttribute",
                    predicate: (node, _) =>
                        node is ClassDeclarationSyntax cls &&
                        cls.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword),
                    transform: (ctx, _) => GetClassInfo(ctx)
                )
                .Where(info => info is not null)
                .Select((info, _) => info!.Value);

            context.RegisterSourceOutput(classProvider, (spc, classInfo) =>
            {
                var source = GeneratePartial(classInfo.Namespace, classInfo.ClassName);
                spc.AddSource(
                    $"{classInfo.ClassName}.bepinexlog.g.cs",
                    SourceText.From(source, Encoding.UTF8)
                );
            });
        }

        private static ClassInfo? GetClassInfo(GeneratorAttributeSyntaxContext context)
        {
            var cls = (ClassDeclarationSyntax)context.TargetNode;
            var model = context.SemanticModel;
            var clsSymbol = model.GetDeclaredSymbol(cls);

            if (clsSymbol == null)
                return null;

            var className = clsSymbol.Name;
            var namespaceName = clsSymbol.ContainingNamespace.IsGlobalNamespace
                ? ""
                : clsSymbol.ContainingNamespace.ToString();

            return new ClassInfo(className, namespaceName);
        }

        private static string GeneratePartial(string? ns, string className)
        {
            var sb = new StringBuilder();

            if (ns != null)
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"public partial class {className}");
            sb.AppendLine("{");
            sb.AppendLine($"        [MemoryPack.MemoryPackIgnore]");
            sb.AppendLine($"        private static SgrYuki.LogWrapper Log => new ({LogSource}, nameof({className}));");
            sb.AppendLine("}");

            if (ns != null)
                sb.AppendLine("}");

            return sb.ToString();
        }

        private readonly struct ClassInfo(string className, string @namespace)
        {
            public readonly string ClassName = className;
            public readonly string Namespace = @namespace;
        }
    }
}
