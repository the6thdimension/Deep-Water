# Test patterns at a rung

The real shapes from the three slices. Paths are under `Assets/General Quarters/Tests/Editor/` unless they say otherwise; `:N` is the line of the method. All are EditMode and headless: they build a `CombatSystem` from a loadout and step it by hand, with no scene.

## 1. Behaviour against an answer fixed first

Write the expected number beside the test, with where it came from, before the first run.

- **A closed form, with the tolerances in the class doc:** `DamageControlL1Tests` (header lines 23–25, the `Within` helper at :32). From `FloodedWing_ListsTrimsAndLosesGm_AsPredicted` (:163):
  ```csharp
  // python -I flood_predict.py --bilge "06 wing S":
  //   LIST +4.34 deg, TRIM +12.9 cm by the head, sinkage at amidships 8.0 cm, GM 1.560 -> 1.345 m (lost buoyancy).
  Within("list, deg", 4.34f, r.list, 0.05f, 0.2f);
  Within("GM, m", 1.345f, r.gm, 0.05f, 0f);
  ```
- **A number worked by hand:** `RadarL1Tests.Calibration_PutsTheReferenceTargetAtItsPd_AtThePublicRange` (:61) asserts the Swerling 1 SNR (12.7719 dB) and that the equation passes through the public range.
- **A textbook limit:** `GunsL1Tests.Vacuum_GreatestRangeIsVSquaredOverG_At45Degrees` (:92).
- **An identity that holds by construction:** `DamageControlL1Tests.SpaceOpenToTheSea_HoldsExactlyTheBuoyancyItsColumnsLose` (:65).
- **CALIBRATED and VALIDATION, kept apart:** `GunsL1Tests.CalibratedFormFactors_ReproduceThePublishedRanges_AndTheValidationFigures` (:101). The calibrated ranges guard against regressions; the validation figures are evidence (C16).

## 2. Seeded draws behave as the statistics say

`RadarL1Tests.SeededLooks_DetectAsSwerling1Predicts_AndSpendTheRunsDraws` (:77) runs 4,000 looks at the Pd 0.5 range.
- The detections fall within 3σ of 2,000.
- The draw count is exact (one per look, six more per detection), so the Fix sees every draw.
- The same seed replays every plot.

`SeededRandom_DrawsOnlyFromTheRunsGenerator` (:184) proves the `System.Random` adapter spends only the run's draws.

## 3. The rung refuses to build without its data

```csharp
// RadarL1Tests.L1_WithoutItsProfileData_RefusesToBuild (:170)
CombatLoadout l = TestLoadouts.Ddg51L0();
l.RadarLevel = FidelityLevel.L1;
Assert.Throws<System.ArgumentException>(() => new CombatSystem(l), "Radar L1 with no data: no silent fall back");
```

The same test for guns: `GunsL1Tests.L1_WithoutBallisticsData_RefusesToBuild` (:237).

## 4. A level above the built one falls back out loud

```csharp
// CoreContractTests.Ladder_FallsBackToTheHighestBuiltLevel_AndSaysSo (:93)
FidelityResolution r = FidelityLadder.Resolve(ElementKind.Radar, FidelityLevel.L2);
Assert.AreEqual(FidelityLevel.L1, r.Resolved, "Radar L1 is the highest built (Stream A)");
StringAssert.Contains("L2 requested, L1 used", r.ToString());
```

- `CombatSystem` writes each fallback to the log as `EventKind.FidelityFallback` (`Core/CombatSystem.cs:221`). `templates/fix-pin-test.cs` checks it end to end.
- Air Wing: `Assets/Air Wing/Tests/Editor/CoreContractTests.cs`, `Ladder_RequestAboveBuilt_FallsBackOutLoud` (:49).

## 5. The Fix pin: three shapes

Every one uses the same seed and the same Orders, runs twice, and compares Fixes. The new rung never gets a golden constant (C29).

