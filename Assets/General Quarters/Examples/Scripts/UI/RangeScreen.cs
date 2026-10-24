using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using DeepWater.GeneralQuarters.Core;
using DeepWater.GeneralQuarters.Core.Captain;
using DeepWater.GeneralQuarters.Core.Cic;
using DeepWater.GeneralQuarters.Core.Helm;
using DeepWater.GeneralQuarters.Core.Log;
using DeepWater.GeneralQuarters.Core.Magazine;
using DeepWater.GeneralQuarters.Core.Orders;
using DeepWater.GeneralQuarters.Core.Scenario;
using DeepWater.GeneralQuarters.Modules.Combat;
using DeepWater.GeneralQuarters.Modules.Propulsion;
using AppButton = Unity.AppUI.UI.Button;
using ActionButton = Unity.AppUI.UI.ActionButton;
using AppText = Unity.AppUI.UI.Text;
using Heading = Unity.AppUI.UI.Heading;
using Modal = Unity.AppUI.UI.Modal;
using AppSize = Unity.AppUI.UI.Size;
using TextSize = Unity.AppUI.UI.TextSize;
using ButtonVariant = Unity.AppUI.UI.ButtonVariant;

namespace DeepWater.GeneralQuarters.Examples
{
    /// <summary>
    /// The range screen (App UI, G25): fills the rows the UXML leaves empty, keeps every readout current a few times a
    /// second, and turns clicks into the same calls the keys and pads make (the conn, the TAO station, the director).
    /// The layer for the player's seat is shown; the other is hidden (G26). Nothing here changes the ship except
    /// through Orders.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class RangeScreen : MonoBehaviour
    {
        const float KnToMs = 0.514444f;
        static readonly EngineBell[] Ahead = { EngineBell.AheadOneThird, EngineBell.AheadTwoThirds, EngineBell.AheadStandard, EngineBell.AheadFull, EngineBell.AheadFlank };
        static readonly string[] AheadLabels = { "⅓", "⅔", "Std", "Full", "Flank" };
        static readonly EngineBell[] Astern = { EngineBell.BackOneThird, EngineBell.BackTwoThirds, EngineBell.BackFull, EngineBell.BackEmergency };
        static readonly string[] AsternLabels = { "⅓", "⅔", "Full", "Emerg" };
        static readonly float[] Rudders = { -35f, -20f, -15f, -10f, 0f, 10f, 15f, 20f, 35f };
        static readonly WeaponClass[] DoctrineWeapons = { WeaponClass.Essm, WeaponClass.Mk45, WeaponClass.Ciws, WeaponClass.Mk38 };
        static readonly DoctrineMode[] Modes = { DoctrineMode.Auto, DoctrineMode.SemiAuto, DoctrineMode.Manual };
        static readonly string[] ModeLabels = { "Auto", "Semi", "Manual" };
        static readonly Roe[] Roes = { Roe.WeaponsFree, Roe.WeaponsTight, Roe.WeaponsHold };
        static readonly string[] RoeLabels = { "Free", "Tight", "Hold" };

        [SerializeField] RangeDirector director;
        [Tooltip("Readout refreshes per second (real time).")]
        [SerializeField] float refreshHz = 15f;

        VisualElement _root, _panel, _captainLayer, _taoLayer, _help, _trackList, _hullFill;
        AppText _seatLabel, _raidStatus, _clock, _cameraLabel, _cameraWindowLabel, _helmOrders, _tickerCaptain, _tickerTao;
        AppText _selected, _proposal, _magazine, _systems, _hull, _helpText, _courseValue, _speedValue;
        readonly AppText[] _readouts = new AppText[5];
        AppButton _raidButton, _timeButton, _seatButton, _helpButton;
        AppButton _engageEssm, _engageMk45, _engageCiws;
        readonly List<ActionButton> _raidButtons = new List<ActionButton>();
        readonly ActionButton[] _roeButtons = new ActionButton[3], _aheadButtons = new ActionButton[5], _asternButtons = new ActionButton[4], _rudderButtons = new ActionButton[9];
        ActionButton _stopButton;
        readonly Dictionary<WeaponClass, ActionButton[]> _doctrineButtons = new Dictionary<WeaponClass, ActionButton[]>();
        TrackScope _scopeMain, _scopeInset;
        readonly List<TrackRow> _rows = new List<TrackRow>();
        readonly List<TrackScope.Blip> _blips = new List<TrackScope.Blip>();
        readonly List<TrackScope.MissileBlip> _missileBlips = new List<TrackScope.MissileBlip>();
        readonly List<(Track track, ThreatGeometry g)> _sorted = new List<(Track, ThreatGeometry)>();
        readonly StringBuilder _sb = new StringBuilder(1024);
        Modal _debrief;
        float _nextRefresh, _courseDraft = -1f, _speedDraft = 20f;
        bool _helpShown;

        sealed class TrackRow
        {
            public VisualElement Root;
            public AppText Id, Kind, Bearing, Range, Cpa, Weapon;
            public int TrackId = -1;
        }

        // Start, not OnEnable: the UIDocument builds its tree in its own OnEnable, and the order of OnEnables between
        // components is not fixed. By Start every Awake and OnEnable in the scene has run.
        void Start()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            if (director == null || _root == null) { Debug.LogError("[RangeScreen] needs the range director and a UIDocument.", this); enabled = false; return; }
            Bind();
            director.SeatChanged += OnSeatChanged;
            director.Debriefed += ShowDebrief;
            director.RaidStarted += CloseDebrief;
            director.ScopeZoomRequested += ZoomScopes;
            OnSeatChanged(director.PlayerSeat);
        }

