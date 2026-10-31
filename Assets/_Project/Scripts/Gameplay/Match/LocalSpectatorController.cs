using System;
using System.Collections.Generic;
using PawArena.Gameplay.Characters;
using PawArena.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawArena.Gameplay.Match
{
    /// <summary>Lets an eliminated local player follow and cycle through active participants.</summary>
    [DisallowMultipleComponent]
    public sealed class LocalSpectatorController : MonoBehaviour
    {
        [SerializeField] private CharacterReviewCamera _camera = null;
        [SerializeField] private CatPlayerController _player = null;

        private IReadOnlyList<LocalMatchActor> _actors;
        private int _targetIndex = -1;

        /// <summary>Raised whenever the spectator camera changes participant.</summary>
        public event Action<string> TargetChanged;

        /// <summary>Gets whether local spectator input is active.</summary>
        public bool IsSpectating { get; private set; }

        /// <summary>Gets the participant currently followed by the camera.</summary>
        public string CurrentTargetId { get; private set; } = string.Empty;

        private void Update()
        {
            if (!IsSpectating)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                CycleTarget(1);
            }
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
            {
                CycleTarget(-1);
            }
        }

        private void OnDisable() => StopSpectating();

        /// <summary>Starts following active actors and disables local character input.</summary>
        public bool BeginSpectating(IReadOnlyList<LocalMatchActor> actors)
        {
            if (_camera == null || _player == null || actors == null)
            {
                return false;
            }

            _actors = actors;
            IsSpectating = true;
            _player.SetInputEnabled(false);
            _camera.SetPartyMatchMode(false);
            _targetIndex = -1;
            return CycleTarget(1);
        }

        /// <summary>Moves away from an eliminated target when another active actor exists.</summary>
        public void RefreshTarget()
        {
            if (!IsSpectating || _targetIndex < 0 || _targetIndex >= _actors.Count ||
                _actors[_targetIndex].IsEliminated)
            {
                CycleTarget(1);
            }
        }

        /// <summary>Selects the previous active participant and wraps at the list boundary.</summary>
        public bool TrySelectPrevious() => IsSpectating && CycleTarget(-1);

        /// <summary>Selects the next active participant and wraps at the list boundary.</summary>
        public bool TrySelectNext() => IsSpectating && CycleTarget(1);

        /// <summary>Pauses the spectator camera while retaining the active participant list.</summary>
        public bool PauseSpectating()
        {
            if (!IsSpectating)
            {
                return false;
            }

            IsSpectating = false;
            CurrentTargetId = string.Empty;
            RestoreLocalControl();
            TargetChanged?.Invoke(string.Empty);
            return true;
        }

        /// <summary>Resumes spectating from the previous target in the requested direction.</summary>
        public bool ResumeSpectating(int direction)
        {
            if (IsSpectating || _camera == null || _player == null ||
                _actors == null || _actors.Count == 0 || direction == 0)
            {
                return false;
            }

            IsSpectating = true;
            _player.SetInputEnabled(false);
            _camera.SetPartyMatchMode(false);
            if (CycleTarget(direction > 0 ? 1 : -1))
            {
                return true;
            }

            IsSpectating = false;
            RestoreLocalControl();
            return false;
        }

        /// <summary>Follows the server-confirmed winner, including a last-to-fall winner.</summary>
        public bool FollowWinner(string playerId)
        {
            if (!IsSpectating || string.IsNullOrWhiteSpace(playerId) || _actors == null)
            {
                return false;
            }

            for (var index = 0; index < _actors.Count; index++)
            {
                if (string.Equals(_actors[index].PlayerId, playerId, StringComparison.Ordinal))
                {
                    return FollowAt(index, false);
                }
            }

            return false;
        }

        /// <summary>Returns camera and input ownership to the local player.</summary>
        public void StopSpectating()
        {
            IsSpectating = false;
            _actors = null;
            ClearTarget();
            RestoreLocalControl();
        }

        private bool CycleTarget(int direction)
        {
            if (_actors == null || _actors.Count == 0)
            {
                ClearTarget();
                return false;
            }

            var start = _targetIndex < 0 ? (direction > 0 ? -1 : 0) : _targetIndex;
            for (var offset = 1; offset <= _actors.Count; offset++)
            {
                var index = (start + direction * offset) % _actors.Count;
                if (index < 0)
                {
                    index += _actors.Count;
                }

                if (FollowAt(index, true))
                {
                    return true;
                }
            }

            ClearTarget();
            return false;
        }

        private bool FollowAt(int index, bool requireActive)
        {
            var actor = _actors[index];
            if (actor?.Transform == null || (requireActive && actor.IsEliminated))
            {
                return false;
            }

            _targetIndex = index;
            CurrentTargetId = actor.PlayerId;
            _camera.Follow(actor.Transform);
            TargetChanged?.Invoke(actor.PlayerId);
            return true;
        }

        private void ClearTarget()
        {
            _targetIndex = -1;
            CurrentTargetId = string.Empty;
            TargetChanged?.Invoke(string.Empty);
        }

        private void RestoreLocalControl()
        {
            if (_camera == null || _player == null)
            {
                return;
            }

            _camera.Follow(_player.transform);
            _camera.SetPartyMatchMode(false);
            _camera.AlignBehindTarget();
            _player.SetInputEnabled(true);
        }
    }
}
