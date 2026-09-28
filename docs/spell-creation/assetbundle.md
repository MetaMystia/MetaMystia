# AssetBundle 打包与加载

阅读限制见 [README.md](README.md)。构建方法见 [art-pipeline.md](art-pipeline.md)。

## 放入资源包

- AssetBundle 文件没有扩展名，例如 `assets/Spell/11001`，ResourceEx 会把它识别为二进制资源；在 `ResourceEx.json` 的 `assetBundles` 中声明后才会预加载。符卡通过 `spells[].vfxBundle` 引用同一路径。图标等 PNG 会被自动创建为精灵图（PPU 48，pivot 居中）。
- 测试时游戏读取的是游戏目录 `ResourceEx/` 下的 zip。更新时替换 zip 内对应的条目，路径保持不变，然后重启游戏。

## 运行时加载

由 `AssetBundleRegistry.LoadAll` 在启动时对每个声明的包调用 `VfxBundle.Load`，全部同步执行。注册器通过 `AssetBundleRegistry.TryGet(uri, out bundle)` 查询，再赋给符卡实例的 `Vfx`：

1. 从 `RexAssetRegistry.Assets` 取出资源字节，调用 `AssetBundle.LoadFromMemory`。
2. 调用**非泛型**的 `LoadAllAssets(Il2CppType.Of<GameObject>())`。泛型的 `LoadAllAssets<T>()` 会转发到 `ConvertObjects<T>`，游戏没有实例化 `ConvertObjects<GameObject>`，Il2CppInterop 会抛出 `Method unstripping failed`。
3. 给每个 prefab 设置 `hideFlags = HideFlags.DontUnloadUnusedAsset`。**托管引用挡不住切场景时的 `Resources.UnloadUnusedAssets`**：不设置时，prefab 在进入夜间场景前就会被卸载，之后 `Instantiate` 抛出空引用。这一点已在运行时验证：卸载后，设置了标记的全部保留，未设置的全部被销毁。
4. 按 prefab 名称缓存。资源包本身不调用 `Unload` 就会一直保持加载，不需要在托管侧保存引用。

包缺失或加载失败时不缓存为成功，查询返回 `false`。`VfxBundle.Contains` 用于注册前检查必要预制件；依赖不齐全的符卡不创建、不登记。

运行时代码自己创建的精灵图（例如符卡专用立绘）同样要设置 `HideFlags.HideAndDontSave`。

## 接口选择

- 优先使用同步、非泛型、项目中已经用过的接口。不要为了加载资源引入异步，也不要引入新的泛型调用。
- `AssetBundleRequest` 等异步对象在 Interop 中不能直接 `yield`；异步请求在完成前读取 `asset` 或 `allAssets` 会得到空值。
- 需要新接口时，先确认它在 `BepInEx/interop/` 的实际签名中存在，再确认泛型版本在游戏里是否被实例化过；不确定时优先选 `Type` 参数的重载。

## 播放与销毁

- 一次性特效：`Instantiate` 后调用 `Object.Destroy(instance, lifetime)`。
- 持续特效：停止所有 `ParticleSystem` 的发射（`StopEmitting`），等已发出的粒子消散后再销毁。
- 全屏遮罩：淡出后销毁，见 [art-pipeline.md](art-pipeline.md)。
- 特效实例不挂在常驻对象下，离开场景时随场景一起销毁。
