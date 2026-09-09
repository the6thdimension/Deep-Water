#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using GuidedFury.Core.Aero;
using GuidedFury.Core.Autopilot;
using GuidedFury.Core.Guidance;
using GuidedFury.Core.Integrators;
using GuidedFury.Core.Propulsion;
using GuidedFury.Core.Seekers;
using GuidedFury.ScriptableObjects.Profiles;

namespace GuidedFury.Examples.Editor
{
    /// <summary>
    /// One-shot authoring command that builds the RIM-162 ESSM as a fully-configured L4
    /// missile profile asset. All field values are sourced from publicly-available reference
    /// data for the RIM-162 (Evolved Sea Sparrow Missile) family; where exact values aren't
    /// published, the chosen values are tuned to produce visually plausible flight characteristics
    /// for a slender-body solid-rocket SAM in the 280 kg / Mach 4+ / 50 km class.
    ///
    /// **Real-world reference points used:**
    /// - Mass at launch: ~280 kg total
    /// - Length: 3.66 m, body diameter 25.4 cm (0.25 m), cross-section area ~0.0506 m²
    /// - Propulsion: Mk 134 single-stage solid-rocket motor, ~6 s burn (modeled as
    ///   ConstantBoost, not BoostSustain — ESSM is genuinely single-stage)
    /// - Top speed: Mach 4+ (~1370 m/s at sea level)
    /// - Max range: ~50 km (50 000 m)
    /// - Maneuverability: 50 g (the airframe is rated very high; we cap at 50 here)
    /// - Warhead: 39 kg blast-fragmentation with proximity fuze
    /// - Guidance: SARH with mid-course command updates (we model with the closest equivalent
    ///   currently available — ProNav + cone seeker; SARH proper is Phase 5+)
    ///
    /// **Mach-dependent aero curves** are hand-authored from slender-body theory + ESSM
    /// references. Real CFD fitting is days of work per missile; these produce believable
    /// flight characteristics including transonic drag rise.
    ///
    /// **Re-running the command** prompts before overwriting an existing asset — safe to
    /// re-run when you want to reset tuning to the authored defaults.
    /// </summary>
    public static class EssmProfileBuilder
    {
        private const string AssetPath = "Assets/Guided Fury/Examples/Profiles/RIM-162_ESSM.asset";

