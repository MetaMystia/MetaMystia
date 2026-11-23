// 离屏截图：启用的相机按 depth 依次渲染到同一张纹理，存为 PATH 指定的 PNG（游戏所在机器上的路径）。不依赖窗口是否可见。
// Screen Space - Overlay 的 Canvas 不经过相机，对话框等 UI 不在图里。
public static class Program {
  public static object Execute() {
    int w = 1280, h = 720;
    var cams = UnityEngine.Object.FindObjectsOfType<Camera>();
    var list = new System.Collections.Generic.List<Camera>();
    foreach (var c in cams) if (c != null && c.enabled && c.gameObject.activeInHierarchy) list.Add(c);
    list.Sort((a, b) => a.depth.CompareTo(b.depth));
    var rt = new RenderTexture(w, h, 24);
    var old = new System.Collections.Generic.List<RenderTexture>();
    foreach (var c in list) { old.Add(c.targetTexture); c.targetTexture = rt; c.Render(); }
    for (int i = 0; i < list.Count; i++) list[i].targetTexture = old[i];
    var prev = RenderTexture.active; RenderTexture.active = rt;
    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
    RenderTexture.active = prev;
    var png = ImageConversion.EncodeToPNG(tex);
    System.IO.File.WriteAllBytes(@"@@PATH@@", png);
    return "cams=" + list.Count + " png=" + png.Length;
  }
}