        void OnDestroy()
        {
            if (director == null) return;
            director.SeatChanged -= OnSeatChanged;
            director.Debriefed -= ShowDebrief;
            director.RaidStarted -= CloseDebrief;
            director.ScopeZoomRequested -= ZoomScopes;
        }

        void CloseDebrief()
        {
            _debrief?.Dismiss();
            _debrief = null;
        }

        // ── Building ─────────────────────────────────────────────────────

        T Q<T>(string name) where T : VisualElement
        {
            var e = _root.Q<T>(name);
            if (e == null) throw new System.InvalidOperationException($"RangeScreen.uxml has no {typeof(T).Name} named '{name}'.");
            return e;
        }

        void Bind()
        {
            _panel = Q<Unity.AppUI.UI.Panel>("root");
            _captainLayer = Q<VisualElement>("captain-layer");
            _taoLayer = Q<VisualElement>("tao-layer");
            _help = Q<VisualElement>("help");
            _trackList = Q<ScrollView>("track-list");
            _hullFill = Q<VisualElement>("hull-fill");
            _seatLabel = Q<AppText>("seat-label");
            _raidStatus = Q<AppText>("raid-status");
            _clock = Q<AppText>("clock");
            _cameraLabel = Q<AppText>("camera-label");
            _cameraWindowLabel = Q<AppText>("camera-window-label");
            _helmOrders = Q<AppText>("helm-orders");
            _tickerCaptain = Q<AppText>("ticker-captain-text");
            _tickerTao = Q<AppText>("ticker-tao-text");
            _selected = Q<AppText>("selected-text");
            _proposal = Q<AppText>("proposal-text");
            _magazine = Q<AppText>("magazine-text");
            _systems = Q<AppText>("systems-text");
            _hull = Q<AppText>("hull-text");
            _helpText = Q<AppText>("help-text");
            _helpText.text = HelpText;

            _seatButton = Q<AppButton>("seat-button");
            _seatButton.clickable.clicked += () => director.SwitchSeat();
            _raidButton = Q<AppButton>("raid-button");
            _raidButton.clickable.clicked += () => director.StartRaid();
            _timeButton = Q<AppButton>("time-button");
            _timeButton.clickable.clicked += () => director.CycleTimeCompression();
            _helpButton = Q<AppButton>("help-button");
            _helpButton.clickable.clicked += ToggleHelp;
            Q<AppButton>("steady-button").clickable.clicked += () => director.Conn.Steady();

            // Raids on offer.
            VisualElement picker = Q<VisualElement>("raid-picker");
            for (int i = 0; i < director.Range.raids.Length; i++)
            {
                int index = i;
                var b = new ActionButton(() => director.SelectRaid(index)) { label = director.Range.raids[i].displayName, size = AppSize.S };
                picker.Add(b);
                _raidButtons.Add(b);
            }

            // ROE: the Captain's and the TAO's to set.
            VisualElement roeRow = Q<VisualElement>("roe-row");
            for (int i = 0; i < Roes.Length; i++)
            {
                Roe roe = Roes[i];
                _roeButtons[i] = new ActionButton(() => director.Tao.SetRoe(roe, director.PlayerSeat == Seat.Tao ? OrderIssuer.Tao : OrderIssuer.Captain))
                    { label = RoeLabels[i], size = AppSize.S };
                roeRow.Add(_roeButtons[i]);
            }

            // Helm panel.
            VisualElement readouts = Q<VisualElement>("helm-readouts");
            for (int i = 0; i < _readouts.Length; i++) { _readouts[i] = new AppText("") { size = i == 4 ? TextSize.XS : TextSize.S }; readouts.Add(_readouts[i]); }
            VisualElement ahead = Q<VisualElement>("telegraph-ahead");
            ahead.Add(RowLabel("Ahead"));
            for (int i = 0; i < Ahead.Length; i++)
            {
                EngineBell bell = Ahead[i];
                _aheadButtons[i] = new ActionButton(() => director.Conn.RingBell(bell)) { label = AheadLabels[i], size = AppSize.S };
                ahead.Add(_aheadButtons[i]);
            }
            VisualElement back = Q<VisualElement>("telegraph-back");
            back.Add(RowLabel("Back"));
            for (int i = 0; i < Astern.Length; i++)
            {
                EngineBell bell = Astern[i];
                _asternButtons[i] = new ActionButton(() => director.Conn.RingBell(bell)) { label = AsternLabels[i], size = AppSize.S };
                back.Add(_asternButtons[i]);
            }
            _stopButton = new ActionButton(() => director.Conn.RingBell(EngineBell.Stop)) { label = "STOP", size = AppSize.S };
            back.Add(_stopButton);
            VisualElement rudder = Q<VisualElement>("rudder-row");
            for (int i = 0; i < Rudders.Length; i++)
            {
                float r = Rudders[i];
                string label = r == 0f ? "Mid" : $"{(r < 0f ? "L" : "R")}{Mathf.Abs(r):0}";
                _rudderButtons[i] = new ActionButton(() => director.Conn.Rudder(r)) { label = label, size = AppSize.S };
                rudder.Add(_rudderButtons[i]);
            }
            _courseValue = DraftRow(Q<VisualElement>("course-row"), "Course", 10f, 1f, d => _courseDraft = CompassMath.Normalize(_courseDraft + d), "Steer it",
                () => director.Conn.SteerCourse(_courseDraft));
            _speedValue = DraftRow(Q<VisualElement>("speed-row"), "Speed", 5f, 1f, d => _speedDraft = Mathf.Clamp(_speedDraft + d, 0f, 31f), "Hold speed",
                () => director.Conn.HoldSpeed(_speedDraft));

            // Scopes.
            _scopeMain = new TrackScope(false);
            _scopeMain.TrackClicked += id => director.Tao.Select(id);
            Q<VisualElement>("scope-main").Add(_scopeMain);
            _scopeInset = new TrackScope(true);
            _scopeInset.TrackClicked += id => { director.Tao.Select(id); director.SetSeat(Seat.Tao); };
            Q<VisualElement>("scope-inset").Add(_scopeInset);

            // TAO: engage, cease fire, doctrine.
            VisualElement engage = Q<VisualElement>("engage-row");
            _engageEssm = Btn(engage, "ESSM  (1)", () => director.Tao.Engage(WeaponClass.Essm));
            _engageMk45 = Btn(engage, "Mk 45  (2)", () => director.Tao.Engage(WeaponClass.Mk45));
            _engageCiws = Btn(engage, "CIWS  (3)", () => director.Tao.Engage(WeaponClass.Ciws));
            VisualElement cease = Q<VisualElement>("cease-row");
            Btn(cease, "Cease fire  (Bksp)", () => director.Tao.CeaseFire());
            Btn(cease, "Cease all  (Del)", () => director.Tao.CeaseFireAll());
            VisualElement doctrine = Q<VisualElement>("doctrine-rows");
            foreach (WeaponClass w in DoctrineWeapons)
            {
                var row = new VisualElement();
                row.AddToClassList("gq-doctrine-row");
                var name = new AppText(WeaponName(w)) { size = TextSize.S };
                name.AddToClassList("gq-doctrine-label");
                row.Add(name);
                var buttons = new ActionButton[Modes.Length];
                for (int m = 0; m < Modes.Length; m++)
                {
                    DoctrineMode mode = Modes[m];
                    WeaponClass weapon = w;
                    buttons[m] = new ActionButton(() => director.Tao.SetDoctrine(weapon, mode)) { label = ModeLabels[m], size = AppSize.S };
                    row.Add(buttons[m]);
                }
                _doctrineButtons[w] = buttons;
                doctrine.Add(row);
            }
        }

