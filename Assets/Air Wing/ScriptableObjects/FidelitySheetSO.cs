using UnityEngine;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Simulation;

namespace DeepWater.AirWing.ScriptableObjects
{
    /// <summary>
    /// An aircraft's fidelity sheet (BLUEPRINT §2, W25): the ladder level each part asks for, the substeps per
    /// tick (W24) and the seed (P7). Called a sheet, not a "loadout": in aviation a loadout is weapons and stores.
    /// Baked once into a <see cref="FidelitySheet"/> when a run starts (C7). A level that isn't built yet falls back
    /// out loud when the aircraft is assembled (C14).
    /// </summary>
    [CreateAssetMenu(fileName = "NewFidelitySheet", menuName = "Deep Water/Air Wing/Fidelity Sheet", order = 10)]
    public class FidelitySheetSO : ScriptableObject
    {
        [Header("Ladder levels (BLUEPRINT §4)")]
        public FidelityLevel airframe = FidelityLevel.L0;
        public FidelityLevel powerplant = FidelityLevel.L0;
        public FidelityLevel flightControls = FidelityLevel.L0;
        public FidelityLevel landingGear = FidelityLevel.L0;
        public FidelityLevel atmosphere = FidelityLevel.L0;

        [Header("Clock (W24)")]
        [Tooltip("Fixed substeps inside each 50 Hz tick. 4 gives 200 Hz.")]
        [Range(1, SubstepClock.MaxSubsteps)]
        public int substepsPerTick = 4;

        [Header("Randomness (P7)")]
        [Tooltip("Seed of the flight core's DeterministicRng: same seed, same input log gives the same run.")]
        public long seed = 1442;

        public FidelitySheet Bake() =>
            new FidelitySheet(airframe, powerplant, flightControls, landingGear, atmosphere, substepsPerTick, unchecked((ulong)seed));
    }
}
