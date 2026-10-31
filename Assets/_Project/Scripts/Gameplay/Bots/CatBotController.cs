using PawArena.Gameplay.Characters;
using UnityEngine;

namespace PawArena.Gameplay.Bots
{
    /// <summary>Separate bot owner. Step one is deliberately idle; test running is opt-in, without combat AI.</summary>
    public sealed class CatBotController : MonoBehaviour
    {
        [SerializeField] private CatLocomotion _motor;
        [SerializeField] private bool _runForTesting;
        [SerializeField] private Vector3 _direction = Vector3.forward;
        private void Awake()
        {
            var botLayer = LayerMask.NameToLayer("Bot");
            var playerLayer = LayerMask.NameToLayer("Player");
            if (botLayer >= 0)
            {
                SetActorLayerRecursively(transform, botLayer, playerLayer, botLayer);
            }
        }
        private void OnEnable()
        {
            if (_motor != null && _runForTesting) { return; }
            _motor?.ClearInput();
            enabled = false;
        }
        private void Update() => _motor.Submit(_direction, false);
        private void OnDisable() { _motor?.ClearInput(); }
        private static void SetActorLayerRecursively(
            Transform node,
            int targetLayer,
            int playerLayer,
            int botLayer)
        {
            var currentLayer = node.gameObject.layer;
            if (currentLayer == 0 || currentLayer == playerLayer || currentLayer == botLayer)
            {
                node.gameObject.layer = targetLayer;
            }
            for (var i = 0; i < node.childCount; i++)
            {
                SetActorLayerRecursively(node.GetChild(i), targetLayer, playerLayer, botLayer);
            }
        }
    }
}
