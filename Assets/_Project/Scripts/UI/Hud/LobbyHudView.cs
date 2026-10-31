using System;
using PawArena.Core.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PawArena.UI.Hud
{
    /// <summary>Connects lobby HUD controls to wallet state and local AFK presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class LobbyHudView : MonoBehaviour
    {
        private const long CandyTestGrant = 10L;
        private const long CoinTestGrant = 500L;

        [Header("Currency")]
        [SerializeField] private TMP_Text _candyText = null;
        [SerializeField] private TMP_Text _coinText = null;
        [SerializeField] private Button _addCandyButton = null;
        [SerializeField] private Button _addCoinButton = null;

        [Header("AFK")]
        [SerializeField] private Button _afkToggleButton = null;
        [SerializeField] private Image _afkToggleBackground = null;
        [SerializeField] private RectTransform _afkToggleKnob = null;
        [SerializeField] private TMP_Text _afkStateText = null;
        [SerializeField] private Sprite _toggleOnSprite = null;
        [SerializeField] private Sprite _toggleOffSprite = null;

        private IWalletService _wallet;
        private Action _save;
        private bool _isAfk;
        private bool _isBound;

        /// <summary>Raised whenever the local player changes AFK state.</summary>
        public event Action<bool> AfkChanged;

        /// <summary>True while the local player has enabled AFK mode.</summary>
        public bool IsAfk => _isAfk;

        /// <summary>Binds wallet-backed counters and lobby-only controls.</summary>
        public void Bind(IWalletService wallet, Action save)
        {
            Unbind();
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _save = save;
            _wallet.BalanceChanged += HandleBalanceChanged;
            _addCandyButton?.onClick.AddListener(HandleAddCandyRequested);
            _addCoinButton?.onClick.AddListener(HandleAddCoinRequested);
            _afkToggleButton?.onClick.AddListener(HandleAfkToggleRequested);
            _isBound = true;
            RefreshCurrency();
            RefreshAfk();
        }

        /// <summary>Disconnects wallet and button callbacks.</summary>
        public void Unbind()
        {
            if (!_isBound)
            {
                return;
            }

            if (_wallet != null)
            {
                _wallet.BalanceChanged -= HandleBalanceChanged;
            }

            _addCandyButton?.onClick.RemoveListener(HandleAddCandyRequested);
            _addCoinButton?.onClick.RemoveListener(HandleAddCoinRequested);
            _afkToggleButton?.onClick.RemoveListener(HandleAfkToggleRequested);
            _wallet = null;
            _save = null;
            _isBound = false;
        }

        private void OnDestroy() => Unbind();

        private void HandleAddCandyRequested() => Grant(CurrencyType.Candy, CandyTestGrant);
        private void HandleAddCoinRequested() => Grant(CurrencyType.Coins, CoinTestGrant);

        private void Grant(CurrencyType currency, long amount)
        {
            if (_wallet == null)
            {
                return;
            }

            var result = _wallet.TryApply(
                "hud-test-grant:" + Guid.NewGuid().ToString("N"),
                currency,
                amount,
                "hud_test_grant",
                nameof(LobbyHudView),
                out _);
            if (result == WalletTransactionResult.Success)
            {
                _save?.Invoke();
            }
        }

        private void HandleAfkToggleRequested()
        {
            _isAfk = !_isAfk;
            RefreshAfk();
            AfkChanged?.Invoke(_isAfk);
        }

        private void HandleBalanceChanged(CurrencyType currency, long balance)
        {
            switch (currency)
            {
                case CurrencyType.Candy when _candyText != null:
                    _candyText.text = balance.ToString("N0");
                    break;
                case CurrencyType.Coins when _coinText != null:
                    _coinText.text = balance.ToString("N0");
                    break;
            }
        }

        private void RefreshCurrency()
        {
            if (_wallet == null)
            {
                return;
            }

            HandleBalanceChanged(CurrencyType.Candy, _wallet.GetBalance(CurrencyType.Candy));
            HandleBalanceChanged(CurrencyType.Coins, _wallet.GetBalance(CurrencyType.Coins));
        }

        private void RefreshAfk()
        {
            if (_afkStateText != null)
            {
                _afkStateText.text = _isAfk ? "On" : "Off";
                var statePosition = _afkStateText.rectTransform.anchoredPosition;
                statePosition.x = _isAfk ? -18f : 18f;
                _afkStateText.rectTransform.anchoredPosition = statePosition;
            }

            if (_afkToggleBackground != null)
            {
                _afkToggleBackground.sprite = _isAfk ? _toggleOnSprite : _toggleOffSprite;
                _afkToggleBackground.color = _isAfk
                    ? new Color(0.45f, 1f, 0.58f, 1f)
                    : new Color(0.3f, 0.39f, 0.57f, 1f);
            }

            if (_afkToggleKnob != null)
            {
                var position = _afkToggleKnob.anchoredPosition;
                position.x = _isAfk ? 23f : -23f;
                _afkToggleKnob.anchoredPosition = position;
            }
        }
    }
}
