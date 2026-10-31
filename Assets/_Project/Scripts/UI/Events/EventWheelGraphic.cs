using UnityEngine;
using UnityEngine.UI;

namespace PawArena.UI.Events
{
    /// <summary>Draws code-authored circular layers for a themed Event Wheel.</summary>
    [DisallowMultipleComponent]
    public class EventWheelGraphic : MaskableGraphic
    {
        private const int SectorCount = 6;
        private const int ArcSubdivisions = 10;
        private const int RingSegmentCount = 24;

        [SerializeField] private EventWheelGraphicLayer _layer = EventWheelGraphicLayer.Disc;
        [SerializeField] private Color _primaryColor = new(0.11f, 0.055f, 0.18f, 1f);
        [SerializeField] private Color _secondaryColor = new(0.16f, 0.08f, 0.25f, 1f);
        [SerializeField] private Color _accentColor = new(0.64f, 0.12f, 1f, 1f);
        [SerializeField] private Color _specialColor = new(1f, 0.62f, 0.04f, 1f);
        [SerializeField, Range(0, SectorCount - 1)] private int _specialSectorIndex = 1;

        /// <summary>Applies the visual role and palette for this graphic layer.</summary>
        public void Configure(
            EventWheelGraphicLayer layer,
            Color primaryColor,
            Color secondaryColor,
            Color accentColor,
            Color specialColor,
            int specialSectorIndex = 1)
        {
            _layer = layer;
            _primaryColor = primaryColor;
            _secondaryColor = secondaryColor;
            _accentColor = accentColor;
            _specialColor = specialColor;
            _specialSectorIndex = Mathf.Clamp(specialSectorIndex, 0, SectorCount - 1);
            raycastTarget = false;
            SetVerticesDirty();
        }

        /// <summary>Applies a reusable Wheel presentation without changing this layer's role.</summary>
        public void ApplyPalette(
            Color primaryColor,
            Color secondaryColor,
            Color accentColor,
            Color specialColor)
        {
            _primaryColor = primaryColor;
            _secondaryColor = secondaryColor;
            _accentColor = accentColor;
            _specialColor = specialColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var size = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height);
            if (size <= 0f)
            {
                return;
            }

            var radius = size * 0.5f;
            switch (_layer)
            {
                case EventWheelGraphicLayer.RuneRing:
                    DrawRuneRing(vertexHelper, radius);
                    break;
                case EventWheelGraphicLayer.Hub:
                    DrawHub(vertexHelper, radius);
                    break;
                case EventWheelGraphicLayer.Pointer:
                    DrawPointer(vertexHelper);
                    break;
                default:
                    DrawDisc(vertexHelper, radius);
                    break;
            }
        }

        private void DrawDisc(VertexHelper vertexHelper, float radius)
        {
            var discRadius = radius * 0.96f;
            for (var sectorIndex = 0; sectorIndex < SectorCount; sectorIndex++)
            {
                var sectorColor = sectorIndex == _specialSectorIndex
                    ? _specialColor
                    : sectorIndex % 2 == 0 ? _primaryColor : _secondaryColor;
                var centerAngle = 90f - sectorIndex * 60f;
                AddSector(
                    vertexHelper,
                    Vector2.zero,
                    discRadius,
                    centerAngle + 30f,
                    centerAngle - 30f,
                    sectorColor);
            }

            AddAnnulus(vertexHelper, discRadius, discRadius * 0.965f, new Color(0.025f, 0.012f, 0.05f, 1f), 72);
            for (var boundaryIndex = 0; boundaryIndex < SectorCount; boundaryIndex++)
            {
                var angle = 120f - boundaryIndex * 60f;
                AddRadialSeparator(vertexHelper, angle, discRadius * 0.18f, discRadius, 5f);
            }
        }

        private void DrawRuneRing(VertexHelper vertexHelper, float radius)
        {
            AddAnnulus(vertexHelper, radius, radius * 0.88f, new Color(_accentColor.r, _accentColor.g, _accentColor.b, 0.32f), 72);
            AddAnnulus(vertexHelper, radius * 0.965f, radius * 0.925f, _accentColor, 72);
            AddAnnulus(vertexHelper, radius * 0.90f, radius * 0.86f, new Color(0.05f, 0.01f, 0.09f, 1f), 72);

            for (var segmentIndex = 0; segmentIndex < RingSegmentCount; segmentIndex++)
            {
                var centerAngle = 90f - segmentIndex * (360f / RingSegmentCount);
                var halfWidth = segmentIndex % 2 == 0 ? 3.8f : 2.4f;
                var color = segmentIndex % 2 == 0 ? Color.white : _accentColor;
                color.a = segmentIndex % 2 == 0 ? 0.92f : 0.72f;
                AddRingSlice(
                    vertexHelper,
                    radius * 0.91f,
                    radius * 0.875f,
                    centerAngle + halfWidth,
                    centerAngle - halfWidth,
                    color);
            }
        }

