using UnityEngine;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Cockpit;
using DeepWater.AirWing.Core.Gates;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.World;

namespace DeepWater.AirWing.Modules.Cockpit
{
    /// <summary>
    /// The HUD (W11, W22, W26, W27): true airspeed, altitude, climb, heading, angle of attack, G, Mach and throttle,
    /// each with its unit; the active device and seat; each part's level and any fallback. A velocity-vector marker
    /// shows where the jet is going, and a ring shows the mouse aim while the instructor flies. With a gate course
    /// (slice 1e), the next gate, its bands and a marker on it, and what the jet held through the last one.
    /// <para>Drawn with IMGUI: no assets to set up and nothing to build, which suits a first flight. It allocates a
    /// little per frame; a UI Toolkit HUD can replace it once the layout settles.</para>
    /// </summary>
    public sealed class FlightHud : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;
        [Tooltip("Show whichever aircraft the player flies (the seat cycle, W42); off, or with no player, show Aircraft Id.")]
        [SerializeField] bool followPlayer = true;
        [SerializeField] int aircraftId = 1;
        [SerializeField] Camera viewCamera;
        [Tooltip("Optional: the gate course to show.")]
        [SerializeField] GateCourseView gates;
        [SerializeField] Color colour = new Color(0.35f, 1f, 0.45f, 0.95f);

        AircraftView _view;
        GUIStyle _left, _right, _centre;

        void OnGUI()
        {
            if ((_view == null || _view.AircraftId != Target()) && !Find()) return;
            Styles();
            var aircraft = _view.Aircraft;
            AirData air = aircraft.Atmosphere.Sample(aircraft.State.Position.y);
            HudReadout r = HudReadout.From(aircraft.State, air, aircraft.LastInputs);
            PlayerSeat seat = runner.PlayerSeat;

            float w = Screen.width, h = Screen.height, s = Mathf.Max(1f, h / 1080f);
            var size = new Vector2(320f, 34f) * s;

            GUI.Label(new Rect(40f * s, 0.35f * h, size.x, size.y), $"{r.TrueAirspeedKt,4:F0} KT TAS", _left);
            GUI.Label(new Rect(40f * s, 0.35f * h + size.y, size.x, size.y), $"M {r.Mach:F2}", _left);
            GUI.Label(new Rect(40f * s, 0.35f * h + 2f * size.y, size.x, size.y), $"α {r.AngleOfAttackDeg,4:F1}°", _left);
            GUI.Label(new Rect(40f * s, 0.35f * h + 3f * size.y, size.x, size.y), $"{r.LoadFactorG,4:F1} G", _left);

            GUI.Label(new Rect(w - 40f * s - size.x, 0.35f * h, size.x, size.y), $"{r.AltitudeFt,6:F0} FT", _right);
            GUI.Label(new Rect(w - 40f * s - size.x, 0.35f * h + size.y, size.x, size.y), $"{r.ClimbFpm,6:F0} FT/MIN", _right);
            GUI.Label(new Rect(w - 40f * s - size.x, 0.35f * h + 2f * size.y, size.x, size.y), r.Afterburner ? "THR AB" : $"THR {r.ThrottlePct,3:F0} %", _right);

            GUI.Label(new Rect(0.5f * w - size.x * 0.5f, 30f * s, size.x, size.y), $"HDG {r.HeadingDeg:000}°", _centre);
            if (gates != null && gates.Course != null) Course(gates.Course, s, size);
            if (r.Crashed)
            {
                GUI.Label(new Rect(0.5f * w - size.x * 0.5f, 0.45f * h, size.x, size.y * 2f), "CRASHED", _centre);
                GUI.Label(new Rect(0.5f * w - size.x, 0.45f * h + 2f * size.y, 2f * size.x, size.y), "R  /  VIEW BUTTON:  RESET", _centre);
                if (aircraft.CrashReason != null)
                    GUI.Label(new Rect(0.5f * w - 2f * size.x, 0.45f * h + 3f * size.y, 4f * size.x, size.y), aircraft.CrashReason.ToUpperInvariant(), _centre);
            }

            string device = runner.Roster != null ? runner.Roster.Describe(_view.AircraftId) : _view.Seat.ToString().ToUpperInvariant();
            if (seat != null && _view.AircraftId == runner.PlayerAircraftId && seat.LevelHolding) device += "  ·  LEVEL HOLD";
            if (aircraft.Gear is Core.LandingGear.LandingGearL1)
            {
                float g = aircraft.State.GearDeployed;
                string gear = g >= 1f ? "GEAR DOWN" : g <= 0f ? "GEAR UP" : $"GEAR {g * 100f:F0} %";
                if (aircraft.State.InContact) gear += "  ·  WOW";
                if (aircraft.LastInputs.Brakes) gear += "  ·  BRAKES";
                GUI.Label(new Rect(40f * s, h - 4f * size.y, w, size.y), gear, _left);
            }
            GUI.Label(new Rect(40f * s, h - 3f * size.y, w, size.y), $"SEAT {device}", _left);
            GUI.Label(new Rect(40f * s, h - 2f * size.y, w, size.y), string.Join("  ·  ", aircraft.Levels), _left);

            if (viewCamera != null)
            {
                Marker(aircraft.State.Position + aircraft.State.Velocity.normalized * 1000f, "-O-", s);
                if (seat != null && seat.Owner == DeviceSource.Mouse && _view.AircraftId == runner.PlayerAircraftId)
                    Marker(aircraft.State.Position + seat.AimDirection * 1000f, "( )", s);
            }
            if (runner.Views.Count > 1 && runner.Roster != null) PickList(w, s, size);
        }