        static AppText RowLabel(string text)
        {
            var t = new AppText(text) { size = TextSize.S };
            t.AddToClassList("gq-row-label");
            return t;
        }

        static AppButton Btn(VisualElement parent, string title, System.Action click)
        {
            var b = new AppButton(click) { title = title, size = AppSize.S };
            parent.Add(b);
            return b;
        }

        AppText DraftRow(VisualElement row, string title, float big, float small, System.Action<float> step, string apply, System.Action order)
        {
            row.Add(RowLabel(title));
            Btn(row, $"−{big:0}", () => step(-big));
            Btn(row, $"−{small:0}", () => step(-small));
            var value = new AppText("") { size = TextSize.S };
            value.AddToClassList("gq-draft-value");
            row.Add(value);
            Btn(row, $"+{small:0}", () => step(small));
            Btn(row, $"+{big:0}", () => step(big));
            Btn(row, apply, order);
            return value;
        }

        // ── Seats, help, zoom ─────────────────────────────────────────────

        void OnSeatChanged(Seat seat)
        {
            bool tao = seat == Seat.Tao;
            _captainLayer.style.display = tao ? DisplayStyle.None : DisplayStyle.Flex;
            _taoLayer.style.display = tao ? DisplayStyle.Flex : DisplayStyle.None;
            _seatLabel.text = tao ? "TAO  ·  AI captain has the conn" : "CAPTAIN  ·  AI captain is TAO";
            _nextRefresh = 0f;
        }

