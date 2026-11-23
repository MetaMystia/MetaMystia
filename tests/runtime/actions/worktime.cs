// 设置下一次联机营业的总时长（秒），各端都要设。模组只在有房间成员时用 GameFlow.WorkTimeSecondOverride（Release 默认 360），单机走原版时长。
public static class Program {
  public static object Execute() {
    int old = MetaMystia.GameFlow.WorkTimeSecondOverride;
    MetaMystia.GameFlow.WorkTimeSecondOverride = @@SECONDS@@;
    return "work time " + old + " -> " + MetaMystia.GameFlow.WorkTimeSecondOverride;
  }
}
