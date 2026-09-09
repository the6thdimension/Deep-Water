using UnityEngine;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Draws a screen-space bounding box around <see cref="TargetSelector.CurrentTarget"/>.
    /// Reads world-space AABB from any Renderer on the target (or its children), projects
    /// the 8 corners to screen, computes the 2D screen-space min/max box, draws four colored
    /// rectangles forming the outline.
    ///
    /// **Visual style:** corner brackets (4 L-shapes at the corners) for a tactical / HUD
    /// feel, rather than a solid outline. Configurable via <see cref="drawStyle"/>.
    ///
    /// **Behind-camera handling:** if the target is behind the camera plane,
    /// `Camera.WorldToScreenPoint` returns z<0. We bail out and skip the draw — drawing a
    /// rect at the corner of the screen for an off-screen target would be visually wrong.
    /// </summary>
    public sealed class TargetBoundingBoxRenderer : MonoBehaviour
    {
        public enum DrawStyle
        {
            /// <summary>Solid rectangle outline.</summary>
            Outline = 0,

            /// <summary>Four L-shaped corner brackets — tactical HUD look.</summary>
            Brackets = 1,
        }

        [Header("Source")]
        [SerializeField] private TargetSelector selector;
        [SerializeField] private Camera viewCamera;

        [Header("Style")]
        [SerializeField] private DrawStyle drawStyle = DrawStyle.Brackets;
        [SerializeField] private Color boxColor = new Color(1f, 0.15f, 0.15f, 0.95f);
        [SerializeField] private float lineThickness = 2f;
        [Tooltip("Length of each corner bracket arm, in pixels (Brackets style only).")]
        [SerializeField] private float bracketLengthPx = 18f;

        [Header("Labels")]
        [Tooltip("Show range to target as a small label below the box.")]
        [SerializeField] private bool showRangeLabel = true;

        // -- Internals ----------------------------------------------------
        private Texture2D _whiteTexture;
        private GUIStyle _labelStyle;

        private void Awake()
        {
            if (selector == null)   selector = FindFirstObjectByType<TargetSelector>();
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void OnGUI()
        {
            if (selector == null || viewCamera == null) return;
            Transform target = selector.CurrentTarget;
            if (target == null) return;

            // Get the target's world-space AABB. Prefer the bounds of the first Renderer
            // (which encompasses the visible mesh); fall back to a small box around the
            // transform if there's no Renderer.
            Bounds worldBounds;
            if (TryGetWorldBounds(target, out worldBounds) == false)
                return;

            // Project 8 corners to screen; compute 2D min/max.
            Vector2 sMin, sMax;
            bool anyInFront;
            if (!Project(worldBounds, viewCamera, out sMin, out sMax, out anyInFront))
                return;
            if (!anyInFront) return;

            // GUI coordinates: top-left origin. Unity's WorldToScreenPoint uses bottom-left.
            // Flip Y for GUI.
            float guiTop    = Screen.height - sMax.y;
            float guiBottom = Screen.height - sMin.y;
            float guiLeft   = sMin.x;
            float guiRight  = sMax.x;

            EnsureResources();

            switch (drawStyle)
            {
                case DrawStyle.Outline:
                    DrawOutline(guiLeft, guiTop, guiRight, guiBottom);
                    break;
                case DrawStyle.Brackets:
                    DrawBrackets(guiLeft, guiTop, guiRight, guiBottom);
                    break;
            }

            if (showRangeLabel)
            {
                float range = Vector3.Distance(viewCamera.transform.position, target.position);
                string label = range >= 1000f ? $"{range / 1000f:0.0} km" : $"{range:0} m";
                var labelRect = new Rect(guiLeft, guiBottom + 4f, guiRight - guiLeft, 18f);
                GUI.Label(labelRect, $"<color=#FF4444>{label}  •  {target.name}</color>", _labelStyle);
            }
        }

        // ============================================================
        // Drawing helpers
        // ============================================================

        private void DrawOutline(float left, float top, float right, float bottom)
        {
            // Four edges of the rectangle.
            DrawLine(left,  top,    right - left, lineThickness);          // top
            DrawLine(left,  bottom - lineThickness, right - left, lineThickness); // bottom
            DrawLine(left,  top,    lineThickness, bottom - top);          // left
            DrawLine(right - lineThickness, top, lineThickness, bottom - top); // right
        }

        private void DrawBrackets(float left, float top, float right, float bottom)
        {
            float bw = Mathf.Min(bracketLengthPx, (right - left) * 0.4f);
            float bh = Mathf.Min(bracketLengthPx, (bottom - top) * 0.4f);

            // Top-left corner
            DrawLine(left, top, bw, lineThickness);
            DrawLine(left, top, lineThickness, bh);
            // Top-right corner
            DrawLine(right - bw, top, bw, lineThickness);
            DrawLine(right - lineThickness, top, lineThickness, bh);
            // Bottom-left corner
            DrawLine(left, bottom - lineThickness, bw, lineThickness);
            DrawLine(left, bottom - bh, lineThickness, bh);
            // Bottom-right corner
            DrawLine(right - bw, bottom - lineThickness, bw, lineThickness);
            DrawLine(right - lineThickness, bottom - bh, lineThickness, bh);
        }

        private void DrawLine(float x, float y, float w, float h)
        {
            var orig = GUI.color;
            GUI.color = boxColor;
            GUI.DrawTexture(new Rect(x, y, w, h), _whiteTexture, ScaleMode.StretchToFill);
            GUI.color = orig;
        }

        private void EnsureResources()
        {
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(1, 1);
                _whiteTexture.SetPixel(0, 0, Color.white);
                _whiteTexture.Apply();
                _whiteTexture.hideFlags = HideFlags.HideAndDontSave;
            }
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12, fontStyle = FontStyle.Bold, richText = true,
                    alignment = TextAnchor.MiddleCenter,
                };
            }
        }

        // ============================================================
        // Geometry helpers
        // ============================================================

        private static bool TryGetWorldBounds(Transform target, out Bounds worldBounds)
        {
            // Prefer renderer bounds — they reflect the visible mesh, which is what the
            // player expects the bounding box to wrap.
            var renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                worldBounds = renderer.bounds;
                return true;
            }

            var collider = target.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                worldBounds = collider.bounds;
                return true;
            }

            // Final fallback: a small box around the transform.
            worldBounds = new Bounds(target.position, Vector3.one * 2f);
            return true;
        }

        /// <summary>
        /// Project an AABB to a 2D screen-space rectangle (min, max) and report whether any
        /// of the eight corners are in front of the camera. If all are behind, we skip the
        /// draw entirely.
        /// </summary>
        private static bool Project(Bounds b, Camera cam, out Vector2 sMin, out Vector2 sMax, out bool anyInFront)
        {
            anyInFront = false;
            sMin = new Vector2(float.MaxValue, float.MaxValue);
            sMax = new Vector2(float.MinValue, float.MinValue);

            // Eight corners of the AABB.
            Vector3 c = b.center, e = b.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z));
                Vector3 screen = cam.WorldToScreenPoint(corner);
                if (screen.z > 0f) anyInFront = true;
                // If the corner is BEHIND the camera, WorldToScreenPoint can return mirrored
                // coordinates. For a robust on-screen rect when the target straddles the
                // camera plane we'd need full frustum clipping; for "target is in front of
                // camera" we just include the in-front corners.
                if (screen.z <= 0f) continue;
                if (screen.x < sMin.x) sMin.x = screen.x;
                if (screen.y < sMin.y) sMin.y = screen.y;
                if (screen.x > sMax.x) sMax.x = screen.x;
                if (screen.y > sMax.y) sMax.y = screen.y;
            }

            // Clamp to screen edges so the box draws even when the target partially exits
            // the viewport.
            sMin.x = Mathf.Max(sMin.x, 0f);
            sMin.y = Mathf.Max(sMin.y, 0f);
            sMax.x = Mathf.Min(sMax.x, Screen.width);
            sMax.y = Mathf.Min(sMax.y, Screen.height);

            return sMax.x > sMin.x && sMax.y > sMin.y;
        }
    }
}
