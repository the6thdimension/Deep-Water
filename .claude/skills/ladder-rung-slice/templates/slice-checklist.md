# <Element> L<n>: slice checklist

Copy this into the session's scratchpad (not the repo), fill the angle brackets, and tick as you go. Each **Gate** must pass before the next block starts. The steps are the `ladder-rung-slice` skill's; the worked examples are in its `references/`.

- Module: `<Assets/General Quarters | Assets/Air Wing | …>`
- Stream and slice: `<Stream A | B | C …, slice id>`
- Rows it builds from: `<G.. – G..>`
- Working branch: `<feat/general-quarters | …>`

## 1. Orient
- [ ] Read the module's `HANDOFF.md`: the hard-won findings and the next steps.
- [ ] Read the ROADMAP item for this rung.
- [ ] Read the BLUEPRINT §4 row and every decision row the item cites.
- [ ] Read the METHODOLOGY entries those rows name.
- [ ] Search MemPalace for `<element>`.
- [ ] Open the scene that runs it (`Examples/Scenes/GQ_Range.unity` or the module's own).
- [ ] Look for prior art in first-party code (Guided Fury is out of scope; a missile is a black box).
- [ ] Every item maps to a **Locked** row: `<list>`.
- [ ] Each choice no row covers has a **Proposed** row and a priced §9 question: `<Gnn, Qn>`.
- [ ] Five or more questions: write a question file first (`concept-doc-interview`).
- [ ] The plan is put to SIX in one paragraph (intent, files, trade-offs), and SIX said go.
- **Gate:** no item rests on an unrecorded choice.

## 2. Safe limits (BLUEPRINT §6 #8)
- [ ] Every item is textbook physics, public cited data, abstract lethality or a generic system.
- [ ] Nothing for real hardware, real targets, engineering-grade lethality, defeat-optimisation, CBRN or a specific real peer.
- [ ] The check is written as a "Safe limits, checked before the build" bullet in the ROADMAP item.
- **Gate:** the check is written.

## 3. Data
- [ ] The level's data struct, beside its core: `<Element>L<n>Data`, `[Serializable]`, with `IsSet`.
- [ ] The loadout or profile SO carries it, and its editor builder writes it with a provenance paragraph.
- [ ] Every number is labelled VERIFIED / MEASURED / DERIVED / CALIBRATED / VALIDATION / AUTHORED.
- [ ] Each CALIBRATED number names its one public figure.
- [ ] Each AUTHORED number says so.
- [ ] More than a handful of numbers: `Calibration~/<ELEMENT>-SOURCES.md`.
- **Gate:** no unlabelled number, and no placeholder in real data.

## 4. Pure core, adapter, tick
- [ ] `<Element>L<n> : I<Element>`, in `Core/`. No `MonoBehaviour`, `Time`, `UnityEngine.Random`, `Physics` or scene objects.
- [ ] The factory resolves through `FidelityLadder.Resolve`.
- [ ] Built without its data, the rung throws `ArgumentException`.
- [ ] Every outcome draw comes from the run's `DeterministicRng`, as a chance root.
- [ ] Outcome state is mixed into the Fix; presentation data is not.
- [ ] `FidelityLadder.HighestImplemented` is raised in this commit.
- [ ] The banned-API guard covers the new files: under `Core/`, or a copied scan, or `noEngineReferences`.
- [ ] The adapter steps the core from `FixedUpdate`, with its `[DefaultExecutionOrder]` checked against its neighbours.
- [ ] A missing world input is logged once, with its default.
- **Gate:** it compiles (`wait_build.sh` on a new class or method name), and the banned-API test is green.

## 5. Tests (`Tests/Editor/<Element>L<n>Tests.cs`)
- [ ] The tolerances are written in the class doc before the first run.
- [ ] Behaviour tests against a closed form, an identity, a hand-worked number or a mirror.
- [ ] Statistics of the seeded draws, if the rung draws.
- [ ] `L<n>_WithoutItsData_RefusesToBuild`.
- [ ] Out-loud fallback: the expected level is updated in `CoreContractTests.Ladder_FallsBackToTheHighestBuiltLevel_AndSaysSo`, and a request above this rung lands here with a `FidelityFallback` log line.
- [ ] Fix pin (`templates/fix-pin-test.cs`): two runs with the same seed and Orders give the same Fix; another seed differs; every chain ends in roots; ≤ 1 ms per fixed step.
- [ ] The lower rung is unchanged (an L0 test at L0).
- [ ] The golden and the L0-meaning tests pin their old levels, if the shipped loadout moves up: `<tests touched>`.
- [ ] `[GQ]`-tagged measurement lines for every number the ROADMAP will report.
- **Gate:** all green. Count before `<n>`, after `<n>`.

## 6. Verify
- [ ] EditMode, every touched assembly: `<GQ a/a, Sea State b/b, RH Radar Suite c/c, Gunnery d/d>`.
- [ ] Any failure present before and after is named.
- [ ] Core cost at the rung's worst case: `<ms>` per fixed step against 1 ms. A speed claim has a `unity-perf-ab` A/B.
- [ ] Play mode on the range: `<what was seen, with numbers>`.
- [ ] The order log was read for a human at the controls.
- [ ] Play was stopped only with SIX's OK.
- [ ] Each claim is labelled verified, build-verified or unverified, and the "Not verified" list is written.
- **Gate:** the evidence is in the commit body.

## 7. Close out
- [ ] ROADMAP: `[x]`, the date the gate closed, the commits, the counts; Results, Found, Safe limits, "Not at L<n>"; a change-log line; the skip / fail log.
- [ ] HANDOFF: the "Last verified" counts, a §2 finding, a §3 verdict row, §4 paths, §5 crib sheet, §6 next steps, glossary.
- [ ] BLUEPRINT: the §4 row says what was built; new rows (Proposed unless SIX picked); a change-log line with the safe-limits check.
- [ ] METHODOLOGY via `methodology-scribe`: `<AP/C ids>`, each citing the commit.
- [ ] Memory: the module's note and its `MEMORY.md` line; MemPalace for verbatim findings.
- **Gate:** every document is committed.

## 8. Commit and level
- [ ] No stage in the shared checkout while another session may level: use the scratch index or a pathspec commit.
- [ ] `git fetch origin`; HEAD is the fresh tip; `git diff --cached --name-only HEAD` lists only this slice's paths.
- [ ] `commit-to-branch` with `--deny 'UnityMcpUnlock|/DAVFX/'`: the `feat` commit, then `docs` if it's separate.
- [ ] `level_branches.sh --protected Guided-Fury --repo the6thdimension/Deep-Water …` with every working branch, never the archive branch.
- [ ] Other worktrees whose branch moved are clean on the changed paths.
- [ ] Hashes and PR link reported: `<feat>`, `<docs>`, `<all branches at …>`, `<PR #>`.
