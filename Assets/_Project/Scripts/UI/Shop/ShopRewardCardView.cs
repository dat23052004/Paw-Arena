using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawArena.UI.Shop
{
    /// <summary>Renders one reward preview without owning reward or probability logic.</summary>
    [DisallowMultipleComponent]
    public sealed class ShopRewardCardView : MonoBehaviour
    {
        [SerializeField] private Image _icon = null;
        [SerializeField] private TMP_Text _title = null;
        [SerializeField] private TMP_Text _subtitle = null;
        [SerializeField] private TMP_Text _chance = null;
        [SerializeField] private GameObject _lockedOverlay = null;

        /// <summary>Applies immutable catalog data to this reward card.</summary>
        public void Bind(ShopCatalog.RewardDefinition reward)
        {
            if (reward == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            if (_icon != null)
            {
                _icon.sprite = reward.Icon;
                _icon.enabled = reward.Icon != null;
            }

            SetText(_title, reward.Title);
            SetText(_subtitle, reward.Subtitle);
            SetText(_chance, reward.ChancePercent > 0f ? $"{reward.ChancePercent:0.#}%" : string.Empty);
            if (_lockedOverlay != null)
            {
                _lockedOverlay.SetActive(reward.Locked);
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }
    }
}
