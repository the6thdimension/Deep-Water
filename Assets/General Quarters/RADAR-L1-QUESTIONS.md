# Radar L1: questions for SIX

**PREPARED QUESTIONS. NOTHING DECIDED, NOTHING BUILT.** Written 2026-10-27 (overnight, Claude) so that Stream A's
first slice can start from one sitting of answers. Each question has Claude's leaning; the leaning is not a decision.
Answers become G61 onward in the BLUEPRINT decision register.

**Job of this document:** hold the open Radar L1 questions until SIX answers them. Once answered it is superseded by
the BLUEPRINT and can be struck through.

## Where things stand

- **General Quarters today:** `Core/Cic/Radar.cs` is Radar L0. Every live target inside instrumented range and above
  the horizon, every sweep: no noise, no misses, identity from truth. Tracks L0 copies detections to tracks.
- **What now exists to build on:** RH Radar Suite's pure core (2026-10-27, Radar-Suite-Dev, on every branch), all
  engine-free C# with tests (37/37):
  - the radar equation in dB and the range for a required SNR;
  - Albersheim, North and Swerling 1 detection statistics;
  - the 4/3-Earth horizon;
  - Barton's range, angle and range-rate errors, with floors;
  - constant-γ surface clutter (grazing angle, cell, CNR, SCNR);
  - an α-β filter, and a track file (M of N, coast, gating, quality).
- **A second radar plan exists:** `Assets/RH Radar/` holds only a README, a routine ROADMAP (2026-09, Phase 0
  unchecked) and an asmdef: a greenfield "SO-Profile + Pure-Core + MB-Adapter" radar module for the autonomous
  routines, with its own L0–L4 ladder and `RH Shared` contracts (`ITargetTrack`, `ISensorReport`). Nothing in it is
  built. Two radar plans side by side contradict each other (doctrine: one lock per decision), so R1 now asks which
  is the home.
- **The BLUEPRINT's risk line** (§8) already says: "General Quarters owns a pure sensor contract; Radar Suite physics
  ported at radar L1+". The questions below decide how.

## Round 1: shape

| # | Question | Options | Claude's leaning |
|---|---|---|---|
| R1 | Where does the radar physics live? | **A.** GQ references the `RHRadarSuite` assembly as it is. **B.** Split the suite's pure core (`Core/Detection`, `Core/Tracking`) into its own engine-free assembly, `RHRadarSuite.Core`, that both the suite and GQ reference. **C.** Copy the pure files into GQ and let them diverge. **D.** Move the pure core into the empty `RH Radar` module (its plan already asks for a pure core) and retire one of the two radar plans | **B**, and retire the `RH Radar` plan (strike its ROADMAP with a reason). One implementation and one test suite. A drags the legacy MonoBehaviour modules into GQ's references. C would let two copies of the same equations drift (doctrine 14). D is sound too, but it moves tested code to follow an unbuilt plan |
| R2 | Which radars at L1? | **A.** SPY-1 only: one search-and-track radar. **B.** SPY-1 and a surface-search radar. **C.** Those and a navigation radar | **A.** The SPG-62 illuminators are already Stream B's (G44). One radar end to end first |
| R3 | Where its numbers come from | **A.** A generic S-band array in SPY-1's role, AUTHORED, calibrated to one public detection-range figure (flagged, as G57 was). **B.** Wholly AUTHORED, no calibration. **C.** Open-literature parameter estimates | **A.** Safe limits (BLUEPRINT constraint 8): public data only, a game radar in the real one's role, no claim about the real system's performance |
| R4 | Tracks with it? | **A.** Radar L1 feeds Tracks L0 first. **B.** Radar L1 and Tracks L1 (the suite's track file: α-β, M of N, gating) in one slice | **B.** Noisy plots through Tracks L0 would make every track jitter by the measurement error, and fire control reads tracks, never truth |

## Round 2: physics

| # | Question | Options | Claude's leaning |
|---|---|---|---|
| R5 | Target radar cross-section | **A.** One dBsm per `TargetKind` (AUTHORED). **B.** Per kind, with coarse aspect lobes (nose, beam, tail). **C.** B plus a Swerling class per kind | **A.** Aspect is the suite's Phase 2 item, not built; one figure per kind is honest at L1 |
| R6 | How a look decides "seen" | **A.** A Swerling 1 Pd draw per look at a fixed Pfa, from the run's seeded generator (the Fix replays it). **B.** A threshold on mean SNR, no randomness. **C.** Cumulative Pd over a dwell | **A.** Fluctuation is what makes a small target flicker in and out. It stays deterministic through the seed (Reckoning R3) |
| R7 | Sea clutter | **A.** Constant-γ with γ from Sea State's sea state (needs a γ-by-sea-state table from a public source: research first). **B.** No clutter at L1, horizon only. **C.** Constant-γ with one AUTHORED γ | **A**, with the table as the slice's first task. The ROADMAP line already says "sea clutter from `SeaConditions`". If no public table holds up, fall back to C, flagged |
| R8 | Identification | **A.** Keep L0's identity from truth. **B.** IFF interrogation | **A.** L1 is detection physics; IFF is its own step |

## For the RH Radar Suite itself (not General Quarters)

| # | Question | Options | Claude's leaning |
|---|---|---|---|
| RS1 | Wire the pure core into the five legacy LOD modules? | **A.** Rewrite them onto it now (they step in `Update` with `Time.time`). **B.** Leave them; GQ is the first real consumer. **C.** Retire them once GQ's radar works | **B**, then decide between A and C with GQ's radar in hand. CLAUDE.md asks for a go before a rewrite beyond one file, so this waits for SIX either way |

## Cost to answer

About 10 minutes for both rounds. R1 and R4 shape the slice; the rest can take the leaning.
