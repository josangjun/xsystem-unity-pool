using UnityEngine;
using UnityEngine.Pool;

namespace XSystem
{
    public class GameObjectPool : System.IDisposable
    {
        private ObjectPool<GameObject> _pool;

        public event System.Action<GameObject> OnGet;
        public event System.Action<GameObject> OnRelease;

        private GameObject _prefab;
        private Transform _parent;
        
        public GameObjectPool(GameObject prefab, int defaultCapacity = 50, int maxSize = 100)
        {
            _prefab = prefab;
            _pool = new(CreateInstance,
                HandleGet,
                HandleRelease,
                HandleOnDestroy,
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }
        
        public GameObject Get(Transform parent = null)
        {
            _parent = parent;
            return _pool.Get();
        }
        
        public void Release(GameObject go)
        {
            _pool.Release(go);
        }
        
        public void Clear()
        {
            _pool.Clear();
        }
        
        public void Dispose()
        {
            _pool.Clear();
            _prefab = null;
        }
        
        private GameObject CreateInstance()
        {
            var go = Object.Instantiate(_prefab, _parent);
            return go;
        }
        
        private void HandleGet(GameObject go)
        {
            go.SetActive(true);
            OnGet?.Invoke(go);
        }
        
        private void HandleRelease(GameObject go)
        {
            OnRelease?.Invoke(go);
            go.SetActive(false);
        }
        
        private void HandleOnDestroy(GameObject go)
        {
            Object.Destroy(go);
        }
    }
}
