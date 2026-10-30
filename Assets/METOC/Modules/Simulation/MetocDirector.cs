using Unity.Profiling;
using UnityEngine;

namespace DeepWater.Metoc
{
    /// <summary>
    /// Pumps METOC's reckoned core on the fixed tick (P2, E47), before the sea (−200) so the sea can read this tick's
    /// report. Everything that changes METOC goes in through <see cref="Issue"/>, and so into the order log.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class MetocDirector : MonoBehaviour
    {
        static readonly ProfilerMarker StepMarker = new ProfilerMarker("METOC.Step");

        [SerializeField] MetocScenarioSO scenario;
        [SerializeField] IlluminationProfileSO illumination;

        public MetocSimulation Simulation { get; private set; }
        public bool Ready => Simulation != null;
        public MetocReport Report => Simulation.Report;

        void Awake()
        {
            if (scenario == null || illumination == null)
            {
                Debug.LogError($"[METOC] {name}: the director needs a scenario and an illumination profile; METOC is off.", this);
                enabled = false;
                return;
            }
            Simulation = new MetocSimulation(scenario.ToScenario(), illumination.ToProfile());
            Debug.Log($"[METOC] Started at {Simulation.Report.TimeText}, {scenario.latitudeDeg:0.###}°, {scenario.longitudeDeg:0.###}°, day speed {scenario.daySpeed:0.###}× ({scenario.placeProvenance})", this);
        }

        void FixedUpdate()
        {
            using (StepMarker.Auto()) Simulation.Step(Time.fixedDeltaTime);
        }

        /// <summary>Queue an order for the next fixed step; it is logged when applied, refused or not.</summary>
        public void Issue(in MetocOrder order)
        {
            if (Simulation == null)
            {
                Debug.LogWarning($"[METOC] Order dropped, the director is not running: {order}", this);
                return;
            }
            Simulation.Issue(order);
        }
    }
}
