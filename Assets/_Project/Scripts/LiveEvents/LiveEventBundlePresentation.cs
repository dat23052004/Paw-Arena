using UnityEngine;

namespace PawArena.LiveEvents
{
    /// <summary>Reusable visual skin for Bundle layouts across multiple Events.</summary>
    [CreateAssetMenu(
        fileName = "EventBundlePresentation",
        menuName = "Paw Arena/Live Event/Bundle Presentation")]
    public sealed class LiveEventBundlePresentation : ScriptableObject
    {
        [SerializeField] private string _presentationId = "default-bundle";
        [SerializeField] private Sprite _background = null;
        [SerializeField] private Sprite _cardBackground = null;
        [SerializeField] private Color _primaryColor = new(0.08f, 0.04f, 0.18f, 1f);
        [SerializeField] private Color _accentColor = new(0.42f, 0.85f, 0.12f, 1f);
        [SerializeField] private Color _titleColor = Color.white;

        public string PresentationId => _presentationId;
        public Sprite Background => _background;
        public Sprite CardBackground => _cardBackground;
        public Color PrimaryColor => _primaryColor;
        public Color AccentColor => _accentColor;
        public Color TitleColor => _titleColor;
    }
}
