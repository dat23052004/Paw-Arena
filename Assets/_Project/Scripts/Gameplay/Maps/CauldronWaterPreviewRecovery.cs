using PawArena.Gameplay.Player;
using UnityEngine;

namespace PawArena.Gameplay.Maps
{
    /// <summary>Preview-only recovery; deliberately not installed on the shared player prefab.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CauldronWaterInteractor), typeof(CatPlayerController))]
    public sealed class CauldronWaterPreviewRecovery : MonoBehaviour
    {
        private CauldronWaterInteractor _waterActor;
        private CatPlayerController _player;

        private void Awake()
        {
            _waterActor = GetComponent<CauldronWaterInteractor>();
            _player = GetComponent<CatPlayerController>();
        }

        private void OnEnable() => _waterActor.SinkCompleted += HandleSinkCompleted;
        private void OnDisable() => _waterActor.SinkCompleted -= HandleSinkCompleted;
        private void HandleSinkCompleted(CauldronWaterInteractor actor) => _player.ResetPlayer();
    }
}
