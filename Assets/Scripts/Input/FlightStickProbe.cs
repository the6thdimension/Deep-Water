using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace DeepWater.Input
{
    /// <summary>
    /// Bypass the action map and look directly at the device. Shows the live state of every
    /// control on every input device Unity has detected — paths included.
    ///
    /// **Use this when bindings aren't working:** when `FlightStickInput.Pitch` (etc.) reads
    /// zero no matter how you move the stick, that means the action's binding path doesn't
    /// match how the device reports the control. This probe shows you the actual paths so
    /// you can edit the `.inputactions` file with the right ones.
    ///
    /// **The VelocityOne might register as Joystick (DirectInput) OR Gamepad (XInput).** This
    /// probe surveys both. Many HOTAS sticks expose themselves as gamepads — in which case
    /// the `<Joystick>/...` bindings the default .inputactions uses won't fire at all, and
    /// you need to rebind to `<Gamepad>/leftStick`, etc.
    ///
    /// **What you see:** for each detected device, every Control (sticks, axes, buttons,
    /// hats) with its path and current value. Move a lever — its value changes — note the
    /// path — paste into your binding.
    /// </summary>
    public sealed class FlightStickProbe : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Vector2 anchor = new Vector2(400f, 12f);
        [SerializeField] private float width = 520f;
        [SerializeField] private float maxHeight = 700f;

        [Header("Filtering")]
        [Tooltip("Hide controls whose absolute value is below this threshold. Helps focus on the axis you're moving.")]
        [Range(0f, 1f)]
        [SerializeField] private float noiseFloor = 0.05f;

        [Tooltip("If true, only show controls that have changed since last frame (live discovery mode). Off shows the full snapshot.")]
        [SerializeField] private bool changedOnly = false;

        [Header("Logging")]
        [Tooltip("Log every changed control to the Console as you move it. Spammy — turn off when you're done discovering paths.")]
        [SerializeField] private bool logChangesToConsole = false;

        private Vector2 scroll;
        private GUIStyle headerStyle, rowStyle, hotStyle;

        // Track previous values so we can highlight changes and optionally log them.
        private readonly Dictionary<string, float> previousValues = new Dictionary<string, float>();

        private void OnGUI()
        {
            EnsureStyles();

            var rect = new Rect(anchor.x, anchor.y, width, maxHeight);
            GUI.Box(rect, "");

            GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f));
            GUILayout.Label("Joystick Probe — raw device state", headerStyle);
            GUILayout.Label($"Showing controls with |value| ≥ {noiseFloor:0.00}.  Toggle 'changedOnly' / 'logChangesToConsole' in Inspector.", rowStyle);
            GUILayout.Space(4f);

            scroll = GUILayout.BeginScrollView(scroll);

            int candidateCount = 0;
            foreach (var device in InputSystem.devices)
            {
                if (!IsLikelyStick(device)) continue;
                candidateCount++;

                GUILayout.Label($"<b>{device.displayName}</b>  ({device.layout})  path={device.path}  [{device.GetType().Name}]", headerStyle);

                DrawAllControls(device);
                GUILayout.Space(6f);
            }

            if (candidateCount == 0)
                GUILayout.Label("(no Joystick or Gamepad devices detected — plug in your VelocityOne, or run 'Deep Water/Input/Dump All Devices' to see what Unity does see)", rowStyle);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawAllControls(InputDevice device)
        {
            // Walk every control on the device and render its current value.
            foreach (var control in device.allControls)
            {
                if (control is InputControl<float> f)
                {
                    float v = f.ReadValue();
                    HandleControl(control.path, v);
                    if (changedOnly && !ValueChangedSince(control.path, v)) continue;
                    if (Mathf.Abs(v) < noiseFloor) continue;
                    DrawAxisRow(control.path, v);
                }
                else if (control is InputControl<Vector2> v2)
                {
                    Vector2 vv = v2.ReadValue();
                    HandleControl(control.path, vv.magnitude);
                    if (changedOnly && !ValueChangedSince(control.path, vv.magnitude)) continue;
                    if (vv.magnitude < noiseFloor) continue;
                    GUILayout.Label($"  {control.path}  =  ({vv.x:+0.00;-0.00;0.00}, {vv.y:+0.00;-0.00;0.00})", rowStyle);
                }
                else if (control is ButtonControl b)
                {
                    bool pressed = b.isPressed;
                    HandleControl(control.path, pressed ? 1f : 0f);
                    if (changedOnly && !ValueChangedSince(control.path, pressed ? 1f : 0f)) continue;
                    if (!pressed) continue;
                    GUILayout.Label($"  <color=#FFCC00>{control.path}  =  PRESSED</color>", hotStyle);
                }
                // Other control types (DpadControl etc.) inherit from one of the above.
            }
        }

        /// <summary>
        /// True if the device is plausibly a flight stick / HOTAS / gamepad. The VelocityOne
        /// can show up as either a `Joystick` (DirectInput HID) or a `Gamepad` (XInput) depending
        /// on driver. We accept both.
        /// </summary>
        private static bool IsLikelyStick(InputDevice device)
        {
            return device is Joystick || device is Gamepad;
        }

        private bool ValueChangedSince(string path, float value)
        {
            if (!previousValues.TryGetValue(path, out var prev)) return true;
            return Mathf.Abs(value - prev) > 0.02f;
        }

        private void HandleControl(string path, float currentValue)
        {
            if (!previousValues.TryGetValue(path, out var prev))
            {
                previousValues[path] = currentValue;
                return;
            }
            float delta = Mathf.Abs(currentValue - prev);
            if (delta > 0.05f)
            {
                if (logChangesToConsole)
                    Debug.Log($"[FlightStickProbe] {path}  prev={prev:0.00}  now={currentValue:0.00}");
                previousValues[path] = currentValue;
            }
        }

        private void DrawAxisRow(string path, float value)
        {
            const int barWidth = 21;
            int idx = Mathf.Clamp(Mathf.RoundToInt(value * 10f) + 10, 0, barWidth - 1);
            var sb = new System.Text.StringBuilder(barWidth);
            for (int i = 0; i < barWidth; i++)
            {
                if (i == idx) sb.Append('●');
                else if (i == 10) sb.Append('|');
                else sb.Append('·');
            }
            GUILayout.Label($"  {path,-38} [{sb}]  {value:+0.00;-0.00;0.00}", rowStyle);
        }

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13, fontStyle = FontStyle.Bold, richText = true,
                };
                headerStyle.normal.textColor = Color.white;
            }
            if (rowStyle == null)
            {
                rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true };
                rowStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            }
            if (hotStyle == null)
            {
                hotStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, fontStyle = FontStyle.Bold };
                hotStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
            }
        }
    }
}
