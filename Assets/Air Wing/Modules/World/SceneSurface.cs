using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.Sea.Core;

namespace DeepWater.AirWing.Modules.World
{
    /// <summary>
    /// The truth feed's ground for a scene (BLUEPRINT §3.1): the Sea State sea's height under a point, from the
    /// simulation (<see cref="ISeaSurface"/>), never from HDRP water (AP11); or the top of a <see cref="FlatGround"/>
    /// (land, a runway), where one lies under the point and stands higher. A scene with no sea uses a flat fallback
    /// height and says so once in the log.
    /// </summary>
    public sealed class SceneSurface : ISurfaceTruth
    {
        readonly ISeaSurface _sea;
        readonly IReadOnlyList<FlatGround> _ground;
        readonly float _fallbackHeightM;

        public SceneSurface(ISeaSurface sea, IReadOnlyList<FlatGround> ground, float fallbackHeightM)
        {
            _sea = sea;
            _ground = ground ?? new List<FlatGround>();
            _fallbackHeightM = fallbackHeightM;
            if (_sea == null)
                Debug.LogWarning($"[Air Wing] No Sea State sea in the scene: the surface is flat at {fallbackHeightM} m.");
        }

        public float HeightAt(float x, float z)
        {
            float h = _sea != null ? _sea.Sample(new float3(x, 0f, z)).SurfaceHeight : _fallbackHeightM;
            for (int i = 0; i < _ground.Count; i++)
                if (_ground[i].Contains(x, z) && _ground[i].TopHeightM > h) h = _ground[i].TopHeightM;
            return h;
        }
    }
}
