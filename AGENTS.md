# MetaMystia

《东方夜雀食堂》的 BepInEx IL2CPP 模组：联机（`Multiplayer`、`MetaMystia.Network`、独立服务端）与资源扩展（`ResourceEx`）。目标游戏版本与模组版本见 `Versions.props`。

本机路径（逆向仓库、游戏目录、编译依赖、调试器等）见 `AGENTS.local.md`，格式见 `AGENTS.local.md.example`；缺失时向用户索取。

## 规则

- 游戏行为以逆向代码为依据，不猜测；调用、签名和 Hook 目标以项目引用的 Interop DLL 为准。逆向代码缺失或存疑时，用本机逆向工具或运行时调试核实；仍无法确认时向用户说明。
- 禁止对游戏对象使用反射；禁止在网络、文件等 IO 边界以外使用 `try-catch`；禁止 `UnityEngine.Debug`，日志使用 `[AutoLog]`。
- 代码极简，不写无意义的包装、转发层和调用链。发现可复用的代码时提醒用户考虑重构。
- 新增延迟、等待和周期逻辑使用协程，不扩展 `CommandScheduler`。
- 面向用户的文本使用 `TextId`，五种语言同时补齐。
- 提交到 Git 的内容不得包含本机路径。
- 游戏实测只在用户要求时进行。

代码风格细则见 [code-style](docs/rules/code-style.md)，文档编写规则见 [docs/README.md](docs/README.md)。

## 任务索引

| 改动 | 先读 | 最低验证 |
|---|---|---|
| Harmony Patch | [harmony-hook](docs/rules/harmony-hook.md)、[il2cpp-interop](docs/rules/il2cpp-interop.md) | 审计 + 编译 |
| 联机消息与流程 | [network-message](docs/rules/network-message.md)、[threading](docs/rules/threading.md)、[multiplayer/](docs/reference/multiplayer/architecture.md) | 编译 + 网络、流程测试 |
| 用户文本、控制台命令 | [localization](docs/rules/localization.md)、[console-command](docs/rules/console-command.md) | 编译 + `tools/check.py` |
| ResourceEx 配置 | [package-contract](docs/reference/resourceex/package-contract.md)、[module-structure](docs/reference/resourceex/module-structure.md) | 编译 + 加载示例包 |
| 符卡 | [spell-creation/](docs/spell-creation/README.md) | 编译 + 符卡测试 |
| 白天地图 | [map-creation/](docs/map-creation/README.md)、[day-maps](docs/reference/resourceex/day-maps.md) | 离线校验 |
| 服务端 | [server/](docs/reference/server/console.md) | 网络测试 |

游戏名词见 [glossary](docs/reference/glossary.md)。符卡创作与白天地图绘制目录有阅读限制，见各自 README。

## 验证

```text
dotnet build MetaMystia.sln -c Release -p:DeployToGame=false
dotnet run --project src/MetaMystia.Network.Tests -c Release --no-build
dotnet run --project src/MetaMystia.Flow.Tests -c Release --no-build
dotnet run --project src/MetaMystia.Spell.Tests -c Release --no-build
python tools/check.py
```

环境准备见 [setup](docs/dev/setup.md)，验证等级与汇报见 [verification](docs/dev/verification.md)，提交与 PR 见 [workflow](docs/dev/workflow.md)，游戏实测见 [runtime-testing](docs/dev/runtime-testing.md)。
