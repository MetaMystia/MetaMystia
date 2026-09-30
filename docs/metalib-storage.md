# MetaLib 存储与按键

`MetaMystia.MetaLib` 可独立加载，不依赖 MetaMystia 主模组。安装目录为 `BepInEx/plugins/MetaMystia.MetaLib/`。

```powershell
dotnet build src/MetaMystia.MetaLib/MetaMystia.MetaLib.csproj -c Release -p:DeployToGame=true
dotnet run --project src/MetaMystia.MetaLib.Tests -c Release
```

去掉 `-p:DeployToGame=true` 则只输出到项目 `bin/Release/net6.0/`。MetaLib 及其测试项目不加入主解决方案，普通整体构建和现有 CI/CD 不包含它们；项目禁用 `publish` 和 `pack`。

## 数据格式

只使用 `schedulerPartialDLC["MetaLib"]`，始终不注册为 DLC。`finishedEvents` 每个字符串对应一个模块，其他集合为空：

```json
"finishedEvents": [
  "{\"module\":\"example.mod.a\",\"version\":1,\"data\":{\"count\":3}}",
  "{\"module\":\"example.mod.b\",\"version\":2,\"data\":[1,2,3]}"
]
```

每条只有模块键 `module`、模块版本 `version` 和模块数据 `data`。使用 `System.Text.Json`，不加 Base64、压缩、外层格式标记或容器版本；不迁移旧键。模块键区分大小写，建议使用插件 GUID 加功能名，版本为正整数。

`data` 支持任意常规 JSON 值，内部结构与迁移由模块管理。按模块整存整取；修改某个字段时，模块自行修改自己的对象再写回。只替换目标模块的字符串，其他记录逐字保留；目标记录异常、名称重复或版本高于写入方时拒绝覆盖。不认识、无法识别模块键的记录保留原文。

## 自动回调

其他插件引用 MetaLib 并声明 `[BepInDependency("MetaMystia.MetaLib")]`，在插件初始化的主线程注册一次：

```csharp
int count = 0;

bool registered = ModSaveData.TryRegister(
    "example.mod.a", 1,
    onLoad: saved =>
    {
        count = 0;
        if (saved == null)
            return true;
        if (saved.Version != 1 || saved.Data.ValueKind != JsonValueKind.Object ||
            !saved.Data.TryGetProperty("count", out var value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out count))
            return false;
        return true;
    },
    onSave: () => JsonSerializer.SerializeToElement(new { count }),
    out string error);
```

所需命名空间：`System.Text.Json`、`MetaMystia.MetaLib.Storage`。注册失败时由调用方记录 `error`。回调约定：

- 游戏 `LoadPlayerData` 完成后，按模块键分发版本和数据。没有记录时传 `null`；加载回调应先重置模块状态，再恢复数据或完成迁移。此时不保证场景对象已生成。
- 加载回调返回 `true` 才启用本次会话的保存回调；返回 `false` 则保留原记录，直到下次读档重新判断。
- 记录异常或版本过高时传 `null` 以清空旧槽状态，同时记录错误并禁用该模块本次会话的保存回调。默认状态不会覆盖原记录。
- 游戏进入 `WriteCurrentPlayerDataToSlotAsync` 前，在主线程同步调用保存回调，使用注册版本写入其完整 `data`。回调返回 C# `null` 表示本次不更新；JSON `null` 请返回 `JsonSerializer.SerializeToElement<object?>(null)`。
- 所有结果收集成功后一次性替换载体。显式校验失败时不提交本轮结果，记录错误，原版保存继续使用旧记录。未注册模块的记录保留。
- 回调只处理本模块状态并返回结果，不应递归触发保存、调用写入/删除接口或依赖其他模块回调顺序。回调必须同步、简短；不吞掉处理器抛出的编程异常。

实际文件保存仍由游戏完成。生成存档和写文件会在线程池执行，所以不在 `GenerateCurrentPlayerSaveData` 内调用处理器。非主线程进入保存接口时不执行模块回调，记录错误并保留上次数据。

## 直接读写

用于主动更新或检查；也必须在已读档的主线程调用。同一模块通常选择自动回调或直接写入其中一种，避免保存回调重新覆盖手动修改。

```csharp
bool found = ModSaveData.TryRead("example.mod.a", out ModuleData? saved, out string error);
// false 且 error 为空：没有记录；成功后从 saved.Version / saved.Data 取值。

bool written = ModSaveData.TryWrite("example.mod.a", 1,
    JsonSerializer.SerializeToElement(new { count = 3 }), out error);
bool removed = ModSaveData.TryRemove("example.mod.a", 1, out error);
```

这些操作只更新当前存档的内存，不立即写文件。删除自动回调模块的记录前，模块应先让保存回调返回 `null`，避免下次保存又创建记录。

数据跟随游戏的 `NotLoadedDLCSchedulerSaveData`，不另设跨槽内容缓存。加载期间及主菜单拒绝直接读写；回主菜单停用保存回调，下次读档重新分发。当前版本日期回溯保留运行数据，加载历史备份则使用那份备份的数据。

写入创建新数组、载体和字典再替换引用，后台保存可继续读取旧快照。原生值类型通过非泛型 `IDictionary` 读写装箱值，不使用反射或手工指针操作。

## F1～F9

结果显示在左上方并写入 BepInEx 日志。按键不自动写磁盘或推进日期；MetaLib 加载时，更新后的主模组停用原有调试 F1～F3。

| 按键 | 操作 |
| --- | --- |
| F1 | 查看模块键、记录数、字符串 UTF-8 总字节数和数组 SHA256 |
| F2 | 写入复杂样例 A；回调样例在内存中归零，等待保存时收集 |
| F3 | 校验 A，并显示从加载回调恢复的内存计数 |
| F4 | A 计数加一，检查旧字典快照不变；同步更新回调样例的内存计数 |
| F5 | 写入 B，记录此刻 A 的摘要 |
| F6 | 检查 B 及 A 是否与 F5 时一致 |
| F7 | 写入并回读独立的 64 KiB 字符串 |
| F8 | 停用回调样例的数据输出，删除四个 `metalib.sample.*` 记录，保留其他模块 |
| F9 | 执行托管检查、收集保存回调，再调用游戏的生成存档和 JSON 解析，逐项比较数组；不写磁盘、不加载场景 |

回调样例的键是 `metalib.sample.callback`。F2/F4 不直接写该记录；游戏保存或 F9 才收集它。重读后日志应出现 `加载回调样例：counter=...`。

## 验收步骤

1. 测试槽读档，按 F2、F3、F4、F5、F6、F7。A 和回调内存计数为 1；使用游戏原有流程保存。重启读档后按 F3，回调计数应恢复为 1，日志应有加载回调记录。这一步不要提前按 F9，以单独检查自动保存入口。
2. 按 F9，检查四条记录的内存往返，再记录 F1 摘要。
3. 退出并移除模组，用原版读档、推进并保存，再重启原版读存一次。重新装回 MetaLib，按 F1、F3、F6、F9；摘要不变，A 与回调计数为 1。
4. 切换空槽或新游戏，F3 应提示没有 A；回主菜单后 F2 应被拒绝。回到样例槽应重新恢复对应数据。
5. 按 F8 后保存、重读，四个样例不应重新出现。日期回溯保留当前状态；加载历史备份应恢复备份中的记录。

当前版本已核对 Release4.4.0e 保存入口并通过编译、托管格式与回调检查。旧实现初次启动由用户反馈正常；多字符串格式、自动回调 Hook 和卸载后的完整往返尚未实测。保存链证据见 [存档扩展调研](save-extension-research.md)。
