using UnityEngine;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Cockpit;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Modules.Aircraft;

namespace DeepWater.AirWing.Modules.Cockpit
{
    /// <summary>
    /// The HUD (W11, W22, W26, W27): true airspeed, altitude, climb, heading, angle of attack, G, Mach and throttle,
    /// each with its unit; the active device and seat; each part's level and any fallback. A velocity-vector marker
    /// shows where the jet is going, and a ring shows the mouse aim while the instructor flies.
    /// <para>Drawn with IMGUI: no assets to set up and nothing to build, which suits a first flight. It allocates a
    /// little per frame; a UI Toolkit HUD can replace it once the layout settles.</para>
    /// </summary>
    public sealed class FlightHud : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;
        [SerializeField] int aircraftId = 1;
        [SerializeField] Camera viewCamera;
        [SerializeField] Color colour = new Color(0.35f, 1f, 0.45f, 0.95f);

        AircraftView _view;
        GUIStyle _left, _right, _centre;

        void OnGUI()
        {
            if (_view == null && !Find()) return;
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
            if (r.Crashed) GUI.Label(new Rect(0.5f * w - size.x * 0.5f, 0.45f * h, size.x, size.y * 2f), "CRASHED", _centre);

            string device = seat != null && _view.Seat == AircraftView.StartSeat.Player ? $"PLAYER · {seat.Owner}" : _view.Seat.ToString().ToUpperInvariant();
            GUI.Label(new Rect(40f * s, h - 3f * size.y, w, size.y), $"SEAT {device}", _left);
            GUI.Label(new Rect(40f * s, h - 2f * size.y, w, size.y), string.Join("  ·  ", aircraft.Levels), _left);

            if (viewCamera != null)
            {
                Marker(aircraft.State.Position + aircraft.State.Velocity.normalized * 1000f, "-O-", s);
                if (seat != null && seat.Owner == DeviceSource.Mouse) Marker(aircraft.State.Position + seat.AimDirection * 1000f, "( )", s);
            }
        }

        void Marker(Vector3 world, string glyph, float s)
        {
            Vector3 p = viewCamera.WorldToScreenPoint(world);
            if (p.z <= 0f) return;
            var rect = new Rect(p.x - 20f * s, Screen.height - p.y - 20f * s, 40f * s, 40f * s);
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

        bool Find()
        {
            if (runner == null) return false;
            foreach (AircraftView v in runner.Views)
                if (v.AircraftId == aircraftId && v.Aircraft != null) { _view = v; return true; }
            return false;
        }
    }
}
