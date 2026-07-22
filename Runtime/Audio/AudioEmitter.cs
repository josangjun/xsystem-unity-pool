using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace XSystem
{
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public class AudioEmitter : PoolItem
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

        public bool loop {
            get => _source.loop;
            set => _source.loop = value;
        }
        
        public AudioMixerGroup mixerGroup {
            get => _source.outputAudioMixerGroup;
            set => _source.outputAudioMixerGroup = value;
        }
        
        private int _waitVersion;
        
        public void Play()
        {
            var waitVersion = ++_waitVersion;
            _source.time = 0;
            _source.Play();
            if (_onComplete == null)
                return;
            if (_source.loop)
                return;
            _ = Wait_(_source, waitVersion);
        }
        
        async Awaitable Wait_(AudioSource source, int version)
        {
            try
            {
                var time = source.clip.length / source.pitch;
                for (var t = 0f; t < time; t += Time.unscaledDeltaTime)
                {
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                    if (version != _waitVersion || isActiveAndEnabled == false)
                        return;
                }
                if (this && version == _waitVersion)
                {
                    _onComplete(this);
                    _onComplete = null;
                }
            }
            catch (System.OperationCanceledException)
            {
                return;
            }
        }
        
        private System.Action<AudioEmitter> _onComplete;
        
        public void OnComplete(System.Action<AudioEmitter> action)
        {
            _onComplete = action;
        }
    }
}
