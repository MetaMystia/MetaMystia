// 一张符卡的特效集合：对应一个 AssetBundle，内含若干 prefab。
// 新增符卡时实现此接口即可，SpellBundleBuilder 会自动发现。

using System.Collections.Generic;
using UnityEngine;

public interface ISpellVfxSet
{
    /// <summary>资源目录名，生成的贴图、材质、prefab 位于 Assets/Spells/&lt;Name&gt;/。</summary>
    string Name { get; }

    /// <summary>AssetBundle 文件名，输出到 Build/&lt;BundleName&gt;。</summary>
    string BundleName { get; }

    /// <summary>构建全部 prefab 根对象；prefab 以根对象名称保存，运行时按该名称查找。</summary>
    IEnumerable<GameObject> BuildPrefabs(SpellAssetFactory assets);
}
