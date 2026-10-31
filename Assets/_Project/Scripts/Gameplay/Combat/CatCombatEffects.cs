using UnityEngine;

namespace PawArena.Gameplay.Combat
{
    /// <summary>Small prewarmed pool for player projectiles and Hairball ground hazards.</summary>
    [DisallowMultipleComponent]
    public sealed class CatCombatEffects : MonoBehaviour
    {
        private const int PoolSize = 24;
        private const int HitCapacity = 16;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private CatCombatSettings _settings;
        private readonly Effect[] _effects = new Effect[PoolSize];
        private MaterialPropertyBlock _properties;
        private int _activeEffectCount;
        private bool _initialized;

        private void Awake() => Initialize();
        private void Update()
        {
            if (_activeEffectCount <= 0)
            {
                enabled = false;
                return;
            }

            Step(Time.deltaTime);
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _effects.Length; i++)
            {
                if (_effects[i]?.Visual == null) { continue; }
                if (Application.isPlaying) { Destroy(_effects[i].Visual); }
                else { DestroyImmediate(_effects[i].Visual); }
            }

            _activeEffectCount = 0;
            _initialized = false;
        }

        /// <summary>Prewarms every visual once so ability use does not instantiate or destroy.</summary>
        public void Initialize()
        {
            if (_initialized) { return; }
            _properties ??= new MaterialPropertyBlock();
            if (_settings == null)
            {
                Debug.LogError($"{nameof(CatCombatEffects)} requires combat settings.", this);
                enabled = false;
                return;
            }
            for (var i = 0; i < _effects.Length; i++)
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = $"CombatEffect_{i:00}";
                visual.transform.SetParent(transform, false);
                var collider = visual.GetComponent<Collider>();
                collider.enabled = false;
                visual.SetActive(false);
                _effects[i] = new Effect(visual, visual.GetComponent<Renderer>());
            }
            _initialized = true;
            enabled = false;
        }

        /// <summary>Launches a pooled projectile; returns false only when all slots are busy.</summary>
        public bool SpawnProjectile(CatCombatant owner, Vector3 position, Vector3 velocity, float radius, float life, float damage, float impulse)
        {
            return Spawn(owner, position, velocity, radius, life, damage, impulse, false);
        }

        /// <summary>Creates a pooled slippery zone without applying direct damage.</summary>
        public bool SpawnPuddle(CatCombatant owner, Vector3 position, float radius, float life)
        {
            position.y = .05f;
            return Spawn(owner, position, Vector3.zero, radius, life, 0, 0, true);
        }

        /// <summary>Advances active effects and performs segment collision checks.</summary>
        public void Step(float delta)
        {
            if (!_initialized || !float.IsFinite(delta) || delta <= 0) { return; }
            if (_activeEffectCount <= 0)
            {
                enabled = false;
                return;
            }

            var actors = CatCombatant.Active;
            for (var i = 0; i < _effects.Length; i++)
            {
                var effect = _effects[i];
                if (!effect.Active) { continue; }
                effect.Remaining -= delta;
                if (effect.Remaining <= 0) { Release(effect); continue; }
                var from = effect.Position;
                effect.Position += effect.Velocity * delta;
                effect.Visual.transform.position = effect.Position;
                for (var actorIndex = 0; actorIndex < actors.Count; actorIndex++)
                {
                    var target = actors[actorIndex];
                    if (target == null || target == effect.Owner || !target.isActiveAndEnabled) { continue; }
                    var centre = target.Body.position + Vector3.up * (effect.Puddle ? .1f : .8f);
                    if (effect.Puddle)
                    {
                        if ((centre - effect.Position).sqrMagnitude <= effect.Radius * effect.Radius)
                        { target.ApplySlip(effect.Owner, .35f); }
                        continue;
                    }
                    if (WasHit(effect, target.GetEntityId())) { continue; }
                    var segment = effect.Position - from;
                    var fraction = segment.sqrMagnitude > .0001f
                        ? Mathf.Clamp01(Vector3.Dot(centre - from, segment) / segment.sqrMagnitude) : 0;
                    var nearest = from + segment * fraction;
                    if ((centre - nearest).sqrMagnitude > (effect.Radius + .45f) * (effect.Radius + .45f)) { continue; }
                    MarkHit(effect, target.GetEntityId());
                    var direction = effect.Velocity.sqrMagnitude > .001f ? effect.Velocity.normalized : effect.Owner.transform.forward;
                    target.ReceiveHit(effect.Owner, effect.Damage, direction * effect.Impulse + Vector3.up * 1.2f);
                    Release(effect);
                    break;
                }
            }

            if (_activeEffectCount <= 0)
            {
                enabled = false;
            }
        }

        private bool Spawn(CatCombatant owner, Vector3 position, Vector3 velocity, float radius, float life,
            float damage, float impulse, bool puddle)
        {
            Initialize();
            if (!_initialized || owner == null) { return false; }
            for (var i = 0; i < _effects.Length; i++)
            {
                var effect = _effects[i];
                if (effect.Active) { continue; }
                effect.Active = true;
                effect.Owner = owner;
                effect.Position = position;
                effect.Velocity = velocity;
                effect.Radius = radius;
                effect.Remaining = life;
                effect.Damage = damage;
                effect.Impulse = impulse;
                effect.Puddle = puddle;
                effect.HitCount = 0;
                effect.Visual.transform.position = position;
                effect.Visual.transform.localScale = puddle
                    ? new Vector3(radius * 2, .08f, radius * 2) : Vector3.one * radius * 2;
                effect.Visual.SetActive(true);
                _properties.Clear();
                _properties.SetColor(BaseColorId, puddle ? new Color(.35f, .8f, .25f, .75f) : new Color(.2f, .75f, 1, 1));
                effect.Renderer.SetPropertyBlock(_properties);
                _activeEffectCount++;
                enabled = true;
                return true;
            }
            return false;
        }

        private static bool WasHit(Effect effect, EntityId id)
        {
            for (var i = 0; i < effect.HitCount; i++) { if (effect.HitIds[i] == id) { return true; } }
            return false;
        }

        private static void MarkHit(Effect effect, EntityId id)
        {
            if (effect.HitCount < effect.HitIds.Length) { effect.HitIds[effect.HitCount++] = id; }
        }

        private void Release(Effect effect)
        {
            if (!effect.Active) { return; }
            effect.Active = false;
            effect.Owner = null;
            effect.Visual.SetActive(false);
            _activeEffectCount = Mathf.Max(0, _activeEffectCount - 1);
        }

        private sealed class Effect
        {
            public Effect(GameObject visual, Renderer renderer)
            {
                Visual = visual;
                Renderer = renderer;
                HitIds = new EntityId[HitCapacity];
            }

            public readonly GameObject Visual;
            public readonly Renderer Renderer;
            public readonly EntityId[] HitIds;
            public CatCombatant Owner;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Radius;
            public float Remaining;
            public float Damage;
            public float Impulse;
            public int HitCount;
            public bool Active;
            public bool Puddle;
        }
    }
}
