using UnityEngine;
using UnityEngine.Pool;

namespace XSystem
{
    [System.Serializable]
    public class GameObjectPool : System.IDisposable
    {
        private const int DefaultCapacity = 50;
        private const int DefaultMaxSize = 100;
        private const string SharedPoolRootName = "[XSystem Pool]";

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
                return;

            _parent = GetSharedPoolRoot();
            _pool.Release(go);
        }

        public void Clear()
        {
            _pool?.Clear();
        }

        public void Dispose()
        {
            _pool?.Clear();
            _pool = null;
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
                return _sharedPoolRoot;

            var poolRoot = new GameObject(SharedPoolRootName);
            _sharedPoolRoot = poolRoot.transform;
            return _sharedPoolRoot;
        }

        private void HandleGet(GameObject go)
        {
            go.SetActive(true);
            go.transform.SetParent(_parent);
            go.GetComponent<PooledItem>()?.OnGet();
            OnGet?.Invoke(go);
        }

        private void HandleRelease(GameObject go)
        {
            go.GetComponent<PooledItem>()?.OnRelease();
            OnRelease?.Invoke(go);
            go.transform.SetParent(_parent);
            go.SetActive(false);
        }

        private void HandleOnDestroy(GameObject go)
        {
            Object.Destroy(go);
        }
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
