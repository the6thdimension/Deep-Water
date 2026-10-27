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
        readonly List<AircraftView> _views = new List<AircraftView>();

        public FlightCore Flight => _core;
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
            ISeaSurface seaSurface = sea as ISeaSurface;
            if (sea != null && seaSurface == null)
                Debug.LogError($"[Air Wing] {sea.name} is not a Sea State surface (ISeaSurface); using the flat fallback.", this);
            if (sea is SeaStateDirector director && !director.IsReady)
            {
                Debug.LogError($"[Air Wing] The Sea State director on {sea.name} is not ready (no library?); using the flat fallback.", this);
                seaSurface = null;
            }
            var surface = new SceneSurface(seaSurface, FindObjectsByType<FlatGround>(FindObjectsInactive.Exclude), fallbackSurfaceHeightM);
            _core = new FlightCore(new SubstepClock(Time.fixedDeltaTime, sheet.SubstepsPerTick), surface, new PhysXContactProbe());

            _views.AddRange(FindObjectsByType<AircraftView>(FindObjectsInactive.Exclude).OrderBy(v => v.AircraftId));
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
                _core.Add(aircraft, SeatFor(view));
                view.Bind(aircraft);
                Debug.Log($"[Air Wing] aircraft {view.AircraftId} ({p.Name}, {view.Seat} seat): {string.Join("; ", aircraft.Levels)}", view);
            }
        }

        ISeat SeatFor(AircraftView view)
        {
            if (view.Seat == AircraftView.StartSeat.Player)
            {
                if (PlayerSeat != null) return PlayerSeat;
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
            for (int i = 0; i < _views.Count; i++) _views[i].BeforeTick();
            _core.Step();
            for (int i = 0; i < _views.Count; i++) _views[i].AfterTick();
        }
    }
}
