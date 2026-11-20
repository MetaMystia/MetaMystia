// 执行 MetaMystia 控制台命令并返回同步输出。CMD 放在 @"" 字面量里，命令中的 " 写成 ""。
public static class Program {
  public static object Execute() {
    var lines = new System.Collections.Generic.List<string>();
    var ctx = new MetaMystia.ConsoleSystem.ConsoleContext(new System.Action<string>(s => lines.Add(s)));
    MetaMystia.ConsoleSystem.CommandRegistry.Execute(@"@@CMD@@", ctx);
    return string.Join("\n", lines);
  }
}
