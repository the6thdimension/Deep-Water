using System.Collections.Generic;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.Gunnery.Guns;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeepWater.AirWing.Modules.World
{
    /// <summary>
    /// Tracers (Gunnery K4): each volley in flight drawn as a short bright streak along its path, read from the flight
    /// core every frame. Presentation only; the rounds themselves fly in Gunnery. The runner adds one at start if the
    /// scene has none, so no scene needs saving for the guns to show.
    /// </summary>
    public sealed class TracerView : MonoBehaviour
    {
        [SerializeField] AirWingRunner runner;
        [Tooltip("Optional. Without one an HDRP Unlit material is made at start.")]
        [SerializeField] Material material;
        [SerializeField] Color colour = new Color(1f, 0.85f, 0.35f, 1f);
        [Tooltip("The streak's length, as seconds of the round's flight.")]
        [SerializeField] float streakS = 0.025f;
        [SerializeField] float widthM = 0.6f;

        readonly List<LineRenderer> _pool = new List<LineRenderer>();

        public void Use(AirWingRunner r) => runner = r;

        void Start()
        {
            if (runner == null) runner = GetComponent<AirWingRunner>();
            if (material == null)
            {
                Shader unlit = Shader.Find("HDRP/Unlit");
                if (unlit == null) { Debug.LogWarning("[Air Wing] tracers: no HDRP/Unlit shader and no material set; tracers off.", this); enabled = false; return; }
                material = new Material(unlit);
                material.SetColor("_UnlitColor", colour);
            }
        }

        void LateUpdate()
        {
            if (runner == null || runner.Flight == null) return;
            IReadOnlyList<Volley> live = runner.Flight.Rounds.Live;
            while (_pool.Count < live.Count) _pool.Add(Streak());
            for (int i = 0; i < _pool.Count; i++)
            {
                bool on = i < live.Count;
                if (_pool[i].enabled != on) _pool[i].enabled = on;
                if (!on) continue;
                Volley v = live[i];
                _pool[i].SetPosition(0, v.Position - v.Velocity * streakS);
                _pool[i].SetPosition(1, v.Position);
            }
        }

        LineRenderer Streak()
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = widthM;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
    }
}
