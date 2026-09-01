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
using UnityEngine.Serialization;
using UnityEngine.Pool;

namespace XSystem
{
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour, System.IDisposable
    {
        private ObjectPool<AudioEmitter> _emitterPool;
        
        [SerializeField]
        [FormerlySerializedAs("_libraryRefs")]
        private AssetReferenceT<AudioLibrary>[] _libraryReferences = {};

        private List<AudioLibrary> _loadedLibraries = new List<AudioLibrary>();
        private int _pendingLibraryLoads;

        public event System.Action LibrariesLoaded;

        public bool AreLibrariesLoaded { get; private set; }

        private static int _nextEventKey;
        
        private struct PendingAudioEvent
        {
            private string _presetName;
            private System.Action<int> _onDispatched;
            
            public string PresetName => _presetName;
            
            public PendingAudioEvent(string presetName, System.Action<int> onDispatched)
            {
                _presetName = presetName;
                _onDispatched = onDispatched;
            }
            
            public void Dispatch(int eventKey)
            {
                if (_onDispatched != null)
                    _onDispatched(eventKey);
            }
        }
        
        private static Dictionary<int, PendingAudioEvent> _pendingEvents = new();
        
        internal static int PostEvent(string presetName, System.Action<int> onDispatched = null)
        {
            var eventKey = ++_nextEventKey;
            _pendingEvents.Add(eventKey, new PendingAudioEvent(presetName, onDispatched));
            return eventKey;
        }
        
        internal static bool CancelEvent(int eventKey)
        {
            return _pendingEvents.Remove(eventKey);
        }
        
        private void Update()
        {
            if (_pendingEvents.Count > 0)
            {
                foreach (var pendingEvent in _pendingEvents)
                {
                    Play(pendingEvent.Value.PresetName);
                    pendingEvent.Value.Dispatch(pendingEvent.Key);
                }
                _pendingEvents.Clear();
            }
        }
        
        private Dictionary<string, AsyncOperationHandle> _libraryHandles = new();
        
        protected virtual void Awake()
        {
            _emitterPool = new ObjectPool<AudioEmitter>(CreateEmitter, OnGet, OnRelease);
        }
        
        public void Start()
        {
            AreLibrariesLoaded = false;
            _pendingLibraryLoads = 0;
            foreach (var libraryReference in _libraryReferences)
            {
                if (libraryReference.RuntimeKeyIsValid())
                {
                    ++_pendingLibraryLoads;
                }
            }

            if (_pendingLibraryLoads == 0)
            {
                MarkLibrariesLoaded();
                return;
            }

            foreach (var libraryReference in _libraryReferences)
            {
                if (libraryReference.RuntimeKeyIsValid())
                    _ = LoadLibrary(libraryReference.AssetGUID);
            }
        }
        
        public AsyncOperationHandle LoadLibrary(string libraryKey)
        {
            var handle = Addressables.LoadAssetAsync<AudioLibrary>(libraryKey);
            handle.Completed += completedHandle => {
                if (completedHandle.Status == AsyncOperationStatus.Succeeded) {
                    _loadedLibraries.Add(completedHandle.Result);
                }
                if (_pendingLibraryLoads > 0 && --_pendingLibraryLoads == 0)
                    MarkLibrariesLoaded();
            };
            _libraryHandles.Add(libraryKey, handle);
            return handle;
        }
        
        public bool Unload(string libraryKey)
        {
            if (_libraryHandles.TryGetValue(libraryKey, out var handle))
            {
                if (handle.IsValid())
                {
                    var library = handle.Result as AudioLibrary;
                    if (library != null)
                    {
                        library.Clear();
                    }
                    
                    Addressables.Release(handle);
                    _libraryHandles.Remove(libraryKey);
                    return true;
                }
                _libraryHandles.Remove(libraryKey);
            }
            return false;
        }

        private void OnDestroy()
        {
            foreach (var libraryHandle in _libraryHandles)
            {
                var handle = libraryHandle.Value;
                if (handle.IsValid())
                {
                    var library = handle.Result as AudioLibrary;
                    if (library != null)
                    {
                        library.Clear();
                    }
                    
                    Addressables.Release(handle);
                }
            }
            _libraryHandles.Clear();
        }

        private void OnGet(AudioEmitter emitter)
        {
            emitter.gameObject.SetActive(true);
        }
        
        private void OnRelease(AudioEmitter emitter)
        {
            emitter.clip = null;
            emitter.loop = false;
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
        
        public bool Stop(string clipName, Transform emitterParent = null)
        {
            if (emitterParent == null)
                emitterParent = transform;
            
            int count = 0;
            for (var i = emitterParent.childCount - 1; i >= 0; --i)
            {
                var child = emitterParent.GetChild(i);
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

        private AsyncOperationHandle<AudioClip> GetOrLoadClipHandle(AudioClipLink clipLink)
        {
            // AssetReferenceT keeps the handle after the first load, including while
            // the operation is still pending. Reuse it instead of starting a second
            // load for the same AssetReference instance.
            if (clipLink.OperationHandle.IsValid())
            {
                return clipLink.OperationHandle.Convert<AudioClip>();
            }

            return clipLink.LoadAssetAsync();
        }
        
        public AudioEmitterHandle Play(string presetName, Transform emitterParent = null)
        {
            foreach (var library in _loadedLibraries)
            {
                var preset = library.GetPreset(presetName);
                if (preset == null)
                    continue;
                var outputMixerGroup = library.mixerGroup;
                if (preset.clip.Asset)
                {
                    return Play(outputMixerGroup, preset, emitterParent);
                }
                else
                {
                    var clipHandle = GetOrLoadClipHandle(preset.clip);
                    return Load_();
                    async Awaitable<AudioEmitter> Load_() {
                        await clipHandle.Task;
                        return Play(outputMixerGroup, preset, emitterParent);
                    }
                }
            }
            return default;
        }
        
        private AudioEmitter Play(AudioMixerGroup outputMixerGroup, AudioPreset preset, Transform emitterParent)
        {
            if (emitterParent == null)
                emitterParent = transform;
            
            if (preset.Overlap == false)
            {
                for (var i = emitterParent.childCount - 1; i >= 0; --i)
                {   
                    var child = emitterParent.GetChild(i);
                    var existingEmitter = child.GetComponent<AudioEmitter>();
                    if (existingEmitter != null && existingEmitter.isActiveAndEnabled && existingEmitter.clip == preset.clip.Asset)
                    {
                        if (preset.Override == false)
                        {
                            return existingEmitter;
                        }
                        else
                        {
                            existingEmitter.Play();
                            return existingEmitter;
                        }
                    }
                }
            }
            var emitter = _emitterPool.Get();
            emitter.transform.SetParent(emitterParent);
            emitter.transform.localPosition = Vector3.zero;
            emitter.clip = preset.clip.Asset;
            emitter.volume = preset.Volume;
            emitter.pitch = preset.Pitch;
            emitter.loop = preset.Loop;
            emitter.mixerGroup = outputMixerGroup;
            emitter.OnComplete(Release);
            emitter.Play();
            return emitter;
        }

        private void MarkLibrariesLoaded()
        {
            if (AreLibrariesLoaded)
                return;

            AreLibrariesLoaded = true;
            LibrariesLoaded?.Invoke();
        }
        
        public void Release(AudioEmitter emitter)
        {
            emitter.transform.SetParent(transform);
            _emitterPool.Release(emitter);
        }
        
        private List<AsyncOperationHandle> _prepareTasks = new();
        
        public async Awaitable Prepare(params string[] presetNames)
        {
            _prepareTasks.Clear();
            
            foreach (var presetName in presetNames)
            {
                foreach (var library in _loadedLibraries)
                {
                    var preset = library.GetPreset(presetName);
                    if (preset != null)
                    {
                        if (preset.clip.Asset)
                            continue;
                        var clipHandle = GetOrLoadClipHandle(preset.clip);
                        _prepareTasks.Add(clipHandle);
                        break;
                    }
                }
            }

            while (_prepareTasks.All(handle => handle.Status != AsyncOperationStatus.None) == false)
            {
                await Awaitable.NextFrameAsync();
            }
        }
        
        public void Clear()
        {
            foreach (var library in _loadedLibraries)
            {
                library.Clear();
                Addressables.Release(library);
            }
            _loadedLibraries.Clear();
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
        
        public bool HasImmediateResult()
        {
            if (Result != null)
                return true;
            if (Task == null)
                return true;
            return false;
        }
        
        public void OnComplete(System.Action<AudioEmitter> onCompleted)
        {
            if (Result != null) {
                onCompleted.Invoke(Result);
                return;
            }
            
            async void WaitForEmitter(Awaitable<AudioEmitter> emitterTask)
            {
                var emitter = await emitterTask;
                onCompleted.Invoke(emitter);
            }
            WaitForEmitter(Task);
        }
        
        public System.Runtime.CompilerServices.INotifyCompletion GetAwaiter()
        {
            return Task.GetAwaiter();
        }
    }
}
