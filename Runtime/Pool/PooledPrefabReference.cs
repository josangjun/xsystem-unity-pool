using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Pool;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace XSystem
{
    [System.Serializable]
    public class PooledPrefabReference : System.IDisposable
    {
        [SerializeField]
        private AssetReferenceGameObject _assetReference = new AssetReferenceGameObject(string.Empty);

        [SerializeField]
        private PrefabPoolSettings _settings = new PrefabPoolSettings();

        [System.NonSerialized]
        private PrefabInstancePool _pool;

        public PooledPrefabReference()
        {
        }

        public PooledPrefabReference(
            string guid,
            int defaultCapacity = PrefabPoolSettings.DefaultCapacityValue,
            int maxSize = PrefabPoolSettings.DefaultMaxSizeValue)
        {
            _assetReference = new AssetReferenceGameObject(guid);
            _settings = new PrefabPoolSettings(defaultCapacity, maxSize);
        }

        public event System.Action<GameObject> OnGet;
        public event System.Action<GameObject> OnRelease;

        public int Capacity => _settings != null ? _settings.DefaultCapacity : 0;
        public int MaxSize => _settings != null ? _settings.MaxSize : 1;

        public bool IsPrefabLoaded =>
            HasAssetReference &&
            _assetReference.IsValid() &&
            _assetReference.IsDone &&
            _assetReference.OperationHandle.Status == AsyncOperationStatus.Succeeded &&
            _assetReference.Asset is GameObject;

        public async Awaitable WarmUpAsync(int count = 0, Transform parent = null)
        {
            GameObject prefab;
            if (!HasAssetReference || !_assetReference.OperationHandle.IsValid())
            {
                prefab = await LoadPrefabAsync();
            }
            else
            {
                if (!_assetReference.OperationHandle.IsDone)
                    await _assetReference.OperationHandle.Task;

                prefab = _assetReference.Asset as GameObject;
            }

            if (prefab == null)
                return;

            List<GameObject> instances = ListPool<GameObject>.Get();
            PrefabInstancePool pool = GetOrCreatePool(prefab);
            for (int i = 0; i < count; i++)
            {
                GameObject instance = pool.Get(parent);
                if (instance != null)
                    instances.Add(instance);
            }

            for (int i = 0; i < instances.Count; i++)
                pool.Release(instances[i]);

            instances.Clear();
            ListPool<GameObject>.Release(instances);
        }

        public GameObject Get(Transform parent = null)
        {
            if (!TryGetLoadedPrefab(out GameObject prefab))
                return null;

            return GetOrCreatePool(prefab).Get(parent);
        }

        public void Release(GameObject instance)
        {
            _pool?.Release(instance);
        }

        public void Clear()
        {
            _pool?.Clear();
        }

        public void Dispose()
        {
            _pool?.Dispose();
            _pool = null;

            if (HasAssetReference && _assetReference.IsValid())
                _assetReference.ReleaseAsset();
        }

        public void ReleaseAsset()
        {
            Dispose();
        }

        private async Awaitable<GameObject> LoadPrefabAsync()
        {
            if (!HasAssetReference || !_assetReference.RuntimeKeyIsValid())
            {
                Debug.LogError("Pooled prefab reference has no valid asset assigned.");
                return null;
            }

            AsyncOperationHandle<GameObject> handle;
            if (_assetReference.IsValid())
                handle = _assetReference.OperationHandle.Convert<GameObject>();
            else
                handle = _assetReference.LoadAssetAsync();

            if (!handle.IsValid())
                return null;

            if (!handle.IsDone)
                await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Debug.LogError($"Failed to load pooled prefab reference: {_assetReference.RuntimeKey}");
                return null;
            }

            return handle.Result;
        }

        private bool TryGetLoadedPrefab(out GameObject prefab)
        {
            prefab = null;
            if (!HasAssetReference || !_assetReference.RuntimeKeyIsValid())
            {
                Debug.LogError("Pooled prefab reference has no valid asset assigned.");
                return false;
            }

            if (!_assetReference.IsValid())
            {
                _assetReference.LoadAssetAsync();
                Debug.LogWarning("Pooled prefab reference is loading. Call WarmUpAsync before Get.");
                return false;
            }

            if (!_assetReference.IsDone)
            {
                Debug.LogWarning("Pooled prefab reference is still loading. Call WarmUpAsync before Get.");
                return false;
            }

            if (_assetReference.OperationHandle.Status != AsyncOperationStatus.Succeeded ||
                !(_assetReference.Asset is GameObject loadedPrefab))
            {
                Debug.LogError($"Failed to load pooled prefab reference: {_assetReference.RuntimeKey}");
                return false;
            }

            prefab = loadedPrefab;
            return true;
        }

        private PrefabInstancePool GetOrCreatePool(GameObject prefab)
        {
            if (_pool != null)
                return _pool;

            _pool = new PrefabInstancePool(
                prefab,
                Capacity,
                MaxSize,
                HandleGet,
                HandleRelease);
            return _pool;
        }

        private void HandleGet(GameObject instance)
        {
            OnGet?.Invoke(instance);
        }

        private void HandleRelease(GameObject instance)
        {
            OnRelease?.Invoke(instance);
        }

        private bool HasAssetReference => _assetReference != null;
    }
}
