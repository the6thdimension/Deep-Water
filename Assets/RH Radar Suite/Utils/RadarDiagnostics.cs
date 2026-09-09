using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RHRadarSuite
{
    /// <summary>
    /// Scene-view diagnostic gizmos for a radar, organized as three toggleable
    /// layers answering three questions (the Stimson layers — see ROADMAP):
    ///   Coverage      — what COULD this radar see? (range, sector, elevation limits)
    ///   Activity      — where is it looking RIGHT NOW? (sweep wedge + trail)
    ///   Measurements  — what DID it see, and how wrong? (measured pos, truth ghost,
    ///                   velocity, lost-contact fade-out)
    /// Coverage draws in edit mode too, from the assigned profile, so a radar can
    /// be aimed before entering play mode.
    /// </summary>
    [AddComponentMenu("RH Radar Suite/Radar Diagnostics")]
    [RequireComponent(typeof(RadarSuiteController))]
    public class RadarDiagnostics : MonoBehaviour
    {
        [Header("Layers")]
        [Tooltip("Draw the coverage volume: range ring, sector, elevation limits")]
        public bool showCoverage = true;

        [Tooltip("Draw the live sweep wedge and its recent trail (play mode)")]
        public bool showActivity = true;

        [Tooltip("Draw contacts: measured position, truth ghost, velocity, lost fade")]
        public bool showMeasurements = true;

        [Tooltip("Draw text labels (radar summary, per-contact range/bearing/signal)")]
        public bool showLabels = true;

        [Header("Colors")]
        public Color coverageColor = new Color(0.20f, 0.80f, 1.00f, 0.55f);
        public Color elevationColor = new Color(0.20f, 1.00f, 0.70f, 0.45f);
        public Color sweepColor = new Color(1.00f, 0.85f, 0.20f, 0.90f);
        public Color contactColor = new Color(1.00f, 0.25f, 0.25f, 1.00f);
        public Color truthColor = new Color(1.00f, 1.00f, 1.00f, 0.45f);
        public Color velocityColor = new Color(1.00f, 0.55f, 0.10f, 1.00f);
        public Color lostColor = new Color(0.60f, 0.60f, 0.60f, 0.90f);

        [Header("Tuning")]
        [Tooltip("Seconds a lost contact stays visible while fading out")]
        [Range(0.5f, 15f)]
        public float lostContactFadeSeconds = 4f;

        [Tooltip("How many recent sweep positions to keep as a fading trail")]
        [Range(0, 60)]
        public int sweepTrailLength = 24;

        [Tooltip("Contact marker radius as a fraction of detection range")]
        [Range(0.001f, 0.05f)]
        public float contactMarkerScale = 0.008f;

        [Tooltip("Seconds of travel shown by the velocity vector")]
        [Range(0.5f, 10f)]
        public float velocityVectorSeconds = 2f;

        private RadarSuiteController controller;

        private struct LostContact
        {
            public Vector3 Position;
            public float Time;
            public string Name;
        }

        private struct SweepSample
        {
            public float BodyAngleDeg;
            public float Time;
        }

        private readonly List<LostContact> lostContacts = new List<LostContact>();
        private readonly Queue<SweepSample> sweepTrail = new Queue<SweepSample>();
        private float lastSweepSampleTime;

        private void Awake()
        {
            controller = GetComponent<RadarSuiteController>();
        }

        private void OnEnable()
        {
            if (controller == null) controller = GetComponent<RadarSuiteController>();
            if (controller != null) controller.OnContactLost += HandleContactLost;
        }

        private void OnDisable()
        {
            if (controller != null) controller.OnContactLost -= HandleContactLost;
        }

        private void HandleContactLost(RadarContact contact)
        {
            if (contact == null) return;
            lostContacts.Add(new LostContact
            {
                Position = contact.Position,
                Time = Time.time,
                Name = contact.TargetName
            });
        }

        private void Update()
        {
            // Age out faded lost-contact markers
            for (int i = lostContacts.Count - 1; i >= 0; i--)
            {
                if (Time.time - lostContacts[i].Time > lostContactFadeSeconds)
                {
                    lostContacts.RemoveAt(i);
                }
            }

            // Sample the sweep for the trail
            if (showActivity && controller != null && controller.IsActive &&
                controller.ActiveModule != null &&
                controller.ActiveModule.TryGetScanState(out float angle, out _))
            {
                if (Time.time - lastSweepSampleTime > 0.05f)
                {
                    sweepTrail.Enqueue(new SweepSample { BodyAngleDeg = angle, Time = Time.time });
                    while (sweepTrail.Count > Mathf.Max(1, sweepTrailLength))
                    {
                        sweepTrail.Dequeue();
                    }
                    lastSweepSampleTime = Time.time;
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (controller == null) controller = GetComponent<RadarSuiteController>();
            if (controller == null) return;

            float range = CoverageRange();
            if (range <= 0f) return;

            if (showCoverage) DrawCoverage(range);
            if (showActivity && Application.isPlaying) DrawActivity(range);
            if (showMeasurements && Application.isPlaying) DrawMeasurements(range);

#if UNITY_EDITOR
            if (showLabels) DrawRadarLabel(range);
#endif
        }

        // ----- Coverage: what could I see? -------------------------------------

        private float CoverageRange()
        {
            RadarProfileSO profile = controller.Profile;
            return profile != null ? profile.maxDetectionRangeM : controller.MaxDetectionRange;
        }

        private void DrawCoverage(float range)
        {
            RadarProfileSO profile = controller.Profile;
            Vector3 origin = transform.position;
            bool fullRotation = profile == null || profile.fullRotation;
            float sectorCenter = profile != null ? profile.sectorCenterDeg : 0f;
            float sectorSize = profile != null ? profile.sectorSizeDeg : 90f;

            Gizmos.color = coverageColor;

            if (fullRotation)
            {
                DrawHorizontalCircle(origin, range);
            }
            else
            {
                // Sector arc + edge rays, body-relative
                DrawHorizontalArc(origin, range, sectorCenter - sectorSize * 0.5f, sectorCenter + sectorSize * 0.5f);
                Gizmos.DrawLine(origin, origin + RadarMath.BodyDirection(transform, sectorCenter - sectorSize * 0.5f) * range);
                Gizmos.DrawLine(origin, origin + RadarMath.BodyDirection(transform, sectorCenter + sectorSize * 0.5f) * range);
                // Faint full ring for context
                Gizmos.color = new Color(coverageColor.r, coverageColor.g, coverageColor.b, coverageColor.a * 0.25f);
                DrawHorizontalCircle(origin, range);
            }

            // Elevation limits, drawn as a vertical wedge in the boresight plane
            if (profile != null)
            {
                Gizmos.color = elevationColor;
                float boresight = fullRotation ? 0f : sectorCenter;
                DrawElevationWedge(origin, range, boresight, profile.minElevationDeg, profile.maxElevationDeg);
            }
        }

        private void DrawElevationWedge(Vector3 origin, float range, float bodyAngleDeg, float minElev, float maxElev)
        {
            Vector3 lower = RadarMath.BodyDirection(transform, bodyAngleDeg, minElev);
            Vector3 upper = RadarMath.BodyDirection(transform, bodyAngleDeg, maxElev);

            Gizmos.DrawLine(origin, origin + lower * range);
            Gizmos.DrawLine(origin, origin + upper * range);

            // Arc between the two elevation limits
            int segments = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(maxElev - minElev) / 5f));
            Vector3 prev = origin + lower * range;
            for (int i = 1; i <= segments; i++)
            {
                float e = Mathf.Lerp(minElev, maxElev, (float)i / segments);
                Vector3 next = origin + RadarMath.BodyDirection(transform, bodyAngleDeg, e) * range;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        // ----- Activity: where am I looking now? -------------------------------

        private void DrawActivity(float range)
        {
            if (!controller.IsActive || controller.ActiveModule == null) return;
            if (!controller.ActiveModule.TryGetScanState(out float angle, out float beamWidth)) return;

            Vector3 origin = transform.position;

            // Fading trail of recent sweep positions
            foreach (SweepSample sample in sweepTrail)
            {
                float age = Time.time - sample.Time;
                float alpha = Mathf.Clamp01(1f - age / 1.5f) * 0.35f;
                if (alpha <= 0.01f) continue;
                Gizmos.color = new Color(sweepColor.r, sweepColor.g, sweepColor.b, alpha);
                Gizmos.DrawLine(origin, origin + RadarMath.BodyDirection(transform, sample.BodyAngleDeg) * range);
            }

            // Current beam wedge: center ray + beam-edge rays + arc between them
            Gizmos.color = sweepColor;
            Gizmos.DrawLine(origin, origin + RadarMath.BodyDirection(transform, angle) * range);

            float half = Mathf.Max(0.5f, beamWidth * 0.5f);
            Color edge = new Color(sweepColor.r, sweepColor.g, sweepColor.b, sweepColor.a * 0.5f);
            Gizmos.color = edge;
            Gizmos.DrawLine(origin, origin + RadarMath.BodyDirection(transform, angle - half) * range);
            Gizmos.DrawLine(origin, origin + RadarMath.BodyDirection(transform, angle + half) * range);
            DrawHorizontalArc(origin, range, angle - half, angle + half);
        }

        // ----- Measurements: what did I see, and how wrong? --------------------

        private void DrawMeasurements(float range)
        {
            Vector3 origin = transform.position;
            float markerSize = Mathf.Max(0.25f, range * contactMarkerScale);

            if (controller.IsActive)
            {
                foreach (RadarContact contact in controller.ActiveContacts)
                {
                    if (contact == null || !contact.IsActive) continue;

                    // Measured position + line of bearing
                    Gizmos.color = contactColor;
                    Gizmos.DrawSphere(contact.Position, markerSize);
                    Gizmos.DrawLine(origin, contact.Position);

                    // Velocity vector (where it will be in velocityVectorSeconds)
                    if (contact.Speed > 0.1f)
                    {
                        Gizmos.color = velocityColor;
                        Gizmos.DrawLine(contact.Position, contact.Position + contact.Velocity * velocityVectorSeconds);
                    }

                    // Truth ghost: how wrong is the measurement?
                    if (contact.Target != null)
                    {
                        Vector3 truth = contact.Target.transform.position;
                        Gizmos.color = truthColor;
                        Gizmos.DrawWireCube(truth, Vector3.one * markerSize * 1.5f);
                        Gizmos.DrawLine(contact.Position, truth);
                    }

#if UNITY_EDITOR
                    if (showLabels)
                    {
                        Handles.Label(
                            contact.Position + Vector3.up * markerSize * 3f,
                            $"{contact.TargetName}\nRNG {contact.Range:F0} m  BRG {contact.Azimuth:F0}°  EL {contact.Elevation:F0}°\nSIG {contact.SignalStrength:P0}");
                    }
#endif
                }
            }

            // Lost contacts fade out as X markers
            foreach (LostContact lost in lostContacts)
            {
                float alpha = Mathf.Clamp01(1f - (Time.time - lost.Time) / lostContactFadeSeconds);
                if (alpha <= 0.01f) continue;
                Gizmos.color = new Color(lostColor.r, lostColor.g, lostColor.b, lostColor.a * alpha);
                float s = markerSize * 1.5f;
                Gizmos.DrawLine(lost.Position + new Vector3(-s, 0f, -s), lost.Position + new Vector3(s, 0f, s));
                Gizmos.DrawLine(lost.Position + new Vector3(-s, 0f, s), lost.Position + new Vector3(s, 0f, -s));
            }
        }

        // ----- Shared drawing helpers ------------------------------------------

        private void DrawHorizontalCircle(Vector3 origin, float radius)
        {
            DrawHorizontalArc(origin, radius, 0f, 360f);
        }

        private void DrawHorizontalArc(Vector3 origin, float radius, float fromBodyDeg, float toBodyDeg)
        {
            float span = toBodyDeg - fromBodyDeg;
            int segments = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(span) / 5f));
            Vector3 prev = origin + RadarMath.BodyDirection(transform, fromBodyDeg) * radius;
            for (int i = 1; i <= segments; i++)
            {
                float a = fromBodyDeg + span * i / segments;
                Vector3 next = origin + RadarMath.BodyDirection(transform, a) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

#if UNITY_EDITOR
        private void DrawRadarLabel(float range)
        {
            RadarProfileSO profile = controller.Profile;
            string title = profile != null
                ? $"{profile.role}  ·  {range:F0} m  ·  EL {profile.minElevationDeg:F0}°..{profile.maxElevationDeg:F0}°"
                : $"{controller.CurrentLOD}  ·  {range:F0} m";

            string status = Application.isPlaying
                ? (controller.IsActive ? $"ACTIVE · {controller.ActiveContacts.Count} contact(s)" : "STANDBY")
                : "EDIT PREVIEW";

            Handles.Label(transform.position + Vector3.up * Mathf.Max(2f, range * 0.01f), $"{title}\n{status}");
        }
#endif
    }
}
