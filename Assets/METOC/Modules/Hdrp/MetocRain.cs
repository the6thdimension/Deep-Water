using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.VFX;

namespace DeepWater.Metoc.Hdrp
{
    /// <summary>
    /// Draws METOC's rain (E53) with <c>MetocRain.vfx</c>: a box of falling drops that follows a viewer's position, not
    /// its rotation. The rate comes from the report; the drawn count, fall, size and brightness are the look's. Nothing
    /// it draws feeds back into the report.
    /// <para>
    /// The graph's box, bounds and velocity are in this object's space while the drops live in world space. Moving this
    /// object to the viewer moves the box, and the drops already falling keep falling where they are, wrapped back into
    /// the box when they leave it (VFX Graph's Tile/Warp).
    /// </para>
    /// <para>
    /// A drop's luminance is a multiple of E/π (E the reckoned ground light), and the graph's output applies the
    /// camera's exposure, as HDRP does to the sky. So rain darkens with the day and the cloud, from the reckoned light,
    /// never from anything read back from HDRP.
    /// </para>
    /// <para>Dry for longer than a drop's fall through the box, the effect is switched off: no rain costs nothing.</para>
    /// </summary>
    [RequireComponent(typeof(VisualEffect))]
    [DefaultExecutionOrder(510)]
    public sealed class MetocRain : MonoBehaviour
    {
        static readonly ProfilerMarker DrawMarker = new ProfilerMarker("METOC.Rain");
        static readonly int RateId = Shader.PropertyToID(MetocRainVfx.Rate);
        static readonly int LifetimeId = Shader.PropertyToID(MetocRainVfx.Lifetime);
        static readonly int BoxSizeId = Shader.PropertyToID(MetocRainVfx.BoxSize);
        static readonly int VelocityId = Shader.PropertyToID(MetocRainVfx.Velocity);
        static readonly int DropWidthId = Shader.PropertyToID(MetocRainVfx.DropWidth);
        static readonly int StretchId = Shader.PropertyToID(MetocRainVfx.Stretch);
        static readonly int ColorId = Shader.PropertyToID(MetocRainVfx.Color);
        static readonly int AlphaId = Shader.PropertyToID(MetocRainVfx.Alpha);
        static readonly int FadedDistanceId = Shader.PropertyToID(MetocRainVfx.FadedDistance);
        static readonly int VisibleDistanceId = Shader.PropertyToID(MetocRainVfx.VisibleDistance);

        [SerializeField] MetocDirector director;
        [Tooltip("What the rain falls around: the camera's transform. Only its position is followed.")]
        [SerializeField] Transform viewer;
        [SerializeField] HdrpMetocLookSO look;

        VisualEffect _vfx;
        float _dryForSeconds;

        /// <summary>Drops drawn now (the steady count the spawn rate keeps); 0 when dry.</summary>
        public float DrawnDrops { get; private set; }
        /// <summary>A drop's luminance this frame, nits.</summary>
        public float DropLuminanceNits { get; private set; }
        /// <summary>Whether the effect is running (it stops once the last drop has fallen after the rain ends).</summary>
        public bool Drawing => _vfx != null && _vfx.enabled;

        void OnEnable()
        {
            _vfx = GetComponent<VisualEffect>();
            if (director == null || viewer == null || look == null || _vfx.visualEffectAsset == null)
            {
                Debug.LogError($"[METOC] {name}: the rain needs the director, a viewer, a look and the rain graph on its VisualEffect; no rain.", this);
                enabled = false;
                return;
            }
            string missing = MissingProperty(_vfx);
            if (missing != null)
            {
                Debug.LogError($"[METOC] {name}: '{_vfx.visualEffectAsset.name}' has no exposed '{missing}'; rebuild it with " +
                               "Deep Water → METOC → Build Rain VFX. No rain.", this);
                enabled = false;
                return;
            }
            _dryForSeconds = float.PositiveInfinity;   // nothing has fallen yet
        }

        void OnDisable()
        {
            if (_vfx != null) _vfx.enabled = false;
            DrawnDrops = 0f;
        }

        void LateUpdate()
        {
            if (!director.Ready) return;
            using (DrawMarker.Auto())
            {
                MetocReport r = director.Report;
                double rate = r.Weather.RainMmPerHour;
                float lifetime = look.rainBoxSizeM.y / look.rainFallSpeedMps;
                _dryForSeconds = rate > 0 ? 0f : _dryForSeconds + Time.deltaTime;
                bool draw = _dryForSeconds <= lifetime;   // after the rain ends, let the last drops fall out
                if (_vfx.enabled != draw) _vfx.enabled = draw;
                if (!draw)
                {
                    DrawnDrops = 0f;
                    return;
                }

                transform.SetPositionAndRotation(viewer.position, Quaternion.identity);
                DrawnDrops = DrawnCount(rate, look.rainDropsAt10MmPerHour, look.rainDropsExponent, MetocRainVfx.Capacity);
                DropLuminanceNits = DropLuminance(r.Light.TotalLux, look.rainLuminanceFactor);
                _vfx.SetFloat(RateId, DrawnDrops / lifetime);
                _vfx.SetFloat(LifetimeId, lifetime);
                _vfx.SetVector3(BoxSizeId, look.rainBoxSizeM);
                _vfx.SetVector3(VelocityId, new Vector3(0f, -look.rainFallSpeedMps, 0f));
                _vfx.SetFloat(DropWidthId, look.rainDropWidthM);
                _vfx.SetFloat(StretchId, look.rainStreakLengthM / look.rainDropWidthM);
                _vfx.SetVector3(ColorId, new Vector3(DropLuminanceNits, DropLuminanceNits, DropLuminanceNits));
                _vfx.SetFloat(AlphaId, look.rainAlpha);
                _vfx.SetFloat(FadedDistanceId, look.rainFadedDistanceM);
                _vfx.SetFloat(VisibleDistanceId, look.rainVisibleDistanceM);
            }
        }

        /// <summary>
        /// Drops to draw for a rain rate (LOOK): <paramref name="dropsAt10"/> · (R / 10 mm/h)^<paramref name="exponent"/>,
        /// held under 95 % of the graph's capacity; 0 when dry.
        /// </summary>
        public static float DrawnCount(double rainMmPerHour, double dropsAt10, double exponent, uint capacity) =>
            rainMmPerHour > 0 ? (float)Math.Min(dropsAt10 * Math.Pow(rainMmPerHour / 10.0, exponent), 0.95 * capacity) : 0f;

        /// <summary>A drop's luminance, nits: <paramref name="factor"/> · E / π for E lux of reckoned ground light.</summary>
        public static float DropLuminance(double groundLux, double factor) => (float)(factor * groundLux / Math.PI);

        /// <summary>The first exposed property the rain needs that the effect lacks, or null.</summary>
        static string MissingProperty(VisualEffect vfx)
        {
            foreach (string f in new[] { MetocRainVfx.Rate, MetocRainVfx.Lifetime, MetocRainVfx.DropWidth, MetocRainVfx.Stretch,
                                         MetocRainVfx.Alpha, MetocRainVfx.FadedDistance, MetocRainVfx.VisibleDistance })
                if (!vfx.HasFloat(f)) return f;
            foreach (string v in new[] { MetocRainVfx.BoxSize, MetocRainVfx.Velocity, MetocRainVfx.Color })
                if (!vfx.HasVector3(v)) return v;
            return null;
        }
    }
}
