using UnityEngine;

namespace PawArena.Gameplay.Cameras
{
    /// <summary>Shared camera input tuning and flow-specific aim presets.</summary>
    [CreateAssetMenu(
        fileName = "CameraSystemConfig",
        menuName = "Paw Arena/Cameras/Camera System Config")]
    public sealed class CameraSystemConfig : ScriptableObject
    {
        [Header("Shared input tuning")]
        [SerializeField, Min(0)] private float _followHeight = 1.05f;
        [SerializeField, Min(0)] private float _lookSensitivity = 1;
        [SerializeField, Min(0)] private float _zoomSensitivity = 1;
        [SerializeField, Min(0)] private float _resultsOrbitSpeed = 10;

        [Header("Flow presets")]
        [SerializeField] private CameraAimPreset _lobby = new(
            CameraInputMode.Lobby, 1.5f, -20, 80, 7.3f, 3.5f, 20);
        [SerializeField] private CameraAimPreset _preMatch = new(
            CameraInputMode.Lobby, 1.5f, -20, 80, 7.3f, 1.5f, 20);
        [SerializeField] private CameraAimPreset _match = new(
            CameraInputMode.Match, 1.5f, -20, 80, 7.3f, 3.5f, 15);
        [SerializeField] private CameraAimPreset _spectator = new(
            CameraInputMode.Spectator, 25, -20, 80, 13, 7, 13);
        [SerializeField] private CameraAimPreset _results = new(
            CameraInputMode.Disabled, 14, -10, 45, 4.25f, 4.25f, 4.25f);

        public float FollowHeight => _followHeight;
        public float LookSensitivity => _lookSensitivity;
        public float ZoomSensitivity => _zoomSensitivity;
        public float ResultsOrbitSpeed => _resultsOrbitSpeed;
        public CameraAimPreset Lobby => _lobby;
        public CameraAimPreset PreMatch => _preMatch;
        public CameraAimPreset Match => _match;
        public CameraAimPreset Spectator => _spectator;
        public CameraAimPreset Results => _results;

        private void OnValidate()
        {
            _followHeight = SanitizeNonNegative(_followHeight, 1.05f);
            _lookSensitivity = SanitizeNonNegative(_lookSensitivity, 1);
            _zoomSensitivity = SanitizeNonNegative(_zoomSensitivity, 1);
            _resultsOrbitSpeed = SanitizeNonNegative(_resultsOrbitSpeed, 10);
            _lobby.Validate();
            _preMatch.Validate();
            _match.Validate();
            _spectator.Validate();
            _results.Validate();
        }

        private static float SanitizeNonNegative(float value, float fallback) =>
            float.IsFinite(value) ? Mathf.Max(0, value) : fallback;
    }
}