| Shape | Test | Asserts |
|---|---|---|
| Final Fix, warm, with the budget | `GunsL1Tests.Saturation_AtGunsL1_Resolves_Repeats_AndStaysInBudget` (:359) | A warm-up run first (JIT, firing tables), then `a.fix == b.fix`; every threat accounted for; ≤ 1 ms per fixed step |
| A verdict on the same tick with the same Fix | `DamageControlL1Tests.PastTheMargin_SevenStations_SinksHer_TheSameWayEveryTime` (:265) | `a.tick == b.tick` and `a.fix == b.fix`; the verdict's cause chain ends in roots (the Breach Orders); ≤ 1 ms |
| The Fix on every tick, and another seed differs | `RadarL1Tests.RaidL1_SameSeedSameFix_EveryTick_AndEveryKillTracesToADetection` (:306), after `CombatCoreL0Tests.RaidL0_PlaysEndToEnd_ExplainsEveryKill_AndRepeatsExactly` (:200) | `CollectionAssert.AreEqual(fixesA, fixesB)`; seed 7 gives a different last Fix; every kill traces to a track and the detection draw that confirmed it |

The runner they share (`RadarL1Tests`, the `Raid` helper above :306):
```csharp
for (int i = 0; i < maxTicks; i++)
{
    hits.Clear();
    raid.Step(cs.Tick, cs.TickSeconds, Own, truth, hits);
    kills.Clear();
    watch.Start();
    cs.Step(Own, truth, hits, kills);
    watch.Stop();
    raid.ApplyKills(kills);
    fixes.Add(cs.Fix);
    ...
}
```

Air Wing's: `FlightL0Tests.TwoRunsWithTheSameInputs_GiveTheSameFixOnEveryTick` (:112) and `ControlsL1Tests.AtL1_TheFlightCore_IsBitIdentical_AndReplays` (:261), which compare with `Fixes.FirstDivergence`.

## 6. The golden pins the levels it was recorded at

```csharp
// PresentationTests.Saturation_Fix_IsUnchangedByPresentationData (:75)
loadout.GunsLevel = Core.Fidelity.FidelityLevel.L0;   // the slice 5 run this constant pins was at guns L0 (Stream B's L1 has its own test)
loadout.DamageLevel = Core.Fidelity.FidelityLevel.L0; // and at Damage Control L0 (Stream C's L1 has its own tests)
loadout.RadarLevel = Core.Fidelity.FidelityLevel.L0;  // and at Radar and Tracks L0 (Stream A's L1 has its own tests)
loadout.TracksLevel = Core.Fidelity.FidelityLevel.L0;
...
Assert.AreEqual(0x2AF5C040E4AC6B5EUL, cs.Fix, $"Fix {cs.Fix:X16}");
```

Each slice added its own line here. Change the constant only for a change of behaviour you meant, and say so in the commit (C20).

## 7. The lower rung is unchanged

`DamageControlL1Tests.DamageControlL0_DoesNotFlood` (:347): a Breach Order at L0 floods nothing, and the log says "breach not modelled".

## 8. A regression test from what play mode showed

`GunsL1Tests.Mk45_StaysOnItsTarget_UntilItIsGone` (:276) runs the Standard raid and counts what SIX saw on the range: target changes, drop-and-regains within 2 s, and train reversals.
- It asserts 0 regains, and no more reversals than target changes + 2.
- The old code fails it.

## 9. Measured, not asserted

A test named `…_IsMeasured` or `…_AreMeasured` logs the numbers the ROADMAP reports, as a `[GQ]` line, and asserts only invariants. Examples: `GunsL1Tests.GunsOnly_AgainstEightSeaSkimmers_L0AndL1_AreMeasured` (:326) and `RadarL1Tests.Mk45_LaysOnL1Tracks_AndTheirNoiseMovesTheLay_IsMeasured` (:364).

Use this form when a number comes from AUTHORED values and is a measurement, not a validation.

## 10. The engine-free guard

- **The source scan:** `CoreBannedApiTests.CombatCore_UsesNoBannedApis` (:31) scans every `.cs` under `Assets/General Quarters/Core`, with comments and strings stripped.
  - Its self-test is `Scanner_CatchesWhatItShould_AndIgnoresComments` (:53).
  - Copies guard other cores: `Assets/Gunnery/Tests/Editor/GunneryCoreBannedApiTests.cs`, and `Assets/Air Wing/Tests/Editor/CoreBannedApiTests.cs`, whose `Scan` RH Aviator reuses.
- **The compiler:** an asmdef with `"noEngineReferences": true` (`Assets/RH Radar Suite/Core/Detection/RHRadarSuite.Core.asmdef`).
