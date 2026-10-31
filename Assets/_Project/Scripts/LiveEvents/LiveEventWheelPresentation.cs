using UnityEngine;

namespace PawArena.LiveEvents
{
    /// <summary>Reusable visual skin for any scheduled Event Wheel.</summary>
    [CreateAssetMenu(
        fileName = "EventWheelPresentation",
        menuName = "Paw Arena/Live Event/Wheel Presentation")]
    public sealed class LiveEventWheelPresentation : ScriptableObject
    {
        [SerializeField] private string _presentationId = "default-wheel";
        [SerializeField] private Sprite _background = null;
        [SerializeField] private Sprite _headerDecoration = null;
        [SerializeField] private Color _primaryColor = new(0.11f, 0.055f, 0.18f, 1f);
        [SerializeField] private Color _secondaryColor = new(0.16f, 0.08f, 0.25f, 1f);
        [SerializeField] private Color _accentColor = new(0.64f, 0.12f, 1f, 1f);
        [SerializeField] private Color _specialSectorColor = new(1f, 0.62f, 0.04f, 1f);

        public string PresentationId => _presentationId;
        public Sprite Background => _background;
        public Sprite HeaderDecoration => _headerDecoration;
        public Color PrimaryColor => _primaryColor;
        public Color SecondaryColor => _secondaryColor;
        public Color AccentColor => _accentColor;
        public Color SpecialSectorColor => _specialSectorColor;
    }
}
