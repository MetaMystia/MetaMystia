# 符卡美术流程

阅读限制见 [README.md](README.md)。

本文说明如何在 [`tools/spell-vfx`](../../tools/spell-vfx/) 中为新符卡设计并构建特效、全屏遮罩、buff 图标和宣言立绘。工程用法见该目录的 README。

## 原则

- **资源由代码生成**：贴图由 Python 脚本生成，prefab 由编辑器脚本生成。这样结果可以复现，改动能在 diff 里审阅，也不依赖手工编辑器操作。
- **不复用游戏资源**：贴图、图标和着色器都自己制作；游戏资源只用来测量规格和对比效果。
- **先测量，再设计**：尺寸、层级、配色、画风都先从运行时读出原版数据（方法见 [debugging.md](debugging.md)），不靠目测。
- **每一步都在游戏里核对**：编辑器里的预览无法反映游戏的相机、UI 层级和资源卸载。

## 工程结构

```
tools/spell-vfx/
  ProjectSettings/TagManager.asset   游戏的排序层（名称与 ID 必须和游戏一致）
  Assets/Shaders/                    通用粒子着色器 SpellVfx/AdditiveParticle、SpellVfx/AlphaParticle
  Assets/Editor/SpellVfx/            通用编辑器脚本
  Assets/Editor/Spells/<Name>/       每张符卡的特效集合（实现 ISpellVfxSet）
  scripts/<name>/gen_textures.py     每张符卡的贴图生成脚本
  scripts/build.ps1                  构建入口
  Assets/Spells/<Name>/              生成的贴图、材质、prefab（不提交）
  Build/                             构建产物（不提交）
```

| 通用脚本 | 作用 |
|---|---|
| `ISpellVfxSet` | 一张符卡的特效集合：`Name`（目录名）、`BundleName`（资源包文件名）、`BuildPrefabs`（返回各 prefab 根对象） |
| `SpellAssetFactory` | 设置贴图导入参数，创建并**保存**材质，按名称读取贴图和精灵图 |
| `ParticleBuilder` | 发射器与各模块的简写；按名称取排序层 ID，找不到时直接报错 |
| `CameraQuad` | 全屏遮罩层的数据载体 |
| `SpellBundleBuilder` | 自动发现所有 `ISpellVfxSet`，保存 prefab 并构建 AssetBundle |
| `ParticleProbe` | 批处理模拟 prefab，输出粒子在 XY 与 Z 上的分布和速度 |

## 新增一张符卡的特效

1. 在 `Assets/Editor/Spells/<Name>/` 中新建一个类实现 `ISpellVfxSet`，可以参考 `MaiVfxSet`。
2. 在 `scripts/<name>/gen_textures.py` 中生成贴图，输出到 `Assets/Spells/<Name>/Textures/`。
3. 运行 `pwsh scripts/build.ps1 -Spell <Name> -Probe`。
4. 把 `Build/<BundleName>` 放进资源包，保存为无扩展名文件 `assets/Spell/<id>`，在 `assetBundles` 中声明，并用 `spells[].vfxBundle` 关联。注册器将已加载的包赋给符卡实例的 `Vfx`，代码按 prefab 名称调用 `Play` 等方法，见 [assetbundle.md](assetbundle.md)。

## 设计特效

### 先写分镜

为每个效果写清楚这几点，每项对应一个 prefab：
- 在什么时刻出现；
- 出现在什么位置（施法者、目标桌、全屏）；
- 持续多久；
- 由什么结束。

例如示例符卡的分镜：施法法阵（一次性）、持续飘雪（buff 期间）、每杯酒的拖尾与落点冰晶、黑卡全屏霜冻（buff 期间）。

- 一次性特效：由 burst 发射，播放完毕销毁。
- 持续特效：用发射速率或循环，运行时停止发射后自然消散。
- 全屏遮罩：单独处理，见下文。

### 配色与形状语言

- 每张符卡选定 2–3 个主色，所有特效只用这几种颜色。示例只用冷白与冰蓝（`#F4FAFF`、`#9FD8FF`、`#4A90C8`），用来和店内的暖色灯光区分。
- 粒子贴图用白色加 alpha 绘制，颜色交给材质的 `_TintColor` 和粒子颜色，同一张贴图可以复用于不同色调。
- 用 `scripts/contact_sheet.py` 把贴图排在暖、冷两种底色上，检查边缘和对比度。

### 相机约束

夜间场景的主相机是正交相机，沿 Z 轴观察（运行时测得位置 (0, 0, -10)，`orthographicSize` 7.5）。**沿 Z 轴的运动在屏幕上不可见**：

- Unity 默认的 Cone 和 Box 发射形状沿局部 Z 轴发射。`ParticleBuilder.AddEmitter` 已把默认 Cone 转为朝上；`SetShapeBox` 会把 Box 转成朝上或朝下，并把厚度放到 Z 方向。
- Circle 形状不旋转时位于 XY 平面，并在平面内径向发射。给它加 90° 旋转会把它转成侧面朝向相机，粒子会被压成一条线。
- 发射器统一使用 World 模拟空间。
- 构建时加 `-Probe`：如果某个发射器 Z 方向的速度（`|vz|`）明显大于 XY 方向（`|vxy|`），就说明方向错了。

