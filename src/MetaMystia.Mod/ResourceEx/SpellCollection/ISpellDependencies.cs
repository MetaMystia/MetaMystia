using MetaMystia.ResourceEx.Vfx;

namespace MetaMystia.ResourceEx.SpellCollection;

/// <summary>纯托管的注册前检查契约，不注册到 IL2CPP；返回 null 表示依赖齐全。</summary>
public interface ISpellDependencies
{
    // .NET 6 将静态抽象接口成员标记为预览功能，仅用于托管泛型约束。
#pragma warning disable CA2252
    static abstract string CheckDependencies(VfxBundle vfx);
#pragma warning restore CA2252
}