        void ToggleHelp()
        {
            _helpShown = !_helpShown;
            _help.EnableInClassList("gq-help--shown", _helpShown);
        }

        void ZoomScopes(int step) => _scopeMain.Zoom(step);

        // ── Refresh ─────────────────────────────────────────────────────

        void Update()
        {
            if (_scopeMain == null) return;   // not bound yet (Start runs before the first Update, unless it failed)
            ConnController conn = director.Conn;
            if (conn.Controls != null && conn.Controls.Ready && conn.Controls.TogglePanelPressed) ToggleHelp();
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 1f / Mathf.Max(1f, refreshHz);
            ShipCombat combat = director.Combat;
            CombatSystem c = combat.Combat;
            if (c == null) return;

            RefreshTopBar(combat, c);
            RefreshScopes(combat, c);
            if (director.PlayerSeat == Seat.Captain) RefreshHelm(combat, c);
            else RefreshTao(combat, c);
            string ticker = Ticker(c, director.PlayerSeat == Seat.Captain ? 6 : 4);
            if (director.PlayerSeat == Seat.Captain) _tickerCaptain.text = ticker; else _tickerTao.text = ticker;
        }

        void RefreshTopBar(ShipCombat combat, CombatSystem c)
        {
            for (int i = 0; i < _raidButtons.Count; i++)
            {
                _raidButtons[i].selected = i == director.SelectedRaidIndex;
                _raidButtons[i].SetEnabled(!director.RaidRunning);
            }
            ScriptedRaid raid = combat.Raid;
            _raidButton.SetEnabled(!director.RaidRunning);
            _raidButton.title = director.RaidRunning ? "Raid in progress" : director.LastSummary != null ? "Run again  (G)" : "General quarters  (G)";
            _raidStatus.text = raid == null
                ? $"{director.SelectedRaid?.displayName}: {director.SelectedRaid?.threats.Length} threats, ready"
                : raid.Resolved
                    ? $"resolved: {raid.Killed} killed, {raid.Leaked} leaked of {raid.Threats}"
                    : $"{raid.Launched}/{raid.Threats} launched · {raid.Active} inbound · {raid.Killed} killed · {raid.Leaked} leaked";
            for (int i = 0; i < Roes.Length; i++) _roeButtons[i].selected = c.Doctrine.Roe == Roes[i];
            _timeButton.title = $"{director.TimeScale:0}×  (T)";
            float t = c.Tick * c.TickSeconds;
            _clock.text = raid == null ? $"run {Clock(t)}" : $"run {Clock(t)} · raid +{Clock((c.Tick - combat.RaidStartTick) * c.TickSeconds)}";
            _cameraLabel.text = $"Camera: {director.CameraTargetName}  (C)";
            _cameraWindowLabel.text = $"3D: {director.CameraTargetName}  (C to cycle)";
        }

        void RefreshScopes(ShipCombat combat, CombatSystem c)
        {
            Core.World.OwnShipState own = combat.Own;
            _blips.Clear();
            IReadOnlyList<Track> tracks = c.Tracks.Tracks;
            IReadOnlyList<Assignment> proposals = c.Tewa.Proposals;
            int selected = director.Tao.SelectedTrackId;
            for (int i = 0; i < tracks.Count; i++)
            {
                Track t = tracks[i];
                if (!TaoLogic.Selectable(t)) continue;
                Vector3 d = t.PredictedPosition(c.Tick, c.TickSeconds) - own.Position;
                _blips.Add(new TrackScope.Blip
                {
                    Id = t.Id, Kind = t.Kind, Identity = t.Identity, Offset = new Vector2(d.x, d.z), Velocity = new Vector2(t.Velocity.x, t.Velocity.z),
                    Selected = t.Id == selected, Proposed = IsProposed(proposals, t.Id),
                });
            }
            _missileBlips.Clear();
            IReadOnlyList<MissileInFlight> inFlight = c.Vls.InFlight;
            for (int i = 0; i < inFlight.Count; i++)
            {
                MissileInFlight m = inFlight[i];
                if (m.Done) continue;
                Vector3 d = m.Entity.State.Position - own.Position;
                var blip = new TrackScope.MissileBlip { Offset = new Vector2(d.x, d.z), Velocity = new Vector2(m.Entity.State.Velocity.x, m.Entity.State.Velocity.z) };
                if (c.Tracks.TryGet(m.TrackId, out Track target))
                {
                    Vector3 td = target.PredictedPosition(c.Tick, c.TickSeconds) - own.Position;
                    blip.TargetOffset = new Vector2(td.x, td.z);
                    blip.HasTarget = true;
                }
                _missileBlips.Add(blip);
            }
            float essm = Reach(combat.Loadout, WeaponClass.Essm), ciws = Reach(combat.Loadout, WeaponClass.Ciws);
            float speed = combat.Drive.Motion.SurgeMs;
            TrackScope scope = director.PlayerSeat == Seat.Tao ? _scopeMain : _scopeInset;
            scope.SetPicture(own.HeadingDeg, speed, essm, ciws, _blips, _missileBlips);
        }

