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
    /// latitude, longitude and day speed, the weather presets as buttons and sliders for the weather, plus keys. Every
    /// slider, button and key issues a <see cref="MetocOrder"/> through the director, so every change is in the order
    /// log; nothing here sets METOC's state directly. A drag issues at most one order per kind per frame. While no
    /// slider has been touched for a moment, the sliders follow the report.
    /// <para>A preset button blends to its weather over a labelled time of day; a weather slider sets it at once.</para>
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
        [Tooltip("The weather presets the buttons blend to.")]
        [SerializeField] MetocWeatherSO[] weatherPresets = Array.Empty<MetocWeatherSO>();
        [Tooltip("A preset button blends over this much time of day, minutes (E33).")]
        [Min(0f)] [SerializeField] float presetBlendDayMinutes = 15f;

        const float MinVisibilityLog10 = 2f, MaxVisibilityLog10 = 4.7f;   // 100 m to 50 km

        SliderFloat _date, _time, _lat, _lon, _speed, _cover, _base, _high, _visibility;
        AppText _readout;
        readonly StringBuilder _sb = new StringBuilder(512);
        double _startMidnightUt, _pausedSpeed = 1;
        bool _timeDirty, _placeDirty, _speedDirty, _weatherDirty;
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

            card.Add(new Heading("Weather") { size = HeadingSize.XS });
            var presets = new VisualElement();
            presets.style.flexDirection = FlexDirection.Row;
            presets.style.flexWrap = Wrap.Wrap;
            foreach (MetocWeatherSO preset in weatherPresets)
            {
                if (preset == null) continue;
                var button = new Unity.AppUI.UI.Button { title = preset.displayName, size = Unity.AppUI.UI.Size.S };
                button.style.marginRight = button.style.marginBottom = 4;
                MetocWeatherSO p = preset;
                button.clickable.clicked += () => director.Issue(MetocOrder.SetWeather(p.ToWeather(), presetBlendDayMinutes * 60.0, $"panel preset {p.displayName}"));
                presets.Add(button);
            }
            card.Add(presets);
            var blendNote = new AppText($"Presets blend over {presetBlendDayMinutes:0.#} min of day time; sliders set at once.") { size = TextSize.XS };
            blendNote.style.opacity = 0.7f;
            card.Add(blendNote);
            _cover = AddSlider(card, "Cloud cover", 0f, 8f, 0.5f, v => $"{v:0.#}/8", () => _weatherDirty = true);
            _base = AddSlider(card, "Cloud base", 0f, 5000f, 50f, v => $"{v:0} m", () => _weatherDirty = true);
            _high = AddSlider(card, "High cloud", 0f, 8f, 1f, v => $"{v:0}/8", () => _weatherDirty = true);
            _visibility = AddSlider(card, "Visibility", MinVisibilityLog10, MaxVisibilityLog10, 0.02f, v => Distance(Math.Pow(10, v)), () => _weatherDirty = true);

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
            if (_weatherDirty)
            {
                // The sliders change what they show; thickness and the obscuration top stay as the target has them.
                MetocWeather t = director.Simulation.WeatherTarget;
                var w = new MetocWeather(_cover.value, _base.value, t.CloudThicknessM, _high.value, Math.Pow(10, _visibility.value), t.ObscurationTopM);
                director.Issue(MetocOrder.SetWeather(w, 0, Issuer));
                _weatherDirty = false;
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
            _cover.SetValueWithoutNotify((float)r.Weather.CoverOktas);
            _base.SetValueWithoutNotify(Mathf.Clamp((float)r.Weather.CloudBaseM, _base.lowValue, _base.highValue));
            _high.SetValueWithoutNotify((float)r.Weather.HighCloudOktas);
            _visibility.SetValueWithoutNotify(Mathf.Clamp((float)Math.Log10(r.Weather.VisibilityM), MinVisibilityLog10, MaxVisibilityLog10));
        }

        string Readout(in MetocReport r)
        {
            _sb.Clear();
            _sb.Append(r.TimeText).Append("   ");
            _sb.Append(r.DaySpeed > 0 ? $"{r.DaySpeed:0.#}× day speed" : "paused").AppendLine();
            _sb.Append($"Sun   {r.Sun.Sky.ElevationDeg,6:0.00}° elev  {r.Sun.Sky.AzimuthDeg,6:0.0}° az").AppendLine();
            _sb.Append($"Moon  {r.Moon.Sky.ElevationDeg,6:0.00}° elev  {r.Moon.Sky.AzimuthDeg,6:0.0}° az  ");
            _sb.Append($"{r.Moon.IlluminatedFraction:P0} lit, {(r.Moon.Waxing ? "waxing" : "waning")}").AppendLine();
            _sb.Append($"Ground light {Lux(r.Light.TotalLux)}  (sun {Lux(r.Light.SunLux)}, moon {Lux(r.Light.MoonLux)}, sky {Lux(r.Light.NightSkyLux)})").AppendLine();
            MetocWeather w = r.Weather;
            _sb.Append($"Cloud {w.CoverCode} {w.CoverOktas:0.#}/8 at {w.CloudBaseM:0} m, {w.CloudThicknessM:0} m thick · high {w.HighCloudOktas:0.#}/8").AppendLine();
            _sb.Append(w.HasCeiling ? $"Ceiling {w.CeilingM:0} m" : "No ceiling");
            _sb.Append($" · cloud lets {r.Light.CloudTransmission:P0} of the light through").AppendLine();
            _sb.Append($"Visibility {Distance(w.VisibilityM)}");
            if (w.ObscurationLabel.Length > 0) _sb.Append($" · {w.ObscurationLabel} to {w.ObscurationTopM:0} m");
            if (director.Simulation.BlendProgress < 1) _sb.AppendLine().Append($"Blending to {director.Simulation.WeatherTarget.CoverCode}, {director.Simulation.BlendProgress:P0}");
            if (metocRenderer != null && metocRenderer.isActiveAndEnabled)
            {
                _sb.AppendLine().Append($"Exposure floor EV {metocRenderer.ExposureFloorEv:0.0} · clouds' shapeFactor {metocRenderer.CloudShapeFactor:0.000}");
                if (!metocRenderer.CoverCalibrated) _sb.Append(" (UNCALIBRATED)");
            }
            _sb.AppendLine().Append($"Tick {r.Tick}   orders logged {director.Simulation.Log.Count}");
            return _sb.ToString();
        }

        static string Distance(double m) => m >= 10000 ? $"{m / 1000:0} km" : m >= 1000 ? $"{m / 1000:0.0} km" : $"{m:0} m";

        static string Lux(double lux) =>
            lux >= 1000 ? $"{lux / 1000:0.#} klux" : lux >= 1 ? $"{lux:0.#} lux" : lux >= 0.01 ? $"{lux:0.###} lux" : $"{lux * 1000:0.##} mlux";
    }
}
