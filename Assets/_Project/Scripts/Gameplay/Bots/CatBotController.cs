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
        private void Update() { _motor.Submit(_runForTesting ? _direction : Vector3.zero, false); }
        private void OnDisable() { _motor?.ClearInput(); }
    }
}
