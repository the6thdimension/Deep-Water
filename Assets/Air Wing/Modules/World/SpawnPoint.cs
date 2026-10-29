using System.Collections.Generic;
using UnityEngine;
using DeepWater.AirWing.Modules.Aircraft;

namespace DeepWater.AirWing.Modules.World
{
    /// <summary>
    /// Where a group of jets starts and comes back (the arena's spawn point, W59). Its jets sit in line abreast across
    /// this transform's heading, <see cref="SpacingM"/> apart and centred on it, each in the slot of its index. In Awake,
    /// before Air Wing's runner builds anything, it moves its jets onto their slots, so moving the point moves the
    /// squadron; the runner then builds each jet there, and the arena brings it back to that state. Only the yaw of the
    /// transform counts. Presentation of the slots is a gizmo; nothing is drawn in play.
    /// </summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        [Tooltip("For the log and the gizmo label.")]
        [SerializeField] string label = "Blue";
        [Tooltip("The jets that start and respawn here, slot 0 first.")]
        [SerializeField] List<AircraftView> jets = new List<AircraftView>();
        [Tooltip("Line abreast: metres between neighbouring slots.")]
        [Min(0f)] [SerializeField] float spacingM = 300f;
        [SerializeField] Color gizmoColour = new Color(0.3f, 0.6f, 1f, 0.9f);

        public string Label => label;
        public IReadOnlyList<AircraftView> Jets => jets;
        public float SpacingM => spacingM;
        /// <summary>The point's colour (its gizmo, and its jets' tags in the spectator camera).</summary>
        public Color GizmoColour => gizmoColour;

        /// <summary>Slot <paramref name="index"/> of <paramref name="count"/>, in the world: centred on this point, across its heading.</summary>
        public Vector3 SlotPosition(int index, int count)
        {
            Quaternion yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            return transform.position + yaw * Vector3.right * ((index - 0.5f * (count - 1)) * spacingM);
        }

        public Quaternion Heading => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        /// <summary>Sets the point up from code (the scene builders).</summary>
        public void Configure(string pointLabel, IEnumerable<AircraftView> pointJets, float spacing, Color colour)
        {
            label = pointLabel;
            jets = new List<AircraftView>(pointJets);
            spacingM = spacing;
            gizmoColour = colour;
            PlaceJets();
        }

        void Awake() => PlaceJets();

        /// <summary>Every jet onto its slot, facing the point's heading.</summary>
        public void PlaceJets()
        {
            for (int i = 0; i < jets.Count; i++)
            {
                if (jets[i] == null) { Debug.LogWarning($"[Air Wing] spawn point {label}: slot {i} has no jet.", this); continue; }
                jets[i].transform.SetPositionAndRotation(SlotPosition(i, jets.Count), Heading);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColour;
            int n = Mathf.Max(1, jets.Count);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = SlotPosition(i, n);
                Gizmos.DrawWireSphere(p, 40f);
                Gizmos.DrawLine(p, p + Heading * Vector3.forward * 600f);
            }
            Gizmos.DrawLine(transform.position, new Vector3(transform.position.x, 0f, transform.position.z));
        }
    }
}
