using UnityEngine;
using UnityEngine.Pool;

namespace XSystem
{
    [System.Serializable]
    public class GameObjectPool : System.IDisposable
    {
        private const int DefaultCapacity = 10;
        private const int DefaultMaxSize = 30;
        private const string SharedPoolRootName = "[XSystem Pool]";

#if UNITY_EDITOR
        private static readonly System.Collections.Generic.HashSet<GameObjectPool> s_activePools =
            new System.Collections.Generic.HashSet<GameObjectPool>();

        internal static System.Collections.Generic.IEnumerable<GameObjectPool> ActivePools => s_activePools;
#endif

        [System.NonSerialized]
        private static Transform _sharedPoolRoot;

        [SerializeField]
        private GameObject _prefab;

        [SerializeField, Min(0)]
        private int _defaultCapacity = DefaultCapacity;

        [SerializeField, Min(1)]
        private int _maxSize = DefaultMaxSize;

        [System.NonSerialized]
        private ObjectPool<GameObject> _pool;

#if UNITY_EDITOR
        [System.NonSerialized]
        private System.Collections.Generic.HashSet<GameObject> _idleObjects;
#endif

        public event System.Action<GameObject> OnGet;
        public event System.Action<GameObject> OnRelease;

        [System.NonSerialized]
        private Transform _parent;

        public GameObjectPool()
        {
        }

        public GameObjectPool(GameObject prefab, int defaultCapacity = DefaultCapacity, int maxSize = DefaultMaxSize)
        {
            _prefab = prefab;
            _defaultCapacity = defaultCapacity;
            _maxSize = maxSize;
            EnsurePool();
        }

        public GameObject Get(Transform parent = null)
        {
            if (!EnsurePool())
                return null;

            _parent = parent;
            return _pool.Get();
        }

        public void Release(GameObject go)
        {
            if (go == null || _pool == null)
            {
                Object.Destroy(go);
                return;
            }

            _parent = GetSharedPoolRoot();
            _pool.Release(go);
        }

        public void Clear()
        {
            _pool?.Clear();
#if UNITY_EDITOR
            _idleObjects?.Clear();
#endif
        }

        private bool _isDisposed = false;

        public void Dispose()
        {
            _pool?.Clear();
            _pool = null;
            _isDisposed = true;
#if UNITY_EDITOR
            _idleObjects?.Clear();
            s_activePools.Remove(this);
#endif
        }

        private bool EnsurePool()
        {
            if (_pool != null)
                return true;

            if (_prefab == null)
            {
                Debug.LogError("GameObjectPool requires a prefab asset.");
                return false;
            }

            if (_isDisposed)
            {
                Debug.LogError("GameObjectPool has been disposed and cannot be used.");
                return false;
            }

            var defaultCapacity = Mathf.Max(0, _defaultCapacity);
            var maxSize = Mathf.Max(defaultCapacity, _maxSize);

            _pool = new(CreateInstance,
                HandleGet,
                HandleRelease,
                HandleOnDestroy,
                #if UNITY_EDITOR
                collectionCheck: true,
                #else
                collectionCheck: false,
                #endif
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
#if UNITY_EDITOR
            s_activePools.Add(this);
#endif
            return true;
        }

        private GameObject CreateInstance()
        {
            var go = Object.Instantiate(_prefab, _parent);
            return go;
        }

        private static Transform GetSharedPoolRoot()
        {
            if (_sharedPoolRoot != null)
            {
#if UNITY_EDITOR
                EnsureDebugView(_sharedPoolRoot);
#endif
                return _sharedPoolRoot;
            }

            var poolRoot = new GameObject(SharedPoolRootName);
#if UNITY_EDITOR
            poolRoot.AddComponent<GameObjectPoolDebugView>();
#endif
            _sharedPoolRoot = poolRoot.transform;
            return _sharedPoolRoot;
        }

#if UNITY_EDITOR
        private static void EnsureDebugView(Transform poolRoot)
        {
            if (!poolRoot.TryGetComponent<GameObjectPoolDebugView>(out _))
                poolRoot.gameObject.AddComponent<GameObjectPoolDebugView>();
        }
#endif

        private void HandleGet(GameObject go)
        {
#if UNITY_EDITOR
            _idleObjects?.Remove(go);
#endif
            go.SetActive(true);
            go.transform.SetParent(_parent);
            go.GetComponent<PooledItem>()?.OnGet();
            OnGet?.Invoke(go);
        }

        private void HandleRelease(GameObject go)
        {
#if UNITY_EDITOR
            GetIdleObjects().Add(go);
#endif
            go.GetComponent<PooledItem>()?.OnRelease();
            OnRelease?.Invoke(go);
            go.transform.SetParent(_parent);
            go.SetActive(false);
        }

        private void HandleOnDestroy(GameObject go)
        {
#if UNITY_EDITOR
            _idleObjects?.Remove(go);
#endif
            Object.Destroy(go);
        }

#if UNITY_EDITOR
        internal GameObject Prefab => _prefab;
        internal int ConfiguredDefaultCapacity => _defaultCapacity;
        internal int MaxSize => Mathf.Max(_defaultCapacity, _maxSize);
        internal int Size => _pool?.CountAll ?? 0;
        internal int ActiveCount => _pool?.CountActive ?? 0;
        internal int IdleCount => _pool?.CountInactive ?? 0;
        internal int IdleObjectCount => _idleObjects?.Count ?? 0;
        internal System.Collections.Generic.IEnumerable<GameObject> IdleObjects =>
            _idleObjects != null
                ? (System.Collections.Generic.IEnumerable<GameObject>)_idleObjects
                : System.Array.Empty<GameObject>();

        private System.Collections.Generic.HashSet<GameObject> GetIdleObjects()
        {
            return _idleObjects ??= new System.Collections.Generic.HashSet<GameObject>();
        }
#endif
    }
}

#if UNITY_EDITOR
namespace XSystem
{
    using UnityEditor;

    [CustomPropertyDrawer(typeof(GameObjectPool))]
    public class GameObjectPoolDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            return EditorGUIUtility.singleLineHeight * 4f + Spacing * 3f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var prefabProperty = property.FindPropertyRelative("_prefab");
            var defaultCapacityProperty = property.FindPropertyRelative("_defaultCapacity");
            var maxSizeProperty = property.FindPropertyRelative("_maxSize");

            EditorGUI.BeginProperty(position, label, property);

            var linePosition = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(linePosition, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            if (prefabProperty == null || defaultCapacityProperty == null || maxSizeProperty == null)
            {
                linePosition.y += EditorGUIUtility.singleLineHeight + Spacing;
                EditorGUI.LabelField(linePosition, "Invalid GameObjectPool data");
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;

            linePosition.y += EditorGUIUtility.singleLineHeight + Spacing;
            EditorGUI.PropertyField(linePosition, prefabProperty, new GUIContent("Prefab"));

            linePosition.y += EditorGUIUtility.singleLineHeight + Spacing;
            EditorGUI.PropertyField(linePosition, defaultCapacityProperty, new GUIContent("Default Capacity"));

            linePosition.y += EditorGUIUtility.singleLineHeight + Spacing;
            EditorGUI.PropertyField(linePosition, maxSizeProperty, new GUIContent("Max Size"));

            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }
    }
}
#endif
