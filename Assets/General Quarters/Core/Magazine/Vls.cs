using System.Collections.Generic;
using UnityEngine;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.Core.State;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Simulation;

namespace DeepWater.GeneralQuarters.Core.Magazine
{
    /// <summary>One missile type loaded in the VLS.</summary>
    public struct VlsMissileLoad
    {
        public WeaponClass Weapon;
        public MissileProfileData Profile;
        /// <summary>The missile's own fidelity (Guided Fury's ladder), chosen per weapon.</summary>
        public MissileLod MissileLevel;
        public int Rounds;
        /// <summary>Single-shot kill probability when the fuze functions. AUTHORED unless the loadout cites a source.</summary>
        public float PkSingleShot;
    }

    /// <summary>A launcher group (the Mk 41 modules fore or aft).</summary>
    public struct VlsGroupProfile
    {
        public string Name;
        /// <summary>Launch point in the ship frame, m.</summary>
        public Vector3 OffsetShip;
        /// <summary>The damageable system that takes this group offline.</summary>
        public ShipSystem System;
    }

    public struct VlsProfileData
    {
        /// <summary>Seconds between launches from one group.</summary>
        public float LaunchIntervalS;
        public VlsGroupProfile[] Groups;
        public VlsMissileLoad[] Loads;
    }

    public interface IVls : IMissileAvailability
    {
        FidelityLevel Level { get; }
        IReadOnlyList<MissileInFlight> InFlight { get; }
        int Rounds(WeaponClass weapon);
        void SetGroupOnline(ShipSystem system, bool online);
        bool TryLaunch(EngagementContext ctx, in Assignment assignment, out int launchEvent);
        void StepInFlight(EngagementContext ctx);
        ulong Fingerprint(ulong h);
    }

    /// <summary>
    /// VLS L0: groups with a launch interval, a magazine per missile type, and Guided Fury missiles guided
    /// on CIC tracks. Intercepts are resolved by closest approach over each step against the missile's
    /// fuze radius; then a seeded single-shot Pk draw decides kill or miss. What it does NOT model (L1+):
    /// hatch and launch-sequence timing, the boost-phase pitch-over constraints of a real vertical
    /// launch, midcourse uplink limits, illuminator scheduling (Illuminators L1), the ship's velocity
    /// added at launch.
    /// </summary>
    public sealed class VlsL0 : IVls
    {
        const string Rule = "VLS L0 (closest approach, seeded Pk)";

        readonly VlsProfileData _profile;
        readonly CombatClock _clock;
        readonly int[] _rounds;
        readonly long[] _groupReadyTick;
        readonly bool[] _groupOnline;
        readonly bool[] _fallbackNoted;
        readonly List<MissileInFlight> _inFlight = new List<MissileInFlight>();
        int _launches;

        public VlsL0(in VlsProfileData profile, CombatClock clock)
        {
            _profile = profile;
            _clock = clock;
            _rounds = new int[profile.Loads.Length];
            _fallbackNoted = new bool[profile.Loads.Length];
            for (int i = 0; i < _rounds.Length; i++) _rounds[i] = profile.Loads[i].Rounds;
            _groupReadyTick = new long[profile.Groups.Length];
            _groupOnline = new bool[profile.Groups.Length];
            for (int i = 0; i < _groupOnline.Length; i++) _groupOnline[i] = true;
        }

        public FidelityLevel Level => FidelityLevel.L0;
        public IReadOnlyList<MissileInFlight> InFlight => _inFlight;

        public int Rounds(WeaponClass weapon)
        {
            int n = 0;
            for (int i = 0; i < _rounds.Length; i++) if (_profile.Loads[i].Weapon == weapon) n += _rounds[i];
            return n;
        }

        public bool CanEngage(WeaponClass weapon)
        {
            bool anyGroup = false;
            for (int g = 0; g < _groupOnline.Length; g++) anyGroup |= _groupOnline[g];
            return anyGroup && Rounds(weapon) > 0;
        }

        public int InFlightAt(int trackId, WeaponClass weapon)
        {
            int n = 0;
            for (int i = 0; i < _inFlight.Count; i++)
            {
                MissileInFlight m = _inFlight[i];
                if (!m.Done && m.TrackId == trackId && m.Weapon == weapon) n++;
            }
            return n;
        }

        public void SetGroupOnline(ShipSystem system, bool online)
        {
            for (int g = 0; g < _profile.Groups.Length; g++)
                if (_profile.Groups[g].System == system) _groupOnline[g] = online;
        }

