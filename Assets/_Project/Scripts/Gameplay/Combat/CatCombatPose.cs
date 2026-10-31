using UnityEngine;

namespace PawArena.Gameplay.Combat
{
    /// <summary>Loose procedural presentation for basic combat, leaving authored states to skills only.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(140)]
    public sealed class CatCombatPose : MonoBehaviour
    {
        [SerializeField] private CatCombatant _actor;
        [SerializeField] private CatPlayerCombat _playerCombat;
        [SerializeField] private Animator _animator;
        [SerializeField, Range(0, 1)] private float _strength = .9f;

        private Transform _spine;
        private Transform _leftUpperArm;
        private Transform _leftLowerArm;
        private Transform _leftHand;
        private Transform _rightUpperArm;
        private Transform _rightLowerArm;
        private Transform _rightHand;
        private bool _ready;

        private void Awake() => Initialize();
        private void LateUpdate() => Present();

        /// <summary>Caches the Humanoid bones once.</summary>
        public void Initialize()
        {
            if (_ready || _actor == null || _animator == null || !_animator.isHuman) { return; }
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            _ready = _spine != null && _leftUpperArm != null && _leftLowerArm != null && _leftHand != null &&
                _rightUpperArm != null && _rightLowerArm != null && _rightHand != null;
        }

        private void Present()
        {
            Initialize();
            if (!_ready || _strength <= 0) { return; }
            switch (_actor.Phase)
            {
                case CatCombatPhase.Punch:
                    PresentPunch();
                    break;
                case CatCombatPhase.Grab:
                case CatCombatPhase.Holding:
                case CatCombatPhase.Lifting:
                case CatCombatPhase.Carrying:
                case CatCombatPhase.Throwing:
                    PresentGrab();
                    break;
                case CatCombatPhase.HitStun:
                    RotateWorld(_spine, new Vector3(-12, 0, 14) * _strength);
                    break;
                case CatCombatPhase.KnockedOut:
                    RotateWorld(_spine, new Vector3(-65, 0, 18) * _strength);
                    break;
            }
        }

        private void PresentPunch()
        {
            var progress = _playerCombat == null ? .5f : _playerCombat.ActionNormalizedTime;
            var arc = Mathf.Sin(progress * Mathf.PI);
            RotateWorld(_spine, new Vector3(arc * 8, -arc * 12, 0) * _strength);
            RotateWorld(_leftUpperArm, new Vector3(-arc * 68, -arc * 22, 0) * _strength);
            RotateWorld(_leftLowerArm, new Vector3(0, 0, arc * 32) * _strength);
        }

        private void PresentGrab()
        {
            var target = _actor.Held;
            var point = target == null ? transform.position + transform.forward * 1.1f + Vector3.up :
                target.transform.position + Vector3.up * .9f;
            Aim(_leftUpperArm, _leftLowerArm, point + transform.right * -.18f, _strength, 75);
            Aim(_leftLowerArm, _leftHand, point + transform.right * -.18f, _strength, 95);
            Aim(_rightUpperArm, _rightLowerArm, point + transform.right * .18f, _strength, 75);
            Aim(_rightLowerArm, _rightHand, point + transform.right * .18f, _strength, 95);
        }

        private static void Aim(Transform bone, Transform child, Vector3 point, float weight, float limit)
        {
            var axis = child.position - bone.position;
            var direction = point - bone.position;
            if (axis.sqrMagnitude < .000001f || direction.sqrMagnitude < .000001f) { return; }
            var offset = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(axis, direction), limit);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, offset, weight) * bone.rotation;
        }

        private void RotateWorld(Transform bone, Vector3 angles)
        {
            var basis = transform.rotation;
            bone.rotation = basis * Quaternion.Euler(angles) * Quaternion.Inverse(basis) * bone.rotation;
        }
    }
}
