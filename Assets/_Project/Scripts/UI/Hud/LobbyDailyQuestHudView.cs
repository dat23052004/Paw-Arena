using System;
using PawArena.Core.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawArena.UI.Hud
{
    /// <summary>Renders all lobby daily quests and forwards button intents to a presenter.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class LobbyDailyQuestHudView : MonoBehaviour
    {
        private const int QuestCount = 3;
        private const string InviteButtonName = "FriendQuestInviteButton";
        private const float InviteButtonWidth = 90f;
        private const float InviteButtonHeight = 40f;
        private const float InviteButtonGap = 10f;

        private static readonly Color IncompleteColor = new(0.08f, 0.29f, 0.49f, 0.96f);
        private static readonly Color ClaimableColor = new(0.92f, 0.58f, 0.07f, 0.98f);
        [SerializeField] private Canvas _parentCanvas = null;
        [SerializeField] private GameObject _questRoot = null;
        [SerializeField] private TMP_Text _resetText = null;
        [SerializeField] private Button _inviteButton = null;

        private readonly GameObject[] _questRows = new GameObject[QuestCount];
        private readonly Button[] _questButtons = new Button[QuestCount];
        private readonly Image[] _questBackgrounds = new Image[QuestCount];
        private readonly TMP_Text[] _questTexts = new TMP_Text[QuestCount];
        private readonly Vector2[] _questSlotPositions = new Vector2[QuestCount];
        private readonly bool[] _questClaimed = new bool[QuestCount];
        private DateTimeOffset _resetAtUtc;
        private int _lastDisplayedMinute = -1;
        private bool _hasResetTimestamp;
        private bool _initialized;
        private bool _presentationVisible = true;
        private bool _inviteAvailable = true;

        /// <summary>Raised when the player clicks one of the three quest rows.</summary>
        public event Action<DailyQuestId> QuestRequested;

        /// <summary>Raised when the player asks the platform integration to invite friends.</summary>
        public event Action InviteRequested;

        private void Start() => Initialize();

        private void OnEnable() => BindListeners();

        private void OnDisable() => UnbindListeners();

        private void Update()
        {
            if (!_hasResetTimestamp || _resetText == null)
            {
                return;
            }

            var remaining = _resetAtUtc - DateTimeOffset.UtcNow;
            var totalMinutes = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalMinutes));
            if (totalMinutes == _lastDisplayedMinute)
            {
                return;
            }

            _lastDisplayedMinute = totalMinutes;
            _resetText.text = totalMinutes > 0
                ? $"Resets in {totalMinutes / 60}h {totalMinutes % 60}m"
                : "Resetting...";
        }

        /// <summary>Initializes an authored or complete runtime quest panel exactly once.</summary>
        public bool Initialize()
        {
            if (_initialized)
            {
                return true;
            }

            _parentCanvas ??= FindRootCanvas();
            if (_parentCanvas == null)
            {
                return false;
            }

            var runtimeRoot = FindDescendant(_parentCanvas.transform, "DailyQuestHud");
            if (runtimeRoot != null && BindQuestPanel(runtimeRoot))
            {
                _questRoot = runtimeRoot.gameObject;
            }
            else if (!TryBindAuthoredPanel())
            {
                DisableLegacyPanels();
                BuildFullQuestPanel();
            }

            if (_questRoot == null || _resetText == null || _inviteButton == null)
            {
                return false;
            }

            DisableObsoleteFriendOverlay();
            _presentationVisible = _questRoot.activeSelf;
            _initialized = true;
            BindListeners();
            RefreshPresentationVisibility();
            return true;
        }

        /// <summary>Renders one quest snapshot without owning quest progression logic.</summary>
        public void SetQuestProgress(DailyQuestProgress progress)
        {
            if (!Initialize())
            {
                return;
            }

            var index = (int)progress.QuestId;
            if (index < 0 || index >= QuestCount)
            {
                return;
            }

            var title = GetQuestTitle(progress.QuestId);
            _questClaimed[index] = progress.IsClaimed;
            if (progress.IsClaimed)
            {
                _questButtons[index].interactable = false;
            }
            else if (progress.IsCompleted)
            {
                _questTexts[index].text = $"{title}    CLAIM +{progress.CoinReward}";
                _questBackgrounds[index].color = ClaimableColor;
                _questButtons[index].interactable = true;
            }
            else
            {
                _questTexts[index].text =
                    $"{title}    {progress.Progress}/{progress.Target}    +{progress.CoinReward}";
                _questBackgrounds[index].color = IncompleteColor;
                _questButtons[index].interactable = true;
            }

            RefreshQuestRows();

            if (progress.QuestId == DailyQuestId.PlayWithFriend && _inviteButton != null)
            {
                _inviteAvailable = !progress.IsClaimed;
                RefreshPresentationVisibility();
            }
        }

        /// <summary>Sets the server/service-owned UTC reset timestamp.</summary>
        public void SetResetAtUtc(DateTimeOffset resetAtUtc)
        {
            if (!Initialize())
            {
                return;
            }

            _resetAtUtc = resetAtUtc;
            _hasResetTimestamp = true;
            _lastDisplayedMinute = -1;
        }

        /// <summary>Shows or hides the complete Daily Quest panel.</summary>
        public void SetVisible(bool visible)
        {
            if (!Initialize())
            {
                return;
            }

            _presentationVisible = visible;
            RefreshPresentationVisibility();
        }

        private bool TryBindAuthoredPanel()
        {
            var authoredRoot = FindDescendant(_parentCanvas.transform, "HUD_Quests");
            if (authoredRoot == null)
            {
                var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
                for (var index = 0; index < canvases.Length; index++)
                {
                    authoredRoot = FindDescendant(canvases[index].transform, "HUD_Quests");
                    if (authoredRoot == null)
                    {
                        continue;
                    }

                    _parentCanvas = canvases[index].rootCanvas;
                    break;
                }
            }

            if (authoredRoot == null || !BindQuestPanel(authoredRoot))
            {
                return false;
            }

            _questRoot = authoredRoot.gameObject;
            return true;
        }

        private bool BindQuestPanel(Transform root)
        {
            for (var index = 0; index < QuestCount; index++)
            {
                var row = root.Find($"QuestRow_{index}");
                var text = row?.Find($"QuestText_{index}")?.GetComponent<TMP_Text>();
                var background = row?.GetComponent<Image>();
                if (row == null || text == null || background == null)
                {
                    return false;
                }

                _questRows[index] = row.gameObject;
                _questTexts[index] = text;
                _questBackgrounds[index] = background;
                _questButtons[index] = row.GetComponent<Button>() ?? row.gameObject.AddComponent<Button>();
                _questSlotPositions[index] = ((RectTransform)row).anchoredPosition;
                ConfigureQuestButton(_questButtons[index], background);
            }

            _resetText = root.Find("QuestTimer")?.GetComponent<TMP_Text>();
            if (_resetText == null)
            {
                return false;
            }

            _inviteButton = ResolveInviteButton(root);
            if (_inviteButton == null)
            {
                _inviteButton = BuildInviteButton(root.parent, _questTexts[2].font);
            }

            ConfigureFriendPresentation(root);
            return _inviteButton != null;
        }

        private void BuildFullQuestPanel()
        {
            var root = new GameObject("DailyQuestHud", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(_parentCanvas.transform, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0.7f);
            rootRect.anchorMax = new Vector2(1f, 0.7f);
            rootRect.pivot = new Vector2(1f, 0.5f);
            rootRect.sizeDelta = new Vector2(420f, 242f);
            rootRect.anchoredPosition = new Vector2(-32f, 0f);
            var rootImage = root.GetComponent<Image>();
            rootImage.color = new Color32(13, 42, 78, 235);
            rootImage.raycastTarget = false;
            _questRoot = root;

            var font = _parentCanvas.GetComponentInChildren<TMP_Text>(true)?.font;
            CreateText(root.transform, "QuestHeader", "Daily Quests", new Vector2(0f, 96f),
                new Vector2(380f, 40f), 27f, font);

            for (var index = 0; index < QuestCount; index++)
            {
                var row = new GameObject(
                    $"QuestRow_{index}",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                row.transform.SetParent(root.transform, false);
                var rowRect = row.GetComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.5f, 0.5f);
                rowRect.anchorMax = new Vector2(0.5f, 0.5f);
                rowRect.sizeDelta = new Vector2(382f, 48f);
                rowRect.anchoredPosition = new Vector2(0f, 48f - index * 53f);
                _questSlotPositions[index] = rowRect.anchoredPosition;
                _questRows[index] = row;
                _questBackgrounds[index] = row.GetComponent<Image>();
                _questButtons[index] = row.GetComponent<Button>();
                ConfigureQuestButton(_questButtons[index], _questBackgrounds[index]);
                _questTexts[index] = CreateText(
                    row.transform,
                    $"QuestText_{index}",
                    string.Empty,
                    Vector2.zero,
                    new Vector2(356f, 40f),
                    20f,
                    font);
            }

            _resetText = CreateText(root.transform, "QuestTimer", string.Empty,
                new Vector2(0f, -105f), new Vector2(340f, 30f), 18f, font);
            _resetText.color = new Color32(255, 142, 174, 255);
            _inviteButton = BuildInviteButton(root.transform.parent, font);
            ConfigureFriendPresentation(root.transform);
        }

        private void RefreshQuestRows()
        {
            var visibleSlot = 0;
            for (var questIndex = 0; questIndex < QuestCount; questIndex++)
            {
                var row = _questRows[questIndex];
                if (row == null)
                {
                    continue;
                }

                var isVisible = !_questClaimed[questIndex];
                row.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                ((RectTransform)row.transform).anchoredPosition = _questSlotPositions[visibleSlot];
                visibleSlot++;
            }

            if (_questRoot != null)
            {
                ConfigureFriendPresentation(_questRoot.transform);
            }
        }

        private void ConfigureFriendPresentation(Transform questRoot)
        {
            if (_questTexts[2] == null || _inviteButton == null)
            {
                return;
            }

            var textRect = _questTexts[2].rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            var friendRowRect = (RectTransform)_questRows[2].transform;
            textRect.sizeDelta = new Vector2(Mathf.Max(80f, friendRowRect.rect.width - 20f), 40f);
            textRect.anchoredPosition = Vector2.zero;

            PositionInviteOutsidePanel((RectTransform)questRoot, friendRowRect);
        }

        private void PositionInviteOutsidePanel(RectTransform questRoot, RectTransform friendRow)
        {
            if (questRoot.parent is not RectTransform presentationParent)
            {
                return;
            }

            var rootLeftWorld = questRoot.TransformPoint(new Vector3(questRoot.rect.xMin, 0f, 0f));
            var friendCenterWorld = friendRow.TransformPoint(friendRow.rect.center);

            var inviteRect = (RectTransform)_inviteButton.transform;
            inviteRect.SetParent(presentationParent, false);
            inviteRect.name = InviteButtonName;
            inviteRect.anchorMin = new Vector2(0.5f, 0.5f);
            inviteRect.anchorMax = new Vector2(0.5f, 0.5f);
            inviteRect.pivot = new Vector2(0.5f, 0.5f);
            inviteRect.sizeDelta = new Vector2(InviteButtonWidth, InviteButtonHeight);

            var rootLeftLocal = presentationParent.InverseTransformPoint(rootLeftWorld);
            var friendCenterLocal = presentationParent.InverseTransformPoint(friendCenterWorld);
            inviteRect.localPosition = new Vector3(
                rootLeftLocal.x - InviteButtonGap - InviteButtonWidth * 0.5f,
                friendCenterLocal.y,
                0f);
            inviteRect.SetAsLastSibling();
        }

        private static Button ResolveInviteButton(Transform questRoot)
        {
            var external = questRoot.parent?.Find(InviteButtonName)?.GetComponent<Button>();
            var nested = FindDescendant(questRoot, InviteButtonName)?.GetComponent<Button>();
            if (external != null)
            {
                if (nested != null && nested != external)
                {
                    nested.gameObject.SetActive(false);
                }

                return external;
            }

            return nested;
        }

        private void RefreshPresentationVisibility()
        {
            if (_questRoot != null)
            {
                _questRoot.SetActive(_presentationVisible);
            }

            if (_inviteButton != null)
            {
                _inviteButton.gameObject.SetActive(_presentationVisible && _inviteAvailable);
            }
        }

        private void DisableLegacyPanels()
        {
            var transforms = _parentCanvas.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == "HUD_Quests" ||
                    transforms[index].name == "DailyFriendQuestHud")
                {
                    transforms[index].gameObject.SetActive(false);
                }
            }
        }

        private void DisableObsoleteFriendOverlay()
        {
            var obsolete = FindDescendant(_parentCanvas.transform, "DailyFriendQuestHud");
            obsolete?.gameObject.SetActive(false);
        }

        private void BindListeners()
        {
            if (!_initialized)
            {
                return;
            }

            UnbindListeners();
            _questButtons[0].onClick.AddListener(HandlePlayMatchesClicked);
            _questButtons[1].onClick.AddListener(HandleWinMatchesClicked);
            _questButtons[2].onClick.AddListener(HandleFriendMatchesClicked);
            _inviteButton.onClick.AddListener(HandleInviteClicked);
        }

        private void UnbindListeners()
        {
            _questButtons[0]?.onClick.RemoveListener(HandlePlayMatchesClicked);
            _questButtons[1]?.onClick.RemoveListener(HandleWinMatchesClicked);
            _questButtons[2]?.onClick.RemoveListener(HandleFriendMatchesClicked);
            _inviteButton?.onClick.RemoveListener(HandleInviteClicked);
        }

        private void HandlePlayMatchesClicked() => QuestRequested?.Invoke(DailyQuestId.PlayMatches);

        private void HandleWinMatchesClicked() => QuestRequested?.Invoke(DailyQuestId.WinMatches);

        private void HandleFriendMatchesClicked() => QuestRequested?.Invoke(DailyQuestId.PlayWithFriend);

        private void HandleInviteClicked() => InviteRequested?.Invoke();

        private static string GetQuestTitle(DailyQuestId questId)
        {
            return questId switch
            {
                DailyQuestId.PlayMatches => "Play 5 matches",
                DailyQuestId.WinMatches => "Win 1 match",
                DailyQuestId.PlayWithFriend => "Play with friends",
                _ => throw new ArgumentOutOfRangeException(nameof(questId))
            };
        }

        private static Button BuildInviteButton(Transform parent, TMP_FontAsset font)
        {
            var buttonObject = new GameObject(
                InviteButtonName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color32(62, 190, 78, 255);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.None
            };
            CreateText(buttonObject.transform, "Label", "Invite", Vector2.zero,
                new Vector2(82f, 36f), 20f, font);
            return button;
        }

        private static void ConfigureQuestButton(Button button, Image targetGraphic)
        {
            button.targetGraphic = targetGraphic;
            button.navigation = new UnityEngine.UI.Navigation
            {
                mode = UnityEngine.UI.Navigation.Mode.None
            };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.86f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string objectName,
            string value,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            TMP_FontAsset font)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
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
            text.fontSizeMin = Mathf.Max(11f, fontSize - 8f);
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineColor = new Color32(10, 22, 51, 242);
            text.outlineWidth = 0.14f;
            text.raycastTarget = false;
            return text;
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

        private static Transform FindDescendant(Transform root, string objectName)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == objectName)
                {
                    return transforms[index];
                }
            }

            return null;
        }
    }
}
