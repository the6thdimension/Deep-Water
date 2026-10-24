using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Modules.Combat;
using DeepWater.GeneralQuarters.ScriptableObjects;
using DeepWater.Sea.Modules.Hdrp;

namespace DeepWater.GeneralQuarters.Modules.Presentation
{
    /// <summary>
    /// How the fight looks and sounds (slice 6, G18; G30–G33). It reads the combat core's engagement log as it grows and
    /// turns each event into presentation:
    /// <list type="bullet">
    /// <item>Launches open a VLS hatch and throw a plume.</item>
    /// <item>Bursts lay the mount on its aim point, flash the muzzle and stream tracers.</item>
    /// <item>Detonations and kills flash, burn and smoke; anything that comes down low throws a splash and leaves foam.</item>
    /// <item>Hits explode and keep burning; systems that go offline smoke.</item>
    /// <item>Every event sounds, late by its distance at the speed of sound.</item>
    /// <item>The ship lays a foam wake and throws bow spray as she makes way.</item>
    /// </list>
    /// Unreckoned (BLUEPRINT §4, Presentation): it only reads, so turning it off changes nothing in the run, only the
    /// frame cost, which is how its cost is measured.
    /// </summary>
    [DefaultExecutionOrder(500)]   // after the combat host has stepped and the drive has moved the ship
    public sealed class CombatPresentation : MonoBehaviour
    {
        const float KnToMs = 0.514444f;
        static readonly ProfilerMarker Marker = new ProfilerMarker("GQ.Presentation");

        [SerializeField] ShipCombat combat;
        [SerializeField] CombatPresentationSO presentation;
        [Tooltip("The sea's renderer, for splash and wake foam. Empty: effects without foam.")]
        [SerializeField] HdrpSeaRenderer sea;
        [Tooltip("Where the sounds are heard from (the camera). Empty: the ship.")]
        [SerializeField] Transform listener;
        [Tooltip("Off: no effects, foam or sound (the run is unchanged). For measuring what presentation costs.")]
        [SerializeField] bool effectsOn = true;

        Transform _root, _ship;
        EffectPool _pool;
        MountAnimator _mounts;
        VlsHatches _hatches;
        CombatAudio _audio;
        CombatSystem _seen;
        int _cursor;
        float _nextWake;
        GameObject _bowSpray;
        ParticleSystem[] _bowSystems;
        readonly List<GameObject> _persistent = new List<GameObject>();

        public bool EffectsOn
        {
            get => effectsOn;
            set { effectsOn = value; if (!value) ClearScene(); }
        }

        /// <summary>Effect instances alive now.</summary>
        public int EffectsActive => _pool != null ? _pool.Active : 0;

        /// <summary>Start a looping trail on a missile stand-in (ESSM or threat). Release it with <see cref="ReleaseTrail"/>.</summary>
        public GameObject AttachTrail(Transform missile, bool ownMissile)
        {
            if (!effectsOn || _pool == null || missile == null) return null;
            EffectSlot slot = ownMissile ? presentation.essmTrail : presentation.threatTrail;
            return _pool.Attach(slot, missile, missile.position, missile.forward);
        }

        /// <summary>Stop a trail; its smoke stays in the air and fades.</summary>
        public void ReleaseTrail(GameObject trail, bool ownMissile)
        {
            if (trail == null || _pool == null) return;
            EffectSlot slot = ownMissile ? presentation.essmTrail : presentation.threatTrail;
            _pool.Release(trail, Time.time, Mathf.Max(0.5f, slot.lifetimeS));
        }

        void Awake()
        {
            if (combat == null || presentation == null)
            {
                Debug.LogError("[CombatPresentation] needs the ship's combat system and a presentation profile; the fight will be silent and unseen.", this);
                enabled = false;
                return;
            }
            _ship = combat.transform;
            _root = new GameObject("Presentation (pooled)").transform;
            _root.SetParent(transform, false);
            _pool = new EffectPool(_root);
            _audio = new CombatAudio(_root, presentation.voices, presentation.speedOfSoundMs);
            _hatches = new VlsHatches(_ship, presentation, _root);
            combat.RaidStarted += OnRaidStarted;
        }

        void Start()
        {
            if (combat.Combat == null) { Debug.LogError("[CombatPresentation] the combat system has not started.", this); enabled = false; return; }
            _mounts = new MountAnimator(_ship, combat.Combat.Guns, presentation.mounts);
            if (presentation.bowSpray.prefab != null)
            {
                _bowSpray = _pool.Attach(presentation.bowSpray, _ship, _ship.TransformPoint(presentation.bowSprayPoint), _ship.forward);
                _bowSystems = _bowSpray != null ? _bowSpray.GetComponentsInChildren<ParticleSystem>() : null;
            }
        }

        void OnDestroy()
        {
            if (combat != null) combat.RaidStarted -= OnRaidStarted;
        }

        void OnRaidStarted(ShipCombat host)
        {
            if (effectsOn) _audio.PlayFlat(presentation.alarmSound);
        }

        void LateUpdate()
        {
            CombatSystem c = combat.Combat;
            if (c == null) return;
            float now = Time.time;
            if (c != _seen)
            {
                // A fresh run: its log starts again, and the last run's fires and smoke go out.
                _seen = c;
                _cursor = 0;
                ClearScene();
                _mounts?.Bind(c.Guns);
            }

            IReadOnlyList<EngagementEvent> events = c.Log.Events;
            using var _ = Marker.Auto();
            if (effectsOn)
            {
                Vector3 listener = ListenerPosition();
                for (int i = _cursor; i < events.Count; i++) Handle(events[i], now, listener);
                _mounts.Update(now, Time.deltaTime);
                _hatches.Update(now);
                Wake(now);
            }
            _cursor = events.Count;
            _pool.Update(now);
        }

