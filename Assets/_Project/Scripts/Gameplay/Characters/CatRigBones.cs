using UnityEngine;

namespace PawArena.Gameplay.Characters
{
    /// <summary>Resolves cat skeleton transforms for either Generic or Humanoid imports.</summary>
    public static class CatRigBones
    {
        /// <summary>Finds a standard cat bone without requiring Humanoid retargeting.</summary>
        public static Transform Find(Animator animator, HumanBodyBones bone)
        {
            if (animator == null)
            {
                return null;
            }

            if (animator.isHuman)
            {
                return animator.GetBoneTransform(bone);
            }

            var boneName = bone.ToString();
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == boneName)
                {
                    return transforms[index];
                }
            }

            return null;
        }
    }
}
