# 执行顺序与并行批次

配套：施工约定 [`conventions.md`](conventions.md)、审计事实 [`audit-facts.md`](audit-facts.md)、工单 [`mefx-handoff.md`](mefx-handoff.md)、[`mod-handoff.md`](mod-handoff.md)。

## 1. 依赖关系

```
WP-M0 工具链 ──► WP-M1/M2/M3 ──┐
                                ├─► WP-M4/M5/M6 ──► 打包 SDK ──┐
WP-M3（Config/Caching/Log）─────┘                              │
                                                               ▼
                                            WP-A 工程与入口（不能并行，是前提）
                                                               │
                        ┌──────────────────┬───────────────────┼───────────────────┐
                        ▼                  ▼                   ▼                   ▼
                     WP-B 白天         WP-C 备菜           WP-D 营业           WP-E 数据注入
                                                                          （可再分 E1–E4）
                        └──────────────────┴───────────────────┴───────────────────┘
                                                               ▼
                                                     WP-F 清理与交付
```

- 模组侧的 `WP-B`～`WP-E` 依赖中间件批次的 **SDK 已打包可用**（新接口编译通过）。
- 模组侧所有包都依赖 `WP-A` 完成（工程改造是前提，且 `WP-A` 改动 `PatchRegistry.cs` 等单写者文件）。
- `WP-E` 与其他模组包无文件交集，可完全并行；`WP-B`/`WP-D` 若拆分，按补丁文件划分即可。

## 2. 批次

| 批次 | 内容 | 并行度 | 合并点检查 |
| --- | --- | --- | --- |
| 0 | `WP-M0`：扩展 InteropGen、生成互操作、打包 SDK、建 `nuget.config` 与 `MetaMystia.local.props`、跑迁移前基线 | 串行 | 三个基线命令的结论被记录（既有失败与迁移失败可区分） |
| 1 | `WP-M1` 全局宿主、`WP-M2` 协程、`WP-M3` 宿主能力 | 3 路并行（各自新文件） | 主线接线 `CommonServices`／`RuntimeInstall.cs`；`dotnet build MystiaExtensionFramework.slnx` + `Mystia.Net.Sdk.Tests` |
| 2 | `WP-M4` 监听管线、`WP-M5` 服务与开关、`WP-M6` 数据注入 | 3 路并行（`Listeners.cs`／`SceneLoops.cs`／`Database.cs` 各属一个包） | 全解决方案编译 + SDK 打包出新版本 |
| 3 | `WP-A` 工程与入口 | 串行 | 全解决方案编译 + 两个测试工程；此步允许暂时保留 Harmony 引用 |
| 4 | `WP-B`/`WP-C`/`WP-D`/`WP-E` | 4 路并行（`WP-E` 可再分 4 路） | 每包完成后各自编译 + 静态检查；跨包冲突文件由主线串行处理 |
| 5 | `WP-F` 清理与交付 | 串行 | 全量验收命令、静态检查、三列对照表、缺口清单、行为变更清单 |

## 3. 每个工作包的收尾（DoD）

1. 范围内文件编译通过（`WP-A`～`WP-F` 另需全解决方案编译通过）。
2. `conventions.md` 第 5 节的静态检查清单逐条自查。
3. 工单里的验收项逐条给出结论（命令、结果、证据引用）。
4. 按 `conventions.md` 第 9 节格式回报；未决疑点不得静默带过。

## 4. 合并点检查单（主线执行）

- 编译：中间件 `dotnet build MystiaExtensionFramework.slnx`；模组 `dotnet build src/MetaMystia.Mod/MetaMystia.csproj`。
- 测试：`dotnet run --project src/MetaMystia.Network.Tests`、`dotnet run --project src/MetaMystia.Flow.Tests`（与迁移前基线对比）。
- 静态检查：`conventions.md` 第 5 节全部条目，逐条记录命中/未命中。
- 冲突复核：单写者文件是否只被主线改动；同一文件是否被两个工作包改动；新增公开签名是否与工单一致（不一致则先改工单再改代码）。
- 记录：本批次改动文件、签名差异、未决疑点，追加到交付说明。

## 5. 冲突与回退

- 两个工作包需要改同一文件时，由主线串行执行，或先按工单拆分文件（中间件侧优先「新增优先」，把实现落到新文件）。
- 工作包内发现工单与代码不符时：先报告差异与证据，由主线改工单后再继续，不得自行改口径。
- 出现无法在工单范围内解决的阻塞（例如互操作缺失、SDK 接口不可用）时，停止该包并上报，不带病前进。
