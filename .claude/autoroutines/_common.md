# Autonomous Development Routine — Common Spec

This file is loaded by every module-specific routine. It defines the shared loop,
quality gate, on-failure policy, and commit format.

## Required reading at start of each iteration

In order:

1. `CLAUDE.md` — project rules (Prime Virtues, non-negotiables).
2. `METHODOLOGY.md` — established Principles (P1–P7) and Patterns (Pat1+).
3. This file (`.claude/autoroutines/_common.md`).
4. Your module-specific spec (`.claude/autoroutines/{missile|radar|aviator}.md`).
5. Your module's `ROADMAP.md` at `Assets/<Module>/ROADMAP.md`.

Do not skip these. They constrain the work.

## The loop

```
loop:
  branch = "auto/<module>-dev"
  git checkout $branch || git checkout -b $branch
  git pull --rebase origin $branch || echo "first push"

  task = pick_topmost_unchecked_from_roadmap()
  if task == null:
    write_to_BLOCKED("ROADMAP exhausted at $(date)")
    exit 0

  do_the_work(task)        # write code, tests, docs

  run_verification_gate()  # see below
  if green:
    git add -A
    git commit -m "<commit msg per format below>"
    git push origin $branch
    check_off_in_roadmap(task)
    continue loop
  else:
    git reset --hard HEAD   # revert all changes
    append_to_FAILURES(task, gate_log_excerpt, what_was_tried)
    continue loop   # next iteration picks a DIFFERENT task or a different angle
```

You iterate UNTIL the verification gate passes — there is no time cap. If you cannot
land green after 5 attempts on different tasks, stop and write to
`Assets/<Module>/BLOCKED.md` with a multiple-choice question for the human operator.

## Verification gate

```powershell
pwsh tools/ci/run-checks.ps1 -Module <Module>
```

Exit 0 = green, anything else = red. The script runs:

1. Unity batchmode compile
2. EditMode tests (filter to your module's tests)
3. PlayMode smoke (loads your module's smoke scene, ticks N seconds)

If the harness itself is broken (e.g. `unity-path.txt` missing on a fresh clone),
that's a BLOCKED condition — do NOT modify the harness to skip checks. Write to
`BLOCKED.md` and stop.

## Commit format

```
<module>(auto): <subject — imperative, under 70 chars>

<body — 1–3 paragraphs explaining what changed and why. Reference the ROADMAP item.>

ROADMAP-item: <copy of the task line you ticked off>
Routine-iteration: <ISO 8601 UTC>
Verification: compile=PASS, editmode=PASS, smoke=PASS|SKIP
```

Example:
```
radar(auto): build RadarState struct + bake from RadarProfileSO

Lays the foundation for L0 — gives every scanner a universal interchange struct (C4).
Mirrors the Guided Fury shape: SO authors data, Bake() produces an unmanaged copy.
No behavior yet; next iteration adds the IPhysicsScanner interface.

ROADMAP-item: State struct + profile struct
Routine-iteration: 2026-06-08T02:14:00Z
Verification: compile=PASS, editmode=PASS, smoke=SKIP
```

## Non-negotiables (from CLAUDE.md, re-asserted here)

- No magic numbers in code. Tuning values go in `ScriptableObject`s.
- All sim/physics on FixedUpdate. Never `Update`.
- No `FindObjectOfType` in hot paths.
- No silent failures. Every catch must log or rethrow.
- No vendor edits. Wrap, don't mutate.
- Test colocation: EditMode tests live in `Assets/<Module>/Tests/Editor/`.
- Methodology updates: when a non-trivial decision is made, append a candidate to
  `METHODOLOGY.md` (under `Candidates` or `Anti-patterns`).

## On-failure policy

When the gate fails:

1. **Revert.** `git reset --hard HEAD`. No half-landed work on the branch.
2. **Log.** Append to `Assets/<Module>/FAILURES.md`:
   - Task line attempted
   - First 40 lines of the failing log
   - What the routine thinks went wrong (one paragraph)
3. **Diverge.** Next iteration picks a DIFFERENT task. Do not retry the same task
   3+ times in a row without a structural change to the approach — that's a sign the
   roadmap item is mis-scoped and needs a human.

After 5 consecutive failures (across different tasks), write the run summary to
`BLOCKED.md` and stop.

## What "until green" means precisely

"Iterate until verification passes" applies to **per-iteration commits**, not the
session. Each successful iteration commits green code and moves to the next task.
Stop when:

- ROADMAP is exhausted → write to BLOCKED + exit 0.
- 5 consecutive failures → write to BLOCKED + exit 1.
- The harness itself is broken → write to BLOCKED + exit 2.

## Branch hygiene

You only touch `auto/<module>-dev`. Never push to `main`. Never touch the other
modules' branches. If you need a change in `RH Shared/` (a new contract), commit it
to your branch and document the cross-cut in your commit body — the human operator
will fan it to other modules.

## Tools you have

- All Bash, Read, Edit, Write, Grep, Glob, Task tools.
- Unity batchmode via `tools/ci/run-checks.ps1`.
- Git for commit/push to the dev branch.
- You do NOT have access to the user's local Unity Editor GUI. Scene/prefab edits
  happen via YAML edits or editor scripts you write.
