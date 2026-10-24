using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Editor
{
    /// <summary>
    /// Builds General Quarters' presentation (slice 6, G30) from code: the particle textures, materials on the HDRP particle
    /// shader, one prefab per effect, the hatch material, the camera-cut volume profile and the presentation profile that
    /// ties them to events, mount rigs and sounds. Idempotent: rerun after changing a number here. Every effect is a
    /// Shuriken particle system, so each can be rebuilt from this file and tuned in its prefab or here.
    /// Looks are AUTHORED. Real numbers where they exist: Phalanx 4,500 rounds/min (100-round bursts, 1.33 s), 115°/s train,
    /// 86°/s elevation; Mk 38 180 rounds/min; Mk 45 barrel 7.1 m, CIWS 2.4 m and Mk 38 3.6 m measured on the Blender model.
    /// </summary>
    public static class CombatEffectsBuilder
    {
        const string Root = "Assets/General Quarters";
        const string FxDir = Root + "/Prefabs/Effects";
        const string TexDir = FxDir + "/Textures";
        const string MatDir = FxDir + "/Materials";
        const string ShaderPath = Root + "/Modules/Presentation/Shaders/GQ_Particle.shader";
        public const string PresentationAsset = Root + "/ScriptableObjects/GQ_Presentation.asset";
        public const string CameraCutProfile = Root + "/Examples/Range/GQ_CameraCut.asset";
        const string FdsAudio = "Assets/FDS Assets/Audio";

        static Material s_smoke, s_flame, s_glow, s_tracer, s_spray, s_ribbon;

        [MenuItem("Deep Water/General Quarters/Build Combat Effects")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            Directory.CreateDirectory(TexDir);
            Directory.CreateDirectory(MatDir);
            Texture2D smokeTex = Texture(TexDir + "/GQ_Smoke.png", 128, SmokePixel);
            Texture2D glowTex = Texture(TexDir + "/GQ_Glow.png", 64, GlowPixel);
            Texture2D sprayTex = Texture(TexDir + "/GQ_Spray.png", 128, SprayPixel);
            Texture2D ribbonTex = Texture(TexDir + "/GQ_Ribbon.png", 64, RibbonPixel);

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) throw new FileNotFoundException("GQ particle shader missing", ShaderPath);
            s_smoke = Mat("GQ_FX_Smoke", shader, smokeTex, false, 1f, Color.white, 4f);
            s_flame = Mat("GQ_FX_Flame", shader, smokeTex, true, 5f, new Color(1f, 0.62f, 0.25f), 1.5f);
            s_glow = Mat("GQ_FX_Glow", shader, glowTex, true, 9f, new Color(1f, 0.82f, 0.55f), 1f);
            s_tracer = Mat("GQ_FX_Tracer", shader, glowTex, true, 12f, new Color(1f, 0.7f, 0.35f), 0.5f);
            s_spray = Mat("GQ_FX_Spray", shader, sprayTex, false, 1f, Color.white, 2f);
            s_ribbon = Mat("GQ_FX_Ribbon", shader, ribbonTex, false, 1f, Color.white, 2f);
            s_ribbon.SetFloat("_FogWeight", 0.5f);   // a smoke trail stays legible through the haze a little further than the sea does

            var p = LoadOrCreate<CombatPresentationSO>(PresentationAsset);
            p.vlsLaunch = Slot(VlsLaunch(), 9f);
            p.muzzleMk45 = Slot(Muzzle("GQ_FX_Muzzle_Mk45", 5f, 0f, 1, 0f, 18, 0.35f), 4.5f);
            p.muzzleCiws = Slot(Muzzle("GQ_FX_Muzzle_Ciws", 1.4f, 40f, 0, 18f, 0, 1.33f), 4f);
            p.muzzleMk38 = Slot(Muzzle("GQ_FX_Muzzle_Mk38", 1.6f, 3f, 0, 3f, 0, 3.3f), 5f);
            p.airburst = Slot(Airburst(), 5.5f);
            p.warhead = Slot(Warhead(), 9f);
            p.splash = Slot(Splash(), 4.5f);
            p.shipHit = new EffectSlot { prefab = p.warhead.prefab, lifetimeS = 9f, scale = 1.4f };
            p.fire = new EffectSlot { prefab = Fire(), lifetimeS = 14f, scale = 2.5f };
            p.systemSmoke = Slot(SystemSmoke(), 10f);
            p.bowSpray = Slot(BowSpray(), 1.5f);
            p.essmTrail = Slot(Trail("GQ_FX_Trail_Essm", 1.4f, new Color(1f, 0.85f, 0.6f), 1.8f, 10f, 4.5f, 0.93f, 0.8f), 5f);
            p.threatTrail = Slot(Trail("GQ_FX_Trail_Threat", 0.8f, new Color(1f, 0.5f, 0.3f), 0.8f, 4f, 2.5f, 0.75f, 0.45f), 3f);
            p.splashBelowAltitudeM = 20f;
            p.tracerSpeedMs = 1100f;

            p.splashFoamRadiusM = 12f;
            p.splashFoamLevel = 0.5f;
            p.wakeIntervalS = 0.25f;
            p.wakeFoamRadiusM = 9f;
            p.wakeFoamLevelAt30Kn = 0.35f;
            p.wakeMinSpeedKn = 3f;
            // Waterline 141 m (bow about +70, stern about −71): the stern and quarters, and the bow wave either side.
            p.wakePoints = new[] { new Vector3(0f, 0f, -72f), new Vector3(-6f, 0f, -66f), new Vector3(6f, 0f, -66f), new Vector3(-7f, 0f, 55f), new Vector3(7f, 0f, 55f) };
            p.bowSprayRateAt30Kn = 40f;
            p.bowSprayPoint = new Vector3(0f, 1.5f, 68f);

            p.mounts = new[]
            {
                new MountRig { mountName = "Mk 45", trainTransform = "GQ_Mk45_Train", elevateTransform = "GQ_Mk45_Elevate", trainRateDegS = 30f, elevateRateDegS = 20f, barrelM = 7.1f, holdS = 4f },
                new MountRig { mountName = "CIWS fwd", trainTransform = "GQ_CIWS_Fwd_Train", elevateTransform = "GQ_CIWS_Fwd_Elevate", trainRateDegS = 115f, elevateRateDegS = 86f, barrelM = 2.4f, holdS = 3f },
                new MountRig { mountName = "CIWS aft", trainTransform = "GQ_CIWS_Aft_Train", elevateTransform = "GQ_CIWS_Aft_Elevate", trainRateDegS = 115f, elevateRateDegS = 86f, barrelM = 2.4f, holdS = 3f },
                new MountRig { mountName = "Mk 38 port", trainTransform = "GQ_Mk38_Port_Train", elevateTransform = "", trainRateDegS = 60f, elevateRateDegS = 0f, barrelM = 3.6f, holdS = 3f },
                new MountRig { mountName = "Mk 38 stbd", trainTransform = "GQ_Mk38_Stbd_Train", elevateTransform = "", trainRateDegS = 60f, elevateRateDegS = 0f, barrelM = 3.6f, holdS = 3f },
            };
            p.vlsCellPrefixes = new[] { "GQ_VLS_F_", "GQ_VLS_A_" };
            p.hatchMaterial = LitMaterial(MatDir + "/GQ_Hatch.mat", new Color(0.33f, 0.35f, 0.38f));
            p.hatchSize = new Vector3(0.62f, 0.06f, 0.62f);
            p.hatchOpenS = 0.3f; p.hatchHoldS = 2.5f; p.hatchCloseS = 0.6f; p.hatchOpenDeg = 110f;

            p.vlsLaunchSound = Sound(Clip("MissileFireHeavy.ogg"), SynthSound.MissileLaunch, 1f, 40f, 6000f);
            p.detonationSound = Sound(Clip("MissileExplosion.ogg"), SynthSound.Detonation, 1f, 100f, 12000f);
            p.mk45Sound = Sound(null, SynthSound.Mk45Report, 1f, 60f, 8000f);
            p.ciwsSound = Sound(null, SynthSound.CiwsBurst, 0.9f, 30f, 4000f);
            p.mk38Sound = Sound(null, SynthSound.Mk38Burst, 0.8f, 20f, 3000f);
            p.airburstSound = Sound(null, SynthSound.Airburst, 0.7f, 80f, 9000f);
            p.splashSound = Sound(null, SynthSound.Splash, 0.6f, 30f, 3000f);
            p.shipHitSound = Sound(null, SynthSound.ShipHit, 1f, 100f, 12000f);
            p.alarmSound = Sound(null, SynthSound.Alarm, 0.5f, 0f, 0f);
            p.speedOfSoundMs = 343f;
            p.voices = 24;
            p.provenance =
                "AUTHORED looks (slice 6, G30). Code-built particle systems on DeepWater/General Quarters/Particle (HDRP). " +
                "HD_Naval_Explosion and the VFX Arsenal flames were tried first and did not fit: the built-in particle shader draws untextured quads under HDRP, " +
                "and the flames are about a metre across and too dim under daylight exposure.\n" +
                "Public: Phalanx 4,500 rounds/min in 100-round bursts (1.33 s), 115°/s train and 86°/s elevation; Mk 38 180 rounds/min. " +
                "Measured on the Blender model: Mk 45 barrel 7.1 m, CIWS 2.4 m, Mk 38 3.6 m; waterline 141 m.\n" +
                "Sounds: VLS launch and detonation are the repo's FDS recordings; the rest are synthesised stand-ins (G32) until recordings are dropped in.";
            EditorUtility.SetDirty(p);

            BuildCameraCutProfile();
            AssetDatabase.SaveAssets();
            return $"[GQ] Combat effects built: 12 effect prefabs in {FxDir}, {PresentationAsset}, {CameraCutProfile}.";
        }

        // ── Effects ────────────────────────────────────────────────────

        static GameObject VlsLaunch()
        {
            var go = new GameObject("GQ_FX_VlsLaunch");
            Burst(Layer(go, "Flash", s_glow, 0.2f, false, R(0.15f), R(0f), R(9f), C(1f, 0.85f, 0.6f, 1f), C(1f, 0.6f, 0.3f, 0f), 0f, true), 1);
            ParticleSystem jet = Layer(go, "Jet", s_flame, 0.7f, false, R(0.25f, 0.4f), R(35f, 50f), R(1.5f, 3f), C(1f, 0.9f, 0.6f, 1f), C(1f, 0.3f, 0.1f, 0f), 0f, false);
            Rate(jet, 120f); Cone(jet, 6f, 0.3f); Shrink(jet);
            ParticleSystem smoke = Layer(go, "Smoke", s_smoke, 0.6f, false, R(5f, 8f), R(2f, 6f), R(4f, 6f), C(0.88f, 0.88f, 0.88f, 0.9f), C(0.82f, 0.82f, 0.82f, 0f), -0.02f, false);
            Burst(smoke, 50); Rate(smoke, 40f); Hemisphere(smoke, 1.5f); Grow(smoke, 0.3f, 3.5f); Noise(smoke, 0.6f);
            return Save(go);
        }

        /// <summary>A gun's muzzle: flash(es), tracers (CIWS and Mk 38) and smoke. Rates are per second over the burst.</summary>
        static GameObject Muzzle(string name, float flashSize, float flashRate, int flashBurst, float tracerRate, int smokeBurst, float burstS)
        {
            var go = new GameObject(name);
            ParticleSystem flash = Layer(go, "Flash", s_glow, Mathf.Max(0.1f, burstS), false, R(0.04f, 0.08f), R(0f), R(flashSize * 0.8f, flashSize), C(1f, 0.85f, 0.55f, 1f), C(1f, 0.6f, 0.3f, 0f), 0f, true);
            if (flashBurst > 0) Burst(flash, flashBurst);
            if (flashRate > 0f) Rate(flash, flashRate);
            if (tracerRate > 0f)
            {
                ParticleSystem tracers = Layer(go, "Tracers", s_tracer, burstS, false, R(1f), R(1100f), R(0.3f, 0.4f), C(1f, 0.85f, 0.6f, 1f), C(1f, 0.6f, 0.3f, 1f), 0f, false);
                Rate(tracers, tracerRate); Cone(tracers, 0.6f, 0.05f);
                var r = tracers.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.015f;
                r.lengthScale = 2f;
            }
            ParticleSystem smoke = Layer(go, "Smoke", s_smoke, Mathf.Max(0.1f, burstS), false, R(2f, 3.5f), R(3f, 9f), R(1.5f, 2.5f), C(0.78f, 0.78f, 0.78f, 0.75f), C(0.75f, 0.75f, 0.75f, 0f), -0.01f, false);
            if (smokeBurst > 0) Burst(smoke, smokeBurst); else Rate(smoke, 8f);
            Cone(smoke, 20f, 0.2f); Grow(smoke, 0.4f, 3.5f);
            return Save(go);
        }

        static GameObject Airburst()
        {
            var go = new GameObject("GQ_FX_Airburst");
            Burst(Layer(go, "Flash", s_glow, 0.2f, false, R(0.1f), R(0f), R(6f), C(1f, 0.8f, 0.5f, 1f), C(1f, 0.5f, 0.2f, 0f), 0f, true), 1);
            ParticleSystem puff = Layer(go, "Puff", s_smoke, 0.2f, false, R(3f, 5f), R(1f, 4f), R(3f, 4f), C(0.22f, 0.22f, 0.22f, 0.9f), C(0.3f, 0.3f, 0.3f, 0f), -0.01f, false);
            Burst(puff, 12); Sphere(puff, 1f); Grow(puff, 0.4f, 3f);
            return Save(go);
        }

        static GameObject Warhead()
        {
            var go = new GameObject("GQ_FX_Warhead");
            Burst(Layer(go, "Flash", s_glow, 0.2f, false, R(0.12f), R(0f), R(18f), C(1f, 0.85f, 0.6f, 1f), C(1f, 0.5f, 0.2f, 0f), 0f, true), 1);
            ParticleSystem fire = Layer(go, "Fireball", s_flame, 0.2f, false, R(0.4f, 0.8f), R(3f, 10f), R(4f, 6f), C(1f, 0.75f, 0.35f, 1f), C(1f, 0.3f, 0.1f, 0f), 0f, false);
            Burst(fire, 25); Sphere(fire, 1.5f); Grow(fire, 0.6f, 1.8f);
            ParticleSystem smoke = Layer(go, "Smoke", s_smoke, 0.3f, false, R(5f, 8f), R(2f, 6f), R(6f, 8f), C(0.28f, 0.27f, 0.26f, 0.9f), C(0.35f, 0.35f, 0.35f, 0f), -0.01f, false);
            Burst(smoke, 30); Sphere(smoke, 2f); Grow(smoke, 0.5f, 3.5f); Noise(smoke, 0.5f);
            ParticleSystem debris = Layer(go, "Debris", s_tracer, 0.2f, false, R(1f, 2.5f), R(30f, 80f), R(0.2f, 0.3f), C(1f, 0.7f, 0.4f, 1f), C(1f, 0.4f, 0.2f, 0f), 1f, false);
            Burst(debris, 40); Sphere(debris, 1f);
            var r = debris.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 1f;
            return Save(go);
        }

        static GameObject Splash()
        {
            var go = new GameObject("GQ_FX_Splash");
            ParticleSystem column = Layer(go, "Column", s_spray, 0.2f, false, R(1.8f, 3f), R(22f, 40f), R(0.8f, 1.4f), C(0.92f, 0.96f, 1f, 0.95f), C(0.9f, 0.95f, 1f, 0f), 1f, false);
            Burst(column, 140); Cone(column, 7f, 1.2f); Grow(column, 0.6f, 2.5f);
            ParticleSystem.MainModule columnMain = column.main;
            columnMain.maxParticles = 200;
            ParticleSystem mist = Layer(go, "Mist", s_smoke, 0.3f, false, R(2f, 3.5f), R(1f, 4f), R(6f, 8f), C(0.95f, 0.97f, 1f, 0.5f), C(0.95f, 0.97f, 1f, 0f), -0.01f, false);
            Burst(mist, 12); Hemisphere(mist, 3f); Grow(mist, 0.5f, 3f);
            ParticleSystem ring = Layer(go, "Ring", s_spray, 0.1f, false, R(0.6f, 1f), R(6f, 12f), R(1f, 1.5f), C(0.95f, 0.97f, 1f, 0.8f), C(0.95f, 0.97f, 1f, 0f), 1f, false);
            Burst(ring, 40); Cone(ring, 80f, 2f); Grow(ring, 0.8f, 2f);
            return Save(go);
        }

        static GameObject Fire()
        {
            var go = new GameObject("GQ_FX_Fire");
            ParticleSystem flames = Layer(go, "Flames", s_flame, 1f, true, R(0.5f, 1f), R(2f, 5f), R(1.5f, 3.5f), C(1f, 0.7f, 0.3f, 1f), C(1f, 0.25f, 0.05f, 0f), 0f, false);
            Rate(flames, 50f); Cone(flames, 10f, 2f); Shrink(flames);
            ParticleSystem smoke = Layer(go, "Smoke", s_smoke, 1f, true, R(10f, 14f), R(3f, 6f), R(4f, 6f), C(0.08f, 0.08f, 0.08f, 0.85f), C(0.22f, 0.22f, 0.22f, 0f), -0.03f, false);
            Rate(smoke, 8f); Cone(smoke, 8f, 2f); Grow(smoke, 0.25f, 5f); Noise(smoke, 1f);
            return Save(go);
        }

        static GameObject SystemSmoke()
        {
            var go = new GameObject("GQ_FX_SystemSmoke");
            ParticleSystem smoke = Layer(go, "Smoke", s_smoke, 1f, true, R(8f, 10f), R(2f, 4f), R(2f, 3f), C(0.42f, 0.42f, 0.42f, 0.7f), C(0.5f, 0.5f, 0.5f, 0f), -0.02f, false);
            Rate(smoke, 5f); Cone(smoke, 10f, 1f); Grow(smoke, 0.3f, 5f); Noise(smoke, 0.8f);
            return Save(go);
        }

        static GameObject BowSpray()
        {
            var go = new GameObject("GQ_FX_BowSpray");
            ParticleSystem spray = Layer(go, "Spray", s_spray, 1f, true, R(0.8f, 1.4f), R(4f, 9f), R(0.8f, 1.2f), C(0.95f, 0.97f, 1f, 0.8f), C(0.95f, 0.97f, 1f, 0f), 1f, false);
            Rate(spray, 0f);
            Cone(spray, 55f, 1.5f);
            ParticleSystem.ShapeModule shape = spray.shape;
            shape.rotation = new Vector3(-90f, 0f, 0f);   // the effect faces along the ship; the spray goes up and out
            Grow(spray, 0.6f, 2f);
            return Save(go);
        }

        /// <summary>
        /// A missile's exhaust glow (a particle at the nozzle) and its smoke trail: a ribbon that widens from
        /// <paramref name="startWidth"/> to <paramref name="endWidth"/> m and fades over <paramref name="lifeS"/>.
        /// A ribbon, not puffs: at 700 m/s, puffs laid every few metres read as a dotted line, and a dense enough stream
        /// costs thousands of particles per missile.
        /// </summary>
        static GameObject Trail(string name, float glowSize, Color glow, float startWidth, float endWidth, float lifeS, float grey, float alpha)
        {
            var go = new GameObject(name);
            ParticleSystem g = Layer(go, "Exhaust", s_glow, 1f, true, R(0.05f), R(0f), R(glowSize), glow, new Color(glow.r, glow.g, glow.b, 0f), 0f, true);
            Rate(g, 60f);
            var ribbon = new GameObject("Smoke");
            ribbon.transform.SetParent(go.transform, false);
            var tr = ribbon.AddComponent<TrailRenderer>();
            tr.sharedMaterial = s_ribbon;
            tr.time = lifeS;
            tr.minVertexDistance = 3f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, startWidth), new Keyframe(0.25f, Mathf.Lerp(startWidth, endWidth, 0.5f)), new Keyframe(1f, endWidth));
            tr.widthMultiplier = 1f;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(grey, grey, grey), 0f), new GradientColorKey(new Color(grey, grey, grey), 1f) },
                             new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = gradient;
            tr.textureMode = LineTextureMode.Stretch;
            tr.alignment = LineAlignment.View;
            tr.numCapVertices = 2;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
            return Save(go);
        }

        // ── Particle system helpers ───────────────────────────────────────

        static ParticleSystem Layer(GameObject parent, string name, Material mat, float duration, bool loop, ParticleSystem.MinMaxCurve lifetime,
            ParticleSystem.MinMaxCurve speed, ParticleSystem.MinMaxCurve size, Color start, Color end, float gravity, bool local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.duration = duration;
            main.loop = loop;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 400;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                             new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(Mathf.Lerp(start.a, end.a, 0.35f), 0.4f), new GradientAlphaKey(end.a, 1f) });
            col.color = gradient;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.maxParticleSize = 3f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static ParticleSystem.MinMaxCurve R(float v) => new ParticleSystem.MinMaxCurve(v);
        static ParticleSystem.MinMaxCurve R(float a, float b) => new ParticleSystem.MinMaxCurve(a, b);
        static Color C(float r, float g, float b, float a) => new Color(r, g, b, a);

        static void Burst(ParticleSystem ps, int count)
        {
            ParticleSystem.EmissionModule em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        static void Rate(ParticleSystem ps, float perS)
        {
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = perS;
        }

        static void Cone(ParticleSystem ps, float angleDeg, float radius)
        {
            ParticleSystem.ShapeModule s = ps.shape;
            s.shapeType = ParticleSystemShapeType.Cone;
            s.angle = angleDeg;
            s.radius = radius;
        }

        static void Sphere(ParticleSystem ps, float radius)
        {
            ParticleSystem.ShapeModule s = ps.shape;
            s.shapeType = ParticleSystemShapeType.Sphere;
            s.radius = radius;
        }

        static void Hemisphere(ParticleSystem ps, float radius)
        {
            ParticleSystem.ShapeModule s = ps.shape;
            s.shapeType = ParticleSystemShapeType.Hemisphere;
            s.radius = radius;   // domes along the effect's forward, which the presentation points up
        }

        /// <summary>Size grows from <paramref name="from"/> to <paramref name="to"/> times the start size over its life.</summary>
        static void Grow(ParticleSystem ps, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule s = ps.sizeOverLifetime;
            s.enabled = true;
            float top = Mathf.Max(from, to);
            s.size = new ParticleSystem.MinMaxCurve(top, new AnimationCurve(new Keyframe(0f, from / top), new Keyframe(0.35f, Mathf.Lerp(from, to, 0.7f) / top), new Keyframe(1f, to / top)));
        }

        static void Shrink(ParticleSystem ps)
        {
            ParticleSystem.SizeOverLifetimeModule s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f)));
        }

        static void Noise(ParticleSystem ps, float strength)
        {
            ParticleSystem.NoiseModule n = ps.noise;
            n.enabled = true;
            n.strength = strength;
            n.frequency = 0.25f;
            n.quality = ParticleSystemNoiseQuality.Low;
        }

        static GameObject Save(GameObject go)
        {
            string path = $"{FxDir}/{go.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static EffectSlot Slot(GameObject prefab, float lifetimeS) => new EffectSlot { prefab = prefab, lifetimeS = lifetimeS, scale = 1f };

        // ── Textures and materials ───────────────────────────────────────

        delegate Color Pixel(float u, float v, System.Random rng);

        static Texture2D Texture(string path, int size, Pixel pixel)
        {
            var rng = new System.Random(path.Length * 7919);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, pixel((x + 0.5f) / size, (y + 0.5f) / size, rng));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color SmokePixel(float u, float v, System.Random rng)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float n = 0.55f * Mathf.PerlinNoise(u * 4f + 3.1f, v * 4f + 1.7f) + 0.3f * Mathf.PerlinNoise(u * 9f + 7.3f, v * 9f + 2.9f) + 0.15f * Mathf.PerlinNoise(u * 19f, v * 19f);
            float a = Mathf.Clamp01((1f - r) * 1.6f) * Mathf.Clamp01(n * 1.6f - 0.15f);
            float shade = 0.85f + 0.15f * n;
            return new Color(shade, shade, shade, a * a);
        }

        static Color GlowPixel(float u, float v, System.Random rng)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Exp(-r * r * 6f) * Mathf.Clamp01(1f - r);
            return new Color(1f, 1f, 1f, a);
        }

        /// <summary>Soft across the ribbon (v), with a little streaky variation along it (u).</summary>
        static Color RibbonPixel(float u, float v, System.Random rng)
        {
            float across = Mathf.Abs(v - 0.5f) * 2f;
            float streak = 0.75f + 0.25f * Mathf.PerlinNoise(u * 3f + 2f, v * 12f + 5f);
            return new Color(1f, 1f, 1f, Mathf.Exp(-across * across * 3.5f) * Mathf.Clamp01(1f - across) * streak);
        }

        static Color SprayPixel(float u, float v, System.Random rng)
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float n = Mathf.PerlinNoise(u * 14f + 5f, v * 14f + 9f);
            float drops = Mathf.Clamp01((n - 0.45f) * 4f);
            float a = Mathf.Clamp01(1f - r) * (0.35f + 0.65f * drops);
            return new Color(1f, 1f, 1f, a);
        }

        static Material Mat(string name, Shader shader, Texture2D tex, bool additive, float brightness, Color tint, float softM)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetTexture("_MainTex", tex);
            m.SetColor("_Tint", tint);
            m.SetFloat("_Brightness", brightness);
            m.SetFloat("_SoftDistance", softM);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material LitMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial);   // the pipeline's default (AP5)
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static AudioClip Clip(string file)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{FdsAudio}/{file}");
            if (clip == null) Debug.LogWarning($"[GQ] {FdsAudio}/{file} not found; its slot falls back to the synthesised sound.");
            return clip;
        }

        static SoundSlot Sound(AudioClip clip, SynthSound synth, float volume, float minM, float maxM) =>
            new SoundSlot { clip = clip, synth = synth, volume = volume, minDistanceM = minM, maxDistanceM = maxM, pitch = 1f };

        /// <summary>A volume profile that turns motion blur off; the chase camera weights it in for a few frames after a cut.</summary>
        static void BuildCameraCutProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(CameraCutProfile);
            if (profile == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CameraCutProfile));
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, CameraCutProfile);
            }
            if (!profile.TryGet(out MotionBlur blur))
            {
                blur = profile.Add<MotionBlur>(true);
                blur.name = "MotionBlur";
                AssetDatabase.AddObjectToAsset(blur, profile);
            }
            blur.intensity.Override(0f);
            EditorUtility.SetDirty(blur);
            EditorUtility.SetDirty(profile);
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
