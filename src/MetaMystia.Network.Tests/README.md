# 联机核心回归

直接调用实际的 Endpoint、ClientSession、TcpConnection 和资源目录实现，不引用游戏程序集。资源目录与清单以链接源码方式测试，不在协议项目中引入游戏业务。

```sh
dotnet run --project src/MetaMystia.Network.Tests -f net6.0 -- --tcp
dotnet run --project src/MetaMystia.Network.Tests -f net10.0 -- --tcp
```

每种运行时执行 34 项状态/资源检查及 6 项真实回环 TCP 检查。TCP 检查使用动态端口和进程内房主，监听地址限定为本机回环。

覆盖握手前隔离、连接代次、公共与房间关系、入房权限、容量、踢人、快照替换、旧绑定消息、确认超时后的查询恢复、房主掉线、心跳清理及资源 ID 边界。

`--fixtures <目录>` 可输出 Hello、RoomCommand、Payload、Welcome 四组固定样本，用于比较不同运行时的序列化结果。它验证这些样本的字节兼容性，不代表所有游戏载荷已经实测。

测试项目同时保留 .NET 6，以覆盖游戏端的实际运行时；SDK 会提示该目标框架已停止支持。独立服务器使用 .NET 10。

本回归不启动游戏，也不验证 Unity 角色、选店和营业效果。游戏改动仍需完整 Mod 构建和相应源码审计。
