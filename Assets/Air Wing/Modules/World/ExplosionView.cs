using System.Collections.Generic;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Modules.World
{
    /// <summary>
    /// Explosions (SIX, 2026-10-29: "collisions and kills should render explosions"): read from the flight core after
    /// every tick, one where a jet is shot down (Gunnery K5), one where two jets collide (the arena, W60), one where
    /// a jet meets the sea or the ground, and a smaller one where a game missile bursts without a kill (W65). Each plays the aircraft's own explosion (<see cref="AircraftVisualsSO"/>), scaled
    /// for the air or the surface, and is removed after its life. Presentation only: the core never knows. The runner
    /// adds one at start when any jet has an explosion set, so no scene needs saving for explosions to show.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class ExplosionView : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;

        int _kills, _arena, _missiles;
        readonly Dictionary<int, FlightStatus> _status = new Dictionary<int, FlightStatus>();
        readonly Dictionary<int, AircraftView> _views = new Dictionary<int, AircraftView>();

        /// <summary>Explosions played so far (for smokes).</summary>
        public int Played { get; private set; }

        public void Use(AirWingRunner r) => runner = r;

        void FixedUpdate()
        {
            if (runner == null) runner = GetComponent<AirWingRunner>();
            if (runner == null || runner.Flight == null) return;
            FlightCore core = runner.Flight;
            if (_views.Count != runner.Views.Count)
                foreach (AircraftView v in runner.Views) _views[v.AircraftId] = v;

            // Shot down: where the victim was when the last hit landed.
            for (; _kills < core.Kills.Count; _kills++)
            {
                Core.Aircraft victim = core.Find(core.Kills[_kills].VictimId);
                Play(victim.Id, victim.State.Position, air: true);
            }
            // Collided: one explosion between the two (the arena logs each side of the pair).
            if (core.Arena != null)
                for (; _arena < core.Arena.Events.Count; _arena++)
                {
                    ArenaEvent e = core.Arena.Events[_arena];
                    if (e.Kind != ArenaEventKind.Collided || e.AircraftId > e.OtherId) continue;
                    Play(e.AircraftId, 0.5f * (core.Find(e.AircraftId).State.Position + core.Find(e.OtherId).State.Position), air: true);
                }
            // A missile's burst that did not kill (a kill already plays at its victim, above): smaller, where it burst.
            for (; _missiles < core.Missiles.Events.Count; _missiles++)
            {
                Core.Missiles.MissileEvent e = core.Missiles.Events[_missiles];
                if (e.Kind == Core.Missiles.MissileEventKind.Burst && !e.Killed) Play(e.ShooterId, e.Point, air: true, missileBurst: true);
            }
            // Met the surface: flying or falling, it became Crashed this tick.
            foreach (Core.Aircraft a in core.Aircraft)
            {
                FlightStatus now = a.State.Status;
                if (_status.TryGetValue(a.Id, out FlightStatus before) && before != FlightStatus.Crashed && now == FlightStatus.Crashed)
                    Play(a.Id, a.State.Position, air: false);
                _status[a.Id] = now;
            }
        }

        void Play(int aircraftId, Vector3 at, bool air, bool missileBurst = false)
        {
            if (!_views.TryGetValue(aircraftId, out AircraftView view) || view.Visuals == null || view.Visuals.explosion == null) return;
            AircraftVisualsSO v = view.Visuals;
            GameObject boom = Instantiate(v.explosion, at, Quaternion.identity);
            boom.transform.localScale = Vector3.one * (missileBurst ? v.missileBurstScale : air ? v.airExplosionScale : v.impactExplosionScale);
            // Let the instance's scale reach every system, and start them: an effect built to be pooled (General Quarters'
            // are) does not play on awake, and an instance that is never told to play emits nothing (found 2026-10-29).
            foreach (ParticleSystem ps in boom.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                ps.Play(false);
            }
            Destroy(boom, v.explosionLifeS);
            Played++;
        }
    }
}
