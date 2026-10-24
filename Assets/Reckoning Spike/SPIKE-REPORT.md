# Reckoning physics spike — report

**Status: SPIKE. Throwaway code that settles two questions; not the Reckoning core.**
Run 2026-10-23 20:14, Unity 6000.4 Editor, Unity Physics 1.4.2, EditMode tests in
`Tests/ReckoningSpikeTests.cs`. Every number below is copied from that run's `[SPIKE]` log
lines. Each claim is labelled **verified** (measured here), **build-verified** (it compiled
or the binary shows it), or **unverified** (reasoned, not measured).

## In plain language

Reckoning needs a physics core that gives the same result every run. That makes a replay,
a "what if", and a Board of Inquiry possible. Two things could have broken that plan:

1. **Can reckoned code use Unity Physics at all?** The plan was to keep reckoned code
   "engine-free", so it could never call into the parts of Unity that are not repeatable.
2. **Does Unity Physics move a ship correctly?** A ship in water is heavier in some
   directions than others. Pushing it sideways or up means pushing water too; pushing it
   along its length barely does. This extra weight is called *added mass*. Unity Physics
   gives a body one mass for every direction.

The spike built the same small world two ways and tested both against four pass criteria:

- **Variant A.** Unity Physics moves the ships. The direction-dependent added mass has to
  be averaged into its single mass.
- **Variant B.** Our own integrator moves the ships, with the correct added mass per
  direction. Unity Physics only finds contacts.

**Verdict: use variant B.** It passed all four criteria. A missed the roll-period
criterion: 10.4 % against a 10 % limit. The miss is built into A's design, not a tuning
problem. "Engine-free" turned out to be the wrong guard. Reckoned code cannot be
engine-free, because Unity's own containers and jobs live in the engine's core module. The
guard should ban specific APIs instead. The spike also found a defect in Sea State's
buoyancy: today's hulls roll about 1.5× too fast.

## Results

| | Criterion | A · Unity Physics moves ships | B · own integrator + UP contacts |
|---|---|---|---|
| C1 | Same Orders → same Fix, 10,000 ticks × 2 runs, rough sea, a collision | **PASS**: identical every tick; 274 contact ticks; max overlap 0.000 m; Fix `81697D49DD1BEA9F` | **PASS**: identical every tick; 65 contact ticks; max overlap 0.111 m; Fix `86211BC9983C6DEE` |
| C2 | Roll period within 10 % of the IMO formula (9.72 s, GM 2.44 m) | **FAIL**: 10.73 s, +10.4 % | **PASS**: 9.46 s, −2.7 % |
| C3 | Tick ≤ 0.6 ms (16× real time), 4 ships, 414 probes, 64 wave components | **PASS**: 0.204 ms (≈ 98× max speed) | **PASS**: 0.189 ms (≈ 106×) |
| C4 | Compiles engine-free, or the bridge is small | Strict form impossible for any job code (both variants); with the engine core module allowed, the core module is the only engine module referenced | same assembly |

All rows are **verified** except C4's explanation, labelled below.

### C1 — determinism (verified, with limits)

Both variants produced bit-identical state on every one of 10,000 ticks across two runs.
The run was a Douglas 6 sea plus a 3 m swell, with an 11 m RHIB driven into a 154 m
destroyer's beam at 6 m/s. The Fix is FNV-1a over the raw bits of every ship's position,
rotation and velocities, so "identical" means identical bits, not merely close.

Contact quality differs. A's solver allowed no overlap. B uses a soft spring-damper
contact (0.25 s contact period, damping ratio 0.6, both authored) and let the hulls overlap
by up to 0.111 m. B's contact response is its weak spot.

**Not tested:** another machine, a Player build, IL2CPP, a different CPU. Unity Physics
with Burst's deterministic float mode is designed for cross-platform determinism; this
spike verified the same build on the same machine only.

### C2 — roll period (verified)

