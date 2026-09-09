using System.Collections.Generic;
using UnityEngine;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;

namespace GuidedFury.Examples
{
    /// <summary>
    /// In-play telemetry overlay that tracks per-missile outcomes and lets you see, in real
    /// time, how the LODs are performing on identical scenarios. Sits at the bottom-left
    /// (clear of HUD top-left and ControlPanel top-right).
    ///
    /// For each MissileBehaviour found in the scene, the tracker maintains:
    /// - Time of flight at outcome (impact or fail)
    /// - Closest approach distance ("miss distance") to its bound target
    /// - Peak speed observed
    /// - Mass / fuel at outcome (proxy for energy retention)
    /// - Outcome label (HIT / FAIL / ACTIVE)
    ///
    /// **What this is good for:** seeing quantitatively that L3 takes longer to settle on a
    /// crossing target than L1 does — or that L0 hits "perfectly" only because it ignores
    /// physics. The eye can mis-rank LODs visually; the numbers don't.
    ///
    /// **Lifecycle:** tracked rows persist until the panel is cleared. New launches add new
    /// rows. Rows for terminated missiles freeze their stats so you can review post-flight.
    /// </summary>
    public sealed class LodComparisonStats : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("Pixel size of the overlay anchor (from bottom-left).")]
        [SerializeField] private Vector2 marginFromBottomLeft = new Vector2(12f, 12f);

        [Tooltip("Width of the stats panel.")]
        [SerializeField] private float width = 560f;

        [Tooltip("Maximum number of rows to retain. Oldest discarded first.")]
        [SerializeField] private int maxRows = 12;

        [Header("Behavior")]
        [Tooltip("Auto-clear old rows when a new salvo / LOD compare runs.")]
        [SerializeField] private bool autoClearOnNewRun = false;

        private readonly List<Row> rows = new List<Row>();
        // Per-tracked-missile state so we can update peak-speed / miss-distance each frame.
        private readonly Dictionary<int, Row> rowsByMissileId = new Dictionary<int, Row>();

        private GUIStyle headerStyle;
        private GUIStyle rowStyle;
        private float lastScan;
        private const float ScanIntervalS = 0.5f;
        private MissileBehaviour[] cachedScene;

        // ============================================================
        // Public API
        // ============================================================

        public void Clear()
        {
            rows.Clear();
            rowsByMissileId.Clear();
        }

        // ============================================================
        // Lifecycle
        // ============================================================

        private void Update()
        {
            if (Time.unscaledTime - lastScan >= ScanIntervalS)
            {
                cachedScene = FindObjectsByType<MissileBehaviour>(FindObjectsSortMode.None);
                lastScan = Time.unscaledTime;
            }

            // Per-frame update of tracked rows. Cheap — N is small.
            if (cachedScene == null) return;
            foreach (var b in cachedScene)
            {
                if (b == null || !b.IsLaunched) continue;
                int id = b.GetInstanceID();
                if (!rowsByMissileId.TryGetValue(id, out var row))
                {
                    if (rows.Count >= maxRows) EvictOldest();
                    row = new Row { Missile = b, Name = b.name };
                    rowsByMissileId[id] = row;
                    rows.Add(row);
                }
                UpdateRow(row);
            }
        }

        private void UpdateRow(Row r)
        {
            var b = r.Missile;
            if (b == null) return;

            MissileState s = b.GetState();
            r.Lod = b.Entity?.Integrator?.Lod ?? MissileLod.L0_Kinematic;
            r.PeakSpeed = Mathf.Max(r.PeakSpeed, s.Velocity.magnitude);

            if (b.Target != null)
            {
                float range = Vector3.Distance(s.Position, b.Target.position);
                if (range < r.MissDistance) r.MissDistance = range;
            }

            // Outcome bookkeeping.
            if (!r.IsTerminal)
            {
                if (s.Phase == MissilePhase.Detonated)
                {
                    r.IsTerminal = true;
                    r.OutcomeToF = s.TimeOfFlight;
                    r.OutcomeMass = s.Mass;
                    r.OutcomeFuel = s.Fuel;
                    // Heuristic: if missile detonated within 1.5× of its proximity-fuze
                    // radius from a bound target, count it as a hit; otherwise it detonated
                    // from a non-target event (lifetime expiry mid-flight, command detonate).
                    float hitRadius = b.Entity != null
                        ? b.Entity.Profile.FuzeProximityRadiusM * 1.5f
                        : 10f;
                    r.HitOutcome = b.Target != null && r.MissDistance <= hitRadius
                                   ? Outcome.Hit
                                   : Outcome.Detonated;
                }
                else if (s.Phase == MissilePhase.Failed)
                {
                    r.IsTerminal = true;
                    r.OutcomeToF = s.TimeOfFlight;
                    r.OutcomeMass = s.Mass;
                    r.OutcomeFuel = s.Fuel;
                    r.HitOutcome = Outcome.Failed;
                }
                else
                {
                    r.OutcomeToF = s.TimeOfFlight;
                    r.OutcomeMass = s.Mass;
                    r.OutcomeFuel = s.Fuel;
                }
            }
        }

        private void EvictOldest()
        {
            if (rows.Count == 0) return;
            var oldest = rows[0];
            rows.RemoveAt(0);
            if (oldest.Missile != null)
                rowsByMissileId.Remove(oldest.Missile.GetInstanceID());
        }

        // ============================================================
        // Drawing
        // ============================================================

        private void OnGUI()
        {
            EnsureStyles();
            if (rows.Count == 0) return;

            float rowHeight = 18f;
            float headerHeight = 22f;
            float footerHeight = 24f;
            float panelHeight = headerHeight + rows.Count * rowHeight + footerHeight + 8f;

            float x = marginFromBottomLeft.x;
            float y = Screen.height - panelHeight - marginFromBottomLeft.y;

            var rect = new Rect(x, y, width, panelHeight);
            GUI.Box(rect, "");

            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f));
            GUILayout.Label("LOD Comparison — Per-Missile Telemetry", headerStyle);

            // Column header.
            GUILayout.Label(
                "Missile        LOD  ToF      Peak v     Miss      Fuel  Outcome",
                rowStyle);

            for (int i = 0; i < rows.Count; i++)
                DrawRow(rows[i]);

            // Footer: clear button + summary.
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", GUILayout.Width(80f)))
                Clear();
            GUILayout.Label(BuildSummary(), rowStyle);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawRow(Row r)
        {
            string colorHex = LodTrailColors.HexFor(r.Lod);
            string missStr = r.MissDistance < float.MaxValue
                ? r.MissDistance < 1000f ? $"{r.MissDistance:0.}m" : $"{r.MissDistance / 1000f:0.0}km"
                : "—";

            string row = string.Format(
                "<color={0}>{1,-14}</color> {2,-4} {3,6:0.0}s {4,6:0} m/s {5,7} {6,5:0.}kg  {7}",
                colorHex,
                Truncate(r.Name, 14),
                ShortLod(r.Lod),
                r.OutcomeToF,
                r.PeakSpeed,
                missStr,
                r.OutcomeFuel,
                OutcomeLabel(r));

            GUILayout.Label(row, rowStyle);
        }

        private string BuildSummary()
        {
            int hit = 0, det = 0, fail = 0, active = 0;
            foreach (var r in rows)
            {
                switch (r.HitOutcome)
                {
                    case Outcome.Hit:       hit++; break;
                    case Outcome.Detonated: det++; break;
                    case Outcome.Failed:    fail++; break;
                    case Outcome.Active:    active++; break;
                }
            }
            return $"<b>{hit}</b> hit · {det} detonated · {fail} failed · {active} in flight";
        }

        private static string OutcomeLabel(Row r)
        {
            switch (r.HitOutcome)
            {
                case Outcome.Hit:        return "<color=#88FF88>HIT</color>";
                case Outcome.Detonated:  return "<color=#FFCC66>DET</color>";
                case Outcome.Failed:     return "<color=#FF8888>FAIL</color>";
                default:                 return "<color=#AAAAAA>active</color>";
            }
        }

        private static string ShortLod(MissileLod lod)
        {
            switch (lod)
            {
                case MissileLod.L0_Kinematic:         return "L0";
                case MissileLod.L1_PointMass3Dof:     return "L1";
                case MissileLod.L2_RateLimited3Dof:   return "L2";
                case MissileLod.L3_PseudoRb6Dof:      return "L3";
                case MissileLod.L4_FullAero6Dof:      return "L4";
                case MissileLod.L5_HardwareInTheLoop: return "L5";
                default: return "?";
            }
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13, fontStyle = FontStyle.Bold, richText = true,
                };
                headerStyle.normal.textColor = Color.white;
            }
            if (rowStyle == null)
            {
                rowStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12, richText = true,
                };
                rowStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            }
        }

        // ============================================================
        // Row + outcome enum
        // ============================================================

        private enum Outcome { Active = 0, Hit, Detonated, Failed }

        private sealed class Row
        {
            public MissileBehaviour Missile;
            public string Name;
            public MissileLod Lod;
            public float PeakSpeed;
            public float MissDistance = float.MaxValue;
            public float OutcomeToF;
            public float OutcomeMass;
            public float OutcomeFuel;
            public Outcome HitOutcome = Outcome.Active;
            public bool IsTerminal;
        }
    }
}
