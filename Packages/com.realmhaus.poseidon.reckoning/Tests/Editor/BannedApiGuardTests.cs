using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

namespace POSEIDON.Reckoning.Tests
{
    // (f) Deliberately bad code, one sin per type. These live only in the test assembly and are never
    // called; the guard reads their IL. Each must be caught by name.
#pragma warning disable CS0618 // obsolete Unity API is fine here; the code never runs
    internal static class BadUnityTime { public static float Sample() => UnityEngine.Time.deltaTime; }
    internal static class BadUnityRandom { public static float Sample() => UnityEngine.Random.value; }
    internal static class BadUnityPhysics { public static UnityEngine.Vector3 Sample() => UnityEngine.Physics.gravity; }
    internal static class BadFindObject { public static UnityEngine.Camera Sample() => UnityEngine.Object.FindFirstObjectByType<UnityEngine.Camera>(); }
    internal static class BadDateTimeNow { public static DateTime Sample() => DateTime.Now; }
    internal static class BadDateTimeUtcNow { public static DateTime Sample() => DateTime.UtcNow; }
    internal static class BadTickCount { public static int Sample() => Environment.TickCount; }
    internal static class BadSystemRandom { public static int Sample() => new Random(5).Next(); }
    internal static class BadSin { public static double Sample(double x) => Math.Sin(x); }
    internal static class BadCos { public static double Sample(double x) => Math.Cos(x); }
    internal static class BadExp { public static double Sample(double x) => Math.Exp(x); }
    internal static class BadAtan2 { public static double Sample(double x) => Math.Atan2(x, x); }
    internal static class BadPow { public static double Sample(double x) => Math.Pow(x, x); }
    internal static class BadLog { public static double Sample(double x) => Math.Log(x); }
    internal static class BadDictionaryWalk
    {
        public static int Sample(Dictionary<int, int> map)
        {
            int total = 0;
            foreach (var pair in map) total += pair.Value;
            return total;
        }
    }
    internal sealed class BadBehaviour : UnityEngine.MonoBehaviour { }
    internal static class BadInsideLambda
    {
        // The ban must reach into the closure class the compiler makes for this lambda.
        public static Func<double> Sample(double x) => () => Math.Sin(x);
    }
    internal sealed class BadInGeneric<T>
    {
        public double Sample(T unused) => Math.Exp(1.0);
    }
    internal static class BadInStaticConstructor
    {
        public static readonly double Value = Math.Cos(1.0);
    }
#pragma warning restore CS0618

    internal static class CleanSample
    {
        public static double Sample(double x, Dictionary<int, double> map)
        {
            // Everything here is allowed: exact math and a keyed lookup.
            return Math.Sqrt(Math.Abs(x)) + (map.TryGetValue(1, out double value) ? value : 0.0);
        }
    }

    public class BannedApiGuardTests
    {
        private static readonly System.Reflection.Assembly Reckoning = typeof(ReckonedWorld).Assembly;

        [Test]
        public void TheReckoningPackage_UsesNoBannedApi()
        {
            var report = BannedApiGuard.Scan(Reckoning);

            Assert.AreEqual("POSEIDON.Reckoning", Reckoning.GetName().Name);
            Assert.IsEmpty(report.Violations, string.Join("\n", report.Violations));

            // A broken reader would also find nothing. Prove it walked the real code.
            Assert.Greater(report.TypesScanned, 15);
            Assert.Greater(report.MethodsScanned, 100);
            Assert.Greater(report.ReferencesChecked, 300);

            string references = string.Join(", ", Reckoning.GetReferencedAssemblies().Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal));
            Debug.Log($"[RECKONING] (f) guard on POSEIDON.Reckoning: {report.TypesScanned} types, {report.MethodsScanned} method bodies, {report.ReferencesChecked} member references, 0 violations; assembly references: {references}");
        }

        [Test]
        public void TheReckoningAssembly_ReferencesNoEngineAssembly()
        {
            var engine = Reckoning.GetReferencedAssemblies().Where(a => a.Name.StartsWith("Unity", StringComparison.Ordinal)).Select(a => a.Name).ToArray();
            CollectionAssert.IsEmpty(engine, "Phase 0 is pure C#. When Burst jobs arrive this will need the engine core module; loosen it then, on purpose.");
        }

        [TestCase(typeof(BadUnityTime), "UnityEngine.Time")]
        [TestCase(typeof(BadUnityRandom), "UnityEngine.Random")]
        [TestCase(typeof(BadUnityPhysics), "UnityEngine.Physics")]
        [TestCase(typeof(BadFindObject), "UnityEngine.Object.Find")]
        [TestCase(typeof(BadDateTimeNow), "System.DateTime.get_Now")]
        [TestCase(typeof(BadDateTimeUtcNow), "System.DateTime.get_UtcNow")]
        [TestCase(typeof(BadTickCount), "System.Environment.get_TickCount")]
        [TestCase(typeof(BadSystemRandom), "System.Random")]
        [TestCase(typeof(BadSin), "System.Math.Sin")]
        [TestCase(typeof(BadCos), "System.Math.Cos")]
        [TestCase(typeof(BadExp), "System.Math.Exp")]
        [TestCase(typeof(BadAtan2), "System.Math.Atan2")]
        [TestCase(typeof(BadPow), "System.Math.Pow")]
        [TestCase(typeof(BadLog), "System.Math.Log")]
        [TestCase(typeof(BadDictionaryWalk), "Dictionary`2.GetEnumerator")]
        [TestCase(typeof(BadBehaviour), "UnityEngine.MonoBehaviour")]
        [TestCase(typeof(BadInsideLambda), "System.Math.Sin")]
        [TestCase(typeof(BadInGeneric<>), "System.Math.Exp")]
        [TestCase(typeof(BadInStaticConstructor), "System.Math.Cos")]
        public void TheGuard_FailsOnBadCode_ByName(Type bad, string expected)
        {
            var report = BannedApiGuard.Scan(bad);

            Assert.IsTrue(report.Violations.Any(v => v.Contains(expected)),
                $"Expected a violation naming {expected}, got: [{string.Join(" | ", report.Violations)}]");
        }

        [Test]
        public void TheGuard_FailsOnTheTestAssemblyAsAWhole()
        {
            // The same entry point the package test uses, aimed at an assembly with planted sins.
            var report = BannedApiGuard.Scan(typeof(BannedApiGuardTests).Assembly);
            Assert.GreaterOrEqual(report.Violations.Count, 19, string.Join("\n", report.Violations));
            Debug.Log($"[RECKONING] (f) guard on the test assembly: {report.Violations.Count} violations found, as planted");
        }

        [Test]
        public void TheGuard_PassesCleanCode()
        {
            var report = BannedApiGuard.Scan(typeof(CleanSample));
            Assert.IsEmpty(report.Violations, string.Join("\n", report.Violations));
            Assert.Greater(report.ReferencesChecked, 2, "Sqrt, Abs and TryGetValue should each have been read.");
        }
    }
}
