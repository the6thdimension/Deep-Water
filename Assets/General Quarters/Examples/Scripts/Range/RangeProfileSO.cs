using UnityEngine;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// The General Quarters range: the patrol box the AI captain steers when nothing threatens (rule C1), the raids
    /// on offer, and the camera's window in the TAO seat. AUTHORED: an exercise area, not a real one.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRange", menuName = "Deep Water/General Quarters/Range Profile", order = 30)]
    public class RangeProfileSO : ScriptableObject
    {
        [Tooltip("Patrol box corners (world x/z, metres), steered in order. The route repeats them for the laps below.")]
        public Vector2[] patrolCorners = { new Vector2(-600f, -900f), new Vector2(-600f, 7000f), new Vector2(5000f, 7000f), new Vector2(5000f, -900f) };
        [Tooltip("Laps of the box in the route (the Helm stops answering at the route's end).")]
        [Min(1)] public int patrolLaps = 6;

        [Tooltip("Raids the range offers, in the order the picker lists them. The first is the default.")]
        public RaidScenarioSO[] raids = new RaidScenarioSO[0];

        [Tooltip("Time compression steps, cycled in order.")]
        public float[] timeScales = { 1f, 2f, 4f };

        [Tooltip("The 3D view's window in the TAO seat, as a viewport rect (0–1, origin bottom left). The TAO screen leaves it clear.")]
        public Rect taoCameraViewport = new Rect(0.64f, 0.5f, 0.36f, 0.44f);

        [Tooltip("Seconds the camera stays on a target after it is destroyed, before it goes back to the ship.")]
        public float cameraLingerS = 2.5f;

        [TextArea(3, 10)] public string provenance;

        /// <summary>The patrol route's waypoints: the box, lap after lap, closing back on its first corner.</summary>
        public Vector2[] RouteWaypoints()
        {
            int n = patrolCorners.Length;
            var w = new Vector2[n * patrolLaps + 1];
            for (int lap = 0; lap < patrolLaps; lap++)
                for (int i = 0; i < n; i++) w[lap * n + i] = patrolCorners[i];
            w[w.Length - 1] = patrolCorners[0];
            return w;
        }
    }
}