        /// <summary>Who flies what (W42), top right: one line per aircraft, the one shown marked; Tab or D-pad right steps on.</summary>
        void PickList(float w, float s, Vector2 size)
        {
            float x = w - 40f * s - 2f * size.x, y = 30f * s;
            GUI.Label(new Rect(x, y, 2f * size.x, size.y), "TAB / D-PAD RIGHT: NEXT SEAT", _right);
            for (int i = 0; i < runner.Views.Count; i++)
            {
                AircraftView v = runner.Views[i];
                if (v.Aircraft == null) continue;
                string crashed = v.Aircraft.State.Status == Core.State.FlightStatus.Crashed ? "  ·  CRASHED" : "";
                string mark = v.AircraftId == _view.AircraftId ? "> " : "";
                GUI.Label(new Rect(x, y + (i + 1) * size.y, 2f * size.x, size.y), $"{mark}{v.AircraftId}  {runner.Roster.Describe(v.AircraftId)}{crashed}", _right);
            }
        }

        void Course(GateCourse course, float s, Vector2 size)
        {
            Gate next = course.Gates[course.Next];
            float x = 40f * s, y = 30f * s, wide = 1000f * s;
            GUI.Label(new Rect(x, y, wide, size.y), $"NEXT {course.Next + 1}/{course.Gates.Count}  {next.Name.ToUpperInvariant()}", _left);
            GUI.Label(new Rect(x, y + size.y, wide, size.y), Bands(next), _left);
            if (course.Passes.Count > 0)
            {
                GatePass p = course.Passes[course.Passes.Count - 1];
                Gate g = course.Gates[p.Gate];
                string speed = g.SpeedKt.IsAny ? $"{p.SpeedKt:F0} KT" : $"{p.SpeedKt:F0} KT {(p.SpeedOk ? "OK" : "MISS")}";
                string alt = g.AltitudeFt.IsAny ? $"{p.AltitudeFt:F0} FT" : $"{p.AltitudeFt:F0} FT {(p.AltitudeOk ? "OK" : "MISS")}";
                GUI.Label(new Rect(x, y + 2f * size.y, wide, size.y), $"LAST {g.Name.ToUpperInvariant()}:  {speed}  ·  {alt}", _left);
            }
            if (viewCamera != null) Marker(next.Centre, $"[ {course.Next + 1} ]", s);
        }

        static string Bands(in Gate g)
        {
            if (g.SpeedKt.IsAny && g.AltitudeFt.IsAny) return "FLY THROUGH";
            if (g.SpeedKt.IsAny) return g.AltitudeFt.Describe("FT");
            if (g.AltitudeFt.IsAny) return g.SpeedKt.Describe("KT");
            return $"{g.SpeedKt.Describe("KT")}  ·  {g.AltitudeFt.Describe("FT")}";
        }

        void Marker(Vector3 world, string glyph, float s)
        {
            Vector3 p = viewCamera.WorldToScreenPoint(world);
            if (p.z <= 0f) return;
            var rect = new Rect(p.x - 40f * s, Screen.height - p.y - 20f * s, 80f * s, 40f * s);
            GUI.Label(rect, glyph, _centre);
        }

        void Styles()
        {
            if (_left != null) return;
            _left = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22f * Mathf.Max(1f, Screen.height / 1080f)), fontStyle = FontStyle.Bold };
            _left.normal.textColor = colour;
            _right = new GUIStyle(_left) { alignment = TextAnchor.UpperRight };
            _centre = new GUIStyle(_left) { alignment = TextAnchor.MiddleCenter };
        }

        int Target() => followPlayer && runner != null && runner.PlayerAircraftId != 0 ? runner.PlayerAircraftId : aircraftId;

        bool Find()
        {
            if (runner == null) return false;
            int id = Target();
            foreach (AircraftView v in runner.Views)
                if (v.AircraftId == id && v.Aircraft != null) { _view = v; return true; }
            return false;
        }
    }
}
