# ResourceEx 资源包约定

本文描述当前 ResourceEx 加载器的输入约定。修改加载、校验或冲突规则时，必须同步更新本文和用户文档。

## 包格式

- ResourceEx 包是放在 ResourceEx 根目录中的 `.zip` 文件；该目录现在是**模组目录**下的 `ResourceEx/`（框架不再提供游戏根目录）。
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
- `idRangeStart`、`idRangeEnd`、`idSignature`：托管 ID 段声明与签名；
- `dependencies`：依赖的 DLC / 包标签数组（如 `["CORE", "DLC2", "DLC5"]`），加载前必须全部处于激活状态。

需要稳定引用或发布多个版本的包必须提供稳定且唯一的 `label`。不得通过更改 `label` 绕过版本冲突或 ID 段管理。

## DLC 依赖

- 资源包通过 `packInfo.dependencies` 声明所需 DLC，值为 DLC 标签（如 `"CORE"`、`"DLC1"`、`"DLCMUSIC"`）。
- 资源包在 DLC 激活状态确定后（`SteamPlatformProfile.GetActiveKeys`）才加载，此时才做依赖检查。
- 依赖项必须全部位于 `ResourceExManager.ActivePackTags`（激活 DLC + 已加载包标签）中，否则拒绝加载并记录日志。
- 未声明 `dependencies` 的包视为仅依赖 `CORE`，始终可加载。
- `CORE` 恒激活，无需实际声明。
- 配置项 `IgnoreDlcDependencyCheck`（`General` 分区，默认 `false`）可跳过依赖检查并放行所有包；启用时启动阶段会输出警告。

## 加载顺序

当前加载流程为：

1. 扫描所有 ZIP。
2. 读取并解析 `ResourceEx.json`。
3. 校验声明的资源 ID 和签名。
4. 按 `label` 解决版本冲突。
5. 创建 `LoadedResourcePackage` 并注册资源。

单个 ZIP 的文件或解析错误只影响该包，不应阻止其他包加载。明确拒绝的包应同时记录框架日志和 ResourceEx 查询结果。

## 版本冲突

- 具有相同非空 `label` 的包视为同一包的不同候选版本。
- 使用 `System.Version` 解析 `packInfo.version`，选择最高版本。
- 缺失或无法解析的版本按 `0.0.0` 处理。
- 没有 `label` 的包不参与版本冲突合并。

不得依赖目录扫描顺序解决同版本冲突。如需确定行为，应补充显式规则和测试。

模组不要求最低资源包版本。资源包可在发布说明中注明最低模组版本；配置模型与加载器尚无对应字段或自动版本检查，`packInfo.version` 仅表示资源包版本。

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

## 白天地图

`dayMaps` 声明整数 ID 的独立白天地图，参与上述 ID 校验。PNG 切片、图层、装饰、矩形碰撞、出生点、相机和音乐由 JSON 配置。可选 `height.cells` 保存每格 `x/y/slope`，坡度范围 `[-1,1]`，加载为原生隐藏高度图；省略则为平地。地图 ID 冲突时全部停用；结构校验失败的地图不注册，错误记录在日志中。当前只通过单机指令进入，不加入世界地图面板。格式与操作见 [白天地图首版](resourceex-day-maps.md)。

## 礼物邮箱

顶层可选数组 `gifts` 配置礼物邮箱；缺失、`null` 或空数组时不显示该包的邮箱。

```json
"gifts": [
  {
    "itemId": 9000,
    "allowRepeat": false,
    "title": "小恶魔的工作装",
    "dialogPackageName": "_ResourceExample_Gift_Clothes_9000"
  }
]
```

| 字段 | 说明 |
|---|---|
| `itemId` | 必填整数，引用 `DataBaseCore.Items` 中的 Item 或派生类，每次发放一件；不接受食材、料理、酒水等其他类别 |
| `allowRepeat` | 默认 `false`；为 `false` 且当前持有该 Item 时跳过发放 |
| `title` | 必填非空字符串，礼物菜单标题 |
| `dialogPackageName` | 必填非空字符串，精确匹配已构建且非空的 ResourceEx `dialogPackages[].name` |

