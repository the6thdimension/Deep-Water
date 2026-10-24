# Radar Scripts (legacy)

**Archived 2026-10-24** by General Quarters slice 7 (BLUEPRINT G13, METHODOLOGY P8). The full checklist is in `Assets/General Quarters/ROADMAP.md`, slice 7.

| File | What it did | Superseded by |
|---|---|---|
| `SPG62FireControlRadar.cs` | A scanning radar (TWS and STT) with track quality that sends fire commands to `BurkeVLSSystem`. It is named for the SPG-62 illuminator but behaves as a search-and-track radar | Radar L0, tracks and TEWA in `Assets/General Quarters/Core/Cic/`. The real illuminator is Illuminators L1 (General Quarters Stream A) |
| `VLSManager.cs` | A 32-cell VLS fed by `RH_RadarModule` contacts, with wind, roll and pitch launch limits and reloads | `Core/Magazine/Vls.cs`. The random wind (`UnityEngine.Random`) and the reloads were dropped deliberately; see the checklist |
| `Editor/SPG62FireControlRadarEditor.cs`, `Editor/SPG62RadarScreenWindow.cs`, `Editor/VLSManagerEditor.cs` | Their inspectors and the radar screen window | — |

**Still used by:** `SPG62FireControlRadar` by `_SCENES/DDG Engagement.unity` and `_SCENES/Rocket.unity`. The `.meta` files moved with the scripts, so the references hold. `VLSManager.cs` also defines the `MissileType` enum that `Missile Scripts/MissileData.cs` uses; it still compiles from here.

**Not archived here yet:** `RH_RadarModule.cs` stays in `Assets/Scripts/Radar Scripts/` for the RH Radar routine to archive to this folder (`Assets/RH Radar/README.md`).

**To restore:** move the files (with their `.meta`) back to `Assets/Scripts/Radar Scripts/`, keeping `Editor/` as a subfolder.
