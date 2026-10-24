using System;
using UnityEngine;
using GuidedFury.Core.Integrators;
using GuidedFury.ScriptableObjects.Profiles;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.DamageControl;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Magazine;

namespace DeepWater.GeneralQuarters.ScriptableObjects
{
    /// <summary>One missile type in the VLS: the Guided Fury profile, the missile's own fidelity level, and the count.</summary>
    [Serializable]
    public struct VlsLoadEntry
    {
        public WeaponClass weapon;
        public MissileProfileSO profile;
        [Tooltip("The missile's own Guided Fury fidelity, chosen apart from the launcher's (BLUEPRINT §3.5).")]
        public MissileLod missileLevel;
        public int rounds;
        [Tooltip("Single-shot kill probability drawn at intercept (AUTHORED at L0).")]
        [Range(0f, 1f)] public float pkSingleShot;
    }

    /// <summary>
    /// A ship's combat loadout (BLUEPRINT §2, Loadout): for each combat element the fidelity level it asks for and
    /// that element's tuning, the seed, and the scenario's starting doctrine. Baked once into a
    /// <see cref="CombatLoadout"/> when a run starts; editing the asset during a run changes nothing until the next
    /// run (C7). <see cref="provenance"/> names the source of each number and flags the AUTHORED ones (G15).
    /// </summary>
    [CreateAssetMenu(fileName = "NewLoadout", menuName = "Deep Water/General Quarters/Ship Loadout", order = 20)]
    public class ShipLoadoutSO : ScriptableObject
    {
        [Tooltip("Seed of the combat core's DeterministicRng: same seed, same orders, same truth gives the same run.")]
        public long seed = 1442;
        [Tooltip("Combat tick, seconds. Matches the fixed timestep (50 Hz).")]
        public float tickSeconds = 0.02f;

        [Header("CIC")]
        public FidelityLevel radarLevel;
        public RadarProfileData radar;
        public FidelityLevel tracksLevel;
        public TrackProfileData tracks;
        public FidelityLevel tewaLevel;
        public TewaProfileData tewa;

        [Header("Magazine")]
        public FidelityLevel vlsLevel;
        [Tooltip("Seconds between launches from one VLS group.")]
        public float vlsLaunchIntervalS = 1f;
        public VlsGroupProfile[] vlsGroups = new VlsGroupProfile[0];
        public VlsLoadEntry[] vlsLoads = new VlsLoadEntry[0];
        public FidelityLevel gunsLevel;
        public GunMountProfileData[] guns = new GunMountProfileData[0];

        [Header("Damage Control")]
        public FidelityLevel damageLevel;
        public DamageProfileData damage;

        [Header("Scenario defaults")]
        public Roe defaultRoe = Roe.WeaponsFree;
        public DoctrineDefault[] defaultDoctrine = new DoctrineDefault[0];

        [TextArea(8, 32)] public string provenance;

        /// <summary>The plain loadout the combat core is built from. Fails loudly on a missing missile profile.</summary>
        public CombatLoadout Bake()
        {
            var loads = new VlsMissileLoad[vlsLoads.Length];
            for (int i = 0; i < loads.Length; i++)
            {
                VlsLoadEntry e = vlsLoads[i];
                if (e.profile == null)
                    throw new InvalidOperationException($"Loadout '{name}': VLS load {i} ({e.weapon}) has no missile profile.");
                loads[i] = new VlsMissileLoad
                {
                    Weapon = e.weapon, Profile = e.profile.Bake(), MissileLevel = e.missileLevel, Rounds = e.rounds, PkSingleShot = e.pkSingleShot,
                };
            }
            return new CombatLoadout
            {
                Seed = (ulong)seed,
                TickSeconds = tickSeconds,
                RadarLevel = radarLevel, Radar = radar,
                TracksLevel = tracksLevel, Tracks = tracks,
                TewaLevel = tewaLevel, Tewa = tewa,
                VlsLevel = vlsLevel,
                Vls = new VlsProfileData { LaunchIntervalS = vlsLaunchIntervalS, Groups = vlsGroups, Loads = loads },
                GunsLevel = gunsLevel, Guns = guns,
                DamageLevel = damageLevel, Damage = damage,
                DefaultRoe = defaultRoe,
                DefaultDoctrine = defaultDoctrine,
            };
        }
    }
}
