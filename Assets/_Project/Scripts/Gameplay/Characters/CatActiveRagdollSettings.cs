using UnityEngine;

namespace PawArena.Gameplay.Characters
{
    /// <summary>Shared tuning for the physics-driven presentation rig used by every cat actor.</summary>
    [CreateAssetMenu(
        fileName = "CatActiveRagdollSettings",
        menuName = "Paw Arena/Characters/Active Ragdoll Settings")]
    public sealed class CatActiveRagdollSettings : ScriptableObject
    {
        [Header("Muscles")]
        [SerializeField, Min(0), Tooltip("How strongly every body rotates back toward the Animator pose. Raise this if the cat cannot stand; lower it for looser motion.")]
        private float _angularSpring = 180;
        [SerializeField, Min(0), Tooltip("Brakes angular oscillation. Raise this when limbs shake; too much makes motion sluggish.")]
        private float _angularDamping = 28;
        [SerializeField, Min(0), Tooltip("Safety cap for muscle acceleration. Raise only when strong springs still recover too slowly.")]
        private float _maximumAngularAcceleration = 520;

        [Header("Hands Only — Animated Arms")]
        [SerializeField, Range(1, 30), Tooltip("Maximum visual fist deflection from its animated wrist. Upper/lower arms follow animation, except during knockout.")]
        private float _handAngularLimit = 12;
        [SerializeField, Min(0), Tooltip("Acceleration drive returning each physical fist to its animated orientation.")]
        private float _handSpring = 160;
        [SerializeField, Min(0), Tooltip("Damping of the fist drive. Does not change torso/leg muscles.")]
        private float _handDamping = 24;
        [SerializeField, Min(.01f), Tooltip("Mass of each presentation-only fist. Wrist anchors cannot pull the animated arms down.")]
        private float _handMass = .12f;

        [Header("Body State Strength")]
        [SerializeField, Range(0, 1), Tooltip("Muscle strength while reacting to a hit. Lower values produce a heavier flop.")]
        private float _hitStrength = .45f;
        [SerializeField, Range(0, 1), Tooltip("Muscle strength while held or carried. Keep above zero so the body remains controllable.")]
        private float _heldStrength = .3f;
        [SerializeField, Range(0, 1), Tooltip("Muscle strength while knocked out. Near zero gives a limp ragdoll.")]
        private float _knockedOutStrength = .025f;
        [SerializeField, Range(0, 1), Tooltip("Multiplier applied while airborne. Higher values keep jumps readable and upright.")]
        private float _airborneStrength = .92f;

        [Header("Pose Position Tracking")]
        [SerializeField, Min(0), Tooltip("Pulls spine and limbs toward their animated positions using physics forces. Raise this if the upper body still collapses; set to zero for angular-only muscles.")]
        private float _bodyPositionSpring = 220;
        [SerializeField, Min(0), Tooltip("Brakes position overshoot on spine and limbs. Raise this when the body vibrates around the animation pose.")]
        private float _bodyPositionDamping = 32;
        [SerializeField, Min(0), Tooltip("Safety cap for spine and limb recovery acceleration. Raise only if fast movement leaves the puppet behind.")]
        private float _maximumBodyAcceleration = 320;

        [Header("Balance")]
        [SerializeField, Min(0), Tooltip("Pulls the hips toward the gameplay capsule. This is the main standing and root-follow force.")]
        private float _pelvisPositionSpring = 300;
        [SerializeField, Min(0), Tooltip("Brakes hip bouncing. Increase together with Pelvis Position Spring when the body oscillates.")]
        private float _pelvisPositionDamping = 38;
        [SerializeField, Min(0), Tooltip("Maximum hip recovery acceleration. Raise if the body cannot catch a fast-moving root.")]
        private float _maximumPelvisAcceleration = 360;
        [SerializeField, Min(0), Tooltip("Maximum distance the hips may drift from the gameplay anchor. Smaller is more stable; larger is floppier.")]
        private float _pelvisLinearLimit = .08f;

