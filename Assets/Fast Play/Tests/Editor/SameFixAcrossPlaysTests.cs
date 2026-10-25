using System.Collections;
using System.Collections.Generic;
using DeepWater.GeneralQuarters.Examples;
using DeepWater.GeneralQuarters.Modules.Combat;
using DeepWater.GeneralQuarters.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeepWater.FastPlay.Tests
{
    /// <summary>
    /// The determinism pin for play mode without a domain reload (BUILD-SPEED-LEDGER, round 6).
    /// - Plays GQ_Range twice in a row with no domain reload between the plays.
    /// - Each play starts the saturation raid at the same core tick, from inside FixedUpdate, so frame timing cannot move it.
    /// - It captures the combat core's Fix a fixed number of ticks later. Static state leaking from the first play into
    ///   the second shows up as a different Fix.
    /// About 40 s. It runs only when the project's Enter Play Mode Options skip the domain reload.
    /// </summary>
    public class SameFixAcrossPlaysTests
    {
        const string RangeScene = "Assets/General Quarters/Examples/Scenes/GQ_Range.unity";
        const string RaidAsset = "Assets/General Quarters/Examples/Raids/Raid_Saturation.asset";
        const long RaidStartTick = 50;           // 1 s into the play, at 50 Hz
        const int TicksAfterRaidStart = 750;     // 15 s more: missiles in flight, ESSM launched
        const float WallClockLimitS = 60f;

        // Survives the play-mode transitions: there is no domain reload in this test.
        static readonly List<ulong> Fixes = new List<ulong>();

        [UnityTest]
        public IEnumerator TwoPlays_WithoutADomainReload_GiveTheSameFix()
        {
            if (!EditorSettings.enterPlayModeOptionsEnabled || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0)
                Assert.Ignore("the project reloads the domain on play; this pin is for play without a reload");
            string before = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            var raid = AssetDatabase.LoadAssetAtPath<RaidScenarioSO>(RaidAsset);
            Assert.IsNotNull(raid, RaidAsset);
            EditorSceneManager.OpenScene(RangeScene);
            Fixes.Clear();
            bool runInBackground = PlayerSettings.runInBackground;
            PlayerSettings.runInBackground = true;   // an unfocused Editor barely ticks the player loop otherwise

            for (int play = 0; play < 2; play++)
            {
                yield return new EnterPlayMode(expectDomainReload: false);
                Application.runInBackground = true;
                var director = Object.FindAnyObjectByType<RangeDirector>();
                Assert.IsNotNull(director, "GQ_Range has its RangeDirector");
                var probe = new GameObject("FixProbe").AddComponent<FixProbe>();
                probe.Arm(director.Combat, raid, RaidStartTick, RaidStartTick + TicksAfterRaidStart);
                float t0 = Time.realtimeSinceStartup;
                while (!probe.Done && Time.realtimeSinceStartup - t0 < WallClockLimitS) yield return null;
                bool captured = probe.Captured;
                Fixes.Add(probe.Fix);
                Debug.Log($"[FastPlay] play {play + 1}: {(captured ? $"Fix {probe.Fix:X16}" : "no capture: " + probe.Problem)} at core tick {probe.CaptureTick}, raid from tick {probe.RaidTick} " +
                          $"(fixedTime {Time.fixedTime:F2}, frame {Time.frameCount})");
                yield return new ExitPlayMode();
                if (!captured)
                {
                    PlayerSettings.runInBackground = runInBackground;
                    Assert.Fail($"play {play + 1}: {probe.Problem ?? $"the core did not reach tick {probe.CaptureTick} within {WallClockLimitS} s"}");
                }
            }

            PlayerSettings.runInBackground = runInBackground;
            if (!string.IsNullOrEmpty(before)) EditorSceneManager.OpenScene(before);
            Assert.AreEqual(Fixes[0], Fixes[1], "same seed, same raid from the same tick: the same Fix in both plays, so nothing leaked between them");
        }

        /// <summary>
        /// Runs after ShipCombat's own FixedUpdate. At the raid tick it starts the raid; at the capture tick it reads the Fix.
        /// </summary>
        [DefaultExecutionOrder(int.MaxValue)]
        sealed class FixProbe : MonoBehaviour
        {
            ShipCombat _combat;
            RaidScenarioSO _raid;
            public long RaidTick { get; private set; }
            public long CaptureTick { get; private set; }
            public bool Captured { get; private set; }
            public bool Done => Captured || Problem != null;
            public string Problem { get; private set; }
            public ulong Fix { get; private set; }

            public void Arm(ShipCombat combat, RaidScenarioSO raid, long raidTick, long captureTick)
            {
                _combat = combat;
                _raid = raid;
                RaidTick = raidTick;
                CaptureTick = captureTick;
            }

            void FixedUpdate()
            {
                if (Done || _combat == null || _combat.Combat == null) return;
                long tick = _combat.Combat.Tick;
                if (_combat.Raid == null)
                {
                    if (tick > RaidTick) { Problem = $"the probe first ran at tick {tick}, after the raid tick {RaidTick}"; return; }
                    if (tick == RaidTick) _combat.StartRaid(_raid);
                    return;
                }
                if (tick < CaptureTick) return;
                if (tick > CaptureTick) { Problem = $"skipped the capture tick: at {tick}"; return; }
                Fix = _combat.Combat.Fix;
                Captured = true;
            }
        }
    }
}
