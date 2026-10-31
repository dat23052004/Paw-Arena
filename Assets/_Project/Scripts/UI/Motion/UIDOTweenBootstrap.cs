using DG.Tweening;
using UnityEngine;

namespace PawArena.UI.Motion
{
    /// <summary>Initializes DOTween once with UI-safe defaults before any scene creates a tween.</summary>
    internal static class UIDOTweenBootstrap
    {
        private const int TweenCapacity = 320;
        private const int SequenceCapacity = 48;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
            DOTween.SetTweensCapacity(TweenCapacity, SequenceCapacity);
        }
    }
}
