using System.Collections.Generic;

namespace MetaMystia.ResourceEx.Models;

/// <summary>
/// 符卡的显示数据。id 为所属角色 ID（对应 characters），implementation 指定代码中的实现；
/// 符卡行为由代码实现，名称、说明、立绘由资源包提供。
/// </summary>
public class SpellConfig
{
    public int id { get; set; }
    public string implementation { get; set; }
    /// <summary>特效包路径，须在 assetBundles 中声明；不使用特效包时可省略。</summary>
    public string vfxBundle { get; set; }
    public SpellCardConfig positive { get; set; }
    public SpellCardConfig negative { get; set; }

    /// <summary>宣言立绘的归一化 pivot [x, y]；省略时取原版立绘平均值。</summary>
    public List<float> portrayalPivot { get; set; }
}

public class SpellCardConfig
{
    public string name { get; set; }
    public string description { get; set; }
    public string portrait { get; set; }
}
