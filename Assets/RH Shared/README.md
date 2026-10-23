# RH Shared

Cross-module contracts. **No concrete logic lives here** — only interfaces, enums, and
small unmanaged structs that two or more modules need to agree on.

## Rule

> Modules reference RH Shared. RH Shared references nothing.

If you find yourself wanting to `using GuidedFury.X` from RH Shared, stop and rethink:
the dependency is upside down. Either the consumer doesn't need that detail, or the
detail belongs in RH Shared (promoted from the producer).

## Current contracts

| Contract | Producers | Consumers |
|---|---|---|
| `ITargetTrack` | `RH Radar`, `RH Aviator` (IRST), `Guided Fury` (seeker output) | `Guided Fury` (seeker input), `RH Aviator` (tactical brain), HUD |
| `ISensorReport` | `RH Radar`, future EO/IR | `RH Aviator`, fire-control UI |
| `IThreat` | Any aircraft, ship, missile, ground unit | `RH Radar` (target list), `RH Aviator` (priority) |

## Adding a contract

1. Decide if two+ existing modules need it. If only one does, it belongs *in* that module.
2. Write the interface here under `Contracts/` with full XML doc on every member.
3. Document the producer/consumer table in this README.
4. Update both module routines' ROADMAPs so they know to consume / produce it.