        void RefreshHelm(ShipCombat combat, CombatSystem c)
        {
            ShipDrive ship = combat.Drive;
            if (ship.EngineRoom == null) return;
            ShipMotion m = ship.Motion;
            HelmCommand cmd = c.Helm;
            HelmOutput h = ship.LastHelm;
            if (_courseDraft < 0f) _courseDraft = Mathf.Round(m.HeadingDeg) % 360f;
            float heel = Mathf.Asin(Mathf.Clamp((ship.transform.rotation * Vector3.right).y, -1f, 1f)) * Mathf.Rad2Deg;
            string steering = cmd.Course == CourseMode.Manual ? $"rudder by hand, {Side(cmd.RudderDeg)}"
                            : cmd.Course == CourseMode.Route ? $"route {cmd.RouteId}, steering {h.SteeringCourseDeg:000}"
                            : $"steering {cmd.CourseDeg:000}";
            string engines = cmd.Speed == SpeedMode.Bell ? ConnController.BellName(cmd.Bell) : $"holding {cmd.SpeedKn:0} kn";
            _readouts[0].text = $"Heading {CompassMath.Normalize(Mathf.Round(m.HeadingDeg * 10f) / 10f):000.0}°    {steering}";
            _readouts[1].text = $"Speed {m.SurgeMs / KnToMs:0.0} kn    {engines}";
            _readouts[2].text = $"Shafts {ship.EngineRoom.PortShaftRps * 60f:0} / {ship.EngineRoom.StarboardShaftRps * 60f:0} rpm    Rudders {Side(ship.EngineRoom.PortRudderDeg)}";
            _readouts[3].text = $"Turning {m.YawRateRadS * Mathf.Rad2Deg * 60f:+0;-0;0}°/min    Heel {Mathf.Abs(heel):0.0}° {(heel > 0.05f ? "to port" : heel < -0.05f ? "to starboard" : "")}";
            _readouts[4].text = $"Last control: {director.Conn.Controls.LastDevice}";
            for (int i = 0; i < Ahead.Length; i++) _aheadButtons[i].selected = IsBell(cmd, Ahead[i]);
            for (int i = 0; i < Astern.Length; i++) _asternButtons[i].selected = IsBell(cmd, Astern[i]);
            _stopButton.selected = IsBell(cmd, EngineBell.Stop);
            for (int i = 0; i < Rudders.Length; i++) _rudderButtons[i].selected = cmd.Course == CourseMode.Manual && Mathf.Abs(cmd.RudderDeg - Rudders[i]) < 0.5f;
            _courseValue.text = $"{Mathf.RoundToInt(_courseDraft) % 360:000}";
            _speedValue.text = $"{_speedDraft:0} kn";
            _sb.Clear();
            IReadOnlyList<string> orders = director.Conn.RecentOrders;
            for (int i = Mathf.Max(0, orders.Count - 6); i < orders.Count; i++) _sb.AppendLine(orders[i]);
            _helmOrders.text = _sb.ToString();
        }

