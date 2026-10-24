---
name: hull-stability-check
description: "Predict and verify how a floating hull rolls in Deep Water: compute KB, BM (including the probe-waterplane correction), KG, GM, the righting moment and the roll period from a hull's dimensions and centre of mass, then check the Sea State buoyancy model against them, first in EditMode tests and then in play mode with PhysX. Use this whenever a hull or BuoyancyHullSO is added or changed, buoyancy or ship-motion code changes, a ship 'rolls too fast', bobs like a cork, capsizes, lies on its side, floats upside down or needs ballast, a centre of mass is set, or work starts on the DDG sway or Reckoning ship motion. Includes a calculator script and the closed-form theory, with the traps found on 2026-10-23."
allowed-tools: Bash, Read, Edit, Write, Grep, Glob
---

# Hull stability check

On 2026-10-22 Sea State hulls floated at the right draft and passed every test, yet rolled 1.5× too fast. The buoyancy force sat at the wrong point, and nothing checked roll. This skill makes roll a checked quantity. Climb the ladder from cheap to expensive and stop when confident.

## 1. Predict (seconds)

```bash
python -I .claude/skills/hull-stability-check/scripts/hull_predict.py \
  --length 154 --beam 20 --height 12 --draft 6.3 --across 5 [--com-y -0.7] [--inertia-roll <from Unity>]
```

| Quantity | Box formula | Meaning |
|---|---|---|
| KB | d/2 | Centre of buoyancy above the keel |
| BM | B²/(12 d); probe model: × (n²−1)/n² for n columns across | Metacentre above B. The probe waterplane has less second moment than the true box |
| KG | H/2 + centre-of-mass offset | Centre of gravity above the keel |
| GM | KB + BM − KG | Stability. Negative means it won't float upright |
| Righting moment | m g sin φ (GM + ½ BM tan² φ) | Wall-sided formula, good to moderate heel |
| Roll period | 2π √(I / (m g GM)) | Undamped. **I is about the centre of mass, as Unity reports it** |
| IMO period | 2CB/√GM, C = 0.373 + 0.023 B/d − 0.043 L/100 | Real-ship yardstick (2008 IS Code A 2.3); includes added inertia, so a bare probe hull rolls faster |

Reference values (verified 2026-10-23):

| Hull | GM (probe model) | Predicted | Measured |
|---|---|---|---|
| 154 × 20 × 12 m box, 6.3 m draft, 5 across | 2.23 m | 9.05 s undamped | PhysX 9.42 s (damping adds ~4 %) |
| Same box, 20 across | 2.43 m | 8.67 s | 8.67 s |
| 2 m cube buoy, 0.8 m draft, ballasted to 0.3 m above the keel | 0.41 m | 3.36 s | PhysX 3.36 s |

## 2. Check the force model in EditMode (minutes)

Pattern: `Assets/Sea State/Tests/Editor/BuoyancyModelTests.cs`. Run through the `unity-mcp-workflow` skill's `templates/run-tests.cs`.

| Test | What it checks |
|---|---|
| `HeeledBox_RightingMoment_MatchesWallSidedTheory` | Pose the hull at its design draft, heeled 5°, in a calm sea. Run the real `BuoyancyProbeJob` + `BuoyancySumJob`. Righting moment = −torque.z, within 2 % of theory. One TestCase with the centre of mass offset |
| `HeeledBox_RollPeriod_MatchesBoxTheory` | 1-DOF roll integration using the job's torque each step; period within 2 % |
| `InvertedBox_RightsLikeAnUprightBox` | 185° must load like 5° for a symmetric box |
| `CubeBuoy_UniformTipsOver_BallastedRightsItself` | Sign check: unstable without ballast, theory-matching with it |

Copy the helpers (`BoxProbes`, `HeeledPose`, `BodyLoad`, `MetacentricHeight`) for a new hull. The 2 % band is chosen so a return of the column-centre defect (GM = BM) fails loudly.

## 3. Measure in play mode, PhysX (minutes)

Use the `unity-mcp-workflow` skill's `templates/play-recorder.cs`, then `scripts/periods.py`:

1. Calm the sea first: `SeaStateDirector.SetSeaState(0)`, `SetSwell(0)`. Wait out `transitionSeconds` (8 s) plus settling.
2. Heel each hull 5° **about its own forward axis**, zero its velocities, and record heel per fixed step for about 40 s.
3. The recorder logs `rb.inertiaTensor`. Feed `--inertia-roll` (the component about the length axis, z for Sea State hulls) back into `hull_predict.py`, and compare the periods.

## Traps

- **Unity recomputes its automatic inertia tensor about a scripted centre of mass** (parallel-axis shift: the buoy went 2187 → 3794 kg·m²). Predict with the tensor Unity reports.
- **The measured period is the damped one.** Probe damping lengthens it by 1/√(1−ζ²): a few percent for ships, more for small bodies.
- **The probe model breaks down near 90°** of heel, because its columns assume nearer-vertical than horizontal. A hull with negative GM may come to rest on its side there instead of finding its true large-angle equilibrium. Fix the mass model (ballast via `BuoyancyHullSO.overrideCenterOfMass` / `centerOfMassM`), not the probe model.
- **A uniform box is often unstable.** A cube is stable upright only for density ratio d/H below ~0.21 or above ~0.79. Real buoys and ships carry ballast, so set the centre of mass deliberately and label it AUTHORED if it isn't sourced.
- **Upside-down symmetry is the check for any "along the body axis" offset.** It caught the first version of the 2026-10-23 fix, which none of the upright tests did.
- **The IMO period is not a pass/fail gate for a bare probe hull.** It includes added inertia that Sea State doesn't model. Use box theory as the gate and IMO as context, until ship motion with added mass exists (Reckoning, variant B).
