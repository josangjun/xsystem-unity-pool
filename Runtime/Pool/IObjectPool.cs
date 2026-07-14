
namespace XSystem
{
    public interface IObjectPool<T> {
        T Get();
        void Release(T item);
        void Clear();
    }
}