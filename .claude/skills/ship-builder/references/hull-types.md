# Hull types

| Type | Status | What carries over | What is missing |
|---|---|---|---|
| **Displacement monohull** (destroyer, frigate, cruiser, carrier, auxiliary) | **PROVEN** on the DDG-51 | Everything in this skill | — |
| **Displacement multihull** (catamaran, trimaran) | UNPROVEN, small step | Column buoyancy (it handles several hulls naturally); collision; loadout | `check_layout.py` ranges will WARN, which is expected: say so in the provenance. MMG needs multihull interaction terms (the hulls' wakes and the rudders between them); find basin data for a similar form |
| **Planing craft** (patrol boats, RHIBs, fast attack craft) | **UNPROVEN** | Geometry, collision, loadout | Dynamic lift. Above roughly Fn_∇ 1, hydrodynamic pressure carries most of the weight, and the hull rides up and trims. Column buoyancy has no lift term. Needs a Savitsky-style planing model (lift and drag from trim, speed and wetted length), porpoising limits and slamming. MMG does not apply in planing |
| **Submarine** | **UNPROVEN** | Geometry, collision, compartments, loadout | Submerged buoyancy is fixed (no waterplane), so stability comes from BG, not GM. It needs: trim and ballast tanks and their control; hydroplanes and a depth-keeping autopilot; a submerged manoeuvring model (6-DOF, for example the DREA / Feldman standard submarine equations); a surfaced mode that hands over to the column model; and sonar instead of radar for the CIC |

**Before building an UNPROVEN type:**

1. **Agree the scope with SIX.** This is a new physics element with its own fidelity ladder, so it needs a BLUEPRINT row.
2. **Build the missing physics as its own spike first** (`spike-with-pass-criteria`), with numbers to pass. For example, a planing hull's resistance hump and running trim against a published Savitsky example, or a submarine's depth change against a published manoeuvre.
3. **Then run this skill's layers.** Add `references/<type>.md` from what the first ship of the type taught, and move the type to PROVEN here.

Sea State's column buoyancy stays the surface model for anything at the surface: CPU, FixedUpdate, deterministic. Never query HDRP water for physics.
