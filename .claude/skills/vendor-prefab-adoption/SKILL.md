---
name: vendor-prefab-adoption
description: "Safely adopt a vendor / legacy prefab (a missile shell, aircraft, vehicle, ship) into a first-party system that owns its motion or metrics. Use whenever you assign a vendor prefab as the instance for a system whose adapter writes the transform each FixedUpdate, or compose camera/hardpoint/muzzle/effect offsets around a prefab you didn't author. Covers neutralizing foreign motion drivers AND metric assumptions (scale), and normalizing scale at the asset. Codifies METHODOLOGY anti-patterns AP8 and AP10."
allowed-tools: Bash, Read, Write, Edit, Glob, Grep
---

# Vendor Prefab Adoption (AP8 + AP10)

Vendor art is authored for someone else's engine/pipeline. When you drop it into a system that owns motion (FixedUpdate transform writes) or composes spatial offsets around it, its hidden assumptions fight yours. Two failure classes, both from the 2026-09-08 ESSM work.

## AP8 — Neutralize foreign MOTION drivers on activation

Any component on (or under) the adopted GameObject that also writes the transform or applies physics is a hidden competitor. The `ESSM Shell.prefab` shipped a non-kinematic `Rigidbody` (gravity on), a legacy `BurkeMissile`/`ESSMHomingMissile6DoF` script, AND an embedded fullscreen `Missile Cam` (far clip 1000). Result: the missile fell straight down (PhysX gravity beat the kinematic writes) and the embedded camera hijacked the display and died with the missile ("camera disappears after a moment").

In the adapter's activation path (`MissileBehaviour.NeutralizeForeignComponents()` is the reference), do:
- **Rigidbody**: zero linear + angular velocity **BEFORE** setting `isKinematic = true` (PhysX warns and ignores a velocity write on an already-kinematic body), then `useGravity = false`.
- **Foreign MonoBehaviours**: disable every `MonoBehaviour` whose namespace is outside your system's (skip `UnityEngine.*` engine components). Warn loudly per component.
- **Embedded Cameras + AudioListeners** anywhere in children: disable them (they render over / fight your scene cameras, or trip "2 audio listeners" spam).
- Make it **opt-out** via a serialized bool so a user who genuinely wants vendor physics to coexist can.

Better still: strip these from the prefab so the per-activation scan + warning goes away.

## AP10 — Neutralize foreign METRIC assumptions (scale)

`foreign.TransformPoint(localOffset)` applies position, rotation **and lossyScale**. The ESSM prefab root scale is `(65, 65, 100)`, so a 15 m chase-camera offset became ~1.5 km — the camera orbited from a kilometer away and the missile "vanished."

- For any offset around an object you don't author (camera rig, hardpoint, muzzle, effect anchor), **compose position + rotation only**:
  ```csharp
  Vector3 pos = foreign.position + foreign.rotation * localOffset;   // NOT TransformPoint
  ```
- Reserve `TransformPoint` for hierarchies whose scale you own (== 1).

## Normalize scale at the ASSET, once

The ESSM prefab was **364 m long** (root `(65,65,100)`). Fix the vendor scale at the asset a single time, and every compensating hack downstream (camera offsets, HUD sizes, fuze radii) dies of natural causes.

Technique (reference: the in-editor normalizer used on ESSM Shell): fold `rootScale × shrinkFactor` into each direct child's TRS by decomposing `Scale(S) * TRS(child)` back to position/rotation/scale, with a **shear check** (columns should stay orthogonal), then set root scale to 1. Compute the shrink from renderer bounds along the length axis vs the real dimension (ESSM → 3.66 m). Do it via `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset`.

## Also watch for

- **Stray edit-mode clones**: firing/spawn commands run outside play mode can leave giant-scale clones parked in the scene. Sweep root objects named `*(Clone)` / `GF_*` / test-prop names and `DestroyObject` them before saving.
- After adopting, verify in play mode (or a pure-core sim) that the missile actually flies — don't trust that neutralization worked from code alone.
