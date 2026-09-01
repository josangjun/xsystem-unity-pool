using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace XSystem
{
    [System.Serializable]
    public class PooledPrefabReference : AssetReferenceGameObject, System.IDisposable
    {
        [System.NonSerialized]
        private GameObjectPool _pool;

        [System.NonSerialized]
        private System.Action<GameObject> _onGet;

        [System.NonSerialized]
        private System.Action<GameObject> _onRelease;

        public PooledPrefabReference() : base(string.Empty)
        {
        }

        public PooledPrefabReference(string guid) : base(guid)
        {
        }

        public event System.Action<GameObject> OnGet
        {
            add
            {
                _onGet += value;
                if (_pool != null)
                    _pool.OnGet += value;
            }
            remove
            {
                _onGet -= value;
                if (_pool != null)
                    _pool.OnGet -= value;
            }
        }

        public event System.Action<GameObject> OnRelease
        {
            add
            {
                _onRelease += value;
                if (_pool != null)
                    _pool.OnRelease += value;
            }
            remove
            {
                _onRelease -= value;
                if (_pool != null)
                    _pool.OnRelease -= value;
            }
        }

        public bool IsPrefabLoaded =>
            IsValid() &&
            IsDone &&
            OperationHandle.Status == AsyncOperationStatus.Succeeded &&
            Asset is GameObject;

        public async Awaitable WarmUpAsync(int count = 0)
        {
            GameObject prefab;
            if (OperationHandle.IsValid() == false)
            {
                prefab = await LoadPrefabAsync();
            }
            else
            {
                if (OperationHandle.IsDone == false)
                {
                    await OperationHandle.Task;
                }
                prefab = Asset as GameObject;
            }
            if (prefab == null)
                return;

            var pool = GetOrCreatePool(prefab);
            for (var i = 0; i < count; i++)
            {
                var go = pool.Get();
                pool.Release(go);
            }
        }

        public GameObject Get(Transform parent = null)
        {
            if (!TryGetLoadedPrefab(out var prefab))
                return null;

            return GetOrCreatePool(prefab).Get(parent);
        }

        public void Release(GameObject go)
        {
            if (_pool != null)
                _pool.Release(go);
        }

        public void Clear()
        {
            if (_pool != null)
                _pool.Clear();
        }

        public void Dispose()
        {
            if (_pool != null)
            {
                _pool.Dispose();
                _pool = null;
            }

            if (IsValid())
                base.ReleaseAsset();
        }

        public override void ReleaseAsset()
        {
            Dispose();
        }

        private async Awaitable<GameObject> LoadPrefabAsync()
        {
            if (!RuntimeKeyIsValid())
            {
                Debug.LogError("Pooled prefab reference has no valid asset assigned.");
                return null;
            }

            AsyncOperationHandle<GameObject> handle;
            if (IsValid())
                handle = OperationHandle.Convert<GameObject>();
            else
                handle = LoadAssetAsync();

            if (!handle.IsValid())
                return null;

            if (!handle.IsDone)
                await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Debug.LogError($"Failed to load pooled prefab reference: {RuntimeKey}");
                return null;
            }

            return handle.Result;
        }

        private bool TryGetLoadedPrefab(out GameObject prefab)
        {
            prefab = null;
            if (!RuntimeKeyIsValid())
            {
                Debug.LogError("Pooled prefab reference has no valid asset assigned.");
                return false;
            }

            if (!IsValid())
            {
                LoadAssetAsync();
                Debug.LogWarning("Pooled prefab reference is loading. Call WarmUpAsync before Get.");
                return false;
            }

            if (!IsDone)
            {
                Debug.LogWarning("Pooled prefab reference is still loading. Call WarmUpAsync before Get.");
                return false;
            }

            if (OperationHandle.Status != AsyncOperationStatus.Succeeded || !(Asset is GameObject loadedPrefab))
            {
                Debug.LogError($"Failed to load pooled prefab reference: {RuntimeKey}");
                return false;
            }

            prefab = loadedPrefab;
            return true;
        }

        private GameObjectPool GetOrCreatePool(GameObject prefab)
        {
            if (_pool != null)
                return _pool;

            _pool = new GameObjectPool(prefab);
            _pool.OnGet += _onGet;
            _pool.OnRelease += _onRelease;
            return _pool;
        }
    }
}
