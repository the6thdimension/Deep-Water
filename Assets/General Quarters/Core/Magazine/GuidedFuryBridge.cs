using UnityEngine;
using GuidedFury.Core.Aero;
using GuidedFury.Core.Atmosphere;
using GuidedFury.Core.Autopilot;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.Propulsion;
using GuidedFury.Core.State;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.Simulation;

namespace DeepWater.GeneralQuarters.Core.Magazine
{
    /// <summary>The combat core's clock, shared by everything that predicts ahead (guidance, gun lead).</summary>
    public sealed class CombatClock
    {
        public long Tick;
        public float TickSeconds;
    }

    /// <summary>
    /// Feeds a Guided Fury missile's guidance from a CIC track instead of a scene transform: the missile
    /// flies on what the ship believes, not on truth. At Tracks L0 those coincide; at L1 track error flows
    /// into guidance on its own. When the track drops, guidance gets no target.
    /// </summary>
    public sealed class TrackTargetSource : ITargetSource
    {
        readonly ITrackManager _tracks;
        readonly CombatClock _clock;
        readonly int _trackId;

        public TrackTargetSource(ITrackManager tracks, CombatClock clock, int trackId)
        {
            _tracks = tracks;
            _clock = clock;
            _trackId = trackId;
        }

        public void Update(in MissileState missileState, float dt) { }

        public TargetTrack Sample()
        {
            if (!_tracks.TryGet(_trackId, out Track t) || !t.Active || t.Destroyed) return TargetTrack.None;
            return TargetTrack.Omniscient(t.PredictedPosition(_clock.Tick, _clock.TickSeconds), t.Velocity);
        }
    }

    /// <summary>
    /// Builds Guided Fury missiles headless, the way its own tests do. The missile's fidelity is Guided
    /// Fury's ladder (L0 kinematic … L4 full aero), chosen per weapon in the loadout and resolved honestly:
    /// L4 needs the profile asset's aero tables, which baked data doesn't carry, so a tabulated-aero
    /// profile asked for L4 flies at L3 and the note says so.
    /// </summary>
    public static class GuidedFuryBridge
    {
        public static MissileEntity Create(in MissileProfileData profile, MissileLod requested, ITargetSource target,
            out MissileLod used, out string note)
        {
            used = requested;
            note = null;
            if (used >= MissileLod.L5_HardwareInTheLoop) { used = MissileLod.L4_FullAero6Dof; note = "L5 is not built in Guided Fury; L4 used"; }
            if (used == MissileLod.L4_FullAero6Dof && profile.AeroModel != AeroModelKind.Simple)
            {
                used = MissileLod.L3_PseudoRb6Dof;
                note = "L4 needs the profile asset's tabulated aero curves, which baked data does not carry; L3 used";
            }

            IPhysicsIntegrator integrator;
            switch (used)
            {
                case MissileLod.L0_Kinematic: integrator = new KinematicL0Integrator(); break;
                case MissileLod.L1_PointMass3Dof: integrator = new PointMass3DofL1Integrator(); break;
                case MissileLod.L2_RateLimited3Dof: integrator = new RateLimited3DofL2Integrator(); break;
                case MissileLod.L3_PseudoRb6Dof: integrator = new PseudoRb6DofL3Integrator(); break;
                default:
                    integrator = new FullAero6DofL4Integrator(SimpleAeroModel.Instance,
                        AutopilotFactory.Create(profile.Autopilot), ThrustModelFactory.Create(profile.ThrustModel));
                    break;
            }
            return new MissileEntity(in profile, integrator, GuidanceFactory.Create(profile.GuidanceLaw), StandardAtmosphere.Instance, target);
        }
    }

    /// <summary>One missile in the air, and what the combat core needs to resolve it.</summary>
    public sealed class MissileInFlight
    {
        public MissileEntity Entity;
        public WeaponClass Weapon;
        public int TrackId;
        public int TargetTruthId;
        public long LaunchTick;
        public int LaunchEvent;
        public float Pk;
        public bool Done;

        public ulong Fingerprint(ulong h)
        {
            MissileState s = Entity.State;
            h = CombatFix.Mix(h, s.Position);
            h = CombatFix.Mix(h, s.Velocity);
            h = CombatFix.Mix(h, (int)s.Phase);
            return CombatFix.Mix(h, Done);
        }
    }
}
