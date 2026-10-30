using System;
using System.Text;
using DeepWater.Metoc.Hdrp;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using AppText = Unity.AppUI.UI.Text;

namespace DeepWater.Metoc.Examples
{
    /// <summary>
    /// The METOC demo's runtime panel (App UI, E35): a readout of the report and sliders for the date, the time of day,
    /// latitude, longitude and day speed, plus keys. Every slider and key issues a <see cref="MetocOrder"/> through the
    /// director, so every change is in the order log; nothing here sets METOC's state directly. A drag issues at most
    /// one order per kind per frame. While no slider has been touched for a moment, the sliders follow the report.
    /// <para>Keys: T / Shift+T one hour on or back; = and − double or halve the day speed; P pauses or resumes.</para>
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MetocPanel : MonoBehaviour
    {
        const string Issuer = "panel";
        const float FollowAfterTouchSeconds = 0.75f;
        const double MaxDaySpeedLog10 = 3.6;

        [SerializeField] MetocDirector director;
        [Tooltip("Optional: shows the exposure floor it writes.")]
        [SerializeField] HdrpMetocRenderer metocRenderer;
        [Tooltip("Readout refreshes per second (real time).")]
        [SerializeField] float refreshHz = 10f;
        [Tooltip("The date slider's reach either side of the scenario's start date, days.")]
        [SerializeField] int dateReachDays = 183;

        SliderFloat _date, _time, _lat, _lon, _speed;
        AppText _readout;
        readonly StringBuilder _sb = new StringBuilder(512);
        double _startMidnightUt, _pausedSpeed = 1;
        bool _timeDirty, _placeDirty, _speedDirty;
        float _lastTouch = -10f, _nextRefresh;

        // Start, not OnEnable: the UIDocument builds its tree in its own OnEnable, and the order of OnEnables is not
        // fixed. By Start the director's Awake has made the simulation.
        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (director == null || !director.Ready || root == null)
            {
                Debug.LogError("[METOC] The panel needs a running director and a UIDocument; no panel.", this);
                enabled = false;
                return;
            }
            double startUt = director.Report.UtSeconds;
            _startMidnightUt = startUt - Calendar.SecondsOfDay(startUt);
            Build(root);
            Follow(director.Report);
        }

        void Build(VisualElement root)
        {
            // The App UI panel paints its theme's opaque background by default; over a 3D view it must be clear,
            // or it hides the whole scene (it did, the first time this demo ran). GQ's range clears it in its USS.
            var panel = new Panel { theme = "dark", scale = "small", pickingMode = PickingMode.Ignore };
            panel.style.position = Position.Absolute;
            panel.style.left = panel.style.top = panel.style.right = panel.style.bottom = 0;
            panel.style.backgroundColor = Color.clear;
            root.Add(panel);

            var card = new VisualElement();
            card.style.position = Position.Absolute;
            card.style.right = 16;
            card.style.top = 16;
            card.style.width = 380;
            card.style.paddingLeft = card.style.paddingRight = 12;
            card.style.paddingTop = card.style.paddingBottom = 10;
            card.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.82f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 6;
            panel.Add(card);

            card.Add(new Heading("METOC · sky") { size = HeadingSize.XS });
            _readout = new AppText { size = TextSize.S };
            _readout.style.whiteSpace = WhiteSpace.Normal;
            _readout.style.marginBottom = 6;
            card.Add(_readout);

            _date = AddSlider(card, "Date (UTC)", -dateReachDays, dateReachDays, 1f, v => DateText(v), () => _timeDirty = true);
            _time = AddSlider(card, "Time (UT)", 0f, 24f, 1f / 60f, v => $"{(int)v:00}:{(int)Math.Round((v - (int)v) * 60) % 60:00}", () => _timeDirty = true);
            _lat = AddSlider(card, "Latitude", -90f, 90f, 0.1f, v => $"{Math.Abs(v):0.0}° {(v >= 0 ? "N" : "S")}", () => _placeDirty = true);
            _lon = AddSlider(card, "Longitude", -180f, 180f, 0.1f, v => $"{Math.Abs(v):0.0}° {(v >= 0 ? "E" : "W")}", () => _placeDirty = true);
            _speed = AddSlider(card, "Day speed", 0f, (float)MaxDaySpeedLog10, 0.05f, v => $"{Math.Pow(10, v):0.#}×", () => _speedDirty = true);

            var help = new AppText("T / Shift+T  ±1 h     = / −  day speed ×2 / ÷2     P  pause") { size = TextSize.XS };
            help.style.opacity = 0.7f;
            card.Add(help);
        }

        SliderFloat AddSlider(VisualElement parent, string label, float low, float high, float step, FormatFunction<float> format, Action touched)
        {
            parent.Add(new AppText(label) { size = TextSize.XS });
            var slider = new SliderFloat { lowValue = low, highValue = high, step = step, formatFunction = format };
            slider.style.marginBottom = 4;
            slider.RegisterValueChangingCallback(_ => { touched(); _lastTouch = Time.unscaledTime; });
            slider.RegisterValueChangedCallback(_ => { touched(); _lastTouch = Time.unscaledTime; });
            parent.Add(slider);
            return slider;
        }

