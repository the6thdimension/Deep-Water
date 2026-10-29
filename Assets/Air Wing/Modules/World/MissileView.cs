using System.Collections.Generic;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Missiles;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Modules.World
{
    /// <summary>
    /// The game missiles, drawn (W65): one on each loaded pylon of every jet that carries them, and every missile in
    /// flight with its smoke trail, read from the flight core each frame. A missile that bursts or expires leaves its trail
    /// to fade. The shapes and the trail come from the jet's visuals (<see cref="AircraftVisualsSO"/>). Presentation only;
    /// the runner adds one at start when a jet carries missiles, so no scene needs saving.
    /// </summary>
    public sealed class MissileView : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;

        sealed class Flying
        {
            public GameObject Body, Trail;
            public float LingerS;
            public bool Seen;
        }

        readonly Dictionary<int, Flying> _flying = new Dictionary<int, Flying>();
        readonly List<int> _gone = new List<int>();
        readonly Dictionary<int, GameObject[]> _stores = new Dictionary<int, GameObject[]>();
        readonly Dictionary<int, AircraftView> _views = new Dictionary<int, AircraftView>();

        public void Use(AirWingRunner r) => runner = r;

        void LateUpdate()
        {
            if (runner == null) runner = GetComponent<AirWingRunner>();
            if (runner == null || runner.Flight == null) return;
            FlightCore core = runner.Flight;
            if (_views.Count != runner.Views.Count)
                foreach (AircraftView v in runner.Views) _views[v.AircraftId] = v;

            // Stores on the pylons: the first (carried − left) pylons have fired.
            foreach (AircraftView v in runner.Views)
            {
                if (v.Aircraft == null || v.Visuals == null || v.Visuals.missileModel == null || !v.Aircraft.Profile.Missiles.IsSet) continue;
                if (!_stores.TryGetValue(v.AircraftId, out GameObject[] stores)) _stores[v.AircraftId] = stores = Hang(v);
                int fired = v.Aircraft.Profile.Missiles.Carried - v.Aircraft.State.Missiles;
                bool flying = v.Aircraft.State.Status == FlightStatus.Flying || v.Aircraft.State.Status == FlightStatus.Killed;
                for (int i = 0; i < stores.Length; i++)
                {
                    bool show = flying && i >= fired;
                    if (stores[i].activeSelf != show) stores[i].SetActive(show);
                }
            }

            // Missiles in flight, carried on from the last tick by their velocity, so they move every frame.
            float ahead = Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
            foreach (Flying f in _flying.Values) f.Seen = false;
            IReadOnlyList<MissileInFlight> live = core.Missiles.Live;
            for (int i = 0; i < live.Count; i++)
            {
                MissileInFlight m = live[i];
                if (!_flying.TryGetValue(m.Id, out Flying f))
                {
                    AircraftVisualsSO vis = _views.TryGetValue(m.ShooterId, out AircraftView sv) ? sv.Visuals : null;
                    if (vis == null || vis.missileModel == null) continue;
                    // Made where the missile is: a trail made anywhere else draws a streak from there to it (2026-10-29: lines
                    // from the world origin across the sky).
                    Quaternion start = m.Velocity.sqrMagnitude > 1f ? Quaternion.LookRotation(m.Velocity) : Quaternion.identity;
                    f = new Flying { Body = Instantiate(vis.missileModel, m.Position, start), LingerS = vis.missileTrailLingerS };
                    if (vis.missileTrail != null)
                    {
                        f.Trail = Instantiate(vis.missileTrail, f.Body.transform);
                        f.Trail.transform.localPosition = new Vector3(0f, 0f, -1.6f);   // the tail
                        foreach (TrailRenderer tr in f.Trail.GetComponentsInChildren<TrailRenderer>(true)) { tr.Clear(); tr.emitting = true; }
                        foreach (ParticleSystem ps in f.Trail.GetComponentsInChildren<ParticleSystem>(true)) ps.Play(false);
                    }
                    _flying[m.Id] = f;
                }
                f.Seen = true;
                Vector3 at = m.Position + m.Velocity * ahead;
                Quaternion facing = m.Velocity.sqrMagnitude > 1f ? Quaternion.LookRotation(m.Velocity) : f.Body.transform.rotation;
                f.Body.transform.SetPositionAndRotation(at, facing);
            }

            // Gone: the body goes at once; the trail stops and fades, then goes.
            _gone.Clear();
            foreach (KeyValuePair<int, Flying> kv in _flying) if (!kv.Value.Seen) _gone.Add(kv.Key);
            foreach (int id in _gone)
            {
                Flying f = _flying[id];
                _flying.Remove(id);
                if (f.Trail != null)
                {
                    f.Trail.transform.SetParent(null, true);
                    foreach (TrailRenderer tr in f.Trail.GetComponentsInChildren<TrailRenderer>(true)) tr.emitting = false;
                    foreach (ParticleSystem ps in f.Trail.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    Destroy(f.Trail, f.LingerS);
                }
                Destroy(f.Body);
            }
        }

        /// <summary>One store per pylon, a child of the jet's view, so it flies with it.</summary>
        GameObject[] Hang(AircraftView v)
        {
            Vector3[] pylons = v.Aircraft.Profile.Missiles.PylonsM;
            var stores = new GameObject[pylons.Length];
            for (int i = 0; i < pylons.Length; i++)
            {
                stores[i] = Instantiate(v.Visuals.missileModel, v.transform);
                stores[i].name = $"Pylon {i + 1}";
                stores[i].transform.localPosition = pylons[i];
                stores[i].transform.localRotation = Quaternion.identity;
            }
            return stores;
        }
    }
}
