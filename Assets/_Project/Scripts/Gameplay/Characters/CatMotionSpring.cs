using UnityEngine;

namespace PawArena.Gameplay.Characters
{
    /// <summary>Closed-form damped spring, stable for changing render frame durations.</summary>
    public struct CatMotionSpring
    {
        public Vector3 Value { get; private set; }
        public Vector3 Velocity { get; private set; }
        public void Reset() { Value = Velocity = Vector3.zero; }
        public void Step(Vector3 target, float frequency, float damping, float delta)
        {
            if (!float.IsFinite(delta) || delta <= 0) { return; }
            var omega = 2 * Mathf.PI * Mathf.Max(.01f, frequency);
            var decay = omega * Mathf.Clamp(damping, .05f, .99f);
            var oscillation = Mathf.Sqrt(omega * omega - decay * decay);
            var offset = Value - target;
            var coefficient = (Velocity + decay * offset) / oscillation;
            var sine = Mathf.Sin(oscillation * delta); var cosine = Mathf.Cos(oscillation * delta);
            var envelope = Mathf.Exp(-decay * delta);
            Value = target + envelope * (offset * cosine + coefficient * sine);
            Velocity = envelope * (-decay * (offset * cosine + coefficient * sine) + oscillation * (coefficient * cosine - offset * sine));
        }
    }
}
