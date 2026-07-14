using System.Collections.Generic;

namespace XSystem
{
    public class ListPool<T> : StaticPool<List<T>> where T : new()
    {
        static ListPool()
        {
            _pool.OnRelease += OnRelease;
        }

        private static void OnRelease(List<T> list)
        {
            list.Clear();
        }
    }
}