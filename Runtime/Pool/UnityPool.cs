using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

#if HAS_VCONTAINER
using VContainer.Unity;
#endif
namespace XSystem
{
    internal class ItemStack : Stack<PoolItem>
    {
        public ItemStack(string key, PoolItem asset)
        {
            Key = key;
            Asset = asset;
        }
        public ItemStack(string key)
        {
            Key = key;
        }
        public string Key { get; private set;}
        private PoolItem Asset;
        private AsyncOperationHandle<GameObject> OperationHandle;
        
        public T LoadAsset<T>() where T : PoolItem
        {
            if (Asset != null)
                return (T)Asset;
            
            if (OperationHandle.IsValid())
            {
                Addressables.Release(OperationHandle);
            }
            OperationHandle = Addressables.LoadAssetAsync<GameObject>(Key);
            var go = OperationHandle.WaitForCompletion();
            if (OperationHandle.Status != AsyncOperationStatus.Succeeded || go == null)
            {
                Debug.LogError($"Failed to load addressable: {Key}");
                Addressables.Release(OperationHandle);
                OperationHandle = default;
                return default;
            }

            Asset = go.GetComponent<PoolItem>();
            if (Asset == null)
            {
                Addressables.Release(OperationHandle);
                OperationHandle = default;
                return default;
            }
            try {
                return (T)Asset;
            } catch (System.InvalidCastException) {
                Debug.LogError($"Failed to cast asset to {typeof(T)}: {Key}");
                return default;
            }
        }

        public async Awaitable<T> LoadAssetAsync<T>(CancellationToken cts = default) where T : PoolItem
        {
            if (Asset != null)
                return (T)Asset;
            
            if (OperationHandle.IsValid())
            {
                Addressables.Release(OperationHandle);
            }
            OperationHandle = Addressables.LoadAssetAsync<GameObject>(Key);
            await OperationHandle.Task;
            if (cts.IsCancellationRequested)
            {
                Addressables.Release(OperationHandle);
                OperationHandle = default;
                throw new System.OperationCanceledException($"the operation is canceled. {Key}");
            }

            if (OperationHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"Failed to load addressable: {Key}");
                Addressables.Release(OperationHandle);
                OperationHandle = default;
                return default;
            }
            var prefab = OperationHandle.Result;
            Asset = prefab.GetComponent<PoolItem>();
            if (Asset == null)
            {
                Debug.LogError($"Failed to get component {typeof(T)} from prefab {Key}");
                Addressables.Release(OperationHandle);
                OperationHandle = default;
                return default;
            }
            try {
                return (T)Asset;
            } catch (System.InvalidCastException) {
                Debug.LogError($"Failed to cast asset to {typeof(T)}: {Key}");
                return default;
            }
        }
        
