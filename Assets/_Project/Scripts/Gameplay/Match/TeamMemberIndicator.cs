using UnityEngine;
using UnityEngine.UI;

namespace PawArena.Gameplay.Match
{
    /// <summary>Displays a non-interactive world-space marker above one local teammate.</summary>
    [DisallowMultipleComponent]
    public sealed class TeamMemberIndicator : MonoBehaviour
    {
        private const string VisualRootName = "TeammateIndicator";
        private const string LabelText = "TEAM";

        private static readonly Color32 MarkerColor = new(45, 220, 105, 238);
        private static readonly Color32 ShadowColor = new(7, 45, 25, 220);

        [Header("World Marker")]
        [SerializeField] private Vector3 _localOffset = new(0f, 2.15f, 0f);
        [SerializeField] private Vector2 _size = new(172f, 44f);
        [SerializeField, Min(0.001f)] private float _worldScale = 0.006f;

        private RectTransform _visualRoot;
        private Camera _camera;
        private bool _initialized;

        /// <summary>Gets whether the teammate marker is currently visible.</summary>
        public bool IsVisible => _visualRoot != null && _visualRoot.gameObject.activeSelf;

        private void Awake()
        {
            _camera = Camera.main;
            Initialize();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!IsVisible || _camera == null)
            {
                return;
            }

            _visualRoot.rotation = _camera.transform.rotation;
        }

        private void OnValidate()
        {
            _size.x = Mathf.Max(100f, _size.x);
            _size.y = Mathf.Max(28f, _size.y);
            _worldScale = Mathf.Max(0.001f, _worldScale);
        }

        /// <summary>Creates or binds the world-space presentation.</summary>
        public bool Initialize()
        {
            if (_initialized)
            {
                return true;
            }

            _visualRoot = transform.Find(VisualRootName) as RectTransform;
            if (_visualRoot == null)
            {
                BuildView();
            }

            if (_visualRoot == null)
            {
                return false;
            }

            ConfigureRoot();
            _initialized = true;
            return true;
        }

        /// <summary>Shows or hides the marker without affecting the actor.</summary>
        public void SetVisible(bool visible)
        {
            if (!Initialize())
            {
                return;
            }

            _visualRoot.gameObject.SetActive(visible);
        }

        private void BuildView()
        {
            var root = new GameObject(
                VisualRootName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            _visualRoot = root.GetComponent<RectTransform>();

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 200;

            var group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var background = CreateImage(root.transform, "Background", MarkerColor);
            Stretch(background.rectTransform, Vector2.zero, Vector2.zero);

            var shadow = background.gameObject.AddComponent<Outline>();
            shadow.effectColor = ShadowColor;
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.useGraphicAlpha = true;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(root.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            Stretch(labelRect, new Vector2(8f, 3f), new Vector2(-8f, -3f));
            var label = labelObject.GetComponent<Text>();
            label.text = LabelText;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = FontStyle.Bold;
            label.fontSize = 28;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = 28;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            var pointer = CreateImage(root.transform, "Pointer", MarkerColor);
            var pointerRect = pointer.rectTransform;
            pointerRect.anchorMin = new Vector2(0.5f, 0f);
            pointerRect.anchorMax = new Vector2(0.5f, 0f);
            pointerRect.pivot = new Vector2(0.5f, 0.5f);
            pointerRect.sizeDelta = new Vector2(18f, 18f);
            pointerRect.anchoredPosition = new Vector2(0f, -9f);
            pointerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private void ConfigureRoot()
        {
            _visualRoot.sizeDelta = _size;
            _visualRoot.localPosition = _localOffset;
            _visualRoot.localScale = Vector3.one * _worldScale;
            _visualRoot.localRotation = Quaternion.identity;
        }

        private static Image CreateImage(Transform parent, string objectName, Color color)
        {
            var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
