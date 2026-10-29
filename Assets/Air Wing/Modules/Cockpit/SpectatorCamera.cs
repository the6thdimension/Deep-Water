using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.World;

namespace DeepWater.AirWing.Modules.Cockpit
{
    /// <summary>
    /// A spectator's camera for a fight nobody flies (SIX, 2026-10-29: "a camera rig to fly around the scene to witness
    /// engagement… track/follow entities with a quick push of a button or go free roam"). Two modes:
    /// <list type="bullet">
    /// <item><b>Follow:</b> behind the jet, eased into its turns like the chase camera; hold the right mouse button (or
    /// move the right stick) to orbit it, scroll to move in or out. Tab or ] (pad RB) follows the next jet, Shift+Tab or
    /// [ (pad LB) the one before, 1–9 a jet by its id.</item>
    /// <item><b>Free:</b> F (pad Y) lets go of the jet where the camera is. WASD to fly, Q and E down and up, the right
    /// mouse button (or right stick) to look, Shift for four times the speed, scroll to change the speed. F again
    /// follows the last jet.</item>
    /// </list>
    /// A tag over every jet names it, coloured by its spawn point (L hides them), and a line at the top says what is
    /// followed and who flies it (the seat roster's own words). Presentation only: it reads the runner and moves
    /// itself, on unscaled time so it handles the same while training runs fast.
    /// </summary>
    public sealed class SpectatorCamera : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;
        [Tooltip("Start following this aircraft id (0: start free).")]
        [SerializeField] int startFollowing = 1;
        [Header("Follow")]
        [SerializeField] float distanceM = 40f;
        [SerializeField] float heightM = 8f;
        [SerializeField] float followRate = 4f;
        [SerializeField] Vector2 distanceRangeM = new Vector2(15f, 2000f);
        [Header("Free")]
        [SerializeField] float speedMps = 150f;
        [SerializeField] Vector2 speedRangeMps = new Vector2(10f, 3000f);
        [SerializeField] float lookDegPerPixel = 0.15f;
        [SerializeField] float stickLookDegPerS = 120f;
        [Header("Labels")]
        [SerializeField] bool showTags = true;
        [SerializeField] Color textColour = new Color(1f, 1f, 1f, 0.95f);

        readonly List<AircraftView> _jets = new List<AircraftView>();
        readonly Dictionary<int, (string label, Color colour)> _sides = new Dictionary<int, (string, Color)>();
        AircraftView _target;
        int _lastFollowed;
        bool _free;
        float _yaw, _pitch, _orbitYaw, _orbitPitch;
        int _resets = -1;
        Camera _camera;
        GUIStyle _style, _tag;

        public bool Free => _free;
        public AircraftView Target => _free ? null : _target;

