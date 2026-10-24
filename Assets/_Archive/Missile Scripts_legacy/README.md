# Missile Scripts (legacy)

**Archived 2026-10-24** by General Quarters slice 7 (BLUEPRINT G13, METHODOLOGY P8). The full checklist is in `Assets/General Quarters/ROADMAP.md`, slice 7.

| File | What it did | Superseded by |
|---|---|---|
| `BurkeMissile.cs` | `BurkeVLSSystem`'s missile: boost, sustain and coast homing on a Rigidbody, with a proximity fuze | Guided Fury's ESSM (`Assets/Guided Fury/`), which flies inside the General Quarters combat core |

**Still used by:** `Assets/rim-162essm/ESSM Shell.prefab`. Guided Fury disables it at launch (METHODOLOGY AP8). The `.meta` moved with the script, so the prefab's reference holds.

**To restore:** move `BurkeMissile.cs` and its `.meta` back to `Assets/Scripts/Missile Scripts/`.
