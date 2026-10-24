using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Modules.Presentation
{
    /// <summary>
    /// Pooled particle-effect instances. One-shot effects play and come back after their slot's lifetime; looping ones
    /// (fire, smoke, trails) stay until released. Instances are created on first use and reused, so a raid allocates
    /// only while the pools grow.
    /// </summary>
    public sealed class EffectPool
    {
        sealed class Live
        {
            public GameObject Prefab, Instance;
            public ParticleSystem[] Systems;
            public TrailRenderer[] Trails;
            public float ReturnAt;
        }

        readonly Transform _root;
        readonly Dictionary<GameObject, Stack<Live>> _free = new Dictionary<GameObject, Stack<Live>>();
        readonly List<Live> _oneShots = new List<Live>();
        readonly Dictionary<GameObject, Live> _byInstance = new Dictionary<GameObject, Live>();

        public EffectPool(Transform root) => _root = root;

        /// <summary>Instances in use (one-shot and looping).</summary>
        public int Active => _byInstance.Count;

        /// <summary>Play a one-shot effect at a point, facing <paramref name="forward"/>. Null when the slot has no prefab.</summary>
        public GameObject Play(in EffectSlot slot, Vector3 position, Vector3 forward, float scaleFactor = 1f, float now = 0f, float lifetimeOverride = -1f)
        {
            Live l = Take(slot, position, forward, scaleFactor, null);
            if (l == null) return null;
            l.ReturnAt = now + (lifetimeOverride > 0f ? lifetimeOverride : Mathf.Max(0.1f, slot.lifetimeS));
            _oneShots.Add(l);
            return l.Instance;
        }

        /// <summary>Start a looping effect, parented to <paramref name="parent"/> (or the pool root). Release it with <see cref="Release"/>.</summary>
        public GameObject Attach(in EffectSlot slot, Transform parent, Vector3 position, Vector3 forward, float scaleFactor = 1f)
        {
            Live l = Take(slot, position, forward, scaleFactor, parent);
            return l?.Instance;
        }

        /// <summary>
        /// Stop a looping effect: it stops emitting, moves off whatever it was attached to, and returns to the pool once its
        /// last particles have faded (<paramref name="fadeS"/>).
        /// </summary>
        public void Release(GameObject instance, float now, float fadeS)
        {
            if (instance == null || !_byInstance.TryGetValue(instance, out Live l)) return;
            instance.transform.SetParent(_root, true);
            foreach (ParticleSystem ps in l.Systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            foreach (TrailRenderer tr in l.Trails) tr.emitting = false;   // the ribbon already laid stays and fades
            l.ReturnAt = now + fadeS;
            if (!_oneShots.Contains(l)) _oneShots.Add(l);
        }

        /// <summary>Return everything to the pool now (a new run).</summary>
        public void ReleaseAll()
        {
            var all = new List<Live>(_byInstance.Values);
            foreach (Live l in all) Return(l);
            _oneShots.Clear();
        }

        public void Update(float now)
        {
            for (int i = _oneShots.Count - 1; i >= 0; i--)
            {
                if (now < _oneShots[i].ReturnAt) continue;
                Return(_oneShots[i]);
                _oneShots.RemoveAt(i);
            }
        }

        Live Take(in EffectSlot slot, Vector3 position, Vector3 forward, float scaleFactor, Transform parent)
        {
            if (slot.prefab == null) return null;
            if (!_free.TryGetValue(slot.prefab, out Stack<Live> stack)) _free[slot.prefab] = stack = new Stack<Live>();
            Live l;
            if (stack.Count > 0) l = stack.Pop();
            else
            {
                GameObject go = Object.Instantiate(slot.prefab, _root);
                l = new Live
                {
                    Prefab = slot.prefab, Instance = go, Systems = go.GetComponentsInChildren<ParticleSystem>(true),
                    Trails = go.GetComponentsInChildren<TrailRenderer>(true),
                };
            }
            Transform t = l.Instance.transform;
            t.SetParent(parent != null ? parent : _root, false);
            Vector3 up = Mathf.Abs(forward.normalized.y) > 0.99f ? Vector3.forward : Vector3.up;   // an effect pointed straight up needs another "up"
            t.SetPositionAndRotation(position, forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, up) : Quaternion.identity);
            t.localScale = Vector3.one * Mathf.Max(0.01f, (slot.scale > 0f ? slot.scale : 1f) * scaleFactor);
            l.Instance.SetActive(true);
            foreach (ParticleSystem ps in l.Systems) { ps.Clear(false); ps.Play(false); }
            foreach (TrailRenderer tr in l.Trails) { tr.Clear(); tr.emitting = true; }
            _byInstance[l.Instance] = l;
            return l;
        }

        void Return(Live l)
        {
            foreach (ParticleSystem ps in l.Systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (TrailRenderer tr in l.Trails) { tr.emitting = false; tr.Clear(); }
            l.Instance.SetActive(false);
            l.Instance.transform.SetParent(_root, false);
            _byInstance.Remove(l.Instance);
            _free[l.Prefab].Push(l);
        }
    }
}
