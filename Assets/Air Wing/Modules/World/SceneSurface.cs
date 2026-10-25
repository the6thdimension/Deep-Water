using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.Sea.Core;

namespace DeepWater.AirWing.Modules.World
{
    /// <summary>
    /// A flat runway: a rectangle in the horizontal plane, its top at this transform's height, length along its +Z
    /// and width along its +X. The truth feed treats it as ground above the sea (L0 crashes on it; gear from L1).
    /// </summary>
    public sealed class RunwayStrip : MonoBehaviour
    {
        [SerializeField] float lengthM = 2400f;
        [SerializeField] float widthM = 60f;

        public float TopHeightM => transform.position.y;

        public bool Contains(float x, float z)
        {
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, transform.eulerAngles.y, 0f)) * new Vector3(x - transform.position.x, 0f, z - transform.position.z);
            return Mathf.Abs(local.x) <= 0.5f * widthM && Mathf.Abs(local.z) <= 0.5f * lengthM;
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), Vector3.one);
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(widthM, 0.5f, lengthM));
        }
    }

    /// <summary>
    /// The truth feed's ground for a scene (BLUEPRINT §3.1): the Sea State sea's height under a point, from the
    /// simulation (<see cref="ISeaSurface"/>), never from HDRP water (AP11); or a runway's top, where one lies under
    /// the point and stands higher. A scene with no sea uses a flat fallback height and says so once in the log.
    /// </summary>
    public sealed class SceneSurface : ISurfaceTruth
    {
        readonly ISeaSurface _sea;
        readonly IReadOnlyList<RunwayStrip> _runways;
        readonly float _fallbackHeightM;

        public SceneSurface(ISeaSurface sea, IReadOnlyList<RunwayStrip> runways, float fallbackHeightM)
        {
            _sea = sea;
            _runways = runways ?? new List<RunwayStrip>();
            _fallbackHeightM = fallbackHeightM;
            if (_sea == null)
                Debug.LogWarning($"[Air Wing] No Sea State sea in the scene: the surface is flat at {fallbackHeightM} m.");
        }

        public float HeightAt(float x, float z)
        {
            float h = _sea != null ? _sea.Sample(new float3(x, 0f, z)).SurfaceHeight : _fallbackHeightM;
            for (int i = 0; i < _runways.Count; i++)
                if (_runways[i].Contains(x, z) && _runways[i].TopHeightM > h) h = _runways[i].TopHeightM;
            return h;
        }
    }
}
