using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("_clipName")]
        private string _presetName;

        private int _pendingEventKey;

        private void OnEnable()
        {
            Play();
        }
        
        private void OnDisable()
        {
            Cancel();
        }
        
        public void Play()
        {
            _pendingEventKey = AudioManager.PostEvent(_presetName, OnDispatched);
        }
        
        public void Cancel()
        {
            if (_pendingEventKey != 0)
            {
                AudioManager.CancelEvent(_pendingEventKey);
                _pendingEventKey = 0;
            }
        }
        
        private void OnDispatched(int eventKey)
        {
            if (_pendingEventKey == eventKey)
            {
                _pendingEventKey = 0;
            }
        }
    }
}
