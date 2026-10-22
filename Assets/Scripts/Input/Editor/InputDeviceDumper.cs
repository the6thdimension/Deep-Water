#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeepWater.Input.Editor
{
    /// <summary>
    /// Editor menu commands for diagnosing flight stick input. These exist because the
    /// Input System's "Window → Analysis → Input Debugger" is great but takes setup; the
    /// menus below dump information directly to the Console where you can see and copy it.
    ///
    /// **Menu paths:**
    /// - `Deep Water/Input/Dump All Devices`            — every device Unity sees, with paths
    /// - `Deep Water/Input/Dump All Joystick Controls`  — for each Joystick, every control with its path
    /// </summary>
    public static class InputDeviceDumper
    {
        [MenuItem("Deep Water/Input/Dump All Devices")]
        public static void DumpDevices()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Input System: All Devices ===");
            int n = 0;
            foreach (var device in InputSystem.devices)
            {
                n++;
                sb.AppendLine($"  [{n}] {device.displayName}");
                sb.AppendLine($"      layout : {device.layout}");
                sb.AppendLine($"      path   : {device.path}");
                sb.AppendLine($"      class  : {device.GetType().Name}");
                sb.AppendLine($"      added  : {device.added}");
            }
            if (n == 0) sb.AppendLine("  (no devices found — plug in your flight stick and try again)");
            Debug.Log(sb.ToString());
        }

        [MenuItem("Deep Water/Input/Dump All Joystick Controls")]
        public static void DumpJoystickControls()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Input System: Joystick + Gamepad Controls ===");
            sb.AppendLine("Copy the 'path' field of any control into your .inputactions binding to use it.");
            sb.AppendLine("Some HOTAS sticks register as Gamepad (XInput) instead of Joystick (DirectInput) — both are shown.");
            sb.AppendLine();

            int candidates = 0;
            foreach (var device in InputSystem.devices)
            {
                bool isStick = device is Joystick;
                bool isGamepad = device is Gamepad;
                if (!isStick && !isGamepad) continue;
                candidates++;
                sb.AppendLine($"==== {device.displayName}  [{device.GetType().Name}] ====");
                sb.AppendLine($"  device path: {device.path}");
                sb.AppendLine($"  layout     : {device.layout}");
                sb.AppendLine();

                foreach (var c in device.allControls)
                {
                    string kind = c.GetType().Name;
                    string value = TryReadValueAsString(c);
                    sb.AppendLine($"  path: {c.path,-50}  layout: {c.layout,-14}  kind: {kind,-22}  value: {value}");
                }
                sb.AppendLine();
            }

            if (candidates == 0)
                sb.AppendLine("(no Joystick or Gamepad devices detected — plug in your VelocityOne and try again, or check 'Deep Water/Input/Dump All Devices')");

            Debug.Log(sb.ToString());
        }

        private static string TryReadValueAsString(InputControl c)
        {
            try
            {
                object boxed = c.ReadValueAsObject();
                return boxed?.ToString() ?? "null";
            }
            catch
            {
                return "(unreadable)";
            }
        }
    }
}
#endif
