using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Missile;
using GuidedFury.ScriptableObjects.Profiles;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.DamageControl;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Scenario;

namespace DeepWater.GeneralQuarters.Tests
{
    /// <summary>
    /// A DDG-51 L0 loadout for tests. Numbers are public figures where marked "public", otherwise AUTHORED.
    /// Mount positions use the DDG model's reference empties (mk45 CENTER at z +49.5, CIWS ref at z −25.8).
    /// The real loadout asset (slice 5) carries the same values with provenance text.
    /// </summary>
    internal static class TestLoadouts
    {
        public const string EssmAsset = "Assets/Guided Fury/Examples/Profiles/RIM-162_ESSM.asset";

        public static MissileProfileData Essm() =>
            AssetDatabase.LoadAssetAtPath<MissileProfileSO>(EssmAsset).Bake();

        public static CombatLoadout Ddg51L0(ulong seed = 1442)
        {
            return new CombatLoadout
            {
                Seed = seed,
                TickSeconds = 0.02f,
                Radar = new RadarProfileData
                {
                    InstrumentedRangeM = 185_000f,   // public: SPY-1 "over 100 nmi"; exact figure not public
                    AntennaHeightM = 20f,            // AUTHORED: array centre height above the waterline
                    SweepPeriodTicks = 25,           // AUTHORED: 0.5 s search/track revisit
                },
                Tracks = new TrackProfileData { DropAfterTicks = 150 },
                Tewa = new TewaProfileData
                {
                    EvaluatePeriodTicks = 5,         // 10 Hz
                    ThreatCpaM = 3000f,
                    MissileBands = new[]
                    {
                        new WeaponBand { Weapon = WeaponClass.Sm2, MinRangeM = 20_000f, MaxRangeM = 150_000f, AntiAir = true, MaxInFlightPerTrack = 1 },
                        new WeaponBand { Weapon = WeaponClass.Essm, MinRangeM = 1_500f, MaxRangeM = 30_000f, AntiAir = true, MaxInFlightPerTrack = 1 },
                    },
                },
                Vls = new VlsProfileData
                {
                    LaunchIntervalS = 1f,            // AUTHORED: one launch per second per group
                    Groups = new[]
                    {
                        new VlsGroupProfile { Name = "fore VLS", OffsetShip = new Vector3(0f, 3f, 38f), System = ShipSystem.ForeVls },
                        new VlsGroupProfile { Name = "aft VLS", OffsetShip = new Vector3(0f, 3f, -42f), System = ShipSystem.AftVls },
                    },
                    Loads = new[]
                    {
                        new VlsMissileLoad { Weapon = WeaponClass.Essm, Profile = Essm(), MissileLevel = MissileLod.L2_RateLimited3Dof, Rounds = 32, PkSingleShot = 0.8f },
                    },
                },
                Guns = new[]
                {
                    new GunMountProfileData
                    {
                        Name = "Mk 45", Weapon = WeaponClass.Mk45, System = ShipSystem.Mk45, OffsetShip = new Vector3(0f, 7.7f, 49.5f),
                        FacingDeg = 0f, ArcHalfDeg = 150f, MinRangeM = 1_000f, MaxRangeM = 15_000f, AntiAir = true, AntiSurface = true,
                        ThreatCpaM = 3000f, RoundsPerMinute = 20f, BurstRounds = 1, MuzzleVelocityMps = 808f,   // public: Mk 45 Mod 4, 16-20 rpm, 808 m/s
                        MeanVelocityFraction = 0.6f, MagazineRounds = 600, PkPerBurstAtMinRange = 0.03f, PkPerBurstAtMaxRange = 0.005f,
                    },
                    new GunMountProfileData
                    {
                        Name = "CIWS", Weapon = WeaponClass.Ciws, System = ShipSystem.Ciws, OffsetShip = new Vector3(0f, 12.9f, -25.8f),
                        FacingDeg = 180f, ArcHalfDeg = 150f, MinRangeM = 300f, MaxRangeM = 2_000f, AntiAir = true, SelfDefence = true,
                        ThreatCpaM = 500f, RoundsPerMinute = 4500f, BurstRounds = 100, MuzzleVelocityMps = 1100f,   // public: Phalanx 4 500 rpm, 1 550-round drum
                        MeanVelocityFraction = 0.85f, MagazineRounds = 1550, PkPerBurstAtMinRange = 0.35f, PkPerBurstAtMaxRange = 0.08f,
                    },
                    new GunMountProfileData
                    {
                        Name = "Mk 38 port", Weapon = WeaponClass.Mk38, System = ShipSystem.Mk38Port, OffsetShip = new Vector3(-7f, 5f, -5f),
                        FacingDeg = -90f, ArcHalfDeg = 90f, MinRangeM = 100f, MaxRangeM = 2_500f, AntiSurface = true,
                        ThreatCpaM = 3000f, RoundsPerMinute = 180f, BurstRounds = 10, MuzzleVelocityMps = 1100f,
                        MeanVelocityFraction = 0.8f, MagazineRounds = 400, PkPerBurstAtMinRange = 0.2f, PkPerBurstAtMaxRange = 0.05f,
                    },
                },
                Damage = new DamageProfileData
                {
                    HullHitPoints = 1000f, HullDamagePerWarheadKg = 1f, SystemDamagePerWarheadKg = 1f, SystemReachM = 25f,
                    Systems = new[]
                    {
                        new SystemProfile { System = ShipSystem.Radar, PositionShip = new Vector3(0f, 18f, 15f), HitPoints = 150f },
                        new SystemProfile { System = ShipSystem.ForeVls, PositionShip = new Vector3(0f, 3f, 38f), HitPoints = 150f },
                        new SystemProfile { System = ShipSystem.AftVls, PositionShip = new Vector3(0f, 3f, -42f), HitPoints = 150f },
                        new SystemProfile { System = ShipSystem.Mk45, PositionShip = new Vector3(0f, 7.7f, 49.5f), HitPoints = 150f },
                        new SystemProfile { System = ShipSystem.Ciws, PositionShip = new Vector3(0f, 12.9f, -25.8f), HitPoints = 150f },
                        new SystemProfile { System = ShipSystem.Propulsion, PositionShip = new Vector3(0f, -3f, -20f), HitPoints = 150f },
                    },
                },
                DefaultRoe = Roe.WeaponsFree,
                DefaultDoctrine = new[]
                {
                    new DoctrineDefault { Weapon = WeaponClass.Essm, Mode = DoctrineMode.Auto },
                    new DoctrineDefault { Weapon = WeaponClass.Mk45, Mode = DoctrineMode.Auto },
                    new DoctrineDefault { Weapon = WeaponClass.Ciws, Mode = DoctrineMode.Auto },
                    new DoctrineDefault { Weapon = WeaponClass.Mk38, Mode = DoctrineMode.Auto },
                },
            };
        }

