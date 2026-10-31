using PawArena.UI.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawArena.UI.Hud
{
    /// <summary>Displays the selected ability below a non-overlapping in-match control guide.</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayAbilityHudView : MonoBehaviour
    {
        private const string EventWindowName = "Window_Event";
        private const string ModalDimmerName = "ModalDimmer";

        private static readonly string[] ControlRootNames =
        {
            "Control_Punch",
            "Control_Grab",
            "Control_Lift",
            "Control_Jump"
        };

        private static readonly string[] EventObjectNames =
        {
            "Nav_Event",
            "EventSparkle",
            "EventBadge"
        };

        private static readonly string[] LobbyOnlyRootNames =
        {
            "HUD_NavigationPanel",
            "HUD_CurrencyPanel"
        };

        [SerializeField] private Canvas _parentCanvas = null;
        [SerializeField] private Sprite[] _abilityIcons = new Sprite[0];

        [Header("Match Layout")]
        [SerializeField] private Vector2 _cardPosition = new(-28f, 24f);
        [SerializeField] private Vector2 _cardSize = new(190f, 210f);
        [SerializeField] private Vector2 _controlGuideSize = new(270f, 232f);
        [SerializeField, Min(0f)] private float _columnGap = 12f;

        [Header("Runtime Bindings")]
        [SerializeField] private CanvasGroup _canvasGroup = null;
        [SerializeField] private Image _abilityIcon = null;
        [SerializeField] private Image _cooldownOverlay = null;
        [SerializeField] private TMP_Text _abilityNameText = null;
        [SerializeField] private TMP_Text _cooldownText = null;

        private readonly RectTransform[] _controlRoots = new RectTransform[4];
        private readonly Transform[] _originalControlParents = new Transform[4];
        private readonly int[] _originalControlSiblingIndexes = new int[4];
        private readonly Vector2[] _originalControlAnchorMin = new Vector2[4];
        private readonly Vector2[] _originalControlAnchorMax = new Vector2[4];
        private readonly Vector2[] _originalControlPivots = new Vector2[4];
        private readonly Vector2[] _originalControlSizes = new Vector2[4];
        private readonly Vector2[] _originalControlPositions = new Vector2[4];
        private readonly Vector3[] _originalControlScales = new Vector3[4];
        private readonly Quaternion[] _originalControlRotations = new Quaternion[4];
        private readonly GameObject[] _eventObjects = new GameObject[3];
        private readonly bool[] _eventOriginalActive = new bool[3];
        private readonly GameObject[] _lobbyOnlyRoots = new GameObject[2];
        private readonly bool[] _lobbyOnlyOriginalActive = new bool[2];

        private RectTransform _matchControlsRoot;
        private GameObject _eventWindow;
        private UIScreen _eventScreen;
        private GameObject _modalDimmer;
        private bool _initialized;
        private bool _matchLayoutActive;
        private int _lastCooldownSecond = -1;

        /// <summary>Gets whether the in-match skill card is visible.</summary>
        public bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0f;

        private void Awake()
        {
            if (!EnsureInitialized())
            {
                Debug.LogWarning("Gameplay ability HUD could not find a root canvas.", this);
                enabled = false;
                return;
            }

            Hide();
        }

        private void OnDisable() => SetMatchLayoutActive(false);

        private void OnValidate()
        {
            _cardSize.x = Mathf.Max(140f, _cardSize.x);
            _cardSize.y = Mathf.Max(160f, _cardSize.y);
            _controlGuideSize.x = Mathf.Max(220f, _controlGuideSize.x);
            _controlGuideSize.y = Mathf.Max(208f, _controlGuideSize.y);
            _columnGap = Mathf.Max(0f, _columnGap);
        }

        /// <summary>Shows the selected ability below the control guide and hides Event UI.</summary>
        public void ShowAbility(int abilityIndex, string displayName, float cooldownNormalized, float seconds)
        {
            if (!EnsureInitialized() || _canvasGroup == null)
            {
                return;
            }

            SetMatchLayoutActive(true);
            CloseOpenEventWindow();
            if (_matchControlsRoot != null)
            {
                RuntimeUiState.SetActive(_matchControlsRoot.gameObject, true);
            }

            RuntimeUiState.SetCanvasGroup(_canvasGroup, 1f, false, false);
            RuntimeUiState.SetText(_abilityNameText, displayName);

            if (_abilityIcon != null)
            {
                var icon = abilityIndex >= 0 && abilityIndex < _abilityIcons.Length
                    ? _abilityIcons[abilityIndex]
                    : null;
                if (_abilityIcon.sprite != icon)
                {
                    _abilityIcon.sprite = icon;
                }

                Color iconColor = icon == null
                    ? new Color32(193, 156, 255, 255)
                    : Color.white;
                if (_abilityIcon.color != iconColor)
                {
                    _abilityIcon.color = iconColor;
                }

                var preserveAspect = icon != null;
                if (_abilityIcon.preserveAspect != preserveAspect)
                {
                    _abilityIcon.preserveAspect = preserveAspect;
                }
            }

            _lastCooldownSecond = -1;
            SetCooldown(cooldownNormalized, seconds);
        }

        /// <summary>Updates the dark cooldown mask from the gameplay-owned timer.</summary>
        public void SetCooldown(float normalized, float seconds)
        {
            normalized = Mathf.Clamp01(normalized);
            if (_cooldownOverlay != null)
            {
                if (!Mathf.Approximately(_cooldownOverlay.fillAmount, normalized))
                {
                    _cooldownOverlay.fillAmount = normalized;
                }

                RuntimeUiState.SetActive(_cooldownOverlay.gameObject, normalized > 0.001f);
            }

            var displayedSecond = normalized > 0.001f ? Mathf.CeilToInt(Mathf.Max(0f, seconds)) : 0;
            if (_cooldownText == null || displayedSecond == _lastCooldownSecond)
            {
                return;
            }

            _lastCooldownSecond = displayedSecond;
            RuntimeUiState.SetText(
                _cooldownText,
                displayedSecond > 0 ? displayedSecond.ToString() : "READY");
            Color cooldownColor = displayedSecond > 0
                ? Color.white
                : new Color32(139, 255, 143, 255);
            if (_cooldownText.color != cooldownColor)
            {
                _cooldownText.color = cooldownColor;
            }
        }

        /// <summary>Hides the skill card, restores lobby navigation, and returns controls to lobby layout.</summary>
        public void Hide()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            HideCard();

            SetMatchLayoutActive(false);
        }

        private bool EnsureInitialized()
        {
            if (_initialized)
            {
                return true;
            }

            _initialized = TryBindOrBuild();
            return _initialized;
        }

        private bool TryBindOrBuild()
        {
            _parentCanvas ??= FindRootCanvas();
            if (_parentCanvas == null)
            {
                return false;
            }

            BindHudElements();
            EnsureMatchControlsRoot();
            var existing = _parentCanvas.transform.Find("GameplayAbilityHud");
            if (existing != null && BindExisting(existing))
            {
                ConfigureCardRect((RectTransform)existing);
                RuntimeUiState.EnsureNonInteractiveNestedCanvas(existing.gameObject);
                return true;
            }

            BuildView(_parentCanvas.transform);
            RuntimeUiState.EnsureNonInteractiveNestedCanvas(_canvasGroup?.gameObject);
            return _canvasGroup != null && _abilityIcon != null && _cooldownOverlay != null &&
                   _abilityNameText != null && _cooldownText != null;
        }

        private void BindHudElements()
        {
            var transforms = _parentCanvas.GetComponentsInChildren<RectTransform>(true);
            for (var rootIndex = 0; rootIndex < ControlRootNames.Length; rootIndex++)
            {
                for (var index = 0; index < transforms.Length; index++)
                {
                    if (transforms[index].name != ControlRootNames[rootIndex])
                    {
                        continue;
                    }

                    var control = transforms[index];
                    _controlRoots[rootIndex] = control;
                    _originalControlParents[rootIndex] = control.parent;
                    _originalControlSiblingIndexes[rootIndex] = control.GetSiblingIndex();
                    _originalControlAnchorMin[rootIndex] = control.anchorMin;
                    _originalControlAnchorMax[rootIndex] = control.anchorMax;
                    _originalControlPivots[rootIndex] = control.pivot;
                    _originalControlSizes[rootIndex] = control.sizeDelta;
                    _originalControlPositions[rootIndex] = control.anchoredPosition;
                    _originalControlScales[rootIndex] = control.localScale;
                    _originalControlRotations[rootIndex] = control.localRotation;
                    break;
                }
            }

            for (var eventIndex = 0; eventIndex < EventObjectNames.Length; eventIndex++)
            {
                for (var index = 0; index < transforms.Length; index++)
                {
                    if (transforms[index].name != EventObjectNames[eventIndex])
                    {
                        continue;
                    }

                    _eventObjects[eventIndex] = transforms[index].gameObject;
                    _eventOriginalActive[eventIndex] = transforms[index].gameObject.activeSelf;
                    break;
                }
            }

            for (var rootIndex = 0; rootIndex < LobbyOnlyRootNames.Length; rootIndex++)
            {
                for (var index = 0; index < transforms.Length; index++)
                {
                    if (transforms[index].name != LobbyOnlyRootNames[rootIndex])
                    {
                        continue;
                    }

                    _lobbyOnlyRoots[rootIndex] = transforms[index].gameObject;
                    _lobbyOnlyOriginalActive[rootIndex] = transforms[index].gameObject.activeSelf;
                    break;
                }
            }

            for (var index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == EventWindowName)
                {
                    _eventWindow = transforms[index].gameObject;
                    _eventScreen = _eventWindow.GetComponent<UIScreen>();
                }
                else if (transforms[index].name == ModalDimmerName)
                {
                    _modalDimmer = transforms[index].gameObject;
                }
            }
        }

        private void EnsureMatchControlsRoot()
        {
            var existing = _parentCanvas.transform.Find("GameplayControlGuide");
            if (existing != null)
            {
                _matchControlsRoot = existing as RectTransform;
            }
            else
            {
                var root = new GameObject("GameplayControlGuide", typeof(RectTransform));
                root.transform.SetParent(_parentCanvas.transform, false);
                _matchControlsRoot = root.GetComponent<RectTransform>();
            }

            _matchControlsRoot.anchorMin = new Vector2(1f, 0f);
            _matchControlsRoot.anchorMax = new Vector2(1f, 0f);
            _matchControlsRoot.pivot = new Vector2(1f, 0f);
            _matchControlsRoot.sizeDelta = _controlGuideSize;
            _matchControlsRoot.anchoredPosition = new Vector2(
                _cardPosition.x,
                _cardPosition.y + _cardSize.y + _columnGap);
            RuntimeUiState.SetActive(_matchControlsRoot.gameObject, false);
        }

        private bool BindExisting(Transform root)
        {
            _canvasGroup = root.GetComponent<CanvasGroup>();
            _abilityIcon = root.Find("AbilityIcon")?.GetComponent<Image>();
            _cooldownOverlay = root.Find("CooldownOverlay")?.GetComponent<Image>();
            _abilityNameText = root.Find("AbilityName")?.GetComponent<TMP_Text>();
            _cooldownText = root.Find("CooldownText")?.GetComponent<TMP_Text>();
            return _canvasGroup != null && _abilityIcon != null && _cooldownOverlay != null &&
                   _abilityNameText != null && _cooldownText != null;
        }

        private void BuildView(Transform parent)
        {
            var root = new GameObject(
                "GameplayAbilityHud",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            ConfigureCardRect(rootRect);
            var background = root.GetComponent<Image>();
            background.color = new Color32(66, 42, 117, 242);
            background.raycastTarget = false;
            _canvasGroup = root.GetComponent<CanvasGroup>();

            var iconSize = Mathf.Min(_cardSize.x - 28f, _cardSize.y - 58f);
            var iconObject = new GameObject("AbilityIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(root.transform, false);
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(0f, -10f);
            _abilityIcon = iconObject.GetComponent<Image>();
            _abilityIcon.raycastTarget = false;

            var cooldownObject = new GameObject("CooldownOverlay", typeof(RectTransform), typeof(Image));
            cooldownObject.transform.SetParent(root.transform, false);
            var cooldownRect = cooldownObject.GetComponent<RectTransform>();
            cooldownRect.anchorMin = iconRect.anchorMin;
            cooldownRect.anchorMax = iconRect.anchorMax;
            cooldownRect.pivot = iconRect.pivot;
            cooldownRect.sizeDelta = iconRect.sizeDelta;
            cooldownRect.anchoredPosition = iconRect.anchoredPosition;
            _cooldownOverlay = cooldownObject.GetComponent<Image>();
            _cooldownOverlay.color = new Color(0f, 0f, 0f, 0.72f);
            _cooldownOverlay.type = Image.Type.Filled;
            _cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            _cooldownOverlay.fillOrigin = 2;
            _cooldownOverlay.fillClockwise = false;
            _cooldownOverlay.raycastTarget = false;

            var font = parent.GetComponentInChildren<TMP_Text>(true)?.font;
            var iconCenterFromBottom = _cardSize.y - 10f - iconSize * 0.5f;
            _cooldownText = CreateText(root.transform, "CooldownText", string.Empty,
                new Vector2(0f, iconCenterFromBottom), new Vector2(126f, 46f), 32f, font);
            _abilityNameText = CreateText(root.transform, "AbilityName", string.Empty,
                new Vector2(0f, 21f), new Vector2(_cardSize.x - 16f, 42f), 25f, font);

            var keyBadge = new GameObject("AbilityKey", typeof(RectTransform), typeof(Image));
            keyBadge.transform.SetParent(root.transform, false);
            var keyRect = keyBadge.GetComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(1f, 1f);
            keyRect.anchorMax = new Vector2(1f, 1f);
            keyRect.pivot = new Vector2(1f, 1f);
            keyRect.sizeDelta = new Vector2(48f, 48f);
            keyRect.anchoredPosition = new Vector2(-4f, -4f);
            var keyImage = keyBadge.GetComponent<Image>();
            keyImage.color = new Color32(20, 48, 93, 255);
            keyImage.raycastTarget = false;
            CreateText(keyBadge.transform, "Label", "Q", new Vector2(0f, 24f),
                new Vector2(44f, 44f), 28f, font);
        }

        private void ConfigureCardRect(RectTransform rootRect)
        {
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.sizeDelta = _cardSize;
            rootRect.anchoredPosition = _cardPosition;
        }

        private void SetMatchLayoutActive(bool active)
        {
            if (_matchLayoutActive == active)
            {
                return;
            }

            for (var index = 0; index < _eventObjects.Length; index++)
            {
                if (_eventObjects[index] != null)
                {
                    RuntimeUiState.SetActive(
                        _eventObjects[index],
                        !active && _eventOriginalActive[index]);
                }
            }

            for (var index = 0; index < _lobbyOnlyRoots.Length; index++)
            {
                if (_lobbyOnlyRoots[index] != null)
                {
                    RuntimeUiState.SetActive(
                        _lobbyOnlyRoots[index],
                        !active && _lobbyOnlyOriginalActive[index]);
                }
            }

            if (active)
            {
                MoveControlsIntoMatchColumn();
            }
            else
            {
                RestoreControls();
            }

            _matchLayoutActive = active;
        }

        private void HideCard()
        {
            if (_canvasGroup == null)
            {
                return;
            }

            RuntimeUiState.SetCanvasGroup(_canvasGroup, 0f, false, false);
        }

        private void CloseOpenEventWindow()
        {
            if (_eventScreen != null)
            {
                _eventScreen.Hide();
            }
            else
            {
                RuntimeUiState.SetActive(_eventWindow, false);
            }

            RuntimeUiState.SetActive(_modalDimmer, false);
        }

        private void MoveControlsIntoMatchColumn()
        {
            if (_matchControlsRoot == null)
            {
                return;
            }

            RuntimeUiState.SetActive(_matchControlsRoot.gameObject, true);
            for (var index = 0; index < _controlRoots.Length; index++)
            {
                var control = _controlRoots[index];
                if (control == null)
                {
                    continue;
                }

                control.SetParent(_matchControlsRoot, false);
                control.anchorMin = new Vector2(0.5f, 0.5f);
                control.anchorMax = new Vector2(0.5f, 0.5f);
                control.pivot = new Vector2(0.5f, 0.5f);
                control.sizeDelta = _originalControlSizes[index];
                control.anchoredPosition = new Vector2(0f, 90f - index * 60f);
                control.localScale = Vector3.one;
                control.localRotation = Quaternion.identity;
            }
        }

        private void RestoreControls()
        {
            for (var index = 0; index < _controlRoots.Length; index++)
            {
                var control = _controlRoots[index];
                if (control == null || _originalControlParents[index] == null)
                {
                    continue;
                }

                control.SetParent(_originalControlParents[index], false);
                control.SetSiblingIndex(_originalControlSiblingIndexes[index]);
                control.anchorMin = _originalControlAnchorMin[index];
                control.anchorMax = _originalControlAnchorMax[index];
                control.pivot = _originalControlPivots[index];
                control.sizeDelta = _originalControlSizes[index];
                control.anchoredPosition = _originalControlPositions[index];
                control.localScale = _originalControlScales[index];
                control.localRotation = _originalControlRotations[index];
            }

            if (_matchControlsRoot != null)
            {
                RuntimeUiState.SetActive(_matchControlsRoot.gameObject, false);
            }
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

        private static TextMeshProUGUI CreateText(Transform parent, string name, string value,
            Vector2 anchoredPosition, Vector2 size, float fontSize, TMP_FontAsset font)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
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
            text.fontStyle = FontStyles.Bold;
            text.fontSize = fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = Mathf.Max(12f, fontSize - 8f);
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineColor = new Color32(10, 22, 51, 242);
            text.outlineWidth = 0.16f;
            text.raycastTarget = false;
            return text;
        }
    }
}
