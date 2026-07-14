using UnityEngine;

namespace XSystem
{
    public enum AudioConcurrency
    {
        Overlap,
        Override,
        Cancel
    }
    
    [System.Serializable]
    public class AudioPreset : System.IComparable<AudioPreset>, System.IComparable<string>
    {
        [SerializeField]
        private string _name;
        public string Name  {
            get => _name;
            set {
                _name = value;
                _hash = StringToHash(_name);
            }
        }
        public AudioClipLink clip;
        [SerializeField, Range(0, 5)]
        private float _volume;

        public int CompareTo(AudioPreset other)
        {
            if (other == null) return 1;
            return string.Compare(Name, other.Name, System.StringComparison.Ordinal);
        }

        public AudioPreset() {}

        public AudioPreset(string guid, string name, float volume, float delay, float pitch, AudioConcurrency concurrency)
        {
            this.clip = new AudioClipLink(guid);
            this.Name = name;
            _volume = volume;
            _delay = delay;
            _pitch = pitch;
            _concurrency = concurrency;
        }

        public float Volume => _volume;

        [SerializeField, Range(0, 5)]
        private float _delay;
        public float Delay { get => _delay; set => _delay = value; }

        [SerializeField, Range(0, 5)]
        private float _pitch;
        public float Pitch { get => _pitch; set => _pitch = value; }

        public bool Overlap => _concurrency == AudioConcurrency.Overlap;

        public bool Override => _concurrency == AudioConcurrency.Override;

        [SerializeField]
        private AudioConcurrency _concurrency = AudioConcurrency.Overlap;

        public AudioConcurrency Concurrency { get => _concurrency; set => _concurrency = value; }


        [SerializeField, HideInInspector]
        public int _hash;
        public int Hash => _hash;

        public void OnValidate()
        {
            _hash = StringToHash(Name);
        }
        
        public static int StringToHash(string value)
        {
            return Animator.StringToHash(value);
        }

        public int CompareTo(string other)
        {
            return string.Compare(Name, other, System.StringComparison.Ordinal);
        }
    }
    
}