- `itemId` 是引用，不是新增资源声明，不参与 ID 段与签名校验；可引用原版或已加载包的 Item。
- 对话名称沿用全局匹配规则，建议添加包名前缀，避免同名覆盖。
- 包按加载后的顺序显示，礼物按数组顺序显示。已持有的礼物仍可播放对话，发放判断在对话结束时进行。
- 不保存领取记录；物品移除后可再次领取。从其他来源获得的同号 Item 也会阻止不可重复礼物的发放。
- 发放走原生 `ItemInRange`，保留入库通知、图鉴、成就和衣服、装饰、唱片的去重；`allowRepeat` 不强制这些物品累加。
- 配置在数据库与对话注册完成后校验。无效礼物保留并禁用，记录包名、数组下标和原因，不影响其他礼物；缺失标题显示“无效礼物”。
- 领取结束后回到场景，沿用正常存档流程，不主动保存，也不新增联机同步。通过剧情回放播放同一对话不会发奖。

## 修改检查表

- JSON 模型、Mapper 和实际游戏注册逻辑是否同步。
- 新资源 ID 是否进入范围校验。
- `label`、版本和冲突行为是否保持稳定。
- `rex://` URI 是否规范化且不能路径逃逸。
- IO 错误是否只影响当前包并有明确日志。
- 新 Unity 资源是否在主线程创建。

## 符卡、buff 与 AssetBundle

顶层可选数组 `spells`、`buffs`、`assetBundles`。符卡行为由 Mod 代码实现，资源包只提供数据；设计方法见 [`spell-creation/`](spell-creation/README.md)。

```json
"spells": [
  {
    "id": 11001,
    "implementation": "Mai",
    "vfxBundle": "assets/Spell/11001",
    "positive": { "name": "舞符「冰晶特调」", "description": "……", "portrait": "assets/Character/11001/Portrait/0.png" },
    "negative": { "name": "舞符「冰封酒宴」", "description": "……", "portrait": "assets/Character/11001/Portrait/5.png" },
    "portrayalPivot": [0.497, 0.644]
  }
],
"buffs": [
  { "id": 11002, "name": "冰晶特调", "description": "……剩余$c秒", "icon": "assets/Buff/11002.png" },
  { "id": 11003, "name": "冰封酒宴", "description": "……剩余$c秒", "icon": "assets/Buff/11003.png" }
],
"assetBundles": [
  { "path": "assets/Spell/11001" }
]
```

| 字段 | 说明 |
|---|---|
| `spells[].id` | 所属角色 ID，须在 `characters` 中声明；归属角色标识取该角色的 `label` |
| `spells[].implementation` | Mod 中的符卡实现名（`SpellRegistry.Implementations`）；不存在时跳过注册并记录警告 |
| `spells[].vfxBundle` | 可选，关联 `assetBundles` 中的包路径；注册时赋给符卡实例的 `Vfx`。不使用特效包的实现可省略，Mai 需要配置 |
| `positive` / `negative` | 红卡、黑卡的宣言名称、说明与立绘路径；立绘读取失败时跳过注册 |
| `portrayalPivot` | 可选，宣言立绘的归一化 pivot；省略时取原版立绘平均值 |
| `buffs[]` | 计时 buff 的显示数据，`id` 即 `BuffType` 值；`description` 中的占位符由使用方的回调替换 |
| `assetBundles[].path` | 启动时同步预加载的 AssetBundle；代码按其 `rex://` URI 取用 |

`spells` 与 `buffs` 的 `id` 参与 ID 范围校验。路径与其他资源一样按相对路径书写，加载时转换为 `rex://` URI。

示例中的 `assets/Spell/11001` 是无扩展名的 AssetBundle 文件，不是目录；角色立绘位于 `assets/Character/11001/Portrait/`。

未声明 `spells` 时，不注入符卡类型或创建实例。注册前检查角色、红黑卡名称与说明、立绘，以及具体实现的必要依赖；缺失时只跳过该符卡并记录原因，不要求整包升级。Mai 要求有效的 `vfxBundle`、六个命名预制件及 buff 11002/11003 的名称、说明和图标。无特效的实现可省略包引用，依赖检查明确返回通过。

`SpellRegistry.Implementations` 保留手工工厂列表。泛型工厂要求实现纯托管接口 `ISpellDependencies`，无依赖的实现也须明确返回通过。只有检查通过的符卡才登记语言、角色符卡标记与实例。AssetBundle 加载失败不会缓存为成功，查询缺失包返回 `false`。
