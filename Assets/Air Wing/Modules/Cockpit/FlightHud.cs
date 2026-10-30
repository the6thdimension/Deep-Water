using UnityEngine;
using DeepWater.AirWing.Core.Atmosphere;
using DeepWater.AirWing.Core.Cockpit;
using DeepWater.AirWing.Core.Gates;
using DeepWater.AirWing.Core.Radar;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.World;
using DeepWater.Gunnery.Guns;
using RHRadarSuite.Tracking;

namespace DeepWater.AirWing.Modules.Cockpit
{
    /// <summary>
    /// The HUD (W11, W22, W26, W27): true airspeed, altitude, climb, heading, angle of attack, G, Mach and throttle,
    /// each with its unit; the active device and seat; each part's level and any fallback. A velocity-vector marker
    /// shows where the jet is going, and a ring shows the mouse aim while the instructor flies. With a gate course
    /// (slice 1e), the next gate, its bands and a marker on it, and what the jet held through the last one.
    /// With a radar (SENSORS-CONCEPT J10), a radar display bottom right: its mode and range, each firm track by azimuth
    /// and range (F an IFF friend, ? asked and not answered, [ ] locked), the warning receiver, chaff and flares left, and
    /// the keys; the locked track is marked in the view.
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
        GUIStyle _left, _right, _centre, _small;

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
            if (aircraft.State.Status == Core.State.FlightStatus.Killed)
            {
                GUI.Label(new Rect(0.5f * w - size.x * 0.5f, 0.45f * h, size.x, size.y * 2f), "SHOT DOWN", _centre);
                if (aircraft.CrashReason != null)
                    GUI.Label(new Rect(0.5f * w - 2f * size.x, 0.45f * h + 2f * size.y, 4f * size.x, size.y), aircraft.CrashReason.ToUpperInvariant(), _centre);
            }
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
            if (aircraft.Profile.Gun.IsSet || aircraft.Profile.Combat.IsSet)
            {
                string gun = aircraft.Profile.Gun.IsSet ? $"GUN {aircraft.State.Rounds}{(aircraft.LastInputs.Trigger && aircraft.State.Rounds > 0 ? "  FIRING" : "")}" : "";
                string hp = aircraft.Profile.Combat.IsSet ? $"  ·  HP {aircraft.State.HitPoints:F0}/{aircraft.Profile.Combat.HitPoints:F0}" : "";
                string msl = aircraft.Profile.Missiles.IsSet ? $"  ·  MSL {aircraft.State.Missiles}{(aircraft.Profile.Missiles.HasRadarMissiles ? $" + {aircraft.State.RadarMissiles} RDR" : "")}{(runner.Flight.IsLocked(aircraft) ? $"  LOCK {aircraft.State.LockTargetId}" : aircraft.State.LockTargetId != 0 ? "  LOCKING" : "")}" : "";
                GUI.Label(new Rect(40f * s, h - 5f * size.y, w, size.y), gun + hp + msl, _left);
            }
            GUI.Label(new Rect(40f * s, h - 3f * size.y, w, size.y), $"SEAT {device}", _left);
            GUI.Label(new Rect(40f * s, h - 2f * size.y, w, size.y), string.Join("  ·  ", aircraft.Levels), _left);