        public bool TryLaunch(EngagementContext ctx, in Assignment assignment, out int launchEvent)
        {
            launchEvent = -1;
            int load = -1;
            for (int i = 0; i < _rounds.Length; i++)
                if (_profile.Loads[i].Weapon == assignment.Weapon && _rounds[i] > 0) { load = i; break; }
            if (load < 0 || !ctx.Tracks.TryGet(assignment.TrackId, out Track track) || !track.Active || track.Destroyed) return false;

            // Groups take turns (fore, aft, fore …) among those online and ready.
            int group = -1;
            for (int k = 0; k < _profile.Groups.Length; k++)
            {
                int g = (_launches + k) % _profile.Groups.Length;
                if (_groupOnline[g] && ctx.Tick >= _groupReadyTick[g]) { group = g; break; }
            }
            if (group < 0) return false;

            VlsMissileLoad l = _profile.Loads[load];
            var source = new TrackTargetSource(ctx.Tracks, _clock, track.Id);
            MissileEntity entity = GuidedFuryBridge.Create(l.Profile, l.MissileLevel, source, out MissileLod used, out string note);
            if (note != null && !_fallbackNoted[load])
            {
                _fallbackNoted[load] = true;
                ctx.Log.Add(ctx.Tick, EventKind.FidelityFallback, "Guided Fury missile level",
                    $"{l.Weapon}: missile {l.MissileLevel} requested, {used} used: {note}", root: RootKind.Scenario);
            }

            // Vertical launch: nose up, the missile's top toward the bow.
            Vector3 cell = ctx.Own.ToWorld(_profile.Groups[group].OffsetShip);
            Quaternion up = Quaternion.LookRotation(Vector3.up, ctx.Own.HeadingRotation * Vector3.forward);
            entity.Launch(cell, up);

            _rounds[load]--;
            _groupReadyTick[group] = ctx.Tick + Mathf.Max(1, Mathf.RoundToInt(_profile.LaunchIntervalS / ctx.TickSeconds));
            _launches++;

            float range = Vector3.Distance(track.PredictedPosition(ctx.Tick, ctx.TickSeconds), ctx.Own.Position);
            launchEvent = ctx.Log.Add(ctx.Tick, EventKind.Launched, Rule,
                System.FormattableString.Invariant($"{l.Weapon} launched from {_profile.Groups[group].Name} at track {track.Id} ({range / 1000f:0.0} km); missile {used}; {Rounds(l.Weapon)} left"),
                track.Id, assignment.CauseEvent);

            _inFlight.Add(new MissileInFlight
            {
                Entity = entity, Weapon = l.Weapon, TrackId = track.Id, TargetTruthId = track.TruthId,
                LaunchTick = ctx.Tick, LaunchEvent = launchEvent, Pk = l.PkSingleShot,
            });
            return true;
        }

        public void StepInFlight(EngagementContext ctx)
        {
            float dt = ctx.TickSeconds;
            for (int i = 0; i < _inFlight.Count; i++)
            {
                MissileInFlight m = _inFlight[i];
                if (m.Done) continue;

                Vector3 p0 = m.Entity.State.Position;
                m.Entity.Step(dt);
                Vector3 p1 = m.Entity.State.Position;

                if (!ctx.TargetAlive(m.TargetTruthId) || !ctx.TryTruth(m.TargetTruthId, out var target))
                {
                    m.Done = true;
                    ctx.Log.Add(ctx.Tick, EventKind.Note, Rule,
                        $"{m.Weapon} at track {m.TrackId} self-destructed: target already destroyed or gone", m.TrackId, m.LaunchEvent);
                    continue;
                }

                Vector3 q1 = target.Position;
                Vector3 q0 = q1 - target.Velocity * dt;
                float miss = Intercept.MinDistance(p0, p1, q0, q1, out _);
                ref readonly MissileProfileData prof = ref m.Entity.Profile;
                bool armed = m.Entity.State.TimeOfFlight >= prof.FuzeArmDelayS;

                if (armed && miss <= prof.FuzeProximityRadiusM)
                {
                    m.Entity.Detonate();
                    m.Done = true;
                    bool kill = ctx.Rng.Chance(m.Pk, out ChanceRoot root);
                    string text = System.FormattableString.Invariant(
                        $"{m.Weapon} detonated {miss:0.0} m from track {m.TrackId} after {m.Entity.State.TimeOfFlight:0.0} s; Pk {m.Pk:0.00} → {(kill ? "KILL" : "survived")}");
                    int ev = ctx.Log.Add(ctx.Tick, kill ? EventKind.Kill : EventKind.Miss, Rule, text, m.TrackId, m.LaunchEvent,
                        root: RootKind.Chance, chance: root);
                    if (kill) ctx.RecordKill(m.TargetTruthId, m.TrackId, m.Weapon, ev);
                    continue;
                }

                MissilePhase phase = m.Entity.State.Phase;
                if (phase == MissilePhase.Failed || phase == MissilePhase.Detonated)
                {
                    m.Done = true;
                    ctx.Log.Add(ctx.Tick, EventKind.Miss, Rule,
                        System.FormattableString.Invariant($"{m.Weapon} at track {m.TrackId} failed after {m.Entity.State.TimeOfFlight:0.0} s (lifetime or energy)"),
                        m.TrackId, m.LaunchEvent);
                }
            }
            _inFlight.RemoveAll(m => m.Done);
        }

        public ulong Fingerprint(ulong h)
        {
            for (int i = 0; i < _rounds.Length; i++) h = CombatFix.Mix(h, _rounds[i]);
            for (int g = 0; g < _groupReadyTick.Length; g++) h = CombatFix.Mix(CombatFix.Mix(h, _groupReadyTick[g]), _groupOnline[g]);
            for (int i = 0; i < _inFlight.Count; i++) h = _inFlight[i].Fingerprint(h);
            return h;
        }
    }

    public static class VlsFactory
    {
        public static IVls Create(FidelityLevel requested, in VlsProfileData profile, CombatClock clock, out FidelityResolution resolution)
        {
            resolution = FidelityLadder.Resolve(ElementKind.Vls, requested);
            switch (resolution.Resolved)
            {
                default: return new VlsL0(profile, clock);
            }
        }
    }
}
