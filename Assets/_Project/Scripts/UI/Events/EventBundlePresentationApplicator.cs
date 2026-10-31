using PawArena.LiveEvents;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawArena.UI.Events
{
    /// <summary>Applies a reusable Bundle skin without owning Bundle business state.</summary>
    public static class EventBundlePresentationApplicator
    {
        /// <summary>Applies the primary Bundle profile to the shared detail and carousel chrome.</summary>
        public static void Apply(
            LiveEventBundlePresentation presentation,
            Image panelBackground,
            Image detailsBackground,
            Image featuredBackground,
            TMP_Text title,
            TMP_Text featuredTitle,
            TMP_Text discount,
            Button purchaseButton)
        {
            if (presentation == null)
            {
                return;
            }

            ApplyImage(panelBackground, presentation.Background, presentation.PrimaryColor);
            ApplyImage(detailsBackground, presentation.CardBackground, presentation.PrimaryColor);
            ApplyImage(featuredBackground, presentation.CardBackground, presentation.PrimaryColor);
            SetColor(title, presentation.TitleColor);
            SetColor(featuredTitle, presentation.TitleColor);
            SetColor(discount, presentation.AccentColor);
            if (purchaseButton != null && purchaseButton.targetGraphic != null)
            {
                purchaseButton.targetGraphic.color = presentation.AccentColor;
            }
        }

        /// <summary>Applies the profile owned by the currently displayed carousel Bundle.</summary>
        public static void ApplyFeatured(
            LiveEventBundlePresentation presentation,
            Image background,
            TMP_Text title,
            TMP_Text status)
        {
            if (presentation == null)
            {
                return;
            }

            ApplyImage(background, presentation.CardBackground, presentation.PrimaryColor);
            SetColor(title, presentation.TitleColor);
            SetColor(status, presentation.AccentColor);
        }

        private static void ApplyImage(Image image, Sprite sprite, Color color)
        {
            if (image == null)
            {
                return;
            }

            if (sprite != null)
            {
                image.sprite = sprite;
            }

            image.color = color;
        }

        private static void SetColor(Graphic graphic, Color color)
        {
            if (graphic != null)
            {
                graphic.color = color;
            }
        }
    }
}
