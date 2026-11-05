# POSEIDON Reckoning

The deterministic core of POSEIDON. Give it the same scenario, the same build and the same
Orders, and it reaches the same state, bit for bit, on every tick. Replay, save and load, "what
if", and finding the exact tick two runs parted all follow from that.

This is phase 0: the core and its tests, with no game in it yet. It is pure C#, with no engine
references (the compiled assembly references only `netstandard`), and nothing else in the project
uses it yet.

## Vocabulary

| Word | What it is | In code |
|---|---|---|
| **Chronometer** | The one clock: an integer tick counter. A tick is 20 ms (50 Hz). | `Chronometer` |
| **Watch** | A system's rate: it runs every Nth tick. | `Watch.EveryTicks(n)` |
| **Written order** | The order systems run in within a tick: the order they were registered. | `Register` |
| **Order** | A command, stamped with its tick and issuer. The only way to change the world from outside. | `Order`, `ReckonedWorld.Issue` |
| **Log** | Every Order, plus the scenario hash, seed and build id. | `OrderLog` |
| **Event** | Something a system scheduled for a later tick. Ties break on (tick, phase, source system, entity, sequence). | `ScheduledEvent`, `EventScheduler` |
| **Fix** | The fingerprint of the state: FNV-1a 64 over raw bits, per system and for the world. | `FixHasher`, `FixTrace` |
| **Fix-diff** | Names the first tick and system where two runs differ. | `FixDiff.Compare` |
| **Draw** | One named random number, computed from (seed, rule, entity, tick, index). No generator state. | `DrawId`, `ReckonedRandom` |
| **Snapshot / Save** | System and scheduler state as bytes. Save = snapshot + Log. | `SaveSnapshot`, `ReckoningSave` |
| **Cause record** | Why a notable thing happened: tick, rule, numbers, links. A stub for Inquiry later. | `CauseRecord` |

## What one tick does

Tick T always runs in this order:

1. Orders stamped T, by (issuer, sequence).
2. Events due on T, by their key.
3. Systems due on T, in the written order.
4. The Fix for T.

## Registering a system

Implement `IReckonedSystem`. All outcome-changing state goes through `WriteFix` and `SaveState`.

```csharp
public sealed class Wind : IReckonedSystem
{
    private double _speed;

    public uint SystemId => 1;          // stable, above 0
    public string Name => "Wind";

    public void ApplyOrder(in Order order, TickContext context) => _speed = order.OpenPayload().ReadDouble();
    public void OnEvent(in ScheduledEvent scheduledEvent, TickContext context) { }

    public void Step(TickContext context)
    {
        // Rule 10, entity 0, draw 0: the name alone is enough to recompute this number later.
        _speed += 0.01 * context.DrawNormal(10, 0, 0);
    }

    public void WriteFix(ref FixHasher hasher) => hasher.AddDouble(_speed);
    public void SaveState(StateWriter writer) => writer.WriteDouble(_speed);
    public void RestoreState(StateReader reader) => _speed = reader.ReadDouble();
}
```

Then build the world. The registration lines are the written order:

```csharp
var log = new OrderLog(scenarioHash, scenarioSeed, buildId);
var world = new ReckonedWorld(log);
world.Register(new Wind(), Watch.EveryTicks(5));
world.Register(new Ships(), Watch.EveryTicks(1));

world.Issue(tick: 100, issuer: 7, targetSystem: 1, kind: 1, payload);
world.StepTo(1000);
```

Reference the `POSEIDON.Reckoning` assembly from your asmdef; it is not auto-referenced, so nothing
depends on it by accident.

## Rules for reckoned code

The guard test (`BannedApiGuard`) reads the compiled IL of `POSEIDON.Reckoning` and fails on:

