# MetaMystia.ResourceEx.Addressables.RuntimeAddressables

[English](./README.en.md) | **中文**

为 BepInEx + Il2CppInterop 模组提供一种**运行时 Addressables 资产注入**思路。
适用：基于 Unity 引擎、IL2CPP 编译、且大量使用 Addressables 系统的游戏。

---

## 问题背景

许多 Unity 游戏的资产字段不是直接的 `Sprite` / `AudioClip`，而是 `AssetReference` / `AssetReferenceT<T>`：

```csharp
public AssetReferenceSprite m_SpriteAsset;
var handle = LoadAssetAllowNull(m_SpriteAsset);
```

这意味着 Mod 想替换一张 CG、塞一段语音、注入一个自定义 ScriptableObject 时，
**不能**简单地把 `Sprite.Create(...)` 的实例赋给字段——字段类型是 `AssetReferenceSprite`，
必须给一个能被 Addressables 系统识别并解析的引用对象。

`RuntimeAddressables` 解决的就是：**把一个内存里的 `T` 转换成游戏认得出的 `AssetReferenceT<T>`**，
而无需 Patch 任何业务代码(il2cppInterop 难以 Patch 一个泛型方法)、也无需重打包游戏 Bundle。

> 我有一个 `T` 实例，怎么变成一个能被游戏代码消费的 `AssetReferenceT<T>`

`RuntimeAddressables` 可用下面代码解决：

```csharp
AssetReferenceT<Sprite> spriteRef =
    RuntimeAddressables.Register("mymod://cg/painting_a", spriteInstance);

someGameField.m_SpriteAsset = spriteRef;
```

`Register` 返回的引用之后被游戏代码 `await` / `LoadAssetAsync` 时，
会沿着原生 Addressables 路径走到我们注册的 Provider，再把内存里的 `T` 实例返回出去。

---

## 为什么 "随便给 key" 行不通

实现这套机制时，会遇到几个绕不开的设计约束。本节只列出问题与方向，**具体实现请查看源码或自行设计**：

1. **`AssetReference.RuntimeKeyIsValid()` 内部要求 key 能被 `Guid.TryParse` 解析。**
   非 GUID 形式的 key 会让游戏代码静默早退，且通常没有任何报错。
   坑在于：`LoadAssetAsync` 不会抛异常，只是返回空 handle，画面"看起来什么都没发生"，调试时极难定位。
   *方向*：在内部把任意人类可读 key 通过一个稳定的哈希函数映射到 GUID 形式，对调用者透明；`Unregister` / `IsRegistered` 走同一映射即可。

2. **`Il2CppInterop` 不能注入闭合泛型类型。**
   `MyProvider<T> : ResourceProviderBase` 这种"通用解法"在运行时会抛
   `Type ... is generic and can't be used in il2cpp`。
   *方向*：每个资产类型对应一个**具体（非泛型）** Provider 子类。

3. **`Addressables.ResourceManager.ResourceProviders` 实际类型不是 `List<>`。**
   *方向*：拿到列表后用 `Cast` 转到正确的内部类型再 `Add`。

源码中对这些问题的处理都很短小直白，遇到时一眼就能看明白。

---

## 使用示例

### 注册 Sprite

```csharp
using MetaMystia.ResourceEx.Addressables;

Sprite mySprite = LoadFromSomewhere(...);
var spriteRef = RuntimeAddressables.Register("mymod://cg/a", mySprite);
someGameField.m_SpriteAsset = spriteRef;
```

### 注册 AudioClip

```csharp
AudioClip clip = WavLoader.LoadFromFile(@"path\to\voice.wav");
var audioRef = RuntimeAddressables.Register("mymod://voice/001", clip);
dialogAction.m_AudioAsset = audioRef;
```

### 卸载 / 查询

```csharp
RuntimeAddressables.Unregister<Sprite>("mymod://cg/a");
bool ok = RuntimeAddressables.IsRegistered<AudioClip>("mymod://voice/001");
```

### key 命名

key 字符串本身不限格式，但建议加上 mod 标识前缀避免 mod 间冲突：
`"<mod_name>://<category>/<id>"`。

---

## 扩展到新的资产类型

目前内置只覆盖 `Sprite` 与 `AudioClip`。要加 `Material` / `TextAsset` / 自定义 `ScriptableObject` 等：

1. 复制 [`Providers/InMemorySpriteProvider.cs`](Providers/InMemorySpriteProvider.cs) 整体改类型即可（约 30 行，结构对称）。
2. 在你的 mod 初始化时调用一次：
   ```csharp
   ClassInjector.RegisterTypeInIl2Cpp<InMemoryYourTypeProvider>();
   RuntimeAddressables.RegisterProviderType<YourType>(
       provider:    new InMemoryYourTypeProvider(),
       providerId:  InMemoryYourTypeProvider.ProviderIdConst,
       addAsset:    InMemoryYourTypeProvider.AddAsset,
       removeAsset: InMemoryYourTypeProvider.RemoveAsset,
       hasAsset:    InMemoryYourTypeProvider.HasAsset);
   ```
3. 之后 `RuntimeAddressables.Register<YourType>(key, instance)` 就可用了。

---

## 多 Mod 共存

- 多个 Mod 各自调用 `Register(...)` 互不干扰，底层共享一个 `ResourceLocationMap`。
- 同 key 重复注册会**覆盖**已有条目并打印一条警告。

---

## 适用边界

- 仅用于运行时动态注入，不参与游戏启动时的 `.bundle` Catalog 解析。
- 资产必须事先加载到内存，不支持远程 / 流式，主要也是为了方便快捷。
- 不持久化，游戏重启后需要重新注册。
- 一个 Provider 类型只服务一种资产类型，不要混用。

---

## 文件结构

```
Addressables/
├── RuntimeAddressables.cs           主入口，公共 API
├── WavLoader.cs                     PCM/Float WAV → AudioClip 的独立工具
└── Providers/
    ├── ProviderRegistration.cs      Type → Provider 路由元数据
    ├── InMemorySpriteProvider.cs    内置 Sprite 支持
    └── InMemoryAudioClipProvider.cs 内置 AudioClip 支持
```

---

源自 [MetaMystia](https://github.com/MetaMystia/MetaMystia)。

许可与项目根目录 `LICENSE` 一致。

这只是一种可行解，远谈不上最佳实践。把它公开出来，是希望同样在 BepInEx + Il2CppInterop
生态里挣扎过的人能少绕一点弯路；如果有更优雅的思路，欢迎 issue / PR 指出。
