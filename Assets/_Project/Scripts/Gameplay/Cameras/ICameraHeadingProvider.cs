using UnityEngine;

namespace PawArena.Gameplay.Cameras
{
    /// <summary>Provides camera-relative facing intent without coupling player movement to Cinemachine.</summary>
    public interface ICameraHeadingProvider
    {
        Vector3 PlanarForward { get; }
        float PlanarTurnSpeed { get; }
        void AlignBehindTarget();
    }
}
