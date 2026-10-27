using UnityEngine;
using DeepWater.AirWing.ScriptableObjects;

namespace DeepWater.AirWing.Modules.Aircraft
{
    /// <summary>
    /// The model's landing gear following the core's <c>GearDeployed</c> (presentation only, never read by the core).
    /// Each part turns from its rest pose (gear down) about one hinge axis, given in the model's frame, by its stowed
    /// angle times its progress through its own window of the retraction: the legs fold first, the doors close last,
    /// and going down runs the same windows backwards (doors open, then legs). Parts that live inside the bays are
    /// hidden once the gear is fully up, behind the closed doors.
    /// </summary>
    public sealed class GearRig
    {
        readonly AircraftVisualsSO.GearPart[] _spec;
        readonly Transform[] _parts;
        readonly Quaternion[] _rest;
        readonly Vector3[] _axisLocal;
        readonly Renderer[][] _hidden;
        bool _hiddenNow;

        /// <summary>Finds each part under <paramref name="root"/>; <paramref name="model"/> is the frame the hinge axes are in.</summary>
        public GearRig(Transform root, Transform model, AircraftVisualsSO.GearPart[] spec, Object logContext = null)
        {
            _spec = spec ?? new AircraftVisualsSO.GearPart[0];
            int n = _spec.Length;
            _parts = new Transform[n];
            _rest = new Quaternion[n];
            _axisLocal = new Vector3[n];
            _hidden = new Renderer[n][];
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < n; i++)
            {
                string name = _spec[i].transformName;
                foreach (Transform t in all)
                    if (t.name == name) { _parts[i] = t; break; }
                Transform p = _parts[i];
                if (p == null)
                {
                    Debug.LogWarning($"[Air Wing] gear part '{name}': no such transform under the model; it will not move.", logContext);
                    continue;
                }
                _rest[i] = p.localRotation;
                // The hinge axis in the part's parent frame, taken at rest, so the rotation is local and frame-rate free.
                Quaternion parentInModel = Quaternion.Inverse(model.rotation) * p.parent.rotation;
                _axisLocal[i] = (Quaternion.Inverse(parentInModel) * _spec[i].axis).normalized;
                // Only renderers that are on at rest: the model keeps some spare meshes switched off, and they stay off.
                _hidden[i] = _spec[i].hideWhenStowed ? System.Array.FindAll(p.GetComponentsInChildren<Renderer>(true), x => x.enabled) : new Renderer[0];
            }
        }

        public int PartsFound
        {
            get { int k = 0; foreach (Transform t in _parts) if (t != null) k++; return k; }
        }

        /// <summary>How far a part with window [start, end] has moved when the retraction is <paramref name="retraction"/> (0 down … 1 up).</summary>
        public static float Progress(float retraction, float start, float end)
        {
            float x = Mathf.Clamp01((retraction - start) / Mathf.Max(end - start, 1e-4f));
            return x * x * (3f - 2f * x);   // eased at both ends, like a hydraulic jack
        }

        /// <summary>Poses every part for <paramref name="deployed"/> (the core's GearDeployed: 1 down … 0 up).</summary>
        public void Pose(float deployed)
        {
            float r = 1f - Mathf.Clamp01(deployed);
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] == null) continue;
                AircraftVisualsSO.GearPart s = _spec[i];
                float a = s.stowedAngleDeg * Progress(r, s.startFraction, s.endFraction);
                _parts[i].localRotation = Quaternion.AngleAxis(a, _axisLocal[i]) * _rest[i];
            }
            bool hide = r >= 0.999f;
            if (hide == _hiddenNow) return;
            _hiddenNow = hide;
            foreach (Renderer[] set in _hidden)
                if (set != null)
                    foreach (Renderer x in set) x.enabled = !hide;
        }
    }
}
