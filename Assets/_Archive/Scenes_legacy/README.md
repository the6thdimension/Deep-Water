# Scenes_legacy

Legacy scenes SIX no longer uses (decided 2026-10-25, build-speed work, `BUILD-SPEED-LEDGER.md`). They are moved here with their `.meta` files, so their GUIDs survive (METHODOLOGY P8).

| Scene | Was in | What it used that has been removed | Status |
|---|---|---|---|
| `OutdoorsScene.unity` | `Assets/_SCENES/` (the only scene in Build Settings) | Obi, NWH, RCC, FDS Assets and AerialArcade are still in the project; it is archived because it is not used | Opens. Taken out of Build Settings |
| `Mapping.unity` | `Assets/_SCENES/` | Cesium for Unity (`com.cesium.unity`) and `Assets/CesiumSettings` (which held a public ion token) | Cesium components show as missing scripts |
| `Rocket.unity` | `Assets/_SCENES/` | Nothing; not used. Its shared assets in `Assets/_SCENES/Rocket/` stay, because live scenes use them | Opens |
| `Rocket Man.unity` | `Assets/_SCENES/` | `Assets/SceneTemplateAssets` | Template assets missing |

Superseded by the live scenes: `GQ_Range` (General Quarters), `Dogfight` (Air Wing), `SeaStateDemo` (Sea State) and `MissileRange` (Guided Fury).

## Restore

1. Move a scene back to `Assets/_SCENES/` with its `.meta`.
2. For Mapping, add `"com.cesium.unity": "1.23.2"` and the Cesium scoped registry back to `Packages/manifest.json`, and restore `Assets/CesiumSettings` from git history (commit `0c50c04e`).
3. For Rocket Man, restore `Assets/SceneTemplateAssets` from the same commit.
