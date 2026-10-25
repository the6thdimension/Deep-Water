using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using GuidedFury.Core.Integrators;
using GuidedFury.ScriptableObjects.Profiles;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Captain;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.DamageControl;
using DeepWater.GeneralQuarters.Core.Fidelity;
using DeepWater.GeneralQuarters.Core.Ballistics;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.Scenario;
using DeepWater.GeneralQuarters.Examples;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Editor
{
    /// <summary>
    /// Builds the range's data assets (slice 5): the DDG-51 combat loadout with each number's source, the AI captain's
    /// habits, three exercise raids, the range profile, the stand-in materials and the UI's panel settings.
    /// Idempotent: rerun after changing a number here. Mount positions come from the Blender layout (slice 3).
    /// Public figures are marked "public" with their source; everything else is AUTHORED (G15).
    /// </summary>
    public static class Ddg51CombatData
    {
        const string SoDir = "Assets/General Quarters/ScriptableObjects";
        const string ExampleDir = "Assets/General Quarters/Examples";
        public const string LoadoutAsset = SoDir + "/DDG51_GQ_Loadout.asset";
        public const string AiCaptainAsset = SoDir + "/GQ_AiCaptain.asset";
        public const string RaidDir = ExampleDir + "/Raids";
        public const string RangeAsset = ExampleDir + "/Range/GQ_Range.asset";
        public const string PanelSettingsAsset = ExampleDir + "/UI/GQ_RangePanelSettings.asset";
        public const string ScreenUxml = ExampleDir + "/UI/RangeScreen.uxml";
        public const string FriendlyMaterial = ExampleDir + "/Materials/GQ_StandIn_Friendly.mat";
        public const string HostileMaterial = ExampleDir + "/Materials/GQ_StandIn_Hostile.mat";
        public const string EssmModel = "Assets/rim-162essm/source/ESSM-RIM-162_SH.fbx";
        public const string AircraftModel = "Assets/_AIRCRAFT/F16/F16.fbx";
        const string EssmProfile = "Assets/Guided Fury/Examples/Profiles/RIM-162_ESSM.asset";
        const string AppUiTheme = "Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI.tss";

        [MenuItem("Deep Water/General Quarters/Build Range Assets")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            ShipLoadoutSO loadout = Loadout(File.ReadAllText(Ddg51HullBuilder.LayoutJson));
            AiCaptainProfileSO ai = AiCaptain();
            RaidScenarioSO[] raids = { Raid("Raid_Small", SmallRaid()), Raid("Raid_Standard", StandardRaid()), Raid("Raid_Saturation", SaturationRaid()) };
            RangeProfileSO range = LoadOrCreate<RangeProfileSO>(RangeAsset);
            range.raids = raids;
            range.patrolCorners = new[] { new Vector2(0f, 0f), new Vector2(0f, 8000f), new Vector2(6000f, 8000f), new Vector2(6000f, 0f) };
            range.patrolLaps = 6;
            range.timeScales = new[] { 1f, 2f, 4f };
            range.taoCameraViewport = new Rect(0.64f, 0.5f, 0.36f, 0.44f);
            range.cameraLingerS = 2.5f;
            range.breaches = BreachPresets();
            range.provenance = "AUTHORED: an 8 × 6 km patrol box from the range origin (the DDG starts there, heading 000 at 12 kn), six laps. " +
                               "The TAO camera window matches RangeScreen.uss (.gq-camera-window). " + BreachProvenance;
            EditorUtility.SetDirty(range);
            EnsureMaterial(FriendlyMaterial, new Color(0.86f, 0.88f, 0.9f));
            EnsureMaterial(HostileMaterial, new Color(0.85f, 0.18f, 0.14f));
            BuildPanelSettings();
            AssetDatabase.SaveAssets();
            return $"[GQ] Range assets built: {LoadoutAsset} ({loadout.guns.Length} gun mounts, {loadout.vlsLoads[0].rounds} ESSM), {AiCaptainAsset}, " +
                   $"{raids.Length} raids in {RaidDir}, {RangeAsset}, {PanelSettingsAsset}.";
        }

        // ── Loadout ─────────────────────────────────────────────────────

        static ShipLoadoutSO Loadout(string json)
        {
            Vector3 mk45 = Point(json, "mk45", "train_pivot");
            Vector3 ciwsFwd = Point(json, "ciws_Fwd", "train_pivot");
            Vector3 ciwsAft = Point(json, "ciws_Aft", "train_pivot");
            Vector3 mk38Port = Point(json, "mk38_Port", "pivot");
            Vector3 mk38Stbd = Point(json, "mk38_Stbd", "pivot");
            Vector3 vlsFore = new Vector3(0f, 7.85f, 42.24f), vlsAft = new Vector3(0f, 7.12f, -38.87f);   // centres of the layout's cell rows

            ShipLoadoutSO so = LoadOrCreate<ShipLoadoutSO>(LoadoutAsset);
            so.seed = 1442;
            so.tickSeconds = 0.02f;
            so.radarLevel = so.tracksLevel = so.tewaLevel = so.vlsLevel = FidelityLevel.L0;
            so.gunsLevel = FidelityLevel.L1;   // Stream B slice B1 (G34)
            so.radar = new RadarProfileData { InstrumentedRangeM = 185_000f, AntennaHeightM = 20f, SweepPeriodTicks = 25 };
            so.tracks = new TrackProfileData { DropAfterTicks = 150 };
            so.tewa = new TewaProfileData
            {
                EvaluatePeriodTicks = 5,
                ThreatCpaM = 3000f,
                MissileBands = new[]
                {
                    new WeaponBand { Weapon = WeaponClass.Sm2, MinRangeM = 20_000f, MaxRangeM = 150_000f, AntiAir = true, MaxInFlightPerTrack = 1 },
                    new WeaponBand { Weapon = WeaponClass.Essm, MinRangeM = 1_500f, MaxRangeM = 30_000f, AntiAir = true, MaxInFlightPerTrack = 1 },
                },
            };
            so.vlsLaunchIntervalS = 1f;
            so.vlsGroups = new[]
            {
                new VlsGroupProfile { Name = "fore VLS", OffsetShip = vlsFore, System = ShipSystem.ForeVls },
                new VlsGroupProfile { Name = "aft VLS", OffsetShip = vlsAft, System = ShipSystem.AftVls },
            };
            var essm = AssetDatabase.LoadAssetAtPath<MissileProfileSO>(EssmProfile);
            if (essm == null) throw new FileNotFoundException("ESSM profile missing", EssmProfile);
            so.vlsLoads = new[] { new VlsLoadEntry { weapon = WeaponClass.Essm, profile = essm, missileLevel = MissileLod.L2_RateLimited3Dof, rounds = 32, pkSingleShot = 0.8f } };
            so.guns = new[]
            {
                Gun("Mk 45", WeaponClass.Mk45, ShipSystem.Mk45, mk45, 0f, 150f, 1_000f, 15_000f, true, true, false, 3000f, 20f, 1, 808f, 0.6f, 600, 0.03f, 0.005f, Mk45L1),
                Gun("CIWS fwd", WeaponClass.Ciws, ShipSystem.Ciws, ciwsFwd, 0f, 150f, 300f, 2_000f, true, false, true, 500f, 4500f, 100, 1100f, 0.85f, 1550, 0.35f, 0.08f, CiwsL1),
                Gun("CIWS aft", WeaponClass.Ciws, ShipSystem.Ciws, ciwsAft, 180f, 150f, 300f, 2_000f, true, false, true, 500f, 4500f, 100, 1100f, 0.85f, 1550, 0.35f, 0.08f, CiwsL1),
                Gun("Mk 38 port", WeaponClass.Mk38, ShipSystem.Mk38Port, mk38Port, -90f, 90f, 100f, 2_500f, false, true, false, 3000f, 180f, 10, 1100f, 0.8f, 400, 0.2f, 0.05f, Mk38L1),
                Gun("Mk 38 stbd", WeaponClass.Mk38, ShipSystem.Mk38Starboard, mk38Stbd, 90f, 90f, 100f, 2_500f, false, true, false, 3000f, 180f, 10, 1100f, 0.8f, 400, 0.2f, 0.05f, Mk38L1),
            };
            so.damage = new DamageProfileData
            {
                HullHitPoints = 1000f, HullDamagePerWarheadKg = 1f, SystemDamagePerWarheadKg = 1f, SystemReachM = 25f,
                Systems = new[]
                {
                    new SystemProfile { System = ShipSystem.Radar, PositionShip = new Vector3(0f, 18f, 15f), HitPoints = 150f },
                    new SystemProfile { System = ShipSystem.ForeVls, PositionShip = vlsFore, HitPoints = 150f },
                    new SystemProfile { System = ShipSystem.AftVls, PositionShip = vlsAft, HitPoints = 150f },
                    new SystemProfile { System = ShipSystem.Mk45, PositionShip = mk45, HitPoints = 150f },
                    new SystemProfile { System = ShipSystem.Ciws, PositionShip = ciwsAft, HitPoints = 150f },
                    new SystemProfile { System = ShipSystem.Mk38Port, PositionShip = mk38Port, HitPoints = 100f },
                    new SystemProfile { System = ShipSystem.Mk38Starboard, PositionShip = mk38Stbd, HitPoints = 100f },
                    new SystemProfile { System = ShipSystem.Propulsion, PositionShip = new Vector3(0f, -3f, -20f), HitPoints = 150f },
                },
            };
            so.defaultRoe = Roe.WeaponsTight;
            so.defaultDoctrine = new[]
            {
                new DoctrineDefault { Weapon = WeaponClass.Essm, Mode = DoctrineMode.SemiAuto },
                new DoctrineDefault { Weapon = WeaponClass.Mk45, Mode = DoctrineMode.Auto },
                new DoctrineDefault { Weapon = WeaponClass.Ciws, Mode = DoctrineMode.Auto },
                new DoctrineDefault { Weapon = WeaponClass.Mk38, Mode = DoctrineMode.Auto },
            };
            so.provenance =
                "DDG-51 Flight I/II combat loadout, L0 (slice 5). Public figures are marked; the rest is AUTHORED (G15).\n" +
                "SEED 1442, tick 0.02 s (50 Hz, the fixed timestep).\n" +
                "RADAR (SPY-1D) L0: instrumented range 185 km (public: SPY-1 'over 100 nmi'; the exact figure is not public, AUTHORED). " +
                "Array height 20 m above the waterline (AUTHORED). Revisit 0.5 s (AUTHORED).\n" +
                "TRACKS L0: drop after 3 s silent (AUTHORED).\n" +
                "TEWA L0: evaluates at 10 Hz; a threat is a track closing within 3 km (AUTHORED). Bands: SM-2 20-150 km (none loaded), ESSM 1.5-30 km " +
                "(AUTHORED; public ESSM reach is longer, but a sea-skimmer clears the radar horizon at about 27.6 km).\n" +
                "VLS (Mk 41) L0: 32 fore and 64 aft cells (public, Flight I/II; positions from the Blender layout). 32 ESSM, quad-packed (public: 4 per cell; " +
                "the count is AUTHORED). One launch per second per group (AUTHORED). ESSM flies at Guided Fury L2; Pk 0.8 single shot (AUTHORED).\n" +
                "Mk 45 Mod 4: 16-20 rounds/min and 808 m/s (public, NAVSEA fact file); 600 rounds (AUTHORED); arc ±150° from the bow, 1-15 km, Pk per round " +
                "0.03 falling to 0.005 (AUTHORED).\n" +
                "CIWS (Phalanx) ×2, forward and aft (public: Flight I/II carries two): 4,500 rounds/min and a 1,550-round drum (public, NAVSEA fact file); " +
                "300-2,000 m, 100-round bursts, Pk per burst 0.35 falling to 0.08 (AUTHORED). Both are Damage Control's one CIWS system at L0.\n" +
                "Mk 38 ×2 (25 mm), port and starboard: about 180 rounds/min (public, M242); beam arcs ±90°, 100-2,500 m, surface targets only (AUTHORED).\n" +
                "GUNS L1 (slice B1, G35-G41; the L0 numbers above still drive target choice, rate of fire and magazines). Drag: the BRL G7 curve (McCoy, " +
                "ARBRL-TR-02293) times a form factor CALIBRATED per projectile; ISA atmosphere; no wind. Sources: NavWeaps (navweaps.com) gun pages, read 2026-10-24.\n" +
                "  Mk 45 5\"/54 (Flight I/II; the earlier 'Mod 4' above is the Flight IIA 5\"/62): HE-CVT 31.1 kg (Mk 68/127), 127 mm, 808 m/s with the Mk 67 cartridge, " +
                "elevation -15/+65°, train 30°/s, elevation 20°/s (public). Form factor 0.9251 CALIBRATED to 23,130 m at 47° (public); VALIDATION: greatest range comes " +
                "at 48.0° (published 47°). Proximity fuze against air: lethal radius 8 m, fuze 0.9 (AUTHORED); 0.3 a hit against surface targets (AUTHORED). " +
                "Dispersion 2 mrad, solution latency 0.05 s, laid within 0.5° (AUTHORED).\n" +
                "  CIWS Mk 149 APDS: penetrator 0.10 kg, 12.75 mm, 1,113 m/s; Block 1B elevation -25/+85°, train and elevation 115°/s (public). Form factor 3.2484 " +
                "CALIBRATED to 5,500 m at 45° (public). A form factor that far from 1 says the G7 shape fits this penetrator poorly; no public figure checks the " +
                "curve between muzzle and maximum range. 0.3 a hit against air, 0.05 against surface (AUTHORED). Closed-loop spotting: bias 3 mrad, time constant " +
                "0.5 s (AUTHORED; Phalanx spots its own rounds, public, but no figure is). Dispersion 2 mrad, latency 0.02 s, laid within 1° (AUTHORED).\n" +
                "  Mk 38 25 mm (M242) HEI-T: 0.184 kg, 25 mm, 1,100 m/s, elevation -20/+55° (public, Mk 88 mounting). Form factor 1.2057 CALIBRATED to the 6,800 m " +
                "maximum range (public; the elevation is not given: greatest range over the mount's elevations, at 37.5°). Train and elevation 60°/s (AUTHORED: " +
                "not public). 0.2 a hit against air, 0.15 against surface (AUTHORED). Dispersion 1.5 mrad, latency 0.1 s, laid within 1° (AUTHORED).\n" +
                "DAMAGE CONTROL L0: hull 1,000 hit points, 1 per kg of warhead; systems 100-150 hit points within 25 m of the hit (AUTHORED: no public damage table).\n" +
                "SCENARIO DEFAULTS: ROE weapons tight; ESSM semi-automatic (TEWA proposes, the TAO confirms), guns automatic, CIWS automatic (G14).\n" +
                DamageControlL1Provenance;
            ApplyDamageControl(so);
            EditorUtility.SetDirty(so);
            return so;
        }

        /// <summary>
        /// The range's scripted breaches (G57), the cases Damage Control L1's tests are gated on: one wing (list), one bow hold
        /// (trim), three holds amidships (within the margin) and seven stations (past it). Public so they can be applied alone.
        /// </summary>
        public static BreachPreset[] BreachPresets()
        {
            BreachHole H(float x, float y, float z, float m2) => new BreachHole { pointShip = new Vector3(x, y, z), areaM2 = m2 };
            BreachHole[] Gash(int firstStation, int stations)
            {
                var holes = new List<BreachHole>();
                for (int st = firstStation; st < firstStation + stations; st++)
                {
                    float z = 70f - 12f * st;   // mid-station
                    holes.Add(H(0f, -4f, z, 3f));
                    holes.Add(H(7f, -2f, z, 2f));
                    holes.Add(H(-7f, -2f, z, 2f));
                }
                return holes.ToArray();
            }
            return new[]
            {
                new BreachPreset { name = "Wing 06 S", holes = new[] { H(8f, -3f, 10f, 2f) } },
                new BreachPreset { name = "Hold 02", holes = new[] { H(0f, -4f, 58f, 2f) } },
                new BreachPreset { name = "Holds 05-07", holes = Gash(4, 3) },
                new BreachPreset { name = "Holds 03-09", holes = Gash(2, 7) },
            };
        }

        const string BreachProvenance =
            "Breach presets (AUTHORED scenario damage, G57): starboard wing 06 (2 m², 3 m below the waterline), hold 02 (2 m², 4 m down), " +
            "holds 05-07 and their wings (within the margin), holds 03-09 and their wings (past it: she sinks).";

        /// <summary>
        /// Damage Control L1 (Stream C slice C1, G53-G60): the level, the hull whose spaces flood, and the water and loss tuning.
        /// Public so the values can be applied to the loadout asset without rebuilding everything.
        /// </summary>
        public static void ApplyDamageControl(ShipLoadoutSO so)
        {
            so.damageLevel = FidelityLevel.L1;
            so.hull = AssetDatabase.LoadAssetAtPath<ShipHullProfileSO>(Ddg51HullBuilder.HullAsset);
            if (so.hull == null) throw new FileNotFoundException("hull profile missing: run Build DDG-51 Hull", Ddg51HullBuilder.HullAsset);
            so.flooding = new FloodingTuning
            {
                seawaterDensityKgM3 = 1025f, gravity = 9.81f, dischargeCoefficient = 0.6f,
                holeAreaAtRefM2 = 13.9f, holeRefWarheadKg = 165f,
                capsizeHeelDeg = 45f, capsizeHoldS = 5f,
            };
            if (!so.provenance.Contains("DAMAGE CONTROL L1")) so.provenance += "\n" + DamageControlL1Provenance;
            EditorUtility.SetDirty(so);
        }

        public const string DamageControlL1Provenance =
            "DAMAGE CONTROL L1 (G53-G60): the hull's spaces are in the hull profile (its SUBDIVISION and OPENINGS). Seawater 1,025 kg/m³ " +
            "(matches Sea State) and g 9.81. Orifice flow Q = Cd·A·√(2g·Δh) with Cd 0.6, the textbook sharp-edged orifice (public). A hit's " +
            "hole: area = 13.9 m² × (warhead / 165 kg)^(2/3), CALIBRATED to one public figure: USS Stark, 17 May 1987, an Exocet AM39 with a " +
            "165 kg warhead left a hole about 10 × 15 ft (3.0 × 4.6 m) in her port side, above the waterline; the 2/3 power is cube-root blast " +
            "scaling (AUTHORED as the law). FLAGGED: one figure. CAPSIZED past 45° held for 5 s (AUTHORED). SINKING when the water aboard leaves " +
            "the hull's whole volume less buoyancy than her weight (G60). Hull hit points still damage systems and no longer decide loss.";

        static GunMountProfileData Gun(string name, WeaponClass weapon, ShipSystem system, Vector3 at, float facing, float arcHalf, float minRange, float maxRange,
            bool antiAir, bool antiSurface, bool selfDefence, float threatCpa, float rpm, int burst, float muzzle, float meanFraction, int magazine,
            float pkMin, float pkMax, GunBallisticsData ballistics) => new GunMountProfileData
        {
            Name = name, Weapon = weapon, System = system, OffsetShip = at, FacingDeg = facing, ArcHalfDeg = arcHalf, MinRangeM = minRange, MaxRangeM = maxRange,
            AntiAir = antiAir, AntiSurface = antiSurface, SelfDefence = selfDefence, ThreatCpaM = threatCpa, RoundsPerMinute = rpm, BurstRounds = burst,
            MuzzleVelocityMps = muzzle, MeanVelocityFraction = meanFraction, MagazineRounds = magazine, PkPerBurstAtMinRange = pkMin, PkPerBurstAtMaxRange = pkMax,
            Ballistics = ballistics,
        };

        // Guns L1 data (G35-G41); every number's source is in the loadout provenance. The form factors are CALIBRATED with
        // FiringTable.CalibrateFormFactor; GunsL1Tests re-derives each published range from them.
        public static readonly GunBallisticsData Mk45L1 = new GunBallisticsData
        {
            Projectile = new ProjectileData { MassKg = 31.1f, DiameterM = 0.127f, MuzzleVelocityMps = 808f, FormFactor = 0.9251f },
            ElevationMinDeg = -15f, ElevationMaxDeg = 65f, TrainRateDps = 30f, ElevateRateDps = 20f, LayToleranceDeg = 0.5f,
            DispersionMrad = 2f, SolutionLatencyS = 0.05f, Fuze = FuzeKind.ProximityVsAir, LethalRadiusM = 8f, FuzeReliability = 0.9f,
            PkPerHitAir = 0.9f, PkPerHitSurface = 0.3f,
        };

        public static readonly GunBallisticsData CiwsL1 = new GunBallisticsData
        {
            Projectile = new ProjectileData { MassKg = 0.10f, DiameterM = 0.01275f, MuzzleVelocityMps = 1113f, FormFactor = 3.2484f },
            ElevationMinDeg = -25f, ElevationMaxDeg = 85f, TrainRateDps = 115f, ElevateRateDps = 115f, LayToleranceDeg = 1f,
            DispersionMrad = 2f, SolutionLatencyS = 0.02f, Fuze = FuzeKind.Impact, PkPerHitAir = 0.3f, PkPerHitSurface = 0.05f,
            ClosedLoopSpotting = true, SpotBiasMrad = 3f, SpotTimeConstantS = 0.5f,
        };

        public static readonly GunBallisticsData Mk38L1 = new GunBallisticsData
        {
            Projectile = new ProjectileData { MassKg = 0.184f, DiameterM = 0.025f, MuzzleVelocityMps = 1100f, FormFactor = 1.2057f },
            ElevationMinDeg = -20f, ElevationMaxDeg = 55f, TrainRateDps = 60f, ElevateRateDps = 60f, LayToleranceDeg = 1f,
            DispersionMrad = 1.5f, SolutionLatencyS = 0.1f, Fuze = FuzeKind.Impact, PkPerHitAir = 0.2f, PkPerHitSurface = 0.15f,
        };

        /// <summary>A named point from the layout's "mounts" block (JsonUtility cannot read that free-form block).</summary>
        static Vector3 Point(string json, string mount, string key)
        {
            int m = json.IndexOf($"\"{mount}\"", System.StringComparison.Ordinal);
            int k = m < 0 ? -1 : json.IndexOf($"\"{key}\"", m, System.StringComparison.Ordinal);
            if (k < 0) throw new InvalidDataException($"layout has no mounts.{mount}.{key}");
            int open = json.IndexOf('[', k), close = json.IndexOf(']', open);
            string[] parts = json.Substring(open + 1, close - open - 1).Split(',');
            float F(int i) => float.Parse(parts[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);
            return new Vector3(F(0), F(1), F(2));
        }

        // ── AI captain ───────────────────────────────────────────────────

        static AiCaptainProfileSO AiCaptain()
        {
            AiCaptainProfileSO so = LoadOrCreate<AiCaptainProfileSO>(AiCaptainAsset);
            so.profile = new AiCaptainProfileData
            {
                EvaluatePeriodTicks = 25, RouteId = 1, RouteSpeedKn = 20f, ThreatBell = EngineBell.AheadFlank,
                ArcHeadingStepDeg = 15f, ArcHeadingSteps = 6, ArcMinGain = 1, ArcTurnIntervalS = 20f, SecureAfterS = 30f, ConfirmDelayS = 2f,
            };
            so.provenance = "AUTHORED (the AI captain's habits, G19): looks twice a second; patrols at 20 kn (C1); rings flank on the first inbound hostile (C2); " +
                            "turns in 15° steps, up to 90° either way, when that brings at least one more (threat, mount) pair into arc, at most every 20 s (C3); " +
                            "secures after 30 s with nothing inbound (C4); as TAO, confirms a SemiAuto proposal after it has stood 2 s (T1).";
            EditorUtility.SetDirty(so);
            return so;
        }

        // ── Raids (AUTHORED exercise raids) ───────────────────────────────

        static RaidThreatSpec SeaSkimmer(float bearing, float rangeM, float launchS) => new RaidThreatSpec
        {
            Kind = TargetKind.AntiShipMissile, Identity = Identity.Hostile, BearingDeg = bearing, RangeM = rangeM, AltitudeM = 5f, SpeedMps = 290f,
            LaunchTick = Mathf.RoundToInt(launchS / 0.02f), RcsDbsm = -10f, WarheadKg = 220f,
            LengthM = 4.6f, DiameterM = 0.34f,   // Harpoon-class (public), for guns L1's presented area
        };

        static (string name, RaidThreatSpec[] threats, string provenance) SmallRaid() => ("Small 4", new[]
        {
            SeaSkimmer(30f, 40_000f, 0f), SeaSkimmer(40f, 40_000f, 4f), SeaSkimmer(50f, 40_000f, 8f), SeaSkimmer(60f, 40_000f, 12f),
        }, "4 sea-skimmers from 030-060°, 40 km, 4 s apart.");

        static (string name, RaidThreatSpec[] threats, string provenance) StandardRaid()
        {
            var t = new List<RaidThreatSpec>();
            float[] wave1 = { 50f, 55f, 65f, 70f }, wave2 = { 290f, 295f, 305f, 310f };
            for (int i = 0; i < 4; i++) t.Add(SeaSkimmer(wave1[i], 40_000f, i * 3f));
            for (int i = 0; i < 4; i++) t.Add(SeaSkimmer(wave2[i], 40_000f, 40f + i * 3f));
            for (int i = 0; i < 2; i++)
                t.Add(new RaidThreatSpec
                {
                    Kind = TargetKind.Aircraft, Identity = Identity.Hostile, BearingDeg = i == 0 ? 0f : 20f, RangeM = 80_000f, AltitudeM = 6_000f,
                    SpeedMps = 250f, LaunchTick = Mathf.RoundToInt((10f + i * 5f) / 0.02f), RcsDbsm = 5f, WarheadKg = 0f,
                    LengthM = 15f, DiameterM = 3f,   // a strike fighter's length and fuselage depth (AUTHORED)
                });
            return ("Standard 8+2", t.ToArray(),
                "Two waves of 4 sea-skimmers, from 050-070° then 290-310° 40 s later, 3 s apart; 2 strike aircraft at 6 km altitude from 000° and 020°, 80 km out, " +
                "flying a pass over the ship's starting position. They pass 6 km up, so TEWA L0 never ranks them a threat (closest approach over 3 km): " +
                "they are the TAO's to engage by hand.");
        }

        static (string name, RaidThreatSpec[] threats, string provenance) SaturationRaid()
        {
            var t = new RaidThreatSpec[24];
            for (int i = 0; i < t.Length; i++) t[i] = SeaSkimmer(i * 15f, 40_000f, i * 1f);
            return ("Saturation 24", t, "24 sea-skimmers from every 15°, 40 km, 1 s apart: the Phase 1 budget case (G16), the same raid as the headless 24-threat test.");
        }

        static RaidScenarioSO Raid(string file, (string name, RaidThreatSpec[] threats, string provenance) spec)
        {
            RaidScenarioSO so = LoadOrCreate<RaidScenarioSO>($"{RaidDir}/{file}.asset");
            so.displayName = spec.name;
            so.threats = spec.threats;
            so.provenance = "AUTHORED exercise raid. Sea-skimmers: Harpoon-class (220 kg warhead, public), 290 m/s (AUTHORED, about Mach 0.85), 5 m, RCS −10 dBsm. " + spec.provenance;
            EditorUtility.SetDirty(so);
            return so;
        }

        // ── Look ───────────────────────────────────────────────────────────

        static void EnsureMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                mat = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial);   // the pipeline's default (AP5), never Shader.Find
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
        }

        static void BuildPanelSettings()
        {
            var ps = LoadOrCreate<PanelSettings>(PanelSettingsAsset);
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AppUiTheme);
            if (theme == null) throw new FileNotFoundException("App UI theme missing (is com.unity.dt.app-ui installed?)", AppUiTheme);
            ps.themeStyleSheet = theme;
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.clearColor = false;
            EditorUtility.SetDirty(ps);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
