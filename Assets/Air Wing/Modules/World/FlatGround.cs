using UnityEngine;

namespace DeepWater.AirWing.Modules.World
{
    /// <summary>
    /// Flat solid ground for the truth feed: a rectangle in the horizontal plane, its top at this transform's height,
    /// its length along the transform's +Z and its width along +X (only the yaw counts). A runway, an apron, the top
    /// of a stretch of land. At L0 meeting it is a crash; landing gear comes at L1.
    /// <para>Renamed from <c>RunwayStrip</c> in slice 1e: the same shape serves land as well as runways, and a
    /// MonoBehaviour must live in a file of its own name for a scene to save it.</para>
    /// </summary>
    public sealed class FlatGround : MonoBehaviour
    {
        [Tooltip("Along the transform's +Z, m.")]
        [SerializeField] float lengthM = 2400f;
        [Tooltip("Along the transform's +X, m.")]
        [SerializeField] float widthM = 60f;

        public float TopHeightM => transform.position.y;
        public float LengthM => lengthM;
        public float WidthM => widthM;

        /// <summary>Sets the size from code (the scene builders).</summary>
        public void SetSize(float length, float width)
        {
            lengthM = length;
            widthM = width;
        }

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
}
