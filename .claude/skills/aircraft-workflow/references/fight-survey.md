# The fight survey: judging a fight before and after a rule change

**Job:** fly the same set of fights on the code before a change to the fight and on the code after it, and say what moved. Slice 3f (the fight in the vertical, A11) is judged this way. Everything here is headless: no scene, no play mode.

## Two scores, one diagnostic

| Measure | What counts | Where |
|---|---|---|
| **Position cone time** (the score) | Seconds an attacker sits inside 30° of an enemy's tail within 1 km, wherever it points (W46; A9 declined by SIX 2026-10-27) | `RearCone.Inside`, `Engagement.SecondsInCone`, `Engagement.SideSeconds` |
| **Pointed cone time** (the diagnostic) | The same, and the attacker's flight path within 30° of the line to the target: the check A9 proposed, which c1501942 took out of `RearCone.Inside` | `Pointed` in `templates/fight-survey.cs` only. Never put it back in the core: SIX declined it as the score |
| **Kills** (guns hot) | Enemies a side shot down (K6, A12: kills first) | `Engagement.SideKills` |

Position minus pointed is mostly head-on-pass time. A change that raises position time and not pointed time did nothing for the fight. Pointed time is always inside position time, so the share is at most 100 %.

## The runners

| Runner | Scores | Starts | How to run |
|---|---|---|---|
| `templates/fight-survey.cs` | position, pointed, kills (guns hot), the lowest height, fights under the floor, crashes; the 4 v 4's closest pass | `Starts` 0, 1 or 2 (below) | `Unity_RunCommand` with `__OUT__` set; `wait_file.sh <out> 900 '^# end'`. Not yet run (written 2026-10-27; it compiles against the project's assemblies) |
| `FightTests.Survey` (`[Explicit]`) | position only, plus 1 v 2, 2 v 2 and the 4 v 4 two ways; logs `[Aviator survey]` | 0 | The Test Runner by name: unity-mcp-workflow's `run-tests.cs` with `testNames = new[] { "DeepWater.Aviator.Tests.FightTests.Survey" }` in the `Filter` (an explicit test runs only when named). A clean scene. About 55 s on 2026-10-27 |
| `FightTests.TheAce_BeatsTheRookie_InAtLeast15Of20NeutralMerges_AndNobodyCrashes` | position; slice 3d's pass criterion; logs `[Aviator 3d] the ace won` | 1 | any test run, or `templates/run-direct.cs` |
| `GunFightTests.GunsHot_TheAce_BeatsTheRookie_ByKillsThenCone_InAtLeast15Of20_AndNobodyCrashes` | kills, then cone; logs `[Aviator guns]` | 2 | any test run |

**Starts** (every merge: head on, 6 km apart, 9,000–11,000 ft, offset 200–500 m to one side, 180 s):
- **0**, `FightTests.Survey`: seed 7000 + i; 400 kt; blue from the south.
- **1**, `FightTests.Fly` (the 3d pass test): seed 5000 + i; 380–420 kt; which side the ace starts on is drawn.
- **2**, `GunFightTests.Fly`: seed 5000 + i; 380–420 kt; blue from the south; `gunnerySeed` 900 + i.

The 4 v 4 is `FightTests.Survey`'s: four line pilots blue (aircraft 1 is the player's jet), red rookie, line, line, ace from 20 km, red hunting aircraft 1, 300 s, no formation orders.

## The procedure

1. **Fix the criteria in the ROADMAP item before building** (W50, C42). Slice 3f's (A11): line v line and ace v line each produce a winner in some of 20 merges by pointed cone time; no fight sinks to the floor (the yo-yo failure of slice 3d, fights down to 700 ft). Write the floor as a number then: the pilots' `FightFloorM` is 300 m, and `SinkMarginM` adds any margin the criteria name.
2. **Before.** On the unchanged code: `rm` the old report in its own shell command, set `__OUT__`, send the template, wait for `# end`. A domain reload during the run (a compile, play mode) leaves no `# end`: wait 30 s for the heartbeat to lapse and send it again.
3. **Calibrate.** With `Starts` 0 and guns safe, the position columns must equal `FightTests.Survey`'s `[Aviator survey]` lines on the same code. With `Starts` 1, ace v rookie must equal the 3d pass test's `[Aviator 3d] the ace won` line. If either differs, the template has drifted from the tests: fix it before reading the pointed columns.
4. **Change one rule.** Wait for a new field or method in the build (aircraft-workflow trap 2), and re-run the slice's own tests.
5. **After.** The same template, the same `Starts`, `N`, `GunsHot`, `SinkMarginM` and matchups. Other starts make another survey, not an "after".
6. **Report** both tables in the ROADMAP item, before beside after, with the commit of each: per matchup the wins by position and by pointed, the mean seconds a fight both ways, the pointed share, the lowest height, the fights under the floor, crashes, and kills when guns are hot. Name the runner and starts behind every number. "Promising, not proven" is a valid verdict; never re-run with other starts until one passes.

## The before numbers (2026-10-27, from the ROADMAP's "Next: slice 3f")

| Matchup | Runner (starts) | Position: wins; s a fight (blue / red) | Pointed (what the score read while A9's check was in) |
|---|---|---|---|
| line v line | `FightTests.Survey` (0) | 14–6; 6.1 / 5.6 | 0 / 0 |
| ace v line | `FightTests.Survey` (0) | 14–6; 8.5 / 7.0 | 0 / 0 |
| ace v rookie | the 3d pass test (1) | 19 of 20; 1,278 s to 60 s over 20 | 1,217 s to 0 |
| 4 v 4, the player's jet fighting | `FightTests.Survey` 4 v 4 | 63.4 / 88.4 s | 2.8 / 41.9 s |

The pointed column is a prediction for the template's first run: the "was" numbers are what `RearCone.Inside` scored with the pointing check, and the gun (3e34e232) left the guns-safe fight unchanged (the pass test logged the same 19 of 20, 1,278 / 60 s before and after it). The first run confirms or refutes this.

Mind the mix: the ace v rookie row comes from the pass test, not the survey. `FightTests.Survey`'s own ace v rookie logged 18–2, 921 / 71 s on the same day (Editor.log). Compare like with like.

## Open

- **The survey's 4 v 4 closest pass reads 0 m and 1 m** (`HANDOFF-2026-10-27-OVERNIGHT.md`), not investigated; the final 4 v 4 test reads 146 m. The template measures it the same way, so it will show the same. Look before trusting a 4 v 4 row's closest pass.
- `templates/fight-survey.cs` was compiled outside Unity (Unity's bundled Roslyn, against `Library/ScriptAssemblies` and the Editor's assemblies, wrapped in RunCommand's namespace); it has not yet been run in the Editor.
