// 选店面板：MODE=info 只看当前选择；MODE=go 用当前选择提交「前往营业」（联机时两端选择一致后房主确认）。
public static class Program {
  public static object Execute() {
    var p = MetaMystia.Patch.IzakayaSelectorPanelPatch.instanceRef;
    if (p == null) return "selector not open";
    var spot = p.m_CurrentSelectedSpot;
    string info = "spot=" + (spot == null ? "null" : spot.PrimaryName) + " level=" + p.m_CurrentSelectedIzakayaLevel;
    if ("@@MODE@@" != "go") return info;
    if (spot == null) return info + " (no spot selected)";
    p._OnGuideMapInitialize_b__21_0();
    return info + " submitted";
  }
}
