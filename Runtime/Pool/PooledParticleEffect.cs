using UnityEngine;

namespace XSystem
{
    /// <summary>
    /// Reusable particle effect component for Addressables-backed UnityPool items.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledParticleEffect : PooledItem
    {
        [SerializeField]
        [Min(0f)]
        private float _visibleDurationSeconds;

        [SerializeField]
        private ParticleSystem[] _particleSystems;

        private float _playedAtSeconds = -1f;
        private bool _isPlaying;

        protected override void Awake()
        {
            base.Awake();
            StopAndClear();
        }

        public override void OnGet()
        {
            base.OnGet();
            ReactivateParticleSystems();
            StopAndClear();
        }

        public override void OnRelease()
        {
            StopAndClear();
            base.OnRelease();
        }

        public void Play()
        {
            ReactivateParticleSystems();
            StopAndClear();

            ParticleSystem rootParticleSystem = GetComponent<ParticleSystem>();
            if (rootParticleSystem != null)
            {
                rootParticleSystem.Play(true);
                _isPlaying = true;
                _playedAtSeconds = Time.time;
                return;
            }

            if (_particleSystems == null)
            {
                return;
            }

            bool hasParticleSystem = false;
            for (int i = 0; i < _particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = _particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                hasParticleSystem = true;
                particleSystem.Play(true);
            }

            if (!hasParticleSystem)
            {
                return;
            }

            _isPlaying = true;
            _playedAtSeconds = Time.time;
        }

        public void Stop()
        {
            StopAndClear();
        }

        public bool IsAlive()
        {
            if (!_isPlaying)
            {
                return false;
            }

            if (_visibleDurationSeconds > 0f &&
                Time.time - _playedAtSeconds >= _visibleDurationSeconds)
            {
                return false;
            }

            ParticleSystem rootParticleSystem = GetComponent<ParticleSystem>();
            if (rootParticleSystem != null)
            {
                return rootParticleSystem.IsAlive(true);
            }

            if (_particleSystems == null)
            {
                return false;
            }

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = _particleSystems[i];
                if (particleSystem != null && particleSystem.IsAlive(true))
                {
                    return true;
                }
            }

            return false;
        }

        private void StopAndClear()
        {
            _isPlaying = false;
            _playedAtSeconds = -1f;

            ParticleSystem rootParticleSystem = GetComponent<ParticleSystem>();
            if (rootParticleSystem != null)
            {
                rootParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            if (_particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = _particleSystems[i];
                if (particleSystem != null)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void ReactivateParticleSystems()
        {
            ParticleSystem rootParticleSystem = GetComponent<ParticleSystem>();
            if (rootParticleSystem != null)
            {
                if (!rootParticleSystem.gameObject.activeSelf)
                {
                    rootParticleSystem.gameObject.SetActive(true);
                }

                return;
            }

            if (_particleSystems == null)
            {
                return;
            }

            for (int i = 0; i < _particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = _particleSystems[i];
                if (particleSystem != null && !particleSystem.gameObject.activeSelf)
                {
                    particleSystem.gameObject.SetActive(true);
                }
            }
        }

        private void OnValidate()
        {
            var root = GetComponent<ParticleSystem>();
            if (root != null)
            {
                _particleSystems = new[] { root };
                return;
            }

            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }
    }
}
