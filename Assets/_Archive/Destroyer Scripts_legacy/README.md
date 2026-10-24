# Destroyer Scripts (legacy)

**Archived 2026-10-24** by General Quarters slice 7 (BLUEPRINT G13, METHODOLOGY P8). The full checklist of what replaced each feature is in `Assets/General Quarters/ROADMAP.md`, slice 7.

| File | What it did | Superseded by |
|---|---|---|
| `BurkeVLSSystem.cs` | 96-cell Mk 41 VLS on a MonoBehaviour: loadout, priority launch queue, hatch triggers, cell cooldown | `Assets/General Quarters/Core/Magazine/Vls.cs` (deterministic, logged) and `Modules/Presentation/VlsHatches.cs` |
| `BurkeVLSFXController.cs` | Launch plume, launch sound, hatch animators | `Modules/Presentation/CombatPresentation.cs` |
| `Editor/BurkeVLSCellGenerator.cs` | Menus that generate the 96 cell sockets and wire SPG-62s to the VLS | General Quarters' builders (`Editor/Ddg51CombatData.cs`, `RangeSceneBuilder.cs`) |
| `Editor/EngagementTestSetup.cs` | Menu that sets up a three-target auto-fire test | The range (`GQ_Range.unity`) and its scripted raids |

**Still used by:** `_SCENES/DDG Engagement.unity` and `_SCENES/Rocket.unity`. The `.meta` files moved with the scripts, so those scenes keep working. The scripts still compile, and their `Tools/Deep Water/…` menus still appear.

**To restore:** move the files (with their `.meta`) back to `Assets/Scripts/Destroyer Scripts/`, keeping `Editor/` as a subfolder.

**Delete only when** no scene or prefab references these GUIDs.
