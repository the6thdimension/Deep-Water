using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace DeepWater.Metoc.Hdrp
{
    /// <summary>
    /// Draws METOC's report in HDRP; HDRP only draws (E29). Each frame it points the scene's Sun and METOC's Moon
    /// light at the reckoned bodies, sets their above-air illuminance (HDRP's Physically Based Sky dims and reddens
    /// them through the air and hides them below the horizon), gives shadows to the body whose beam is up, and lowers
    /// the auto-exposure floor with the reckoned ground light. It draws the weather too: the volumetric clouds' cover,
    /// base and thickness, the fog from the visibility, and the 2D layer from the high cloud (E48, E51, E52). Nothing
    /// it draws feeds back into the report.
    /// <para>
    /// The sun light keeps its above-air value at night on purpose: the sky already removes it below the horizon,
    /// and the moon (shaded as "reflect sun light") takes its brightness from it.
    /// </para>
    /// <para>
    /// Exposure goes through a runtime overlay volume (E42): its own global volume above the shared sky, with a profile
    /// made at run time that overrides only what METOC decides: <c>limitMin</c>, and the cloud, fog and cloud-layer
    /// parameters above. Everything else falls through to the shared sky. No sky asset is written: in play mode, an
    /// edit to a profile asset persists to disk.
    /// </para>
    /// <para>
    /// HDRP's cloud shadows dim only the sun, so the moon light is dimmed by the reckoned cloud transmission here.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class HdrpMetocRenderer : MonoBehaviour
    {
        static readonly ProfilerMarker DrawMarker = new ProfilerMarker("METOC.Draw");

        [SerializeField] MetocDirector director;
        [Tooltip("The scene's sun: a directional light that interacts with the Physically Based Sky.")]
        [SerializeField] Light sun;
        [Tooltip("METOC's moon: a directional light, shaded by reflecting the sun.")]
        [SerializeField] Light moon;
        [SerializeField] HdrpMetocLookSO look;

        HDAdditionalLightData _moonHd;
        Volume _volume;
        VolumeProfile _profile;
        Exposure _exposure;
        VolumetricClouds _clouds;
        Fog _fog;
        CloudLayer _cloudLayer;
        Vector3 _drawnSun, _drawnMoon;
        bool _drawnOnce, _warnedUncalibrated;

        /// <summary>Times the drawn sun was moved (E46's A/B counts them).</summary>
        public int SunRedraws { get; private set; }
        /// <summary>The exposure floor written this frame, EV100.</summary>
        public float ExposureFloorEv { get; private set; }
        public MetocDirector Director => director;
        public HdrpMetocLookSO Look => look;
        /// <summary>The volumetric clouds' shapeFactor written this frame (1 when the clouds are off).</summary>
        public float CloudShapeFactor { get; private set; }
        /// <summary>Whether the cover comes from a measured table (E48) rather than the uncalibrated guess.</summary>
        public bool CoverCalibrated => look != null && look.cloudCover != null && look.cloudCover.Problem() == null;

        void OnEnable()
        {
            if (director == null || sun == null || moon == null || look == null)
            {
                Debug.LogError($"[METOC] {name}: the renderer needs the director, the sun and moon lights and a look; METOC draws nothing.", this);
                enabled = false;
                return;
            }
            _moonHd = moon.GetComponent<HDAdditionalLightData>();
            if (_moonHd == null || sun.GetComponent<HDAdditionalLightData>() == null)
            {
                Debug.LogError($"[METOC] {name}: the sun and moon must be HDRP lights (HDAdditionalLightData); METOC draws nothing.", this);
                enabled = false;
                return;
            }

            // The moon is lit by the sun, so HDRP draws its phase from the two directions (no phase is set by hand).
            _moonHd.celestialBodyShadingSource = HDAdditionalLightData.CelestialBodyShadingSource.ReflectSunLight;
            _moonHd.sunLightOverride = sun;
            _moonHd.interactsWithSky = true;
            moon.useColorTemperature = true;
            moon.colorTemperature = look.moonColorTemperatureK;

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "METOC overlay (runtime)";
            _profile.hideFlags = HideFlags.DontSave;
            _exposure = _profile.Add<Exposure>(false);
            _clouds = _profile.Add<VolumetricClouds>(false);
            _fog = _profile.Add<Fog>(false);
            _cloudLayer = _profile.Add<CloudLayer>(false);
            _volume = gameObject.AddComponent<Volume>();
            _volume.hideFlags = HideFlags.DontSave;
            _volume.isGlobal = true;
            _volume.priority = look.overlayPriority;
            _volume.sharedProfile = _profile;
            _drawnOnce = false;
        }

        void OnDisable()
        {
            if (_volume != null) Destroy(_volume);
            if (_profile != null) Destroy(_profile);
            _volume = null;
            _profile = null;
            _exposure = null;
            _clouds = null;
            _fog = null;
            _cloudLayer = null;
        }

        void LateUpdate()
        {
            if (!director.Ready) return;
            using (DrawMarker.Auto())
            {
                MetocReport r = director.Report;
                Vector3 toSun = ToUnity(r.Sun.Sky.Direction), toMoon = ToUnity(r.Moon.Sky.Direction);

                // E46: move a drawn body only past the quantum (0 = every frame); HDRP re-renders the sky when one moves.
                if (!_drawnOnce || Vector3.Angle(_drawnSun, toSun) > look.redrawQuantumDeg)
                {
                    sun.transform.rotation = Facing(-toSun);
                    _drawnSun = toSun;
                    SunRedraws++;
                }
                if (!_drawnOnce || Vector3.Angle(_drawnMoon, toMoon) > look.redrawQuantumDeg)
                {
                    moon.transform.rotation = Facing(-toMoon);
                    _drawnMoon = toMoon;
                }
                _drawnOnce = true;

                sun.intensity = (float)(r.Light.SunAboveAirLux * look.sunLookMultiplier);
                moon.intensity = (float)(r.Light.MoonAboveAirLux * look.moonLookMultiplier * r.Light.CloudTransmission);
                _moonHd.angularDiameter = (float)Almanac.MoonAngularDiameterDeg(r.Moon.DistanceKm);

                // Shadows to the beam that is up: the sun by day, the moon at night, none on a moonless night.
                bool sunUp = r.Sun.Sky.AboveHorizon;
                SetShadows(sun, sunUp);
                SetShadows(moon, !sunUp && r.Moon.Sky.AboveHorizon);

                ExposureFloorEv = (float)MetocExposure.FloorEv(r.Light.TotalLux, look.adaptationBiasEv, look.absoluteMinEv, look.dayFloorEv);
                _exposure.limitMin.Override(ExposureFloorEv);

                DrawWeather(r.Weather);
            }
        }

        void DrawWeather(in MetocWeather w)
        {
            // Volumetric clouds: off when clear, else the shapeFactor that covers the reckoned share of the sky (E48).
            bool cloudy = w.CoverOktas > 0;
            CloudShapeFactor = cloudy ? ShapeFactorFor(w.CoverOktas / 8.0) : 1f;
            _clouds.enable.Override(cloudy);
            _clouds.shapeFactor.Override(CloudShapeFactor);
            _clouds.bottomAltitude.Override((float)Math.Max(0.01, w.CloudBaseM));      // HDRP's minimums
            _clouds.altitudeRange.Override((float)Math.Max(100.0, w.CloudThicknessM));
            SetOrRelease(_clouds.numPrimarySteps, look.cloudPrimarySteps);
            SetOrRelease(_clouds.numLightSteps, look.cloudLightSteps);

            // Fog from the visibility (E51).
            MetocFogSettings f = MetocFog.For(w, look.fogUniformFraction, look.fogDistanceFactor, look.fogMinDistanceM);
            _fog.enabled.Override(true);
            _fog.meanFreePath.Override((float)f.MeanFreePathM);
            _fog.baseHeight.Override((float)f.BaseHeightM);
            _fog.maximumHeight.Override((float)f.MaximumHeightM);
            _fog.maxFogDistance.Override((float)f.MaxFogDistanceM);

            // The 2D layer carries the high cloud (E52).
            _cloudLayer.opacity.Override((float)(w.HighCloudOktas / 8.0) * look.highCloudOpacityAtEightOktas);
        }

        float ShapeFactorFor(double coverFraction)
        {
            if (CoverCalibrated) return look.cloudCover.ShapeFactorFor(coverFraction);
            if (!_warnedUncalibrated)
            {
                string why = look.cloudCover == null ? "none" : look.cloudCover.Problem();
                Debug.LogWarning($"[METOC] {look.name}: no cloud-cover calibration ({why}); drawing an UNCALIBRATED guess, " +
                                 "shapeFactor = 1 − cover. Run Deep Water → METOC → Calibrate Cloud Cover.", look);
                _warnedUncalibrated = true;
            }
            return (float)(1.0 - coverFraction);
        }

        /// <summary>A positive look value overrides HDRP's parameter; 0 lets the shared sky's value through.</summary>
        static void SetOrRelease(ClampedIntParameter p, int value)
        {
            if (value > 0) p.Override(value);
            else p.overrideState = false;
        }

        /// <summary>The theatre frame (Theatre E2): Unity X east, Y up, Z north.</summary>
        static Vector3 ToUnity(in Enu d) => new Vector3((float)d.East, (float)d.Up, (float)d.North);

        /// <summary>A rotation facing <paramref name="forward"/>, with north as "up" when it points straight up or down.</summary>
        static Quaternion Facing(Vector3 forward) =>
            Quaternion.LookRotation(forward, Mathf.Abs(forward.y) > 0.9999f ? Vector3.forward : Vector3.up);

        static void SetShadows(Light light, bool on)
        {
            var wanted = on ? LightShadows.Soft : LightShadows.None;
            if (light.shadows != wanted) light.shadows = wanted;
        }
    }
}
