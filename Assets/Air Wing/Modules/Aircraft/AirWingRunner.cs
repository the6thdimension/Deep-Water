using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Modules.Input;
using DeepWater.AirWing.Modules.World;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Sea.Core;

namespace DeepWater.AirWing.Modules.Aircraft
{
    /// <summary>
    /// Runs the flight core in a scene (P2, BLUEPRINT §3.2). On Start it builds every <see cref="AircraftView"/>'s
    /// aircraft from the profile and the fidelity sheet, logs each part's level and any fallback (C14), and seats it.
    /// Each FixedUpdate it steps the core once. Execution order −50: after the player seat's frame (−60), after the
    /// sea (−200), before anything that reads the jets.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class AirWingRunner : MonoBehaviour
    {
        [SerializeField] AircraftProfileSO profile;
        [SerializeField] FidelitySheetSO fidelitySheet;
        [Tooltip("The player's seat; aircraft set to the Player seat fly from it.")]
        [SerializeField] PlayerSeatInput playerSeat;
        [Tooltip("The Sea State director (its ISeaSurface is the truth feed's sea). Empty: a flat surface at the fallback height.")]
        [SerializeField] MonoBehaviour sea;
        [SerializeField] float fallbackSurfaceHeightM = 0f;

        FlightCore _core;
        SeatRoster _roster;
        readonly List<AircraftView> _views = new List<AircraftView>();

        public FlightCore Flight => _core;
        /// <summary>Who flies what (W42): RH Aviator gives each jet its own seat here; the cycle key moves the player.</summary>
        public SeatRoster Roster => _roster;
        /// <summary>The aircraft the player flies (0: none). The camera and the HUD follow it.</summary>
        public int PlayerAircraftId => _roster != null ? _roster.PlayerAircraftId : 0;
        public IReadOnlyList<AircraftView> Views => _views;
        public PlayerSeat PlayerSeat => playerSeat != null ? playerSeat.Seat : null;

        void Start()
        {
            if (profile == null || fidelitySheet == null)
            {
                Debug.LogError($"[Air Wing] {name}: assign the aircraft profile and the fidelity sheet.", this);
                enabled = false;
                return;
            }
            AircraftProfile p = profile.Bake();
            FidelitySheet sheet = fidelitySheet.Bake();
            PlayerSeat?.UseControls(p.Controls);   // the level hold's stick map (W54)
            ISeaSurface seaSurface = sea as ISeaSurface;
            if (sea != null && seaSurface == null)
                Debug.LogError($"[Air Wing] {sea.name} is not a Sea State surface (ISeaSurface); using the flat fallback.", this);
            if (sea is SeaStateDirector director && !director.IsReady)
            {
                Debug.LogError($"[Air Wing] The Sea State director on {sea.name} is not ready (no library?); using the flat fallback.", this);
                seaSurface = null;
            }
            var surface = new SceneSurface(seaSurface, FindObjectsByType<FlatGround>(FindObjectsInactive.Exclude), fallbackSurfaceHeightM);
            _core = new FlightCore(new SubstepClock(Time.fixedDeltaTime, sheet.SubstepsPerTick), surface, new PhysXContactProbe(), sheet.Seed);
            if (p.Gun.IsSet && FindAnyObjectByType<World.TracerView>() == null) gameObject.AddComponent<World.TracerView>().Use(this);   // the guns show without a scene change

            _views.AddRange(FindObjectsByType<AircraftView>(FindObjectsInactive.Exclude).OrderBy(v => v.AircraftId));
            int playerAircraft = 0;
            foreach (AircraftView view in _views)
            {
                Transform t = view.transform;
                float fuel = view.StartFuelFraction * p.Airframe.InternalFuelKg;
                bool onRunway = view.StartingMode == AircraftView.StartMode.Runway && view.StartRunway != null;
                var start = onRunway
                    ? AircraftFactory.GroundStart(p, sheet, view.StartRunway.TakeOffPoint(out float runwayHeading), runwayHeading, fuel)
                    : AircraftFactory.LevelStart(p, sheet, t.position, t.eulerAngles.y, view.StartSpeedMps, fuel, view.HoldThrottle);
                if (onRunway && view.Seat == AircraftView.StartSeat.Player && PlayerSeat != null)
                    PlayerSeat.SetStart(PilotSwitches.GearDown, 0f);   // gear down until the pilot raises it, engines at idle
                Core.Aircraft aircraft = AircraftFactory.Build(view.AircraftId, p, sheet, start);
                ISeat seat = SeatFor(view);
                _core.Add(aircraft, seat);
                if (seat is PlayerSeat)
                {
                    if (playerAircraft != 0) Debug.LogWarning($"[Air Wing] aircraft {view.AircraftId} also asks for the player seat; the player starts in {playerAircraft} and it holds.", view);
                    else playerAircraft = view.AircraftId;
                }
                view.Bind(aircraft);
                Debug.Log($"[Air Wing] aircraft {view.AircraftId} ({p.Name}, {view.Seat} seat): {string.Join("; ", aircraft.Levels)}", view);
            }
            // Explosions show without a scene change too, once any jet's visuals carry one.
            if (_views.Any(v => v.Visuals != null && v.Visuals.explosion != null) && FindAnyObjectByType<World.ExplosionView>() == null)
                gameObject.AddComponent<World.ExplosionView>().Use(this);
            // And the game missiles (W65), on their pylons and in flight, once any jet carries them and has their shape.
            if (p.Missiles.IsSet && _views.Any(v => v.Visuals != null && v.Visuals.missileModel != null) && FindAnyObjectByType<World.MissileView>() == null)
                gameObject.AddComponent<World.MissileView>().Use(this);
            _roster = new SeatRoster(_core, PlayerSeat, playerAircraft);
            _roster.Moved += (left, taken) => Debug.Log($"[Air Wing] tick {_core.Tick}: the player left aircraft {left} ({_roster.Describe(left)}) and flies {taken}.", this);
        }

        ISeat SeatFor(AircraftView view)
        {
            if (view.Seat == AircraftView.StartSeat.Player)
            {
                if (PlayerSeat != null && !_core.Aircraft.Any(a => _core.SeatOf(a.Id) == PlayerSeat)) return PlayerSeat;
                Debug.LogWarning($"[Air Wing] aircraft {view.AircraftId} wants the player seat, but none is assigned: it holds its start throttle.", view);
            }
            // A held jet on the runway waits there: idle, brakes on, gear down.
            if (view.StartingMode == AircraftView.StartMode.Runway)
                return new HoldSeat(new PilotInputs { Switches = PilotSwitches.GearDown, Brakes = true });
            return new HoldSeat(new PilotInputs { Throttle = view.HoldThrottle });
        }

        void FixedUpdate()
        {
            if (_core == null) return;
            // The seat cycle moves the player between ticks (W42): the next tick is the first one flown from the new seat.
            for (int n = playerSeat != null ? playerSeat.TakeCyclePresses() : 0; n > 0; n--) _roster.Cycle();
            for (int i = 0; i < _views.Count; i++) _views[i].BeforeTick();
            _core.Step();
            for (int i = 0; i < _views.Count; i++) _views[i].AfterTick();
        }
    }
}
