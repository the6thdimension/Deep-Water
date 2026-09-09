# Deep Water — Project Directive for Claude

## The Goal

Build **efficient, optimized, high-fidelity physics-based simulations** in Unity for:

- Military vehicles (naval, air, ground)
- Weaponry and ballistics
- Sensors (radar, EO/IR, sonar)
- Combat systems and engagements
- Behavioral / AI modeling
- Environmental effects

Fidelity without performance is unshippable. Performance without fidelity is uninteresting. The job is both, at production quality.

## The Meta-Goal — McMahon Simulation Methodology

Deep Water is also the **proving ground for the McMahon Simulation Methodology** — a codified framework of virtues, principles, patterns, and anti-patterns intended to become the standard reference ("the new bible") for military 3D simulation development in Unity.

This means every session has two outputs: the sim itself, and contributions to that methodology. Practically:

- **[METHODOLOGY.md](METHODOLOGY.md) is a living document.** Append to it as principles emerge from real work. Don't wait for a dedicated "methodology" session.
- **When making a non-obvious decision, articulate the underlying rule.** If it generalizes, it's a candidate principle — note it in `METHODOLOGY.md` under `Candidates` (or `Principles` if it's already proven across systems).
- **When rewriting legacy code, diagnose before prescribing.** Name *why* the old pattern failed in methodology terms; that diagnosis becomes an entry under `Anti-patterns`.
- **Ground every principle in a real case.** Reference the file, scene, or commit that exercised it. Abstract principles without provenance get demoted to `Candidates`.
- **Prefer concrete and falsifiable** ("Tuning values live in ScriptableObjects") over vague ("write clean code"). Vague principles get rewritten or removed.

---

## Repo Map (read this first)

### First-party systems (yours)

| Folder | What it is |
|---|---|
| `Assets/Guided Fury/` | Missile framework — `Core/`, `Modules/`, `ScriptableObjects/`, `Prefabs/`, `Examples/` |
| `Assets/JMAC/` | AI / sensor / decision systems — `ATLAS/`, `SAURON/`, `Agents/` |
| `Assets/RH Testing Suite/` | Test framework — `Core/`, `Editor/`, `Examples/` |
| `Assets/RH Utilities/` | Scene management, validation (`RH_SceneManager`, `SceneValidationSystem`) |
| `Assets/rim-162essm/` | ESSM SAM (specific weapon implementation) |
| `Assets/_AIRCRAFT/`, `_GROUND UNITS/`, `_ENVIRONMENT/` | Unit & environment assets |
| `Assets/Scripts/` | Older controllers — Missile, Radar, Destroyer, FighterJet, Helicopter, Aircraft handlers |

### Scenes (`Assets/_SCENES/`)

- `CVN-78 FORD` — Gerald R. Ford carrier
- `DDG Engagement` — Arleigh Burke destroyer SAM engagement
- `Dogfight` — fixed-wing air combat
- `Helo` — helicopter
- `Rocket`, `Rocket Man` — rocketry / ballistics
- `SENTINAL` — (sentinel scenario)
- `Mapping`, `OutdoorsScene` — terrain / environment

### Vendor / third-party (read-only by default)

`RealisticCarControllerV3`, `NWH`, `Obi`, `RootMotion` (PuppetMaster/FinalIK), `SensorToolkit`, `HurricaneVR`, `Cesium`, `ML-Agents`, `FlightSimLite`, `VFX Arsenal`, and others under `Assets/Plugins/`.

**Do not modify vendor code unless explicitly asked.** Default is to wrap, extend, or compose around it. If a vendor change seems necessary, surface it and ask before editing.

---

## Prime Virtues (above all else)

Every change is judged against these, in this order when they conflict:

1. **Maintainability** — Read far more than written. Leave the codebase easier to change than you found it. Clear names, small focused units, obvious control flow, no hidden coupling.
2. **Extensibility** — New vehicles, weapons, sensors, effects should slot in by *adding* code, not rewriting it. Composition over inheritance. Data-driven seams designed in from the start.
3. **Reliability** — Predictable across runs, frame rates, hardware, load. Determinism where it matters (ballistics, scoring, replays). Honest error handling — no silent failures, no swallowed exceptions, no "it usually works."
4. **Testability** — Logic must be reachable by tests. Pure functions over hidden state. Separate simulation logic from `MonoBehaviour` lifecycle where possible. Dependencies injected, not summoned via singletons or `FindObjectOfType`. If something can't be tested, that's a design defect — fix the design.
5. **Performance** — Real-time, high-fidelity sim is the target. Be relentless about: per-frame allocations, GC pressure, `Update`/`FixedUpdate` hot paths, `GetComponent` in loops, physics query counts, draw calls, cache locality. Measure with the Profiler before optimizing — but don't be carelessly wasteful by default.

