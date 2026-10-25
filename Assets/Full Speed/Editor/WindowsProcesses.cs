#if UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DeepWater.FullSpeed.Editor
{
    /// <summary>
    /// The Win32 calls Full Speed needs:
    /// - a process snapshot with parent ids (Toolhelp32);
    /// - each process's power-throttling (EcoQoS) state, read and set;
    /// - whether the machine is on AC power.
    /// Every call returns its Win32 error as text instead of failing quietly.
    /// </summary>
    public static class WindowsProcesses
    {
        const uint SnapProcess = 0x00000002;                  // TH32CS_SNAPPROCESS
        const uint ProcessSetInformation = 0x0200;            // PROCESS_SET_INFORMATION
        const uint ProcessQueryLimitedInformation = 0x1000;   // PROCESS_QUERY_LIMITED_INFORMATION
        const int ProcessPowerThrottling = 4;                 // PROCESS_INFORMATION_CLASS.ProcessPowerThrottling
        const uint ThrottlingVersion = 1;                     // PROCESS_POWER_THROTTLING_CURRENT_VERSION
        public const uint ExecutionSpeed = 0x1;               // PROCESS_POWER_THROTTLING_EXECUTION_SPEED

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ProcessEntry32W
        {
            public uint Size, Usage, ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId, Threads, ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExeFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ThrottlingState
        {
            public uint Version, ControlMask, StateMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SystemPowerStatus
        {
            public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
            public int BatteryLifeTime, BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry32W entry);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry32W entry);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetProcessInformation(IntPtr process, int infoClass, ref ThrottlingState info, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetProcessInformation(IntPtr process, int infoClass, ref ThrottlingState info, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetSystemPowerStatus(out SystemPowerStatus status);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

        static readonly IntPtr InvalidHandle = new IntPtr(-1);
        static readonly uint StateSize = (uint)Marshal.SizeOf(typeof(ThrottlingState));

        /// <summary>Every process on the machine, with its parent id. Throws <see cref="Win32Exception"/> if Windows refuses.</summary>
        public static List<ProcessEntry> Snapshot()
        {
            IntPtr snap = CreateToolhelp32Snapshot(SnapProcess, 0);
            if (snap == InvalidHandle) throw new Win32Exception(Marshal.GetLastWin32Error(), "process snapshot");
            try
            {
                var list = new List<ProcessEntry>(400);
                var e = new ProcessEntry32W { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry32W)) };
                for (bool more = Process32FirstW(snap, ref e); more; more = Process32NextW(snap, ref e))
                    list.Add(new ProcessEntry((int)e.ProcessId, (int)e.ParentProcessId, e.ExeFile));
                return list;
            }
            finally
            {
                CloseHandle(snap);
            }
        }

        /// <summary>
        /// Full speed: the process is never put in EcoQoS. This is the switch Task Manager's "Efficiency mode" flips,
        /// and it lasts until the process exits. Returns null on success, or the Win32 error text.
        /// </summary>
        public static string SetFullSpeed(int processId) => SetState(processId, ExecutionSpeed);

        /// <summary>Back to the default: Windows decides again, as for any process. Returns null or the Win32 error text.</summary>
        public static string SetWindowsDefault(int processId) => SetState(processId, 0);

        static string SetState(int processId, uint controlMask)
        {
            IntPtr h = OpenProcess(ProcessSetInformation, false, processId);
            if (h == IntPtr.Zero) return new Win32Exception(Marshal.GetLastWin32Error()).Message;
            try
            {
                var s = new ThrottlingState { Version = ThrottlingVersion, ControlMask = controlMask, StateMask = 0 };
                return SetProcessInformation(h, ProcessPowerThrottling, ref s, StateSize) ? null : new Win32Exception(Marshal.GetLastWin32Error()).Message;
            }
            finally
            {
                CloseHandle(h);
            }
        }

        /// <summary>A process's explicit throttling state: (control mask, state mask), or the Win32 error text.</summary>
        public static (uint control, uint state, string error) GetState(int processId)
        {
            IntPtr h = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (h == IntPtr.Zero) return (0, 0, new Win32Exception(Marshal.GetLastWin32Error()).Message);
            try
            {
                var s = new ThrottlingState { Version = ThrottlingVersion };
                return GetProcessInformation(h, ProcessPowerThrottling, ref s, StateSize)
                    ? (s.ControlMask, s.StateMask, null)
                    : (0, 0, new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }
            finally
            {
                CloseHandle(h);
            }
        }

        /// <summary>True on AC power. When Windows can't tell (no battery, unknown), it counts as AC, so a desktop stays at full speed.</summary>
        public static bool OnAcPower()
        {
            if (!GetSystemPowerStatus(out SystemPowerStatus s)) return true;
            return s.AcLineStatus != 0;   // 0 = battery, 1 = AC, 255 = unknown
        }
    }
}
#endif
