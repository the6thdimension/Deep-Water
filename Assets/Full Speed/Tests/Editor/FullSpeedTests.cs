using System.Collections.Generic;
using System.Diagnostics;
using DeepWater.FullSpeed.Editor;
using NUnit.Framework;
using FullSpeedService = DeepWater.FullSpeed.Editor.FullSpeed;   // inside DeepWater.FullSpeed.*, a bare "FullSpeed" names the namespace

namespace DeepWater.FullSpeed.Tests
{
    public class ProcessTreeTests
    {
        static readonly List<ProcessEntry> Machine = new List<ProcessEntry>
        {
            new ProcessEntry(0, 0, "[System Process]"),   // the idle process is its own parent
            new ProcessEntry(4, 0, "System"),
            new ProcessEntry(100, 4, "explorer.exe"),
            new ProcessEntry(200, 100, "Unity.exe"),
            new ProcessEntry(210, 200, "UnityShaderCompiler.exe"),
            new ProcessEntry(220, 200, "bee_backend.exe"),
            new ProcessEntry(221, 220, "dotnet.exe"),          // a grandchild: csc under Bee
            new ProcessEntry(300, 100, "obs64.exe"),           // a sibling of Unity, not its child
        };

        [Test]
        public void TheTree_HoldsTheRoot_ItsChildren_AndGrandchildren_NotItsSiblings()
        {
            HashSet<int> tree = ProcessTree.WithDescendants(200, Machine);
            CollectionAssert.AreEquivalent(new[] { 200, 210, 220, 221 }, tree);
        }

        [Test]
        public void AParentCycle_FromReusedIds_Ends()
        {
            var cycle = new List<ProcessEntry> { new ProcessEntry(1, 2, "a"), new ProcessEntry(2, 1, "b") };
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, ProcessTree.WithDescendants(1, cycle));
        }

        [Test]
        public void AProcessWithNoChildren_IsATreeOfOne()
        {
            CollectionAssert.AreEquivalent(new[] { 300 }, ProcessTree.WithDescendants(300, Machine));
        }
    }

#if UNITY_EDITOR_WIN
    /// <summary>Against Windows itself: a real child process of the Editor, set to full speed and handed back.</summary>
    public class PowerThrottlingTests
    {
        Process _child;

        [SetUp]
        public void StartChild()
        {
            // A harmless child that lives a few seconds: ping waits ~1 s per count on the loopback address.
            _child = Process.Start(new ProcessStartInfo("ping", "-n 8 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true });
        }

        [TearDown]
        public void StopChild()
        {
            if (_child != null && !_child.HasExited) _child.Kill();
            _child?.Dispose();
        }

        [Test]
        public void AChildOfTheEditor_IsFoundInItsTree()
        {
            HashSet<int> tree = ProcessTree.WithDescendants(Process.GetCurrentProcess().Id, WindowsProcesses.Snapshot());
            Assert.IsTrue(tree.Contains(_child.Id), "the snapshot links the ping process to the Editor");
        }

        [Test]
        public void FullSpeed_ThenWindowsDefault_RoundTrips()
        {
            Assert.IsNull(WindowsProcesses.SetFullSpeed(_child.Id));
            var (control, state, error) = WindowsProcesses.GetState(_child.Id);
            Assert.IsNull(error);
            Assert.AreNotEqual(0u, control & WindowsProcesses.ExecutionSpeed, "Windows now manages execution speed explicitly");
            Assert.AreEqual(0u, state & WindowsProcesses.ExecutionSpeed, "…and the explicit state is: not throttled");

            Assert.IsNull(WindowsProcesses.SetWindowsDefault(_child.Id));
            (control, _, error) = WindowsProcesses.GetState(_child.Id);
            Assert.IsNull(error);
            Assert.AreEqual(0u, control & WindowsProcesses.ExecutionSpeed, "handed back: Windows decides again");
        }

        [Test]
        public void OneScan_SetsANewChild_ToFullSpeed()
        {
            FullSpeedService.ScanNow();
            var (control, state, error) = WindowsProcesses.GetState(_child.Id);
            Assert.IsNull(error);
            Assert.AreNotEqual(0u, control & WindowsProcesses.ExecutionSpeed, "the scan reached the new child");
            Assert.AreEqual(0u, state & WindowsProcesses.ExecutionSpeed);
        }

        [Test]
        public void ASetOnAnExitedProcess_ReportsTheWin32Error_InsteadOfSucceeding()
        {
            _child.Kill();
            _child.WaitForExit(5000);
            int gone = _child.Id;
            _child.Dispose();   // our handle would keep the exited process object open
            _child = null;
            // Another short-lived handle can keep the exited process object alive a moment longer: Full Speed's own scan
            // sets this child too, since it descends from Unity. A headless run hit that once (2026-10-27). The error
            // must come once that handle closes, so allow 2 s for it.
            string error = null;
            for (int i = 0; i < 40 && error == null; i++)
            {
                error = WindowsProcesses.SetFullSpeed(gone);
                if (error == null) System.Threading.Thread.Sleep(50);
            }
            Assert.IsNotNull(error, "an exited process cannot be set; the error is returned, not swallowed");
        }
    }
#endif
}
