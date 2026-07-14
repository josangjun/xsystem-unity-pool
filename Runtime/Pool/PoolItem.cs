using UnityEngine;

namespace XSystem
{
    [DisallowMultipleComponent]
    public class PoolItem : MonoBehaviour
    {
        public virtual void OnGet()
        {
            transform.localScale = _initialScale;
            transform.localRotation = _initialRotation;
        }
        
        public virtual void OnRelease() {}
        
        private Vector3 _initialScale;
        private Quaternion _initialRotation;
        
        protected virtual void Awake()
        {
            _initialScale = transform.localScale;
            _initialRotation = transform.localRotation;
        }
        
        public string Key { get; internal set; }
    }
}