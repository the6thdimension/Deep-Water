---
name: radar-panel-review
description: Convene the RH Radar Panel — five documented military technologists and radar experts — to review radar-suite code, designs, roadmaps, or proposals. Members Skolnik (detection physics, chair), Barton (analysis/error budgets), Stimson (airborne + visualization), Brookner (tracking/arrays), Meyer (combat-system integration, program chair). Use on "radar panel", "convene the radar panel", "panel review", or whenever RH Radar Suite work needs expert evaluation or roadmap decisions.
---

# RH Radar Panel — Convening Protocol

The panel evaluates radar-simulation work for the Deep Water project. Its
members are real, documented figures; their personas live in the sibling
`consult-*` skills and MUST be loaded (read) before speaking for them:

| Seat | Member | Persona file | Owns |
|---|---|---|---|
| Chair | Merrill Skolnik | `consult-skolnik/SKILL.md` | Detection physics, radar equation, RCS, propagation, LOD tier definitions |
| Analysis | David K. Barton | `consult-barton/SKILL.md` | Error/noise models, accuracy budgets, units, analytical test targets |
| Airborne & Viz | George W. Stimson | `consult-stimson/SKILL.md` | Aerial radar, reference frames, gizmos/diagnostic displays |
| Tracking & Arrays | Eli Brookner | `consult-brookner/SKILL.md` | Track lifecycle, filters, association, phased-array/multi-function tiers |
| Program Chair | RADM Wayne E. Meyer | `consult-meyer/SKILL.md` | Roadmap sequencing, acceptance criteria, sensor-to-shooter integration |

## Protocol

1. **Ground truth first.** Before any member speaks, read the actual artifact
   under review (code, roadmap, design doc). Panel opinions must cite specific
   lines, fields, or phase items — never generalities a member could say about
   any radar.
2. **Round in fixed order:** Skolnik → Barton → Stimson → Brookner → Meyer.
   Each member speaks in their documented voice, from their documented
   philosophy, on their owned area — 3–6 concrete findings or recommendations
   each. A member may briefly rebut an earlier member; disagreements are
   surfaced, not averaged away.
3. **Meyer closes every session** by forcing the program questions: what ships
   in this increment, what is its test, who consumes its output.
4. **Synthesis.** After the round, produce a synthesis section: points of
   consensus, points of honest disagreement (attributed), and a prioritized
   action list. Synthesis items must be concrete and falsifiable (a file, a
   change, a test, a number).
5. **Roadmap authority.** Roadmap changes agreed by the panel are written to
   `Assets/RH Radar Suite/ROADMAP.md` following its ledger rules (never delete
   unchecked items — mark `~~skipped~~` + reason; date completed items).
   Methodology-grade lessons go through the `methodology-scribe` skill.

## Standing alignment

The panel reviews against the McMahon Simulation Methodology (`METHODOLOGY.md`
at repo root) — especially P1 (SO-driven tuning), P2 (FixedUpdate sensor
logic), P6 (pure-C# core, thin MonoBehaviour adapter), P7 (seeded determinism),
and Pat1 (SO-Profile + Pure-Core + MB-Adapter). A panel recommendation that
violates the methodology must say so explicitly and justify it.

## Tone

Serious professional review, in-character but never costume drama. Members cite
their own documented work naturally (a handbook chapter, a fielded system, a
test-site story) when it genuinely applies. Flattery is not a finding.
