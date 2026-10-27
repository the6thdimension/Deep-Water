using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;

namespace DeepWater.FastPlay.Tests
{
    /// <summary>
    /// Play mode starts without a domain reload (BUILD-SPEED-LEDGER, round 6), so static fields keep their values from
    /// the last play session. This guard keeps the first-party runtime assemblies safe for that. Every mutable static
    /// must either be reset at play start, by a [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] method in its type,
    /// or be listed in <see cref="Allowed"/> with the reason it may outlive a session.
    /// </summary>
    public class StaticStateTests
    {
        static readonly string[] FirstPartyRuntime =
        {
            "DeepWater.Sea", "DeepWater.Sea.Hdrp", "DeepWater.GeneralQuarters", "DeepWater.GeneralQuarters.Examples", "DeepWater.Gunnery", "DeepWater.AirWing",
            "GuidedFury", "CarrierOps", "RHRadarSuite", "RH.Shared", "DeepWater.Input", "DeepWater.Reckoning.Spike",
        };

        const string Table = "constant lookup table: nothing writes its elements (checked 2026-10-25)";
        const string Scratch = "scratch buffer: overwritten (or cleared) on every use, holds nothing between calls";

        /// <summary>Static state that may outlive a play session, with the reason. Keyed "Namespace.Type.field".</summary>
        static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["CarrierOps.Core.Motion.SumOfSinesMotion.PeriodOffsets"] = Table,
            ["CarrierOps.Core.Motion.SumOfSinesMotion.AmplitudeWeights"] = Table,
            ["DeepWater.GeneralQuarters.Core.EngineRoom.EngineRoomL0.TableSpeedsKn"] = Table,
            ["DeepWater.GeneralQuarters.Core.EngineRoom.EngineRoomL0.TableRuddersDeg"] = Table,
            ["DeepWater.GeneralQuarters.Core.DamageControl.DamageControlL1.ListMarksDeg"] = Table,
            ["DeepWater.Gunnery.Ballistics.Atmosphere.s_density"] = Table,
            ["DeepWater.Gunnery.Ballistics.Atmosphere.s_soundSpeed"] = Table,
            ["DeepWater.Gunnery.Ballistics.DragCurve.Mach"] = Table,
            ["DeepWater.Gunnery.Ballistics.DragCurve.Cd"] = Table,
            ["DeepWater.GeneralQuarters.Examples.ConnPanel.Ahead"] = Table,
            ["DeepWater.GeneralQuarters.Examples.ConnPanel.AheadLabels"] = Table,
            ["DeepWater.GeneralQuarters.Examples.ConnPanel.Astern"] = Table,
            ["DeepWater.GeneralQuarters.Examples.ConnPanel.AsternLabels"] = Table,
            ["DeepWater.GeneralQuarters.Examples.ConnPanel.Rudders"] = Table,
            ["DeepWater.GeneralQuarters.Examples.RangeScreen.Ahead"] = Table,
            ["DeepWater.GeneralQuarters.Examples.RangeScreen.AheadLabels"] = Table,
            ["DeepWater.GeneralQuarters.Examples.RangeScreen.Astern"] = Table,
            ["DeepWater.GeneralQuarters.Examples.RangeScreen.AsternLabels"] = Table,
            ["DeepWater.GeneralQuarters.Examples.RangeScreen.Rudders"] = Table,
            ["DeepWater.GeneralQuarters.Examples.RangeScreen.DoctrineWeapons"] = Table,
            ["DeepWater.GeneralQuarters.Examples.RangeScreen.Modes"] = Table,
            ["DeepWater.GeneralQuarters.Examples.RangeScreen.ModeLabels"] = Table,
            ["DeepWater.GeneralQuarters.Examples.RangeScreen.Roes"] = Table,
            ["DeepWater.GeneralQuarters.Examples.RangeScreen.RoeLabels"] = Table,
            ["DeepWater.GeneralQuarters.Examples.TrackScope.Scales"] = Table,
            ["DeepWater.Sea.Examples.SeaStateSelectorPanel.Digits"] = Table,
            ["DeepWater.AirWing.Core.AircraftFactory.AtmosphereLadder"] = "fidelity-ladder registry: filled once by the static constructor, never changed after",
            ["DeepWater.AirWing.Core.AircraftFactory.AirframeLadder"] = "fidelity-ladder registry: filled once by the static constructor, never changed after",
            ["DeepWater.AirWing.Core.AircraftFactory.PowerplantLadder"] = "fidelity-ladder registry: filled once by the static constructor, never changed after",
            ["DeepWater.AirWing.Core.AircraftFactory.ControlsLadder"] = "fidelity-ladder registry: filled once by the static constructor, never changed after",
            ["DeepWater.AirWing.Core.AircraftFactory.GearLadder"] = "fidelity-ladder registry: filled once by the static constructor, never changed after",
            ["DeepWater.Gunnery.Ballistics.FiringTable.s_cache"] = "deterministic cache keyed by every input of the table: a reused entry is the table that would be rebuilt",
            ["DeepWater.GeneralQuarters.Modules.Presentation.SynthClips.s_Cache"] = "cache of generated clips: a clip destroyed with the last session reads as null and is regenerated (SynthClips.Get)",
            ["DeepWater.Input.Adapters.FlightStickToTestRunner._whitePx"] = "lazily made 1×1 texture: a destroyed one reads as null and is made again",
            ["DeepWater.Sea.Modules.Hdrp.HdrpSeaRenderer.s_FoamGpuSampler"] = "profiler sampler: a name for the profiler, no simulation state",
            ["GuidedFury.Core.Missile.DetonationEffects.BlastOverlapBuffer"] = Scratch,
            ["GuidedFury.Core.Missile.DetonationEffects.BlastSeenBodies"] = Scratch,
            ["GuidedFury.Core.Missile.MissileBehaviour.s_fuzeBuffer"] = Scratch,
        };

