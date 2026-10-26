using UnityEngine;

namespace PawArena.Gameplay.Characters
{
    /// <summary>Soft visual joints on a standalone cat. Shared by player and bot, with no combat dependencies.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(120)]
    public sealed class CatSecondaryMotion : MonoBehaviour
    {
        [SerializeField] private CatLocomotion _motor;
        [SerializeField] private Animator _animator;
        [SerializeField, Range(0, 1)] private float _strength = 1;
        [SerializeField, Range(0, 1)] private float _armHang = .92f;
        [SerializeField, Range(0, 20)] private float _runLean = 12;
        private readonly Transform[] _bones = new Transform[12];
        private readonly Quaternion[] _basePose = new Quaternion[12];
        private readonly Quaternion[] _torsoReference = new Quaternion[2];
        private Transform _leftHand, _rightHand;
        private CatMotionSpring _lean, _arms, _look, _tail;
        private Vector3 _lastVelocity, _lastPosition, _acceleration;
        private float _lastYaw, _time, _lookPitch, _turnRate;
        private float _strafeStability;
        private bool _ready, _sampled, _physicsSampled, _applied;
        public Vector3 LeanDegrees => _lean.Value;
        public Vector3 ArmDeflection => _arms.Value;
        public bool IsReady => _ready;
        private void Awake() => Initialize();
        private void OnEnable() { if (_motor != null) { _motor.Warped += ResetMotion; } }
        private void Update() => RestorePose();
        private void FixedUpdate() => SamplePhysics(Time.fixedDeltaTime);
        private void LateUpdate() => Present(Time.deltaTime);
        private void OnDisable() { if (_motor != null) { _motor.Warped -= ResetMotion; } RestorePose(); ResetMotion(); }
        public void Initialize()
        {
            if (_ready || _motor == null || _animator == null || !_animator.isHuman) { return; }
            _bones[0] = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _bones[1] = _animator.GetBoneTransform(HumanBodyBones.Chest);
            for (var i = 0; i < 2; i++)
            { if (_bones[i] != null) { _torsoReference[i] = Quaternion.Inverse(transform.rotation) * _bones[i].rotation; } }
            _bones[2] = _animator.GetBoneTransform(HumanBodyBones.Head);
            _bones[3] = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _bones[4] = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _bones[5] = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _bones[6] = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand); _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            foreach (var bone in _animator.GetComponentsInChildren<Transform>(true))
            { for (var i = 0; i < 5; i++) { if (bone.name == "Tail_0" + (i + 1)) { _bones[i + 7] = bone; } } }
            _ready = _bones[0] != null && _bones[2] != null && _bones[3] != null && _bones[4] != null &&
                _bones[5] != null && _bones[6] != null && _leftHand != null && _rightHand != null;
        }
        public void SetLookPitch(float degrees) => _lookPitch = float.IsFinite(degrees) ? Mathf.Clamp(degrees, -30, 30) : 0;
        public void ResetMotion()
        { RestorePose(); _lean.Reset(); _arms.Reset(); _look.Reset(); _tail.Reset(); _sampled = _physicsSampled = false; _acceleration = Vector3.zero; _turnRate = _time = _lookPitch = _strafeStability = 0; }
        /// <summary>Sample after the motor's physics step. Render-rate derivatives lose impulses at high FPS.</summary>
        public void SamplePhysics(float delta)
        {
            if (_motor == null || _motor.Body == null || _motor.Suspended || !float.IsFinite(delta) || delta <= 0) { return; }
            var velocity = Vector3.ProjectOnPlane(_motor.Body.linearVelocity, Vector3.up);
            var yaw = _motor.Body.rotation.eulerAngles.y;
            if (_physicsSampled)
            {
                _acceleration = Vector3.ClampMagnitude((velocity - _lastVelocity) / delta, 40);
                _turnRate = Mathf.Clamp(Mathf.DeltaAngle(_lastYaw, yaw) / delta, -540, 540);
            }
            _lastVelocity = velocity; _lastYaw = yaw; _physicsSampled = true;
        }
        /// <summary>Restore before animation evaluation so unkeyed bones never accumulate previous offsets.</summary>
        public void RestorePose()
        {
            if (!_applied) { return; }
            for (var i = 0; i < _bones.Length; i++) { if (_bones[i] != null) { _bones[i].localRotation = _basePose[i]; } }
            _applied = false;
        }
        public void Present(float delta)
        {
            Initialize(); RestorePose();
            if (!_ready || _motor.Body == null) { return; }
            // Clip-only audition is deliberately unmodified, useful for comparing authored clips and live feel.
            if (_motor.Suspended) { ResetMotion(); return; }
            if (_sampled && (transform.position - _lastPosition).sqrMagnitude > 4) { ResetMotion(); }
            var velocity = Vector3.ProjectOnPlane(_motor.Body.linearVelocity, Vector3.up);
            _sampled = true;
            if (float.IsFinite(delta) && delta > 0)
            {
                var step = Mathf.Min(delta, .1f);
                var acceleration = _acceleration;
                var turn = _turnRate;
                var localVelocity = transform.InverseTransformDirection(velocity);
                var localAcceleration = transform.InverseTransformDirection(acceleration);
                var lateralWeight = _motor.Grounded ? Mathf.Abs(localVelocity.x) / Mathf.Max(.5f, localVelocity.magnitude) : 0;
                _strafeStability = Mathf.MoveTowards(_strafeStability, lateralWeight, step * 8);
                // Lateral inertia belongs to the limbs, not a sideways bend/twist of the spine.
                var targetLean = Vector3.right * (localVelocity.z / _motor.Settings.RunSpeed * _runLean + localAcceleration.z * .16f);
                _lean.Step(Vector3.ClampMagnitude(targetLean, 20) * (_motor.Grounded ? 1 : .25f), 2.4f, .72f, step);
                // Low damping intentionally leaves a playful overshoot instead of pinning the arms to the clip.
                _arms.Step(Vector3.ClampMagnitude(-acceleration * .022f - transform.right * (turn * .0005f), .65f), 1.45f, .35f, step);
                _look.Step(Vector3.right * _lookPitch, 2.2f, .85f, step); _time += step;
                _tail.Step(Vector3.ClampMagnitude(new Vector3(-localAcceleration.z * .3f,
                    -turn * .035f + Mathf.Sin(_time * 2.3f) * 7, localAcceleration.x * .25f), 22), 1.35f, .42f, step);
            }
            _lastPosition = transform.position;
            if (_strength <= 0) { return; }
            for (var i = 0; i < _bones.Length; i++) { if (_bones[i] != null) { _basePose[i] = _bones[i].localRotation; } }
            _applied = true;
            // Run also contains authored torso sway. Suppress its roll/yaw when strafing,
            // but retain forward pitch, limb animation and the untouched clip preview.
            for (var i = 0; i < 2; i++) { StabilizeTorso(i, _strafeStability * _strength); }
            var torso = _lean.Value + Vector3.right * (_look.Value.x * .4f + Mathf.Sin(_time * 2.5f) * .85f);
            Rotate(_bones[0], torso * (_bones[1] == null ? 1 : .55f) * _strength);
            if (_bones[1] != null) { Rotate(_bones[1], torso * .45f * _strength); }
            Rotate(_bones[2], new Vector3(_look.Value.x * .6f - _lean.Value.x * .25f, -_lean.Value.y * .5f, -_lean.Value.z * .45f) * _strength);
            // Keep the single airborne pose readable while letting its arms remain loose.
            var hang = _strength * _armHang * (_motor.Grounded ? 1 : .4f);
            Arm(true, hang); Arm(false, hang);
            for (var i = 7; i < 12; i++)
            { if (_bones[i] != null) { Rotate(_bones[i], (_tail.Value * .18f + Vector3.up * (Mathf.Sin(_time * 2.3f - (i - 7) * .6f) * 2.5f)) * _strength); } }
        }
        private void Arm(bool left, float weight)
        {
            var direction = (Vector3.down + transform.right * (left ? -.18f : .18f) + _arms.Value).normalized;
            Aim(_bones[left ? 3 : 5], _bones[left ? 4 : 6], direction, weight, 55);
            // Run clips bend elbows deeply; release the forearm too, otherwise the hands remain rigidly raised.
            Aim(_bones[left ? 4 : 6], left ? _leftHand : _rightHand, direction, weight * .9f, 100);
        }
        private void StabilizeTorso(int index, float weight)
        {
            var bone = _bones[index]; if (bone == null || weight <= 0) { return; }
            var relative = Quaternion.Inverse(transform.rotation) * bone.rotation * Quaternion.Inverse(_torsoReference[index]);
            var forward = relative * Vector3.forward;
            var pitch = Mathf.Atan2(-forward.y, new Vector2(forward.x, forward.z).magnitude) * Mathf.Rad2Deg;
            var upright = transform.rotation * Quaternion.Euler(pitch, 0, 0) * _torsoReference[index];
            bone.rotation = Quaternion.Slerp(bone.rotation, upright, weight);
        }
        private static void Aim(Transform bone, Transform child, Vector3 direction, float weight, float limit)
        {
            var axis = child.position - bone.position; if (axis.sqrMagnitude < .000001f) { return; }
            var offset = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(axis, direction), limit);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, offset, weight) * bone.rotation;
        }
        private void Rotate(Transform bone, Vector3 angles)
        { var basis = transform.rotation; bone.rotation = basis * Quaternion.Euler(angles) * Quaternion.Inverse(basis) * bone.rotation; }
    }
}
