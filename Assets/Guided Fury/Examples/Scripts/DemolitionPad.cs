using UnityEngine;

namespace GuidedFury.Examples
{
    /// <summary>
    /// Range-control for the demolition pad: pressing the trigger key detonates the next
    /// armed <see cref="DemolitionCharge"/> under this object, in child order. Debug/range
    /// tooling only — scans children on demand, no per-frame cost beyond the key check.
    /// </summary>
    public sealed class DemolitionPad : MonoBehaviour
    {
        [Tooltip("Key that detonates the next armed charge on the pad.")]
        [SerializeField] private KeyCode detonateKey = KeyCode.B;

        private void Update()
        {
            if (!Input.GetKeyDown(detonateKey)) return;

            var charges = GetComponentsInChildren<DemolitionCharge>(includeInactive: false);
            foreach (var charge in charges)
            {
                if (!charge.IsArmed) continue;
                charge.Detonate();
                return;
            }
            Debug.Log("[GuidedFury] Demolition pad: no armed charges left. Re-enable them in the hierarchy or rebuild the range.");
        }

        private void OnGUI()
        {
            // One-line hint above the standard camera help blurb.
            var rect = new Rect(12f, Screen.height - 68f, 320f, 18f);
            int armed = 0;
            foreach (var c in GetComponentsInChildren<DemolitionCharge>(false))
                if (c.IsArmed) armed++;
            GUI.Label(rect, $"Demo pad: [{detonateKey}] detonate charge ({armed} armed)");
        }
    }
}
