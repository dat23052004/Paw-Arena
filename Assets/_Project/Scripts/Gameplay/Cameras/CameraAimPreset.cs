using System;
using UnityEngine;

namespace PawArena.Gameplay.Cameras
{
    /// <summary>Immutable-at-runtime input and framing values for one camera view.</summary>
    [Serializable]
    public struct CameraAimPreset
    {
        [SerializeField] private CameraInputMode _inputMode;
        [SerializeField] private float _defaultPitch;
        [SerializeField] private float _minimumPitch;
        [SerializeField] private float _maximumPitch;
        [SerializeField] private float _defaultDistance;
        [SerializeField] private float _minimumDistance;
        [SerializeField] private float _maximumDistance;

        public CameraAimPreset(
            CameraInputMode inputMode,
            float defaultPitch,
            float minimumPitch,
            float maximumPitch,
            float defaultDistance,
            float minimumDistance,
            float maximumDistance)
        {
            _inputMode = inputMode;
            _defaultPitch = defaultPitch;
            _minimumPitch = minimumPitch;
            _maximumPitch = maximumPitch;
            _defaultDistance = defaultDistance;
            _minimumDistance = minimumDistance;
            _maximumDistance = maximumDistance;
            Validate();
        }

        public CameraInputMode InputMode => _inputMode;
        public float DefaultPitch => _defaultPitch;
        public float MinimumPitch => _minimumPitch;
        public float MaximumPitch => _maximumPitch;
        public float DefaultDistance => _defaultDistance;
        public float MinimumDistance => _minimumDistance;
        public float MaximumDistance => _maximumDistance;

        internal void Validate()
        {
            _minimumPitch = float.IsFinite(_minimumPitch)
                ? Mathf.Clamp(_minimumPitch, -80, 80)
                : -20;
            _maximumPitch = float.IsFinite(_maximumPitch)
                ? Mathf.Clamp(_maximumPitch, _minimumPitch, 80)
                : 80;
            _defaultPitch = float.IsFinite(_defaultPitch)
                ? Mathf.Clamp(_defaultPitch, _minimumPitch, _maximumPitch)
                : Mathf.Clamp(16, _minimumPitch, _maximumPitch);
            _minimumDistance = float.IsFinite(_minimumDistance)
                ? Mathf.Max(.5f, _minimumDistance)
                : .5f;
            _maximumDistance = float.IsFinite(_maximumDistance)
                ? Mathf.Max(_minimumDistance, _maximumDistance)
                : Mathf.Max(_minimumDistance, 20);
            _defaultDistance = float.IsFinite(_defaultDistance)
                ? Mathf.Clamp(_defaultDistance, _minimumDistance, _maximumDistance)
                : _minimumDistance;
        }
    }
}
