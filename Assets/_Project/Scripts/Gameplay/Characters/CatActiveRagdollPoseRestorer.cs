using UnityEngine;

namespace PawArena.Gameplay.Characters
{
    /// <summary>Restores the animation skeleton before Unity evaluates its next pose.</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class CatActiveRagdollPoseRestorer : MonoBehaviour
    {
        private CatActiveRagdoll _owner;

        private void Awake() => _owner = GetComponent<CatActiveRagdoll>();

        private void Update()
        {
            if (_owner != null && _owner.isActiveAndEnabled)
            {
                _owner.RestoreAnimationPose();
            }
        }

        internal void Initialize(CatActiveRagdoll owner) => _owner = owner;
    }
}
