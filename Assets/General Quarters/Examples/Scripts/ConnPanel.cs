using UnityEngine;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Modules.Propulsion;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// The conn panel (IMGUI, top right): what the ship is doing, the telegraph, rudder and course buttons for the
    /// mouse, the orders given, and the controls. Every button goes through <see cref="ConnController"/>, the same
    /// path as the keyboard and the pads. Hide it with H / View / button 5. A stand-in until slice 5's helm panel.
    /// </summary>
    [RequireComponent(typeof(ConnController))]
    public sealed class ConnPanel : MonoBehaviour
    {
        const float Width = 440f, Pad = 10f, Line = 19f, RowLabel = 56f, Gap = 4f;
        static readonly EngineBell[] Ahead = { EngineBell.AheadOneThird, EngineBell.AheadTwoThirds, EngineBell.AheadStandard, EngineBell.AheadFull, EngineBell.AheadFlank };
        static readonly string[] AheadLabels = { "⅓", "⅔", "Standard", "Full", "Flank" };
        static readonly EngineBell[] Astern = { EngineBell.BackOneThird, EngineBell.BackTwoThirds, EngineBell.BackFull, EngineBell.BackEmergency };
        static readonly string[] AsternLabels = { "⅓", "⅔", "Full", "Emerg." };
        static readonly float[] Rudders = { -35f, -20f, -15f, -10f, 0f, 10f, 15f, 20f, 35f };

        const string KeysHelp = "Keyboard & mouse — rudder A D or ← → (hold Shift for hard over) · bells W S or ↑ ↓ · course Q E · " +
                                "steady Space · midships X · all stop Z · camera V, right-drag, wheel, R F · panel H";
        const string PadHelp = "Xbox / gamepad — rudder left stick · bells D-pad ↑ ↓ or RB LB · course D-pad ← → · steady A · " +
                               "midships B · all stop Y · camera X, right stick, RT LT · panel View";
        const string GenericHelp = "Generic pad — rudder stick · bells hat ↑ ↓ · course hat ← → · steady trigger · midships button 2 · " +
                                   "all stop 3 · camera 4 · panel 5";
        const string RebindHelp = "Let go of the rudder and she steadies on her heading. Rebind in Project Settings → Input System Package, map 'Conn'.";

        ConnController _conn;
        bool _visible = true, _showControls = true;
        float _courseDraft = -1f, _speedDraft = 15f, _height = 600f;
        GUIStyle _title, _label, _small, _on, _help;

        void Awake() => _conn = GetComponent<ConnController>();

        void Update()
        {
            if (_conn.Controls != null && _conn.Controls.Ready && _conn.Controls.TogglePanelPressed) _visible = !_visible;
        }

        void OnGUI()
        {
            ShipDrive ship = _conn.Ship;
            if (!_visible || ship == null || ship.EngineRoom == null) return;
            Styles();
            ShipMotion m = ship.Motion;
            HelmCommand c = ship.Command;
            HelmOutput h = ship.LastHelm;
            if (_courseDraft < 0f) _courseDraft = Mathf.Round(m.HeadingDeg) % 360f;

            float x0 = Screen.width - Width - 12f, y0 = 12f;
            GUI.Box(new Rect(x0, y0, Width, _height), GUIContent.none);   // sized from last frame's content
            float x = x0 + Pad, y = y0 + Pad, w = Width - 2f * Pad;

            GUI.Label(new Rect(x, y, w, Line + 4f), "Conn — DDG-51 (General Quarters)", _title); y += Line + 6f;
            float heel = Mathf.Asin(Mathf.Clamp((ship.transform.rotation * Vector3.right).y, -1f, 1f)) * Mathf.Rad2Deg;
            string steering = c.Course == CourseMode.Manual ? $"rudder by hand, {Side(c.RudderDeg)}"
                            : c.Course == CourseMode.Route ? $"route {c.RouteId}, steering {h.SteeringCourseDeg:000}"
                            : $"steering {c.CourseDeg:000}";
            string engines = c.Speed == SpeedMode.Bell ? ConnController.BellName(c.Bell) : $"holding {c.SpeedKn:0} kn";
            Row(ref y, x, w, $"Heading {m.HeadingDeg:000.0}°    {steering}");
            Row(ref y, x, w, $"Speed {m.SurgeMs / 0.514444f:0.0} kn    {engines}");
            Row(ref y, x, w, $"Shafts {ship.EngineRoom.PortShaftRps * 60f:0} / {ship.EngineRoom.StarboardShaftRps * 60f:0} rpm    Rudders {Side(ship.EngineRoom.PortRudderDeg)}");
            Row(ref y, x, w, $"Turning {m.YawRateRadS * Mathf.Rad2Deg * 60f:+0;-0;0}°/min    Heel {Mathf.Abs(heel):0.0}° {(heel > 0.05f ? "to port" : heel < -0.05f ? "to starboard" : "")}");
            Row(ref y, x, w, $"Last control used: {_conn.Controls.LastDevice}", _small);
            y += Gap;

            // Telegraph: a labelled row ahead, a labelled row astern with Stop at its end.
            GUI.Label(new Rect(x, y, w, Line), "Engine order telegraph", _label); y += Line;
            float bw = (w - RowLabel - 4f * Gap) / 5f;
            GUI.Label(new Rect(x, y + 3f, RowLabel, 22f), "Ahead", _label);
            for (int i = 0; i < Ahead.Length; i++)
                if (Button(new Rect(x + RowLabel + i * (bw + Gap), y, bw, 24f), AheadLabels[i], IsBell(c, Ahead[i]))) _conn.RingBell(Ahead[i]);
            y += 28f;
            GUI.Label(new Rect(x, y + 3f, RowLabel, 22f), "Back", _label);
            for (int i = 0; i < Astern.Length; i++)
                if (Button(new Rect(x + RowLabel + i * (bw + Gap), y, bw, 24f), AsternLabels[i], IsBell(c, Astern[i]))) _conn.RingBell(Astern[i]);
            if (Button(new Rect(x + RowLabel + 4f * (bw + Gap), y, bw, 24f), "STOP", IsBell(c, EngineBell.Stop))) _conn.RingBell(EngineBell.Stop);
            y += 30f;

            // Rudder: held until steadied or a course is ordered.
            GUI.Label(new Rect(x, y, w, Line), "Rudder — held until you steady her or order a course", _label); y += Line;
            float rw = (w - (Rudders.Length - 1) * Gap) / Rudders.Length;
            for (int i = 0; i < Rudders.Length; i++)
            {
                float r = Rudders[i];
                string label = r == 0f ? "Mid" : $"{(r < 0f ? "L" : "R")}{Mathf.Abs(r):0}";
                if (Button(new Rect(x + i * (rw + Gap), y, rw, 22f), label, c.Course == CourseMode.Manual && Mathf.Abs(c.RudderDeg - r) < 0.5f))
                    _conn.Rudder(r);
            }
            y += 26f;
            if (GUI.Button(new Rect(x, y, w, 22f), "Steady as she goes (hold this heading)")) _conn.Steady();
            y += 28f;

            // Course and speed orders.
            DraftRow(ref y, x, w, "Course", ref _courseDraft, 10f, 1f, $"{Mathf.RoundToInt(_courseDraft) % 360:000}", "Steer it", true,
                () => _conn.SteerCourse(_courseDraft));
            DraftRow(ref y, x, w, "Speed", ref _speedDraft, 5f, 1f, $"{_speedDraft:0} kn", "Hold this speed", false,
                () => _conn.HoldSpeed(_speedDraft));
            y += Gap;

            GUI.Label(new Rect(x, y, w, Line), "Orders", _label); y += Line;
            foreach (string line in _conn.RecentOrders) { GUI.Label(new Rect(x, y, w, 15f), line, _small); y += 15f; }
            y += Gap;

            _showControls = GUI.Toggle(new Rect(x, y, w, Line), _showControls, " Controls", _label); y += Line + 2f;
            if (_showControls)
                foreach (string help in new[] { KeysHelp, PadHelp, GenericHelp, RebindHelp })
                {
                    float hh = _help.CalcHeight(new GUIContent(help), w);
                    GUI.Label(new Rect(x, y, w, hh), help, _help);
                    y += hh + 3f;
                }
            _height = y - y0 + Pad;
        }

        void DraftRow(ref float y, float x, float w, string title, ref float draft, float big, float small, string value, string apply, bool compass, System.Action order)
        {
            GUI.Label(new Rect(x, y + 2f, RowLabel, 22f), title, _label);
            float bx = x + RowLabel;
            float Step(float v, float d) => compass ? CompassMath.Normalize(v + d) : Mathf.Clamp(v + d, 0f, 31f);
            if (GUI.Button(new Rect(bx, y, 38f, 22f), $"−{big:0}")) draft = Step(draft, -big);
            if (GUI.Button(new Rect(bx + 40f, y, 30f, 22f), $"−{small:0}")) draft = Step(draft, -small);
            GUI.Label(new Rect(bx + 76f, y + 2f, 54f, 22f), value, _label);
            if (GUI.Button(new Rect(bx + 128f, y, 30f, 22f), $"+{small:0}")) draft = Step(draft, small);
            if (GUI.Button(new Rect(bx + 160f, y, 38f, 22f), $"+{big:0}")) draft = Step(draft, big);
            if (GUI.Button(new Rect(bx + 206f, y, w - RowLabel - 206f, 22f), apply)) order();
            y += 26f;
        }

        static bool IsBell(in HelmCommand c, EngineBell bell) => c.Speed == SpeedMode.Bell && c.Bell == bell;

        static string Side(float rudderDeg) =>
            Mathf.Abs(rudderDeg) < 0.5f ? "amidships" : $"{(rudderDeg > 0f ? "right" : "left")} {Mathf.Abs(rudderDeg):0}°";

        void Row(ref float y, float x, float w, string text, GUIStyle style = null)
        {
            GUI.Label(new Rect(x, y, w, Line), text, style ?? _label);
            y += Line;
        }

        bool Button(Rect r, string text, bool on) => GUI.Button(r, text, on ? _on : GUI.skin.button);

        void Styles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            _help = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _on = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            _on.normal.textColor = _on.hover.textColor = new Color(1f, 0.8f, 0.2f);
        }
    }
}
