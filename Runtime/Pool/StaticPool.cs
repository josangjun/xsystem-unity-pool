
namespace XSystem
{
    public class StaticPool<T> where T : new()
    {
        protected static ObjectPool<T> _pool = new();
        
        public static int Capacity {
            get => _pool.Capacity;
            set => _pool.Capacity = value;
        }

        public static T Get()
        {
            return _pool.Get();
        }

        public static void Release(T item)
        {
            _pool.Release(item);
        }

        public static System.IDisposable Get(out T item)
        {
            return _pool.Get(out item);
        }

        public static void Clear()
        {
#if UNITY_EDITOR
            //Logger.Log($"StaticPool.Clear:{typeof(T).Name}, {_pool.Count}");
#endif
            _pool.Clear();
        }
    }
}