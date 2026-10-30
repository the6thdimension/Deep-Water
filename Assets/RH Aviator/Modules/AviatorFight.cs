using System.Linq;
using UnityEngine;
using DeepWater.AirWing.Modules.Aircraft;
using DeepWater.Aviator.Core;

namespace DeepWater.Aviator.Modules
{
    /// <summary>
    /// A fight in a scene (slice 3d, W43 up to eight, W46, W52, W53): it owns the <see cref="Engagement"/>, puts every
    /// <see cref="AviatorPilot"/> whose task is Fight on its side and in the fight, makes red hunt whichever jet the
    /// player flies (it follows the seat cycle), and scores the rear cone after every tick. The player's own jet joins
    /// on <see cref="playerSide"/>. Execution order 0: after Air Wing's runner (−50) has stepped the tick.
    /// <para>It also draws a small score panel (IMGUI, top centre): each side's seconds in the rear cone, and the
    /// player's jet's state, "ON 5'S TAIL" or "5 ON YOUR TAIL". No weapons: position only (BLUEPRINT §6.5).</para>
    /// </summary>
    [DefaultExecutionOrder(0)]
    public sealed class AviatorFight : MonoBehaviour
    {
        [SerializeField] Side playerSide = Side.Blue;
        [SerializeField] bool showScore = true;
        [SerializeField] Color colour = new Color(1f, 0.85f, 0.35f, 0.95f);

        AirWingRunner _runner;
        Engagement _fight;
        GUIStyle _style;

        public Engagement Engagement => _fight;

        void Start()
        {
            _runner = FindAnyObjectByType<AirWingRunner>();
            if (_runner == null || _runner.Flight == null)
            {
                Debug.LogError("[RH Aviator] no Air Wing runner: no fight.", this);
                enabled = false;
                return;
            }
            _fight = new Engagement { Hunted = () => _runner.PlayerAircraftId };
            if (_runner.PlayerAircraftId != 0)
            {
                _fight.Join(_runner.PlayerAircraftId, playerSide);
                _runner.Flight.SetIffCode(_runner.PlayerAircraftId, (int)playerSide);   // one IFF code per side (SENSORS-CONCEPT J5)
            }
            // The pilots seat themselves in their own Start; order after them on the first FixedUpdate.
        }

        bool _ordered;

        /// <summary>True once the fight has put its pilots in (the first FixedUpdate); a pilot seated later enlists itself.</summary>
        public bool Ordered => _ordered;

        /// <summary>A pilot seated after the fight was ordered (A23: a brain changed in play) joins it on its side.</summary>
        public void Enlist(AviatorPilot p)
        {
            if (_fight == null || p.FightSide == Side.None || p.Seat == null) return;
            int id = p.GetComponent<AircraftView>().AircraftId;
            _fight.Join(id, p.FightSide);
            _runner.Flight.SetIffCode(id, (int)p.FightSide);
            p.Seat.FightIn(id, _fight);
        }

        void FixedUpdate()
        {
            if (_fight == null) return;
            if (!_ordered)
            {
                _ordered = true;
                foreach (AviatorPilot p in FindObjectsByType<AviatorPilot>(FindObjectsInactive.Exclude).OrderBy(p => p.GetComponent<AircraftView>().AircraftId))
                {
                    if (p.FightSide == Side.None || p.Seat == null) continue;
                    int id = p.GetComponent<AircraftView>().AircraftId;
                    _fight.Join(id, p.FightSide);
                    _runner.Flight.SetIffCode(id, (int)p.FightSide);
                }
                foreach (AviatorPilot p in FindObjectsByType<AviatorPilot>(FindObjectsInactive.Exclude))
                    if (p.FightSide != Side.None && p.Seat != null) p.Seat.FightIn(p.GetComponent<AircraftView>().AircraftId, _fight);
                Debug.Log($"[RH Aviator] fight: {_fight.Fighters.Count} aircraft, blue {_fight.Fighters.Count(i => _fight.SideOf(i) == Side.Blue)}, red {_fight.Fighters.Count(i => _fight.SideOf(i) == Side.Red)}; red hunts aircraft {_runner.PlayerAircraftId}.", this);
            }
            _fight.Score(_runner.Flight);
        }

        void OnGUI()
        {
            if (!showScore || _fight == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22f * Mathf.Max(1f, Screen.height / 1080f)), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
                _style.normal.textColor = colour;
            }
            float s = Mathf.Max(1f, Screen.height / 1080f), w = Screen.width;
            int player = _runner.PlayerAircraftId;
            string state = "";
            if (player != 0)
            {
                foreach (int other in _fight.Fighters)
                {
                    if (!_fight.Enemies(player, other)) continue;
                    if (RearCone.Inside(_runner.Flight.Find(player).State, _runner.Flight.Find(other).State, _fight.ConeDeg, _fight.RangeM)) state = $"  ·  ON {other}'S TAIL";
                    else if (RearCone.Inside(_runner.Flight.Find(other).State, _runner.Flight.Find(player).State, _fight.ConeDeg, _fight.RangeM)) state = $"  ·  {other} ON YOUR TAIL";
                }
            }
            GUI.Label(new Rect(0.5f * w - 400f * s, 70f * s, 800f * s, 34f * s),
                      $"KILLS  BLUE {_fight.SideKills(Side.Blue)}  ·  RED {_fight.SideKills(Side.Red)}{(_fight.FriendlyKills > 0 ? $"  ·  FRIENDLY {_fight.FriendlyKills}" : "")}     REAR CONE  BLUE {_fight.SideSeconds(Side.Blue):F0} S  ·  RED {_fight.SideSeconds(Side.Red):F0} S{state}", _style);
        }
    }
}
