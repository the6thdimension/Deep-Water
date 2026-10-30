using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents.Policies;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Radar;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.AirWing.Modules.Cockpit;
using DeepWater.Aviator.Core;

namespace DeepWater.Aviator.Learning
{
    /// <summary>
    /// Every jet's stats (A23; SIX 2026-10-30: "stats by the plane so I can see when they're using IFF or radar, or how
    /// many missiles they have left, how many bullets"). P shows a table, one row a jet: who flies it, its radar's mode
    /// and lock, IFF questions asked (lit while it asks), rounds, heat and radar missiles, chaff and flares left, fuel,
    /// kills, friends shot down, deaths and its credits on the price ledger. The jet the spectator camera follows also gets
    /// a block of its own, bottom left: the same, its warning receiver, and its account (spent, earned, lost). The training
    /// director adds it when training starts. Presentation only: it reads, it never changes the fight.
    /// </summary>
    public sealed class FightStats : MonoBehaviour
    {
        [SerializeField] bool showTable;
        [SerializeField] Color colour = new Color(0.9f, 0.95f, 1f, 0.95f);
        [SerializeField] Color blue = new Color(0.45f, 0.72f, 1f, 1f), red = new Color(1f, 0.5f, 0.45f, 1f);

        // Columns: header, width in 1080p pixels.
        static readonly (string head, float w)[] Columns =
        {
            ("JET", 60f), ("BRAIN", 150f), ("STATE", 70f), ("RADAR", 120f), ("IFF", 60f), ("RDS", 60f), ("HEAT", 50f), ("RDR", 50f),
            ("CHF", 46f), ("FLR", 46f), ("FUEL KG", 80f), ("K", 34f), ("FK", 34f), ("D", 34f), ("CREDITS", 100f),
        };

        FighterTraining _training;
        SpectatorCamera _camera;
        readonly Dictionary<int, string> _brains = new Dictionary<int, string>();
        readonly string[] _cells = new string[Columns.Length];
        float _brainsAt = -1f;
        GUIStyle _cell, _block;
        Texture2D _shade;

        public bool ShowTable { get => showTable; set => showTable = value; }

        void Start()
        {
            _training = GetComponent<FighterTraining>();
            _camera = FindAnyObjectByType<SpectatorCamera>();
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.pKey.wasPressedThisFrame) showTable = !showTable;
        }

        /// <summary>Credits as the scoreboard writes them: a sign, thousands grouped, "CR".</summary>
        public static string Credits(float c) => (c > 0.5f ? "+" : c < -0.5f ? "−" : "") + Mathf.Abs(c).ToString("#,0") + " CR";

        void OnGUI()
        {
            if (_training == null || _training.Prices == null || _training.Runner == null || _training.Runner.Flight == null) return;
            float s = Mathf.Max(1f, Screen.height / 1080f);
            Styles(s);
            if (Time.unscaledTime > _brainsAt) { _brainsAt = Time.unscaledTime + 1f; ReadBrains(); }   // a label a second, not a lookup a frame
            FlightCore core = _training.Runner.Flight;
            if (showTable) Table(core, s);
            AircraftView followed = _camera != null ? _camera.Target : null;
            if (followed != null && followed.Aircraft != null && !_camera.Cockpit) Detail(core, followed.Aircraft, s);   // in the cockpit its HUD says it
        }

        void Table(FlightCore core, float s)
        {
            IReadOnlyList<Aircraft> all = core.Aircraft;
            float rowH = 22f * s, width = 0f;
            foreach (var c in Columns) width += c.w * s;
            float x0 = 20f * s, y0 = 150f * s;
            GUI.DrawTexture(new Rect(x0 - 8f * s, y0 - 6f * s, width + 16f * s, rowH * (all.Count + 1) + 12f * s), _shade);
            _cell.normal.textColor = colour;
            for (int c = 0; c < Columns.Length; c++) _cells[c] = Columns[c].head;
            Row(x0, y0, s, rowH);
            for (int i = 0; i < all.Count; i++)
            {
                Aircraft a = all[i];
                Side side = _training.Fight.SideEver(a.Id);
                _cell.normal.textColor = side == Side.Blue ? blue : side == Side.Red ? red : colour;
                Fill(core, a);
                Row(x0, y0 + rowH * (i + 1), s, rowH);
            }
            _cell.normal.textColor = colour;
        }

        void Row(float x, float y, float s, float h)
        {
            for (int c = 0; c < Columns.Length; c++)
            {
                GUI.Label(new Rect(x, y, Columns[c].w * s, h), _cells[c], _cell);
                x += Columns[c].w * s;
            }
        }

