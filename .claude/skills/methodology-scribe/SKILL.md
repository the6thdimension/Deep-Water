---
name: methodology-scribe
description: "Format and append a new entry to the McMahon Simulation Methodology (METHODOLOGY.md) or the Guided Fury ROADMAP.md known-issues ledger. Use whenever a non-obvious decision, a rewrite diagnosis, a recurring bug pattern, or a proven cross-system pattern emerges from real work and should be recorded — an anti-pattern (AP), a principle/candidate pattern (Pat), a dated changelog line, or a tracked follow-up. Enforces the format, provenance, and concrete-and-falsifiable rules."
allowed-tools: Read, Edit, Bash
---

# Methodology Scribe

`METHODOLOGY.md` (repo root) is a LIVING document — append to it as principles emerge from real work, don't wait for a dedicated session. This skill keeps entries consistent.

## When to write what

- **Anti-pattern (AP#)** — a way the old/wrong code failed. Write one when you rewrite legacy code or catch a recurring trap. Name *why* it fails in methodology terms.
- **Principle / Candidate Pattern (Pat#)** — a rule that generalizes. New rules start under `Candidates` (or as "candidate PatN"); promote to a full Principle only after it's proven across **two** systems/subsystems.
- **Changelog line** — a dated `- **YYYY-MM-DD** — …` bullet at the end of the log recording what landed and why.
- **Tracked follow-up** — a real problem you won't fix now. Goes in the `ROADMAP.md` known-issues ledger as `- [ ] **Title.** …`, not METHODOLOGY.

## Format rules (enforce these)

1. **Ground every entry in a real case.** Reference the file, scene, commit, or test that exercised it. Abstract principles without provenance get demoted to Candidates. Example: "*Where seen:* `MissileCameraController` chase view; ESSM prefab root scale (65,65,100)…".
2. **Concrete and falsifiable** over vague. "Tuning values live in ScriptableObjects" not "write clean code." "throws ~120 m/s at point-blank, nothing at 38 m" not "blast works."
3. **AP entries** carry: *Where seen*, *Why it fails*, *Do instead*, *Reference impl* (path).
4. **Convert relative dates to absolute** (session date), and use plain ASCII dashes, no em-dashes, in files that Python may post-process.
5. Keep numbering monotonic. Current high-water marks as of 2026-09-08: anti-patterns through **AP10**, candidate patterns through **Pat5** (Pat1 SO-Profile+Pure-Core+MB-Adapter is proven/load-bearing; Pat3 orthogonal subsystems, Pat4 authored-missile-as-editor-command, Pat5 sim-on-FixedUpdate-render-interpolated are candidates).

## Scope discipline

When a rewrite is needed: summarize what the old code does and what's wrong with it (teach the change), state the scope (file/class/module), and confirm before exceeding a single file. Don't shrink a needed rewrite back into a bad-pattern patch, and don't silently expand it.

## How to append

Read the tail of `METHODOLOGY.md` (or the ledger section of `Assets/Guided Fury/ROADMAP.md`), match the existing bullet style exactly, and append. For multi-line entries with backslashes/paths, write via a small Python script to a scratchpad file and run it (triple-quoted strings with `C:\...` paths and em-dashes break inline `python -c`) rather than fighting shell quoting.
