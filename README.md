# XSystem UnityPool

Unity object-pooling and audio playback utilities for XSystem projects.

## Features

### Prefab pooling

* `GameObjectPool` pools instances of an already loaded `GameObject` prefab.
* `PooledPrefabReference` extends `AssetReferenceGameObject` with Addressables loading and prefab pooling.
* `UnityPool`, `PooledItem`, and `PooledParticleEffect` provide keyed pooling for Addressables-backed pool items.
* `GameObjectPool` and `PooledPrefabReference` expose `OnGet` and `OnRelease` callbacks for per-instance initialization and cleanup.
* `UnityPool` invokes `PooledItem.OnGet()` and `PooledItem.OnRelease()` during its item lifecycle.

### Managed-object pooling

* `ObjectPool`, `StaticPool`, `ListPool`, and `RefCountObject` provide managed-object pooling utilities.

### Audio playback

* `AudioManager`, `AudioLibrary`, `AudioEmitter`, and `AudioCue` provide pooled audio playback.

## Dependencies

* `com.unity.addressables` 2.9.1 or newer.
* `xsystem.serialization` 1.0.0 or newer, used by the Addressables-backed `AudioClipLink`.
* VContainer is optional, but must be installed when using `UnityPool` with VContainer integration.

## Installation

Add this package from the Unity Package Manager using:

```text
https://github.com/josangjun/xsystem-unity-pool.git
```

When installing through `Packages/manifest.json`, also add `xsystem.serialization` if it is not already present.

## GameObjectPool

Use `GameObjectPool` when the prefab is already available as a `GameObject`. The constructor does not load Addressables.

```csharp
using UnityEngine;
using XSystem;

var pool = new GameObjectPool(prefab);
pool.OnGet += instance =>
{
    // Runs after the instance is activated.
};
pool.OnRelease += instance =>
{
    // Runs before the instance is deactivated.
};

var instance = pool.Get(parent);
pool.Release(instance);
pool.Clear();
pool.Dispose();
```

`Get(parent)` applies the parent when a new instance is created. `OnGet` also runs when an existing instance is reused.

## PooledPrefabReference

Use `PooledPrefabReference` for a serialized Addressables prefab reference that also owns its instance pool.

```csharp
using UnityEngine;
using XSystem;

public sealed class EffectPlayer : MonoBehaviour
{
    [SerializeField]
    private PooledPrefabReference _effectReference;

    private void Awake()
    {
        _effectReference.OnGet += HandleGet;
        _effectReference.OnRelease += HandleRelease;
    }

    private async void Start()
    {
        await _effectReference.WarmUpAsync(2);
    }

    public void Play()
    {
        var effect = _effectReference.Get(transform);
        if (effect == null)
            return;

        // Play or configure the effect.
        _effectReference.Release(effect);
    }

    private void OnDestroy()
    {
        _effectReference.OnGet -= HandleGet;
        _effectReference.OnRelease -= HandleRelease;
        _effectReference.ReleaseAsset();
    }

    private void HandleGet(GameObject instance)
    {
    }

    private void HandleRelease(GameObject instance)
    {
    }
}
```

`PooledPrefabReference` uses the `OperationHandle` inherited from `AssetReferenceGameObject`. `WarmUpAsync` loads the prefab and optionally creates the requested number of pooled instances. Call `WarmUpAsync` before synchronous `Get` when the reference has not been loaded yet.

`ReleaseAsset()` clears the instance pool and releases the Addressables handle. Do not separately load the same reference through another owner; keep loading and release ownership with `PooledPrefabReference`.

## UnityPool

`UnityPool` is a `MonoBehaviour` that manages keyed pools of `PooledItem` components. It supports serialized `PooledItem` prefab references and string Addressables keys.

```csharp
var item = await unityPool.GetAsync<PooledParticleEffect>(
    "effects/explosion",
    parent);

if (item != null)
{
    item.Play();
    unityPool.Release(item);
}
```

Use `Prepare(path, count)` during initialization to warm up an Addressables-backed pool. `UnityPool.Release` returns the item to its keyed pool, and `UnityPool.Clear` destroys pooled instances and releases the pool state.
