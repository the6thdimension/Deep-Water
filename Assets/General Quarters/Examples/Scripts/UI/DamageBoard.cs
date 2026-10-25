using UnityEngine;
using UnityEngine.UIElements;
using DeepWater.GeneralQuarters.Core.DamageControl;
using AppText = Unity.AppUI.UI.Text;
using TextSize = Unity.AppUI.UI.TextSize;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// The damage-control board (G59): the ship's spaces drawn as gauges, filled from the bottom as they take water, and
    /// the numbers that say how she lies. A side view (upper spaces over holds, bow to the right) and, unless compact, a
    /// plan of the holds (port wing on top, as seen from above with the bow right). A holed space is outlined in amber, a
    /// flooded one is red. Reads the core; never writes to it.
    /// </summary>
    public sealed class DamageBoard : VisualElement
    {
        static readonly Color Dry = new Color(0.47f, 0.63f, 0.78f, 0.12f);
        static readonly Color Edge = new Color(0.43f, 0.63f, 0.78f, 0.35f);
        static readonly Color Water = new Color(0.24f, 0.55f, 1f, 0.85f);
        static readonly Color Flooded = new Color(1f, 0.32f, 0.27f, 0.9f);
        static readonly Color Holed = new Color(1f, 0.72f, 0.25f, 1f);

        readonly VisualElement _side, _plan;
        readonly AppText _lie, _stability, _water;
        readonly bool _compact;
        IFlooding _f;

        public DamageBoard(bool compact)
        {
            _compact = compact;
            _side = View("gq-dc-side", DrawSide);
            if (!compact) _plan = View("gq-dc-plan", DrawPlan);
            _lie = Line();
            _stability = Line();
            _water = Line();
        }

        VisualElement View(string cls, System.Action<MeshGenerationContext> draw)
        {
            var v = new VisualElement();
            v.AddToClassList("gq-dc-view");
            v.AddToClassList(cls);
            v.generateVisualContent += draw;
            Add(v);
            return v;
        }

        AppText Line()
        {
            var t = new AppText("") { size = TextSize.XS };
            t.AddToClassList("gq-mono");
            Add(t);
            return t;
        }

        /// <summary>Redraw from the core's state; <paramref name="flooding"/> is null below Damage Control L1.</summary>
        public void Refresh(IFlooding flooding)
        {
            _f = flooding;
            _side.MarkDirtyRepaint();
            _plan?.MarkDirtyRepaint();
            if (flooding == null)
            {
                _lie.text = "Damage Control L0: hit points only, no compartments (flooding is L1).";
                _stability.text = _water.text = "";
                return;
            }
            float list = flooding.ListDeg, trim = flooding.TrimM;
            _lie.text = $"LIST {Mathf.Abs(list):0.0}° {(list > 0.05f ? "starboard" : list < -0.05f ? "port" : "")}   " +
                        $"TRIM {Mathf.Abs(trim):0.00} m {(trim > 0.005f ? "by the head" : trim < -0.005f ? "by the stern" : "")}   DRAFT {flooding.DraftM:0.00} m";
            DamagedStability s = flooding.Stability;
            _stability.text = _compact
                ? $"GM {flooding.IntactGmM:0.00} → {s.GmM:0.00} m"
                : $"GM {flooding.IntactGmM:0.00} → {s.GmM:0.00} m  (KB {s.KbM:0.00} + BM {s.BmM:0.00} − KG {s.KgM:0.00} − FS {s.FreeSurfaceM:0.00})";
            float tons = flooding.Water.WaterMassKg / 1000f, inflow = flooding.Water.InflowM3s * flooding.Plan.Profile.SeawaterDensityKgM3 / 1000f;
            string verdict = flooding.Verdict == FloodVerdict.Sinking ? "  ·  SINKING" : flooding.Verdict == FloodVerdict.Capsized ? "  ·  CAPSIZED" : "";
            _water.text = $"RESERVE BUOYANCY {flooding.ReserveBuoyancy:0%}   WATER {tons:0} t   INFLOW {inflow:0.0} t/s   " +
                          $"{flooding.WetSpaces} wet · {flooding.FullSpaces} flooded · {flooding.Breaches} holed{verdict}";
        }

        void DrawSide(MeshGenerationContext ctx)
        {
            if (_f == null) return;
            FloodPlan plan = _f.Plan;
            Rect r = _side.contentRect;
            int n = plan.StationCount;
            float w = r.width / n, h = r.height / 2f;
            for (int st = 0; st < n; st++)
            {
                float x = r.width - (st + 1) * w;   // bow (station 0) on the right
                Cell(ctx.painter2D, new Rect(x, 0f, w, h), Fill(plan.UpperSpace(st)), _f.IsBreached(plan.UpperSpace(st)));
                // The hold row shows the fullest of the station's hold and wings, and whether any of them is holed.
                int hold = plan.HoldSpace(st, 0f);
                float fill = Fill(hold);
                bool holed = _f.IsBreached(hold);
                if (plan.HasWings(st))
                {
                    int port = plan.HoldSpace(st, -100f), stbd = plan.HoldSpace(st, 100f);
                    fill = Mathf.Max(fill, Mathf.Max(Fill(port), Fill(stbd)));
                    holed |= _f.IsBreached(port) || _f.IsBreached(stbd);
                }
                Cell(ctx.painter2D, new Rect(x, h, w, h), fill, holed);
            }
        }

        void DrawPlan(MeshGenerationContext ctx)
        {
            if (_f == null) return;
            FloodPlan plan = _f.Plan;
            Rect r = _plan.contentRect;
            int n = plan.StationCount;
            float w = r.width / n, row = r.height / 3f;
            for (int st = 0; st < n; st++)
            {
                float x = r.width - (st + 1) * w;
                if (!plan.HasWings(st))
                {
                    int hold = plan.HoldSpace(st, 0f);
                    Cell(ctx.painter2D, new Rect(x, 0f, w, 3f * row), Fill(hold), _f.IsBreached(hold));
                    continue;
                }
                int port = plan.HoldSpace(st, -100f), centre = plan.HoldSpace(st, 0f), stbd = plan.HoldSpace(st, 100f);
                Cell(ctx.painter2D, new Rect(x, 0f, w, row), Fill(port), _f.IsBreached(port));
                Cell(ctx.painter2D, new Rect(x, row, w, row), Fill(centre), _f.IsBreached(centre));
                Cell(ctx.painter2D, new Rect(x, 2f * row, w, row), Fill(stbd), _f.IsBreached(stbd));
            }
        }

        float Fill(int space)
        {
            float cap = _f.Plan.Spaces[space].CapacityM3;
            return cap > 0f ? Mathf.Clamp01(_f.Water.Volume(space) / cap) : 0f;
        }

        /// <summary>One space: its outline, and water filled from the bottom to its fraction (red once it is full).</summary>
        static void Cell(Painter2D p, Rect cell, float fill, bool holed)
        {
            Rect inner = new Rect(cell.x + 1f, cell.y + 1f, cell.width - 2f, cell.height - 2f);
            Box(p, inner, Dry);
            if (fill > 0f)
            {
                float h = Mathf.Max(1f, inner.height * fill);
                Box(p, new Rect(inner.x, inner.yMax - h, inner.width, h), fill >= 0.999f ? Flooded : Water);
            }
            p.strokeColor = holed ? Holed : Edge;
            p.lineWidth = holed ? 2f : 1f;
            p.BeginPath();
            p.MoveTo(new Vector2(inner.xMin, inner.yMin));
            p.LineTo(new Vector2(inner.xMax, inner.yMin));
            p.LineTo(new Vector2(inner.xMax, inner.yMax));
            p.LineTo(new Vector2(inner.xMin, inner.yMax));
            p.ClosePath();
            p.Stroke();
        }

        static void Box(Painter2D p, Rect r, Color c)
        {
            p.fillColor = c;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMin));
            p.LineTo(new Vector2(r.xMax, r.yMax));
            p.LineTo(new Vector2(r.xMin, r.yMax));
            p.ClosePath();
            p.Fill();
        }
    }
}