        void Handle(in EngagementEvent e, float now, Vector3 listener)
        {
            if (!e.HasPoint) return;
            CombatPresentationSO p = presentation;
            switch (e.Kind)
            {
                case EventKind.Launched:
                {
                    Transform cell = _hatches.Launch(e.Source, now);
                    Vector3 at = cell != null ? cell.position : e.Point;
                    _pool.Play(p.vlsLaunch, at, Vector3.up, 1f, now);
                    _audio.Play(p.vlsLaunchSound, at, listener, now);
                    break;
                }
                case EventKind.BurstFired:
                {
                    _mounts.Engage(e.Source, e.Point, now);
                    if (!_mounts.TryMuzzle(e.Source, out Vector3 muzzle, out _)) muzzle = _ship.position + Vector3.up * 10f;
                    Vector3 toAim = e.Point - muzzle;
                    EffectSlot slot = e.Weapon == WeaponClass.Mk45 ? p.muzzleMk45 : e.Weapon == WeaponClass.Ciws ? p.muzzleCiws : p.muzzleMk38;
                    GameObject fx = _pool.Play(slot, muzzle, toAim, 1f, now);
                    SetTracerReach(fx, toAim.magnitude);
                    _audio.Play(e.Weapon == WeaponClass.Mk45 ? p.mk45Sound : e.Weapon == WeaponClass.Ciws ? p.ciwsSound : p.mk38Sound, muzzle, listener, now);
                    break;
                }
                case EventKind.Kill:
                case EventKind.Miss:
                {
                    bool missile = e.Weapon == WeaponClass.Essm || e.Weapon == WeaponClass.Sm2;
                    bool low = e.Point.y < p.splashBelowAltitudeM;
                    if (missile || e.Kind == EventKind.Kill)
                    {
                        float scale = missile ? 1f : e.Weapon == WeaponClass.Mk45 ? 0.6f : 0.4f;
                        _pool.Play(p.warhead, e.Point, Vector3.up, scale, now);
                        _audio.Play(p.detonationSound, e.Point, listener, now);
                    }
                    else if (e.Weapon == WeaponClass.Mk45)
                    {
                        _pool.Play(p.airburst, e.Point, Vector3.up, 1f, now);
                        _audio.Play(p.airburstSound, e.Point, listener, now);
                    }
                    // A kill at sea-skimming height drops into the water; so do rounds that miss low.
                    if (low && (e.Kind == EventKind.Kill || e.Weapon == WeaponClass.Mk45)) Splash(e.Point, e.Kind == EventKind.Kill ? 1f : 0.5f, listener, now);
                    break;
                }
                case EventKind.ShipHit:
                {
                    _pool.Play(p.shipHit, e.Point, Vector3.up, 1f, now);
                    _persistent.Add(_pool.Attach(p.fire, _ship, e.Point, Vector3.up));
                    _audio.Play(p.shipHitSound, e.Point, listener, now);
                    break;
                }
                case EventKind.SystemOffline:
                    if (e.Source >= 0) _persistent.Add(_pool.Attach(p.systemSmoke, _ship, e.Point, Vector3.up));
                    break;
                case EventKind.Note:
                    // A missile that self-destructs because its target is already gone.
                    if (e.Weapon == WeaponClass.Essm || e.Weapon == WeaponClass.Sm2) _pool.Play(p.warhead, e.Point, Vector3.up, 0.4f, now);
                    break;
            }
        }

        void Splash(Vector3 at, float scale, Vector3 listener, float now)
        {
            CombatPresentationSO p = presentation;
            var water = new Vector3(at.x, 0f, at.z);
            _pool.Play(p.splash, water, Vector3.up, scale, now);
            if (sea != null) sea.DepositFoam(water, p.splashFoamRadiusM * scale, p.splashFoamLevel);
            _audio.Play(p.splashSound, water, listener, now);
        }

        /// <summary>Tracers live as long as rounds take to reach the aim point.</summary>
        void SetTracerReach(GameObject fx, float distanceM)
        {
            if (fx == null) return;
            Transform tracers = fx.transform.Find("Tracers");
            if (tracers == null || !tracers.TryGetComponent(out ParticleSystem ps)) return;
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = Mathf.Clamp(distanceM / Mathf.Max(1f, presentation.tracerSpeedMs), 0.05f, 3f);
        }

        void Wake(float now)
        {
            CombatPresentationSO p = presentation;
            float kn = Mathf.Max(0f, combat.Drive.Motion.SurgeMs / KnToMs);
            if (_bowSystems != null)
                foreach (ParticleSystem ps in _bowSystems)
                {
                    ParticleSystem.EmissionModule em = ps.emission;
                    em.rateOverTime = p.bowSprayRateAt30Kn * (kn / 30f) * (kn / 30f);
                }
            if (sea == null || now < _nextWake || kn < p.wakeMinSpeedKn) return;
            _nextWake = now + p.wakeIntervalS;
            float level = p.wakeFoamLevelAt30Kn * Mathf.Clamp01(kn / 30f);
            for (int i = 0; i < p.wakePoints.Length; i++) sea.DepositFoam(_ship.TransformPoint(p.wakePoints[i]), p.wakeFoamRadiusM, level);
        }

        void ClearScene()
        {
            if (_pool == null) return;
            foreach (GameObject go in _persistent) if (go != null) _pool.Release(go, Time.time, 0.01f);
            _persistent.Clear();
            _hatches.Clear();
        }

        Vector3 ListenerPosition() => listener != null ? listener.position : _ship.position;
    }
}
