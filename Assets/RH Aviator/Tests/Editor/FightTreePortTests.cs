using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Editor;
using DeepWater.Aviator.ScriptableObjects;

namespace DeepWater.Aviator.Tests
{
    /// <summary>
    /// The fight tree's port (RH Aviator A33, slice 4a): the fight rules and sensor habits moved into a behavior tree
    /// must fly exactly as they did. A set of headless fights (rookie, line and ace; one versus one and two versus two,
    /// with and without a hunted jet; far and near starts) is reduced to one hash per fight over every aircraft's Pilot
    /// Inputs on every tick and every rule its pilot logged. The hashes recorded from the rules before the port are kept
    /// in <see cref="GoldenPath"/>; the port must give the same ones.
    /// </summary>
    public class FightTreePortTests
    {
        public const string GoldenPath = "Assets/RH Aviator/Tests/Editor/FightTreePort.golden.txt";
        const float Kt = (float)Units.KnotToMetrePerSecond;
        const float Seconds = 90f, SafeSeconds = 150f;
        static readonly FidelitySheet L1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);

        /// <summary>The rules the set must fire, so that every branch of both trees is in the proof.</summary>
        static readonly string[] Covered =
        {
            "tally", "engage", "clear", "target gone", "break", "tail clear", "avoid", "vector", "search",
            "chaff", "chaff: missed", "flares", "flares: missed", "iff: ask", "iff: forgot", "iff: friend", "identified",
            "radar: lock", "radar: unlock", "radar missile",
        };

        static AircraftProfile E() => AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        static PilotProfile Pilot(string path, bool safe)
        {
            PilotProfile p = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(path).Bake();
            if (safe) { p.TriggerMils = 0f; p.MissileRangeM = 0f; p.RadarMissileRangeM = 0f; }   // the position fight, as FightTests flies it
            return p;
        }

        /// <summary>One fight of the set: who flies on each side, how far apart they start, and whether red hunts blue 1.</summary>
        struct Case
        {
            public string Name;
            public string[] Blue, Red;
            public float ApartM;
            public bool Hunt, Safe;
            public float Seconds;
            public ulong Seed;
        }

        static IEnumerable<Case> Cases()
        {
            string r = AviatorTools.RookiePilotPath, l = AviatorTools.LinePilotPath, a = AviatorTools.AcePilotPath;
            var pairs = new (string name, string[] blue, string[] red, bool hunt)[]
            {
                ("ace v line", new[] { a }, new[] { l }, false),
                ("line v rookie", new[] { l }, new[] { r }, false),
                ("rookie v ace", new[] { r }, new[] { a }, false),
                ("ace+line v line+rookie", new[] { a, l }, new[] { l, r }, false),
                ("rookie+ace v ace+line, red hunts blue 1", new[] { r, a }, new[] { a, l }, true),
            };
            foreach (var p in pairs)
                for (ulong seed = 1; seed <= 5; seed++)
                    yield return new Case { Name = p.name, Blue = p.blue, Red = p.red, Hunt = p.hunt, Seed = seed, ApartM = seed % 2 == 1 ? 30000f : 8000f, Seconds = Seconds };
            // Weapons safe, the position fight: nobody is shot down early, so the break, tail-clear and search branches run.
            foreach (var p in pairs)
                for (ulong seed = 1; seed <= 3; seed++)
                    yield return new Case { Name = p.name + ", weapons safe", Blue = p.blue, Red = p.red, Hunt = p.hunt, Safe = true, Seed = seed, ApartM = 8000f, Seconds = SafeSeconds };
        }

        /// <summary>Flies one case and hashes it: every aircraft's inputs on every tick, then every rule firing.</summary>
        static string Fly(in Case c, out int firings, IDictionary<string, int> rules)
        {
            AircraftProfile e = E();
            var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: c.Seed);
            var sides = new Engagement();
            int n = c.Blue.Length + c.Red.Length;
            var logs = new RuleLog[n + 1];
            var seats = new AviatorSeat[n + 1];
            for (int id = 1; id <= n; id++)
            {
                bool blue = id <= c.Blue.Length;
                int k = blue ? id - 1 : id - 1 - c.Blue.Length;
                logs[id] = new RuleLog();
                seats[id] = new AviatorSeat(core, e, Pilot(blue ? c.Blue[k] : c.Red[k], c.Safe), logs[id]);
                Vector3 at = blue ? new Vector3(600f * k, 3048f, 0f) : new Vector3(600f * k, 3300f, c.ApartM);
                core.Add(AircraftFactory.Build(id, e, L1, AircraftFactory.LevelStart(e, L1, at, blue ? 0f : 180f, 400f * Kt, 0.6f * e.Airframe.InternalFuelKg, 0.7f)), seats[id]);
                Side side = blue ? Side.Blue : Side.Red;
                sides.Join(id, side);
                core.SetIffCode(id, (int)side);
            }
            if (c.Hunt) sides.Hunted = () => 1;
            for (int id = 1; id <= n; id++) seats[id].FightIn(id, sides);

