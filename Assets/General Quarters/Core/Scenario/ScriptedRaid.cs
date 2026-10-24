using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.World;

namespace DeepWater.GeneralQuarters.Core.Scenario
{
    /// <summary>One scripted threat: where it appears, how it flies, when it launches.</summary>
    [System.Serializable]
    public struct RaidThreatSpec
    {
        public TargetKind Kind;
        public Identity Identity;
        /// <summary>True bearing from the own ship's starting position, degrees.</summary>
        public float BearingDeg;
        public float RangeM;
        public float AltitudeM;
        public float SpeedMps;
        public long LaunchTick;
        public float RcsDbsm;
        public float WarheadKg;
    }

    /// <summary>
    /// A raid on set paths: the scenario half of the world. Anti-ship missiles fly level at their altitude,
    /// straight at the own ship's current position; aircraft fly a straight pass over the ship's starting
    /// position. It owns whether its threats are alive: kills arrive from the combat core as reports, and
    /// threats that reach the ship become hits. The ship is a box for hit purposes (L0).
    /// </summary>
    public sealed class ScriptedRaid
    {
        struct Threat
        {
            public RaidThreatSpec Spec;
            public int Id;
            public Vector3 Position;
            public Vector3 Velocity;
            public bool Launched;
            public bool Alive;
            public bool Leaked;
        }

        readonly Threat[] _threats;
        readonly Vector3 _origin;
        readonly float _halfBeamM, _halfLengthM, _hitHeightM;

        /// <summary>Threats in the raid, launched or not.</summary>
        public int Threats => _threats.Length;
        public int Launched { get; private set; }
        public int Killed { get; private set; }
        public int Leaked { get; private set; }
        public int Active { get; private set; }
        /// <summary>True once every threat has launched and none is still flying.</summary>
        public bool Resolved => Launched == _threats.Length && Active == 0;

        public ScriptedRaid(IList<RaidThreatSpec> specs, in OwnShipState ownAtStart, int firstId = 1000,
            float halfBeamM = 10f, float halfLengthM = 77f, float hitHeightM = 30f)
        {
            _origin = ownAtStart.Position;
            _halfBeamM = halfBeamM;
            _halfLengthM = halfLengthM;
            _hitHeightM = hitHeightM;
            _threats = new Threat[specs.Count];
            for (int i = 0; i < specs.Count; i++) _threats[i] = new Threat { Spec = specs[i], Id = firstId + i };
        }

        public void Step(long tick, float dt, in OwnShipState own, List<TruthTarget> truthOut, List<ShipHit> hitsOut)
        {
            truthOut.Clear();
            Active = 0;
            for (int i = 0; i < _threats.Length; i++)
            {
                ref Threat t = ref _threats[i];
                if (!t.Launched && tick >= t.Spec.LaunchTick) Launch(ref t, own);
                if (!t.Launched || !t.Alive) continue;

                if (t.Spec.Kind == TargetKind.AntiShipMissile)
                {
                    Vector3 aim = own.Position; aim.y = t.Spec.AltitudeM;
                    Vector3 to = aim - t.Position;
                    t.Velocity = to.sqrMagnitude > 1e-4f ? to.normalized * t.Spec.SpeedMps : t.Velocity;
                }
                t.Position += t.Velocity * dt;

                if (t.Spec.Kind == TargetKind.AntiShipMissile && HitsShip(t.Position, own))
                {
                    t.Alive = false;
                    t.Leaked = true;
                    Leaked++;
                    hitsOut.Add(new ShipHit { ThreatId = t.Id, Kind = t.Spec.Kind, Point = t.Position, WarheadKg = t.Spec.WarheadKg });
                    continue;
                }
                if (t.Spec.Kind == TargetKind.Aircraft && HorizontalDistance(t.Position, _origin) > t.Spec.RangeM + 1f)
                {
                    t.Alive = false;   // flew through and out of the scenario
                    continue;
                }

                Active++;
                truthOut.Add(new TruthTarget
                {
                    Id = t.Id, Kind = t.Spec.Kind, Identity = t.Spec.Identity, Position = t.Position,
                    Velocity = t.Velocity, Alive = true, RcsDbsm = t.Spec.RcsDbsm,
                });
            }
        }

        public void ApplyKills(List<KillReport> kills)
        {
            for (int k = 0; k < kills.Count; k++)
                for (int i = 0; i < _threats.Length; i++)
                    if (_threats[i].Id == kills[k].TruthId && _threats[i].Alive)
                    {
                        _threats[i].Alive = false;
                        Killed++;
                    }
        }

        void Launch(ref Threat t, in OwnShipState own)
        {
            Quaternion bearing = Quaternion.Euler(0f, t.Spec.BearingDeg, 0f);
            t.Position = _origin + bearing * Vector3.forward * t.Spec.RangeM;
            t.Position.y = t.Spec.AltitudeM;
            Vector3 inbound = _origin - t.Position; inbound.y = 0f;
            t.Velocity = inbound.normalized * t.Spec.SpeedMps;
            t.Launched = true;
            t.Alive = true;
            Launched++;
        }

        // Ground range: an aircraft launches RangeM out at altitude, so its slant range starts beyond RangeM. Measuring
        // slant range removed every aircraft on its first step (found on the range, slice 5).
        static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        bool HitsShip(Vector3 p, in OwnShipState own)
        {
            Vector3 local = own.ToShipFrame(p);
            return Mathf.Abs(local.x) <= _halfBeamM && Mathf.Abs(local.z) <= _halfLengthM && local.y <= _hitHeightM;
        }
    }
}