        void RefreshTao(ShipCombat combat, CombatSystem c)
        {
            Core.World.OwnShipState own = combat.Own;
            IReadOnlyList<Track> tracks = c.Tracks.Tracks;
            IReadOnlyList<Assignment> proposals = c.Tewa.Proposals;

            // Track list, most urgent first.
            _sorted.Clear();
            for (int i = 0; i < tracks.Count; i++)
                if (TaoLogic.Selectable(tracks[i])) _sorted.Add((tracks[i], ThreatGeometry.Of(tracks[i], own, c.Tick, c.TickSeconds)));
            _sorted.Sort((a, b) =>
            {
                float ta = a.g.Inbound ? a.g.TimeToCpaS : float.MaxValue, tb = b.g.Inbound ? b.g.TimeToCpaS : float.MaxValue;
                int k = ta.CompareTo(tb);
                return k != 0 ? k : a.track.Id.CompareTo(b.track.Id);
            });
            while (_rows.Count < _sorted.Count) _rows.Add(NewRow());
            for (int i = 0; i < _rows.Count; i++)
            {
                TrackRow row = _rows[i];
                if (i >= _sorted.Count) { row.Root.style.display = DisplayStyle.None; row.TrackId = -1; continue; }
                (Track t, ThreatGeometry g) = _sorted[i];
                row.TrackId = t.Id;
                row.Root.style.display = DisplayStyle.Flex;
                row.Root.EnableInClassList("gq-track-row--selected", t.Id == director.Tao.SelectedTrackId);
                row.Id.text = t.Id.ToString();
                row.Kind.text = $"{IdentityShort(t.Identity)} {KindShort(t.Kind)}";
                row.Kind.EnableInClassList("gq-hostile", t.Identity == Identity.Hostile);
                row.Kind.EnableInClassList("gq-friend", t.Identity == Identity.Friend);
                row.Bearing.text = $"{Bearing(own, t.Position):000}°";
                row.Range.text = $"{g.RangeM / 1000f:0.0} km";
                row.Cpa.text = g.Inbound ? $"{g.TimeToCpaS:0} s" : "open";
                int essm = c.Vls.InFlightAt(t.Id, WeaponClass.Essm);
                row.Weapon.text = essm > 0 ? $"ESSM×{essm}" : IsProposed(proposals, t.Id) ? "proposed" : "";
            }

            // Selected track and what TEWA proposes for it.
            if (director.Tao.TryGetSelected(out Track sel))
            {
                ThreatGeometry g = ThreatGeometry.Of(sel, own, c.Tick, c.TickSeconds);
                float speed = sel.Velocity.magnitude;
                _selected.text = $"Track {sel.Id}: {sel.Identity} {KindName(sel.Kind)} · bearing {Bearing(own, sel.Position):000}° · {g.RangeM / 1000f:0.0} km · " +
                                 $"alt {sel.Position.y:0} m · {speed:0} m/s ({speed / KnToMs:0} kn) · " +
                                 (g.Inbound ? $"CPA in {g.TimeToCpaS:0} s at {g.CpaDistanceM:0} m" : "opening");
                string proposed = null;
                for (int i = 0; i < proposals.Count; i++) if (proposals[i].TrackId == sel.Id) { proposed = WeaponName(proposals[i].Weapon); break; }
                _proposal.text = proposed != null ? $"TEWA proposes {proposed}: confirm with its engage button" : "";
                _engageEssm.variant = proposed == "ESSM" ? ButtonVariant.Accent : ButtonVariant.Default;
            }
            else
            {
                _selected.text = "No track selected  (Q / E, LB / RB, or click the scope)";
                _proposal.text = "";
                _engageEssm.variant = ButtonVariant.Default;
            }
            _engageEssm.SetEnabled(c.Vls.CanEngage(WeaponClass.Essm));
            _engageMk45.SetEnabled(MountUp(c, WeaponClass.Mk45));
            _engageCiws.SetEnabled(MountUp(c, WeaponClass.Ciws));

            foreach (KeyValuePair<WeaponClass, ActionButton[]> kv in _doctrineButtons)
                for (int m = 0; m < Modes.Length; m++) kv.Value[m].selected = c.Doctrine.Mode(kv.Key) == Modes[m];

            // Magazine, systems, hull.
            _sb.Clear();
            int essmLeft = c.Vls.Rounds(WeaponClass.Essm), essmFlying = 0;
            for (int i = 0; i < c.Vls.InFlight.Count; i++) if (!c.Vls.InFlight[i].Done) essmFlying++;
            _sb.Append($"ESSM {essmLeft} left · {essmFlying} in flight\n");
            for (int i = 0; i < c.Guns.Count; i++)
            {
                IGunMount g = c.Guns[i];
                _sb.Append($"{g.Profile.Name} {g.Rounds}/{g.Profile.MagazineRounds}{(g.Online ? "" : " OFFLINE")}");
                _sb.Append(i % 2 == 1 ? "\n" : " · ");
            }
            _magazine.text = _sb.ToString();
            _sb.Clear();
            DamageProfileSystems(combat.Loadout, c);
            _systems.text = _sb.ToString();
            float hp = c.DamageControl.HullHitPoints, max = Mathf.Max(1f, combat.Loadout.Damage.HullHitPoints);
            _hull.text = $"Hull {hp:0} / {max:0}{(c.DamageControl.OutOfAction ? "  ·  OUT OF ACTION" : "")}";
            _hullFill.style.width = Length.Percent(100f * Mathf.Clamp01(hp / max));
            _hullFill.style.backgroundColor = hp / max > 0.5f ? new Color(0.43f, 0.82f, 0.55f) : hp / max > 0.25f ? new Color(1f, 0.72f, 0.25f) : new Color(1f, 0.32f, 0.27f);
        }

        void DamageProfileSystems(CombatLoadout loadout, CombatSystem c)
        {
            Core.DamageControl.SystemProfile[] systems = loadout.Damage.Systems;
            for (int i = 0; i < systems.Length; i++)
            {
                bool up = c.DamageControl.IsOnline(systems[i].System);
                _sb.Append(up ? "● " : "○ ").Append(systems[i].System).Append(up ? "" : " OFFLINE").Append(i % 3 == 2 ? "\n" : "   ");
            }
        }

        TrackRow NewRow()
        {
            var row = new TrackRow { Root = new VisualElement() };
            row.Root.AddToClassList("gq-track-row");
            row.Id = Col(row.Root, "gq-col--id");
            row.Kind = Col(row.Root, "gq-col--kind");
            row.Bearing = Col(row.Root, "gq-col--num");
            row.Range = Col(row.Root, "gq-col--num");
            row.Cpa = Col(row.Root, "gq-col--num");
            row.Weapon = Col(row.Root, "gq-col--wpn");
            row.Root.RegisterCallback<PointerUpEvent>(_ => { if (row.TrackId >= 0) director.Tao.Select(row.TrackId); });
            _trackList.Add(row.Root);
            return row;
        }

