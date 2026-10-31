using System;
using PawArena.UI.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawArena.UI.Hud
{
    /// <summary>Displays spectator navigation and forwards button intent without owning match logic.</summary>
    [DisallowMultipleComponent]
    public sealed class SpectatorHudView : MonoBehaviour
    {
        [Header("View References")]
        [SerializeField] private CanvasGroup _canvasGroup = null;
        [SerializeField] private Button _previousButton = null;
        [SerializeField] private Button _nextButton = null;
        [SerializeField] private Button _stopButton = null;
        [SerializeField] private Button _inviteButton = null;
        [SerializeField] private TMP_Text _targetText = null;
        [SerializeField] private TMP_Text _watchingText = null;

        [Header("Runtime Setup")]
        [SerializeField] private Canvas _parentCanvas = null;
        [SerializeField] private Sprite _buttonSprite = null;
        [SerializeField] private Sprite _arrowSprite = null;
        [SerializeField] private Vector2 _anchoredPosition = new(0f, 72f);
        [SerializeField] private Vector2 _size = new(580f, 238f);
        [SerializeField] private Vector2 _previousButtonPosition = new(-220f, 82f);
        [SerializeField] private Vector2 _nextButtonPosition = new(220f, 82f);
        [SerializeField] private Vector2 _titlePosition = new(0f, 114f);
        [SerializeField] private Vector2 _targetPosition = new(0f, 66f);

        /// <summary>Raised when the local spectator requests the previous active participant.</summary>
        public event Action PreviousRequested;

        /// <summary>Raised when the local spectator requests the next active participant.</summary>
        public event Action NextRequested;

        /// <summary>Raised when the local player wants to leave spectator camera mode.</summary>
        public event Action StopRequested;

        /// <summary>Raised when the local player asks the platform layer to open friend invitations.</summary>
        public event Action InviteRequested;

        /// <summary>Gets whether spectator navigation is currently visible.</summary>
        public bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0f;

        private void Awake()
        {
            if (!HasCompleteReferences())
            {
                TryBindOrBuildView();
            }
            Hide();
        }

        private void OnEnable()
        {
            if (!HasCompleteReferences() && !TryBindOrBuildView())
            {
                Debug.LogError($"{nameof(SpectatorHudView)} could not create its view.", this);
                enabled = false;
                return;
            }

            if (_previousButton != null)
            {
                _previousButton.onClick.AddListener(HandlePreviousClicked);
            }

            if (_nextButton != null)
            {
                _nextButton.onClick.AddListener(HandleNextClicked);
            }

            if (_stopButton != null)
            {
                _stopButton.onClick.AddListener(HandleStopClicked);
            }

            if (_inviteButton != null)
            {
                _inviteButton.onClick.AddListener(HandleInviteClicked);
            }
        }

        private void OnDisable()
        {
            if (_previousButton != null)
            {
                _previousButton.onClick.RemoveListener(HandlePreviousClicked);
            }

            if (_nextButton != null)
            {
                _nextButton.onClick.RemoveListener(HandleNextClicked);
            }

            if (_stopButton != null)
            {
                _stopButton.onClick.RemoveListener(HandleStopClicked);
            }

            if (_inviteButton != null)
            {
                _inviteButton.onClick.RemoveListener(HandleInviteClicked);
            }
        }

        /// <summary>Shows the active target and enables arrows only when another target exists.</summary>
        public void Show(string targetName, bool canCycle, int watchingCount = 1)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _targetText.text = targetName ?? string.Empty;
            _watchingText.text = $"{Mathf.Max(1, watchingCount)} watching";
            _previousButton.interactable = canCycle;
            _nextButton.interactable = canCycle;
            RuntimeUiState.SetActive(_stopButton.gameObject, true);
            RuntimeUiState.SetActive(_inviteButton.gameObject, true);
            SetVisible(true);
        }

        /// <summary>Shows the compact controls that allow an eliminated player to spectate again.</summary>
        public void ShowAvailable()
        {
            if (_canvasGroup == null)
            {
                return;
            }

            RuntimeUiState.SetText(_targetText, string.Empty);
            RuntimeUiState.SetText(_watchingText, string.Empty);
            _previousButton.interactable = true;
            _nextButton.interactable = true;
            RuntimeUiState.SetActive(_stopButton.gameObject, false);
            RuntimeUiState.SetActive(_inviteButton.gameObject, false);
            SetVisible(true);
        }

        /// <summary>Hides spectator navigation outside active spectating.</summary>
        public void Hide()
        {
            if (_targetText != null)
            {
                _targetText.text = string.Empty;
            }

            if (_watchingText != null)
            {
                _watchingText.text = string.Empty;
            }

            if (_previousButton != null)
            {
                _previousButton.interactable = false;
            }

            if (_nextButton != null)
            {
                _nextButton.interactable = false;
            }

            if (_stopButton != null)
            {
                RuntimeUiState.SetActive(_stopButton.gameObject, true);
            }

            if (_inviteButton != null)
            {
                RuntimeUiState.SetActive(_inviteButton.gameObject, true);
            }

            SetVisible(false);
        }

        private void HandlePreviousClicked() => PreviousRequested?.Invoke();

        private void HandleNextClicked() => NextRequested?.Invoke();

        private void HandleStopClicked() => StopRequested?.Invoke();

        private void HandleInviteClicked() => InviteRequested?.Invoke();

        private void SetVisible(bool visible)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private bool TryBindOrBuildView()
        {
            _parentCanvas ??= FindRootCanvas();
            if (_parentCanvas == null)
            {
                return false;
            }

            var existing = _parentCanvas.transform.Find("SpectatorHudOverlay");
            if (existing != null && TryBindExistingView(existing))
            {
                return true;
            }

            BuildView(_parentCanvas.transform);
            return HasCompleteReferences();
        }

        private Canvas FindRootCanvas()
        {
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            Canvas fallback = null;
            for (var index = 0; index < canvases.Length; index++)
            {
                var canvas = canvases[index];
                if (!canvas.isRootCanvas)
                {
                    continue;
                }

                if (canvas.name == "PawArenaCanvas")
                {
                    return canvas;
                }

                fallback ??= canvas;
            }

            return fallback;
        }

        private bool TryBindExistingView(Transform overlay)
        {
            _canvasGroup = overlay.GetComponent<CanvasGroup>();
            _previousButton = overlay.Find("SpectatorPreviousButton")?.GetComponent<Button>();
            _nextButton = overlay.Find("SpectatorNextButton")?.GetComponent<Button>();
            _stopButton = overlay.Find("SpectatorStopButton")?.GetComponent<Button>();
            _inviteButton = overlay.Find("SpectatorInviteButton")?.GetComponent<Button>();
            _targetText = overlay.Find("SpectatorTarget")?.GetComponent<TMP_Text>();
            _watchingText = overlay.Find("SpectatorWatching")?.GetComponent<TMP_Text>();
            return HasCompleteReferences();
        }

        private void BuildView(Transform parent)
        {
            var overlay = new GameObject(
                "SpectatorHudOverlay",
                typeof(RectTransform),
                typeof(CanvasGroup));
            overlay.transform.SetParent(parent, false);
            overlay.transform.SetAsLastSibling();

            var rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = _size;
            rect.anchoredPosition = _anchoredPosition;

            _canvasGroup = overlay.GetComponent<CanvasGroup>();
            _previousButton = CreateButton(
                overlay.transform,
                "SpectatorPreviousButton",
                _previousButtonPosition,
                false);
            _nextButton = CreateButton(
                overlay.transform,
                "SpectatorNextButton",
                _nextButtonPosition,
                true);

            var referenceFont = parent.GetComponentInChildren<TMP_Text>(true)?.font;
            _stopButton = CreateActionButton(
                overlay.transform,
                "SpectatorStopButton",
                "Stop spectating",
                new Vector2(-108f, 202f),
                new Color32(224, 77, 72, 255),
                referenceFont);
            _inviteButton = CreateActionButton(
                overlay.transform,
                "SpectatorInviteButton",
                "Invite friends",
                new Vector2(108f, 202f),
                new Color32(62, 190, 78, 255),
                referenceFont);
            CreateText(
                overlay.transform,
                "SpectatorTitle",
                "Spectate",
                _titlePosition,
                new Vector2(300f, 58f),
                48f,
                36f,
                referenceFont);
            _targetText = CreateText(
                overlay.transform,
                "SpectatorTarget",
                string.Empty,
                _targetPosition,
                new Vector2(310f, 38f),
                26f,
                18f,
                referenceFont);
            _watchingText = CreateText(
                overlay.transform,
                "SpectatorWatching",
                string.Empty,
                new Vector2(0f, 22f),
                new Vector2(260f, 30f),
                20f,
                15f,
                referenceFont);
        }

        private static Button CreateActionButton(
            Transform parent,
            string objectName,
            string label,
            Vector2 anchoredPosition,
            Color color,
            TMP_FontAsset font)
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(202f, 48f);
            rect.anchoredPosition = anchoredPosition;

            var image = buttonObject.GetComponent<Image>();
            image.color = color;
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.None
            };

            CreateText(
                buttonObject.transform,
                "Label",
                label,
                new Vector2(0f, 24f),
                new Vector2(184f, 40f),
                23f,
                17f,
                font);
            return button;
        }

        private Button CreateButton(
            Transform parent,
            string objectName,
            Vector2 anchoredPosition,
            bool pointsRight)
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(84f, 92f);
            rect.anchoredPosition = anchoredPosition;

            var background = buttonObject.GetComponent<Image>();
            background.sprite = _buttonSprite;
            background.type = _buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            background.color = _buttonSprite != null
                ? Color.white
                : new Color32(52, 122, 224, 255);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.None
            };
            var colors = button.colors;
            colors.highlightedColor = new Color(0.9f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.82f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.5f, 0.55f, 0.65f, 0.65f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (_arrowSprite != null)
            {
                var arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
                arrowObject.transform.SetParent(buttonObject.transform, false);
                var arrowRect = arrowObject.GetComponent<RectTransform>();
                arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
                arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
                arrowRect.pivot = new Vector2(0.5f, 0.5f);
                arrowRect.sizeDelta = new Vector2(62f, 62f);
                arrowRect.anchoredPosition = Vector2.zero;
                arrowRect.localScale = new Vector3(pointsRight ? 1f : -1f, 1f, 1f);
                var arrowImage = arrowObject.GetComponent<Image>();
                arrowImage.sprite = _arrowSprite;
                arrowImage.preserveAspect = true;
                arrowImage.raycastTarget = false;
            }
            else
            {
                CreateText(
                    buttonObject.transform,
                    "Arrow",
                    pointsRight ? ">" : "<",
                    Vector2.zero,
                    new Vector2(62f, 62f),
                    54f,
                    42f,
                    null);
            }

            return button;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string objectName,
            string value,
            Vector2 anchoredPosition,
            Vector2 size,
            float maximumFontSize,
            float minimumFontSize,
            TMP_FontAsset font)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
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
            text.enableAutoSizing = true;
            text.fontSizeMax = maximumFontSize;
            text.fontSizeMin = minimumFontSize;
            text.color = Color.white;
            text.outlineColor = new Color32(10, 22, 51, 242);
            text.outlineWidth = 0.18f;
            text.raycastTarget = false;
            return text;
        }

        private bool HasCompleteReferences()
        {
            return _canvasGroup != null && _previousButton != null &&
                   _nextButton != null && _stopButton != null && _inviteButton != null &&
                   _targetText != null && _watchingText != null;
        }
    }
}
