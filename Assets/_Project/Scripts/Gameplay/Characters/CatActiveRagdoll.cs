using PawArena.Gameplay.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawArena.Gameplay.Characters
{
    /// <summary>
    /// Physics-driven visual puppet. The gameplay capsule remains authoritative for movement,
    /// combat and ring-out rules while this isolated rig supplies weight and impact reactions.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(220)]
    public sealed class CatActiveRagdoll : MonoBehaviour
    {
        private const string RagdollLayerName = "ActiveRagdoll";
        private const float MinimumSegmentLength = .04f;
        private const float MinimumColliderRadius = .045f;
        private const float MaximumColliderRadius = .18f;
        private const int FirstArmBindingIndex = 4;
        private const int LastArmBindingIndex = 7;

        private static readonly BoneDefinition[] Definitions =
        {
            new(HumanBodyBones.Hips, -1, HumanBodyBones.Spine, 1.3f, 30),
            new(HumanBodyBones.Spine, 0, HumanBodyBones.Chest, 1.15f, 24),
            new(HumanBodyBones.Chest, 1, HumanBodyBones.Head, 1.2f, 28),
            new(HumanBodyBones.Head, 2, HumanBodyBones.LastBone, .75f, 42, true),
            new(HumanBodyBones.LeftUpperArm, 2, HumanBodyBones.LeftLowerArm, .72f, 88),
            new(HumanBodyBones.LeftLowerArm, 4, HumanBodyBones.LeftHand, .58f, 96),
            new(HumanBodyBones.RightUpperArm, 2, HumanBodyBones.RightLowerArm, .72f, 88),
            new(HumanBodyBones.RightLowerArm, 6, HumanBodyBones.RightHand, .58f, 96),
            new(HumanBodyBones.LeftUpperLeg, 0, HumanBodyBones.LeftLowerLeg, 1, 52),
            new(HumanBodyBones.LeftLowerLeg, 8, HumanBodyBones.LeftFoot, .9f, 64),
            new(HumanBodyBones.RightUpperLeg, 0, HumanBodyBones.RightLowerLeg, 1, 52),
            new(HumanBodyBones.RightLowerLeg, 10, HumanBodyBones.RightFoot, .9f, 64)
        };

        private static bool _collisionLayerConfigured;

        [SerializeField] private CatActiveRagdollSettings _settings;
        [SerializeField] private CatLocomotion _motor;
        [SerializeField] private Animator _animator;
        [SerializeField] private CatCombatant _combatant;
        [SerializeField] private CapsuleCollider _rootCollider;

        private readonly BodyBinding[] _bindings = new BodyBinding[Definitions.Length];
        private readonly BodyBinding[] _hands = new BodyBinding[2];
        private readonly Rigidbody[] _handAnchors = new Rigidbody[2];
        private GameObject _rigRoot;
        private Rigidbody _anchor;
        private Transform _lodObserver;
        private Vector3 _pendingImpulse;
        private Vector3 _pendingImpulsePoint;
        private Quaternion _previousRootRotation = Quaternion.identity;
        private Vector3 _rootAngularVelocity;
        private bool _hasPendingImpulse;
        private bool _hasRootRotationSample;
        private bool _ready;
        private bool _dynamicsEnabled;
        private bool _armsSimulated;

        /// <summary>Gets whether the runtime puppet was built from a valid cat skeleton.</summary>
        public bool IsReady => _ready;

        /// <summary>Gets whether rigidbody simulation currently drives the rendered skeleton.</summary>
        public bool IsSimulating => _dynamicsEnabled;

        private void Awake() => Initialize();

        private void OnEnable()
        {
            if (_rigRoot != null)
            {
                _rigRoot.SetActive(true);
            }

            if (_ready)
            {
                ResetPose();
            }

            if (_motor != null)
            {
                _motor.Warped += ResetPose;
            }
        }

        private void Start()
        {
            var mainCamera = Camera.main;
            _lodObserver = mainCamera != null ? mainCamera.transform : null;
        }

        private void FixedUpdate()
        {
            if (!_ready)
            {
                return;
            }

            UpdateRootFrameMotion(Time.fixedDeltaTime);
            UpdateAnchor();
            UpdateWorldTargetsFromRoot();
            // A held gameplay capsule is kinematic, but its visual ragdoll must still respond.
            var physicallyHeld = _combatant != null && _combatant.Holder != null;
            if ((_motor.Suspended && !physicallyHeld) || !ShouldSimulate())
            {
                SetDynamics(false);
                SnapBodiesToTargets();
                _pendingImpulse = Vector3.zero;
                _hasPendingImpulse = false;
                return;
            }

            SetDynamics(true);
            UpdateAnimatedArmsAndWrists();
            var strength = ResolveMuscleStrength();
            ApplyPelvisMuscle(strength);
            for (var index = 0; index < _bindings.Length; index++)
            {
                if (IsArm(index) && !_armsSimulated)
                {
                    continue;
                }

                if (index > 0)
                {
                    ApplyBodyPositionMuscle(_bindings[index], strength);
                }
                ApplyAngularMuscle(_bindings[index], strength);
            }

            ApplyPendingImpulse();
        }

        private void LateUpdate()
        {
            if (!_ready)
            {
                return;
            }

            CacheAnimationTargets();
            if (!_dynamicsEnabled)
            {
                return;
            }

            for (var index = 0; index < _bindings.Length; index++)
            {
                // Inherit the physical chest, but retain authored shoulder/elbow local transforms.
                if (IsArm(index) && !_armsSimulated) { continue; }
                var binding = _bindings[index];
                binding.Visual.SetPositionAndRotation(
                    binding.Body.transform.position,
                    binding.Body.transform.rotation * binding.VisualRotationOffset);
            }

            for (var index = 0; index < _hands.Length; index++)
            {
                var hand = _hands[index];
                var deflection = Quaternion.Inverse(_handAnchors[index].transform.rotation) * hand.Body.transform.rotation;
                // Only rotate the fist: never displace the wrist or stretch the skinned forearm.
                hand.Visual.localRotation = hand.AnimationLocalRotation *
                    Quaternion.RotateTowards(Quaternion.identity, deflection, _settings.HandAngularLimit);
            }
        }

        private void OnDisable()
        {
            RestoreAnimationPose();
            if (_ready)
            {
                SetDynamics(false);
            }

            if (_motor != null)
            {
                _motor.Warped -= ResetPose;
            }

            if (_rigRoot != null)
            {
                _rigRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            DestroyRigRoot();
        }

        /// <summary>Builds the non-authoritative physics puppet once from the assigned cat rig.</summary>
        public void Initialize()
        {
            if (_ready)
            {
                return;
            }

            ResolveReferences();
            if (_settings == null || _motor == null || _animator == null || _combatant == null ||
                _rootCollider == null || _animator.avatar == null || !_animator.avatar.isValid)
            {
                Debug.LogError($"{nameof(CatActiveRagdoll)} requires settings, motor, combatant, capsule and a valid cat rig.", this);
                enabled = false;
                return;
            }

            _motor.Initialize();
            if (_motor.Body == null || !ConfigureCollisionLayer(out var ragdollLayer))
            {
                Debug.LogError($"{nameof(CatActiveRagdoll)} could not initialize its physics layer or root body.", this);
                enabled = false;
                return;
            }

            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var poseRestorer = GetComponent<CatActiveRagdollPoseRestorer>();
            if (poseRestorer == null)
            {
                poseRestorer = gameObject.AddComponent<CatActiveRagdollPoseRestorer>();
                poseRestorer.hideFlags = HideFlags.HideInInspector;
            }
            poseRestorer.Initialize(this);
            CreateRigRoot(ragdollLayer);
            if (!CreateBodies(ragdollLayer) || !CreateHands(ragdollLayer))
            {
                DestroyRigRoot();
                enabled = false;
                return;
            }

            CreateJoints();
            IgnoreSelfCollisions();
            CacheAnimationTargets();
            UpdateWorldTargetsFromRoot();
            ResetRootFrameMotion();
            _dynamicsEnabled = true;
            SetDynamics(false);
            SnapBodiesToTargets();
            _ready = true;
        }

        /// <summary>Queues a presentation-only impact without changing authoritative movement or damage.</summary>
        public void ApplyImpulse(Vector3 impulse, Vector3 worldPoint)
        {
            if (!_ready || !IsFinite(impulse) || !IsFinite(worldPoint))
            {
                return;
            }

            _pendingImpulse += impulse * _settings.ImpulseMultiplier;
            _pendingImpulsePoint = worldPoint;
            _hasPendingImpulse = true;
        }

        /// <summary>Snaps the puppet back onto the authored pose after a warp or respawn.</summary>
        public void ResetPose()
        {
            if (!_ready)
            {
                return;
            }

            RestoreAnimationPose();
            CacheAnimationTargets();
            SetDynamics(false);
            ResetRootFrameMotion();
            UpdateAnchor();
            UpdateWorldTargetsFromRoot();
            SnapBodiesToTargets();
            _pendingImpulse = Vector3.zero;
            _hasPendingImpulse = false;
        }

        private void ResolveReferences()
        {
            if (_motor == null)
            {
                _motor = GetComponent<CatLocomotion>();
            }

            if (_combatant == null)
            {
                _combatant = GetComponent<CatCombatant>();
            }

            if (_rootCollider == null)
            {
                _rootCollider = GetComponent<CapsuleCollider>();
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }
        }

        private static bool ConfigureCollisionLayer(out int ragdollLayer)
        {
            ragdollLayer = LayerMask.NameToLayer(RagdollLayerName);
            if (ragdollLayer < 0)
            {
                return false;
            }

            if (_collisionLayerConfigured)
            {
                return true;
            }

            var groundLayer = LayerMask.NameToLayer("Ground");
            for (var layer = 0; layer < 32; layer++)
            {
                var collidesWithPresentation = layer == 0 || layer == groundLayer;
                Physics.IgnoreLayerCollision(ragdollLayer, layer, !collidesWithPresentation);
            }

            _collisionLayerConfigured = true;
            return true;
        }

        private void CreateRigRoot(int ragdollLayer)
        {
            _rigRoot = new GameObject($"[Physics] {name} Active Ragdoll")
            {
                hideFlags = HideFlags.DontSave,
                layer = ragdollLayer
            };
            SceneManager.MoveGameObjectToScene(_rigRoot, gameObject.scene);

            var anchorObject = new GameObject("Gameplay Anchor") { layer = ragdollLayer };
            anchorObject.transform.SetParent(_rigRoot.transform, false);
            anchorObject.transform.SetPositionAndRotation(_motor.Body.position, _motor.Body.rotation);
            _anchor = anchorObject.AddComponent<Rigidbody>();
            _anchor.isKinematic = true;
            _anchor.useGravity = false;
            _anchor.detectCollisions = false;
            _anchor.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private bool CreateBodies(int ragdollLayer)
        {
            for (var index = 0; index < Definitions.Length; index++)
            {
                var definition = Definitions[index];
                var visual = CatRigBones.Find(_animator, definition.Bone);
                if (visual == null)
                {
                    Debug.LogError($"Cat rig bone {definition.Bone} is missing from {name}.", this);
                    return false;
                }

                var bodyObject = new GameObject($"Ragdoll {definition.Bone}") { layer = ragdollLayer };
                bodyObject.transform.SetParent(_rigRoot.transform, false);
                bodyObject.transform.SetPositionAndRotation(visual.position, _motor.Body.rotation);
                var body = bodyObject.AddComponent<Rigidbody>();
                body.mass = _settings.BodyMass * definition.MassMultiplier;
                body.linearDamping = _settings.LinearDamping;
                body.angularDamping = _settings.BodyAngularDamping;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.maxAngularVelocity = 24;
                body.solverIterations = 8;
                body.solverVelocityIterations = 2;

                var collider = CreateCollider(bodyObject.transform, visual, definition, ragdollLayer);
                Physics.IgnoreCollision(_rootCollider, collider, true);
                var visualRotationOffset = Quaternion.Inverse(body.rotation) * visual.rotation;
                _bindings[index] = new BodyBinding(
                    visual,
                    body,
                    collider,
                    definition.ParentIndex,
                    definition.MuscleMultiplier,
                    visualRotationOffset);
            }

            return true;
        }

        private Collider CreateCollider(
            Transform bodyTransform,
            Transform visual,
            BoneDefinition definition,
            int ragdollLayer)
        {
            if (definition.IsSphere)
            {
                var sphere = bodyTransform.gameObject.AddComponent<SphereCollider>();
                sphere.radius = MaximumColliderRadius;
                return sphere;
            }

            var child = CatRigBones.Find(_animator, definition.ChildBone);
            var segment = child == null ? visual.up * .18f : child.position - visual.position;
            var length = Mathf.Max(MinimumSegmentLength, segment.magnitude);
            var localSegment = bodyTransform.InverseTransformVector(segment);
            var colliderObject = new GameObject("Collider") { layer = ragdollLayer };
            colliderObject.transform.SetParent(bodyTransform, false);
            colliderObject.transform.localPosition = localSegment * .5f;
            colliderObject.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localSegment.normalized);
            var capsule = colliderObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = Mathf.Clamp(length * _settings.ColliderRadiusScale, MinimumColliderRadius, MaximumColliderRadius);
            capsule.height = Mathf.Max(length, capsule.radius * 2);
            return capsule;
        }

        private void CreateJoints()
        {
            for (var index = 0; index < _bindings.Length; index++)
            {
                var binding = _bindings[index];
                var definition = Definitions[index];
                var joint = binding.Body.gameObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = definition.ParentIndex < 0 ? _anchor : _bindings[definition.ParentIndex].Body;
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor = joint.connectedBody.transform.InverseTransformPoint(binding.Body.position);
                joint.enableCollision = false;
                joint.enablePreprocessing = false;
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
                joint.projectionDistance = .08f;
                joint.projectionAngle = 12;

                if (definition.ParentIndex < 0)
                {
                    joint.xMotion = ConfigurableJointMotion.Limited;
                    joint.yMotion = ConfigurableJointMotion.Limited;
                    joint.zMotion = ConfigurableJointMotion.Limited;
                    joint.linearLimit = new SoftJointLimit
                    {
                        limit = _settings.PelvisLinearLimit,
                        contactDistance = .015f
                    };
                }
                else
                {
                    joint.xMotion = ConfigurableJointMotion.Locked;
                    joint.yMotion = ConfigurableJointMotion.Locked;
                    joint.zMotion = ConfigurableJointMotion.Locked;
                }

                joint.angularXMotion = ConfigurableJointMotion.Limited;
                joint.angularYMotion = ConfigurableJointMotion.Limited;
                joint.angularZMotion = ConfigurableJointMotion.Limited;
                var limit = Mathf.Clamp(
                    definition.AngularLimit * (_settings.DefaultJointLimit / 55),
                    10,
                    120);
                // Arms must reach chest-height punches and overhead lifts; retain leg/spine balance limits.
                if (index >= 4 && index <= 7) { limit = index % 2 == 0 ? 150 : 125; }
                joint.lowAngularXLimit = new SoftJointLimit { limit = -limit, contactDistance = 2 };
                joint.highAngularXLimit = new SoftJointLimit { limit = limit, contactDistance = 2 };
                joint.angularYLimit = new SoftJointLimit { limit = limit, contactDistance = 2 };
                joint.angularZLimit = new SoftJointLimit { limit = limit, contactDistance = 2 };
                binding.Joint = joint;
            }
        }

        private bool CreateHands(int ragdollLayer)
        {
            for (var index = 0; index < _hands.Length; index++)
            {
                var bone = index == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                var visual = CatRigBones.Find(_animator, bone);
                if (visual == null)
                {
                    Debug.LogError($"Cat rig bone {bone} is missing from {name}.", this);
                    return false;
                }

                var anchorObject = new GameObject($"Animated Wrist {bone}") { layer = ragdollLayer };
                anchorObject.transform.SetParent(_rigRoot.transform, false);
                anchorObject.transform.SetPositionAndRotation(visual.position, visual.rotation);
                var anchor = anchorObject.AddComponent<Rigidbody>();
                anchor.isKinematic = true;
                anchor.useGravity = false;
                anchor.detectCollisions = false;
                anchor.interpolation = RigidbodyInterpolation.Interpolate;
                _handAnchors[index] = anchor;

                var handObject = new GameObject($"Ragdoll {bone}") { layer = ragdollLayer };
                handObject.transform.SetParent(_rigRoot.transform, false);
                handObject.transform.SetPositionAndRotation(visual.position, visual.rotation);
                var body = handObject.AddComponent<Rigidbody>();
                body.mass = _settings.HandMass;
                body.linearDamping = _settings.LinearDamping;
                body.angularDamping = _settings.BodyAngularDamping;
                body.maxAngularVelocity = 24;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.solverIterations = 8;
                body.solverVelocityIterations = 2;
                var lowerArmIndex = index == 0 ? 5 : 7;
                var segment = visual.position - _bindings[lowerArmIndex].Visual.position;
                var sphere = handObject.AddComponent<SphereCollider>();
                sphere.radius = Mathf.Clamp(segment.magnitude * .22f, .045f, .1f);
                sphere.center = Quaternion.Inverse(visual.rotation) * segment.normalized * (sphere.radius * .5f);

                // This independent kinematic wrist is a one-way boundary. Fist forces cannot
                // feed back into the chest or pull the animation-controlled elbow downward.
                var joint = handObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = anchor;
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector3.zero;
                joint.connectedAnchor = Vector3.zero;
                joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
                joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
                joint.lowAngularXLimit = new SoftJointLimit { limit = -_settings.HandAngularLimit };
                joint.highAngularXLimit = joint.angularYLimit = joint.angularZLimit =
                    new SoftJointLimit { limit = _settings.HandAngularLimit };
                joint.rotationDriveMode = RotationDriveMode.Slerp;
                joint.slerpDrive = new JointDrive
                {
                    positionSpring = _settings.HandSpring,
                    positionDamper = _settings.HandDamping,
                    maximumForce = 1000,
                    useAcceleration = true
                };
                joint.targetRotation = Quaternion.identity;
                joint.enableCollision = false;
                joint.enablePreprocessing = false;
                joint.projectionMode = JointProjectionMode.PositionAndRotation;
                joint.projectionDistance = .015f;
                joint.projectionAngle = 3;
                _hands[index] = new BodyBinding(visual, body, sphere, lowerArmIndex, 1, Quaternion.identity)
                {
                    Joint = joint
                };
            }
            return true;
        }

        private void IgnoreSelfCollisions()
        {
            for (var first = 0; first < _bindings.Length; first++)
            {
                for (var second = first + 1; second < _bindings.Length; second++)
                {
                    Physics.IgnoreCollision(_bindings[first].Collider, _bindings[second].Collider, true);
                }
            }

            foreach (var hand in _hands)
            {
                Physics.IgnoreCollision(_rootCollider, hand.Collider, true);
                foreach (var binding in _bindings)
                {
                    Physics.IgnoreCollision(hand.Collider, binding.Collider, true);
                }
            }
            Physics.IgnoreCollision(_hands[0].Collider, _hands[1].Collider, true);
        }

        private void UpdateAnchor()
        {
            if (_anchor == null || _motor == null || _motor.Body == null)
            {
                return;
            }

            _anchor.MovePosition(_motor.Body.position);
            _anchor.MoveRotation(_motor.Body.rotation);
        }

        private void UpdateRootFrameMotion(float deltaTime)
        {
            if (_motor == null || _motor.Body == null || !float.IsFinite(deltaTime) || deltaTime <= 0)
            {
                ResetRootFrameMotion();
                return;
            }

            var currentRotation = _motor.Body.rotation;
            if (!_hasRootRotationSample)
            {
                _previousRootRotation = currentRotation;
                _rootAngularVelocity = Vector3.zero;
                _hasRootRotationSample = true;
                return;
            }

            var deltaRotation = currentRotation * Quaternion.Inverse(_previousRootRotation);
            _previousRootRotation = currentRotation;
            if (deltaRotation.w < 0)
            {
                deltaRotation = new Quaternion(
                    -deltaRotation.x,
                    -deltaRotation.y,
                    -deltaRotation.z,
                    -deltaRotation.w);
            }

            deltaRotation.ToAngleAxis(out var angle, out var axis);
            if (!float.IsFinite(angle) || !IsFinite(axis) || axis.sqrMagnitude < .000001f || angle < .0001f)
            {
                _rootAngularVelocity = Vector3.zero;
                return;
            }

            if (angle > 180)
            {
                angle -= 360;
            }

            _rootAngularVelocity = axis.normalized * (angle * Mathf.Deg2Rad / deltaTime);
        }

        private void ResetRootFrameMotion()
        {
            _rootAngularVelocity = Vector3.zero;
            if (_motor == null || _motor.Body == null)
            {
                _previousRootRotation = Quaternion.identity;
                _hasRootRotationSample = false;
                return;
            }

            _previousRootRotation = _motor.Body.rotation;
            _hasRootRotationSample = true;
        }

        private bool ShouldSimulate()
        {
            if (_lodObserver == null)
            {
                return true;
            }

            var maximumDistance = _settings.MaximumSimulationDistance;
            return (_lodObserver.position - transform.position).sqrMagnitude <= maximumDistance * maximumDistance;
        }

        private float ResolveMuscleStrength()
        {
            var strength = _combatant.Phase switch
            {
                CatCombatPhase.KnockedOut => _settings.KnockedOutStrength,
                CatCombatPhase.HitStun => _settings.HitStrength,
                CatCombatPhase.Held or CatCombatPhase.Carried => _settings.HeldStrength,
                _ => 1
            };
            if (!_motor.Grounded)
            {
                strength *= _settings.AirborneStrength;
            }

            return Mathf.Clamp01(strength);
        }

        private static bool IsArm(int index) => index is >= FirstArmBindingIndex and <= LastArmBindingIndex;

        private void UpdateAnimatedArmsAndWrists()
        {
            if (!_armsSimulated)
            {
                for (var index = FirstArmBindingIndex; index <= LastArmBindingIndex; index++)
                {
                    var arm = _bindings[index];
                    ResolveAttachedPose(arm, _bindings[2], out var position, out var rotation);
                    arm.Body.MovePosition(position);
                    arm.Body.MoveRotation(rotation);
                }
            }

            for (var index = 0; index < _hands.Length; index++)
            {
                var hand = _hands[index];
                var reference = _bindings[_armsSimulated ? hand.ParentIndex : 2];
                ResolveAttachedPose(hand, reference, out var position, out var rotation);
                _handAnchors[index].MovePosition(position);
                _handAnchors[index].MoveRotation(rotation);
            }
        }

        private static void ResolveAttachedPose(BodyBinding binding, BodyBinding reference,
            out Vector3 position, out Quaternion rotation)
        {
            var delta = reference.Body.rotation * Quaternion.Inverse(reference.TargetRotation);
            position = reference.Body.position + delta * (binding.TargetPosition - reference.TargetPosition);
            rotation = delta * binding.TargetRotation;
        }

        private void ApplyPelvisMuscle(float strength)
        {
            var pelvis = _bindings[0];
            var positionError = pelvis.TargetPosition - pelvis.Body.position;
            var relativeVelocity = pelvis.Body.linearVelocity - ResolveTargetFrameVelocity(pelvis.TargetPosition);
            var acceleration = positionError * (_settings.PelvisPositionSpring * strength) -
                relativeVelocity * (_settings.PelvisPositionDamping * Mathf.Sqrt(strength));
            acceleration = Vector3.ClampMagnitude(acceleration, _settings.MaximumPelvisAcceleration);
            pelvis.Body.AddForce(acceleration, ForceMode.Acceleration);
        }

        private void ApplyAngularMuscle(BodyBinding binding, float strength)
        {
            if (strength <= 0)
            {
                return;
            }

            var error = binding.TargetRotation * Quaternion.Inverse(binding.Body.rotation);
            if (error.w < 0)
            {
                error = new Quaternion(-error.x, -error.y, -error.z, -error.w);
            }

            error.ToAngleAxis(out var angle, out var axis);
            if (!float.IsFinite(angle) || !IsFinite(axis) || axis.sqrMagnitude < .000001f)
            {
                return;
            }

            if (angle > 180)
            {
                angle -= 360;
            }

            var muscleStrength = strength * binding.MuscleMultiplier;
            var relativeAngularVelocity = binding.Body.angularVelocity - _rootAngularVelocity;
            var acceleration = axis.normalized * (angle * Mathf.Deg2Rad * _settings.AngularSpring * muscleStrength) -
                relativeAngularVelocity * (_settings.AngularDamping * Mathf.Sqrt(muscleStrength));
            acceleration = Vector3.ClampMagnitude(acceleration, _settings.MaximumAngularAcceleration * Mathf.Max(1, Mathf.Sqrt(strength)));
            binding.Body.AddTorque(acceleration, ForceMode.Acceleration);
        }

        private void ApplyBodyPositionMuscle(BodyBinding binding, float strength)
        {
            var muscleStrength = strength * binding.MuscleMultiplier;
            if (muscleStrength <= 0)
            {
                return;
            }

            var positionError = binding.TargetPosition - binding.Body.position;
            var relativeVelocity = binding.Body.linearVelocity - ResolveTargetFrameVelocity(binding.TargetPosition);
            var acceleration = positionError * (_settings.BodyPositionSpring * muscleStrength) -
                relativeVelocity * (_settings.BodyPositionDamping * Mathf.Sqrt(muscleStrength));
            acceleration = Vector3.ClampMagnitude(acceleration, _settings.MaximumBodyAcceleration * Mathf.Max(1, Mathf.Sqrt(strength)));
            binding.Body.AddForce(acceleration, ForceMode.Acceleration);
        }

        private Vector3 ResolveTargetFrameVelocity(Vector3 targetPosition)
        {
            if (_motor == null || _motor.Body == null)
            {
                return Vector3.zero;
            }

            var rootPosition = _motor.Body.position;
            return _motor.Body.linearVelocity +
                Vector3.Cross(_rootAngularVelocity, targetPosition - rootPosition);
        }

        private void ApplyPendingImpulse()
        {
            if (!_hasPendingImpulse)
            {
                return;
            }

            var closest = _bindings[0];
            var closestDistance = float.PositiveInfinity;
            for (var index = 0; index < _bindings.Length; index++)
            {
                if (_bindings[index].Body.isKinematic) { continue; }
                var distance = (_bindings[index].Body.worldCenterOfMass - _pendingImpulsePoint).sqrMagnitude;
                if (distance >= closestDistance)
                {
                    continue;
                }

                closestDistance = distance;
                closest = _bindings[index];
            }

            foreach (var hand in _hands)
            {
                var distance = (hand.Body.worldCenterOfMass - _pendingImpulsePoint).sqrMagnitude;
                if (hand.Body.isKinematic || distance >= closestDistance) { continue; }
                closestDistance = distance;
                closest = hand;
            }

            closest.Body.AddForceAtPosition(_pendingImpulse, _pendingImpulsePoint, ForceMode.Impulse);
            if (closest.ParentIndex >= 0 && !_bindings[closest.ParentIndex].Body.isKinematic)
            {
                _bindings[closest.ParentIndex].Body.AddForceAtPosition(
                    _pendingImpulse * .35f,
                    _pendingImpulsePoint,
                    ForceMode.Impulse);
            }

            _pendingImpulse = Vector3.zero;
            _hasPendingImpulse = false;
        }

        private void CacheAnimationTargets()
        {
            CacheAnimationTargets(_bindings);
            CacheAnimationTargets(_hands);
        }

        private void CacheAnimationTargets(BodyBinding[] bindings)
        {
            // The animated skeleton inherits the interpolated render transform in LateUpdate.
            // Capture in that same frame, then rebuild against the authoritative physics root in FixedUpdate.
            var rootPosition = transform.position;
            var rootRotation = transform.rotation;
            var inverseRootRotation = Quaternion.Inverse(rootRotation);
            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                var targetBodyRotation = binding.Visual.rotation * Quaternion.Inverse(binding.VisualRotationOffset);
                binding.TargetPosition = binding.Visual.position;
                binding.TargetRotation = targetBodyRotation;
                binding.TargetRootLocalPosition = inverseRootRotation * (binding.Visual.position - rootPosition);
                binding.TargetRootLocalRotation = inverseRootRotation * targetBodyRotation;
                binding.AnimationLocalPosition = binding.Visual.localPosition;
                binding.AnimationLocalRotation = binding.Visual.localRotation;
            }
        }

        private void UpdateWorldTargetsFromRoot()
        {
            UpdateWorldTargetsFromRoot(_bindings);
            UpdateWorldTargetsFromRoot(_hands);
        }

        private void UpdateWorldTargetsFromRoot(BodyBinding[] bindings)
        {
            if (_motor == null || _motor.Body == null)
            {
                return;
            }

            var rootPosition = _motor.Body.position;
            var rootRotation = _motor.Body.rotation;
            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                binding.TargetPosition = rootPosition + rootRotation * binding.TargetRootLocalPosition;
                binding.TargetRotation = rootRotation * binding.TargetRootLocalRotation;
            }
        }

        /// <summary>Restores the clean animation target before Animator evaluates the next frame.</summary>
        internal void RestoreAnimationPose()
        {
            if (!_ready || !_dynamicsEnabled)
            {
                return;
            }

            for (var index = 0; index < _bindings.Length; index++)
            {
                var binding = _bindings[index];
                binding.Visual.localPosition = binding.AnimationLocalPosition;
                binding.Visual.localRotation = binding.AnimationLocalRotation;
            }
            foreach (var hand in _hands)
            {
                hand.Visual.localPosition = hand.AnimationLocalPosition;
                hand.Visual.localRotation = hand.AnimationLocalRotation;
            }
        }

        private void SetDynamics(bool enabledState)
        {
            var simulateArms = enabledState && _combatant.IsKnockedOut;
            if (_dynamicsEnabled == enabledState && _armsSimulated == simulateArms)
            {
                return;
            }

            if (!enabledState) { RestoreAnimationPose(); }

            for (var index = 0; index < _bindings.Length; index++)
            {
                var binding = _bindings[index];
                var body = binding.Body;
                var arm = IsArm(index);
                var dynamicBody = enabledState && (!arm || simulateArms);
                if (arm)
                {
                    var joint = binding.Joint;
                    joint.xMotion = joint.yMotion = joint.zMotion = simulateArms
                        ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Free;
                    joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = simulateArms
                        ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Free;
                    joint.projectionMode = simulateArms ? JointProjectionMode.PositionAndRotation : JointProjectionMode.None;
                    body.detectCollisions = simulateArms;
                    if (simulateArms && body.isKinematic)
                    {
                        body.position = binding.Visual.position;
                        body.rotation = binding.Visual.rotation * Quaternion.Inverse(binding.VisualRotationOffset);
                    }
                }
                SetBodyDynamics(body, dynamicBody, binding.TargetPosition);
            }

            for (var index = 0; index < _hands.Length; index++)
            {
                var hand = _hands[index];
                if (enabledState && !_dynamicsEnabled)
                {
                    var reference = _bindings[simulateArms ? hand.ParentIndex : 2];
                    ResolveAttachedPose(hand, reference, out var position, out var rotation);
                    _handAnchors[index].position = hand.Body.position = position;
                    _handAnchors[index].rotation = hand.Body.rotation = rotation;
                }
                SetBodyDynamics(hand.Body, enabledState, hand.TargetPosition);
            }

            _armsSimulated = simulateArms;
            _dynamicsEnabled = enabledState;
        }

        private void SetBodyDynamics(Rigidbody body, bool dynamicBody, Vector3 targetPosition)
        {
            if (body.isKinematic == !dynamicBody) { return; }
            if (dynamicBody)
            {
                body.isKinematic = false;
                body.linearVelocity = ResolveTargetFrameVelocity(targetPosition);
                body.angularVelocity = _rootAngularVelocity;
            }
            else
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        private void SnapBodiesToTargets()
        {
            for (var index = 0; index < _bindings.Length; index++)
            {
                var binding = _bindings[index];
                binding.Body.position = binding.TargetPosition;
                binding.Body.rotation = binding.TargetRotation;
            }
            for (var index = 0; index < _hands.Length; index++)
            {
                var hand = _hands[index];
                hand.Body.position = _handAnchors[index].position = hand.TargetPosition;
                hand.Body.rotation = _handAnchors[index].rotation = hand.TargetRotation;
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private void DestroyRigRoot()
        {
            if (_rigRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_rigRoot);
            }
            else
            {
                DestroyImmediate(_rigRoot);
            }

            _rigRoot = null;
        }

        private sealed class BodyBinding
        {
            public BodyBinding(
                Transform visual,
                Rigidbody body,
                Collider collider,
                int parentIndex,
                float muscleMultiplier,
                Quaternion visualRotationOffset)
            {
                Visual = visual;
                Body = body;
                Collider = collider;
                ParentIndex = parentIndex;
                MuscleMultiplier = muscleMultiplier;
                VisualRotationOffset = visualRotationOffset;
                TargetPosition = visual.position;
                TargetRotation = body.rotation;
                TargetRootLocalPosition = Vector3.zero;
                TargetRootLocalRotation = Quaternion.identity;
                AnimationLocalPosition = visual.localPosition;
                AnimationLocalRotation = visual.localRotation;
            }

            public Transform Visual { get; }
            public Rigidbody Body { get; }
            public Collider Collider { get; }
            public int ParentIndex { get; }
            public float MuscleMultiplier { get; }
            public Quaternion VisualRotationOffset { get; }
            public ConfigurableJoint Joint { get; set; }
            public Vector3 TargetPosition { get; set; }
            public Quaternion TargetRotation { get; set; }
            public Vector3 TargetRootLocalPosition { get; set; }
            public Quaternion TargetRootLocalRotation { get; set; }
            public Vector3 AnimationLocalPosition { get; set; }
            public Quaternion AnimationLocalRotation { get; set; }
        }

        private readonly struct BoneDefinition
        {
            public BoneDefinition(
                HumanBodyBones bone,
                int parentIndex,
                HumanBodyBones childBone,
                float muscleMultiplier,
                float angularLimit,
                bool isSphere = false)
            {
                Bone = bone;
                ParentIndex = parentIndex;
                ChildBone = childBone;
                MuscleMultiplier = muscleMultiplier;
                AngularLimit = angularLimit;
                IsSphere = isSphere;
            }

            public HumanBodyBones Bone { get; }
            public int ParentIndex { get; }
            public HumanBodyBones ChildBone { get; }
            public float MuscleMultiplier { get; }
            public float MassMultiplier => Bone is HumanBodyBones.Hips or HumanBodyBones.Chest ? 1.5f : 1;
            public float AngularLimit { get; }
            public bool IsSphere { get; }
        }
    }
}
