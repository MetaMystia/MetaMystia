using System.Text.Json;

using MetaMystia.MystiaLib;
using MetaMystia.MystiaLib.Saving;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS " + message);
}
bool Throws<E>(Action action) where E : Exception
{
    try { action(); } catch (E) { return true; }
    return false;
}

Check(Throws<ArgumentException>(() => Saves.Register<Plain>("", "a")) && Throws<ArgumentException>(() => Saves.Register<Plain>("a/b", "c"))
    && Throws<ArgumentException>(() => Saves.Register<Plain>("a", "")) && Throws<ArgumentException>(() => Saves.Register<Plain>("a", "b/c"))
    && Throws<ArgumentException>(() => Saves.Register<Plain>("a", "version", 0)), "拒绝无效的标识与版本");
Check(Throws<ArgumentException>(() => Saves.Register<List<int>>("a", "list")), "拒绝不是 JSON 对象的数据");

var main = Saves.Register<Plain>("Test", "main");
Check(Throws<ArgumentException>(() => Saves.Register<Plain>("Test", "main")), "拒绝重复注册");

Check(main.Value.Count == 0 && !main.Update(p => p.Count = 5) && main.Value.Count == 5 && SaveCarrier.Writes == 0, "还没读过档时只改内存");

SaveCarrier.Load();
Check(main.Value.Count == 0, "读档后丢弃读档前的改动");
Check(main.Update(p => { p.Count = 3; p.Name = "米斯蒂娅 \"引号\"\\\n"; p.Tags.Add("夜雀"); p.Days["a"] = 184; }), "没有记录时创建");
Check(SaveCarrier.Saved.Single() == """{"id":"Test/main","version":1,"data":{"count":3,"name":"米斯蒂娅 \"引号\"\\\n","tags":["夜雀"],"days":{"a":184}}}""",
    "记录格式固定为 id、version、data，中文不转义");
var kept = main.Value;
Check(ReferenceEquals(kept, main.Value), "同一存档内返回同一对象");

string[] slotA = SaveCarrier.Saved;
SaveCarrier.Load();
Check(main.Value.Count == 0 && main.Value.Name == "" && !ReferenceEquals(kept, main.Value), "换到没有记录的存档时回到默认值");
SaveCarrier.Load(slotA);
Check(main.Value.Count == 3 && main.Value.Name == "米斯蒂娅 \"引号\"\\\n" && main.Value.Tags.SetEquals(["夜雀"]) && main.Value.Days["a"] == 184, "换回后数据完整");
main.Update(p => p.Count = 99);
SaveCarrier.Load(slotA);
Check(main.Value.Count == 3, "没有存档就重读时丢弃改动");

main.Value.Count = 7;
Check(SaveCarrier.Saved.SequenceEqual(slotA), "不经 Update 的修改不写入");
SaveCarrier.Load(slotA);
Check(Throws<InvalidOperationException>(() => main.Update(p => { p.Count = 8; throw new InvalidOperationException(); })) && SaveCarrier.Saved.SequenceEqual(slotA),
    "修改函数抛出时不写入");

string[] foreign = ["not json {", "", "null", "[]", "5", "{\"future\":true}", "{ \"id\": \"Other/x\", \"version\":9, \"data\":{\"n\":1e3, \"n\":2}, \"extra\":true }"];
SaveCarrier.Load([.. foreign, .. slotA, "tail"]);
Check(main.Value.Count == 3 && main.Update(p => p.Count++), "混有其他记录时正常读写");
Check(SaveCarrier.Saved[..foreign.Length].SequenceEqual(foreign) && SaveCarrier.Saved[^1] == "tail" && SaveCarrier.Saved.Length == foreign.Length + 2,
    "其他记录逐字保留且顺序不变");

var late = Saves.Register<Plain>("Test", "late");
Check(late.Value.Count == 0 && late.Update(p => p.Count = 1) && SaveCarrier.Saved.Length == foreign.Length + 3 && main.Value.Count == 4, "读档后注册的数据可用");

