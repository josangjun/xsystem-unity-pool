
namespace XSystem
{
    public interface IRefCount : System.IDisposable
    {
        int RefCount { get; }
        void Retain();
        void Release();
    }
    
    public abstract class RefCountObject : IRefCount, System.IDisposable
    {
        public int RefCount { get; private set; }

        public void Retain()
        {
            ++RefCount;
        }

        public void Release()
        {
            --RefCount;
            if (RefCount == 0)
            {
                Dispose();
            }
        }
        
        void System.IDisposable.Dispose()
        {
            Release();
        }

        public void Dispose()
        {
            OnDispose();
        }

        protected abstract void OnDispose();
    }
}