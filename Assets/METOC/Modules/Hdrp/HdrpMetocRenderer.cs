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
    /// the auto-exposure floor with the reckoned ground light. Nothing it draws feeds back into the report.
    /// <para>
    /// The sun light keeps its above-air value at night on purpose: the sky already removes it below the horizon,
    /// and the moon (shaded as "reflect sun light") takes its brightness from it.
    /// </para>
    /// <para>
    /// Exposure goes through a runtime overlay volume (E42): its own global volume above the shared sky, with a profile
    /// made at run time that overrides only <c>limitMin</c>. No sky asset is written: in play mode, an edit to a profile
    /// asset persists to disk.
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
        Vector3 _drawnSun, _drawnMoon;
        bool _drawnOnce;

        /// <summary>Times the drawn sun was moved (E46's A/B counts them).</summary>
        public int SunRedraws { get; private set; }
        /// <summary>The exposure floor written this frame, EV100.</summary>
        public float ExposureFloorEv { get; private set; }

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
                moon.intensity = (float)(r.Light.MoonAboveAirLux * look.moonLookMultiplier);
                _moonHd.angularDiameter = (float)Almanac.MoonAngularDiameterDeg(r.Moon.DistanceKm);

                // Shadows to the beam that is up: the sun by day, the moon at night, none on a moonless night.
                bool sunUp = r.Sun.Sky.AboveHorizon;
                SetShadows(sun, sunUp);
                SetShadows(moon, !sunUp && r.Moon.Sky.AboveHorizon);

                ExposureFloorEv = (float)MetocExposure.FloorEv(r.Light.TotalLux, look.adaptationBiasEv, look.absoluteMinEv, look.dayFloorEv);
                _exposure.limitMin.Override(ExposureFloorEv);
            }
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