foreach (var (record, reason) in new[]
{
    ("""{"id":"Test/main"}""", "缺少字段"),
    ("""{"id":"Test/main","version":0,"data":{}}""", "版本小于 1"),
    ("""{"id":"Test/main","version":1.5,"data":{}}""", "版本不是整数"),
    ("""{"id":"Test/main","version":"1","data":{}}""", "版本不是数字"),
    ("""{"id":"Test/main","version":1,"data":[]}""", "数据不是对象"),
    ("""{"id":"Test/main","version":1,"data":null}""", "数据为 null"),
    ("""{"id":"Test/main","version":1,"data":{"count":"x"}}""", "字段类型不符"),
    ("""{"id":"Test/main","version":2,"data":{"count":1}}""", "版本更高"),
})
{
    SaveCarrier.Load(record);
    int warnings = Bootstrap.Log.Warnings.Count;
    Check(main.Value.Count == 0 && !main.Update(p => p.Count = 6) && main.Value.Count == 6 && SaveCarrier.Saved.Single() == record
        && Bootstrap.Log.Warnings.Count == warnings + 1, $"不可用的记录只记一次日志、不被覆盖：{reason}");
}
SaveCarrier.Load(slotA[0], slotA[0]);
Check(main.Value.Count == 0 && !main.Update(p => p.Count = 6) && SaveCarrier.Saved.SequenceEqual([slotA[0], slotA[0]]), "重复的记录不可用且保留");

SaveCarrier.Load("""{"id":"Test/main","version":1,"data":{"count":2,"unknown":1}}""");
Check(main.Value.Count == 2 && main.Value.Name == "" && main.Value.Tags.Count == 0, "缺少的字段取初始值，忽略不认识的字段");

int migrations = 0;
var migrated = Saves.Register<Plain>("Test", "migrated", 3, (from, data) =>
{
    migrations++;
    if (from < 2)
    {
        var old = data["amount"];
        data.Remove("amount");
        data["count"] = old;
    }
    data["name"] = "from " + from;
    return data;
});
SaveCarrier.Load("""{"id":"Test/migrated","version":1,"data":{"amount":4}}""");
Check(migrated.Value.Count == 4 && migrated.Value.Name == "from 1" && migrations == 1, "旧版本数据经迁移读取，同一存档内只迁移一次");
Check(SaveCarrier.Saved.Single().Contains("\"amount\""), "迁移结果在修改前不写入");
Check(migrated.Update(p => p.Count++) && SaveCarrier.Saved.Single().StartsWith("""{"id":"Test/migrated","version":3,"data":{"count":5,""")
    && migrations == 1, "修改后以当前版本写入");
SaveCarrier.Load("""{"id":"Test/migrated","version":3,"data":{"count":9}}""");
Check(migrated.Value.Count == 9 && migrations == 1, "当前版本不迁移");

var broken = Saves.Register<Plain>("Test", "broken", 2, (_, _) => throw new InvalidOperationException());
SaveCarrier.Load("""{"id":"Test/broken","version":1,"data":{"count":1}}""", slotA[0]);
Check(Throws<InvalidOperationException>(() => _ = broken.Value) && Throws<InvalidOperationException>(() => broken.Update(p => p.Count = 1))
    && main.Value.Count == 3 && main.Update(p => p.Count = 4) && SaveCarrier.Saved[0].Contains("\"version\":1"), "迁移函数抛出时交给调用方，不影响其他数据");

var plain = Saves.Register<Plain>("Test", "plain", 2);
SaveCarrier.Load("""{"id":"Test/plain","version":1,"data":{"count":1}}""");
Check(plain.Value.Count == 1 && plain.Update(p => p.Count = 2) && SaveCarrier.Saved.Single().Contains("\"version\":2"), "没有迁移函数时按当前结构直接读取");

var nan = Saves.Register<Number>("Test", "nan");
SaveCarrier.Load(slotA);
Check(Throws<ArgumentException>(() => nan.Update(n => n.Value = double.NaN)) && SaveCarrier.Saved.SequenceEqual(slotA), "无法序列化时不写入");

Console.WriteLine($"ALL PASS ({checks} assertions; save carrier simulated)");

sealed class Plain
{
    public int Count { get; set; }
    public string Name { get; set; } = "";
    public HashSet<string> Tags { get; set; } = [];
    public Dictionary<string, int> Days { get; set; } = [];
}

sealed class Number
{
    public double Value { get; set; }
}
