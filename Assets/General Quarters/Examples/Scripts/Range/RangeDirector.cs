using System;
using System.Collections.Generic;
using UnityEngine;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Captain;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.Scenario;
using DeepWater.GeneralQuarters.Modules.Combat;
using DeepWater.GeneralQuarters.ScriptableObjects;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// Runs the General Quarters range: the seats (G3, G26), the raids, time compression and what the camera watches.
    /// <list type="bullet">
    /// <item><b>Seats:</b> the player holds the Captain or the TAO seat and switches with Tab / Start / button 6. The AI
    /// captain fills the other one. The conn reads its controls only in the Captain seat, the TAO keys only in the TAO
    /// seat; the screen and the camera's window change with the seat.</item>
    /// <item><b>Raids:</b> G / hold Start / hold button 6 starts the chosen raid; after a debrief it starts a fresh run
    /// (full magazines, no damage) with the ship keeping her way.</item>
    /// <item><b>Time compression:</b> T / right-stick press / button 7 steps 1× → 2× → 4×. Safe for determinism: the
    /// combat core counts fixed ticks, and more of them run per frame.</item>
    /// <item><b>Camera:</b> C / left-stick press / button 8 cycles the ship → the selected track → missiles in flight.</item>
    /// </list>
    /// </summary>
    public sealed class RangeDirector : MonoBehaviour
    {
        [SerializeField] RangeProfileSO range;
        [SerializeField] ShipCombat combat;
        [SerializeField] ConnController conn;
        [SerializeField] RangeInput input;
        [SerializeField] RangeProxies proxies;
        [SerializeField] ShipChaseCamera chaseCamera;
        [SerializeField] Camera viewCamera;
        [Tooltip("The seat the player starts in.")]
        [SerializeField] Seat startSeat = Seat.Captain;

        int _raidIndex, _timeIndex;
        float _lingerUntil = -1f;
        readonly List<Transform> _cameraTargets = new List<Transform>();

        public Seat PlayerSeat { get; private set; }
        public TaoStation Tao { get; private set; }
        public ShipCombat Combat => combat;
        public ConnController Conn => conn;
        public RangeProfileSO Range => range;
        public RaidScenarioSO SelectedRaid => range.raids.Length > 0 ? range.raids[Mathf.Clamp(_raidIndex, 0, range.raids.Length - 1)] : null;
        public int SelectedRaidIndex => _raidIndex;
        public float TimeScale => range.timeScales.Length > 0 ? range.timeScales[_timeIndex] : 1f;
        public bool RaidRunning => combat.Raid != null && !combat.Raid.Resolved;
        /// <summary>The last raid's debrief, once it has resolved.</summary>
        public RaidSummary LastSummary { get; private set; }
        /// <summary>A name for what the camera is watching.</summary>
        public string CameraTargetName { get; private set; } = "own ship";

        public event Action<Seat> SeatChanged;
        /// <summary>Raised when a raid resolves, with its debrief.</summary>
        public event Action<RaidSummary> Debriefed;
        /// <summary>Raised when a raid starts (the screen closes a debrief still open).</summary>
        public event Action RaidStarted;

        public void SelectRaid(int index) => _raidIndex = Mathf.Clamp(index, 0, Mathf.Max(0, range.raids.Length - 1));

        /// <summary>Start the chosen raid. After a raid, a fresh run first. Ignored while one is running.</summary>
        public bool StartRaid()
        {
            if (RaidRunning || SelectedRaid == null) return false;
            if (combat.Raid != null)
            {
                combat.NewRun();
                proxies.Clear();
                RegisterRoute();
                LastSummary = null;
                Tao = new TaoStation(combat);
            }
            combat.StartRaid(SelectedRaid);
            Note($"general quarters: raid '{SelectedRaid.displayName}' started");
            RaidStarted?.Invoke();
            return true;
        }

        public void SwitchSeat() => SetSeat(PlayerSeat == Seat.Captain ? Seat.Tao : Seat.Captain);

        public void SetSeat(Seat seat)
        {
            PlayerSeat = seat;
            combat.AiSeat = seat == Seat.Captain ? Seat.Tao : Seat.Captain;
            conn.HasTheConn = seat == Seat.Captain;
            if (viewCamera != null) viewCamera.rect = seat == Seat.Tao ? range.taoCameraViewport : new Rect(0f, 0f, 1f, 1f);
            Note(seat == Seat.Captain ? "the player has the conn; the AI captain is TAO" : "the player is TAO; the AI captain has the conn");
            SeatChanged?.Invoke(seat);
        }

        public void CycleTimeCompression()
        {
            if (range.timeScales.Length == 0) return;
            _timeIndex = (_timeIndex + 1) % range.timeScales.Length;
            Time.timeScale = TimeScale;
        }

        /// <summary>Ship → selected track → missiles in flight (newest first) → ship.</summary>
        public void CycleCamera()
        {
            if (chaseCamera == null) return;
            _cameraTargets.Clear();
            _cameraTargets.Add(null);   // the ship
            if (Tao.TryGetSelected(out Core.Cic.Track t) && proxies.TryGetThreat(t.TruthId, out Transform threat)) _cameraTargets.Add(threat);
            IReadOnlyList<Transform> missiles = proxies.MissilesNewestFirst;
            for (int i = 0; i < missiles.Count && i < 4; i++) _cameraTargets.Add(missiles[i]);

            int at = chaseCamera.OnShip ? 0 : _cameraTargets.IndexOf(chaseCamera.Target);
            Transform next = _cameraTargets[(at + 1) % _cameraTargets.Count];
            _lingerUntil = -1f;
            if (next == null) { chaseCamera.WatchShip(); CameraTargetName = "own ship"; }
            else { chaseCamera.Watch(next, 45f); CameraTargetName = next.name; }
        }

        void Awake()
        {
            if (range == null || combat == null || conn == null || input == null || proxies == null)
            {
                Debug.LogError("[RangeDirector] needs the range profile, the ship's combat system, the conn, the range input and the proxies.", this);
                enabled = false;
                return;
            }
            Tao = new TaoStation(combat);
        }

        void Start()
        {
            RegisterRoute();
            combat.RaidResolved += OnRaidResolved;
            SetSeat(startSeat);
        }

        void OnDestroy()
        {
            if (combat != null) combat.RaidResolved -= OnRaidResolved;
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (input.SwitchSeat) SwitchSeat();
            if (input.StartRaid) StartRaid();
            if (input.TimeCompression) CycleTimeCompression();
            if (input.CameraTarget) CycleCamera();

            Tao.Refresh();
            if (PlayerSeat == Seat.Tao) ApplyTaoInput(input.Tao());
            KeepCameraHonest();
        }

        void ApplyTaoInput(in TaoInputFrame f)
        {
            if (f.PrevTrack) Tao.Cycle(-1);
            if (f.NextTrack) Tao.Cycle(+1);
            if (f.EngageEssm) Tao.Engage(WeaponClass.Essm);
            if (f.EngageMk45) Tao.Engage(WeaponClass.Mk45);
            if (f.EngageCiws) Tao.Engage(WeaponClass.Ciws);
            if (f.CeaseFire) Tao.CeaseFire();
            if (f.CeaseFireAll) Tao.CeaseFireAll();
            if (f.CycleRoe && combat.Combat != null) Tao.SetRoe(TaoStation.Next(combat.Combat.Doctrine.Roe), OrderIssuer.Tao);
            if (f.ZoomIn) ScopeZoomRequested?.Invoke(-1);
            if (f.ZoomOut) ScopeZoomRequested?.Invoke(+1);
        }

        /// <summary>The scope's zoom keys (−1 in, +1 out); the screen owns the scope's scale.</summary>
        public event Action<int> ScopeZoomRequested;

        /// <summary>When what the camera watches is destroyed, hold on the spot a moment, then go back to the ship.</summary>
        void KeepCameraHonest()
        {
            if (chaseCamera == null || chaseCamera.OnShip) return;
            Transform t = chaseCamera.Target;
            if (t != null && t.gameObject.activeInHierarchy) return;
            if (_lingerUntil < 0f) { _lingerUntil = Time.unscaledTime + range.cameraLingerS; CameraTargetName += " (destroyed)"; return; }
            if (Time.unscaledTime < _lingerUntil) return;
            _lingerUntil = -1f;
            chaseCamera.WatchShip();
            CameraTargetName = "own ship";
        }

        void RegisterRoute()
        {
            AiCaptainProfileData ai = combat.AiProfile;
            combat.Drive.Helm.AddRoute(ai.RouteId, new Route(range.RouteWaypoints()));
        }

        void OnRaidResolved(ShipCombat host)
        {
            LastSummary = RaidSummary.Of(host.RaidScenario.displayName, host.Combat, host.Raid, host.Kills, host.Loadout, host.RaidStartTick);
            Note($"raid resolved: {LastSummary.Killed} killed, {LastSummary.Leaked} leaked of {LastSummary.Threats}");
            Debug.Log($"[GQ] Raid '{LastSummary.RaidName}' resolved: {LastSummary.Killed} killed, {LastSummary.Leaked} leaked of {LastSummary.Threats}; " +
                      $"core {host.Cost.CoreMeanMs:0.000} ms mean, {host.Cost.CorePercentileMs(0.99f):0.000} ms p99, {host.Cost.CoreMaxMs:0.000} ms max " +
                      $"over {host.Cost.Steps} steps (≤ {host.Cost.MaxThreats} threats); Fix {LastSummary.Fix:X16}.", this);
            Debriefed?.Invoke(LastSummary);
        }

        void Note(string text)
        {
            if (combat.Combat == null) return;
            combat.Combat.Log.Add(combat.Combat.Tick, EventKind.Note, "Range", text, root: RootKind.Scenario);
        }
    }
}
