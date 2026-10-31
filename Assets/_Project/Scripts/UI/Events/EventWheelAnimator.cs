using System;
using System.Collections;
using UnityEngine;

namespace PawArena.UI.Events
{
    /// <summary>Rotates Event Wheel content and stops on a service-selected reward sector.</summary>
    [DisallowMultipleComponent]
    public class EventWheelAnimator : MonoBehaviour
    {
        private const float DegreesPerCircle = 360f;

        [Header("References")]
        [SerializeField] private RectTransform _wheelRotator = null;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float _normalDuration = 3.4f;
        [SerializeField, Min(0.1f)] private float _fastDuration = 0.85f;
        [SerializeField, Min(1)] private int _normalTurns = 5;
        [SerializeField, Min(1)] private int _fastTurns = 2;

        private Coroutine _spinRoutine;
        private Action _completion;

        /// <summary>Gets whether a spin presentation is active.</summary>
        public bool IsSpinning => _spinRoutine != null;

        /// <summary>Assigns the transform that contains the rotating sectors.</summary>
        public void Configure(RectTransform wheelRotator) => _wheelRotator = wheelRotator;

        /// <summary>Starts a presentation toward an already selected reward sector.</summary>
        public bool Play(int sectorIndex, int sectorCount, bool fastSpin, Action onComplete)
        {
            if (_wheelRotator == null || sectorCount <= 0 || IsSpinning || !isActiveAndEnabled)
            {
                return false;
            }

            sectorIndex = Mathf.Clamp(sectorIndex, 0, sectorCount - 1);
            var sectorAngle = DegreesPerCircle / sectorCount;
            var desiredAngle = sectorIndex * sectorAngle;
            var currentAngle = _wheelRotator.localEulerAngles.z;
            var forwardDelta = Mathf.Repeat(desiredAngle - currentAngle, DegreesPerCircle);
            var turns = fastSpin ? _fastTurns : _normalTurns;
            var duration = fastSpin ? _fastDuration : _normalDuration;
            var targetAngle = currentAngle + turns * DegreesPerCircle + forwardDelta;

            _completion = onComplete;
            _spinRoutine = StartCoroutine(SpinRoutine(currentAngle, targetAngle, desiredAngle, duration));
            return true;
        }

        /// <summary>Cancels presentation only; reward state remains owned by LiveEvents.</summary>
        public void Cancel()
        {
            if (_spinRoutine != null)
            {
                StopCoroutine(_spinRoutine);
                _spinRoutine = null;
            }

            _completion = null;
        }

        private void OnDisable() => Cancel();

        private IEnumerator SpinRoutine(float startAngle, float targetAngle, float finalAngle, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var easedProgress = 1f - Mathf.Pow(1f - progress, 5f);
                SetWheelAngle(Mathf.LerpUnclamped(startAngle, targetAngle, easedProgress));
                yield return null;
            }

            SetWheelAngle(finalAngle);
            _spinRoutine = null;
            var completion = _completion;
            _completion = null;
            completion?.Invoke();
        }

        private void SetWheelAngle(float angle) =>
            _wheelRotator.localRotation = Quaternion.Euler(0f, 0f, angle);

#if UNITY_EDITOR
        private void OnValidate()
        {
            _normalDuration = Mathf.Max(0.1f, _normalDuration);
            _fastDuration = Mathf.Max(0.1f, _fastDuration);
            _normalTurns = Mathf.Max(1, _normalTurns);
            _fastTurns = Mathf.Max(1, _fastTurns);
        }
#endif
    }
}
