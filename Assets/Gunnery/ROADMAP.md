# Gunnery — roadmap

**Job of this document:** what is built and what is next in Gunnery. Decisions are in `BLUEPRINT.md`.

## Done
- [x] **Slice 1 (2026-10-27):** General Quarters' `Core/Ballistics` moved here (K1), GUIDs kept; GQ's 113 tests pass.
- [x] **Slice 2 (2026-10-27):** the aircraft gun (K4–K6). Round flight in 3-D on the firing table's physics; volleys
  as shot groups with a swept hit test; hits drawn from the caller's seeded source and reported through RH Shared's
  `HitReport`; the lead sight and the miss distance. Air Wing: trigger, rounds, hit points, killed and falling, the
  M61A2's numbers, the HUD pipper, tracers. RH Aviator: trigger discipline by skill, gun tracking, kills first.
- [x] **Fix (2026-10-29, SIX's go):** `ShotGroup.RoundHitProbability` is exact. It is now the circular normal pattern's mass
  inside the disc, 1 − Q₁(d/σ, r/σ), by Gauss–Legendre on the Rice integral, within 5e-7 of the exact series. The
  product formula it replaced under-counted off-centre hits on a target larger than the spread (0.135 against 0.966 at
  r/σ = 4, d/σ = 2).
  - 268/268 across Gunnery, Air Wing, Aviator and General Quarters, was 266/266.
  - Gunnery's 8 mil test: 55 → 58 hits of 100 (seed 42).
  - General Quarters' guns-only L1 raid: 6 → 7 of 8 killed.
  - Every pinned Fix is unchanged.
  - About 0.9 µs a call, against 0.09 µs, inside the 3σ reach only.

## Next
- [ ] Friendly fire in the AI's trigger: hold fire with a friend near the line.
- [ ] Calibrate the 20 mm round's form factor to a published range, flagged (as General Quarters' G41 did).
- [ ] General Quarters' guns onto flown rounds where it pays (CIWS against a missile), behind its fidelity ladder.

## Change log
- **2026-10-27** — Written with slices 1 and 2.
- **2026-10-29** — The hit model made exact (the Fix item above), on SIX's go; METHODOLOGY AP36.
