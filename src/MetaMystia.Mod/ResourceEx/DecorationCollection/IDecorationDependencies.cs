using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.DecorationCollection;

/// <summary>注入类型前检查资源依赖；纯托管接口，返回 null 表示齐全。</summary>
public interface IDecorationDependencies
{
    // 与 ISpellDependencies 一致，仅在托管泛型约束中使用。
#pragma warning disable CA2252
    static abstract string CheckDependencies(DecorationConfig config);
#pragma warning restore CA2252
}
