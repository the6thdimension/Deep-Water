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
        }

        [SerializeField] PilotProfileSO profile;
        [SerializeField] Task task = Task.Route;
        [SerializeField] GateCourseSO route;
        [Tooltip("The aircraft to formate on (Wingman).")]
        [SerializeField] int leaderId = 1;
        [Tooltip("Home (W47): the runway it goes to when ordered home or at bingo. Empty: it has no home.")]
        [SerializeField] FlatGround homeRunway;
        [Tooltip("The runway's name, for the rule log and the pick list.")]
        [SerializeField] string homeRunwayName = "09/27";
        [SerializeField] bool logRules = true;

        readonly RuleLog _log = new RuleLog();
        AviatorSeat _seat;

        public AviatorSeat Seat => _seat;
        public RuleLog Log => _log;

        void Start()
        {
            var view = GetComponent<AircraftView>();
            var runner = FindAnyObjectByType<AirWingRunner>();
            if (runner == null || runner.Flight == null || runner.Roster == null || view.Aircraft == null)
            {
                Debug.LogError("[RH Aviator] no Air Wing runner has built this jet; the AI pilot is not seated.", this);
                return;
            }
            if (profile == null || (task == Task.Route && route == null))
            {
                Debug.LogError("[RH Aviator] assign a pilot profile (and a route for the Route task); the AI pilot is not seated.", this);
                return;
            }
            PilotProfile p = profile.Bake();
            if (logRules) _log.Fired += f => Debug.Log($"[RH Aviator] {f}", this);
            int id = view.AircraftId;
            _seat = task == Task.Route
                ? new AviatorSeat(runner.Flight, view.Aircraft.Profile, p, route.Bake(), _log)
                : new AviatorSeat(runner.Flight, view.Aircraft.Profile, p, _log);
            if (task == Task.Wingman) _seat.JoinOn(id, leaderId);
            if (homeRunway != null)
                _seat.SetHome(new Strip(homeRunwayName, homeRunway.transform.position, homeRunway.transform.eulerAngles.y, homeRunway.LengthM, homeRunway.WidthM));
            runner.Roster.SetOwnSeat(id, _seat);
            string what = task == Task.Route ? $"flying {route.name} ({_seat.Route.Gates.Count} gates)" : task == Task.Wingman ? $"joining aircraft {leaderId}" : "holding course";
            string seated = runner.PlayerAircraftId == id ? "; the player flies it until the seat cycle hands it over" : "";
            Debug.Log($"[RH Aviator] aircraft {id}: pilot '{p.Name}' {what}{seated}{(homeRunway != null ? $"; home {homeRunwayName}" : "; no home runway")}.", this);
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