        /// <summary>One jet's row into the cells.</summary>
        void Fill(FlightCore core, Aircraft a)
        {
            AirframeState st = a.State;
            JetAccount acc = _training.Prices.Of(a.Id);
            JetRadar radar = core.RadarOf(a.Id);
            bool asking = (a.LastInputs.Switches & PilotSwitches.Interrogate) != 0;
            _cells[0] = a.Id.ToString();
            _cells[1] = _brains.TryGetValue(a.Id, out string b) ? b : "—";
            _cells[2] = st.Status == FlightStatus.Flying ? "FLYING" : "DOWN";
            _cells[3] = radar == null ? "—" : Radar(radar);
            _cells[4] = radar == null ? "—" : (asking ? "● " : "") + radar.Interrogations;
            _cells[5] = st.Rounds.ToString();
            _cells[6] = st.Missiles.ToString();
            _cells[7] = st.RadarMissiles.ToString();
            _cells[8] = st.Chaff.ToString();
            _cells[9] = st.Flares.ToString();
            _cells[10] = st.FuelKg.ToString("#,0");
            _cells[11] = acc.Kills.ToString();
            _cells[12] = acc.FriendlyKills.ToString();
            _cells[13] = acc.Deaths.ToString();
            _cells[14] = Credits(acc.Net);
        }

        /// <summary>The radar's mode, and whom it is locked on.</summary>
        static string Radar(JetRadar r)
        {
            switch (r.Mode)
            {
                case RadarMode.Off: return "OFF";
                case RadarMode.RangeWhileSearch: return "RWS";
                case RadarMode.TrackWhileScan: return "TWS";
                case RadarMode.Dogfight: return "DGFT";
                case RadarMode.SingleTarget: return $"STT → {r.LockedAircraftId}";
                default: return r.Mode.ToString().ToUpperInvariant();
            }
        }

        void Detail(FlightCore core, Aircraft a, float s)
        {
            AirframeState st = a.State;
            JetAccount acc = _training.Prices.Of(a.Id);
            JetRadar radar = core.RadarOf(a.Id);
            WarningReceiver rwr = core.WarningReceiverOf(a.Id);
            PilotSwitches sw = a.LastInputs.Switches;
            string warn = "—";
            if (rwr != null && rwr.Contacts.Count > 0)
            {
                WarningContact c = rwr.Contacts[0];
                warn = (c.Kind == PaintKind.Missile ? "MISSILE" : c.Kind == PaintKind.Track ? "TRACKED" : "SEARCHED") + $" {Mathf.Repeat(c.AzimuthDeg, 360f):000}°" +
                       (rwr.Contacts.Count > 1 ? $" +{rwr.Contacts.Count - 1}" : "");
            }
            string using_ = ((sw & PilotSwitches.Interrogate) != 0 ? "IFF " : "") + ((sw & PilotSwitches.Chaff) != 0 ? "CHAFF " : "") + ((sw & PilotSwitches.Flare) != 0 ? "FLARES " : "");
            string text =
                $"JET {a.Id} · {(_brains.TryGetValue(a.Id, out string b) ? b : "—")}\n" +
                $"RADAR {(radar == null ? "NONE" : Radar(radar))}   IFF ASKED {(radar == null ? 0 : radar.Interrogations)}   NOW {(using_.Length == 0 ? "—" : using_.Trim())}\n" +
                $"RWR {warn}\n" +
                $"ROUNDS {st.Rounds}   HEAT {st.Missiles}   RADAR MSL {st.RadarMissiles}   CHAFF {st.Chaff}   FLARES {st.Flares}   FUEL {st.FuelKg:#,0} KG\n" +
                $"KILLS {acc.Kills}   FRIENDS SHOT DOWN {acc.FriendlyKills}   DEATHS {acc.Deaths}\n" +
                $"FIRED {acc.Rounds} RDS · {acc.HeatMissiles} HEAT · {acc.RadarMissiles} RADAR · {acc.Chaff} CHAFF · {acc.Flares} FLR · {acc.FuelKg:#,0} KG FUEL\n" +
                $"SPENT {Credits(-acc.Spent)}   EARNED {Credits(acc.Earned)}   LOST {Credits(-acc.Lost)}   NET {Credits(acc.Net)}";
            var r = new Rect(20f * s, Screen.height - 230f * s, 900f * s, 180f * s);
            GUI.DrawTexture(new Rect(r.x - 8f * s, r.y - 6f * s, r.width, r.height), _shade);
            GUI.Label(r, text, _block);
        }

        /// <summary>Who flies each training jet, in the director's words (TRAINING, LATEST, SAVED name, STAND-IN, ACE…).</summary>
        void ReadBrains()
        {
            _brains.Clear();
            foreach (TrainingSquadron squadron in FindObjectsByType<TrainingSquadron>(FindObjectsInactive.Exclude))
                foreach (AircraftView jet in squadron.Jets)
                    if (jet != null) _brains[jet.AircraftId] = FighterTraining.Who(squadron.BrainOf(jet, out _), jet.GetComponent<BehaviorParameters>());
        }

        void Styles(float s)
        {
            if (_cell != null) return;
            _cell = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14f * s), fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
            _block = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15f * s), fontStyle = FontStyle.Bold };
            _block.normal.textColor = colour;
            _shade = new Texture2D(1, 1);
            _shade.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
            _shade.Apply();
        }

        void OnDestroy()
        {
            if (_shade != null) Destroy(_shade);
        }
    }
}
