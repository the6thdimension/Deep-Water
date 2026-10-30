using UnityEngine;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.World;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Modules
{
    /// <summary>
    /// Puts an RH Aviator pilot in an Air Wing jet (P2). On Start, after Air Wing's runner has built the jets (the
    /// runner runs first, order −50), it gives the runner's seat roster an <see cref="AviatorSeat"/> as this jet's own
    /// seat (W42): it flies the jet whenever the player is not in it, from the start or the moment the player cycles
    /// away. Air Wing never learns about it (W5): it only sees a seat. Each rule that fires is printed to the console
    /// (Observable).
    /// </summary>
    [RequireComponent(typeof(AircraftView))]
    public sealed class AviatorPilot : MonoBehaviour
    {
        public enum Task
        {
            /// <summary>Fly <see cref="route"/>, then hold course.</summary>
            Route,
            /// <summary>Join <see cref="leaderId"/>'s wing in fingertip (W40).</summary>
            Wingman,
            /// <summary>Hold the height and speed it has.</summary>
            HoldCourse,
            /// <summary>Fight on <see cref="side"/> in the scene's <see cref="AviatorFight"/> (slice 3d); a blue wingman rejoins <see cref="leaderId"/>'s wing when clear.</summary>
            Fight,
        }

        [SerializeField] PilotProfileSO profile;
        [SerializeField] Task task = Task.Route;
        [SerializeField] GateCourseSO route;
        [Tooltip("The aircraft to formate on (Wingman).")]
        [SerializeField] int leaderId = 1;
        [Tooltip("The slot in the flight: 0 the profile's, 1 left wing, 2 right wing, 3 outside the right wing.")]
        [SerializeField] int slotPosition = 0;
        [Tooltip("The side it fights on (Fight).")]
        [SerializeField] Side side = Side.Red;
        [Tooltip("Home (W47): the runway it goes to when ordered home or at bingo. Empty: it has no home.")]
        [SerializeField] FlatGround homeRunway;
        [Tooltip("The runway's name, for the rule log and the pick list.")]
        [SerializeField] string homeRunwayName = "09/27";
        [SerializeField] bool logRules = true;

        readonly RuleLog _log = new RuleLog();
        AviatorSeat _seat;
        bool _started, _logging;

        public AviatorSeat Seat => _seat;
        /// <summary>The side it fights on, or None when its task is not Fight.</summary>
        public Side FightSide => task == Task.Fight ? side : Side.None;
        public RuleLog Log => _log;

        void Start()
        {
            _started = true;
            if (!SeatIt()) return;
            // Enabled after the fight was ordered (a brain changed in play, A23): it joins the fight itself.
            if (task == Task.Fight)
            {
                AviatorFight fight = FindAnyObjectByType<AviatorFight>();
                if (fight != null && fight.Ordered) fight.Enlist(this);
            }
        }

        /// <summary>
        /// Seats it again, as its settings are now (A23: a brain changed in play, a rule pilot's skill or the jet handed
        /// back from a learned pilot), in the fight when it fights. False before its Start (which seats it) or when it
        /// cannot be seated.
        /// </summary>
        public bool Reseat()
        {
            if (!_started) return false;
            if (!SeatIt()) return false;
            if (task == Task.Fight)
            {
                AviatorFight fight = FindAnyObjectByType<AviatorFight>();
                if (fight != null && fight.Ordered) fight.Enlist(this);
            }
            return true;
        }

        bool SeatIt()
        {
            var view = GetComponent<AircraftView>();
            var runner = FindAnyObjectByType<AirWingRunner>();
            if (runner == null || runner.Flight == null || runner.Roster == null || view.Aircraft == null)
            {
                Debug.LogError("[RH Aviator] no Air Wing runner has built this jet; the AI pilot is not seated.", this);
                return false;
            }
            if (profile == null || (task == Task.Route && route == null))
            {
                Debug.LogError("[RH Aviator] assign a pilot profile (and a route for the Route task); the AI pilot is not seated.", this);
                return false;
            }
            PilotProfile p = profile.Bake();
            if (logRules && !_logging) { _log.Fired += f => Debug.Log($"[RH Aviator] {f}", this); _logging = true; }
            int id = view.AircraftId;
            _seat = task == Task.Route
                ? new AviatorSeat(runner.Flight, view.Aircraft.Profile, p, route.Bake(), _log)
                : new AviatorSeat(runner.Flight, view.Aircraft.Profile, p, _log);
            if (task == Task.Wingman) _seat.JoinOn(id, leaderId, slotPosition);
            if (homeRunway != null)
                _seat.SetHome(new Strip(homeRunwayName, homeRunway.transform.position, homeRunway.transform.eulerAngles.y, homeRunway.LengthM, homeRunway.WidthM));
            runner.Roster.SetOwnSeat(id, _seat);
            if (task == Task.Fight && side == Side.Blue && runner.PlayerAircraftId != 0 && runner.PlayerAircraftId != id) _seat.JoinOn(id, runner.PlayerAircraftId, slotPosition);
            string what = task == Task.Fight ? $"fighting for {side}" : task == Task.Route ? $"flying {route.name} ({_seat.Route.Gates.Count} gates)" : task == Task.Wingman ? $"joining aircraft {leaderId}" : "holding course";
            string seated = runner.PlayerAircraftId == id ? "; the player flies it until the seat cycle hands it over" : "";
            Debug.Log($"[RH Aviator] aircraft {id}: pilot '{p.Name}' {what}{seated}{(homeRunway != null ? $"; home {homeRunwayName}" : "; no home runway")}.", this);
            return true;
        }

        /// <summary>
        /// Sets the pilot up to fight for <paramref name="fightSide"/> as <paramref name="pilot"/> (a training squadron's
        /// pilot setting, A15). Before play: it takes effect when the pilot seats itself in Start.
        /// </summary>
        /// <returns>True when anything changed (so an editor caller marks the scene dirty only then).</returns>
        public bool ConfigureFight(PilotProfileSO pilot, Side fightSide)
        {
            if (profile == pilot && task == Task.Fight && side == fightSide) return false;
            profile = pilot;
            task = Task.Fight;
            side = fightSide;
            return true;
        }

        /// <summary>The player's order (A3, <see cref="AviatorOrders"/>): go home and land. Refused without a home runway.</summary>
        public bool GoHome()
        {
            if (_seat == null || !_seat.HasHome) return false;
            if (_seat.Mode == PilotMode.Home || _seat.Mode == PilotMode.Landed) return false;
            _seat.GoHome(GetComponent<AircraftView>().AircraftId);
            return true;
        }
    }
}
