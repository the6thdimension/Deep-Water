using NUnit.Framework;
using UnityEngine;
using RHRadarSuite.EditorTools;

namespace RHRadarSuite.Tests
{
    /// <summary>
    /// Phase 1 EditMode tests (see Assets/RH Radar Suite/ROADMAP.md):
    /// body-relative sector math, profile ApplyTo round-trip, idempotent
    /// attach, and Inspector-module reuse by the controller.
    /// </summary>
    public class RadarKitPhase1Tests
    {
        private GameObject go;
        private RadarProfileSO profile;

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            if (profile != null) Object.DestroyImmediate(profile);
        }

        private RadarProfileSO MakeProfile()
        {
            profile = ScriptableObject.CreateInstance<RadarProfileSO>();
            profile.role = RadarRole.GroundSearch;
            profile.maxDetectionRangeM = 7500f;
            profile.radarPower = 2f;
            profile.beamWidthDeg = 25f;
            profile.rotationRpm = 10f;
            profile.fullRotation = false;
            profile.sectorSizeDeg = 120f;
            profile.sectorCenterDeg = 45f;
            profile.updateInterval = 0.2f;
            profile.maxTargets = 64;
            profile.rangeAccuracyM = 12f;
            profile.defaultLOD = RadarLOD.LOD2_BasicRadar;
            return profile;
        }

        // ----- RadarMath: sector + body-relative bearing -----------------------

        [Test]
        public void IsAngleInSector_HandlesZeroWrap()
        {
            Assert.IsTrue(RadarMath.IsAngleInSector(350f, 0f, 40f), "350° should be inside a 40° sector centered on 0°");
            Assert.IsTrue(RadarMath.IsAngleInSector(10f, 0f, 40f), "10° should be inside a 40° sector centered on 0°");
            Assert.IsFalse(RadarMath.IsAngleInSector(30f, 0f, 40f), "30° should be outside a 40° sector centered on 0°");
            Assert.IsFalse(RadarMath.IsAngleInSector(180f, 0f, 40f), "180° should be outside a 40° sector centered on 0°");
        }

        [Test]
        public void BodyRelativeBearing_RotatesWithPlatform()
        {
            Vector3 targetDir = Vector3.right; // due "east" in world space

            // Platform facing world forward: east is bearing 090
            float bearingFacingNorth = RadarMath.BodyRelativeBearing(Vector3.forward, targetDir);
            Assert.AreEqual(90f, bearingFacingNorth, 0.01f);

            // Platform itself facing east: same target is dead ahead (bearing 000)
            float bearingFacingEast = RadarMath.BodyRelativeBearing(Vector3.right, targetDir);
            Assert.AreEqual(0f, bearingFacingEast, 0.01f);
        }

        [Test]
        public void FlatForward_TracksTransformYaw()
        {
            go = new GameObject("rotated");
            go.transform.rotation = Quaternion.Euler(20f, 90f, 10f); // pitched, rolled, yawed east

            Vector3 flat = RadarMath.FlatForward(go.transform);
            Assert.AreEqual(0f, flat.y, 1e-5f, "flat forward must be horizontal");
            Assert.AreEqual(0f, Vector3.Angle(flat, Vector3.right), 0.5f, "yaw-east platform's flat forward should be world right");
        }

        [Test]
        public void NominalSignalScale_MakesMaxRangeDetectable()
        {
            const float maxRange = 8000f;
            const float power = 1.5f;
            const float threshold = 0.1f;

            float scale = RadarMath.NominalSignalScale(maxRange, power, threshold);

            // Reproduce the module computation for a 1 m²-class target at max range
            float baseStrength = 1f / (maxRange * maxRange * maxRange * maxRange) * power;
            float signal = Mathf.Clamp01(baseStrength * scale);

            Assert.Greater(signal, threshold, "nominal target at max range must exceed the detection threshold");
            Assert.AreEqual(threshold * 4f, signal, threshold * 0.1f, "calibration margin should be ~4x threshold");
        }

        // ----- Profile ApplyTo round-trip --------------------------------------

        [Test]
        public void ApplyProfile_ConfiguresControllerAndModule()
        {
            go = new GameObject("radar");
            var controller = go.AddComponent<RadarSuiteController>();
            var module = go.AddComponent<BasicRadarModule>();

            MakeProfile().ApplyTo(controller);

            Assert.AreEqual(7500f, controller.MaxDetectionRange);
            Assert.AreEqual(2f, controller.RadarPower);
            Assert.AreEqual(0.2f, controller.UpdateInterval);
            Assert.AreEqual(64, controller.MaxTargets);
            Assert.AreEqual(RadarLOD.LOD2_BasicRadar, controller.CurrentLOD);
            Assert.AreSame(profile, controller.Profile);

            Assert.AreEqual(25f, module.beamWidth);
            Assert.AreEqual(60f, (float)module.GetParameter("rotationSpeed"), 0.01f, "10 rpm = 60 deg/s");
            Assert.AreEqual(false, (bool)module.GetParameter("enableFullRotation"));
            Assert.AreEqual(120f, (float)module.GetParameter("sectorSize"), 0.01f);
            Assert.AreEqual(45f, (float)module.GetParameter("sectorCenter"), 0.01f);
            Assert.AreEqual(12f, (float)module.GetParameter("rangeAccuracy"), 0.01f);
        }

        // ----- Idempotent attach ------------------------------------------------

        [Test]
        public void AttachRadar_TwiceProducesNoDuplicates()
        {
            go = new GameObject("platform");
            MakeProfile();

            RadarKit.AttachRadar(go, profile);
            RadarKit.AttachRadar(go, profile);

            Assert.AreEqual(1, go.GetComponents<RadarSuiteController>().Length);
            Assert.AreEqual(1, go.GetComponents<BasicRadarModule>().Length);
            Assert.AreEqual(1, go.GetComponents<RadarDiagnostics>().Length);
        }

        // ----- Controller reuses Inspector-configured modules -------------------

        [Test]
        public void Initialize_ReusesExistingModuleAndKeepsTuning()
        {
            go = new GameObject("radar");
            var controller = go.AddComponent<RadarSuiteController>();
            var module = go.AddComponent<BasicRadarModule>();
            module.beamWidth = 55f; // user's Inspector tuning (no profile assigned)

            controller.Initialize();

            Assert.AreEqual(1, go.GetComponents<BasicRadarModule>().Length,
                "Initialize must reuse the Inspector module, not add a duplicate");
            Assert.AreEqual(55f, module.beamWidth, "Inspector tuning must survive Initialize");
        }
    }
}
