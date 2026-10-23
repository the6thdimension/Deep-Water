# RH Radar

Greenfield radar / sensor module. Follows McMahon Pat1: **SO-Profile + Pure-Core + MB-Adapter**.

## Folder layout

```
RH Radar/
  Core/              — interfaces, pure-C# scan / track / Kalman logic, RadarEntity
  Modules/           — pluggable behaviors: search modes, track filters, ECCM, MTI
  ScriptableObjects/ — RadarProfileSO presets per real-world radar (SPG-62, APG-79, ...)
  Prefabs/           — runtime assemblies (antenna + reflector + behaviour)
  Examples/          — demo scenes + sample profiles
  Tests/Editor/      — EditMode tests colocated with the module (C6)
```

## Fidelity ladder (mirrors Guided Fury's LOD pattern)

| Tier | Name | What it models |
|---|---|---|
| L0 | Cone scan | Boolean visibility through a cone, no noise, no range gate. Cheap. |
| L1 | Range-gated detection | Range, beam pattern, RCS, free-space attenuation, SNR threshold. |
| L2 | Track filtering | Kalman / α-β filter on detections → smooth track output. |
| L3 | Doppler / MTI | Closing-velocity gating, ground-clutter rejection, chaff discrimination. |
| L4 | Multi-target track + ECCM | TWS, frequency agility, jam detection, decoy resolution. |

## Contracts implemented

- `RH.Shared.Contracts.ISensorReport` — every radar exposes its current tracks.
- `RH.Shared.Contracts.ITargetTrack` — `RadarTrack` is the concrete struct.

## Legacy

`Assets/Scripts/Radar Scripts/RH_RadarModule.cs` and `SPG62FireControlRadar.cs` are the
pre-McMahon predecessors. The routine archives them to `_Archive/Radar Scripts_legacy/`
on its first iteration (Pat C5).
