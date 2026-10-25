using System;
using UnityEngine;
using DeepWater.GeneralQuarters.Core.DamageControl;
using DeepWater.Sea.ScriptableObjects;

namespace DeepWater.GeneralQuarters.ScriptableObjects
{
    /// <summary>
    /// A ship's hull as General Quarters sees it (P1): mass properties, hydrostatics, compartments, and the
    /// Sea State buoyancy asset that floats it. Built from the Blender layout by the DDG-51 hull builder;
    /// <see cref="provenance"/> says which numbers were measured from the mesh and which are AUTHORED.
    /// </summary>
    [CreateAssetMenu(fileName = "NewShipHull", menuName = "Deep Water/General Quarters/Ship Hull Profile", order = 20)]
    public class ShipHullProfileSO : ScriptableObject
    {
        [Serializable]
        public struct Compartment
        {
            public string name;
            /// <summary>Box corners in the ship frame, m.</summary>
            public Vector3 min, max;
        }

        [Tooltip("Sea State buoyancy columns for this hull.")]
        public BuoyancyHullSO buoyancy;

        [Header("Mass properties")]
        [Min(1f)] public float massKg = 1000f;
        [Tooltip("Centre of mass in the ship frame, m (x starboard, y up, z forward; origin at the waterline amidships).")]
        public Vector3 centerOfMassM;
        [Tooltip("Radii of gyration about the centre of mass, m: x = pitch (about the beam axis), y = yaw, z = roll.")]
        public Vector3 gyradiusM = Vector3.one;

        [Header("Hydrostatics (from the layout)")]
        public float displacementT;
        public float designDraftM;
        public float kbM;
        public float bmM;
        public float metacentricHeightM;
        [Tooltip("Waterline length, m (trim is reported over it).")]
        public float waterlineLengthM;

        [Header("Compartments")]
        [Tooltip("Blender's watertight stations (G53), bow first. Damage Control L1 subdivides them below.")]
        public Compartment[] compartments = Array.Empty<Compartment>();

        [Header("Subdivision (Damage Control L1, G53 G54)")]
        [Tooltip("The damage-control deck, ship-frame y, m: holds below it, upper spaces above.")]
        public float damageControlDeckY = 3.4f;
        [Tooltip("In a wing station, hold columns beyond this |x| (m) are the port and starboard wing spaces.")]
        public float wingInboardXM = 5.32f;
        [Tooltip("The first and last compartment (indices) that have wing spaces.")]
        public int wingFirstStation = 3, wingLastStation = 8;
        [Tooltip("What each compartment's hold is used for (one per compartment, in order): it sets the permeability.")]
        public SpaceUse[] holdUse = Array.Empty<SpaceUse>();
        [Tooltip("Permeability by use, SOLAS II-1 Reg. 7-3. Upper spaces count as accommodation, wing spaces as voids.")]
        public float permeabilityStores = 0.60f, permeabilityMachinery = 0.85f, permeabilityAccommodation = 0.95f, permeabilityVoid = 0.95f;

        [Header("Openings (Damage Control L1, G56)")]
        [Tooltip("Each hold's hatch up through the damage-control deck, m² of leak path.")]
        public float hatchAreaM2 = 0.1f;
        [Tooltip("Each door between neighbouring upper spaces, m² of leak path, and its sill above the deck, m.")]
        public float doorAreaM2 = 0.1f, doorSillM = 0.3f;
        [Tooltip("Each upper space's weather openings at the deck edge, per side, m².")]
        public float weatherAreaM2 = 0.5f;

        [TextArea(4, 24)] public string provenance;

        /// <summary>Principal moments of inertia, kg·m², about x (pitch), y (yaw), z (roll): m k².</summary>
        public Vector3 InertiaTensor => massKg * Vector3.Scale(gyradiusM, gyradiusM);

        /// <summary>
        /// The hull as Damage Control L1 floods it: the buoyancy columns, the stations, the subdivision and the openings
        /// from this profile; the water and loss tuning from the loadout (<paramref name="tuning"/>).
        /// </summary>
        public FloodingProfileData BakeFlooding(in FloodingTuning tuning)
        {
            if (buoyancy == null || buoyancy.probes.Length == 0)
                throw new InvalidOperationException($"Hull '{name}': Damage Control L1 floods the buoyancy columns, and there are none.");
            if (holdUse.Length != compartments.Length)
                throw new InvalidOperationException($"Hull '{name}': {compartments.Length} compartments but {holdUse.Length} hold uses.");

            var columns = new HullColumn[buoyancy.probes.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                BuoyancyHullSO.Probe p = buoyancy.probes[i];
                columns[i] = new HullColumn
                {
                    X = p.localPosition.x, Z = p.localPosition.z, AreaM2 = p.areaM2,
                    BottomY = p.localPosition.y - 0.5f * p.heightM, TopY = p.localPosition.y + 0.5f * p.heightM,
                };
            }
            var stations = new HullStation[compartments.Length];
            for (int i = 0; i < stations.Length; i++)
            {
                string label = compartments[i].name;
                int space = label.LastIndexOf(' ');
                stations[i] = new HullStation
                {
                    Name = space >= 0 ? label.Substring(space + 1) : label,
                    AftZ = compartments[i].min.z, ForeZ = compartments[i].max.z, HoldUse = holdUse[i],
                };
            }
            return new FloodingProfileData
            {
                Columns = columns, Stations = stations,
                DeckY = damageControlDeckY, WingInboardX = wingInboardXM, WingFirstStation = wingFirstStation, WingLastStation = wingLastStation,
                PermeabilityStores = permeabilityStores, PermeabilityMachinery = permeabilityMachinery,
                PermeabilityAccommodation = permeabilityAccommodation, PermeabilityVoid = permeabilityVoid,
                HatchAreaM2 = hatchAreaM2, DoorAreaM2 = doorAreaM2, DoorSillM = doorSillM, WeatherAreaM2 = weatherAreaM2,
                ShipMassKg = massKg, ShipCentreOfMass = centerOfMassM, KeelY = -designDraftM, LengthWlM = waterlineLengthM,
                SeawaterDensityKgM3 = tuning.seawaterDensityKgM3, Gravity = tuning.gravity, DischargeCoefficient = tuning.dischargeCoefficient,
                HoleAreaAtRefM2 = tuning.holeAreaAtRefM2, HoleRefWarheadKg = tuning.holeRefWarheadKg,
                CapsizeHeelDeg = tuning.capsizeHeelDeg, CapsizeHoldS = tuning.capsizeHoldS,
            };
        }

        /// <summary>Undamped natural roll period, s: 2π k_roll / √(g GM).</summary>
        public float NaturalRollPeriodS(float gravity = 9.81f) =>
            metacentricHeightM > 0f ? 2f * Mathf.PI * gyradiusM.z / Mathf.Sqrt(gravity * metacentricHeightM) : float.NaN;
    }
}