### Supporting virtues

Correctness, clarity, simplicity, cohesion & locality, robust boundaries (validate input/IO/network — trust internal invariants), deletability, consistency with existing patterns.

---

## How to Build Things Here

### New simulation systems must:

- **Be ScriptableObject-driven.** Tuning values, weapon profiles, sensor specs, vehicle stats, behavior parameters — all live in SOs. No magic numbers in code. Designers and you should both be able to dial systems without recompiling.
- **Run on `FixedUpdate` for physics/ballistics/sensors.** Frame-rate independent. Reproducible. If two runs at different framerates produce different engagement outcomes, that's a bug.
- **Use Burst / Jobs / ECS for hot paths** where it pays off — many-entity physics, sensor sweeps, ballistic trace, swarm AI. Don't force DOTS on small systems; the cost is complexity.
- **Follow the standard layout** for new modules:
  ```
  SystemName/
    Core/              — interfaces, base types, the simulation logic
    Modules/           — pluggable behaviors / variants
    ScriptableObjects/ — configs, profiles, presets
    Prefabs/           — runtime assemblies
    Examples/          — minimal demo scene & sample configs
  ```
  This mirrors `Guided Fury/`. Deviate only with a stated reason.

### Before any non-trivial change, Claude will:

1. **Read the relevant scene** (`.unity` file) to see what's actually wired up. The Inspector configuration usually matters more than the code in isolation.
2. **Check first-party systems for prior art** — `Guided Fury`, `JMAC`, `RH Utilities`, `Scripts/` — before writing something new. Reuse or extend if it fits.
3. **Confirm the plan with me.** A short paragraph stating intent, files to touch, and trade-offs. Wait for go-ahead on non-trivial work.

### Legacy first-party code

The existing code under `Guided Fury`, `JMAC`, `RH*`, `Scripts/` is a **starting point, not a template**. It predates current standards. Treat it as:

- Valid functional context — read it, understand what it does, keep it working.
- Not authoritative for new patterns. When a legacy convention conflicts with the standards above, the standards win.
- **When a file needs changing, default to rewriting it to the current standard** rather than patching the legacy pattern in place. A one-line fix that perpetuates a bad pattern is a worse outcome than a small rewrite that fixes it. Before rewriting:
  - Summarize what the existing code does and what's wrong with it (teach the change, don't just make it).
  - State the scope of the rewrite (this file, this class, this module) so I can keep it bounded.
  - Confirm with me before proceeding if the rewrite touches more than the file in front of you.
- If the rewrite would sprawl beyond a reasonable scope, stop and ask — don't either (a) shrink it back to a bad-pattern patch or (b) silently expand it.

---

## Working Style with Me

- **Teach as you go.** Explain what code is doing, what changed, how a system works, why a Unity API behaves the way it does. I want to understand, not just receive output. Compare old vs. new when refactoring.
- **Comments are encouraged** when they explain *what* a non-trivial block does or *why* a non-obvious choice was made. I'd rather have a clear comment than re-derive the intent from the code six months later. Skip them for code that's truly self-explanatory.
- **Be proactive with suggestions.** If you see a better approach, a Unity gotcha I might hit, a performance trap, a design improvement, or something I haven't considered — say it. Don't be shy. Frame it as a recommendation, not a fait accompli, and let me decide.
- **Be honest about uncertainty.** If you're not sure how a vendor system behaves or whether a change is safe, say so and propose how to verify (read the source, write a quick test, run the scene).
- **Profile before claiming a perf win.** "Should be faster" is not a perf claim. Numbers from the Unity Profiler are.
- **No silent shortcuts.** If you'd have to violate a Prime Virtue to ship something quickly, surface the trade-off before taking it.

---

## Non-Negotiables

- Don't modify vendor code without permission.
- Don't introduce silent failures, swallowed exceptions, or empty catch blocks.
- Don't put tuning values in code — they go in ScriptableObjects.
- Don't break determinism in physics/ballistics/sensor paths.
- Don't add features, abstractions, or refactors beyond the task. Ask first.
- Don't claim a UI/scene change works without actually loading it in the Editor (or saying explicitly that you couldn't verify).
