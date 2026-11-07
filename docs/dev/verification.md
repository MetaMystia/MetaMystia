# 验证

## 等级

| 等级 | 内容 | 能说明 | 不能说明 |
|---|---|---|---|
| 审计 | 逆向调用链、Interop 签名 | 实现与游戏逻辑一致（推断） | 运行时行为 |
| 编译 | Mod、测试项目；修改服务端时加 Server | 类型与签名正确 | Hook 目标在运行时生效、时机正确 |
| 离线测试 | 网络、流程、符卡测试；`tools/check.py`；地图离线校验 | 被测逻辑、协议与一致性 | 游戏对象、场景时序 |
| 单机实测 | 游戏内操作与状态核对 | 该场景下的单机行为 | 联机一致性、未覆盖的场景 |
| 联机实测 | 主机、客机分别观察 | 该场景下的同步 | 其他场景、断线重连等未覆盖路径 |

前三级可在本地和 CI 完成。实测需要用户启动游戏，方法见 [runtime-testing](runtime-testing.md)。

## 命令

```text
dotnet build src/MetaMystia.Mod/MetaMystia.csproj -c Release -p:DeployToGame=false
dotnet run --project src/MetaMystia.Network.Tests -c Release
dotnet run --project src/MetaMystia.Flow.Tests -c Release
dotnet run --project src/MetaMystia.Spell.Tests -c Release
python tools/check.py
dotnet build src/MetaMystia.Server -c Release              # 修改服务端时
python -m dayart.validate <资源包.zip>      # 在 tools/day-map-art 下运行
```

CI 在 PR 和 `main` 推送时运行编译、三组测试、`tools/check.py` 与服务端发布。

## 离线测试

测试项目是控制台程序：`Program.cs` 顺序执行检查，失败即抛异常。

| 项目 | 覆盖 | 方式 |
|---|---|---|
| `Network.Tests` | TCP 顺序、阶段、房间管理、权限、消息隔离、聊天过滤 | 引用 `MetaMystia.Network`，本机起服务端与客户端 |
| `Flow.Tests` | 入口确认、营业等待、场景转换、玩家提示 | 链接模组源文件；`Ports.cs` 替代游戏与传输入口 |
| `Spell.Tests` | 符卡库存与绕过逻辑 | 链接模组源文件；`Ports.cs` 替代 Interop 类型与 Harmony 特性 |

模组代码通过 `<Compile Include="../MetaMystia.Mod/..." Link="..." />` 进入测试项目，不加载 Unity。判定逻辑与游戏对象调用写在一起时无法链接，需要离线测试的逻辑应能单独编译。新增或修改这类逻辑时补充检查。

## 汇报

- 写明达到的最高等级和未覆盖的范围。
- 结论标注“运行时实测”“源码推断”“待验证”。按钮返回、状态登记和实际效果是不同证据。
- 单机结果不能推出联机一致；编译通过不代表 Hook 生效。
