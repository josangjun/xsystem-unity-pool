// * AudioManager.cs
// --------------
// 오디오 패키지 및 오디오 클립의 로드, 재생, 중지, 해제 등 오디오 전반을 관리하는 매니저 클래스입니다.
// 주요 기능:
// - Addressables 기반의 비동기 오디오 패키지/클립 로딩 및 준비 지원
// - 오디오 플레이어 풀링, 믹서 그룹, 프리셋, 중복/덮어쓰기/오버랩 제어 등 다양한 오디오 제어 기능 제공
// - AudioEmitterHandle을 통한 비동기 재생 결과 및 콜백, Awaiter 패턴 지원
// - UI, 게임, 효과음 등 다양한 상황에서 효율적이고 유연한 오디오 관리 가능

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace XSystem
{
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour, System.IDisposable
    {
        private ObjectPool<AudioEmitter> _pool;
        
        [SerializeField]
        private AssetReferenceT<AudioLibrary>[] _libraryRefs = {};

        private List<AudioLibrary> _libraries = new List<AudioLibrary>();
        private int _pendingLibraryLoads;

        public event System.Action LibrariesLoaded;

        public bool IsLibrariesLoaded { get; private set; }
        
        private Dictionary<string, AsyncOperationHandle> _handles = new();
        
        protected virtual void Awake()
        {
            _pool = new ObjectPool<AudioEmitter>(CreateEmitter);
            _pool.OnRelease += OnRelease;
            _pool.OnGet += OnGet;
        }
        
        public void Start()
        {
            IsLibrariesLoaded = false;
            _pendingLibraryLoads = 0;
            foreach (var libraryRef in _libraryRefs)
            {
                if (libraryRef.RuntimeKeyIsValid())
                {
                    ++_pendingLibraryLoads;
                }
            }

            if (_pendingLibraryLoads == 0)
            {
                MarkLibrariesLoaded();
                return;
            }

            foreach (var libraryRef in _libraryRefs)
            {
                if (libraryRef.RuntimeKeyIsValid())
                    _ = LoadLibrary(libraryRef.AssetGUID);
            }
        }
        
        public AsyncOperationHandle LoadLibrary(string address)
        {
            var handle = Addressables.LoadAssetAsync<AudioLibrary>(address);
            handle.Completed += h => {
                if (handle.Status == AsyncOperationStatus.Succeeded) {
                    _libraries.Add(handle.Result);
                }
                if (_pendingLibraryLoads > 0 && --_pendingLibraryLoads == 0)
                    MarkLibrariesLoaded();
            };
            _handles.Add(address, handle);
            return handle;
        }

        public bool Unload(string address)
        {
            if (_handles.TryGetValue(address, out var h))
            {
                if (h.IsValid())
                {
                    var library = h.Result as AudioLibrary;
                    if (library != null)
                    {
                        library.Clear();
                    }
                    
                    Addressables.Release(h);
                    _handles.Remove(address);
                    return true;
                }
                _handles.Remove(address);
            }
            return false;
        }

        private void OnDestroy()
        {
            foreach (var kv in _handles)
            {
                var h = kv.Value;
                if (h.IsValid())
                {
                    var library = h.Result as AudioLibrary;
                    if (library != null)
                    {
                        library.Clear();
                    }
                    
                    Addressables.Release(h);
                }
            }
            _handles.Clear();
        }

        private void OnGet(AudioEmitter emitter)
        {
            emitter.gameObject.SetActive(true);
        }
        
        private void OnRelease(AudioEmitter emitter)
        {
            emitter.transform.SetParent(transform);
            emitter.gameObject.SetActive(false);
        }

        private AudioEmitter CreateEmitter()
        {
            var go = new GameObject("AudioEmitter", typeof(AudioSource), typeof(AudioEmitter));
            go.transform.SetParent(transform);
            var emitter = go.GetComponent<AudioEmitter>();
            return emitter;
        }
        
        public bool Stop(string clipName, Transform parent = null)
        {
            if (parent == null)
                parent = transform;
            
            int count = 0;
            for (var i = parent.childCount - 1; i >= 0; --i)
            {
                var child = parent.GetChild(i);
                if (child.gameObject.activeSelf == false)
                    continue;
                var emitter = child.GetComponent<AudioEmitter>();
                if (emitter.IsActive() == false || emitter.clip == null)
                    continue;
                    
                if (emitter.clip.name == clipName)
                {
                    Release(emitter);
                    ++count;
                }
            }

            return count > 0;
        }
        
        public void SetVolume(float volume)
        {
            
        }
        
        public AudioEmitterHandle Play(string clipName, Transform parent = null)
        {
            foreach (var library in _libraries)
            {
                var preset = library.GetPreset(clipName);
                if (preset == null)
                    continue;
                var mixerGroup = library.mixerGroup;
                if (preset.clip.Asset)
                {
                    return Play(mixerGroup, preset, parent);
                }
                else
                {
                    var h = preset.clip.LoadAssetAsync();
                    return Load_();
                    async Awaitable<AudioEmitter> Load_() {
                        await h.Task;
                        return Play(mixerGroup, preset, parent);
                    }
                }
            }
            return default;
        }
        
        private AudioEmitter Play(AudioMixerGroup mixerGroup, AudioPreset preset, Transform parent)
        {
            if (parent == null)
                parent = transform;
            
            if (preset.Overlap == false)
            {
                for (var i = parent.childCount - 1; i >= 0; --i)
                {   
                    var child = parent.GetChild(i);
                    var p = child.GetComponent<AudioEmitter>();
                    if (p != null && p.isActiveAndEnabled && p.clip == preset.clip.Asset)
                    {
                        if (preset.Override == false)
                        {
                            return p;
                        }
                        else
                        {
                            p.Play();
                            return p;
                        }
                    }
                }
            }
            var emitter = _pool.Get();
            emitter.transform.SetParent(parent);
            emitter.transform.localPosition = Vector3.zero;
            emitter.clip = preset.clip.Asset;
            emitter.volume = preset.Volume;
            emitter.pitch = preset.Pitch;
            emitter.loop = preset.Loop;
            emitter.mixerGroup = mixerGroup;
            emitter.OnComplete(Release);
            emitter.Play();
            return emitter;
        }

        private void MarkLibrariesLoaded()
        {
            if (IsLibrariesLoaded)
                return;

            IsLibrariesLoaded = true;
            LibrariesLoaded?.Invoke();
        }
        
        public void Release(AudioEmitter emitter)
        {
            emitter.transform.SetParent(transform);
            _pool.Release(emitter);
        }
        
        List<AsyncOperationHandle> _tasks = new();
        
        public async Awaitable Prepare(params string[] clipNames)
        {
            _tasks.Clear();
            
            foreach (var clipName in clipNames)
            {
                foreach (var library in _libraries)
                {
                    var preset = library.GetPreset(clipName);
                    if (preset != null)
                    {
                        if (preset.clip.Asset)
                            continue;
                        var t = preset.clip.LoadAssetAsync();
                        _tasks.Add(t);
                        break;
                    }
                }
            }

            while (_tasks.All(s => s.Status != AsyncOperationStatus.None) == false)
            {
                await Awaitable.NextFrameAsync();
            }
        }
        
        public void Clear()
        {
            foreach (var library in _libraries)
            {
                library.Clear();
                Addressables.Release(library);
            }
            _libraries.Clear();
        }
        
        public void Dispose()
        {
            Clear();
        }
    }
    
    public struct AudioEmitterHandle
    {
        public AudioEmitter Result { get; private set; }
        
        private Awaitable<AudioEmitter> _task;
        public Awaitable<AudioEmitter> Task
        { 
            get
            {
                if (_task == null && Result != null) {
                    var completionSource = new AwaitableCompletionSource<AudioEmitter>();
                    completionSource.SetResult(Result);
                    return completionSource.Awaitable;
                }
                return _task;
            }
            private set => _task = value;
        }

        public static implicit operator AudioEmitterHandle(AudioEmitter emitter)
        {
            return new AudioEmitterHandle { Result = emitter };
        }
        
        public static implicit operator AudioEmitterHandle(Awaitable<AudioEmitter> task)
        {
            return new AudioEmitterHandle { Task = task };
        }
        
        public static implicit operator bool(AudioEmitterHandle handle)
        {
            return handle.IsValid();
        }
        
        public bool IsValid()
        {
            return Task != null || Result != null;
        }
        
        public bool IsCompleted()
        {
            if (Result != null)
                return true;
            if (Task == null)
                return true;
            return false;
        }
        
        public void OnComplete(System.Action<AudioEmitter> action)
        {
            if (Result != null) {
                action.Invoke(Result);
                return;
            }
            
            async void Wait_(Awaitable<AudioEmitter> task)
            {
                var emitter = await task;
                action.Invoke(emitter);
            }
            Wait_(Task);
        }
        
        public System.Runtime.CompilerServices.INotifyCompletion GetAwaiter()
        {
            return Task.GetAwaiter();
        }
    }
}
