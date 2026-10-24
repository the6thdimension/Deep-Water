using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.World;
using DeepWater.GeneralQuarters.Modules.Combat;
using DeepWater.GeneralQuarters.Modules.Presentation;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// Stand-in models for what the combat core is flying: the raid's threats and the ship's missiles in flight,
    /// placed each frame from the core's state (extrapolated from the last fixed step by velocity, so they don't
    /// stutter between steps). Presentation only, never read back by the simulation. With a presentation component they
    /// carry smoke trails (slice 6) that stay in the air after the stand-in is gone.
    /// </summary>
    public sealed class RangeProxies : MonoBehaviour
    {
        [SerializeField] ShipCombat combat;
        [Tooltip("Gives the stand-ins their trails. Empty: no trails.")]
        [SerializeField] CombatPresentation presentation;
        [Tooltip("Model for the ship's missiles (ESSM). A capsule stands in when empty.")]
        [SerializeField] GameObject missileModel;
        [Tooltip("Model for hostile anti-ship missiles. A capsule stands in when empty.")]
        [SerializeField] GameObject threatMissileModel;
        [Tooltip("Model for aircraft. A capsule stands in when empty.")]
        [SerializeField] GameObject aircraftModel;
        [SerializeField] Material friendlyMaterial;
        [SerializeField] Material hostileMaterial;
        [Tooltip("Scale for the threat model (a sea-skimmer is a little longer than an ESSM).")]
        [SerializeField] float threatScale = 1.25f;
        [Tooltip("Turns each model so its nose points along +Z, the stand-in's direction of flight (degrees). The ESSM model's nose is -X: (0, 90, 0).")]
        [SerializeField] Vector3 missileModelEuler, threatModelEuler, aircraftModelEuler;

        readonly Dictionary<int, Transform> _threats = new Dictionary<int, Transform>();
        readonly Dictionary<MissileInFlight, Transform> _missiles = new Dictionary<MissileInFlight, Transform>();
        readonly List<Transform> _missilesNewestFirst = new List<Transform>();
        readonly Stack<Transform> _freeThreats = new Stack<Transform>(), _freeAircraft = new Stack<Transform>(), _freeMissiles = new Stack<Transform>();
        readonly HashSet<int> _seenThreats = new HashSet<int>();
        readonly List<int> _goneThreats = new List<int>();
        readonly List<MissileInFlight> _goneMissiles = new List<MissileInFlight>();
        readonly Dictionary<int, TargetKind> _kindOf = new Dictionary<int, TargetKind>();
        readonly Dictionary<Transform, GameObject> _trails = new Dictionary<Transform, GameObject>();

        public void Bind(ShipCombat host) => combat = host;

        /// <summary>The stand-in for a live threat, by truth id.</summary>
        public bool TryGetThreat(int truthId, out Transform proxy) => _threats.TryGetValue(truthId, out proxy);

        /// <summary>The ship's missiles in flight, newest first.</summary>
        public IReadOnlyList<Transform> MissilesNewestFirst => _missilesNewestFirst;

        void LateUpdate()
        {
            if (combat == null || combat.Combat == null) return;
            float lead = Mathf.Max(0f, Time.time - Time.fixedTime);   // time since the last fixed step

            // Threats: one stand-in per live truth target.
            _seenThreats.Clear();
            IReadOnlyList<TruthTarget> world = combat.World;
            for (int i = 0; i < world.Count; i++)
            {
                TruthTarget t = world[i];
                if (!t.Alive) continue;
                _seenThreats.Add(t.Id);
                if (!_threats.TryGetValue(t.Id, out Transform proxy))
                {
                    proxy = Take(t.Kind);
                    proxy.name = $"{t.Kind} {t.Id}";
                    Place(proxy, t.Position + t.Velocity * lead, t.Velocity);
                    if (t.Kind == TargetKind.AntiShipMissile) StartTrail(proxy, false);
                    _threats.Add(t.Id, proxy);
                    _kindOf[t.Id] = t.Kind;
                }
                Place(proxy, t.Position + t.Velocity * lead, t.Velocity);
            }
            _goneThreats.Clear();
            foreach (KeyValuePair<int, Transform> kv in _threats) if (!_seenThreats.Contains(kv.Key)) _goneThreats.Add(kv.Key);
            for (int i = 0; i < _goneThreats.Count; i++)
            {
                int id = _goneThreats[i];
                Give(_threats[id], _kindOf[id]);
                _threats.Remove(id);
                _kindOf.Remove(id);
            }

            // The ship's missiles in flight.
            IReadOnlyList<MissileInFlight> inFlight = combat.Combat.Vls.InFlight;
            _missilesNewestFirst.Clear();
            for (int i = inFlight.Count - 1; i >= 0; i--)
            {
                MissileInFlight m = inFlight[i];
                if (m.Done) continue;
                if (!_missiles.TryGetValue(m, out Transform proxy))
                {
                    proxy = _freeMissiles.Count > 0 ? _freeMissiles.Pop() : Build(missileModel, friendlyMaterial, 1f, missileModelEuler, "ESSM");
                    proxy.gameObject.SetActive(true);
                    proxy.name = $"{m.Weapon} at track {m.TrackId}";
                    _missiles.Add(m, proxy);
                    Place(proxy, m.Entity.State.Position, m.Entity.State.Velocity);
                    StartTrail(proxy, true);
                }
                Place(proxy, m.Entity.State.Position + m.Entity.State.Velocity * lead, m.Entity.State.Velocity);
                _missilesNewestFirst.Add(proxy);
            }
            _goneMissiles.Clear();
            foreach (KeyValuePair<MissileInFlight, Transform> kv in _missiles) if (kv.Key.Done || !Contains(inFlight, kv.Key)) _goneMissiles.Add(kv.Key);
            for (int i = 0; i < _goneMissiles.Count; i++)
            {
                Transform proxy = _missiles[_goneMissiles[i]];
                StopTrail(proxy, true);
                proxy.gameObject.SetActive(false);
                _freeMissiles.Push(proxy);
                _missiles.Remove(_goneMissiles[i]);
            }
        }

        /// <summary>Clear every stand-in (a new run starts with an empty sky).</summary>
        public void Clear()
        {
            foreach (KeyValuePair<int, Transform> kv in _threats) Give(kv.Value, _kindOf[kv.Key]);
            _threats.Clear();
            _kindOf.Clear();
            foreach (Transform proxy in _missiles.Values) { StopTrail(proxy, true); proxy.gameObject.SetActive(false); _freeMissiles.Push(proxy); }
            _missiles.Clear();
            _missilesNewestFirst.Clear();
        }

        void StartTrail(Transform proxy, bool own)
        {
            if (presentation == null) return;
            GameObject trail = presentation.AttachTrail(proxy, own);
            if (trail != null) _trails[proxy] = trail;
        }

        void StopTrail(Transform proxy, bool own)
        {
            if (presentation == null || !_trails.TryGetValue(proxy, out GameObject trail)) return;
            presentation.ReleaseTrail(trail, own);
            _trails.Remove(proxy);
        }

        static bool Contains(IReadOnlyList<MissileInFlight> list, MissileInFlight m)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == m) return true;
            return false;
        }

        static void Place(Transform proxy, Vector3 position, Vector3 velocity)
        {
            proxy.position = position;
            if (velocity.sqrMagnitude > 1e-4f) proxy.rotation = Quaternion.LookRotation(velocity, Vector3.up);
        }

        Transform Take(TargetKind kind)
        {
            bool air = kind == TargetKind.Aircraft;
            Stack<Transform> free = air ? _freeAircraft : _freeThreats;
            Transform proxy = free.Count > 0 ? free.Pop()
                : air ? Build(aircraftModel, hostileMaterial, 1f, aircraftModelEuler, "Aircraft")
                : Build(threatMissileModel, hostileMaterial, threatScale, threatModelEuler, "Threat");
            proxy.gameObject.SetActive(true);
            return proxy;
        }

        void Give(Transform proxy, TargetKind kind)
        {
            StopTrail(proxy, false);
            proxy.gameObject.SetActive(false);
            (kind == TargetKind.Aircraft ? _freeAircraft : _freeThreats).Push(proxy);
        }

        Transform Build(GameObject model, Material material, float scale, Vector3 noseEuler, string label)
        {
            GameObject go;
            if (model != null)
            {
                go = Instantiate(model, transform);
                // A stand-in: strip anything that could act (colliders, scripts); keep only what renders.
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (MonoBehaviour b in go.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(b);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(0.4f, 2f, 0.4f);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            if (material != null)
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = material;
            // Wrap so the stand-in's forward is the object's velocity, whatever axis the model was authored on.
            var root = new GameObject(label).transform;
            root.SetParent(transform, false);
            go.transform.SetParent(root, false);
            go.transform.localScale *= scale;
            if (model != null) go.transform.localRotation = Quaternion.Euler(noseEuler) * go.transform.localRotation;
            return root;
        }
    }
}
