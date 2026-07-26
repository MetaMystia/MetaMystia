# ResourceEx 资源包约定

本文描述当前 ResourceEx 加载器的输入约定。修改加载、校验或冲突规则时，必须同步更新本文和用户文档。

## 包格式

- ResourceEx 包是放在 ResourceEx 根目录中的 `.zip` 文件。
- ZIP 内必须包含 `ResourceEx.json`，文件名匹配不区分大小写。
- 如果 ZIP 中存在多个 `ResourceEx.json`，当前实现选择路径最短的文件。
- `ResourceEx.json` 所在目录作为包内资源的根前缀。
- JSON 允许注释、尾随逗号、属性名大小写不敏感，并使用字符串解析枚举。

ZIP 文件名形成 `PackageName`。`packInfo.label` 有效时形成 `PackageLabel`；缺失或不适合作为 `rex://` 包名时回退到 ZIP 文件名。

## packInfo

`packInfo` 可包含：

- `name`：显示名称；
- `label`：稳定包标识，也是版本冲突和 `rex://` URI 的主要键；
- `authors`、`description`、`version`、`license`：包元数据；
- `idRangeStart`、`idRangeEnd`、`idSignature`：托管 ID 段声明与签名。

需要稳定引用或发布多个版本的包必须提供稳定且唯一的 `label`。不得通过更改 `label` 绕过版本冲突或 ID 段管理。

## 加载顺序

当前加载流程为：

1. 扫描所有 ZIP。
2. 读取并解析 `ResourceEx.json`。
3. 校验声明的资源 ID 和签名。
4. 按 `label` 解决版本冲突。
5. 创建 `LoadedResourcePackage` 并注册资源。

单个 ZIP 的文件或解析错误只影响该包，不应阻止其他包加载。明确拒绝的包应同时记录 BepInEx 日志和 ResourceEx 查询结果。

## 版本冲突

- 具有相同非空 `label` 的包视为同一包的不同候选版本。
- 使用 `System.Version` 解析 `packInfo.version`，选择最高版本。
- 缺失或无法解析的版本按 `0.0.0` 处理。
- 没有 `label` 的包不参与版本冲突合并。

不得依赖目录扫描顺序解决同版本冲突。如需确定行为，应补充显式规则和测试。

## ID 范围

当前 ID 范围为：

- 小于或等于 `8999`：游戏保留范围，ResourceEx 禁止使用。
- `9000` 至 `1073741823`：托管范围，必须声明合法 ID 段并通过签名校验。
- `1073741824` 至 `2147483647`：非托管范围，无需签名。

使用托管范围时：

- `idRangeStart` 和 `idRangeEnd` 必须存在且位于托管范围内；
- 起始值不得大于结束值；
- 所有托管 ID 必须位于声明区间内；
- 签名内容为 UTF-8 编码的 `label:start-end`；
- 签名算法为 RSA-2048、SHA-256、PKCS#1 v1.5。

配置允许关闭签名校验，但不能跳过范围和保留 ID 校验。

新增带 ID 的资源类型时，必须同步更新 `IdRangeValidator.CollectDeclaredIds()`，否则该类型不会进入范围校验。

## 资源 URI

包内资源使用 `rex://包标识/相对路径`：

```text
rex://example-pack/assets/image.png
```

- Scheme 匹配不区分大小写。
- 包标识和资源路径按大小写精确匹配。
- 路径必须是相对路径。
- 禁止绝对路径、空路径、`.` 和 `..` 路径段。
- 配置中的普通相对路径会结合当前 `PackageLabel` 转换为 `rex://` URI。

不得让资源路径逃逸 ZIP 内部前缀，也不得使用本机绝对路径作为包内资源引用。

## 资源类型

当前注册表按扩展名将资源分为 Image、Text、Audio 和 Binary。新增扩展名时必须确认读取方式、Unity 对象创建时机和 Addressables Provider 是否支持。

资源包和 ZIP 归档持有内存与句柄，生命周期结束时必须调用 `Dispose()`。

## 修改检查表

- JSON 模型、Mapper 和实际游戏注册逻辑是否同步。
- 新资源 ID 是否进入范围校验。
- `label`、版本和冲突行为是否保持稳定。
- `rex://` URI 是否规范化且不能路径逃逸。
- IO 错误是否只影响当前包并有明确日志。
- 新 Unity 资源是否在主线程创建。
