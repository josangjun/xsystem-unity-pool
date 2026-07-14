using System.Collections.Generic;

namespace XSystem
{
    public class ObjectPool<T> : IObjectPool<T>
    {
        private List<T> _instances = new();

        private readonly bool _refCounting;
        private readonly System.Func<T> _createFunc;

        public ObjectPool(System.Func<T> createFunc = null)
        {
            _refCounting = typeof(IRefCount).IsAssignableFrom(typeof(T));
            _createFunc = createFunc;
            InitInternal();
        }
        
        #if FINAL_BUILD
        private readonly SortedSet<int> hashSet = new();
        
        private void InitInternal()
        {
            _onRelease += item => {
                if (hashSet.Contains(item.GetHashCode()))
                    Error(item, "item already exists.");
                hashSet.Add(item.GetHashCode());
            };
            _onGet += item => {
                if (hashSet.Remove(item.GetHashCode()) == false)
                    Error(item, "item does not exist.");
            };
            _onClear += item => hashSet.Clear();
        }
        
        private static void Error(T item, string str) =>
            throw new System.InvalidOperationException($"[Pool({typeof(T).Name})] {str}");
        #else
        [System.Diagnostics.Conditional("__UNDEFINED__")]
        void InitInternal() {}
        #endif
        
        System.Action<T> _onGet, _onRelease;
        System.Action _onClear;
        
        public event System.Action<T> OnGet
        {
            add => _onGet += value;
            remove => _onGet -= value;
        }
        
        public event System.Action<T> OnRelease
        {
            add => _onRelease += value;
            remove => _onRelease -= value;
        }
        
        public int Count => _instances.Count;
        
        public void Clear()
        {
            _onClear?.Invoke();
            if (_instances != null)
            {
                _instances.Clear();
                _instances.Capacity = Capacity;
            }
        }

        private int _capacity = 128;
        
        public int Capacity {
            get => _capacity;
            set {
                _capacity = value;
                if (_instances != null)
                {
                    while (_instances.Count > value)
                        Get();
                    _instances.Capacity = value;
                }
            }
        }

        public T Get()
        {
            T item;
            if (_instances.Count > 0)
            {
                var last = _instances.Count - 1;
                item = _instances[last];
                _instances.RemoveAt(last);
                _onGet?.Invoke(item);
            }
            else
            {
                item = _createFunc();
            }
            return item;
        }

        public System.IDisposable Get(out T item)
        {
            item = Get();
            return new PoolHandle<T>(this, item, _refCounting);
        }

        public void Release(T item)
        {
            if (_instances.Count > Capacity)
            {
                return;
            }
            _onRelease?.Invoke(item);
            _instances.Add(item);
        }
    }

    public struct PoolHandle<T> : System.IDisposable
    {
        public T value;
        private readonly IObjectPool<T> _pool;
        private readonly bool _refCounting;

        public PoolHandle(IObjectPool<T> pool, T item, bool refCounting = false)
        {
            _pool = pool;
            _refCounting = refCounting;
            value = item;
        }

        public readonly void Dispose()
        {
            if (_refCounting)
            {
                ((IRefCount)value).Release();
                return;
            }
            _pool.Release(value);
        }
    }
}