        [Test]
        public void EveryMutableStatic_IsResetAtPlayStart_OrAllowedWithAReason()
        {
            var offenders = new List<string>();
            foreach (Type t in FirstPartyTypes())
            {
                if (t.IsDefined(typeof(CompilerGeneratedAttribute), false) || t.Name.StartsWith("<")) continue;   // lambda caches
                bool resets = HasPlayStartReset(t);
                foreach (FieldInfo f in t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsLiteral || IsImmutable(f)) continue;
                    string key = t.FullName + "." + f.Name;
                    if (resets || Allowed.ContainsKey(key)) continue;
                    offenders.Add($"{key} ({f.FieldType.Name}{(f.IsInitOnly ? ", readonly" : "")})");
                }
            }
            Assert.IsEmpty(offenders, "mutable statics with no play-start reset and no listed reason:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void TheAllowList_NamesOnlyFieldsThatExist()
        {
            var all = new HashSet<string>(FirstPartyTypes().SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                                                                             .Select(f => t.FullName + "." + f.Name)));
            CollectionAssert.IsEmpty(Allowed.Keys.Where(k => !all.Contains(k)), "stale entries in Allowed");
        }

        static IEnumerable<Type> FirstPartyTypes()
        {
            Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies().Where(a => FirstPartyRuntime.Contains(a.GetName().Name)).ToArray();
            Assert.AreEqual(FirstPartyRuntime.Length, loaded.Length, "every first-party runtime assembly is loaded: " + string.Join(", ", loaded.Select(a => a.GetName().Name)));
            return loaded.SelectMany(a => a.GetTypes());
        }

        static bool HasPlayStartReset(Type t) =>
            t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
             .Any(m => m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>() is RuntimeInitializeOnLoadMethodAttribute a
                       && a.loadType == RuntimeInitializeLoadType.SubsystemRegistration);

        // A static that cannot carry state between sessions: readonly, and of a type that cannot be changed through it.
        // That covers values, strings, types, delegates, and stateless strategy singletons (classes with no instance
        // fields anywhere in their hierarchy, e.g. ProportionalNavigation.Instance).
        static bool IsImmutable(FieldInfo f)
        {
            if (!f.IsInitOnly) return false;
            Type ft = f.FieldType;
            return ft.IsValueType || ft == typeof(string) || ft == typeof(Type) || typeof(Delegate).IsAssignableFrom(ft) || IsStateless(ft);
        }

        static bool IsStateless(Type t)
        {
            if (!t.IsClass || t.IsArray) return false;
            for (Type c = t; c != null && c != typeof(object); c = c.BaseType)
                if (c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length > 0) return false;
            return true;
        }
    }
}