        void Start()
        {
            if (runner == null) runner = FindAnyObjectByType<AirWingRunner>();
            _camera = GetComponent<Camera>();
            Vector3 e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
            // The jets' sides, for their tags: whichever spawn point they start and come back at.
            foreach (SpawnPoint p in FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude))
                foreach (AircraftView v in p.Jets)
                    if (v != null) _sides[v.AircraftId] = (p.Label.ToUpperInvariant(), p.GizmoColour);
            _free = startFollowing == 0;
            _lastFollowed = startFollowing;
        }

        void LateUpdate()
        {
            if (runner == null) return;
            if (_jets.Count != runner.Views.Count) { _jets.Clear(); _jets.AddRange(runner.Views); }
            // The jet to start on, once the runner has built them (a smoke on 2026-10-29 found it never picked one up).
            if (!_free && _target == null && _jets.Count > 0)
            {
                AircraftView first = Find(_lastFollowed);
                Follow(first != null ? first : Next(1));
            }
            float dt = Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            bool shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if (kb != null && kb.lKey.wasPressedThisFrame) showTags = !showTags;
            if ((kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) ToggleFree();
            int step = 0;
            if (kb != null && (kb.tabKey.wasPressedThisFrame || kb.rightBracketKey.wasPressedThisFrame)) step = kb.tabKey.wasPressedThisFrame && shift ? -1 : 1;
            if (kb != null && kb.leftBracketKey.wasPressedThisFrame) step = -1;
            if (pad != null && pad.rightShoulder.wasPressedThisFrame) step = 1;
            if (pad != null && pad.leftShoulder.wasPressedThisFrame) step = -1;
            if (step != 0) Follow(Next(step));
            if (kb != null)
                for (int n = 1; n <= 9; n++)
                    if (kb[Key.Digit1 + (n - 1)].wasPressedThisFrame) Follow(Find(n));

            // Looking: the right mouse button held, or the right stick.
            Vector2 look = Vector2.zero;
            if (mouse != null && mouse.rightButton.isPressed) look += mouse.delta.ReadValue() * lookDegPerPixel;
            if (pad != null) look += pad.rightStick.ReadValue() * (stickLookDegPerS * dt);
            float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;

            if (_free || _target == null || _target.Aircraft == null) FlyFree(look, scroll, shift, kb, pad, dt);
            else Chase(look, scroll, dt);
        }

        void FlyFree(Vector2 look, float scroll, bool shift, Keyboard kb, Gamepad pad, float dt)
        {
            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            if (scroll != 0f) speedMps = Mathf.Clamp(speedMps * (scroll > 0f ? 1.25f : 0.8f), speedRangeMps.x, speedRangeMps.y);
            Vector3 move = Vector3.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move += Vector3.forward;
                if (kb.sKey.isPressed) move += Vector3.back;
                if (kb.dKey.isPressed) move += Vector3.right;
                if (kb.aKey.isPressed) move += Vector3.left;
                if (kb.eKey.isPressed) move += Vector3.up;
                if (kb.qKey.isPressed) move += Vector3.down;
            }
            if (pad != null)
            {
                Vector2 l = pad.leftStick.ReadValue();
                move += new Vector3(l.x, pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue(), l.y);
            }
            float speed = speedMps * (shift ? 4f : 1f);
            // Up and down are the world's; the rest follows the view.
            Vector3 world = transform.rotation * new Vector3(move.x, 0f, move.z) + Vector3.up * move.y;
            transform.position += world * (speed * dt);
        }

        void Chase(Vector2 look, float scroll, float dt)
        {
            Transform jet = _target.transform;
            if (scroll != 0f) distanceM = Mathf.Clamp(distanceM * (scroll > 0f ? 0.85f : 1.18f), distanceRangeM.x, distanceRangeM.y);
            _orbitYaw += look.x;
            _orbitPitch = Mathf.Clamp(_orbitPitch - look.y, -80f, 80f);
            // Ease the orbit back behind the jet when nobody holds it round.
            if (look == Vector2.zero)
            {
                float k = 1f - Mathf.Exp(-0.8f * dt);
                _orbitYaw = Mathf.LerpAngle(_orbitYaw, 0f, k);
                _orbitPitch = Mathf.Lerp(_orbitPitch, 0f, k);
            }
            Quaternion goal = Quaternion.LookRotation(jet.forward, jet.up) * Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
            bool snap = _target.Aircraft.Resets != _resets;   // a respawn cuts, it does not swing across the sky
            _resets = _target.Aircraft.Resets;
            transform.rotation = snap ? goal : Quaternion.Slerp(transform.rotation, goal, 1f - Mathf.Exp(-followRate * dt));
            transform.position = jet.position - transform.forward * distanceM + transform.up * heightM;
        }

        void ToggleFree()
        {
            if (_free)
            {
                AircraftView last = Find(_lastFollowed);
                Follow(last != null ? last : Next(1));   // not ??: a Unity object's null is not C#'s (aircraft-workflow trap 4)
                return;
            }
            _free = true;
            Vector3 e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        void Follow(AircraftView jet)
        {
            if (jet == null) return;
            _target = jet;
            _lastFollowed = jet.AircraftId;
            _free = false;
            _resets = -1;
            _orbitYaw = _orbitPitch = 0f;
        }

        AircraftView Find(int id)
        {
            foreach (AircraftView v in _jets) if (v.AircraftId == id && v.Aircraft != null) return v;
            return null;
        }

        /// <summary>The next jet in id order after the one followed, preferring jets still flying.</summary>
        AircraftView Next(int step)
        {
            if (_jets.Count == 0) return null;
            int at = _target != null ? _jets.IndexOf(_target) : -1;
            for (int k = 1; k <= _jets.Count; k++)
            {
                AircraftView v = _jets[((at + step * k) % _jets.Count + _jets.Count) % _jets.Count];
                if (v.Aircraft != null && v.Aircraft.State.Status == FlightStatus.Flying) return v;
            }
            return _jets[((at + step) % _jets.Count + _jets.Count) % _jets.Count];
        }

        void OnGUI()
        {
            if (runner == null || runner.Roster == null) return;
            float s = Mathf.Max(1f, Screen.height / 1080f);
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18f * s), fontStyle = FontStyle.Bold };
                _style.normal.textColor = textColour;
                _tag = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13f * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter };
            }

            string line;
            if (_free || _target == null || _target.Aircraft == null) line = $"FREE CAMERA · {speedMps:F0} M/S";
            else
            {
                var a = _target.Aircraft;
                string side = _sides.TryGetValue(a.Id, out var sd) ? sd.label + " · " : "";
                string state = a.State.Status == FlightStatus.Flying ? $"{a.State.Rounds} RDS · {a.State.HitPoints:F0} HP" : $"DOWN: {a.CrashReason}";
                line = $"FOLLOWING {a.Id} · {side}{runner.Roster.Describe(a.Id)} · {state}";
            }
            GUI.Label(new Rect(20f * s, 12f * s, 1200f * s, 30f * s), line, _style);
            GUI.Label(new Rect(20f * s, Screen.height - 34f * s, 1600f * s, 30f * s),
                      "TAB / ] NEXT JET · SHIFT+TAB / [ PREVIOUS · 1–9 JET · F FREE / FOLLOW · RMB LOOK · WASD QE FLY · SHIFT FAST · SCROLL SPEED/ZOOM · L TAGS", _tag);

            if (!showTags || _camera == null) return;
            foreach (AircraftView v in _jets)
            {
                if (v.Aircraft == null || v == Target) continue;
                Vector3 p = _camera.WorldToScreenPoint(v.transform.position + Vector3.up * 6f);
                if (p.z <= 0f) continue;
                var (label, colour) = _sides.TryGetValue(v.AircraftId, out var sd) ? sd : ("", textColour);
                bool down = v.Aircraft.State.Status != FlightStatus.Flying;
                _tag.normal.textColor = down ? new Color(colour.r, colour.g, colour.b, 0.4f) : colour;
                GUI.Label(new Rect(p.x - 80f * s, Screen.height - p.y - 22f * s, 160f * s, 20f * s), $"{label} {v.AircraftId}{(down ? " ✕" : "")}  {p.z / 1000f:F1} KM", _tag);
            }
            _tag.normal.textColor = textColour;
        }

        /// <summary>Sets the rig up from code (the scene builders).</summary>
        public void Configure(AirWingRunner flightRunner, int follow)
        {
            runner = flightRunner;
            startFollowing = follow;
        }
    }
}