        static AppText Col(VisualElement row, string cls)
        {
            var t = new AppText("") { size = TextSize.XS };
            t.AddToClassList("gq-col");
            t.AddToClassList(cls);
            row.Add(t);
            return t;
        }

        // ── Ticker ─────────────────────────────────────────────────────

        string Ticker(CombatSystem c, int lines)
        {
            IReadOnlyList<EngagementEvent> events = c.Log.Events;
            _tickerLines.Clear();
            for (int i = events.Count - 1; i >= 0 && _tickerLines.Count < lines; i--)
            {
                EngagementEvent e = events[i];
                bool notable = e.Kind == EventKind.Launched || e.Kind == EventKind.Kill || e.Kind == EventKind.Miss || e.Kind == EventKind.ShipHit
                               || e.Kind == EventKind.SystemOffline || e.Kind == EventKind.Decision || (e.Kind == EventKind.Note && e.Rule != null && e.Rule.StartsWith("Range"));
                if (!notable) continue;
                string text = e.Text.Length > 150 ? e.Text.Substring(0, 147) + "…" : e.Text;
                _tickerLines.Add($"{Clock(e.Tick * c.TickSeconds)}  {KindWord(e.Kind)}  {text}");
            }
            _sb.Clear();
            for (int i = _tickerLines.Count - 1; i >= 0; i--) _sb.AppendLine(_tickerLines[i]);
            return _sb.ToString();
        }

        readonly List<string> _tickerLines = new List<string>(8);

        static string KindWord(EventKind k) => k switch
        {
            EventKind.Launched => "LAUNCH",
            EventKind.Kill => "KILL",
            EventKind.Miss => "MISS",
            EventKind.ShipHit => "HIT",
            EventKind.SystemOffline => "DAMAGE",
            EventKind.Decision => "AI",
            _ => "RANGE",
        };

        // ── Debrief ────────────────────────────────────────────────────

        void ShowDebrief(RaidSummary s)
        {
            CloseDebrief();
            CombatCost cost = director.Combat.Cost;
            var card = new VisualElement();
            card.AddToClassList("gq-card");
            card.AddToClassList("gq-debrief");
            card.Add(new Heading($"DEBRIEF · {s.RaidName}") { size = Unity.AppUI.UI.HeadingSize.S });
            Line(card, $"Threats {s.Threats}: {s.Killed} killed, {s.Leaked} leaked ({s.Leaked} hit the ship).");
            Line(card, $"Kills by weapon: ESSM {s.KillsBy[(int)WeaponClass.Essm]} · Mk 45 {s.KillsBy[(int)WeaponClass.Mk45]} · CIWS {s.KillsBy[(int)WeaponClass.Ciws]} · Mk 38 {s.KillsBy[(int)WeaponClass.Mk38]}.");
            _sb.Clear();
            _sb.Append($"Spent: {s.MissilesLaunched} missiles ({s.MissilesLeft} left), {s.Bursts} gun bursts");
            foreach (MountExpenditure m in s.Mounts) if (m.RoundsFired > 0) _sb.Append($" · {m.Name} {m.RoundsFired} rounds");
            Line(card, _sb.Append('.').ToString());
            Line(card, $"Damage: hull {s.HullHitPoints:0} / {s.HullHitPointsMax:0}{(s.OutOfAction ? ", OUT OF ACTION" : "")}" +
                       (s.Offline.Count > 0 ? $"; offline: {string.Join(", ", s.Offline)}." : "; every system online."));
            Line(card, $"{s.Orders} orders ({s.AiDecisions} AI captain decisions) · {Clock(s.DurationS)} of raid · seed {s.Seed} · Fix {s.Fix:X16}");
            float p99 = cost.CorePercentileMs(0.99f);
            Line(card, $"Combat core per fixed step during the raid (this machine{(Application.isEditor ? ", Editor" : "")}): mean {cost.CoreMeanMs:0.000} ms · " +
                       $"p99 {p99:0.000} ms · max {cost.CoreMaxMs:0.000} ms over {cost.Steps} steps, up to {cost.MaxThreats} threats. Whole host step (AI + raid + core): mean {cost.HostMeanMs:0.000} ms.");
            var gate = new AppText(p99 <= 1f ? "Phase 1 budget (G16, ≤ 1 ms at p99): within." : "Phase 1 budget (G16, ≤ 1 ms at p99): OVER.") { size = TextSize.S };
            gate.AddToClassList("gq-debrief-gate");
            card.Add(gate);
            var buttons = new VisualElement();
            buttons.AddToClassList("gq-row");
            buttons.style.marginTop = 10f;
            Btn(buttons, "Run again  (G)", () => director.StartRaid()).variant = ButtonVariant.Accent;
            Btn(buttons, "Close", CloseDebrief);
            card.Add(buttons);
            _debrief = Modal.Build(_panel, card);
            _debrief.Show();
        }