        /// <summary>The same ship at Radar L1 and Tracks L1, with the range loadout's radar and track-file data (Stream A, G61–G68).</summary>
        public static CombatLoadout Ddg51L1(ulong seed = 1442)
        {
            CombatLoadout l = Ddg51L0(seed);
            l.RadarLevel = FidelityLevel.L1;
            l.Radar.L1 = Editor.Ddg51CombatData.RadarL1();
            l.TracksLevel = FidelityLevel.L1;
            l.Tracks.L1 = Editor.Ddg51CombatData.TracksL1();
            return l;
        }

        /// <summary>Sea-skimmers (Harpoon-class: 220 kg warhead is public; speed AUTHORED at ~Mach 0.85) from evenly spread bearings.</summary>
        public static List<RaidThreatSpec> SeaSkimmerRaid(int count, float rangeM = 40_000f, int launchSpacingTicks = 50)
        {
            var specs = new List<RaidThreatSpec>();
            for (int i = 0; i < count; i++)
                specs.Add(new RaidThreatSpec
                {
                    Kind = TargetKind.AntiShipMissile, Identity = Identity.Hostile,
                    BearingDeg = i * 360f / count, RangeM = rangeM, AltitudeM = 5f, SpeedMps = 290f,
                    LaunchTick = i * launchSpacingTicks, RcsDbsm = -10f, WarheadKg = 220f,
                });
            return specs;
        }
    }
}
