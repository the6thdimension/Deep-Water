using System;
using UnityEngine;

namespace DeepWater.GeneralQuarters.ScriptableObjects
{
    /// <summary>
    /// The hull layout written by <c>ArtSource/General Quarters/author_ddg51_gq.py</c> (DDG51_GQ_Layout.json):
    /// hydrostatics measured from the Blender hull on a fine column grid, the coarser buoyancy columns Sea State
    /// floats, and AUTHORED compartments, in the Unity ship frame (x starboard, y up, z forward, origin at the
    /// waterline amidships). Field names follow the JSON. Pivots and launch points are not read from here: they
    /// travel as transforms in the FBX.
    /// </summary>
    [Serializable]
    public class HullLayout
    {
        public string source;
        public string frame;
        public Hydrostatics hydrostatics;
        public string hydrostatics_note;
        public Probes probes;
        public CompartmentBox[] compartments;
        public string compartments_note;

        [Serializable]
        public class Hydrostatics
        {
            public float waterline_y, displacement_t, mass_kg, keel_y, draft_m, deepest_y, lcb_z, kb_m, bm_m;
            public float waterplane_m2, beam_wl_m, length_wl_m, gm_m, kg_m;
            public string gm_note, gyradius_note;
            public float[] centre_of_mass;
            public Gyradius gyradius_m;
        }

        [Serializable]
        public class Gyradius
        {
            public float roll, pitch, yaw;
        }

        [Serializable]
        public class Probes
        {
            public string grid;
            public float area_m2;
            /// <summary>The probe grid's own hydrostatics, for the record: how far its discretisation sits from the hull's.</summary>
            public float displacement_t, bm_m, kb_above_keel_m, lcb_z;
            public Column[] columns;
        }

        [Serializable]
        public class Column
        {
            public float[] centre;
            public float height;
        }

        [Serializable]
        public class CompartmentBox
        {
            public int index;
            public string name;
            public float[] min;
            public float[] max;
        }

        public static HullLayout Parse(string json) => JsonUtility.FromJson<HullLayout>(json);

        public static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);
    }
}