        [MenuItem("Guided Fury/Authored Missiles/Build RIM-162 ESSM Profile")]
        public static void BuildEssm()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MissileProfileSO>(AssetPath);
            if (existing != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "ESSM Profile Exists",
                    $"A profile already exists at\n  {AssetPath}\n\nOverwrite with authored defaults? " +
                    "(any manual tuning you've done will be lost — but recoverable from version control.)",
                    "Overwrite",
                    "Cancel");
                if (!overwrite) return;
                ApplyEssmValues(existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                FocusAsset(existing);
                Debug.Log($"[GuidedFury ESSM] Refreshed {AssetPath} with authored defaults.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var profile = ScriptableObject.CreateInstance<MissileProfileSO>();
            ApplyEssmValues(profile);
            AssetDatabase.CreateAsset(profile, AssetPath);
            AssetDatabase.SaveAssets();
            FocusAsset(profile);

            Debug.Log(
                $"[GuidedFury ESSM] Created {AssetPath}.\n" +
                "Next steps:\n" +
                "  1. Drop this profile onto the Launcher's GuidedFury_TestRunner in the missile range.\n" +
                "  2. Set the runner's LOD to L4_FullAero6Dof.\n" +
                "  3. Spawn a target (Scenario picker → any preset) and fire.\n" +
                "  4. Inspect the curves in the Inspector — Cd should peak around Mach 1.05.");
        }

        // ===========================================================================
        // Authored values
        // ===========================================================================

        private static void ApplyEssmValues(MissileProfileSO p)
        {
            // -- Identification ------------------------------------------------
            p.missileId   = "RIM-162";
            p.displayName = "RIM-162 Evolved Sea Sparrow Missile";
            p.description =
                "USN/NATO medium-range surface-to-air missile. Slender body, aft-control fins, " +
                "single-stage solid rocket motor (Mk 134), SARH guidance with TVM. Reference values " +
                "from publicly-available data; aero curves hand-authored to slender-body norms.";
            p.icon = null;

            // -- Mass properties (RIM-162 family ~280 kg total at launch) ------
            p.dryMassKg        = 150f;   // airframe + warhead + electronics
            p.propellantMassKg = 130f;   // solid propellant in Mk 134 motor

            // -- Propulsion ----------------------------------------------------
            // ESSM is genuinely single-stage (boost only). Mk 134 burns ~6 s.
            // Thrust value chosen to deliver realistic burnout speed (~Mach 3 at sea level)
            // given the mass and burn time. Real published thrust values vary by source.
            p.boostThrustN     = 80000f;  // ~8 tonnes-force; tuned for ~Mach 3 burnout
            p.boostDurationS   = 6f;
            p.thrustModel      = ThrustModelKind.ConstantBoost;
            p.sustainThrustN   = 0f;
            p.sustainDurationS = 0f;

            // -- L0 kinematic fallback ----------------------------------------
            // Initial speed: RIM-162 is launched cold (VLS), so cruise speed is the speed
            // after first ignition. We pick something representative; L4 overrides this anyway.
            p.cruiseSpeedMps = 200f;
            p.l0UseGravity   = false;

            // -- L1 scalar aerodynamics (used by L1/L2/L3; L4 uses tables) -----
            // Cross-section area: π × (D/2)² = π × 0.127² ≈ 0.0506 m²
            p.dragCoefficient = 0.35f;    // scalar fallback — L4 uses Mach-aware curve
            p.referenceAreaM2 = 0.0506f;

            // -- L2 maneuver limits -------------------------------------------
            // ESSM is rated 50 g+. Real fleet doctrine occasionally lower for safe handling.
            p.maxLoadFactorG       = 50f;
            p.maxTurnRateDegPerSec = 90f; // aggressive — ~50 g pull at Mach 3 gives this rate

            // -- L3 rigid-body inertias + scalar lift -------------------------
            // Slender body 3.66 m, mass concentrated near CG. Real numbers not published;
            // these produce realistic-feeling rotational dynamics.
            p.transverseInertiaKgM2 = 50f;   // pitch + yaw
            p.rollInertiaKgM2       = 2.5f;  // ~m·r²/2 with m=280 kg, r=0.127 m
            p.liftSlopePerRad       = 8f;    // slender body theory ~2π
            p.stallAoaDeg           = 25f;
            p.weatherVaneCoefficient = 5f;
            p.autopilotGain         = 6f;

            // -- L4 Full Aero --------------------------------------------------
            p.aeroModel = AeroModelKind.Tabulated;
            p.lengthM   = 3.66f;

            p.cdVsMach     = BuildCdCurve();
            p.clAlphaVsMach = BuildClAlphaCurve();
            p.cmAlphaVsMach = BuildCmAlphaCurve();
            p.cmDeltaVsMach = BuildCmDeltaCurve();

            // -- L4 Autopilot + Control Surfaces ------------------------------
            p.autopilot = AutopilotKind.SurfaceDeflection;
            p.maxControlDeflectionDeg = 25f;     // aggressive fin authority
            p.maxControlRateDegPerSec = 500f;    // fast servos for terminal maneuvering

            // -- Seeker --------------------------------------------------------
            // Real ESSM is SARH; we model with a wide-FOV cone seeker until SARH ships.
            p.seekerKind            = SeekerKind.ConeSeeker;
            p.seekerFovDeg          = 30f;
            p.seekerMaxRangeM       = 50000f;     // ~50 km published max range
            p.seekerAcquisitionTimeS = 0.3f;
            p.seekerCoastTimeS       = 1.0f;      // track memory through the terminal LOS swing
            p.seekerMidcourseDatalink = true;     // ESSM is SARH with midcourse datalink (TVM); enables VLS tip-over

            // -- Guidance ------------------------------------------------------
            p.guidanceLaw = GuidanceLawKind.PursuitThenProNav; // pitch-over capable: pursuit off-course, PN terminal
            p.navigationGain = 3.5f;              // standard PN gain for SARH systems

            // -- Lifetime + Fuzing --------------------------------------------
            p.maxLifetimeS         = 60f;          // long enough to fly out to 50 km
            p.fuzeProximityRadiusM = 10f;          // 39 kg blast-frag warhead; lethal radius ~10 m
            p.fuzeArmDelayS        = 1.0f;         // safe-and-arm delay clears the launcher
        
            // -- Detonation VFX ------------------------------------------------
            // Defaults picked from packs already in the project. These are plain
            // Inspector fields on the SO -- swap them on the asset any time; the
            // behaviour falls back to whichever one is wired if the other is null.
            // NOTE: re-running this builder resets them to these defaults (Pat4).
            p.explosionPrefabAerial = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/DAVFX/Realistic 6D Lighting Aerial Explosions/HDRP/Prefabs/Air Explosion 1.prefab");
            p.explosionPrefabGround = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_VFX/HD Explosion/Prefabs/HD_Naval_Explosion.prefab");
            if (p.explosionPrefabAerial == null)
                Debug.LogWarning("[EssmProfileBuilder] Aerial explosion prefab not found -- detonations will use the ground FX (or none).");
            if (p.explosionPrefabGround == null)
                Debug.LogWarning("[EssmProfileBuilder] Ground explosion prefab not found -- detonations will use the aerial FX (or none).");
            p.explosionLifetimeS = 6f;
            p.aerialAltitudeThresholdM = 5f;

            // -- Physical blast ------------------------------------------------
            // ESSM carries a 39 kg blast-fragmentation warhead. 25 m is a plausible
            // severe-effects radius for knockback purposes (lethal fragment radius is
            // larger, but we model the *shove*, not the frag pattern).
            p.blastRadiusM         = 25f;
            p.blastImpulseNs       = 20000f;
            p.blastUpwardsModifier = 1.5f;
}

        // ===========================================================================
        // Curve builders — hand-authored Mach-dependent aero
        // ===========================================================================

        /// <summary>
        /// Cd(Mach) — drag coefficient. Slender body has low subsonic drag, transonic spike
        /// near M=1.05 (the "sonic boom" drag rise), supersonic drop, then gradual climb at
        /// very high Mach.
        /// </summary>
        private static AnimationCurve BuildCdCurve()
        {
            var c = new AnimationCurve(
                new Keyframe(0f,    0.25f),
                new Keyframe(0.7f,  0.30f),
                new Keyframe(0.9f,  0.45f),  // transonic rise begins
                new Keyframe(1.05f, 0.65f),  // transonic peak (signature drag rise)
                new Keyframe(1.3f,  0.55f),
                new Keyframe(2.0f,  0.42f),
                new Keyframe(3.0f,  0.36f),
                new Keyframe(4.0f,  0.33f),
                new Keyframe(5.0f,  0.32f));
            SmoothAll(c);
            return c;
        }

        /// <summary>
        /// Cl_α(Mach) per radian. Slender body lift slope is ~2π subsonic, drops past M=1.
        /// </summary>
        private static AnimationCurve BuildClAlphaCurve()
        {
            // Effective normal-force slope for the WHOLE airframe (body + strakes + tail),
            // referenced to body cross-section area, per radian. The previous values
            // (8 -> 3.5) were body-alone slender-body theory, which caps the airframe at
            // ~9 g at Mach 1.5 -- a 50 g-class SAM could not physically make its rated
            // maneuvers and overflew short-range targets (diagnosed 2026-09-08: 44 m miss
            // on a stationary ground target at 600 m with guidance saturated). Real
            // tail-controlled missiles get 2-3x body-alone normal force from lifting
            // surfaces; these values give ~50 g at Mach 3 at ~20 deg trim AoA.
            var c = new AnimationCurve(
                new Keyframe(0f,   16.0f),
                new Keyframe(0.7f, 17.0f),
                new Keyframe(0.9f, 18.0f),
                new Keyframe(1.05f, 15.0f),
                new Keyframe(2.0f, 12.0f),
                new Keyframe(3.0f, 10.5f),
                new Keyframe(4.0f, 9.5f),
                new Keyframe(5.0f, 9.0f));
            SmoothAll(c);
            return c;
        }

        /// <summary>
        /// Cm_α(Mach) per radian. **NEGATIVE for a stable airframe** — body returns to
        /// alignment with velocity. ESSM has aft-mounted fins that produce this stability.
        /// Stability tends to peak transonic (more area exposed) and decrease at very high
        /// Mach as control surface effectiveness drops.
        /// </summary>
        private static AnimationCurve BuildCmAlphaCurve()
        {
            var c = new AnimationCurve(
                new Keyframe(0f,   -2.5f),
                new Keyframe(0.9f, -2.8f),
                new Keyframe(1.05f, -3.0f),  // most stable here
                new Keyframe(2.0f, -2.5f),
                new Keyframe(3.0f, -2.2f),
                new Keyframe(4.0f, -2.0f),
                new Keyframe(5.0f, -1.8f));
            SmoothAll(c);
            return c;
        }

        /// <summary>
        /// Cm_δ(Mach) per radian. **NEGATIVE by sign convention** — positive deflection
        /// produces nose-down moment (autopilot accounts for sign). Magnitude is high
        /// subsonic (fins are effective) and drops at high Mach (less effective deflection
        /// for the same surface size).
        /// </summary>
        private static AnimationCurve BuildCmDeltaCurve()
        {
            var c = new AnimationCurve(
                new Keyframe(0f,   -3.5f),
                new Keyframe(0.7f, -3.5f),
                new Keyframe(1.05f, -3.0f),
                new Keyframe(2.0f, -2.0f),
                new Keyframe(3.0f, -1.5f),
                new Keyframe(4.0f, -1.2f),
                new Keyframe(5.0f, -1.0f));
            SmoothAll(c);
            return c;
        }

        /// <summary>
        /// Smooth keyframe tangents so the curves don't look piecewise-linear in the editor.
        /// Without this, the curve preview shows ugly straight segments.
        /// </summary>
        private static void SmoothAll(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++)
                c.SmoothTangents(i, 0f);
        }

        private static void FocusAsset(MissileProfileSO profile)
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = profile;
        }
    }
}
#endif
