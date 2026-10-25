using System.Collections.Generic;
using DeepWater.GeneralQuarters.Core.DamageControl;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;

namespace DeepWater.GeneralQuarters.Core.Scenario
{
    /// <summary>What a gun mount fired in a run.</summary>
    public struct MountExpenditure
    {
        public string Name;
        public WeaponClass Weapon;
        public int RoundsFired;
        public int RoundsLeft;
        public bool Online;
    }

    /// <summary>
    /// The debrief of one raid: counted from the raid, the kill reports, the magazine and the engagement log, never
    /// declared. Pure, so a headless test reads the same numbers the range shows.
    /// </summary>
    public sealed class RaidSummary
    {
        public string RaidName;
        public int Threats, Launched, Killed, Leaked;
        /// <summary>Kills by the weapon class that made them, indexed by <see cref="WeaponClass"/>.</summary>
        public readonly int[] KillsBy = new int[8];
        public int MissilesLaunched, Bursts;
        public int MissilesLeft;
        public readonly List<MountExpenditure> Mounts = new List<MountExpenditure>();
        public float HullHitPoints, HullHitPointsMax;
        public bool OutOfAction;
        public readonly List<ShipSystem> Offline = new List<ShipSystem>();
        public int Orders, AiDecisions;

        /// <summary>Damage Control L1 (G59): whether she had spaces to flood, and what the water did. Empty below L1.</summary>
        public bool HasFlooding;
        public int Breaches, SpacesWet, SpacesFull;
        public float WaterT, PeakListDeg, PeakTrimM, IntactGmM, MinGmM, ReserveBuoyancy;
        public FloodVerdict Verdict;
        /// <summary>The flooding timeline: each breach, space and verdict with its time and what caused it.</summary>
        public readonly List<FloodLine> Flooding = new List<FloodLine>();
        public float DurationS;
        public ulong Seed, Fix;

        public static RaidSummary Of(string raidName, CombatSystem combat, ScriptedRaid raid, IReadOnlyList<KillReport> kills,
            CombatLoadout loadout, long raidStartTick)
        {
            var s = new RaidSummary
            {
                RaidName = raidName,
                Threats = raid.Threats, Launched = raid.Launched, Killed = raid.Killed, Leaked = raid.Leaked,
                HullHitPoints = combat.DamageControl.HullHitPoints, HullHitPointsMax = loadout.Damage.HullHitPoints,
                OutOfAction = combat.DamageControl.OutOfAction,
                Orders = combat.Orders.All.Count,
                DurationS = (combat.Tick - raidStartTick) * combat.TickSeconds,
                Seed = loadout.Seed, Fix = combat.Fix,
            };
            for (int i = 0; i < kills.Count; i++) s.KillsBy[(int)kills[i].Weapon]++;

            IReadOnlyList<EngagementEvent> events = combat.Log.Events;
            for (int i = 0; i < events.Count; i++)
            {
                EngagementEvent e = events[i];
                switch (e.Kind)
                {
                    case EventKind.Launched: s.MissilesLaunched++; break;
                    case EventKind.BurstFired: s.Bursts++; break;
                    case EventKind.Decision: s.AiDecisions++; break;
                }
            }

            for (int i = 0; i < loadout.Vls.Loads.Length; i++) s.MissilesLeft += combat.Vls.Rounds(loadout.Vls.Loads[i].Weapon);
            for (int i = 0; i < combat.Guns.Count; i++)
            {
                IGunMount g = combat.Guns[i];
                s.Mounts.Add(new MountExpenditure
                {
                    Name = g.Profile.Name, Weapon = g.Profile.Weapon, RoundsFired = g.Profile.MagazineRounds - g.Rounds,
                    RoundsLeft = g.Rounds, Online = g.Online,
                });
            }
            for (int i = 0; i < loadout.Damage.Systems.Length; i++)
            {
                ShipSystem system = loadout.Damage.Systems[i].System;
                if (!combat.DamageControl.IsOnline(system)) s.Offline.Add(system);
            }

            IFlooding f = combat.DamageControl.Flooding;
            if (f != null)
            {
                s.HasFlooding = true;
                s.Breaches = f.Breaches;
                s.SpacesWet = f.WetSpaces;
                s.SpacesFull = f.FullSpaces;
                s.WaterT = f.Water.WaterMassKg / 1000f;
                s.PeakListDeg = f.PeakListDeg;
                s.PeakTrimM = f.PeakTrimM;
                s.IntactGmM = f.IntactGmM;
                s.MinGmM = f.MinGmM;
                s.ReserveBuoyancy = f.ReserveBuoyancy;
                s.Verdict = f.Verdict;
                for (int i = 0; i < events.Count; i++)
                {
                    EngagementEvent e = events[i];
                    bool lost = e.Kind == EventKind.SystemOffline && e.Source == (int)ShipSystem.Hull;
                    if (e.Kind != EventKind.Breach && e.Kind != EventKind.Flooding && !lost) continue;
                    s.Flooding.Add(new FloodLine
                    {
                        TimeS = (e.Tick - raidStartTick) * combat.TickSeconds,
                        Text = e.Text,
                        Cause = e.CauseA >= 0 && e.CauseA < events.Count ? events[e.CauseA].Text : RootText(e),
                    });
                }
            }
            return s;
        }

        static string RootText(in EngagementEvent e) => e.Root switch
        {
            RootKind.Order => $"Order #{e.OrderSequence}",
            RootKind.Scenario => "the scenario",
            RootKind.Chance => "a seeded draw",
            _ => "",
        };
    }

    /// <summary>One line of the flooding timeline: when (seconds from the raid's start), what, and what caused it.</summary>
    public struct FloodLine
    {
        public float TimeS;
        public string Text;
        public string Cause;
    }
}