        static void Line(VisualElement card, string text)
        {
            var t = new AppText(text) { size = TextSize.S };
            t.AddToClassList("gq-debrief-line");
            card.Add(t);
        }

        // ── Helpers ────────────────────────────────────────────────────

        static bool IsBell(in HelmCommand c, EngineBell bell) => c.Speed == SpeedMode.Bell && c.Bell == bell;

        static bool IsProposed(IReadOnlyList<Assignment> proposals, int trackId)
        {
            for (int i = 0; i < proposals.Count; i++) if (proposals[i].TrackId == trackId) return true;
            return false;
        }

        static bool MountUp(CombatSystem c, WeaponClass w)
        {
            for (int i = 0; i < c.Guns.Count; i++) if (c.Guns[i].Profile.Weapon == w && c.Guns[i].Online && c.Guns[i].Rounds > 0) return true;
            return false;
        }

        static float Reach(CombatLoadout l, WeaponClass w)
        {
            if (w == WeaponClass.Essm && l.Tewa.MissileBands != null)
                foreach (WeaponBand b in l.Tewa.MissileBands) if (b.Weapon == w) return b.MaxRangeM;
            foreach (GunMountProfileData g in l.Guns) if (g.Weapon == w) return g.MaxRangeM;
            return 0f;
        }

        static float Bearing(in Core.World.OwnShipState own, Vector3 p)
        {
            Vector3 d = p - own.Position;
            return CompassMath.Normalize(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
        }

        static string Side(float rudderDeg) =>
            Mathf.Abs(rudderDeg) < 0.5f ? "amidships" : $"{(rudderDeg > 0f ? "right" : "left")} {Mathf.Abs(rudderDeg):0}°";

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        static string WeaponName(WeaponClass w) => w switch
        {
            WeaponClass.Essm => "ESSM", WeaponClass.Sm2 => "SM-2", WeaponClass.Mk45 => "Mk 45", WeaponClass.Ciws => "CIWS", WeaponClass.Mk38 => "Mk 38",
            WeaponClass.Harpoon => "Harpoon", WeaponClass.Tomahawk => "Tomahawk", _ => "none",
        };

        static string KindName(TargetKind k) => k switch
        {
            TargetKind.AntiShipMissile => "anti-ship missile", TargetKind.Aircraft => "aircraft", TargetKind.SurfaceShip => "surface ship", _ => "unknown",
        };

        static string KindShort(TargetKind k) => k switch
        {
            TargetKind.AntiShipMissile => "MSL", TargetKind.Aircraft => "AIR", TargetKind.SurfaceShip => "SURF", _ => "UNK",
        };

        static string IdentityShort(Identity i) => i switch
        {
            Identity.Hostile => "HOS", Identity.Friend => "FRD", Identity.Neutral => "NEU", _ => "UNK",
        };

        const string HelpText =
            "RANGE (both seats)\n" +
            "  Keyboard: switch seat Tab · general quarters / run again G · time compression T · camera target C · controls H\n" +
            "  Xbox / gamepad: switch seat tap Start · general quarters hold Start · time right-stick press · camera left-stick press · controls View\n" +
            "  Generic pad: seat tap button 6 · general quarters hold button 6 · time button 7 · camera button 8 · controls button 5\n\n" +
            "CAPTAIN (the conn)\n" +
            "  Keyboard & mouse: rudder A D or ← → (Shift: hard over) · bells W S or ↑ ↓ · course Q E · steady Space · midships X · all stop Z · camera V, right-drag, wheel, R F\n" +
            "  Xbox / gamepad: rudder left stick · bells D-pad ↑ ↓ or RB LB · course D-pad ← → · steady A · midships B · all stop Y · camera X, right stick, RT LT\n" +
            "  Generic pad: rudder stick · bells hat ↑ ↓ · course hat ← → · steady trigger · midships button 2 · all stop 3 · camera 4\n\n" +
            "TAO (the weapons)\n" +
            "  Keyboard & mouse: track Q E or [ ] (or click) · engage ESSM 1 · Mk 45 2 · CIWS 3 · cease fire Backspace · cease all Delete · ROE O · scope zoom − = or wheel\n" +
            "  Xbox / gamepad: track LB RB · engage ESSM A · Mk 45 B · CIWS Y · cease fire tap D-pad ↓ · cease all hold D-pad ↓ · ROE D-pad ↑ · scope zoom D-pad ← →\n" +
            "  Generic pad: track hat ← → · ESSM trigger · Mk 45 button 2 · CIWS button 3 · cease fire tap hat ↓ · cease all hold hat ↓ · ROE hat ↑\n\n" +
            "The seat you leave goes to the AI captain; its decisions show in the log as AI. Rebind in Project Settings → Input System Package (maps Conn, Tao, Range).";
    }
}
