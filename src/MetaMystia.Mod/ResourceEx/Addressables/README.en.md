# MetaMystia.ResourceEx.Addressables.RuntimeAddressables

**English** | [中文](./README.md)

A runtime Addressables asset-injection approach for BepInEx + Il2CppInterop mods.
Targeted at games built on Unity, compiled with IL2CPP, and making heavy use of the Addressables system.

---

## Background

Many Unity games don't expose plain `Sprite` / `AudioClip` fields — they expose
`AssetReference` / `AssetReferenceT<T>` instead:

```csharp
public AssetReferenceSprite m_SpriteAsset;
var handle = LoadAssetAllowNull(m_SpriteAsset);
```

So when a mod wants to swap a CG, inject a voice line, or drop in a custom ScriptableObject,
**you can't just assign a `Sprite.Create(...)` instance to the field** — the field type is
`AssetReferenceSprite`, and what it expects is something the Addressables system can recognise
and resolve.

`RuntimeAddressables` exists to do exactly this:
**turn an in-memory `T` into an `AssetReferenceT<T>` the game's own code can consume**,
without patching any business logic (Il2CppInterop has a hard time patching generic methods anyway)
and without rebuilding the game's bundles.

> I have an instance of `T`. How do I make it look like an `AssetReferenceT<T>` to the game?

`RuntimeAddressables`' answer is one line:

```csharp
AssetReferenceT<Sprite> spriteRef =
    RuntimeAddressables.Register("mymod://cg/painting_a", spriteInstance);

someGameField.m_SpriteAsset = spriteRef;
```

When the game later `await`s / `LoadAssetAsync`s that reference, it walks the native Addressables
pipeline straight into the provider we registered, which hands the in-memory `T` instance back.

---

## Why "any string as the key" doesn't work

Building this surfaced a few unavoidable design constraints. This section just sketches the
problems and the directions — **read the source for the actual implementation**:

1. **`AssetReference.RuntimeKeyIsValid()` requires the key to parse as `Guid.TryParse`.**
   A non-GUID key makes the game's code path silently early-return with no error logged.
   The trap: `LoadAssetAsync` doesn't throw, it just returns an empty handle, the screen "looks
   like nothing happened", and the cause is brutal to find while debugging.
   *Direction*: internally map any human-readable key through a stable hash to GUID form,
   transparent to callers; `Unregister` / `IsRegistered` go through the same map.

2. **Il2CppInterop can't inject closed generic types.**
   The intuitive `MyProvider<T> : ResourceProviderBase` will throw at runtime:
   `Type ... is generic and can't be used in il2cpp`.
   *Direction*: one **concrete (non-generic)** Provider subclass per asset type.

3. **`Addressables.ResourceManager.ResourceProviders` is not actually a `List<>`.**
   *Direction*: get the list, `Cast` it to its real internal type, then `Add`.

The handling for each of these in the source is short and self-explanatory — once you know what
the problem is.

---

## Usage examples

### Register a Sprite

```csharp
using MetaMystia.ResourceEx.Addressables;

Sprite mySprite = LoadFromSomewhere(...);
var spriteRef = RuntimeAddressables.Register("mymod://cg/a", mySprite);
someGameField.m_SpriteAsset = spriteRef;
```

### Register an AudioClip

```csharp
AudioClip clip = WavLoader.LoadFromFile(@"path\to\voice.wav");
var audioRef = RuntimeAddressables.Register("mymod://voice/001", clip);
dialogAction.m_AudioAsset = audioRef;
```

### Unregister / query

```csharp
RuntimeAddressables.Unregister<Sprite>("mymod://cg/a");
bool ok = RuntimeAddressables.IsRegistered<AudioClip>("mymod://voice/001");
```

### Naming keys

The key string is unrestricted, but adding a mod identifier prefix is recommended to avoid
collisions across mods: `"<mod_name>://<category>/<id>"`.

---

## Extending to new asset types

Built-in support covers `Sprite` and `AudioClip` only. To add `Material` / `TextAsset` /
custom `ScriptableObject` etc.:

1. Copy [`Providers/InMemorySpriteProvider.cs`](Providers/InMemorySpriteProvider.cs) and swap
   the type — about 30 lines, fully symmetric.
2. Call once during your mod's initialisation:
   ```csharp
   ClassInjector.RegisterTypeInIl2Cpp<InMemoryYourTypeProvider>();
   RuntimeAddressables.RegisterProviderType<YourType>(
       provider:    new InMemoryYourTypeProvider(),
       providerId:  InMemoryYourTypeProvider.ProviderIdConst,
       addAsset:    InMemoryYourTypeProvider.AddAsset,
       removeAsset: InMemoryYourTypeProvider.RemoveAsset,
       hasAsset:    InMemoryYourTypeProvider.HasAsset);
   ```
3. After that, `RuntimeAddressables.Register<YourType>(key, instance)` just works.

---

## Coexisting with other mods

- Multiple mods can each call `Register(...)` independently; they share a single
  `ResourceLocationMap` underneath but do not interfere with each other.
- Re-registering the same key **overwrites** the existing entry and emits a warning log.

---

## Limitations

- Runtime injection only — does not participate in the game's startup `.bundle` catalog parsing.
- Assets must be loaded into memory beforehand; remote / streaming assets are not supported,
  largely for simplicity.
- Not persistent — needs to be re-registered after a game restart.
- One Provider type serves exactly one asset type; do not mix.

---

## Layout

```
Addressables/
├── RuntimeAddressables.cs           Entry point, public API
├── WavLoader.cs                     Standalone PCM/Float WAV → AudioClip helper
└── Providers/
    ├── ProviderRegistration.cs      Type → Provider routing metadata
    ├── InMemorySpriteProvider.cs    Built-in Sprite support
    └── InMemoryAudioClipProvider.cs Built-in AudioClip support
```

---

From [MetaMystia](https://github.com/MetaMystia/MetaMystia).

License follows the project root `LICENSE`.

This is just one workable approach — far from a best practice. It's published in the hope
that anyone else struggling inside the BepInEx + Il2CppInterop ecosystem can take a slightly
shorter detour. If you have a more elegant idea, an issue or PR is very welcome.
