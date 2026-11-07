namespace MetaMystia.ResourceEx.Models;

/// <summary>
/// 自定义计时 buff 的显示数据（标题、说明、图标）。buff 的实际效果由使用它的代码决定。
/// description 中的 $a、$b、$c 等占位符由注册 buff 时传入的回调替换。
/// </summary>
public class BuffConfig
{
    public int id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public string icon { get; set; }
}
