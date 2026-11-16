using System;
using System.Linq;

using MetaMystia.ConsoleSystem;

var checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception("FAIL " + message);
    checks++;
    Console.WriteLine("PASS " + message);
}

// ── LineEditor ──
var ed = new LineEditor();
ed.Insert("hello world");
Check(ed.Text == "hello world" && ed.Caret == 11, "插入后光标在末尾");
ed.Backspace(word: true);
Check(ed.Text == "hello " && ed.Caret == 6, "Ctrl+Backspace 删除一个词");
ed.MoveLeft(word: false, select: true);
ed.MoveLeft(word: false, select: true);
Check(ed.SelectedText == "o " && ed.SelectionStart == 4, "Shift+左 选择");
ed.Insert("X");
Check(ed.Text == "hellX" && ed.Caret == 5 && !ed.HasSelection, "输入替换选区");
ed.SetText("a😀b");
ed.MoveLeft(false, false);
ed.MoveLeft(false, false);
Check(ed.Caret == 1, "左移跨过代理对");
ed.Delete(word: false);
Check(ed.Text == "ab", "Delete 删除整个代理对");
ed.SetText("x\ny\tz");
Check(ed.Text == "x y z", "换行与制表符替换为空格");
var small = new LineEditor { MaxLength = 4 };
small.Insert("abc😀");
Check(small.Text == "abc", "截断不拆开代理对");
ed.SetText("/mp connect 1.2.3.4");
ed.SelectWordAt(5);
Check(ed.SelectedText == "connect", "双击选词");
ed.MoveLeft(false, false);
Check(ed.Caret == 4 && !ed.HasSelection, "有选区时左键收起到选区起点");

// ── ConsoleHistory ──
var h = new ConsoleHistory { Capacity = 3 };
h.Load(["a", "b", "b", "c", "d"]);
Check(h.Items.SequenceEqual(["b", "c", "d"]), "加载去重并截断到容量");
Check(h.Older("draft") == "d" && h.Older("") == "c" && h.Older("") == "b" && h.Older("") == null, "上翻到最早为止");
Check(h.Newer() == "c" && h.Newer() == "d" && h.Newer() == "draft" && h.Newer() == null, "下翻回到草稿");
h.Add("d");
Check(h.Items.Count == 3 && h.Items[^1] == "d", "连续重复不记录");
h.Add("chat message");
Check(h.Items[^1] == "chat message", "聊天消息同样写入历史");

// ── CompletionEngine ──
CompletionQuery Provider(string partial)
{
    string[] subs = ["start", "stop", "status", "connect"];
    var tokens = partial.Split(' ');
    if (tokens.Length == 1)
        return new([.. new[] { "mp", "help" }.Where(c => c.StartsWith(tokens[0])).Select(c => new CompletionItem(c, c + " desc"))], null);
    if (tokens[0] == "mp" && tokens.Length == 2)
        return new([.. subs.Where(s => s.StartsWith(tokens[1])).Select(s => new CompletionItem(s, ""))], null);
    if (tokens[0] == "mp" && tokens[1] == "connect")
        return new([], "/mp connect ‹address›");
    return CompletionQuery.Empty;
}
var c = new CompletionEngine(Provider);
c.Refresh("hello", 5, true);
Check(!c.PopupVisible && !c.SignatureVisible, "聊天文本不补全");
c.Refresh("/mp st", 6, true);
Check(c.PopupVisible && c.Items.Count == 3 && c.Token == "st", "按前缀列出候选");
c.Move(1);
c.Move(1);
Check(c.Selected == 2, "下移选择");
c.Move(1);
Check(c.Selected == 0, "选择循环");
c.Move(-1);
c.Refresh("/mp sta", 7, true);
Check(c.Selected == 1 && c.Items[c.Selected].Value == "status", "刷新后保留同一选中项");
var (text, caret) = c.Accept("/mp sta");
Check(text == "/mp status " && caret == 11, "接受补全并补空格");
c.Refresh("/mp st extra", 5, true);
c.Select(0);
(text, caret) = c.Accept("/mp st extra");
Check(text == "/mp start extra" && caret == 10, "替换光标所在的整个词");
c.Refresh("/mp connect ", 12, true);
Check(!c.PopupVisible && c.SignatureVisible, "参数位置显示签名");
c.Refresh("/mp st", 6, true);
c.Dismiss();
Check(!c.PopupVisible, "Esc 收起");
c.Refresh("/mp st", 5, false);
Check(!c.PopupVisible, "仅移动光标不重新弹出");
c.Refresh("/mp sto", 7, true);
Check(c.PopupVisible, "文本变化后重新弹出");
c.Refresh("/mp stop", 8, true);
Check(!c.SelectionDiffersFromToken, "Enter 不会替换已完整输入的词");

// ── ConsoleText ──
Check(ConsoleText.Escape("hi") == "hi", "无尖括号不转义");
Check(ConsoleText.Escape("<b>x</b>") == "<noparse><b>x</b></noparse>", "富文本被包进 noparse");
Check(!ConsoleText.Escape("a</noparse><size=99>").Contains("</noparse><size"), "无法提前闭合 noparse");
Check(ConsoleText.Strip("<color=#fff>[1]</color> <noparse><b></noparse>") == "[1] <b>", "提取纯文本保留 noparse 内容");
Check(ConsoleText.Strip("a < b") == "a < b", "孤立尖括号保留");

Console.WriteLine($"{checks} checks passed");
