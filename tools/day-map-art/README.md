# day-map-art

绘制 ResourceEx 白天地图（`dayMaps`，formatVersion 1）的工具：程序化像素绘画库、生图结果的拼接与抠图、组装、切片打包、碰撞与坡度生成、离线校验和参考渲染。设计方法见 [`docs/map-creation/`](../../docs/map-creation/README.md)。

## 环境

- Python 3.10+，需要 `numpy`、`Pillow`、`scipy`。
- 像素字体（可选，用于招牌文字）：Fusion Pixel Font 12px 日文版（OFL-1.1），从其 GitHub Releases 下载 `fusion-pixel-font-12px-monospaced-ttf` 压缩包，取 `fusion-pixel-12px-monospaced-ja.ttf`。字体只用于生成图片，不放进仓库或资源包。
- 整图绘制、分块细化拼接是 CPU 与内存密集任务，在开发机上运行；`annotate.py` 只依赖 Pillow，开销小，可在本机运行。

## 用法

```text
# 构建三途川：输出资源包、预览与报告
python tools/day-map-art/maps/sanzu/build.py .tmp/day-map/sanzu --font <字体.ttf> [--chunk 1|2]

# 固定区域的 1:1 裁切
python tools/day-map-art/maps/sanzu/crops.py .tmp/day-map/sanzu [区域名…]

# 生图版（在 tools/day-map-art 目录下运行，流程见 docs/map-creation/paintover.md）
python -m maps.sanzu.refine draft <程序化构建输出> draft.png
python -m maps.sanzu.refine split <地面层.png> <窗口目录>
python -m maps.sanzu.refine stitch <地面层.png> <窗口目录> plate_fine.png [a,b]
python -m maps.sanzu.sprites cut <物件表.png> <底色> <输出目录>          # 切出连通块并编号
python -m maps.sanzu.sprites build maps/sanzu/sprites.json <物件表目录> spr/
python -m maps.sanzu.assemble <工作目录> <输出目录> --font <字体.ttf>

# 按 Mod 规则离线校验任意地图包（在 tools/day-map-art 目录下运行）
python -m dayart.validate <资源包.zip>

# 1:1 标注图：网格、碰撞、坡度、出生点、相机、物件与分区标注、图例（数据读自资源包）
python tools/day-map-art/annotate.py <资源包.zip> <1:1 渲染.png> <输出.png> --font <字体.ttf> [--labels maps/sanzu/labels.json]
    [--collect maps/sanzu/collectables.json] [--layers grid,zones,objects,spawns]     # 采集点规划图

# 参考渲染：formatVersion 1/2 地图包 → PNG
python tools/day-map-art/render_ref.py <解压目录> <输出目录> <像素/格> [地图名…] [--crop=x0,y0,x1,y1] [--debug] [--fx]
```

`build.py` 输出：

| 文件 | 内容 |
|---|---|
| `SanzuRiver.zip` | 资源包：`ResourceEx.json` + `map/sanzu/` 下的图集与 BGM |
| `preview.png`、`preview_half.png`、`overview.png` | 按游戏层级合成的 1:1、1/2、1/4 预览 |
| `preview_noover.png` | 不含前景层（树冠、雾、光）的预览 |
| `debug.png` | 碰撞（红）、坡度（蓝正橙负）、出生点（绿）、相机中心范围（青） |
| `layer_*.png` | 各图层 1/4 缩略图 |
| `report.txt` | 切片与碰撞数量、离线检查问题 |

## 目录

| 路径 | 内容 |
|---|---|
| `dayart/core.py` | 坐标与画布、噪声、遮罩与距离场、色阶量化、众数滤波、合成、散布 |
| `dayart/materials.py` | 地表材质：平涂斑块、苔草地、水面、卵石滩、崖面、雾带、花丛区域 |
| `dayart/sprite.py` | 精灵与绘图原语、自动描边、字符画精灵 |
| `dayart/props.py` | 通用道具：石块、积石、彼岸花、风车、卒塔婆、树冠、阔叶树、杉、柳、枯树、石灯笼、提灯 |
| `dayart/collide.py` | 可走网格 → 碰撞矩形；出生点、连通性、坡度格检查 |
| `dayart/pack.py` | 切片去重、图集、相机范围、占位 BGM、ZIP |
| `dayart/validate.py` | 与 `DayMapRegistry.Validate` 一致的离线校验 |
| `dayart/paintover.py` | 生图结果后处理：配准、低频校色、最小差异接缝拼接、纯色底抠图与去溢色 |
| `dayart/sheet.py` | 道具样张 |
| `render_ref.py` | 参考渲染器 |
| `annotate.py` | 1:1 标注图与采集点规划图；各地图的分区文字与物件名写在 `maps/<name>/labels.json`，采集点设想写在 `collectables.json` |
| `maps/sanzu/` | 三途川。程序化版：`layout.py`、`props.py`、`paint.py`、`build.py`、`crops.py`、试验脚本。生图版：`prompts/`、`refine.py`、`sprites.py`、`sprites.json`、`place.py`、`assemble.py`。设想数据：`labels.json`、`collectables.json`（采集点）、`ingredients.json`（新增食材草案） |

## 新增一张地图

1. 复制 `maps/sanzu/` 为 `maps/<name>/`，先改 `layout.py`：地图范围、各区域多边形与路径、地标、出生点。
2. 在 `props.py` 写本图专属道具，用 `test_*.py` 出样张逐个审阅。
3. 在 `paint.py` 按“地面 → 物件 → 前景层”组织绘制，`walkable()` 定义可走区域，`slope_cells()` 定义坡度。
4. 在 `build.py` 改地图 ID、名称、包标签与输出文件名。
5. 构建后看 `report.txt` 与 `debug.png`，再按 [`docs/map-creation/debugging.md`](../../docs/map-creation/debugging.md) 进游戏核对。