- `UnityEngine.Time`, `Random`, `Physics`, `Physics2D`, `MonoBehaviour`, `Object.Find*`;
- `DateTime.Now`, `UtcNow`, `Today`, `DateTimeOffset.Now`, `UtcNow`, `Environment.TickCount`, `TickCount64`, `Guid.NewGuid`;
- `System.Random`, `Stopwatch`, `Thread`, `ThreadPool`, `Task`, `Parallel`;
- `Math`, `MathF` or `Mathf` `Sin`, `Cos`, `Tan`, their inverse and hyperbolic forms, `Exp`, `Pow`, `Log`, `Log10`, `Log2`, `Cbrt`;
- enumerating a `Dictionary` or `HashSet` (keyed lookup is allowed).

Use `ReckonedMath` where a banned function is needed. It holds only `Log` and `InverseNormalCdf`
so far; trig comes with the first rule pack that needs it.

**What the guard cannot see:** calls made through reflection, and reference types named only in a
signature or a local with no member touched. It reports `calli` and any token it cannot resolve as
violations rather than skipping them.

## What is not enforced yet

- **"Orders are the only way state changes" is a rule, not a wall.** `Issue` and the Log are the
  only input path the core offers, and a system gets no reference to the world. But C# cannot stop
  code holding a reference to a system from changing it. The Fix exposes that kind of change when
  two runs are compared; it does not prevent it.
- **Systems cannot issue Orders yet.** Orders come from outside; a system that wants something later
  schedules an event. AI captains will need a decision here: if a deterministic captain's Orders are
  logged and also recomputed on replay, they apply twice.
- **State missing from both `WriteFix` and `SaveState` is invisible.** Restore checks the Fix, so it
  catches state the Fix covers but the save forgot. Nothing catches state left out of both.

## Test results

Run 2026-11-05: Unity 6000.4.11f1, EditMode, batchmode on the Deep Water project (branch
`feat/poseidon-reckoning`, from `main` at `368504b6`). **71 of 71 passed**, 0 failed, 0 skipped, in
7.9 s. Everything below is **verified** on that setup: one machine, Windows x64, Editor Mono.

| Case | Result |
|---|---|
| (a) Two runs, 10,000 ticks | Identical on every tick; final Fix `3DCFE134307A33AA`; 125 Orders, 200 events fired |
| (b) Snapshot at 5,000, restore through bytes, replay | Identical over 5,000 ticks; same final Fix; save is 6,628 bytes |
| (c) Planted desync of 1e-9 | Named exactly: tick 6789 ToyParticles, tick 6790 ToyWind, tick 6800 ToyBeacon |
| (d) Skip-ahead vs tick by tick | Identical over 10,000 ticks in both worlds; the sparse world ran 252 ticks instead of 10,000 |
| (e) Draws and call order | Forward, backward and interleaved with unrelated draws: identical |
| (f) Guard on this package | 34 types, 163 method bodies, 1,094 member references, 0 violations |
| (f) Guard on planted bad code | All 19 samples caught by name, including inside a lambda, a generic and a static constructor |

Also pinned:

- FNV-1a 64 and SplitMix64 against published vectors.
- One draw against a value computed separately in Python (`0x5275C1AD2D00A447`).
- The toy's final Fix, which came out the same in a second Editor process.
- `ReckonedMath.Log` against `Math.Log`: worst scaled error 2.55e-16 over 20,010 values.
- 200,000 normal draws: mean -0.00058, variance 1.00268, P(|z| > 2) 0.04638 (ideal 0.04550).

**Tick cost of the toy** (8 particles, 3 systems, no trace): 0.011 and 0.015 ms per tick in two runs,
each a single Stopwatch timing of 9,999 ticks in the Editor. That is roughly 1,300x to 1,750x real
time for the toy. It is not a Profiler measurement and says little about a real world.

**Not tested:** another machine, another CPU, a Player build, IL2CPP, Burst. Those are where floating
point could still differ. The golden Fix above is the first number to compare there.

## Running the tests

Embedded packages compile their tests with no manifest entry. With the Editor closed:

```bash
Unity.exe -batchmode -nographics -projectPath "<project>" -runTests -testPlatform EditMode -assemblyNames POSEIDON.Reckoning.Tests -testResults results.xml -logFile editor.log
```

Measured numbers appear in the log, tagged `[RECKONING]`. The bad-code samples use Unity's physics
module, so the host project needs `com.unity.modules.physics`.
