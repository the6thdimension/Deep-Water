using System;
using System.Diagnostics;
using UnityEngine;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;

namespace DeepWater.AirWing.Modules.Diagnostics
{
    /// <summary>One benchmark run: what was flown and what it cost.</summary>
    public readonly struct BenchmarkResult
    {
        public readonly int Aircraft;
        public readonly int Ticks;
        public readonly int Substeps;
        public readonly double MsPerStep;

        public BenchmarkResult(int aircraft, int ticks, int substeps, double msPerStep)
        {
            Aircraft = aircraft;
            Ticks = ticks;
            Substeps = substeps;
            MsPerStep = msPerStep;
        }

        public double MsPerAircraftStep => MsPerStep / Aircraft;

        public override string ToString() =>
            $"{Aircraft} aircraft × {Ticks} ticks ({Substeps} substeps): {MsPerStep * 1000.0:F2} µs per step, " +
            $"{MsPerAircraftStep * 1000.0:F2} µs per aircraft per step";
    }

    /// <summary>
    /// The W21 budget's instrument (unity-perf-ab): times the flight core alone, with no scene. N aircraft fly straight
    /// and level at 5,000 m and 400 kt on hold seats over a flat surface, 500 m apart; the warm-up is discarded; the
    /// clock is a Stopwatch around <see cref="FlightCore.Step"/> only. What it leaves out, and says so: the sea sample
    /// (the scene's <c>ISeaSurface</c>), the views and the HUD. It does not count allocations:
    /// <c>GC.GetAllocatedBytesForCurrentThread</c> returns 0 in Unity's Mono (measured 2026-10-25 on a 100 kB array);
    /// use the profiler's GC instruments for that.
    /// </summary>
    public static class FlightBenchmark
    {
        public static BenchmarkResult Run(AircraftProfile profile, FidelitySheet sheet, int aircraft, int warmupTicks, int ticks)
        {
            if (aircraft < 1 || ticks < 1 || warmupTicks < 0) throw new ArgumentException("at least one aircraft and one tick");
            var core = new FlightCore(new SubstepClock(Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f, sheet.SubstepsPerTick), new FlatSurface(0f));
            float speed = 400f * (float)Units.KnotToMetrePerSecond;
            for (int i = 0; i < aircraft; i++)
            {
                var start = AircraftFactory.LevelStart(profile, new Vector3(500f * i, 5000f, 0f), 0f, speed, 0.6f * profile.Airframe.InternalFuelKg);
                core.Add(AircraftFactory.Build(i + 1, profile, sheet, start), new HoldSeat(new PilotInputs { Throttle = 0.7f }));
            }
            for (int t = 0; t < warmupTicks; t++) core.Step();

            var clock = Stopwatch.StartNew();
            for (int t = 0; t < ticks; t++) core.Step();
            clock.Stop();
            return new BenchmarkResult(aircraft, ticks, sheet.SubstepsPerTick, clock.Elapsed.TotalMilliseconds / ticks);
        }

        /// <summary>The median of <paramref name="repeats"/> runs by cost per step.</summary>
        public static BenchmarkResult Median(AircraftProfile profile, FidelitySheet sheet, int aircraft, int warmupTicks, int ticks, int repeats)
        {
            var runs = new BenchmarkResult[Mathf.Max(1, repeats)];
            for (int i = 0; i < runs.Length; i++) runs[i] = Run(profile, sheet, aircraft, warmupTicks, ticks);
            Array.Sort(runs, (a, b) => a.MsPerStep.CompareTo(b.MsPerStep));
            return runs[runs.Length / 2];
        }
    }
}
