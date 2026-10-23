using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepWater.Input
{
    /// <summary>
    /// Diagnostic IMGUI overlay that shows the live state of every <see cref="FlightStickInput"/>
    /// axis and button. Critical for binding verification: when you plug the VelocityOne in
    /// for the first time, you can move each axis and see which logical action it triggers.
    /// If "Roll" doesn't move when you tilt the stick, you know the binding is wrong before
    /// you fight with an adapter that isn't responding.
    ///
    /// **Also surfaces:** detected raw joystick devices (Joystick API) and their button /
    /// axis counts. Useful when the action map seems silent — confirms Unity sees the
    /// hardware at all.
    ///
    /// Drop on the same GameObject as <see cref="FlightStickInput"/> (or anywhere — it'll
    /// find the reader automatically).
    /// </summary>
    public sealed class FlightStickHud : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private Vector2 anchor = new Vector2(12f, 12f);
        [SerializeField] private float width = 360f;
        [SerializeField] private bool showRawDevice = true;

        private FlightStickInput reader;
        private GUIStyle headerStyle;
        private GUIStyle rowStyle;

        private void OnEnable()
        {
            reader = GetComponent<FlightStickInput>() ?? FindFirstObjectByType<FlightStickInput>();
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (reader == null)
            {
                var rect = new Rect(anchor.x, anchor.y, width, 32f);
                GUI.Box(rect, "");
                GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f),
                    "FlightStickHud: no FlightStickInput in scene", headerStyle);
                return;
            }

            float h = 22f + 7 * 16f + 5 * 18f + (showRawDevice ? 80f : 0f) + 16f;
            var box = new Rect(anchor.x, anchor.y, width, h);
            GUI.Box(box, "");

            GUILayout.BeginArea(new Rect(box.x + 8f, box.y + 6f, box.width - 16f, box.height - 12f));

            GUILayout.Label("Flight Stick — live axes / buttons", headerStyle);
            DrawAxis("Pitch",              reader.Pitch);
            DrawAxis("Roll",               reader.Roll);
            DrawAxis("Yaw",                reader.Yaw);
            DrawAxis("ThrottleMain",       reader.ThrottleMain);
            DrawAxis("ThrottleSecondary",  reader.ThrottleSecondary);
            GUILayout.Label($"Hat: ({reader.Hat.x:+0.00;-0.00;0}, {reader.Hat.y:+0.00;-0.00;0})", rowStyle);
            GUILayout.Label($"Trigger: {reader.Trigger:0.00}", rowStyle);

            if (showRawDevice)
            {
                GUILayout.Space(4f);
                GUILayout.Label("Raw joysticks detected:", headerStyle);
                int count = 0;
                foreach (var device in InputSystem.devices)
                {
                    if (device is Joystick j)
                    {
                        count++;
                        GUILayout.Label($"• {j.displayName}", rowStyle);
                    }
                }
                if (count == 0)
                    GUILayout.Label("• (none — check USB connection)", rowStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawAxis(string name, float value)
        {
            // Build a small "bar" so values are easier to read than raw numbers.
            // Range is [-1..+1]. Draw 21 chars wide; center at index 10.
            const int barWidth = 21;
            int idx = Mathf.Clamp(Mathf.RoundToInt(value * 10f) + 10, 0, barWidth - 1);
            var sb = new System.Text.StringBuilder(barWidth);
            for (int i = 0; i < barWidth; i++)
            {
                if (i == idx) sb.Append('●');
                else if (i == 10) sb.Append('|');
                else sb.Append('·');
            }
            GUILayout.Label($"{name,-18} [{sb}]  {value:+0.00;-0.00;0.00}", rowStyle);
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
                rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
                rowStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }
}
