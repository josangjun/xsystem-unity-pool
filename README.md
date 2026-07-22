# XSystem UnityPool

Unity object-pooling and audio playback utilities for XSystem projects.

## Features

* `UnityPool` and `PoolItem` for pooling Addressables-backed prefabs.
* `ObjectPool`, `StaticPool`, `ListPool`, and `RefCountObject` for managed-object pooling.
* `AudioManager`, `AudioLibrary`, and `AudioEmitter` for pooled audio playback.

## Dependencies

* `com.unity.addressables` 2.9.1 or newer.
* `xsystem.serialization` 1.0.0 or newer, used by `AudioClipLink`.
* VContainer is optional, but must be installed when using `UnityPool` with VContainer integration.

## Installation

Add this package from the Unity Package Manager using:

```text
https://github.com/josangjun/xsystem-unity-pool.git
```

When installing through `Packages/manifest.json`, also add `xsystem.serialization` if it is not already present.
