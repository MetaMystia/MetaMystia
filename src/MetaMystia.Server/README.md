# 独立联机服务器

服务器使用 .NET 10，不需要游戏、Unity、BepInEx 或 Interop。它管理公共连接与房间，按权限转发游戏载荷；玩法仍由房主运行。

## 启动

在仓库根目录执行：

```sh
dotnet run --project src/MetaMystia.Server -- 40815
```

省略端口时使用 40815。启用 IPv6 双栈：

```sh
dotnet run --project src/MetaMystia.Server -- 40815 --ipv6
```

Ctrl+C 关闭服务器。默认最多 64 个已握手玩家，每间房可设置 2–64 人，不能少于现有成员数。

## 游戏内使用

```text
/mp connect <服务器地址> [端口]
/mp room list
/mp room create [名称]
/mp room join <房间ID>
/mp room leave
```

连接完成后先进入公共域，可以聊天并在白天看见同地图玩家。创建或加入房间后继续保留公共能力，玩法消息只在房间内流转。

建立或加入房间仅允许在主界面、白天尚未结束时发起。进入选店、备菜和营业前由房主关闭入房；下一天重新开放。

房主可使用 `/mp kick uid <UID>` 和 `/mp maxplayers <人数>` 请求管理房间，由服务器确认。退房使用 `/mp room leave`；断开公共连接使用 `/mp disconnect`。

房主退房或掉线会解散房间，其他玩家保留公共连接。第一版不迁移房主、不恢复中断的玩法状态。

`/mp start [端口]` 仍可启动嵌入服务器并建立默认房间，连接该端点的玩家会自动申请加入默认房间；控制规则与独立服务器相同。

## 编译与验证

```sh
dotnet build src/MetaMystia.Server
dotnet run --project src/MetaMystia.Network.Tests -f net10.0 -- --tcp
```

详见 [状态架构](../../wip.md) 和 [核心回归](../MetaMystia.Network.Tests/README.md)。本协议替换了旧直连协议，需要所有参与者更新到匹配的版本。
