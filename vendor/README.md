# vendor/

编译用的外部产物，随仓库一起走，避免构建依赖同级目录里的另一个仓库。

## `vendor/nuget/Mystia.Extension.Sdk.<版本>.nupkg`

MEFX（`MystiaExtensionFramework`）打出的模组 SDK 包。`nuget.config` 里的 `mystia-local` 源指向框架仓库的
`artifacts/nuget`（本机开发用），CI 用这里的副本（CI 里没有同级框架仓库）。两者是同一个包，改完 SDK 记得同步：

```bash
# 在框架仓库
dotnet pack sdk/Mystia.Extension.Sdk/Mystia.Extension.Sdk.Pack.csproj -c Release
# 在本仓库（版本号取自 nuspec）
cp ../MystiaExtensionFramework/artifacts/nuget/Mystia.Extension.Sdk.<版本>.nupkg vendor/nuget/
```

**注意**：SDK 版本号不变时，NuGet 按版本号缓存包，所以同步之后本机要清一次缓存
（`rm -rf ~/.nuget/packages/mystia.extension.sdk`），否则仍会用旧包。
