# XSystem UnityPool

XSystem 프로젝트에서 사용할 수 있는 Unity 오브젝트 풀링 및 오디오 재생 유틸리티입니다.

## 기능

### 프리팹 풀링

* `GameObjectPool`은 이미 로드된 `GameObject` 프리팹의 인스턴스를 풀링합니다.
* `PooledPrefabReference`는 직렬화된 `AssetReferenceGameObject`, Addressables 로딩, 프리팹 풀을 하나의 래퍼에서 관리합니다.
* `UnityPool`, `PooledItem`, `PooledParticleEffect`는 Addressables 기반 풀 항목을 키로 관리합니다.
* `GameObjectPool`과 `PooledPrefabReference`는 인스턴스 초기화 및 정리를 위한 `OnGet`, `OnRelease` 콜백을 제공합니다.
* `UnityPool`은 항목의 생명주기에 맞춰 `PooledItem.OnGet()`, `PooledItem.OnRelease()`를 호출합니다.

### 관리 객체 풀링

* `ObjectPool`, `StaticPool`, `ListPool`, `RefCountObject`는 관리 객체 풀링 유틸리티를 제공합니다.

### 오디오 재생

* `AudioManager`, `AudioLibrary`, `AudioEmitter`, `AudioCue`는 풀링을 사용한 오디오 재생 기능을 제공합니다.

## 의존성

* `com.unity.addressables` 2.9.1 이상이 필요합니다.
* Addressables 기반 `AudioClipLink`에서 사용하는 `xsystem.serialization` 1.0.0 이상이 필요합니다.
* VContainer는 선택 사항입니다. `UnityPool`의 VContainer 통합 기능을 사용할 때는 VContainer를 설치해야 합니다.

## 설치

Unity Package Manager에서 다음 Git URL로 패키지를 추가합니다.

```text
https://github.com/josangjun/xsystem-unity-pool.git
```

`Packages/manifest.json`을 통해 설치할 때 `xsystem.serialization`이 프로젝트에 없다면 해당 패키지도 추가합니다.

## GameObjectPool

프리팹이 이미 `GameObject`로 준비되어 있다면 `GameObjectPool`을 사용합니다. 생성자는 Addressables를 통해 프리팹을 로드하지 않습니다.

```csharp
using UnityEngine;
using XSystem;

var pool = new GameObjectPool(prefab);
pool.OnGet += instance =>
{
    // 인스턴스가 활성화된 뒤 호출됩니다.
};
pool.OnRelease += instance =>
{
    // 인스턴스가 비활성화되기 전에 호출됩니다.
};

var instance = pool.Get(parent);
pool.Release(instance);
pool.Clear();
pool.Dispose();
```

`Get(parent)`는 새 인스턴스를 만들 때 부모를 설정합니다. 기존 인스턴스를 재사용할 때도 `OnGet`이 호출됩니다.

## PooledPrefabReference

직렬화된 Addressables 프리팹 참조와 해당 인스턴스 풀을 함께 관리하려면 `PooledPrefabReference`를 사용합니다.

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

        // 이펙트를 재생하거나 설정합니다.
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

`PooledPrefabReference`는 내부의 `AssetReferenceGameObject` 하나와 해당 작업 핸들을 소유합니다. Inspector의 접이식 항목에서 Addressable 프리팹, Default Capacity, Max Size를 설정할 수 있습니다. Default Capacity는 풀의 초기 저장 용량을 지정하며, 인스턴스를 생성하지는 않습니다. `WarmUpAsync`는 프리팹을 로드하고 요청한 수만큼 풀 인스턴스를 미리 생성할 수 있습니다. 참조가 아직 로드되지 않았다면 동기식 `Get`을 호출하기 전에 `WarmUpAsync`를 호출합니다.

`Max Size`는 반납된 인스턴스를 풀에 보관할 최대 개수입니다. 풀이 이 크기에 도달한 뒤 반납된 인스턴스는 파괴됩니다. 풀 설정은 첫 인스턴스 풀이 생성될 때 적용됩니다.

공유 루트 오브젝트 `[XSystem Pool]`은 런타임에만 존재합니다. 애플리케이션 종료 또는 Unity Editor의 Play Mode 종료 시 정리됩니다. 종료 처리 중 늦게 반납되는 인스턴스는 새 루트를 만들지 않고 파괴됩니다.

`ReleaseAsset()`은 인스턴스 풀을 비우고 Addressables 핸들을 해제합니다. 같은 참조를 다른 소유자에서 별도로 로드하지 말고, 로드와 해제 권한을 `PooledPrefabReference`에 맡기세요.

## UnityPool

`UnityPool`은 `PooledItem` 컴포넌트의 키별 풀을 관리하는 `MonoBehaviour`입니다. 직렬화된 `PooledItem` 프리팹 참조와 문자열 Addressables 키를 지원합니다.

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

초기화 과정에서 `Prepare(path, count)`를 호출하면 Addressables 기반 풀을 미리 준비할 수 있습니다. `UnityPool.Release`는 항목을 해당 키의 풀에 반납하고, `UnityPool.Clear`는 풀 인스턴스를 파괴하고 풀 상태를 해제합니다.
