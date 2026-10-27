using UnityEngine;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Modules
{
    /// <summary>
    /// Puts an RH Aviator pilot in an Air Wing jet's seat (P2). On Start, after Air Wing's runner has built the jets
    /// (the runner runs first, order −50), it hands this jet's seat to an <see cref="AviatorSeat"/> flying
    /// <see cref="route"/>. Air Wing never learns about it (W5): it only sees a seat. Each rule that fires is printed
    /// to the console (Observable).
    /// </summary>
    [RequireComponent(typeof(AircraftView))]
    public sealed class AviatorPilot : MonoBehaviour
    {
        [SerializeField] PilotProfileSO profile;
        [SerializeField] GateCourseSO route;
        [SerializeField] bool logRules = true;

        readonly RuleLog _log = new RuleLog();
        AviatorSeat _seat;

        public AviatorSeat Seat => _seat;
        public RuleLog Log => _log;

        void Start()
        {
            var view = GetComponent<AircraftView>();
            var runner = FindAnyObjectByType<AirWingRunner>();
            if (runner == null || runner.Flight == null || view.Aircraft == null)
            {
                Debug.LogError("[RH Aviator] no Air Wing runner has built this jet; the AI pilot is not seated.", this);
                return;
            }
            if (profile == null || route == null)
            {
                Debug.LogError("[RH Aviator] assign a pilot profile and a route; the AI pilot is not seated.", this);
                return;
            }
            PilotProfile p = profile.Bake();
            if (logRules) _log.Fired += f => Debug.Log($"[RH Aviator] {f}", this);
            _seat = new AviatorSeat(runner.Flight, view.Aircraft.Profile, p, route.Bake(), _log);
            runner.Flight.SetSeat(view.AircraftId, _seat);
            Debug.Log($"[RH Aviator] aircraft {view.AircraftId}: pilot '{p.Name}' flying {route.name} ({_seat.Route.Gates.Count} gates).", this);
        }
    }
}