        [Header("Bodies")]
        [SerializeField, Min(.01f), Tooltip("Base mass before per-bone multipliers. It mostly affects impacts because muscle forces use acceleration mode.")]
        private float _bodyMass = .38f;
        [SerializeField, Min(0), Tooltip("Linear drag on each body. Raise slightly if impacts slide for too long.")]
        private float _linearDamping = .6f;
        [SerializeField, Min(0), Tooltip("Built-in angular drag in addition to muscle damping. Raise only for residual spinning.")]
        private float _bodyAngularDamping = 2f;
        [SerializeField, Range(10, 120), Tooltip("Global joint freedom in degrees. Lower values stabilize legs; higher values make the character curl and flop more.")]
        private float _defaultJointLimit = 28;
        [SerializeField, Range(.05f, .4f), Tooltip("Capsule thickness relative to each bone length. Change carefully to avoid floor penetration or oversized limbs.")]
        private float _colliderRadiusScale = .19f;
        [SerializeField, Min(0), Tooltip("Fraction of gameplay knockback copied to the visual ragdoll. It does not alter authoritative movement.")]
        private float _impulseMultiplier = .12f;

        [Header("Performance")]
        [SerializeField, Min(1), Tooltip("Distance from the main camera beyond which the puppet snaps to animation instead of simulating.")]
        private float _maximumSimulationDistance = 28;

        public float AngularSpring => _angularSpring;
        public float AngularDamping => _angularDamping;
        public float MaximumAngularAcceleration => _maximumAngularAcceleration;
        public float HandAngularLimit => _handAngularLimit;
        public float HandSpring => _handSpring;
        public float HandDamping => _handDamping;
        public float HandMass => _handMass;
        public float HitStrength => _hitStrength;
        public float HeldStrength => _heldStrength;
        public float KnockedOutStrength => _knockedOutStrength;
        public float AirborneStrength => _airborneStrength;
        public float BodyPositionSpring => _bodyPositionSpring;
        public float BodyPositionDamping => _bodyPositionDamping;
        public float MaximumBodyAcceleration => _maximumBodyAcceleration;
        public float PelvisPositionSpring => _pelvisPositionSpring;
        public float PelvisPositionDamping => _pelvisPositionDamping;
        public float MaximumPelvisAcceleration => _maximumPelvisAcceleration;
        public float PelvisLinearLimit => _pelvisLinearLimit;
        public float BodyMass => _bodyMass;
        public float LinearDamping => _linearDamping;
        public float BodyAngularDamping => _bodyAngularDamping;
        public float DefaultJointLimit => _defaultJointLimit;
        public float ColliderRadiusScale => _colliderRadiusScale;
        public float ImpulseMultiplier => _impulseMultiplier;
        public float MaximumSimulationDistance => _maximumSimulationDistance;

        private void OnValidate()
        {
            _angularSpring = Mathf.Max(0, _angularSpring);
            _bodyAngularDamping = Mathf.Max(0, _bodyAngularDamping);
            _maximumAngularAcceleration = Mathf.Max(0, _maximumAngularAcceleration);
            _handAngularLimit = Mathf.Clamp(_handAngularLimit, 1, 30);
            _handSpring = Mathf.Max(0, _handSpring);
            _handDamping = Mathf.Max(0, _handDamping);
            _handMass = Mathf.Max(.01f, _handMass);
            _bodyPositionSpring = Mathf.Max(0, _bodyPositionSpring);
            _bodyPositionDamping = Mathf.Max(0, _bodyPositionDamping);
            _maximumBodyAcceleration = Mathf.Max(0, _maximumBodyAcceleration);
            _pelvisPositionSpring = Mathf.Max(0, _pelvisPositionSpring);
            _pelvisPositionDamping = Mathf.Max(0, _pelvisPositionDamping);
            _maximumPelvisAcceleration = Mathf.Max(0, _maximumPelvisAcceleration);
            _pelvisLinearLimit = Mathf.Max(0, _pelvisLinearLimit);
            _bodyMass = Mathf.Max(.01f, _bodyMass);
            _linearDamping = Mathf.Max(0, _linearDamping);
            _angularDamping = Mathf.Max(0, _angularDamping);
            _impulseMultiplier = Mathf.Max(0, _impulseMultiplier);
            _maximumSimulationDistance = Mathf.Max(1, _maximumSimulationDistance);
        }
    }
}
