using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DeepWater.GeneralQuarters.Core;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// The CIC's track picture: a north-up plan centred on the own ship, drawn as vectors (Painter2D) so it is sharp at
    /// any size. Range rings with their ranges written on; dashed rings for the ESSM and CIWS reach; NTDS-style symbols
    /// (air: the top half of the shape, surface: the whole shape; hostile diamond in red, unknown square in yellow,
    /// friend circle in blue; a dot marks a missile); a velocity leader on every track (30 s of travel); the ship's own
    /// missiles as small blue dots joined to their targets. Click a track to select it; the wheel zooms.
    /// It shows what the CIC tracks, not the truth.
    /// </summary>
    public sealed class TrackScope : VisualElement
    {
        public struct Blip
        {
            public int Id;
            public TargetKind Kind;
            public Identity Identity;
            /// <summary>Position relative to the own ship, metres (x east, y north).</summary>
            public Vector2 Offset;
            /// <summary>Velocity, m/s (x east, y north).</summary>
            public Vector2 Velocity;
            public bool Selected, Proposed;
        }

        public struct MissileBlip
        {
            public Vector2 Offset, Velocity, TargetOffset;
            public bool HasTarget;
        }

        public static readonly float[] Scales = { 5_000f, 10_000f, 20_000f, 40_000f, 80_000f, 160_000f };
        const float LeaderS = 30f, MissileLeaderS = 4f, OwnLeaderS = 60f;
        static readonly Color Ring = new Color(0.43f, 0.67f, 0.78f, 0.35f);
        static readonly Color Hostile = new Color(1f, 0.32f, 0.27f);
        static readonly Color Unknown = new Color(1f, 0.82f, 0.24f);
        static readonly Color Friend = new Color(0.38f, 0.73f, 1f);
        static readonly Color Neutral = new Color(0.36f, 0.86f, 0.47f);
        static readonly Color Own = new Color(0.85f, 0.93f, 1f);
        static readonly Color OwnMissile = new Color(0.47f, 0.86f, 1f);
        static readonly Color Amber = new Color(1f, 0.72f, 0.25f);

        readonly List<Blip> _blips = new List<Blip>();
        readonly List<MissileBlip> _missiles = new List<MissileBlip>();
        readonly List<Label> _labels = new List<Label>();
        readonly float _symbolPx, _fontPx;
        int _scale = 3;
        int _usedLabels;
        float _headingDeg, _speedMs, _essmReachM, _ciwsReachM;

        /// <summary>A track was clicked (its id).</summary>
        public event Action<int> TrackClicked;

        public float RadiusM => Scales[_scale];

        /// <param name="compact">The Captain's small inset: smaller symbols and type.</param>
        public TrackScope(bool compact)
        {
            _symbolPx = compact ? 5f : 7f;
            _fontPx = compact ? 9f : 11f;
            style.flexGrow = 1f;
            pickingMode = PickingMode.Position;
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<WheelEvent>(OnWheel);
            // Labels are placed from the layout; a scope shown for the first time (a seat switch) has none until now.
            RegisterCallback<GeometryChangedEvent>(_ => { PlaceLabels(); MarkDirtyRepaint(); });
        }

        /// <summary>Zoom one step: negative in, positive out.</summary>
        public void Zoom(int step) => _scale = Mathf.Clamp(_scale + step, 0, Scales.Length - 1);

        /// <summary>Set the picture for this refresh and redraw.</summary>
        public void SetPicture(float ownHeadingDeg, float ownSpeedMs, float essmReachM, float ciwsReachM,
            List<Blip> blips, List<MissileBlip> missiles)
        {
            _headingDeg = ownHeadingDeg;
            _speedMs = ownSpeedMs;
            _essmReachM = essmReachM;
            _ciwsReachM = ciwsReachM;
            _blips.Clear(); _blips.AddRange(blips);
            _missiles.Clear(); _missiles.AddRange(missiles);
            PlaceLabels();
            MarkDirtyRepaint();
        }

        // ── Drawing ────────────────────────────────────────────────────────

        void Geometry(out Vector2 centre, out float radiusPx)
        {
            Rect r = contentRect;
            centre = r.center;
            radiusPx = Mathf.Max(10f, 0.5f * Mathf.Min(r.width, r.height) - 14f);
        }

        Vector2 ToPx(Vector2 offsetM, Vector2 centre, float radiusPx) =>
            centre + new Vector2(offsetM.x, -offsetM.y) * (radiusPx / RadiusM);

        void Draw(MeshGenerationContext mgc)
        {
            Geometry(out Vector2 c, out float R);
            Painter2D p = mgc.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            // Range rings and the north tick.
            p.lineWidth = 1f;
            p.strokeColor = Ring;
            for (int i = 1; i <= 4; i++) Circle(p, c, R * i / 4f);
            p.BeginPath(); p.MoveTo(c + new Vector2(0f, -R)); p.LineTo(c + new Vector2(0f, -R - 8f)); p.Stroke();

            // Weapon reach, dashed, when it fits the scale.
            if (_essmReachM > 0f && _essmReachM <= RadiusM) DashedCircle(p, c, R * _essmReachM / RadiusM, new Color(OwnMissile.r, OwnMissile.g, OwnMissile.b, 0.45f));
            if (_ciwsReachM > 0f && _ciwsReachM <= RadiusM && R * _ciwsReachM / RadiusM > 6f) DashedCircle(p, c, R * _ciwsReachM / RadiusM, new Color(Hostile.r, Hostile.g, Hostile.b, 0.35f));

            // Own ship: a circle and her heading, a minute of travel long (at least a little).
            p.strokeColor = Own;
            p.lineWidth = 1.5f;
            Circle(p, c, 4f);
            Vector2 dir = new Vector2(Mathf.Sin(_headingDeg * Mathf.Deg2Rad), -Mathf.Cos(_headingDeg * Mathf.Deg2Rad));
            float lead = Mathf.Max(10f, _speedMs * OwnLeaderS * R / RadiusM);
            p.BeginPath(); p.MoveTo(c + dir * 4f); p.LineTo(c + dir * (4f + lead)); p.Stroke();

            // The ship's missiles, joined to their targets.
            for (int i = 0; i < _missiles.Count; i++)
            {
                MissileBlip m = _missiles[i];
                Vector2 at = ToPx(m.Offset, c, R);
                if ((at - c).magnitude > R + 2f) continue;
                if (m.HasTarget)
                {
                    p.strokeColor = new Color(OwnMissile.r, OwnMissile.g, OwnMissile.b, 0.3f);
                    p.lineWidth = 1f;
                    p.BeginPath(); p.MoveTo(at); p.LineTo(ToPx(m.TargetOffset, c, R)); p.Stroke();
                }
                p.strokeColor = OwnMissile;
                p.lineWidth = 1.2f;
                p.BeginPath(); p.MoveTo(at); p.LineTo(at + new Vector2(m.Velocity.x, -m.Velocity.y) * (MissileLeaderS * R / RadiusM)); p.Stroke();
                p.fillColor = OwnMissile;
                p.BeginPath(); p.Arc(at, 2.2f, Angle.Degrees(0f), Angle.Degrees(360f)); p.Fill();
            }

            // Tracks.
            for (int i = 0; i < _blips.Count; i++)
            {
                Blip b = _blips[i];
                Vector2 at = ToPx(b.Offset, c, R);
                if ((at - c).magnitude > R + 2f) continue;
                Color col = ColourOf(b.Identity);
                p.strokeColor = col;
                p.lineWidth = 1.2f;
                p.BeginPath(); p.MoveTo(at); p.LineTo(at + new Vector2(b.Velocity.x, -b.Velocity.y) * (LeaderS * R / RadiusM)); p.Stroke();
                p.lineWidth = 1.6f;
                Symbol(p, at, b.Kind, b.Identity, _symbolPx);
                if (b.Kind == TargetKind.AntiShipMissile) { p.fillColor = col; p.BeginPath(); p.Arc(at, 1.6f, Angle.Degrees(0f), Angle.Degrees(360f)); p.Fill(); }
                if (b.Proposed) { p.strokeColor = Amber; p.lineWidth = 1f; DashedCircle(p, at, _symbolPx + 6f, Amber); }
                if (b.Selected) Brackets(p, at, _symbolPx + 5f);
            }
        }

        static Color ColourOf(Identity id) =>
            id == Identity.Hostile ? Hostile : id == Identity.Friend ? Friend : id == Identity.Neutral ? Neutral : Unknown;

        static void Circle(Painter2D p, Vector2 c, float r)
        {
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Stroke();
        }

        static void DashedCircle(Painter2D p, Vector2 c, float r, Color colour)
        {
            p.strokeColor = colour;
            p.lineWidth = 1f;
            for (float a = 0f; a < 360f; a += 12f)
            {
                p.BeginPath();
                p.Arc(c, r, Angle.Degrees(a), Angle.Degrees(a + 6f));
                p.Stroke();
            }
        }

        /// <summary>NTDS-style: air tracks are the top half of the shape, surface tracks the whole shape.</summary>
        static void Symbol(Painter2D p, Vector2 at, TargetKind kind, Identity id, float s)
        {
            bool air = kind == TargetKind.Aircraft || kind == TargetKind.AntiShipMissile;
            p.BeginPath();
            switch (id)
            {
                case Identity.Hostile:   // diamond
                    p.MoveTo(at + new Vector2(-s, 0f)); p.LineTo(at + new Vector2(0f, -s)); p.LineTo(at + new Vector2(s, 0f));
                    if (!air) { p.LineTo(at + new Vector2(0f, s)); p.ClosePath(); }
                    break;
                case Identity.Friend:
                case Identity.Neutral:   // circle
                    if (air) p.Arc(at, s, Angle.Degrees(180f), Angle.Degrees(360f));
                    else p.Arc(at, s, Angle.Degrees(0f), Angle.Degrees(360f));
                    break;
                default:                 // square
                    p.MoveTo(at + new Vector2(-s, 0f)); p.LineTo(at + new Vector2(-s, -s)); p.LineTo(at + new Vector2(s, -s)); p.LineTo(at + new Vector2(s, 0f));
                    if (!air) { p.LineTo(at + new Vector2(s, s)); p.LineTo(at + new Vector2(-s, s)); p.ClosePath(); }
                    break;
            }
            p.Stroke();
        }

        static void Brackets(Painter2D p, Vector2 at, float s)
        {
            p.strokeColor = Amber;
            p.lineWidth = 1.5f;
            float k = s * 0.45f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    Vector2 corner = at + new Vector2(sx * s, sy * s);
                    p.BeginPath();
                    p.MoveTo(corner - new Vector2(sx * k, 0f));
                    p.LineTo(corner);
                    p.LineTo(corner - new Vector2(0f, sy * k));
                    p.Stroke();
                }
        }

        // ── Labels (Painter2D draws no text) ─────────────────────────────────

        void PlaceLabels()
        {
            _usedLabels = 0;
            Geometry(out Vector2 c, out float R);
            for (int i = 1; i <= 4; i++)
            {
                float km = RadiusM * i / 4f / 1000f;
                PutLabel(System.FormattableString.Invariant($"{km:0.#} km"), c + new Vector2(6f, -R * i / 4f + 2f), Ring);   // just inside each ring, clear of N
            }
            PutLabel("N", c + new Vector2(-4f, -R - 9f - _fontPx), Ring);
            for (int i = 0; i < _blips.Count; i++)
            {
                Blip b = _blips[i];
                Vector2 at = ToPx(b.Offset, c, R);
                if ((at - c).magnitude > R + 2f) continue;
                PutLabel(b.Id.ToString(), at + new Vector2(_symbolPx + 3f, -_fontPx * 0.6f), b.Selected ? Amber : ColourOf(b.Identity));
            }
            for (int i = _usedLabels; i < _labels.Count; i++) _labels[i].style.display = DisplayStyle.None;
        }

        void PutLabel(string text, Vector2 at, Color colour)
        {
            Label l;
            if (_usedLabels < _labels.Count) l = _labels[_usedLabels];
            else
            {
                l = new Label { pickingMode = PickingMode.Ignore };
                l.style.position = Position.Absolute;
                l.style.fontSize = _fontPx;
                l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0f;
                l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0f;
                Add(l);
                _labels.Add(l);
            }
            _usedLabels++;
            l.text = text;
            l.style.color = colour;
            l.style.left = at.x;
            l.style.top = at.y;
            l.style.display = DisplayStyle.Flex;
        }

        // ── Input ──────────────────────────────────────────────────────────

        void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            Geometry(out Vector2 c, out float R);
            Vector2 local = this.WorldToLocal(e.position);
            int best = -1;
            float bestPx = 18f;
            for (int i = 0; i < _blips.Count; i++)
            {
                float d = (ToPx(_blips[i].Offset, c, R) - local).magnitude;
                if (d < bestPx) { bestPx = d; best = _blips[i].Id; }
            }
            if (best >= 0) { TrackClicked?.Invoke(best); e.StopPropagation(); }
        }

        void OnWheel(WheelEvent e)
        {
            Zoom(e.delta.y > 0f ? 1 : -1);
            PlaceLabels();
            MarkDirtyRepaint();
            e.StopPropagation();
        }
    }
}
