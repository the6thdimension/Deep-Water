# Document formats for a rung

How each document records a slice, with the real lines to copy. In General Quarters each document has one job, stated in its header:
- `BLUEPRINT.md` says what and why;
- `ROADMAP.md` says what's next and what's done;
- `HANDOFF.md` lets someone pick the module up cold.

## BLUEPRINT: the decision register (§5)

Columns: `| # | Question | SIX's pick | Leaning | Status |`. **Leaning** is Claude's recommendation before the pick (§5's opening line). The register is append-only: a row is never rewritten to a different decision.

**Locked:** SIX picked it. The pick cell starts with the choice in bold, then the detail. If SIX's pick differs from the leaning, the leaning cell keeps Claude's alternative: G55 says "Added weight as forces on the probe columns", and the change log says so ("G55 did not").
```
| G66 | How a look decides "seen" (R6) | **A Swerling 1 Pd draw per look at a fixed Pfa,** from the run's seeded generator, so the Fix replays it (Reckoning R3) | Same | Locked |
```

**Proposed:** Claude raised it; SIX hasn't decided. Say so in the pick cell, give the reason and what was measured, and put the alternative in the leaning cell. Add a priced question to §9.
```
| G70 | Radar L1's clutter processing (2026-10-27, raised while building G67) | **PROPOSED by Claude, not decided:** an AUTHORED clutter improvement factor of 30 dB … Measured both ways in `RadarL1Tests` | 0 dB (G67 to the letter); or Barton and Ward's critical-angle fall-off at Radar L2 in its place | Proposed (Q7) |
```

When SIX answers, the status becomes Locked with the date and the answer, as G29's did: "Proposed by Claude so the TAO seat has a job; confirmed by SIX 2026-10-24 (Q5)". The question is then struck through in §9 with its answer.

**Superseded:** the new row says what it replaces; the old row keeps its text, struck through, and points at the new one. The cleanest example crosses two documents:
- `Assets/Theatre/BLUEPRINT.md:162`, the new row: E13's pick ends "**Supersedes Reckoning R21** ("later")", with the status "Locked; R21 superseded".
- `RECKONING-CONCEPT.md:231`, the old row: R21's pick is struck through, then "**Superseded 2026-10-24 by Theatre E13**", with the status "Superseded".
- Both documents get a change-log line saying the same.

Within one register, append "**superseded by Gnn** (date)" to the old row's status and leave its text alone.

**§9, open questions:** `| Q7 | <the question, with the options and where the number lives> | 2 min |`. The price is SIX's time to answer. Strike a question through when it's answered, and name the rows it became.

**§4, the ladder row:** when a rung is built, add to its cell what was built, the date and the decision ids: "(built 2026-10-27, G61–G68: a generic S-band array, RH Radar Suite's core)".

**Change log:** one dated bullet per slice that states the safe-limits check. For example (`1439145d`): "Stream A built to G61–G68 … Checked against standing constraint 8: a generic radar, AUTHORED numbers, one public range figure, nothing tuned to a real system."

## ROADMAP: the slice item

The rules are at the top of the file: unchecked items are never deleted, only struck through as skipped with the reason; completed items get a date and their commit; every element level ships with EditMode tests.

**Before the build**, the item names its decisions and its steps (from `1439145d`'s diff):
```
- [ ] **Radar L1 + Tracks L1** (decided 2026-10-27 by SIX, G61–G68; nothing built). The slice, in order:
  1. Split RH Radar Suite's pure core into `RHRadarSuite.Core` (engine-free asmdef; the suite's 42 tests still pass) (G61).
```

**After the build**, tick it and carry the evidence:
```
- [x] **Radar L1 + Tracks L1** (decided 2026-10-27 by SIX, G61–G68; done 2026-10-27, 5bdb3493 and 831e2ff6; 113 GQ tests, …; 46/46 RH Radar Suite; 49/49 Sea State). The slice, in order:
  1. [x] Split RH Radar Suite's pure core … The suite's 42 tests still pass; 4 more for what GQ needed …
  - **Results** (headless, background mirror, Editor timings; `RadarL1Tests`, 13): …
  - **Found, and proposed (G70, Q7; not decided):** …
  - **Not at L1:** beam and energy scheduling, false alarms, aspect RCS, …
```

Slice C1's item adds two more blocks to copy:
- **"Gate, set before the first measurement"**;
- **"Safe limits (BLUEPRINT standing constraint 8), checked before the build"**.

Date the item by when its gate closed, not when the code was written (`a289614`: "built 2026-10-24, PhysX gate closed 2026-10-25").

Also:
- A line it made redundant is closed with a pointer: "Tracks L1: … Done with Radar L1 above (2026-10-27)".
- **Change log:** one dated line per slice, ending with the commits.
- **Skip / fail log:** each failure on the way, with what happened, how it showed, the fix and the METHODOLOGY entry. For example, "the first PhysX sinking run crashed the Unity Editor (METHODOLOGY AP23)".

## HANDOFF: what a slice changes

From `84fb04b`'s diff:
- **"Last verified" line:** append the slice's counts ("After Stream C slice C1 (Damage Control L1, flooding): 100/100 and 49/49").
- **§1, what this is:** a sentence for what the ship now does.
- **§2, hard-won findings:** a numbered block for the slice, with each finding citing its METHODOLOGY entry.
- **§3, verdict ledger:** a row with the verdict (CHAMPION, ITERATE, FAILED, SHELVED, PROMISING-NOT-PROVEN, or unverified). The evidence column states what is AUTHORED.
- **§4, what is where:** rows for the new folders and tools.
- **§5, crib sheet:** the expected test counts and run time, and how to exercise the rung on the range.
- **§6, next steps:** strike the done item through and name what follows it in the stream.
- **§7, standing preferences:** the register's range ("G1–G60").
- **Glossary:** the slice's terms in plain words.

## The commit messages

- **`feat(<module>): <rung>, <what it adds> (Stream X slice Y)`**, as in "feat(general-quarters): Damage Control L1, compartments and flooding (Stream C slice C1)". The body says:
  - **Problem:** what L0 couldn't do, measured if possible ("Guns alone killed 0 of 8 sea-skimmers");
  - **Change:** with the decision ids;
  - **Verification:** with measured counts;
  - **Not verified:** what wasn't run or checked.
- **`docs(<module>): …`** for the ROADMAP, HANDOFF and BLUEPRINT close-out when it isn't in the feat commit (`1439145d`, `a289614`).
- **`docs(methodology): …`** for lessons, each with its evidence (`10902d3`).
- **`fix: …`** for a repair. It says which commit it repairs and how the repair was checked (`d93e245f`: "git diff 1857e7f5 <this> lists only Assets/_Archive paths").
- Every message ends with the attribution trailer the environment gives.
