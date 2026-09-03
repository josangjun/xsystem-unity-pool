using UnityEngine;

namespace XSystem
{
    [System.Serializable]
    public sealed class PrefabPoolSettings
    {
        internal const int DefaultCapacityValue = 10;
        internal const int DefaultMaxSizeValue = 30;

        [SerializeField, Min(0)]
        private int _defaultCapacity = DefaultCapacityValue;

        [SerializeField, Min(1)]
        private int _maxSize = DefaultMaxSizeValue;

        public PrefabPoolSettings()
        {
        }

        public PrefabPoolSettings(int defaultCapacity, int maxSize)
        {
            _defaultCapacity = defaultCapacity;
            _maxSize = maxSize;
        }

        public int DefaultCapacity => Mathf.Max(0, _defaultCapacity);
        public int MaxSize => Mathf.Max(1, Mathf.Max(DefaultCapacity, _maxSize));
    }
}