        string DateText(float daysFromStart)
        {
            Calendar.ToCalendar(_startMidnightUt + Math.Round(daysFromStart) * Calendar.SecondsPerDay, out int y, out int m, out int d, out _, out _, out _);
            return $"{y:0000}-{m:00}-{d:00}";
        }

        void Update()
        {
            if (!director.Ready) return;
            Keys();
            IssueDirty();

            if (Time.unscaledTime >= _nextRefresh)
            {
                MetocReport r = director.Report;
                if (Time.unscaledTime - _lastTouch > FollowAfterTouchSeconds) Follow(r);
                _readout.text = Readout(r);
                _nextRefresh = Time.unscaledTime + 1f / refreshHz;
            }
        }

        void Keys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            double ut = director.Report.UtSeconds, speed = director.Simulation.DaySpeed;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (kb.tKey.wasPressedThisFrame) director.Issue(MetocOrder.JumpTo(ut + (shift ? -3600.0 : 3600.0), "key T"));
            if (kb.equalsKey.wasPressedThisFrame) director.Issue(MetocOrder.DaySpeed(speed > 0 ? Math.Min(speed * 2, Math.Pow(10, MaxDaySpeedLog10)) : 1, "key ="));
            if (kb.minusKey.wasPressedThisFrame) director.Issue(MetocOrder.DaySpeed(Math.Max(speed / 2, 1), "key −"));
            if (kb.pKey.wasPressedThisFrame)
            {
                if (speed > 0) { _pausedSpeed = speed; director.Issue(MetocOrder.DaySpeed(0, "key P")); }
                else director.Issue(MetocOrder.DaySpeed(_pausedSpeed, "key P"));
            }
        }

        // One order per kind per frame, however many slider events the frame had.
        void IssueDirty()
        {
            if (_timeDirty)
            {
                double ut = _startMidnightUt + Math.Round(_date.value) * Calendar.SecondsPerDay + _time.value * 3600.0;
                director.Issue(MetocOrder.JumpTo(ut, Issuer));
                _timeDirty = false;
            }
            if (_placeDirty)
            {
                director.Issue(MetocOrder.Place(_lat.value, _lon.value, Issuer));
                _placeDirty = false;
            }
            if (_speedDirty)
            {
                director.Issue(MetocOrder.DaySpeed(Math.Pow(10, _speed.value), Issuer));
                _speedDirty = false;
            }
        }

        void Follow(in MetocReport r)
        {
            double fromMidnight = r.UtSeconds - _startMidnightUt;
            double days = Math.Floor(fromMidnight / Calendar.SecondsPerDay);
            _date.SetValueWithoutNotify(Mathf.Clamp((float)days, _date.lowValue, _date.highValue));
            _time.SetValueWithoutNotify((float)((fromMidnight - days * Calendar.SecondsPerDay) / 3600.0));
            _lat.SetValueWithoutNotify((float)r.Place.LatitudeDeg);
            _lon.SetValueWithoutNotify((float)r.Place.LongitudeDeg);
            _speed.SetValueWithoutNotify(r.DaySpeed > 0 ? Mathf.Clamp((float)Math.Log10(r.DaySpeed), 0f, (float)MaxDaySpeedLog10) : 0f);
        }

        string Readout(in MetocReport r)
        {
            _sb.Clear();
            _sb.Append(r.TimeText).Append("   ");
            _sb.Append(r.DaySpeed > 0 ? $"{r.DaySpeed:0.#}× day speed" : "paused").AppendLine();
            _sb.Append($"Sun   {r.Sun.Sky.ElevationDeg,6:0.00}° elev  {r.Sun.Sky.AzimuthDeg,6:0.0}° az").AppendLine();
            _sb.Append($"Moon  {r.Moon.Sky.ElevationDeg,6:0.00}° elev  {r.Moon.Sky.AzimuthDeg,6:0.0}° az  ");
            _sb.Append($"{r.Moon.IlluminatedFraction:P0} lit, {(r.Moon.Waxing ? "waxing" : "waning")}").AppendLine();
            _sb.Append($"Ground light {Lux(r.Light.TotalLux)}  (sun {Lux(r.Light.SunLux)}, moon {Lux(r.Light.MoonLux)}, sky {Lux(r.Light.NightSkyLux)})");
            if (metocRenderer != null && metocRenderer.isActiveAndEnabled) _sb.AppendLine().Append($"Exposure floor EV {metocRenderer.ExposureFloorEv:0.0}");
            _sb.AppendLine().Append($"Tick {r.Tick}   orders logged {director.Simulation.Log.Count}");
            return _sb.ToString();
        }

        static string Lux(double lux) =>
            lux >= 1000 ? $"{lux / 1000:0.#} klux" : lux >= 1 ? $"{lux:0.#} lux" : lux >= 0.01 ? $"{lux:0.###} lux" : $"{lux * 1000:0.##} mlux";
    }
}
