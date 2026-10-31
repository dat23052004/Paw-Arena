using System;
using System.Collections.Generic;
using PawArena.Core.Match;
using PawArena.UI.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawArena.UI.Hud
{
    /// <summary>Displays server-selected match data without making matchmaking decisions.</summary>
    [DisallowMultipleComponent]
    public sealed class MatchCountdownView : MonoBehaviour
    {
        private const string ModalDimmerName = "ModalDimmer";
        private const string ModalLayerName = "ModalLayer";

        private static readonly MatchMode[] DisplayModes =
        {
            MatchMode.Solos,
            MatchMode.Duos,
            MatchMode.Squads
        };

        [SerializeField] private CanvasGroup _canvasGroup = null;
        [SerializeField] private Image _rootBackground = null;
        [SerializeField] private TMP_Text _statusText = null;
        [SerializeField] private GameObject _mapPanel = null;
        [SerializeField] private Image _mapThumbnail = null;
        [SerializeField] private TMP_Text _mapNameText = null;
        [SerializeField] private GameObject _modePanel = null;

        private readonly Image[] _modeCards = new Image[3];
        private readonly Button[] _modeButtons = new Button[3];
        private readonly TMP_Text[] _modeLabels = new TMP_Text[3];
        private readonly TMP_Text[] _modeCounts = new TMP_Text[3];
        private Image _fallbackMapArt;
        private bool _dynamicPresentationIsolated;

        /// <summary>Raised when the player submits a vote intent for server validation.</summary>
        public event Action<MatchMode> ModeVoteRequested;

        private void Awake()
        {
            PreparePresentation();
            Hide();
        }

        private void OnEnable() => PreparePresentation();

        private void OnDisable() => UnbindModeButtonListeners();

        /// <summary>Shows the current authoritative match assignment.</summary>
        public void ShowCountdown(string mapName, int participantCount, int seconds)
        {
            ShowCountdown(
                mapName,
                null,
                new Color(0.18f, 0.62f, 0.9f, 1f),
                participantCount,
                seconds,
                false,
                MatchMode.Solos,
                null,
                null);
        }

        /// <summary>Shows map art and the mode already selected by the authoritative server.</summary>
        public void ShowCountdown(
            string mapName,
            Sprite thumbnail,
            Color thumbnailTint,
            int participantCount,
            int seconds,
            bool showModes,
            MatchMode selectedMode,
            IReadOnlyList<MatchMode> supportedModes,
            MatchModeVoteState voteState = null)
        {
            PreparePresentation();
            SetRootBackgroundVisible(true);
            SetVisible(true, showModes);
            RuntimeUiState.SetText(
                _statusText,
                $"Starting in <color=#FFD33D>{Mathf.Max(0, seconds)}</color>");

            RuntimeUiState.SetActive(_mapPanel, true);
            RuntimeUiState.SetText(
                _mapNameText,
                string.IsNullOrWhiteSpace(mapName) ? "Unknown Map" : mapName);

            if (_mapThumbnail != null)
            {
                if (_mapThumbnail.sprite != thumbnail)
                {
                    _mapThumbnail.sprite = thumbnail;
                }

                var thumbnailColor = thumbnail == null ? thumbnailTint : Color.white;
                if (_mapThumbnail.color != thumbnailColor)
                {
                    _mapThumbnail.color = thumbnailColor;
                }

                var preserveAspect = thumbnail != null;
                if (_mapThumbnail.preserveAspect != preserveAspect)
                {
                    _mapThumbnail.preserveAspect = preserveAspect;
                }
            }

            if (_fallbackMapArt != null)
            {
                RuntimeUiState.SetActive(_fallbackMapArt.gameObject, thumbnail == null);
                var fallbackColor = Color.Lerp(thumbnailTint, Color.white, 0.22f);
                if (_fallbackMapArt.color != fallbackColor)
                {
                    _fallbackMapArt.color = fallbackColor;
                }
            }

            UpdateModeCards(showModes, selectedMode, supportedModes, voteState);
        }

        /// <summary>Shows that AFK prevents the local player from receiving an assignment.</summary>
        public void ShowAfk()
        {
            PreparePresentation();
            SetRootBackgroundVisible(true);
            SetVisible(true, false);
            SetMatchDetailsVisible(false);
            RuntimeUiState.SetText(_statusText, "AFK đang bật\nBạn sẽ không được ghép trận");
        }

        /// <summary>Shows the currently followed active participant.</summary>
        public void ShowSpectating(string playerName, int activeCount)
        {
            PreparePresentation();
            SetRootBackgroundVisible(true);
            SetVisible(true, false);
            SetMatchDetailsVisible(false);
            RuntimeUiState.SetText(
                _statusText,
                $"ĐANG THEO DÕI: {playerName}\nCòn lại: {Mathf.Max(0, activeCount)}\n" +
                "Tab / phím trái-phải để đổi người xem");
        }

        /// <summary>Shows the server-confirmed winner.</summary>
        public void ShowWinner(string playerName, bool isLocalPlayer)
        {
            PreparePresentation();
            SetRootBackgroundVisible(true);
            SetVisible(true, false);
            SetMatchDetailsVisible(false);
            RuntimeUiState.SetText(
                _statusText,
                isLocalPlayer ? "BẠN CHIẾN THẮNG!" : $"NGƯỜI CHIẾN THẮNG: {playerName}");
        }

        /// <summary>Shows a server-confirmed draw after the match deadline expires.</summary>
        public void ShowDraw()
        {
            PreparePresentation();
            SetRootBackgroundVisible(true);
            SetVisible(true, false);
            SetMatchDetailsVisible(false);
            RuntimeUiState.SetText(_statusText, "HÒA!");
        }

        /// <summary>Hides the countdown once gameplay starts.</summary>
        public void Hide() => SetVisible(false, false);

        private void PreparePresentation()
        {
            PlaceBelowModalLayers();
            EnsurePresentation();
            BindModeButtonListeners();
        }

        private void EnsurePresentation()
        {
            _canvasGroup ??= GetComponent<CanvasGroup>();
            _rootBackground ??= GetComponent<Image>();
            _statusText ??= transform.Find("StatusText")?.GetComponent<TMP_Text>();
            if (_mapPanel != null && _mapThumbnail != null && _mapNameText != null &&
                _modePanel != null && BindModeCards())
            {
                IsolateDynamicPresentation();
                return;
            }

            if (transform is RectTransform rootRect)
            {
                rootRect.sizeDelta = new Vector2(700f, 440f);
            }

            var referenceFont = _statusText != null ? _statusText.font : null;
            ConfigureStatusText();
            BuildMapPanel(referenceFont);
            BuildModePanel(referenceFont);
            IsolateDynamicPresentation();
        }

        private void IsolateDynamicPresentation()
        {
            if (_dynamicPresentationIsolated || _statusText == null)
            {
                return;
            }

            RuntimeUiState.EnsureNonInteractiveNestedCanvas(_statusText.gameObject);
            _dynamicPresentationIsolated = true;
        }

        private void ConfigureStatusText()
        {
            if (_statusText == null)
            {
                return;
            }

            var rect = _statusText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(650f, 82f);
            rect.anchoredPosition = new Vector2(0f, -18f);
            _statusText.fontSize = 42f;
            _statusText.fontStyle = FontStyles.Bold;
            _statusText.alignment = TextAlignmentOptions.Center;
        }

        private void BuildMapPanel(TMP_FontAsset font)
        {
            var existing = transform.Find("MapChosenPanel");
            if (existing != null)
            {
                _mapPanel = existing.gameObject;
                _mapThumbnail = existing.Find("Thumbnail")?.GetComponent<Image>();
                _mapNameText = existing.Find("MapName")?.GetComponent<TMP_Text>();
                _fallbackMapArt = existing.Find("Thumbnail/FallbackPlatform")?.GetComponent<Image>();
                if (_mapThumbnail != null && _mapNameText != null)
                {
                    return;
                }
            }

            _mapPanel = new GameObject("MapChosenPanel", typeof(RectTransform), typeof(Image));
            _mapPanel.transform.SetParent(transform, false);
            var rect = _mapPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(410f, 210f);
            rect.anchoredPosition = new Vector2(0f, -108f);
            var background = _mapPanel.GetComponent<Image>();
            background.color = new Color32(22, 56, 91, 245);
            background.raycastTarget = false;

            CreateText(_mapPanel.transform, "MapChosenLabel", "Map chosen", new Vector2(0f, -18f),
                new Vector2(360f, 36f), 26f, font, new Vector2(0.5f, 1f));

            var thumbnailObject = new GameObject("Thumbnail", typeof(RectTransform), typeof(Image));
            thumbnailObject.transform.SetParent(_mapPanel.transform, false);
            var thumbnailRect = thumbnailObject.GetComponent<RectTransform>();
            thumbnailRect.anchorMin = new Vector2(0.5f, 0.5f);
            thumbnailRect.anchorMax = new Vector2(0.5f, 0.5f);
            thumbnailRect.sizeDelta = new Vector2(374f, 122f);
            thumbnailRect.anchoredPosition = new Vector2(0f, -8f);
            _mapThumbnail = thumbnailObject.GetComponent<Image>();
            _mapThumbnail.raycastTarget = false;

            var fallbackObject = new GameObject("FallbackPlatform", typeof(RectTransform), typeof(Image));
            fallbackObject.transform.SetParent(thumbnailObject.transform, false);
            var fallbackRect = fallbackObject.GetComponent<RectTransform>();
            fallbackRect.anchorMin = new Vector2(0.5f, 0.5f);
            fallbackRect.anchorMax = new Vector2(0.5f, 0.5f);
            fallbackRect.sizeDelta = new Vector2(190f, 92f);
            fallbackRect.anchoredPosition = new Vector2(0f, -4f);
            fallbackRect.localRotation = Quaternion.Euler(0f, 0f, -7f);
            _fallbackMapArt = fallbackObject.GetComponent<Image>();
            _fallbackMapArt.raycastTarget = false;

            _mapNameText = CreateText(_mapPanel.transform, "MapName", string.Empty, new Vector2(0f, 18f),
                new Vector2(370f, 48f), 31f, font, new Vector2(0.5f, 0f));
            _mapNameText.color = new Color32(255, 218, 62, 255);
        }

        private void BuildModePanel(TMP_FontAsset font)
        {
            var existing = transform.Find("ServerModePanel");
            if (existing != null)
            {
                _modePanel = existing.gameObject;
                if (BindModeCards())
                {
                    return;
                }
            }

            _modePanel = new GameObject("ServerModePanel", typeof(RectTransform));
            _modePanel.transform.SetParent(transform, false);
            var panelRect = _modePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = new Vector2(500f, 104f);
            panelRect.anchoredPosition = new Vector2(0f, 12f);

            for (var index = 0; index < DisplayModes.Length; index++)
            {
                var card = new GameObject(
                    $"Mode_{DisplayModes[index]}",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                card.transform.SetParent(_modePanel.transform, false);
                var rect = card.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(142f, 88f);
                rect.anchoredPosition = new Vector2((index - 1) * 158f, 0f);
                _modeCards[index] = card.GetComponent<Image>();
                _modeCards[index].raycastTarget = true;
                _modeButtons[index] = card.GetComponent<Button>();
                ConfigureModeButton(_modeButtons[index], _modeCards[index]);
                _modeLabels[index] = CreateText(card.transform, "Label", DisplayModes[index].ToString(),
                    new Vector2(0f, 4f), new Vector2(124f, 42f), 25f, font, new Vector2(0.5f, 0.5f));
                _modeCounts[index] = CreateText(card.transform, "Count", string.Empty,
                    new Vector2(52f, -33f), new Vector2(34f, 28f), 17f, font, new Vector2(0.5f, 1f));
            }
        }

        private bool BindModeCards()
        {
            if (_modePanel == null)
            {
                return false;
            }

            for (var index = 0; index < DisplayModes.Length; index++)
            {
                var card = _modePanel.transform.Find($"Mode_{DisplayModes[index]}");
                _modeCards[index] = card?.GetComponent<Image>();
                _modeButtons[index] = card?.GetComponent<Button>();
                if (card != null && _modeButtons[index] == null)
                {
                    _modeButtons[index] = card.gameObject.AddComponent<Button>();
                }

                _modeLabels[index] = card?.Find("Label")?.GetComponent<TMP_Text>();
                _modeCounts[index] = card?.Find("Count")?.GetComponent<TMP_Text>();
                if (_modeCards[index] == null || _modeButtons[index] == null ||
                    _modeLabels[index] == null || _modeCounts[index] == null)
                {
                    return false;
                }

                _modeCards[index].raycastTarget = true;
                ConfigureModeButton(_modeButtons[index], _modeCards[index]);
            }

            BindModeButtonListeners();
            return true;
        }

        private void UpdateModeCards(bool showModes, MatchMode selectedMode,
            IReadOnlyList<MatchMode> supportedModes, MatchModeVoteState voteState)
        {
            if (_modePanel == null)
            {
                return;
            }

            RuntimeUiState.SetActive(_modePanel, showModes);
            if (!showModes)
            {
                return;
            }

            for (var index = 0; index < DisplayModes.Length; index++)
            {
                var mode = DisplayModes[index];
                var supported = ContainsMode(supportedModes, mode);
                RuntimeUiState.SetActive(_modeCards[index].gameObject, supported);
                if (_modeButtons[index].interactable != supported)
                {
                    _modeButtons[index].interactable = supported;
                }
                if (!supported)
                {
                    continue;
                }

                var selected = mode == selectedMode;
                var cardColor = selected
                    ? new Color32(255, 196, 48, 255)
                    : new Color32(61, 160, 218, 230);
                if (_modeCards[index].color != cardColor)
                {
                    _modeCards[index].color = cardColor;
                }

                Color labelColor = selected ? new Color32(34, 36, 41, 255) : Color.white;
                if (_modeLabels[index].color != labelColor)
                {
                    _modeLabels[index].color = labelColor;
                }

                RuntimeUiState.SetText(
                    _modeCounts[index],
                    (voteState?.GetVoteCount(mode) ?? 0).ToString());
            }
        }

        private static bool ContainsMode(IReadOnlyList<MatchMode> modes, MatchMode target)
        {
            if (modes == null)
            {
                return target == MatchMode.Solos;
            }

            for (var index = 0; index < modes.Count; index++)
            {
                if (modes[index] == target)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetMatchDetailsVisible(bool visible)
        {
            RuntimeUiState.SetActive(_mapPanel, visible);
            RuntimeUiState.SetActive(_modePanel, visible);
        }

        private void SetVisible(bool visible, bool allowInteraction)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            RuntimeUiState.SetCanvasGroup(
                _canvasGroup,
                visible ? 1f : 0f,
                visible && allowInteraction,
                visible && allowInteraction);
        }

        private void SetRootBackgroundVisible(bool visible)
        {
            if (_rootBackground != null)
            {
                if (_rootBackground.enabled != visible)
                {
                    _rootBackground.enabled = visible;
                }
            }
        }

        private void PlaceBelowModalLayers()
        {
            var parent = transform.parent;
            if (parent == null)
            {
                return;
            }

            var currentIndex = transform.GetSiblingIndex();
            var targetIndex = currentIndex;
            var modalDimmer = parent.Find(ModalDimmerName);
            if (modalDimmer != null)
            {
                targetIndex = Mathf.Min(targetIndex, modalDimmer.GetSiblingIndex());
            }

            var modalLayer = parent.Find(ModalLayerName);
            if (modalLayer != null)
            {
                targetIndex = Mathf.Min(targetIndex, modalLayer.GetSiblingIndex());
            }

            if (targetIndex < currentIndex)
            {
                transform.SetSiblingIndex(targetIndex);
            }
        }

        private void BindModeButtonListeners()
        {
            if (_modeButtons[0] == null || _modeButtons[1] == null || _modeButtons[2] == null)
            {
                return;
            }

            UnbindModeButtonListeners();
            _modeButtons[0].onClick.AddListener(HandleSolosClicked);
            _modeButtons[1].onClick.AddListener(HandleDuosClicked);
            _modeButtons[2].onClick.AddListener(HandleSquadsClicked);
        }

        private void UnbindModeButtonListeners()
        {
            _modeButtons[0]?.onClick.RemoveListener(HandleSolosClicked);
            _modeButtons[1]?.onClick.RemoveListener(HandleDuosClicked);
            _modeButtons[2]?.onClick.RemoveListener(HandleSquadsClicked);
        }

        private void HandleSolosClicked() => ModeVoteRequested?.Invoke(MatchMode.Solos);

        private void HandleDuosClicked() => ModeVoteRequested?.Invoke(MatchMode.Duos);

        private void HandleSquadsClicked() => ModeVoteRequested?.Invoke(MatchMode.Squads);

        private static void ConfigureModeButton(Button button, Graphic targetGraphic)
        {
            button.targetGraphic = targetGraphic;
            button.navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.None
            };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string value,
            Vector2 anchoredPosition, Vector2 size, float fontSize, TMP_FontAsset font, Vector2 anchor)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.text = value;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(12f, fontSize - 8f);
            text.color = Color.white;
            text.outlineColor = new Color32(10, 22, 51, 242);
            text.outlineWidth = 0.14f;
            text.raycastTarget = false;
            return text;
        }
    }
}
