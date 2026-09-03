using UnityEngine;

namespace XSystem
{
    [System.Serializable]
    public class GameObjectPool : System.IDisposable
    {
        [SerializeField]
        private GameObject _prefab;

        [SerializeField]
        private PrefabPoolSettings _settings = new PrefabPoolSettings();

        [System.NonSerialized]
        private PrefabInstancePool _pool;

        [System.NonSerialized]
        private bool _isDisposed;

        public event System.Action<GameObject> OnGet;
        public event System.Action<GameObject> OnRelease;

        public GameObjectPool()
        {
        }

        public GameObjectPool(
            GameObject prefab,
            int defaultCapacity = PrefabPoolSettings.DefaultCapacityValue,
            int maxSize = PrefabPoolSettings.DefaultMaxSizeValue)
        {
            _prefab = prefab;
            _settings = new PrefabPoolSettings(defaultCapacity, maxSize);
            EnsurePool();
        }

        public int Capacity
        {
            get => _settings != null ? _settings.DefaultCapacity : 0;
            set
            {
                if (_pool != null)
                {
                    Debug.LogError("GameObjectPool capacity cannot be changed after the pool has been initialized.");
                    return;
                }

                if (_settings == null)
                    _settings = new PrefabPoolSettings();

                _settings = new PrefabPoolSettings(value, MaxSize);
            }
        }

        public int MaxSize => _settings != null ? _settings.MaxSize : 1;

        public GameObject Get(Transform parent = null)
        {
            if (!EnsurePool())
                return null;

            return _pool.Get(parent);
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
                return;

            if (_pool == null)
            {
                Object.Destroy(instance);
                return;
            }

            _pool.Release(instance);
        }

        public void Clear()
        {
            _pool?.Clear();
        }

        public void Dispose()
        {
            _pool?.Dispose();
            _pool = null;
            _isDisposed = true;
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

            _pool = new PrefabInstancePool(
                _prefab,
                Capacity,
                MaxSize,
                HandleGet,
                HandleRelease);
            return true;
        }

        private void HandleGet(GameObject instance)
        {
            OnGet?.Invoke(instance);
        }

        private void HandleRelease(GameObject instance)
        {
            OnRelease?.Invoke(instance);
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
            SerializedProperty prefabProperty = property.FindPropertyRelative("_prefab");
            SerializedProperty settingsProperty = property.FindPropertyRelative("_settings");
            SerializedProperty defaultCapacityProperty = settingsProperty?.FindPropertyRelative("_defaultCapacity");
            SerializedProperty maxSizeProperty = settingsProperty?.FindPropertyRelative("_maxSize");

            EditorGUI.BeginProperty(position, label, property);

            Rect linePosition = new Rect(
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
