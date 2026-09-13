using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace XSystem
{
    internal sealed class PrefabInstancePool : IDisposable
    {
        private const string SharedPoolRootName = "[XSystem Pool]";

#if UNITY_EDITOR
        private static readonly HashSet<PrefabInstancePool> s_activePools =
            new HashSet<PrefabInstancePool>();

        internal static IEnumerable<PrefabInstancePool> ActivePools => s_activePools;
#endif

        [NonSerialized]
        private static Transform _sharedPoolRoot;

        private static bool s_isShuttingDown;

        private readonly GameObject _prefab;
        private readonly int _defaultCapacity;
        private readonly int _maxSize;
        private readonly Action<GameObject> _onGet;
        private readonly Action<GameObject> _onRelease;
        private readonly ObjectPool<GameObject> _pool;

#if UNITY_EDITOR
        private readonly HashSet<GameObject> _idleObjects = new HashSet<GameObject>();
#endif

        private Transform _parent;
        private bool _isDisposed;

        internal PrefabInstancePool(
            GameObject prefab,
            int defaultCapacity,
            int maxSize,
            Action<GameObject> onGet,
            Action<GameObject> onRelease)
        {
            _prefab = prefab;
            _defaultCapacity = Mathf.Max(0, defaultCapacity);
            _maxSize = Mathf.Max(1, Mathf.Max(_defaultCapacity, maxSize));
            _onGet = onGet;
            _onRelease = onRelease;
            _pool = new ObjectPool<GameObject>(
                CreateInstance,
                HandleGet,
                HandleRelease,
                HandleDestroy,
#if UNITY_EDITOR
                collectionCheck: true,
#else
                collectionCheck: false,
#endif
                defaultCapacity: _defaultCapacity,
                maxSize: _maxSize);

#if UNITY_EDITOR
            s_activePools.Add(this);
#endif
        }

        internal GameObject Get(Transform parent)
        {
            if (_isDisposed)
            {
                Debug.LogError("Prefab instance pool has been disposed and cannot be used.");
                return null;
            }

#if UNITY_EDITOR
            s_activePools.Add(this);
#endif
            _parent = parent;
            return _pool.Get();
        }

        internal void Release(GameObject instance)
        {
            if (instance == null)
                return;

            if (_isDisposed || IsShuttingDown)
            {
                ReleaseAndDestroy(instance);
                return;
            }

            Transform sharedPoolRoot = GetSharedPoolRoot();
            if (sharedPoolRoot == null)
            {
                ReleaseAndDestroy(instance);
                return;
            }

            _parent = sharedPoolRoot;
            _pool.Release(instance);
        }

        internal void Clear()
        {
            if (_isDisposed)
                return;

            _pool.Clear();
#if UNITY_EDITOR
            _idleObjects.Clear();
#endif
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _pool.Clear();
            _isDisposed = true;
#if UNITY_EDITOR
            _idleObjects.Clear();
            s_activePools.Remove(this);
#endif
        }

        private GameObject CreateInstance()
        {
            return UnityEngine.Object.Instantiate(_prefab, _parent);
        }

        private void HandleGet(GameObject instance)
        {
#if UNITY_EDITOR
            _idleObjects.Remove(instance);
#endif
            instance.SetActive(true);
            instance.transform.SetParent(_parent);
            instance.GetComponent<PooledItem>()?.OnGet();
            _onGet?.Invoke(instance);
        }

        private void HandleRelease(GameObject instance)
        {
#if UNITY_EDITOR
            _idleObjects.Add(instance);
#endif
            instance.GetComponent<PooledItem>()?.OnRelease();
            _onRelease?.Invoke(instance);
            instance.transform.SetParent(_parent);
            instance.SetActive(false);
        }

        private void HandleDestroy(GameObject instance)
        {
#if UNITY_EDITOR
            _idleObjects.Remove(instance);
#endif
            DestroyOwnedObject(instance);
        }

        private void ReleaseAndDestroy(GameObject instance)
        {
            instance.GetComponent<PooledItem>()?.OnRelease();
            _onRelease?.Invoke(instance);
            HandleDestroy(instance);
        }

        private static Transform GetSharedPoolRoot()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying || s_isShuttingDown)
                return null;
#else
            if (s_isShuttingDown)
                return null;
#endif

            if (_sharedPoolRoot != null)
            {
#if UNITY_EDITOR
                EnsureDebugView(_sharedPoolRoot);
#endif
                return _sharedPoolRoot;
            }

            GameObject poolRoot = new GameObject(SharedPoolRootName);
#if UNITY_EDITOR
            poolRoot.AddComponent<GameObjectPoolDebugView>();
#endif
            _sharedPoolRoot = poolRoot.transform;
            return _sharedPoolRoot;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _sharedPoolRoot = null;
            s_isShuttingDown = false;
#if UNITY_EDITOR
            s_activePools.Clear();
#endif
        }

        [RuntimeInitializeOnLoadMethod]
        private static void RegisterApplicationQuitting()
        {
            Application.quitting -= HandleApplicationQuitting;
            Application.quitting += HandleApplicationQuitting;
        }

        private static void HandleApplicationQuitting()
        {
            s_isShuttingDown = true;
            DestroySharedPoolRoot();
        }

        private static bool IsShuttingDown => s_isShuttingDown;

        private static void DestroySharedPoolRoot()
        {
            Transform poolRoot = _sharedPoolRoot;
            _sharedPoolRoot = null;
            if (poolRoot != null)
                DestroyOwnedObject(poolRoot.gameObject);
        }

        private static void DestroyOwnedObject(GameObject instance)
        {
            if (instance == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                return;
            }
#endif
            UnityEngine.Object.Destroy(instance);
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorLifecycle()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                s_isShuttingDown = true;
                DestroySharedPoolRoot();
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_isShuttingDown = false;
                _sharedPoolRoot = null;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                s_isShuttingDown = false;
                _sharedPoolRoot = null;
                s_activePools.Clear();
            }
        }

        private static void EnsureDebugView(Transform poolRoot)
        {
            if (!poolRoot.TryGetComponent<GameObjectPoolDebugView>(out _))
                poolRoot.gameObject.AddComponent<GameObjectPoolDebugView>();
        }

        internal GameObject Prefab => _prefab;
        internal int ConfiguredDefaultCapacity => _defaultCapacity;
        internal int MaxSize => _maxSize;
        internal int Size => _isDisposed ? 0 : _pool.CountAll;
        internal int ActiveCount => _isDisposed ? 0 : _pool.CountActive;
        internal int IdleCount => _isDisposed ? 0 : _pool.CountInactive;
        internal int IdleObjectCount => _idleObjects.Count;
        internal IEnumerable<GameObject> IdleObjects => _idleObjects;
#endif
    }
}