场景级特效（例如全场飘雪）要按相机视野布置。视野大小为 `2 × orthographicSize` 高，宽度再乘以宽高比。

### 排序层

- 排序层的名称和 ID 以 `TagManager.asset` 为准，必须与游戏一致。Unity 找不到层名时会静默回退到 `Default`，所以 `ParticleBuilder.SortingLayerId` 改为直接报错。
- 常用层：`Overlay`（场景内的雪花、羽毛、雾气），`EffectOverlay`（光效）。新增层时，先用探针读取游戏的层名与 ID。

### 着色器与材质

- 只使用 `Assets/Shaders/` 中的两个通用着色器：Additive 用于发光，Alpha 用于雪、羽毛、雾。颜色 = 贴图 × `_TintColor` × 顶点色。不依赖游戏的着色器，避免资源包中出现粉色材质。
- 材质必须通过 `SpellAssetFactory` 创建。它会先把材质保存为资产文件，只存在于内存中的材质不会进入资源包。
- 发射器保留 `playOnAwake = true`：运行时只会 `Instantiate`，不会调用 `Play()`。

## 全屏遮罩

- 游戏 UI 使用 Screen Space Overlay 画布：`Canvas`（order 3000，夜间 HUD；`UIPannelRoot` 8000，操作面板；`RecievedObjectDisplayer` 32767）和 `RootCanvas`（order 32600，暂停菜单、淡入淡出、光标）。世界空间的渲染器不可能盖在这些画布之上。
- 做法：prefab 里用 `CameraQuad` 保存遮罩层（精灵图、材质颜色、排序），运行时读取这些数据，在自建的 Screen Space Overlay 画布上生成铺满全屏的 `RawImage`，并关闭 `raycastTarget`。示例用 order 7999：高于 HUD，低于操作面板与暂停菜单（`VfxBundle.PlayScreenOverlay`）。
- `SpriteRenderer` 会用精灵图的贴图覆盖材质的 `_MainTex`，所以 `CameraQuad` 的精灵图必须就是遮罩贴图本身。
- 出现和移除用 `CanvasGroup.alpha` 渐变，按游戏时间推进。
- 遮罩的贴图应该让画面中央保持通透，避免遮挡操作区域。

## buff 图标

原版规格（运行时导出 212 张分析所得）：
- 48×48，PPU 48，pivot 居中，点采样。
- 画风：一张稍微歪斜、边缘撕裂的纸片，大约占 x 4–40、y 2–46；1 像素深褐描边 (120, 88, 64)；纸面底色约 (184, 168, 136)，带低对比度斑驳；下方 2 行半透明黑色投影；图案主线条 2 像素。少数使用彩色纸片或圆形徽章。

制作方法：参考 `scripts/mai/gen_buff_icons.py`，其中的纸片、斑驳、线条、雪花等函数都可以复用。
- 红卡和黑卡用纸片颜色或图案来区分。
- 放大到 6 倍，与原版图标并排比较；同时检查 1:1 大小下能不能认出图案。
- 像素画里斜线只用 0°、45°、90° 最干净，60° 等角度会出现台阶。

图标放在资源包 `assets/Buff/<buffId>.png`，例如 `assets/Buff/11002.png`、`assets/Buff/11003.png`，在 `buffs` 中声明，由 `BuffRegistry` 读取。

## 宣言立绘

- 宣言面板里的立绘 `Image` 尺寸固定（5.33×7.48，`Simple`，`preserveAspect=false`），精灵图的像素尺寸和 PPU 都不影响显示大小。原版立绘为 256×359、PPU 48，立绘图的宽高比应保持 256:359。
- 宣言动画**按精灵图 pivot 对位**。原版立绘的 pivot 在脸到脖子一带：104 张原版立绘的归一化 pivot 平均为 (0.497, 0.644)，x 范围 0.444–0.546，y 范围 0.500–0.777。
- 资源包的精灵图由 `RexAssets.CreateImageAsset` 以 pivot (0.5, 0.5) 创建，直接用于宣言时整张图会上移，看起来像被放大。
- 当前处理：`SpellRegistry` 用同一张贴图另建一个符卡专用精灵图，pivot 取 `spells[].portrayalPivot`，省略时使用原版平均值。构图特殊的角色可以在资源包中指定，设在脸部下方。不要靠缩小图片来补偿。
- 通用修复待办：先审计对话等其他立绘界面是否同样按 pivot 对位，再决定在 `RexAssets` 中统一处理，还是在资源包中增加 pivot 字段。

## 验收

- 构建日志中出现 `[SpellVfx] bundle written`，粒子检查没有 Z 方向异常。
- UnityPy 检查资源包：贴图、材质、着色器都在包内，排序层 ID 与游戏一致，遮罩精灵图指向正确的贴图。
- 游戏内逐项触发：每个特效在正确的位置出现，层级正确，按时结束；重复触发与离开场景时不残留。
- 用截图和原版对比比例与构图，方法见 [debugging.md](debugging.md)。
