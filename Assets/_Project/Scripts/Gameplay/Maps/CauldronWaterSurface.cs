using UnityEngine;

namespace PawArena.Gameplay.Maps
{
    /// <summary>Local water presentation and trigger volume; does not decide match elimination.</summary>
    [DisallowMultipleComponent]
    public sealed class CauldronWaterSurface : MonoBehaviour
    {
        private const int RippleCapacity = 8;
        private static readonly int RipplesId = Shader.PropertyToID("_Ripples");
        [SerializeField] private Renderer _surface;
        [SerializeField] private ParticleSystem _droplets;
        [SerializeField] private ParticleSystem _bubbles;
        [SerializeField] private AudioSource[] _voices;
        [SerializeField] private AudioClip _splashSound;
        [SerializeField, Min(1)] private float _radius = 18;
        [SerializeField, Min(1)] private float _depth = 14;
        [SerializeField, Min(.1f)] private float _ambientBubbleInterval = .4f;
        private readonly Vector4[] _ripples = new Vector4[RippleCapacity];
        private MaterialPropertyBlock _properties;
        private int _nextRipple;
        private int _nextVoice;
        private float _nextBubble;

        /// <summary>World height of the resting surface; waves are visual only.</summary>
        public float SurfaceY => transform.position.y;

        private void Awake()
        {
            if (_surface == null || _droplets == null || _bubbles == null)
            {
                Debug.LogError($"{nameof(CauldronWaterSurface)} is missing presentation references.", this);
                enabled = false;
                return;
            }
            _properties = new MaterialPropertyBlock();
            for (var i = 0; i < RippleCapacity; i++) { _ripples[i] = new Vector4(0, 0, -1000, 0); }
            PushRipples();
        }

        private void OnEnable()
        {
            if (_properties == null) { return; }
            for (var i = 0; i < RippleCapacity; i++) { _ripples[i] = new Vector4(0, 0, -1000, 0); }
            PushRipples();
            // Manual Emit still needs running simulation, including after a map is reused.
            var drops = _droplets.main;
            drops.loop = true;
            var bubbles = _bubbles.main;
            bubbles.loop = true;
            _droplets.Play();
            _bubbles.Play();
        }

        private void Update()
        {
            if (Time.time < _nextBubble) { return; }
            _nextBubble = Time.time + _ambientBubbleInterval;
            var point = Random.insideUnitCircle * (_radius * .88f);
            EmitBubble(transform.TransformPoint(new Vector3(point.x, -.18f, point.y)), .2f);
        }

        private void OnDisable()
        {
            if (_properties != null && _surface != null) { _surface.SetPropertyBlock(null); }
            if (_droplets != null) { _droplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            if (_bubbles != null) { _bubbles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!enabled || other.attachedRigidbody == null) { return; }
            if (other.attachedRigidbody.TryGetComponent<CauldronWaterInteractor>(out var actor))
            {
                actor.EnterWater(this);
            }
        }

        /// <summary>Checks the cylindrical liquid volume, not its enclosing square bounds.</summary>
        public bool Contains(Vector3 worldPosition, float margin = 0)
        {
            var local = transform.InverseTransformPoint(worldPosition);
            return local.x * local.x + local.z * local.z <= (_radius + margin) * (_radius + margin)
                && local.y <= margin && local.y >= -_depth - margin;
        }

        /// <summary>Emits reusable particles, a radial shader impulse and one spatial audio voice.</summary>
        public void Splash(Vector3 worldPosition, float downwardSpeed)
        {
            if (!enabled || _properties == null) { return; }
            var strength = Mathf.Clamp01(downwardSpeed / 18);
            worldPosition.y = SurfaceY + .04f;
            var local = transform.InverseTransformPoint(worldPosition);
            _ripples[_nextRipple] = new Vector4(local.x, local.z, Time.time, Mathf.Lerp(.35f, 1, strength));
            _nextRipple = (_nextRipple + 1) % RippleCapacity;
            PushRipples();
            var count = Mathf.RoundToInt(Mathf.Lerp(24, 64, strength));
            for (var i = 0; i < count; i++)
            {
                var angle = Random.value * Mathf.PI * 2;
                var outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var particle = new ParticleSystem.EmitParams
                {
                    position = worldPosition + outward * Random.Range(.1f, .45f),
                    velocity = outward * Random.Range(1, 3.5f) * (1 + strength)
                        + Vector3.up * Random.Range(2, 6) * (.65f + strength * .5f),
                    startSize = Random.Range(.06f, .19f),
                    startLifetime = Random.Range(.45f, 1.1f),
                    startColor = new Color(.72f, .47f, 1, .85f)
                };
                _droplets.Emit(particle, 1);
            }
            if (_splashSound != null && _voices != null && _voices.Length > 0)
            {
                var voice = _voices[_nextVoice];
                _nextVoice = (_nextVoice + 1) % _voices.Length;
                if (voice != null)
                {
                    voice.transform.position = worldPosition;
                    voice.pitch = Random.Range(.9f, 1.1f);
                    voice.clip = _splashSound;
                    voice.volume = Mathf.Lerp(.35f, .85f, strength);
                    voice.Play();
                }
            }
        }

        /// <summary>Emits a small rising bubble; lifetime ends at the surface.</summary>
        public void EmitBubble(Vector3 worldPosition, float size)
        {
            if (!enabled || _bubbles == null) { return; }
            var particle = new ParticleSystem.EmitParams
            {
                position = worldPosition,
                velocity = Vector3.up * 1.6f,
                startSize = size,
                startLifetime = Mathf.Clamp((SurfaceY - worldPosition.y) / 1.6f, .15f, 3),
                startColor = new Color(.82f, .66f, 1, .65f)
            };
            _bubbles.Emit(particle, 1);
        }

        private void PushRipples()
        {
            _properties.SetVectorArray(RipplesId, _ripples);
            _surface.SetPropertyBlock(_properties);
        }
    }
}