            ulong h = 14695981039346656037UL;
            long end = (long)(c.Seconds * 50);
            while (core.Tick < end)
            {
                core.Step();
                sides.Score(core);
                for (int id = 1; id <= n; id++) h = Mix(h, core.Find(id).LastInputs);
            }
            firings = 0;
            for (int id = 1; id <= n; id++)
                foreach (RuleFiring f in logs[id].Firings)
                {
                    h = Mix(h, f.ToString());
                    firings++;
                    rules.TryGetValue(f.Rule, out int k);
                    rules[f.Rule] = k + 1;
                }
            return h.ToString("x16");
        }

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++) { h ^= (v >> (8 * i)) & 0xFF; h *= 1099511628211UL; }
            return h;
        }

        static ulong Mix(ulong h, in PilotInputs p)
        {
            h = Mix(h, (ulong)(uint)BitConverter.SingleToInt32Bits(p.StickPitch) | (ulong)(uint)BitConverter.SingleToInt32Bits(p.StickRoll) << 32);
            h = Mix(h, (ulong)(uint)BitConverter.SingleToInt32Bits(p.Pedals) | (ulong)(uint)BitConverter.SingleToInt32Bits(p.Throttle) << 32);
            ulong flags = (p.Afterburner ? 1UL : 0) | (p.Brakes ? 2UL : 0) | (p.Trigger ? 4UL : 0) | (p.Pickle ? 8UL : 0) | (p.RadarPickle ? 16UL : 0);
            return Mix(h, flags | (ulong)p.Radar << 8 | (ulong)(ushort)p.Switches << 16 | (ulong)(uint)p.RadarTrack << 32);
        }

        static ulong Mix(ulong h, string s)
        {
            foreach (char ch in s) { h ^= ch; h *= 1099511628211UL; }
            return h;
        }

        static string Key(in Case c) => $"{c.Name} · {(c.ApartM / 1000f):F0} km · seed {c.Seed}";

        /// <summary>Every case's hash, one line each: "key = hash (firings)"; <paramref name="rules"/>, if given, counts each rule fired.</summary>
        public static List<string> FlyAll(IDictionary<string, int> rules = null)
        {
            rules = rules ?? new SortedDictionary<string, int>();
            var lines = new List<string>();
            foreach (Case c in Cases()) lines.Add($"{Key(c)} = {Fly(c, out int firings, rules)} ({firings} rules)");
            return lines;
        }

        /// <summary>Writes the golden hashes: run once on the rules before the port (2026-10-30), never to make a failing port pass.</summary>
        [Test, Explicit("Records the golden hashes from the code as it is; run only before a behaviour-preserving change")]
        public void RecordTheGoldenFights()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# The fight tree's port (RH Aviator A33): one hash per headless fight over every aircraft's Pilot Inputs on every tick and every rule logged.");
            sb.AppendLine($"# Recorded {DateTime.UtcNow:yyyy-MM-dd HH:mm}Z from the fight rules and sensor habits before the port; {Seconds:F0} s a fight, {SafeSeconds:F0} s weapons safe.");
            foreach (string line in FlyAll()) sb.AppendLine(line);
            File.WriteAllText(GoldenPath, sb.ToString());
            AssetDatabase.ImportAsset(GoldenPath);
            Debug.Log($"[FIGHT TREE] golden recorded: {GoldenPath}");
        }

        [Test]
        public void TheTree_FliesEveryFightExactlyAsTheRulesDid()
        {
            Assert.IsTrue(File.Exists(GoldenPath), "the golden hashes are recorded");
            var golden = new List<string>();
            foreach (string line in File.ReadAllLines(GoldenPath)) if (!line.StartsWith("#") && line.Length > 0) golden.Add(line);
            var rules = new Dictionary<string, int>();
            List<string> now = FlyAll(rules);
            // The proof covers only branches the set runs: every rule of the fight tree and the sensor habits must fire
            // somewhere ("radar: search" excepted: every jet starts with its radar on).
            foreach (string rule in Covered)
                Assert.IsTrue(rules.ContainsKey(rule), $"the golden fights never fire \"{rule}\", so the port of that branch is unproven");
            int same = 0;
            var differ = new List<string>();
            for (int i = 0; i < now.Count; i++)
            {
                if (i < golden.Count && golden[i] == now[i]) same++;
                else differ.Add($"{now[i]}  (was {(i < golden.Count ? golden[i] : "missing")})");
            }
            Debug.Log($"[FIGHT TREE] port: {same} of {now.Count} fights identical to the rules'");
            Assert.AreEqual(golden.Count, now.Count, "the same set of fights");
            Assert.IsEmpty(differ, "fights that differ:\n" + string.Join("\n", differ));
        }
    }
}
