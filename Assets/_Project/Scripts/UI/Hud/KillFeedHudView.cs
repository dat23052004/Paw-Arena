using PawArena.UI.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawArena.UI.Hud
{
    /// <summary>Displays recent server-confirmed ring-outs in the top-right gameplay HUD.</summary>
    [DisallowMultipleComponent]
    public sealed class KillFeedHudView : MonoBehaviour
    {
        private const int MaxEntries = 5;
        private const float EntryHeight = 38f;
        private const float EntryGap = 6f;

        [SerializeField] private Canvas _parentCanvas = null;
        [SerializeField] private RectTransform _root = null;

        [Header("Layout")]
        [SerializeField] private Vector2 _anchoredPosition = new(-24f, -24f);
        [SerializeField] private Vector2 _size = new(430f, 220f);
        [SerializeField, Min(1f)] private float _entryLifetimeSeconds = 6f;

        private readonly CanvasGroup[] _entryGroups = new CanvasGroup[MaxEntries];
        private readonly TMP_Text[] _entryTexts = new TMP_Text[MaxEntries];
        private readonly float[] _expiresAt = new float[MaxEntries];
        private bool _initialized;
        private bool _presentationVisible;

        /// <summary>Gets whether the feed root is currently shown.</summary>
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        private void Awake()
        {
            if (!Initialize())
            {
                Debug.LogWarning("Kill feed HUD could not find a root canvas.", this);
                enabled = false;
                return;
            }

            ClearAndHide();
        }

        private void Update()
        {
            if (!_presentationVisible)
            {
                return;
            }

            var now = Time.unscaledTime;
            for (var index = 0; index < MaxEntries; index++)
            {
                if (_entryGroups[index] == null || _entryGroups[index].alpha <= 0f ||
                    now < _expiresAt[index])
                {
                    continue;
                }

                SetEntryVisible(index, false);
            }
        }

        private void OnValidate()
        {
            _size.x = Mathf.Max(240f, _size.x);
            _size.y = Mathf.Max(MaxEntries * (EntryHeight + EntryGap), _size.y);
            _entryLifetimeSeconds = Mathf.Max(1f, _entryLifetimeSeconds);
        }

        /// <summary>Initializes an authored feed or builds the complete runtime presentation.</summary>
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

            _root ??= _parentCanvas.transform.Find("KillFeedHud") as RectTransform;
            if (_root == null)
            {
                BuildView();
            }
            else if (!BindEntries())
            {
                return false;
            }

            ConfigureRoot();
            RuntimeUiState.EnsureNonInteractiveNestedCanvas(_root.gameObject);
            _initialized = true;
            return true;
        }

        /// <summary>Adds one authoritative elimination with the newest entry at the top.</summary>
        public void ShowElimination(string attackerName, string eliminatedName, bool hasAttacker)
        {
            if (!Initialize())
            {
                return;
            }

            SetVisible(true);
            for (var index = MaxEntries - 1; index > 0; index--)
            {
                RuntimeUiState.SetText(_entryTexts[index], _entryTexts[index - 1].text);
                _expiresAt[index] = _expiresAt[index - 1];
                SetEntryVisible(index, _entryGroups[index - 1].alpha > 0f);
            }

            var safeEliminatedName = string.IsNullOrWhiteSpace(eliminatedName)
                ? "Unknown"
                : eliminatedName;
            RuntimeUiState.SetText(
                _entryTexts[0],
                hasAttacker && !string.IsNullOrWhiteSpace(attackerName)
                    ? $"{attackerName}  hạ  {safeEliminatedName}"
                    : $"{safeEliminatedName}  rơi khỏi sàn");
            _expiresAt[0] = Time.unscaledTime + _entryLifetimeSeconds;
            SetEntryVisible(0, true);
        }

        /// <summary>Shows or hides the whole feed without changing its entries.</summary>
        public void SetVisible(bool visible)
        {
            if (!Initialize())
            {
                return;
            }

            _presentationVisible = visible;
            RuntimeUiState.SetActive(_root.gameObject, visible);
        }

        /// <summary>Clears all entries and hides the feed.</summary>
        public void ClearAndHide()
        {
            if (!Initialize())
            {
                return;
            }

            for (var index = 0; index < MaxEntries; index++)
            {
                RuntimeUiState.SetText(_entryTexts[index], string.Empty);
                _expiresAt[index] = 0f;
                SetEntryVisible(index, false);
            }

            _presentationVisible = false;
            RuntimeUiState.SetActive(_root.gameObject, false);
        }

        private void BuildView()
        {
            var rootObject = new GameObject("KillFeedHud", typeof(RectTransform));
            rootObject.transform.SetParent(_parentCanvas.transform, false);
            _root = rootObject.GetComponent<RectTransform>();
            var font = _parentCanvas.GetComponentInChildren<TMP_Text>(true)?.font;

            for (var index = 0; index < MaxEntries; index++)
            {
                var row = new GameObject(
                    $"KillFeedEntry_{index}",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(Image));
                row.transform.SetParent(_root, false);
                var rowRect = row.GetComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.sizeDelta = new Vector2(0f, EntryHeight);
                rowRect.anchoredPosition = new Vector2(0f, -index * (EntryHeight + EntryGap));

                var background = row.GetComponent<Image>();
                background.color = new Color32(17, 43, 76, 225);
                background.raycastTarget = false;
                _entryGroups[index] = row.GetComponent<CanvasGroup>();

                var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(row.transform, false);
                var textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(12f, 3f);
                textRect.offsetMax = new Vector2(-12f, -3f);
                var text = textObject.GetComponent<TextMeshProUGUI>();
                if (font != null)
                {
                    text.font = font;
                }

                text.fontSize = 19f;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.MidlineRight;
                text.color = Color.white;
                text.richText = false;
                text.enableAutoSizing = true;
                text.fontSizeMin = 13f;
                text.fontSizeMax = 19f;
                text.raycastTarget = false;
                _entryTexts[index] = text;
                SetEntryVisible(index, false);
            }
        }

        private bool BindEntries()
        {
            for (var index = 0; index < MaxEntries; index++)
            {
                var row = _root.Find($"KillFeedEntry_{index}");
                _entryGroups[index] = row?.GetComponent<CanvasGroup>();
                _entryTexts[index] = row?.Find("Label")?.GetComponent<TMP_Text>();
                if (_entryGroups[index] == null || _entryTexts[index] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private void ConfigureRoot()
        {
            _root.anchorMin = Vector2.one;
            _root.anchorMax = Vector2.one;
            _root.pivot = Vector2.one;
            _root.sizeDelta = _size;
            _root.anchoredPosition = _anchoredPosition;
            if (_root.GetSiblingIndex() != _root.parent.childCount - 1)
            {
                _root.SetAsLastSibling();
            }
        }

        private void SetEntryVisible(int index, bool visible)
        {
            RuntimeUiState.SetCanvasGroup(
                _entryGroups[index],
                visible ? 1f : 0f,
                false,
                false);
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
    }
}
