# spell-vfx

符卡特效的 Unity 工程。贴图由 Python 脚本生成，prefab 与 AssetBundle 由编辑器脚本生成。设计方法见 [`docs/spell-creation/art-pipeline.md`](../../docs/spell-creation/art-pipeline.md)。

## 环境

- Unity 2021.3.28f1，须与游戏版本一致。
- Python 3，需要 `numpy`、`Pillow`。
- 检查构建产物时可以使用 `UnityPy`（可选）。

## 构建

```
pwsh scripts/build.ps1 -Spell <Name> [-SkipTextures] [-Probe] [-Unity <Unity.exe>]
```

- 未传 `-Unity` 时读取环境变量 `UNITY_EXE`。
- `-SkipTextures` 跳过贴图生成；`-Probe` 在构建后检查粒子运动方向。
- 产物为 `Build/<BundleName>`，日志在 `Logs/`。

首次打开或构建时，Unity 会生成 `Library/` 等目录，这些目录以及 `Assets/Spells/`、`Build/` 都不提交。

## 新增特效集合

1. `Assets/Editor/Spells/<Name>/<Name>VfxSet.cs`：实现 `ISpellVfxSet`。
2. `scripts/<name 小写>/gen_textures.py`：输出贴图到 `Assets/Spells/<Name>/Textures/`。
3. 需要 buff 图标时，参考 `scripts/mai/gen_buff_icons.py`。

## 目录

| 路径 | 内容 |
|---|---|
| `Assets/Editor/SpellVfx/` | 通用编辑器脚本：`ISpellVfxSet`、`SpellAssetFactory`、`ParticleBuilder`、`CameraQuad`、`SpellBundleBuilder`、`ParticleProbe` |
| `Assets/Editor/Spells/` | 各符卡的特效集合 |
| `Assets/Shaders/` | 通用粒子着色器 |
| `ProjectSettings/TagManager.asset` | 游戏的排序层 |
| `scripts/` | 构建脚本、贴图预览，以及各符卡的生成脚本 |
