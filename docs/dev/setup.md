# 环境与构建

## 编译依赖

模组引用 BepInEx 与游戏 Interop DLL。CI 从 `MetaMystia/TouhouMystiaIzakaya-deps` 的 Release 下载依赖压缩包，地址与 SHA-256 见 `.github/workflows/ci.yml`。

解压后，按 `MetaMystia.local.props.example` 创建 `MetaMystia.local.props`，将 `BepInExPath` 指向依赖目录或游戏的 `BepInEx` 目录。每个工作树需要各自的 `MetaMystia.local.props`；指向仓库内 `.tmp/` 的相对路径在新工作树中不存在，可改用绝对路径。

## .NET

项目目标为 `net6.0`。CI 使用 .NET SDK 10.0.303 和 .NET 6 运行时。只装有更新运行时的机器运行测试时设置 `DOTNET_ROLL_FORWARD=Major`。

## 构建与部署

```text
dotnet build src/MetaMystia.Mod/MetaMystia.csproj -c Release -p:DeployToGame=false
```

- `DeployToGame=false`：只输出到各项目的 `bin/Release`。
- 省略该参数：主模组输出到 `$(BepInExPath)/plugins`。此时 `BepInExPath` 必须是游戏的 `BepInEx` 目录。
- 主模组构建后改名为 `MetaMystia-v<版本>.dll`。游戏运行时 DLL 被锁定，改名失败会留下新旧两份，需关闭游戏后重新构建。
- 网络程序集由 Costura 嵌入主模组。
- 资源包更新后需要重启游戏。

## 版本

根目录 `Versions.props`：

- `Version`：模组版本，联机兼容性以此校验；发布标签必须为 `v<Version>`。
- `TargetGameVersion`：目标游戏版本，生成编译宏，如 `RELEASE 4.4.0e` → `TMI_RELEASE_4_4_0E`。依赖协程状态、闭包类型的文件用该宏加 `#error` 标记，升级游戏版本后逐个核对。
- `NetworkProtocolVersion`：目前保持为 0。

## 独立服务端

```text
dotnet run --project src/MetaMystia.Server -c Release -- 40815 16
```

参数为端口和人数上限。配置与命令见 [服务端控制台](../reference/server/console.md)。

## 工具

- [`tools/day-map-art/`](../../tools/day-map-art/README.md)：白天地图绘制，Python 3.10+，`numpy`、`Pillow`、`scipy`，原版镜像另需 `UnityPy`。
- [`tools/spell-vfx/`](../../tools/spell-vfx/README.md)：符卡特效，Unity 2021.3.28f1。
- `tools/check.py`：文档链接、本地化键与占位符、网络消息登记的一致性检查，只需 Python 标准库。