The test heels a 154 × 20 × 12 m box hull (draft 6.3 m) by 5° in calm water, lets go,
and times the upward zero crossings. The yardstick is the IMO weather criterion (2008 IS
Code, Part A 2.3), T = 2CB/√GM. It is an empirical fit to real ships, so it says whether
a hull rolls at a ship-like rate, not what the exact answer is.

- **B** measured 9.46 s. The ideal box model with B's roll added inertia (0.15) predicts
  9.27 s, so the integrator itself is within 2 %.
- **A** measured 10.73 s. It has to average the added inertia over all three axes, which
  gives 0.48 instead of roll's 0.15, so the hull rolls as if it carried three times the
  water it does. The ideal model with 0.48 predicts 10.53 s.

Why this is structural: A has one mass and one inertia scale. Tuning it to roll correctly
(0.15) would drop heave's added mass (0.8). That would leave heave wrong. **Unverified:**
the spike did not measure heave period. The claim follows from how A is built.

### C3 — tick cost (verified, Editor only)

Both variants run well under the 0.6 ms budget. B is about 7 % cheaper because it skips
Unity Physics' solver. Measured in the Editor with Burst safety checks at their current
setting, which the test did not record. **Not measured:** a Player build or a larger fleet.

### C4 — what reckoned code needs from the engine (build-verified)

The strict form (`noEngineReferences: true`) failed to compile with 75 errors, all CS0246.
Every error named `NativeArray<>`, `IJob`, `IJobParallelFor`, `[ReadOnly]` or
`[WriteOnly]`, which are Unity's own containers and job types in
`UnityEngine.CoreModule`. None involved Unity Physics. So no Burst job code can ever be
strictly engine-free.

With the core module allowed, the compiled spike references DeepWater.Sea, netstandard,
Unity.Burst, Unity.Collections, Unity.Entities, Unity.Mathematics, Unity.Physics,
UnityEngine.CoreModule, and UnityEditor.CoreModule.

The `UnityEditor.CoreModule` reference is not our code. The DLL contains Unity's generated
job-registration code (`__JobReflectionRegistrationOutput`, `EarlyJobInit`,
`InitializeOnLoadMethod`); that much is build-verified. That this is the reference's only
source, and that it disappears in a Player build, is **unverified**.

**Recommended guard:** ban APIs, not the engine. Two ways, cheapest first:

1. **A test**, in the same shape as the C4 test. It reads the reckoned assembly and fails
   on any reference to a banned member: `UnityEngine.Time`, `UnityEngine.Random`,
   `UnityEngine.Physics`, `Object.Find*`, `MonoBehaviour`, `DateTime.Now`,
   `System.Random` without a seed, `Environment.TickCount`, and so on. It is cheap and
   fits "invariants are tests".
2. **A compile-time analyzer** (Roslyn BannedApiAnalyzers with a BannedSymbols list).
   It is stronger because it fails the build, not a test. **Unverified:** how Unity
   passes the analyzer its `BannedSymbols.txt` (an additional file) needs checking before
   committing to it.

## Finding F1 — Sea State hulls roll about 1.5× too fast (verified)

Sea State's `BuoyancyProbeJob` (`Assets/Sea State/Core/Buoyancy/BuoyancyJobs.cs`, line 70)
uses `arm = world − WorldCenterOfMass`, where `world` is the probe's centre. With
`BuoyancyHullSO.FillBoxGrid` probes (columns centred at y = 0 and spanning the full
height), each column's buoyancy acts at mid-height, not at the centre of its submerged
part.

For a vertical force, the point only matters once the hull heels. Then it drops the
"centre of buoyancy sits below the centre of gravity" term. The hull's stability height
becomes BM alone (5.29 m for the test box) instead of GM (2.44 m).

The spike reproduces Sea State's choice behind a switch (`SpikeRunner.SeaStateApplicationPoint`).
On the same box and the same integrator:

- force at the submerged centroid (corrected): **9.46 s**
- force at the column centre (Sea State today): **6.35 s**, so it rolls 49 % faster
- theory: √(BM/GM) = √(5.29/2.44) = 1.47; measured 9.46/6.35 = 1.49

**Fix:** in `BuoyancyProbeJob`, apply the force at
`world + up · (½·submerged − ½·HeightM)`, the spike's line in `Core/SpikeHydro.cs`.
That changes how every Sea State hull moves. It is a separate change, not part of this
spike.

*Correction (2026-10-23, after the fix was made in Sea State):* an earlier version of this
report said the fix would change the DDG scene. It does not. In DDG Engagement the DDG is a
static transform with no Rigidbody and no Sea State components (checked in the Editor).
Only the Sea State demo's hulls are affected. The fix also had to flip the column axis for
an upside-down hull. The spike's line, like the first version of the fix, puts the force
above the wet part once a hull passes 90°.

## Authored values (not measured; each needs a source before production)

| Value | Spike setting | Where |
|---|---|---|
| Added mass fractions (sway, heave, surge) | 0.5, 0.8, 0.05 | `SpikeHull.Box` |
| Added inertia fractions (pitch, yaw, roll) | 0.8, 0.5, 0.15 | `SpikeHull.Box` |
| Horizontal damping as a fraction of vertical | 0.02 | `SpikeHull.Box`: Sea State damps fully both ways, which stops a hull dead |
| Linear damping | 2 · 0.2 · √(g·d) | `SpikeHull.Box` |
| B contact period, damping ratio | 0.25 s, 0.6 | `SpikeWorldB` |
| A solver iterations, box bevel | 4, 0.05 m | `SpikeWorldA`, `SpikeContacts.Hull` |
| Hull mass | displacement at design draft | `SpikeHull.Box` |

A real hull would take its added mass and inertia from strip theory or tank data; the
fractions here are order-of-magnitude values for ship-like sections.

## What variant B costs

- **We own the integrator.** Semi-implicit Euler with a gyroscopic term and anisotropic
  added mass in body axes (`SpikeWorldB`). It is small, but it is ours to keep correct.
- **We own the contact response.** The spring-damper is simple and deterministic, but it
  overlapped 0.111 m. Production needs a better response: a proper impulse solver, or
  Unity Physics' solver used only for contact pairs. That is the first design question
  for the real core.
- **Unity Physics shrinks to a collision detector.** We use its broadphase and distance
  queries (`CollisionWorld.BuildBroadphase`, `CalculateDistance`). That is the most stable
  part of its API.

## Footprint and how to remove it

- `Packages/manifest.json` gains `"com.unity.physics": "1.4.2"`, which brings in
  Unity Entities. `packages-lock.json` changes to match.
- `Assets/Reckoning Spike/`: `Core/` (assembly `DeepWater.Reckoning.Spike`, not
  auto-referenced), `Tests/`, this report.
- No other assembly references the spike. Nothing outside the folder changed.

To remove it, delete the folder and the manifest line, then let Unity re-resolve.

## Methodology candidates

- **A physics engine's mass model is a design constraint.** If the domain needs
  direction-dependent inertia (ships, aircraft), an engine with scalar mass can detect
  collisions but cannot integrate the motion. Measured here: C2, A +10.4 % vs B −2.7 %.
- **Guard what is banned, not where it lives.** An assembly-level "no engine" switch
  fails for any job code in Unity. A banned-API list says what actually breaks
  determinism. Measured here: C4, 75 CS0246 errors, all Unity container and job types.
- **Reproduce the incumbent behind a switch before calling it wrong.** F1 put Sea State's
  application point and the corrected one side by side on one integrator, and the gap
  matched theory (1.49 measured vs 1.47 predicted).

These belong in `METHODOLOGY.md` under Candidates once SIX agrees.
