using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Modules.World
{
    /// <summary>
    /// Puts an arena on the scene's flight core (W59–W61, P2). In Start, after Air Wing's runner has built the jets
    /// (order −45, after the runner's −50), it bakes <see cref="ArenaSO"/>, hands the core an <see cref="Arena"/>, and gives
    /// every jet of every <see cref="SpawnPoint"/> its slot: the state the runner built it in. Each respawn, collision and
    /// exit from the box is printed to the console (Observable).
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public sealed class AirWingArena : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;
        [SerializeField] ArenaSO arena;
        [SerializeField] bool logEvents = true;

        Arena _arena;
        int _logged;
        readonly List<SpawnPoint> _points = new List<SpawnPoint>();

        public Arena Arena => _arena;
        public IReadOnlyList<SpawnPoint> Points => _points;

        void Start()
        {
            if (runner == null) runner = FindAnyObjectByType<AirWingRunner>();
            if (runner == null || runner.Flight == null || arena == null)
            {
                Debug.LogError("[Air Wing] the arena needs a running Air Wing runner and an Arena asset; no respawns.", this);
                enabled = false;
                return;
            }
            FlightCore core = runner.Flight;
            _arena = new Arena(arena.Bake(), core.Clock.TickSeconds, arena.Seed);
            core.UseArena(_arena);
            _points.AddRange(FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude).OrderBy(p => p.Label));
            foreach (SpawnPoint p in _points)
                foreach (AircraftView v in p.Jets)
                    if (v != null && v.Aircraft != null) _arena.Assign(v.AircraftId, v.Aircraft.Start);
            ArenaSettings s = _arena.Settings;
            Debug.Log($"[Air Wing] arena: {string.Join(", ", _points.Select(p => $"{p.Label} {p.Jets.Count} jets"))}; back {s.RespawnDelayS:0.#} s after going down, " +
                      $"protected {s.ProtectionS:0.#} s, jitter ±{s.JitterHeightM:F0} m ±{s.JitterHeadingDeg:F0}°, collisions inside {s.CollisionM:F0} m, " +
                      $"box {2f * s.HalfWidthXM / 1000f:F0} × {2f * s.HalfWidthZM / 1000f:F0} km about ({s.CentreM.x:F0}, {s.CentreM.z:F0}).", this);
        }

        /// <summary>Everyone back at their slots now (a reset of the whole fight).</summary>
        public void RespawnAll()
        {
            if (_arena == null) return;
            FlightCore core = runner.Flight;
            foreach (Core.Aircraft a in core.Aircraft)
                if (_arena.HasSlot(a.Id)) _arena.Respawn(a, core.Tick);
        }

        void FixedUpdate()
        {
            if (_arena == null || !logEvents) return;
            for (; _logged < _arena.Events.Count; _logged++)
            {
                ArenaEvent e = _arena.Events[_logged];
                string why = e.Kind == ArenaEventKind.Respawned ? "" : $" ({runner.Flight.Find(e.AircraftId).CrashReason})";
                Debug.Log($"[Air Wing] {e}{why}", this);
            }
        }
    }
}
