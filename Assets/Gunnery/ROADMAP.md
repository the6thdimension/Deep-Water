# Gunnery — roadmap

**Job of this document:** what is built and what is next in Gunnery. Decisions are in `BLUEPRINT.md`.

## Done
- [x] **Slice 1 (2026-10-27):** General Quarters' `Core/Ballistics` moved here (K1), GUIDs kept; GQ's 113 tests pass.
- [x] **Slice 2 (2026-10-27):** the aircraft gun (K4–K6). Round flight in 3-D on the firing table's physics; volleys
  as shot groups with a swept hit test; hits drawn from the caller's seeded source and reported through RH Shared's
  `HitReport`; the lead sight and the miss distance. Air Wing: trigger, rounds, hit points, killed and falling, the
  M61A2's numbers, the HUD pipper, tracers. RH Aviator: trigger discipline by skill, gun tracking, kills first.

## Next
- [ ] Friendly fire in the AI's trigger: hold fire with a friend near the line.
- [ ] Calibrate the 20 mm round's form factor to a published range, flagged (as General Quarters' G41 did).
- [ ] General Quarters' guns onto flown rounds where it pays (CIWS against a missile), behind its fidelity ladder.

## Change log
- **2026-10-27** — Written with slices 1 and 2.
