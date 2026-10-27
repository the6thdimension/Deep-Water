# RH Radar Suite Prefabs

**There are no prefabs here yet.** Ready-made radar-equipped prefabs are ROADMAP Phase 5 ("Ship/vehicle/aircraft
prefab integration"). This file used to describe `RadarSystem` and `RadarTarget` prefabs that were never made
(corrected 2026-10-27, ROADMAP known issues).

## Until then

- **Outfit any GameObject in one click:** select it, then **GameObject > RH Navy Sims > Radar Suite > Add &lt;role&gt; Radar**
  (ground search, air search, naval surface search, fire control, airborne intercept). It adds the controller, the
  profile's LOD module and the diagnostics gizmos, and keeps your Inspector tuning if run twice.
- **Make something detectable:** add a `RadarSignature` component.
- **See it working:** **RH Navy Sims > Radar Suite > Build Example Scene** (`Examples/README.md`).

Once you have tuned an outfitted object, drag it into the Project window to make your own prefab.