            if (viewCamera != null)
            {
                Marker(aircraft.State.Position + aircraft.State.Velocity.normalized * 1000f, "-O-", s);
                if (seat != null && seat.Owner == DeviceSource.Mouse && _view.AircraftId == runner.PlayerAircraftId)
                    Marker(aircraft.State.Position + seat.AimDirection * 1000f, "( )", s);
                if (aircraft.Profile.Gun.IsSet && aircraft.State.Status == Core.State.FlightStatus.Flying) Pipper(aircraft, s);
                // The missile seeker's box (W65): dotted while it is locking, solid and labelled once locked.
                if (aircraft.State.LockTargetId != 0)
                    Marker(runner.Flight.Find(aircraft.State.LockTargetId).State.Position, runner.Flight.IsLocked(aircraft) ? "[ LOCK ]" : "[ · · ]", s);
            }
            if (runner.Views.Count > 1 && runner.Roster != null) PickList(w, s, size);
            JetRadar radar = runner.Flight.RadarOf(aircraft.Id);
            if (radar != null && aircraft.State.Status == Core.State.FlightStatus.Flying) RadarDisplay(aircraft, radar, w, h, s, size);
        }

        /// <summary>Azimuth either side of the nose the display spans when the mode has no volume of its own, degrees.</summary>
        const float DisplayAzimuthDeg = 60f;

        /// <summary>
        /// The radar display (J10): a B-scope, azimuth across and range up, bottom right; under it the warning receiver,
        /// the decoys and the keys. What the pilot's instruments show, never the truth: tracks, their IFF answers, the
        /// warning receiver's contacts.
        /// </summary>
        void RadarDisplay(Core.Aircraft a, JetRadar radar, float w, float h, float s, Vector2 size)
        {
            float side = 300f * s, x0 = w - 40f * s - side, y0 = h - 6f * size.y - side;
            RadarProfile p = radar.Profile;
            RadarModeProfile? volume = p.Volume(radar.Mode == RadarMode.SingleTarget ? radar.SearchMode : radar.Mode);
            float rangeM = radar.Mode == RadarMode.SingleTarget ? p.SingleTargetRangeM : volume.HasValue ? volume.Value.RangeM : p.SingleTargetRangeM;
            float azHalf = volume.HasValue ? volume.Value.AzimuthHalfDeg : DisplayAzimuthDeg;
            if (rangeM <= 0f) rangeM = 40000f;
            if (azHalf <= 0f) azHalf = DisplayAzimuthDeg;

            // The frame and the centre line.
            Line(x0, y0, side, 2f * s);
            Line(x0, y0 + side, side, 2f * s);
            Line(x0, y0, 2f * s, side);
            Line(x0 + side, y0, 2f * s, side + 2f * s);
            Line(x0 + side * 0.5f, y0 + side - 12f * s, 1f * s, 12f * s);
            string mode = radar.Mode switch
            {
                RadarMode.Off => "OFF", RadarMode.RangeWhileSearch => "RWS", RadarMode.TrackWhileScan => "TWS",
                RadarMode.Dogfight => "DOGFIGHT", RadarMode.SingleTarget => "STT", _ => radar.Mode.ToString().ToUpperInvariant(),
            };
            GUI.Label(new Rect(x0, y0 - size.y, side, size.y), $"RDR {mode}  {rangeM / 1000f:F0} KM", _left);

            Quaternion toBody = Quaternion.Inverse(a.State.Attitude);
            int shown = 0;
            foreach (RadarTrack t in radar.Tracks)
            {
                if (t.State != TrackState.Confirmed && t.State != TrackState.Coasting) continue;
                Vector3 at = new Vector3((float)t.Position.X, (float)t.Position.Y, (float)t.Position.Z);
                Vector3 rel = toBody * (at - a.State.Position);
                float az = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg, d = rel.magnitude;
                if (Mathf.Abs(az) > azHalf || d > rangeM) continue;
                shown++;
                bool locked = radar.LockedTrackId == t.Id;
                string glyph = radar.IdentityOf(t.Id) == TrackIdentity.Friend ? "F" : radar.ChallengesOf(t.Id) > 0 ? "?" : "o";
                if (locked) glyph = $"[{glyph}]";
                float px = x0 + side * (0.5f + 0.5f * az / azHalf), py = y0 + side * (1f - d / rangeM);
                GUI.Label(new Rect(px - 30f * s, py - 15f * s, 60f * s, 30f * s), glyph, _centre);
                if (locked && viewCamera != null) Marker(at, $"[ RDR {d / 1000f:F1} ]", s);
            }
            GUI.Label(new Rect(x0, y0 + side, side, size.y), $"{shown} TRK", _right);

            // The warning receiver, strongest first, and the decoys left.
            WarningReceiver rwr = runner.Flight.WarningReceiverOf(a.Id);
            string warn = "RWR —";
            if (rwr != null && rwr.Contacts.Count > 0)
            {
                WarningContact c = rwr.Contacts[0];
                string kind = c.Kind == PaintKind.Missile ? "MISSILE" : c.Kind == PaintKind.Track ? "TRACK" : "SEARCH";
                warn = $"RWR {kind} {Mathf.Repeat(c.AzimuthDeg, 360f):000}°{(rwr.Contacts.Count > 1 ? $" +{rwr.Contacts.Count - 1}" : "")}";
            }
            string decoys = a.Profile.Countermeasures.IsSet ? $"  ·  CHAFF {a.State.Chaff}  FLR {a.State.Flares}" : "";
            GUI.Label(new Rect(x0 - side, y0 + side + size.y, 2f * side, size.y), warn + decoys, _right);
            GUI.Label(new Rect(x0 - side, y0 + side + 2f * size.y, 2f * side, size.y), "V MODE · L LOCK · I IFF · C CHAFF · X FLARE · N RDR MSL", _small);
        }

        void Line(float x, float y, float width, float height)
        {
            Color was = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = was;
        }

        /// <summary>
        /// The lead-computing pipper (Gunnery K4): where a round fired now will be at the range of the aircraft nearest
        /// the bore line (inside 25° and 2,500 m), or at 700 m with none. Put it on where the target will be and fire.
        /// </summary>
        void Pipper(Core.Aircraft a, float s)
        {
            Core.Profiles.GunMount m = a.Profile.Gun;
            Vector3 muzzle = a.State.Position + a.State.Attitude * m.MuzzleM, bore = a.State.Attitude * m.BoreBody;
            float range = 700f, best = Mathf.Cos(25f * Mathf.Deg2Rad);
            string ranged = "";
            foreach (Core.Aircraft o in runner.Flight.Aircraft)
            {
                if (o.Id == a.Id || o.State.Status != Core.State.FlightStatus.Flying) continue;
                Vector3 to = o.State.Position - muzzle;
                float d = to.magnitude;
                if (d > 2500f || d < 50f) continue;
                float c = Vector3.Dot(to / d, bore);
                if (c > best) { best = c; range = d; ranged = $" {o.Id}"; }
            }
            if (LeadSight.RoundAt(m.Gun, muzzle, a.State.Velocity, bore, range, out Vector3 at, out _))
                Marker(at, $"-+-{ranged}", s);
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
                string crashed = v.Aircraft.State.Status == Core.State.FlightStatus.Crashed ? "  ·  CRASHED"
                    : v.Aircraft.State.Status == Core.State.FlightStatus.Killed ? "  ·  SHOT DOWN" : "";
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
            _small = new GUIStyle(_right) { fontSize = Mathf.RoundToInt(15f * Mathf.Max(1f, Screen.height / 1080f)) };
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