        private void DrawHub(VertexHelper vertexHelper, float radius)
        {
            AddCircle(vertexHelper, radius, new Color(_accentColor.r, _accentColor.g, _accentColor.b, 0.22f), 48);
            AddCircle(vertexHelper, radius * 0.78f, new Color(0.22f, 0.03f, 0.42f, 1f), 48);
            AddAnnulus(vertexHelper, radius * 0.92f, radius * 0.76f, _accentColor, 48);
            AddCircle(vertexHelper, radius * 0.52f, new Color(0.78f, 0.48f, 1f, 1f), 48);
            AddCircle(vertexHelper, radius * 0.25f, Color.white, 32);
        }

        private void DrawPointer(VertexHelper vertexHelper)
        {
            var rect = rectTransform.rect;
            var top = rect.yMax;
            var bottom = rect.yMin;
            var halfWidth = rect.width * 0.46f;
            AddTriangle(
                vertexHelper,
                new Vector2(-halfWidth, top),
                new Vector2(halfWidth, top),
                new Vector2(0f, bottom),
                _accentColor);
            AddTriangle(
                vertexHelper,
                new Vector2(-halfWidth * 0.55f, top * 0.7f),
                new Vector2(halfWidth * 0.55f, top * 0.7f),
                new Vector2(0f, bottom * 0.62f),
                Color.white);
        }

        private static void AddSector(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float startAngle,
            float endAngle,
            Color color)
        {
            var centerIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center, color);
            for (var arcIndex = 0; arcIndex <= ArcSubdivisions; arcIndex++)
            {
                var progress = arcIndex / (float)ArcSubdivisions;
                var angle = Mathf.Lerp(startAngle, endAngle, progress) * Mathf.Deg2Rad;
                AddVertex(
                    vertexHelper,
                    center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    color);
                if (arcIndex > 0)
                {
                    vertexHelper.AddTriangle(centerIndex, centerIndex + arcIndex, centerIndex + arcIndex + 1);
                }
            }
        }

        private static void AddCircle(VertexHelper vertexHelper, float radius, Color color, int segments)
        {
            var centerIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, Vector2.zero, color);
            for (var index = 0; index <= segments; index++)
            {
                var angle = index * 360f / segments * Mathf.Deg2Rad;
                AddVertex(vertexHelper, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color);
                if (index > 0)
                {
                    vertexHelper.AddTriangle(centerIndex, centerIndex + index, centerIndex + index + 1);
                }
            }
        }

        private static void AddAnnulus(
            VertexHelper vertexHelper,
            float outerRadius,
            float innerRadius,
            Color color,
            int segments)
        {
            for (var index = 0; index < segments; index++)
            {
                var startAngle = index * 360f / segments;
                var endAngle = (index + 1) * 360f / segments;
                AddRingSlice(vertexHelper, outerRadius, innerRadius, startAngle, endAngle, color);
            }
        }

        private static void AddRingSlice(
            VertexHelper vertexHelper,
            float outerRadius,
            float innerRadius,
            float startAngle,
            float endAngle,
            Color color)
        {
            var startRadians = startAngle * Mathf.Deg2Rad;
            var endRadians = endAngle * Mathf.Deg2Rad;
            var startDirection = new Vector2(Mathf.Cos(startRadians), Mathf.Sin(startRadians));
            var endDirection = new Vector2(Mathf.Cos(endRadians), Mathf.Sin(endRadians));
            AddQuad(
                vertexHelper,
                startDirection * innerRadius,
                startDirection * outerRadius,
                endDirection * outerRadius,
                endDirection * innerRadius,
                color);
        }

        private static void AddRadialSeparator(
            VertexHelper vertexHelper,
            float angle,
            float innerRadius,
            float outerRadius,
            float width)
        {
            var radians = angle * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            var perpendicular = new Vector2(-direction.y, direction.x) * width * 0.5f;
            var color = new Color(0.015f, 0.008f, 0.03f, 1f);
            AddQuad(
                vertexHelper,
                direction * innerRadius - perpendicular,
                direction * outerRadius - perpendicular,
                direction * outerRadius + perpendicular,
                direction * innerRadius + perpendicular,
                color);
        }

        private static void AddTriangle(
            VertexHelper vertexHelper,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Color color)
        {
            var firstIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, first, color);
            AddVertex(vertexHelper, second, color);
            AddVertex(vertexHelper, third, color);
            vertexHelper.AddTriangle(firstIndex, firstIndex + 1, firstIndex + 2);
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth,
            Color color)
        {
            var firstIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, first, color);
            AddVertex(vertexHelper, second, color);
            AddVertex(vertexHelper, third, color);
            AddVertex(vertexHelper, fourth, color);
            vertexHelper.AddTriangle(firstIndex, firstIndex + 1, firstIndex + 2);
            vertexHelper.AddTriangle(firstIndex, firstIndex + 2, firstIndex + 3);
        }

        private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color color)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = color;
            vertexHelper.AddVert(vertex);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            raycastTarget = false;
            SetVerticesDirty();
        }
#endif
    }

    /// <summary>Identifies one code-authored layer of an Event Wheel.</summary>
    public enum EventWheelGraphicLayer
    {
        Disc,
        RuneRing,
        Hub,
        Pointer
    }
}


