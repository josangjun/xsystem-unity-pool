using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace XSystem
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public class AudioPlayer : PoolItem
    {
        private AudioSource _source;
        
        public bool IsActive()
        {
            return _source && _source.isPlaying;
        }

        protected override void Awake()
        {
            base.Awake();
            _source = GetComponent<AudioSource>();
        }
        
        public AudioClip clip {
            get => _source.clip;
            set => _source.clip = value;
        }

        public float volume {
            get => _source.volume;
            set => _source.volume = value;
        }
        
        public float pitch {
            get => _source.pitch;
            set => _source.pitch = value;
        }
        
        public AudioMixerGroup mixerGroup {
            get => _source.outputAudioMixerGroup;
            set => _source.outputAudioMixerGroup = value;
        }
        
        private Awaitable _coroutine;

        public void Play()
        {
            _source.time = 0;
            _source.Play();
            if (_onComplete == null)
                return;
            if (_source.loop)
                return;
            if (_coroutine != null)
                StopCoroutine(_coroutine);
            _coroutine = Wait_(_source);
            async Awaitable Wait_(AudioSource source)
            {
                var time = source.clip.length / source.pitch;
                for (var t = 0f; t < time; t += Time.deltaTime)
                {
                    await Awaitable.NextFrameAsync();
                }
                if (this)
                {
                    _onComplete(this);
                    _onComplete = null;
                    _coroutine = null;
                }
            }
        }
        
        private System.Action<AudioPlayer> _onComplete;
        
        public void OnComplete(System.Action<AudioPlayer> action)
        {
            _onComplete = action;
        }
    }
}