        public void Release()
        {
            if (OperationHandle.IsValid())
            {
                Addressables.Release(OperationHandle);
                OperationHandle = default;
            }
            Asset = null;
        }
    }
    
    [DisallowMultipleComponent]
    public class UnityPool : MonoBehaviour, System.IDisposable
    {
#if HAS_VCONTAINER
        
        public VContainer.IObjectResolver Container { get; private set; }
        
        public void Construct(VContainer.IObjectResolver resolver)
        {
            Container = resolver;
        }
                
        internal T InstantiateT<T>(T prefab, Transform parent, bool worldPositionStays = false) where T : PoolItem
        {
            var item = Container.Instantiate(prefab, parent, worldPositionStays);
            OnGet(item);
            return item;
        }
#else
        
        internal T InstantiateT<T>(T prefab, Transform parent, bool worldPositionStays = false) where T : PoolItem
        {
            var item = UnityEngine.Object.Instantiate(prefab, parent, worldPositionStays);
            OnGet(item);
            return item;
        }
        
#endif
        
        public int Capacity { get; set; } = 50;
        
        private readonly Dictionary<string, ItemStack> _pool = new();
        
        internal void OnGet(PoolItem item) {
            item.gameObject.SetActive(true);
        }
        
        private void OnRelease(PoolItem item) {
            item.transform.SetParent(transform);
            item.gameObject.SetActive(false);
        }

        public T Get<T>(T prefab, Transform parent = null) where T : PoolItem
        {
            var key = prefab.Key;
            if (string.IsNullOrEmpty(key))
            {
                key = $"@{prefab.name}:{prefab.GetInstanceID()}";
                prefab.Key = key;
            }
            
            if (_pool.TryGetValue(key, out var stack))
            {
                if (stack.Count > 0)
                {
                    var i = stack.Pop();
                    OnGet(i);
                    try {
                        return (T)i;
                    } catch {
                        OnRelease(i);
                        Debug.LogError($"Failed to cast pooled item to {typeof(T)}");
                        return default;
                    }
                }
            }
            else
            {
                stack = new ItemStack(key, prefab);
                _pool[key] = stack;
            }
            var item = Create<T>(stack, parent);
            return item;
        }

        public T Get<T>(string path, Transform parent = null) where T : PoolItem
        {
            if (_pool.TryGetValue(path, out var stack))
            {
                if (stack.Count > 0)
                {
                    var i = stack.Pop();
                    OnGet(i);
                    try {
                        return (T)i;
                    } catch {
                        OnRelease(i);
                        Debug.LogError($"Failed to cast pooled item to {typeof(T)}");
                        return default;
                    }
                }
            }
            else
            {
                stack = new ItemStack(path);
                _pool[path] = stack;
            }
            
            var item = Create<T>(stack, parent);
            return item;
        }
        
        private T Create<T>(ItemStack stack, Transform parent = null) where T : PoolItem
        {
            T item = null;
            try
            {
                var asset = stack.LoadAsset<T>();
                if (asset != null)
                {
                    item = InstantiateT(asset, parent);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load asset {stack.Key}: {ex}");
            }
            
            if (item == null)
            {
                _pool.Remove(stack.Key);
            }
            return item;
        }
        
        private async Awaitable<T> CreateAsync<T>(ItemStack stack, Transform parent = null, CancellationToken cts = default) where T : PoolItem
        {
            T item = null;
            try
            {
                var asset = await stack.LoadAssetAsync<T>(cts);
                
                if (asset != null)
                {
                    item = InstantiateT(asset, parent);
                }
            }
            catch (System.OperationCanceledException)
            {
                return default;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load asset {stack.Key}: {ex}");
            }
            
            if (item == null)
            {
                _pool.Remove(stack.Key);
            }
            return item;
        }
        
        public async Awaitable Prepare(string path, int count)
        {
            var finished = 0;
            async Awaitable WaitAndRelease(Awaitable<PoolItem> t)
            {
                try
                {
                    var item = await t;
                    if (item != null)
                        Release(item);
                }
                finally
                {
                    ++finished;
                }
            }
            var stack = new ItemStack(path);
            _pool[path] = stack;
            for (int i = 0; i < count; i++)
            {
                var item = CreateAsync<PoolItem>(stack);
                _ = WaitAndRelease(item);
            }
            while (finished < count)
                await Awaitable.NextFrameAsync();
        }

        public void Destroy(PoolItem obj)
        {
            if (obj == null)
                return;
            Destroy(obj.gameObject);
        }
        
        public async Awaitable<T> GetAsync<T>(string path, Transform parent = null, CancellationToken cts = default) where T  : PoolItem
        {
            T item;
            if (_pool.TryGetValue(path, out var stack))
            {
                if (stack.Count > 0)
                {
                    item = (T)stack.Pop();
                    OnGet(item);
                    return item;
                }
            }
            else
            {
                stack = new ItemStack(path);
                _pool[path] = stack;
            }
            
            item = await CreateAsync<T>(stack, parent, cts);
            
            return item;
        }
        
        public void Release(PoolItem item)
        {
            OnRelease(item);
            var key = item.name;
            if (!_pool.TryGetValue(key, out var stack))
            {
                Destroy(item.gameObject);
                return;
            }
            if (stack.Count >= Capacity)
            {
                Destroy(item.gameObject);
                return;
            }
            stack.Push(item);
        }
        
        public void Clear()
        {
            foreach (var kv in _pool)
            {
                var stack = kv.Value;
                while (stack.Count > 0)
                {
                    var item = stack.Pop();
                    Destroy(item.gameObject);
                }
                
                stack.Release();
            }
            _pool.Clear();
        }
        
        public void Dispose()
        {
            Clear();
        }
        
        public void OnDestroy()
        {
            Clear();
        }
    }
}
