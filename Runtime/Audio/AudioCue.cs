using UnityEngine;

namespace XSystem
{
    /// <summary>
    /// Plays a named audio-library preset through AudioManager's pooled emitters.
    /// This component does not own a PoolItem, AudioEmitter, or AudioSource.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioCue : MonoBehaviour
    {
        [SerializeField]
        private string _clipName;

        private AudioManager _audioManager;
        private bool _hasPlayedForActivation;

        private void OnEnable()
        {
            _hasPlayedForActivation = false;
            TryPlayOrWaitForLibraries();
        }

        private void OnDisable()
        {
            if (_audioManager != null)
            {
                _audioManager.LibrariesLoaded -= HandleLibrariesLoaded;
            }

            _hasPlayedForActivation = false;
        }

        public void Configure(AudioManager audioManager)
        {
            if (_audioManager != null)
            {
                _audioManager.LibrariesLoaded -= HandleLibrariesLoaded;
            }

            _audioManager = audioManager;

            if (isActiveAndEnabled)
            {
                TryPlayOrWaitForLibraries();
            }
        }

        private void HandleLibrariesLoaded()
        {
            TryPlayOrWaitForLibraries();
        }

        private void TryPlayOrWaitForLibraries()
        {
            if (_hasPlayedForActivation || string.IsNullOrWhiteSpace(_clipName))
            {
                return;
            }

            if (_audioManager == null)
            {
                if (!AudioManager.TryGetActive(out _audioManager))
                {
                    return;
                }
            }

            if (!_audioManager.IsLibrariesLoaded)
            {
                _audioManager.LibrariesLoaded -= HandleLibrariesLoaded;
                _audioManager.LibrariesLoaded += HandleLibrariesLoaded;
                return;
            }

            // AudioManager owns the emitter lifetime and returns it to its pool
            // after playback. Do not parent it to this pooled visual effect.
            _audioManager.Play(_clipName);
            _hasPlayedForActivation = true;
            _audioManager.LibrariesLoaded -= HandleLibrariesLoaded;
        }
    }
}
