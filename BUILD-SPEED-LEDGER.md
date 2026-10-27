# Build-speed ledger

**One job:** this file records the measured rounds of the build-speed work on `perf/build-speed`. That is the baseline, then each change kept or dropped, with its A/B numbers and the seat that drove the cut. Round 0 is the baseline. A change is kept only if its number moves and behaviour stays put: every EditMode test assembly stays green, and play mode still works (methodology C11).

## The instrument

- **Machine:** 20 logical cores, 40 GB RAM, Samsung 980 PRO NVMe (`F:`, 65 GB free), Windows 11. Unity 6000.4.11f1, HDRP 17.4, Mono scripting backend, D3D11.
  - Unity opens the project through the junction `C:\Users\SIX\Unity Projects` → `F:`.
- **Editor state for Round 0:**
  - Up 5–6 h, with `GQ_Range` open and the Editor unfocused.
  - Other apps were running: OBS, NZXT CAM and Stream Deck, at about 30 % total CPU.
  - So the numbers are noisy. Each figure gives n, the median and the range. A/B runs are interleaved.
- **Where the times come from:**
  - Unity's own Editor.log figures: `Domain Reload Profiling`, `Tundra build success` and `Asset Pipeline Refresh`.
  - Wall-clock times come from a timestamping tail of Editor.log: `.claude/skills/unity-perf-ab/scripts/editor_log_clock.py`.
  - Per-assembly compiler and IL post-processing times come from `Library/Bee/backend*.traceevents`, read by `build_cycle.py`.
  - Attribution comes from one Editor profiler capture of a reload.
- **An edit cycle:**
  - A RunCommand logs `MARK <label>`, writes one line into a file and calls `AssetDatabase.Refresh()`.
  - "Ready" is the log line `Mono: successfully reloaded assembly`.
  - Probe lines were `private const int …` (body-only) or `public const int …` (changes the assembly's API), each removed afterwards.
- **Player builds:** `BuildPipeline.BuildPlayer` for StandaloneWindows64, timed by the BuildReport total, written to the git-ignored `Builds/BuildSpeed/`.

## Round 0: baseline (2026-10-25, `36d0f457`)

*Seat: Brendan Gregg (The Optimizer): measure before you change anything.*

| Measure | n | Median | Range | What's inside |
|---|---|---|---|---|
| Domain reload | 15 | **46.5 s** | 39.0–56.8 s | Earlier today, from the log: 37.4–38.2 s (3) |
| … of which the Unity Version Control window's `OnEnable` | 15 (+1 profiled) | **32.6 s** | 31.2–32.9 s | The phase `AwakeInstancesAfterBackupRestoration`, constant whichever scene is open |
| … an intermittent stall | 5 of 15 | +10.3–10.8 s | | Four times inside `BeginReloadAssembly` (12.6–12.8 s against the usual 1.3–2.6 s), once before the play-mode reload started. **Unattributed** |
| One private line in `DeepWater.Sea` → Editor ready | 4 | **52 s** | 47.9–64.9 s | Refresh 1.7 s, compile, reload |
| … its compile (Tundra) | 6 | **4.7 s** | 3.6–8.6 s | 22–23 nodes: `csc` on Sea (1–3 s), then IL post-processing re-runs on ~20 dependents |
| One public line in `DeepWater.Sea`: compile | 2 | **13.5 s** | 13.3–13.7 s | 67 nodes. `Assembly-CSharp` `csc` 3.6–3.8 s and `-firstpass` 3.2–3.9 s are on the critical path |
| … → Editor ready | 2 | **65.5 s** | 65.4–65.6 s | |
| One private line in legacy `Assets/Scripts/CamRot.cs` (in `Assembly-CSharp`): compile | 2 | **6.55 s** | 6.55 s | `csc` 4.05–4.23 s over all 763 files |
| … → Editor ready | 2 | **67 s** | 66.8–67.3 s | Both caught 56 s reloads |
| Refresh, nothing changed | 6 | 15 ms | 10–18 ms | Not a cost |
| Refresh that finds a changed script, before compiling | 6 | 1.7 s | 1.6–1.9 s | |
| Enter play mode (`GQ_Range`) | 2 | **51 s** | 50.4–52.0 s | Domain reload 39–46 s, then scene load 1.2–1.7 s |
| Exit play mode | 2 | 0.7 s | 0.6–0.8 s | |
| Player build, nothing changed | 3 | **14 s** | 9.6–24 s | Incremental: shader cache and player data cache |
| Player build after a one-line code change | 1 | **77 s** | | The player build program took 38 s. Burst AOT took 65.8 s in a 13:40 build |
| Player build, cold shader cache | 1 (log) | **16.5 min** | | Air Wing benchmark scene, earlier today. Shader passes sum to 805 s of the ~990 s |
| Player-script compile errors | 3 builds | **0** | | The eight blocking files were fixed in `0d24231` (PR #15) |

### Diagnosis

**Domain reload.** A profiled reload took 46.2 s, slower than usual because the profiler was on.

- **`UVCSWindow.OnEnable`: 31.2 s, 30.7 s of it self time.**
  - The Unity Version Control (Plastic SCM) window is docked in the layout.
  - Its plugin sees the tracked `.plastic/` workspace (from 2023, last written 2026-09-08) and calls `REALMHAUS@cloud`.
  - The server answers `InvalidCredentials` and closes the socket. The plugin then retries with 1, 2, 5 and 10 s backoffs on the main thread.
  - It keeps retrying in the background all day, and watches the whole project folder for file changes.
  - Its package is `com.unity.collab-proxy`. Git is this project's version control.
- **`ProcessInitializeOnLoadAttributes`: 4.3 s.**
  - AI Assistant `RunCommandUtils` static constructor 1.9 s (the MCP bridge itself).
  - Entities `BindingRegistry` 0.55 s, Plastic entry point 0.37 s, Input System 0.21 s, UVCS toolbar 0.20 s, App UI 0.14 s.
- **The rest:**
  - Restoring backups 1.7 s: `CesiumEditorWindow` constructor 0.29 s, UVCS 0.13 s.
  - Domain unload 1.6 s, `BeforeProcessingInitializeOnLoad` 1.4 s, `[InitializeOnLoadMethod]` 0.97 s, `BuildScriptInfoCaches` 0.96 s.
  - The Editor loads 215 assemblies.
- **After the reload:** FR2 (Find Reference 2) spends 1.4 s on the first tick (cache init plus check).

**Compile.**

- **Every first-party asmdef has `autoReferenced: true`,** so the four predefined assemblies (`Assembly-CSharp`, `-firstpass`, `-Editor`, `-Editor-firstpass`) depend on all of them.
  - A private edit in `DeepWater.Sea` re-runs IL post-processing on about 20 assemblies, because post-processing reads its references' full DLLs.
  - A public edit recompiles all four predefined assemblies.
  - None of their code uses `DeepWater.Sea`.
- **Three IL post-processors run on every assembly:** Entities codegen, Jobs codegen and Burst.
- **`Assembly-CSharp`: 763 files, 97 k lines.** Obi 370, HurricaneVR 257, first-party legacy about 90 (`Scripts`, `_Archive`, `AerialArcade`, `JMAC`, `RH Testing Suite`).
- **`Assembly-CSharp-firstpass`: 403 files, 79 k lines** (RootMotion 280, RCC 80, HexabodyVR 26, NWH 10, FlightSimLite 7).
- **Editor scripts compile in Debug mode** (the Bee graph is `1900b0aEDbg`). That does not change compile time.

**Player builds.**

- **Cold shader compile:** 3,985 variants, 2,579 CPU-s, all D3D11 through FXC. Parallelism averaged 3.2× on 20 cores.
  - `HDRP/Lit`: 849 CPU-s for 88 variants.
  - `Hidden/HDRP/TemporalAA`: 775 CPU-s, 1,276 variants.
  - `HDRP/Water` 225, `CloudLayer` 131, `FinalPass` 124, Cesium tileset shaders 185 (108 variants).
  - The built-in `Standard` shaders: 96 CPU-s and 985 variants. HDRP cannot render them.
  - Four HDRP assets (the default plus three quality levels) differ in features, and HDRP compiles the union.
- **A code change re-runs Burst AOT:** Obi's Burst backend (24 files), the Reckoning spike, Sea State and the package jobs.
- **Warm builds** reuse `Library/ShaderCache` and `Library/PlayerDataCache`.

**Disk.**

- `Library` is 13 GB. `Artifacts` 4.9 GB, `PackageCache` 4.9 GB (Cesium 2.0 GB, Burst 0.85 GB, AI Assistant 0.55 GB), `BurstCache` 0.9 GB, `Bee` 0.74 GB.
- Both drives are NVMe. `C:` has 32 GB free (3 %).

### Incidents while measuring (kept on the record)

- **The bridge re-sent an inline `BuildPlayer` command about 75 times over 30 minutes.**
  - Each build blocked the main thread, and the relay re-sent its cached request on reconnect.
  - Stopped by entering play mode, so the next copy refused its own guard.
  - Every build succeeded, into git-ignored `Builds/BuildSpeed/GQ`.
  - The rule is now trap 12 in the `unity-mcp-workflow` skill.
- **`EditorApplication.delayCall` fired 262 s late** while the Editor was unfocused.
- **Auto-refresh is erratic while unfocused:** one edit was picked up in 7 s, the next in 237 s. Cycles therefore call `Refresh` explicitly.

## The full catalog: everything that could make Deep Water faster to work on (2026-10-25)

*SIX asked for any and everything, from preferences to methods nobody has tried. Each item gives its status:*
- *done*: committed on `perf/build-speed`;
- *measured*: an A/B number exists;
- *estimated*: arithmetic from measured parts, not run;
- *untested*.

*"SIX" in the "who" column means a Windows or account setting Claude must not change.*

**Where the day stands (Editor in the background, the state every MCP session works in):**
- Domain reload 46.5 → 7.4–7.8 s. Play entry 51 → 0.9 s.
- Private-edit compile 4.7 → 1.2–1.6 s; public-edit compile 13.5 → 3.1–3.4 s; a legacy-script edit, ready 67 → 11.4 s.
- Player build after a code change 107 → 54 s. Cold build 1,313 s, with ~5 min of it lost to the efficiency cores (measured before the stripping in round 7).
- Background builds (E2): a player without touching the Editor, 276 s the first time and 45 s when nothing changed. After a one-line code edit (E5): Release 84 s, Iterate (scripts only) 49 s.

### A. The machine (biggest wins, mostly settings)

| # | What | Gain | Status | Who |
|---|---|---|---|---|
| A1 | **Run Unity's process tree at full speed, not on the E-cores.** Windows 11 power throttling holds a background Unity on the 8 efficiency cores | Reload ×2.2 (16.3 → 7.4 s). Shader workers 5.6 → 13 cores. Cold build ~−5 min | measured (reload, cores); estimated (build) | see A1a–d |
| A1a | Build it in: a first-party Editor tool, "Full Speed". On load it opts `Unity.exe` out, and every few seconds every child process (shader workers, Bee, `csc`, ILPP runner, Burst). Windows-only, no admin, a menu toggle, logged | As A1, automatically, in every session | **done** (round 5) | — |
| A1b | Exempt the executables for good: `powercfg /powerthrottling disable /path "<exe>"` (admin) for `Unity.exe`, `UnityShaderCompiler.exe`, `bee_backend.exe`, `Unity.ILPP.Runner.exe`, `dotnet.exe`, `netcorerun.exe`, and Burst's compiler, under `C:\Program Files\Unity\Hub\Editor\6000.4.11f1\` | As A1, without a tool. Redo after each Unity upgrade | untested | SIX |
| A1c | Windows power mode "Best performance" while plugged in (Settings > System > Power). Currently Balanced | Less throttling everywhere | untested | SIX |
| A1d | Keep Unity focused during builds | As A1 for that build | follows from A1 | SIX |
| A2 | Microsoft Defender exclusions for the project folder, `Library`, the Unity install and `%LOCALAPPDATA%\Unity`. Or move the project to a Windows 11 **Dev Drive** (ReFS, with Defender's performance mode) | File-heavy phases: import, compile, Bee. Defender used 0.19 core during shaders, so likely small | untested | SIX |
| A3 | Free space: `C:` 3 % free (32 GB), `F:` 7 % | Avoids slow paths on a nearly full NVMe (Unity's global caches and Temp are on `C:`) | untested | SIX |
| A4 | Cooling: an OEM performance fan profile; the laptop on a stand | Sustained all-core clocks in long builds | untested | SIX |
| A5 | Open the project by its real path `F:\…` instead of the junction `C:\Users\SIX\Unity Projects` | May make auto-refresh reliable (the junction trap) | untested | SIX |

### B. Unity Editor settings and habits

| # | What | Gain | Status | Who |
|---|---|---|---|---|
| B1 | **Enter play mode without a domain reload** (the scene still reloads). Needs about 23 first-party static fields reset at play start, and a test that a seeded run gives the same Fix twice | Play entry 10.1 → 0.9 s | **done** (round 6) | — |
| B2 | FR2 (Find Reference 2): turn off its auto-refresh, or close its window | −1.4 s after every reload | measured cost | SIX or Claude |
| B3 | Preferences > General > Interaction Mode: "No Throttling" | May remove the ~7.8 s compile-to-reload floor seen while unfocused | untested | SIX |
| B4 | Preferences > General > Script Changes While Playing: "Recompile After Finished Playing" | Avoids the play-mode compile freeze (10+ min lost once) | known trap | SIX |
| B5 | Editor code optimization "Release" (the bug icon, bottom right). It is "Debug" now | Faster editor-side C#: tests, play-mode sim. No compile change | untested | SIX |
| B6 | Asset Pipeline: Parallel Import on | Big model and texture imports | untested | SIX |

### C. Project structure

| # | What | Gain | Status | Who |
|---|---|---|---|---|
| C1 | Remove Unity Version Control and untrack `.plastic/` | Reload −31 s | **done** (round 1) | — |
| C2 | First-party asmdefs off auto-reference | Compile ×2.8 (private edit), ×4 (public edit) | **done** (round 2) | — |
| C3 | Remove unused packages | Fewer of the 215 assemblies per reload | Visual Scripting **done** (round 3a); Cesium and PostProcessing v2 **done** (round 3b). ML-Agents kept: SENTINAL uses it | — |
| C4 | Asmdefs inside Obi and HurricaneVR (a vendor change) | Legacy-edit compile 6.6 → ~2.5 s | HurricaneVR removed instead (round 3b), which gave 1.7–2.0 s. Obi **done** (round 8): `Assembly-CSharp` 479 → 109 files | — |
| C5 | **Freeze vendor code into precompiled DLLs** (Obi, HurricaneVR, RootMotion, RCC, NWH): built once, then never recompiled or post-processed | `Assembly-CSharp` 763 → ~90 files; firstpass emptied | untested | SIX decides |
| C6 | Move unused vendor demo content out of `Assets/` (or into `Name~` folders, which Unity ignores): Obi samples, RCC demo, NWH demo, HurricaneVR examples, VFX Arsenal examples | Less to scan, import, compile and ship | **done** for the unused vendors and demo content (round 3b) | — |
| C7 | Audit every `Resources/` folder (RCC, Obi, NWH, Cesium): all of it ships, and its shaders compile in every build | `resources.assets` packaging took 140 s cold | measured cost | Claude |
| C8 | Startup hooks per reload: the AI Assistant bridge 1.9 s (vendor; report to Unity), Entities 0.55 s (locked by Reckoning) | Small | measured cost | — |

### D. Player builds

| # | What | Gain | Status | Who |
|---|---|---|---|---|
| D1 | **Burst AOT for one CPU target (AVX2) instead of SSE2 + SSE4.** Burst compiles every job once per target | Burst step 60.2 → 37.5 s (−38 %) per code-changed build. Needs an AVX2 CPU (any since 2013) | **done** (round 7) | — |
| D2 | Two build profiles. "Iterate": development build, scripts only when only code changed. "Release": as now. (Burst fast compilation, one quality level and no compression were left out: E5 says why) | After a code edit, a player in 49 s instead of 84 s | **done** (E5) | — |
| D3 | Scripts-only player builds when only code changed (`BuildOptions.BuildScriptsOnly`, development builds) | Build 39.5 → 19.6 s after a code edit | **done** (E5, the Iterate profile) | — |
| D4 | **Fewer shader variants**, for cold builds | Lower the 950 s shader phase | Claude, with SIX's look at visuals | |
| D4a | Turn off ray tracing in "HDRP High Fidelity" if it isn't wanted | `HDRP/Lit`'s DXR passes, ~290 s of compile time | **done** (round 7); cold-build gain not measured | — |
| D4b | 6000.4 **Shader Build Settings, keyword declaration overrides**: turn chosen HDRP keywords into runtime branching or material-usage variants, without editing HDRP | `TemporalAA` alone is 1,284 variants and 768 s | untested | Claude |
| D4c | A logged stripper for shaders HDRP can't render (built-in `Standard`, legacy PostProcessing) | 2,354 variants left out per build, `TemporalAA` (guarded) included | **done** (round 7, Lean Build) | — |
| D4d | Make the four HDRP assets agree, or ship one quality level in development builds (HDRP compiles the union of their features) | Unknown | untested | SIX decides |
| D5 | Unity Accelerator (free cache service) for imports and shaders, shared by the laptop and any second checkout | A fresh `Library` or another machine compiles nothing twice | untested | SIX installs; Claude configures |
| D6 | DXC, the faster shader compiler: 6000.4 has no project switch (only per-pass pragmas inside HDRP). A later Unity adds "Shader Compiler Backend Selection", which needs a DX12-only player | Faster per variant (Unity's docs) | blocked on a Unity upgrade | SIX decides |
| D7 | Keep Mono for development builds (IL2CPP adds minutes) | — | already so | — |

### E. Novel and experimental

| # | What | Gain | Status | Who |
|---|---|---|---|---|
| E1 | **"Full Speed" (A1a), as a project tool.** Unity tooling that manages Windows power throttling for its own process tree | ×2 on everything done in the background | **done** (round 5) | — |
| E2 | **A background cache warmer.** A second checkout (`git worktree`) with its own `Library` builds headless overnight, or when HDRP or shader files change, and feeds Accelerator. The Editor's cold builds become cache hits, and the Editor is never locked by a build | The Editor is never locked by a build | **built and measured** (E2 below); the shared cache still needs D5 | SIX installs D5 |
| E3 | **Slowest-variant-first pre-compile.** Unity compiles one shader at a time, and `HDRP/Lit`'s slowest variants (~48 s each) leave cores idle at the end. Compile them in parallel ahead of the build through `ShaderData.Pass.CompileVariant` | Removes the idle tails (780–900 s, 1,050–1,200 s) | **dead end** (E3 below): the API keeps nothing a build can reuse | — |
| E4 | **Hot reload** with open-source FastScriptReload (MIT): apply C# edits without a domain reload, even in play mode | Edit → running ~1 s for supported changes | **tried and dropped 2026-10-25:** play entry hung twice (see "E4: FastScriptReload") | — |
| E5 | `Library/Bee` and `Temp` on a RAM disk or Dev Drive | Small on NVMe | untested | SIX |
| E6 | Unity on CoreCLR (a future Unity 6.x): domain reloads go away | The reload, entirely | watch item | — |

## Rounds

*Each round records the change, its A/B numbers, the tests run, the decision and the seat. SIX picked every item on the menu on 2026-10-25.*

**Behaviour pin (before any change):** EditMode, all seven first-party test assemblies: **301 pass, 3 fail, of 304**. The 3 failures were there before this work:
- `ReckoningSpikeTests.Criterion2_RollPeriod_WithinTenPercentOfImo(A)`: variant A fails by design (R35).
- `GuidedFury.Tests.L3IntegratorTests.L3_AoaProducesLift_PitchedBodyPullsToward`
- `GuidedFury.Tests.SeekerTests.EndToEnd_L2WithConeSeekerAndProNav_AcquiresAndCloses`

Every round must leave exactly this result.

**Pin after rounds 3b–7 and E2–E4 (2026-10-25 20:55):** ten assemblies (the seven, plus Full Speed, Lean Build and Fast Play), 317 tests. Result: **314 pass, 3 fail, the same three**.
- The first run had a fourth failure: the same-Fix test. That Editor session had lost the project-wide Input Actions: `InputSystem.actions` was null though `EditorBuildSettings` pointed at `RHMILCOM.inputactions`, so `RangeInput` logged an error.
- After a domain reload the actions were back. They stayed set over two plays without a reload, enabled in play and disabled between plays. The Fast Play tests then passed 3/3 (40.5 s).
- Likely cause, not proven: that Editor had started on a package change (FSR's removal), before the asset could load.

**Pin after round 8 and E5 (2026-10-27 13:20, `main` at `2afbb624` plus this work):** twelve assemblies (Aviator and Gunnery are new on `main`), 466 tests. Result: **463 pass, 3 fail, the same three**.
- Another session's headless baseline that morning had five more failures (Air Wing and GQ prefab tests, and one Full Speed test). All of them pass in the Editor run.
- The Full Speed one was hardened (round 5). The prefab ones come from how the mirror imports models, and weren't investigated here.

### Round 1: remove Unity Version Control; untrack `.plastic/` (kept)

*Seat: Brendan Gregg (The Optimizer): one profile named the cost, so cut the cost, not the symptoms.*

- **Change:**
  - `com.unity.collab-proxy` removed from `Packages/manifest.json`; the lock file re-resolved.
  - `.plastic/` (5 files, a 2023 Plastic workspace) untracked and added to `.gitignore`.
  - The local folder renamed to `.plastic.pre-git-only-backup-20261025-1651`, not deleted.
- **A/B, domain reload (`RequestScriptReload`, same Editor session, GQ_Range open):**

| | Runs | Reload | `AwakeInstancesAfterBackupRestoration` | `BeginReloadAssembly` |
|---|---|---|---|---|
| A (just before) | 46.6, 56.7 s | median 51.6 s | 32.9–33.0 s | 2.6 s, 12.6 s (the stall) |
| B (after) | 15.1, 15.2, 15.5 s | **15.2 s** | 1.64–1.71 s | 2.5–2.8 s |

- **Result:** reload ×3.0 (46.5 s baseline median → 15.2 s).
  - The 10 s stall (5 of 15 reloads before) did not appear in the 4 reloads since, including the resolve reload at 19.2 s. It was very likely the plugin's retry thread too. That is likely, not proven.
- **Behaviour:** the change is Editor tooling only. It is covered by the test pin after Round 2.
- **Restore:** add `"com.unity.collab-proxy": "2.12.4"` back to the manifest, and rename the backup folder to `.plastic`.

### Round 2: first-party asmdefs off auto-reference (kept)

*Seat: Sam Newman (The Decoupler): an edge nothing uses is still an edge every build walks.*

- **Change:** `autoReferenced: false` in 20 first-party asmdefs:
  - Air Wing ×2, Carrier Ops ×2, General Quarters ×3, Guided Fury ×2, RH Aviator, RH Radar, RH Radar Suite ×2, RH Shared, DeepWater.Input ×2, Sea State ×4.
  - The test asmdefs and the Reckoning spike already had it.
- **What it proves:** the predefined assemblies (`Assembly-CSharp*`) compiled with 0 errors without them, so nothing in them used first-party code. `Unity_RunCommand` still compiles against first-party types (checked with `typeof(DeepWater.Sea.Core.SeaStateDirector)`).
- **A/B, the same one-line probe in `SeaStateDirector.cs`, explicit refresh, 2 runs each:**

| Edit | A compile | B compile | A → ready | B → ready |
|---|---|---|---|---|
| Private line | 3.66, 4.33 s (22 nodes) | **1.56, 1.19 s** (18 nodes) | 24.5, 24.3 s | 24.7, 24.8 s |
| Public line | 12.67, 13.57 s (67 nodes) | **3.40, 3.12 s** (51 nodes) | 34.0, 35.4 s | **25.6, 25.8 s** |

- **Result:** compile ×2.8 for a private edit and ×4.0 for a public one.
  - The private edit's "ready" did not move. In this harness the reload never starts sooner than ~7.8 s after the refresh call, whatever the compile time (see Instrument note below). A faster compile lands inside that floor.
- **Behaviour (R1 + R2):** 301 pass, 3 fail, the same three as the pin.
- **Instrument note:** with the Editor unfocused and driven through the MCP bridge, "Reloading assemblies after finishing script compilation" came at 7.5–8.0 s after the `MARK` in every cycle whose compile ended before that (4 of 4).

### Round 3a: remove Visual Scripting (kept, measured once)

- **Why it could go:** no C# references it, and no scene, prefab or asset references any of its 1,845 asset GUIDs (scanned with `pkg_refs.py`).
- **Cost of the change:** one full recompile (79 s, 1,381 nodes), then a 19.1 s reload, against 16.2 and 16.4 s just before.
- **Not A/B'd:** SIX asked to stop A/B-ing small gains and to focus on shader compilation.
- **Parked (SIX approved them, then redirected):** ML-Agents, Cesium and PostProcessing v2. Cesium and PostProcessing v2 went in round 3b; ML-Agents stays for SENTINAL.
  - ML-Agents: the legacy `SENTINAL.unity` holds 3 `BehaviorParameters`, with no Agent script anywhere.
  - Cesium: `Mapping.unity` and `Assets/CesiumSettings` use it, and its two tileset shaders cost 245 s of compile time in every cold build.
  - PostProcessing v2: NWH's `PostProcessingController.cs` imports it without a guard. The fix is `#if UNITY_POST_PROCESSING_STACK_V2` around the file, plus dropping that stale define.

### Round 3b: remove the vendors nothing uses; archive four legacy scenes (kept)

*Seat: Michael Feathers (The Maintainer): code nobody runs is still code every compile pays for.*

- **Change (SIX's picks), commit `a257e74a`:**
  - Removed through `AssetDatabase.MoveAssetsToTrash` (the Recycle Bin; git history keeps everything):
    - vendors: HurricaneVR, HexabodyVR, StreamDeckIntegration, the Input System samples, RootMotion, CesiumSettings, SceneTemplateAssets;
    - demo content of the kept vendors (NWH, RCC, SensorToolkit, FlightSimLite, VFX Arsenal);
    - stale `_Recovery` scenes.
  - Packages: `com.cesium.unity` (and its scoped registry) and `com.unity.postprocessing`. The stale `UNITY_POST_PROCESSING_STACK_V2` define was dropped from every target.
  - Helo's one RootMotion asset, `Zero Friction.physicMaterial`, moved to `_SCENES/Helo Assets` with its GUID.
  - OutdoorsScene, Mapping, Rocket and Rocket Man moved to `Assets/_Archive/Scenes_legacy/`, with a README and restore steps.
  - Kept, because CVN-78 FORD, DDG Engagement, SENTINAL and Helo need them: Obi, NWH, RCC, SensorToolkit, FlightSimLite, ML-Agents, AerialArcade, FDS Assets.
- **Result (Editor unfocused, Full Speed on):**

| | Before | After |
|---|---|---|
| `Assembly-CSharp` | 763 files (97k lines) | 479 files |
| `Assembly-CSharp-firstpass` | 403 files (79k lines) | 87 files |
| `Assembly-CSharp-firstpass-Editor` | 93 files | 0 |
| Legacy one-line edit (`CamRot.cs`), compile | 6.55 s | 1.68, 2.0 s |
| Legacy edit → ready | 67 s (round 0, throttled) | 11.8, 11.4 s |

- **Behaviour:** one full recompile, 0 errors. In the 10 kept scenes every referenced GUID resolves, except 4 in SENTINAL and Helo that were already missing before today.

### Instrumented cold player build (2026-10-25 17:18, Editor unfocused)

*Seat: Brendan Gregg (The Optimizer): put a clock on the phase and a sampler on every core.*

- **Setup:**
  - The Air Wing benchmark scene, `BuildOptions.CleanBuildCache`, written to `Builds/BuildSpeed/AirWingCold/`.
  - `Library/ShaderCache`, `Library/BurstCache/Windows-Intel` and `Library/PlayerDataCache` were renamed to `*.pre-coldtest-backup-20261025-1717`, not deleted. They are now stale duplicates for SIX to remove when he likes. `ShaderCache.db` was locked by the Editor, so it was left in place.
- **Result:** Succeeded, 0 errors, **1,313 s**, 354 MB. Steps, from `BuildReport`:

| Step | Seconds | What runs |
|---|---|---|
| Compile scripts (player) | 116.6 | `csc` + IL post-processing over ~90 assemblies, ~5.6 cores busy |
| Writing asset files | **1,065.6** | Packaging `globalgamemanagers.assets` 925.0 s and `resources.assets` 140.4 s. **Shader compilation runs here** |
| Postprocess built player | 95.5 | Bee player build, Burst AOT (3–10 cores) |
| Everything else | ~35 | Preprocess, scenes 4.1 s, built-in extras |

- **Shaders:**
  - 207 shaders, 4,555 variants, 2 local cache hits, 2,998 s of logged compile time over ~950 s: **3.2× parallel on 20 threads**.
  - Top: `HDRP/Lit` 982 s (105 variants), `Hidden/HDRP/TemporalAA` 768 s (1,284), `HDRP/Water` 318 s (122), the two Cesium tileset shaders 245 s (108), `CloudLayer` 135 s, `FinalPass` 128 s, `HDRISky` 89 s.
- **Why only 3.2×: Windows held the build on the efficiency cores.**
  - The i9-12900H has 6 P-cores (logical 0–11) and 8 E-cores (logical 12–19). With the Editor in the background, Windows 11 power throttling (EcoQoS) ran all 23 `UnityShaderCompiler` workers on the 8 E-cores.
  - Per logical processor, E-cores 12–19 sat at 100 % while P-cores 0–11 sat at 0–55 %, at 145–168 % clock. Each worker used ~0.33 core and had "Ready" threads (runnable, not scheduled). Not the disk (1 % busy), not Defender (0.19 core), not heat (121 % clock).
  - At +523 s the workers were opted out of power throttling, per process (`SetProcessInformation(ProcessPowerThrottling)`, the switch Task Manager's "Efficiency mode" uses; it ends with the process). All 20 logical processors went to 100 %, and the workers went from **5.6 to 12.4–13.2 cores**.
  - Later dips to 4–7 cores (780–900 s, 1,050–1,200 s) are a few slow `HDRP/Lit` variants compiling alone.
- **Estimate (not measured):** the whole build unthrottled would take ~1,000 s, saving ~4 min of the shader phase and ~1 min of the script compile.
- **Two wrong readings, reported before this measurement and kept on the record (methodology C33):**
  - "~80 % shaders" came from summing per-pass times. It was close, by luck.
  - "~115 s, cores saturated" came from the per-shader maximum. It was wrong.

### Round 4: run the Editor's process tree at full speed (measured; built as round 5)

*Seat: Brendan Gregg (The Optimizer): the machine was half idle; find out who holds the other half.*

- **Change (prototype, scratch only):** a watcher opted `Unity.exe` and every process it starts out of power throttling, every 2 s: 39 processes, including the 22 shader workers, `Unity.ILPP.Runner`, Package Manager and the MCP relay. Nothing in the project or in Windows settings changed. The effect ends when those processes exit.
- **A/B, domain reload (`RequestScriptReload`), Editor unfocused in both:**

| | Runs | Reload | `ProcessInitializeOnLoadAttributes` | `AwakeInstances…` |
|---|---|---|---|---|
| A (round 3a state, throttled) | 16.2, 16.4 s | 16.3 s | 4.38–4.42 s | 1.86–1.91 s |
| B (opted out) | 7.53, 7.27 s | **7.4 s** | 1.87–1.99 s | 0.85–0.86 s |

- **Result:** reload ×2.2. From the day's baseline: **46.5 s → 7.4 s (×6.3)**.
- **What it means:** with the Editor in the background, every Editor operation runs on E-cores. That covers compiles, reloads, tests and builds, and every session driving the Editor over MCP.
  - The numbers in rounds 0–3 are background numbers.
  - With the Editor in the foreground, Windows does not throttle it.

### Round 5: the Full Speed tool (kept)

*Seat: Cindy Sridharan (The Observer): a tool that changes how Windows schedules the Editor must say so, process by process.*

- **Change, commit `16632113`:** `Assets/Full Speed` (editor-only, `DeepWater.FullSpeed.Editor`, `autoReferenced: false`).
  - `ProcessTree`: pure tree logic over a process snapshot, cycle-safe.
  - `WindowsProcesses`: a Toolhelp32 snapshot, `Get/SetProcessInformation(ProcessPowerThrottling)`, and an AC-power check. Errors come back as Win32 text.
  - `FullSpeed` (`[InitializeOnLoad]`): a background timer (1 s) sets `Unity.exe` and every descendant to full speed, so children started during a blocking `BuildPlayer` are caught too.
    - It forgets exited ids. It hands everything back to Windows when turned off, or on battery unless allowed.
    - It warns once per executable on failure.
    - Preferences > Deep Water > Full Speed, and Deep Water > Full Speed > Report.
  - It changes no Windows setting. Each process's switch ends with the process.
- **Result:** on load it set 39 processes to full speed (`UnityShaderCompiler` ×22, the ILPP runner, Package Manager, the MCP relay…). Domain reload, one each: off 16.2 s, on **7.8 s** (×2.1), matching the round-4 prototype.
- **Tests:** `DeepWater.FullSpeed.Tests.Editor` 7/7 in 0.8 s: the tree logic, plus round trips against Windows on a real child process.
  - 2026-10-27: the exited-process test failed once in another session's headless run. It now allows 2 s for the error to come.
  - Likely cause, not reproduced: Full Speed's own scan briefly holds a handle to the child, which keeps the exited process alive.

### Round 6: play mode without a domain reload (kept)

*Seat: Leslie Lamport (The Invariant): same seed, same raid, same tick, the same Fix, or the change doesn't ship.*

- **Change, commit `aab42a66`:**
  - `ProjectSettings/EditorSettings`: Enter Play Mode Options on, Reload Domain off. The scene still reloads.
  - `Assets/Fast Play`:
    - `StaticStateTests` reflects over every first-party runtime assembly (11). A mutable static must be reset by a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method in its type, or be listed in `Allowed` with its reason. Stateless strategy singletons pass by rule.
      - The audit found 50 statics: lookup tables nothing writes, registries filled once, deterministic caches, null-checked lazy objects, scratch buffers. None needed a reset.
    - `SameFixAcrossPlaysTests` plays GQ_Range twice with no reload. It starts the saturation raid at core tick 50 from inside `FixedUpdate` and compares the core's Fix at tick 800.
    - Deep Water > Fast Play > Reload Domain On Play turns the reload back on, for vendor-heavy legacy scenes whose statics are not audited.
  - RH Testing Suite `TestRunner`: a play-start reset for its two static events and its results list (legacy `Assembly-CSharp`, outside the audit's reach).
- **Result (Editor unfocused, Full Speed on):** play entry, GQ_Range: **10.1 s with the reload → 0.9 s without**. It was 51 s at the day's baseline.
- **Tests:** `DeepWater.FastPlay.Tests.Editor` 3/3, then the same-Fix test again 1/1. Four plays in one domain gave Fix `0B9C4DD82783F5C5` every time, over 767–791 frames, so the core does not depend on frame timing.
- **Fixed on the way:**
  - The test first timed out, because an unfocused Editor barely ticks the player loop. It now sets `runInBackground`.
  - Then the raid start moved with frame timing, so the probe now starts the raid from inside `FixedUpdate`.

### Round 7: Lean Build shader stripping, ray tracing off, Burst AOT for AVX2 only (kept)

*Seat: Brendan Gregg (The Optimizer): don't compile what can never be drawn, or what never runs.*

- **Change (SIX's picks), commit `be8e8f86`:**
  - `Assets/Lean Build` (editor-only, `DeepWater.LeanBuild.Editor`): an `IPreprocessShaders` stripper driven by `LeanBuildSettings.asset`, with no lists in code. It leaves out:
    - `Hidden/HDRP/TemporalAA`, only when no camera in the build's scenes uses TAA. This is checked per scene as it builds, and TAA is kept if no scene has been seen yet;
    - the built-in `Standard` family;
    - the `Legacy Shaders/`, `Mobile/` and `Nature/` prefixes.

    Each build logs one line, and the full list goes to `Library/LeanBuild/last-build.txt`.
  - `HDRP High Fidelity.asset`: `supportRayTracing` 1 → 0.
  - `ProjectSettings/BurstAotSettings_StandaloneWindows.json`: `CpuTargetsX64` 64 (AVX2 only; the default 72 is SSE2 + AVX2). All first-party Burst jobs are `FloatMode.Deterministic`, so no result changes.
- **Result (Air Wing benchmark scene, each build after a one-line code change, Editor at full speed):**

| | Before | After |
|---|---|---|
| Variants left out | 0 | 2,354 (`TemporalAA` 1,284, `Standard` 676, `Standard (Specular setup)` 332, `Legacy Shaders/Diffuse` 30…) |
| Burst AOT (`GenerateNativePluginsForAssemblies`) | 60.2 s | 37.5 s (−38 %) |
| Build total | 107 s | 54 s (part of it an incremental script compile) |
| Player size | 356 MB | 302 MB (with round 3b) |

- **Behaviour:** the AVX2 player runs headless at 2.88 µs per step for 1 aircraft and 2.66 µs per aircraft for 20 (the Air Wing budget is 2.7), with no errors in `Player.log`. Policy tests 3/3.
- **Not measured:** the cold-build gain from the stripping and from ray tracing off. The only cold build timed came before this round.

### Round 8: Obi in its own assemblies (kept)

*Seat: Michael Feathers (The Maintainer): a vendor that never changes shouldn't be recompiled with every legacy script.*

- **Change (SIX's pick C4; a vendor change SIX approved):** three asmdefs, and no other edit to Obi.

| Asmdef | Covers | References | Auto-referenced |
|---|---|---|---|
| `Obi` | `Assets/Obi/Scripts`, 330 files | Burst, Collections, Mathematics; unsafe code allowed | yes: CVN-78's `ArrestingWireController.cs` (in `Assembly-CSharp`) uses `ObiRope` and `ObiRopeCursor` |
| `Obi.Editor` | `Assets/Obi/Editor`, 76 files | `Obi`; editor only | no |
| `Obi.Samples` | `Assets/Obi/Samples`, 40 files | `Obi`, `UnityEngine.UI` | no: nothing outside uses a sample class |

  - No version defines. Obi's backends follow the project's define symbols, as before: Standalone defines `OBI_ONI_SUPPORTED`, and the Burst backend stays compiled out (`OBI_BURST` is not defined). An asmdef that added `OBI_BURST` would switch on a solver that's off today.
- **Result:** `Assembly-CSharp` went from 479 to 109 files. Obi's 446 files compile into their own three assemblies, which an edit to a legacy script no longer recompiles. The one full recompile took 3.28 s (55 Bee nodes).
- **A/B, the same one-line edit to `CamRot.cs` (legacy `Assembly-CSharp`), Editor unfocused, Full Speed on, the Editor up 24.6 h:**

| Cycle | Compile (Bee) | `Csc`, `Assembly-CSharp` | IL post-processing, `Assembly-CSharp` | Domain reload | Another session's headless tests running |
|---|---|---|---|---|---|
| A2 | 1.73 s | 0.75 s | 0.43 s | 21.6 s | yes (started 11:53) |
| A3 | 1.86 s | 0.81 s | 0.46 s | 21.8 s | yes |
| B1 (warm-up after the change) | 2.14 s | 1.22 s | 0.25 s | 25.3 s | unknown |
| B2 | 2.75 s | 1.64 s | 0.35 s | 35.6 s | yes, 8.2 cores |
| B3 | — | 3.36 s | 0.26 s | — | yes |

  - A1 (3.06 s `Csc`) warmed up the compiler server; it isn't counted.
- **Verdict: kept on structure, as SIX approved.**
  - The IL post-processing of `Assembly-CSharp` fell from 0.43–0.46 s to 0.25–0.35 s.
  - The compile-time gain is **not shown**. The B runs overlapped another session's headless test runs in the background-build mirror, and `Csc` swung 0.75–3.4 s with them. Auto-refresh also raced the marks twice.
  - Per SIX's "scoped, no waiting" rule, the timing stopped there.
- **Behaviour:** 0 compile errors. All 115 Unity-object classes resolve to their class in the new assemblies (Obi 52, Obi.Editor 23, Obi.Samples 39, `Assembly-CSharp` 1). The script GUIDs didn't change, so scenes keep their components.
- **Found on the way:** domain reloads now take 21–36 s, against 7.4 s two days ago. The Editor had been up 24.6 h (the long-session drift in the `unity-perf-ab` skill), and `main` gained 106 commits and several assemblies since. Not investigated here.

### E2: background builds from a mirror (built)

*Seat: Sam Newman (The Decoupler): a player build is a separate job; the Editor should never wait for it.*

- **What it is:**
  - `Assets/Lean Build/Editor/BackgroundBuild.cs`: the menu Deep Water > Build > Build Active Scene In Background, and the headless entry point `BackgroundBuild.Run` (`-executeMethod`), which writes a report next to the player.
  - `tools/background-build/bg_build.ps1`:
    1. Mirrors `Assets`, `Packages` and `ProjectSettings` with `robocopy /MIR` into `<project> BG`, a sibling folder outside the repo. What's on disk is copied, so uncommitted edits and the untracked DAVFX pack come too.
    2. On the first run, seeds the mirror's `Library` from this project's.
    3. Runs a headless Unity on the mirror at below-normal priority.
    4. Copies the player back to `Builds/Background/<stamp>/`, which git ignores.
  - Guards, because `/MIR` deletes whatever the destination has that the source lacks: the source must be a Unity project, and the mirror must sit outside it.
  - The seed leaves out `EditorInstance.json` (it would make the mirror think the project is already open), the database lock files, and `Bee` (its build graph holds absolute paths).
- **Two runs, GQ_Range, Editor not involved:**

| Phase | Run 1 (seeded Library) | Run 2 (nothing changed) |
|---|---|---|
| Mirror (7.2 GB) | 17 s | 0 s |
| Library seed (9.6 GB, no file failed) | 69 s | — |
| Headless Unity: first refresh | 53.5 s (a re-check, not a reimport) | 17.2 s |
| Headless Unity: build | 107.9 s (scripts 37.3, scenes 7.2, writing assets 12.0, post-process 42.7) | 17.5 s |
| **Start to player** | **276 s** | **45 s** |

  - Both runs: 350 MB, 0 errors. Lean Build left out the same 2,354 variants.
  - The headless Unity has no Hub token, so it logs licensing errors and falls back to the offline license.
- **Not measured:** a run after a code change. It would add the incremental script compile and Burst AOT, which were 54 s together in round 7.
- **Found on the way (each cost one failed run):**
  - Windows PowerShell 5.1 leaves `$PSScriptRoot` empty inside `param()` defaults. The first run resolved the project as `F:\`; robocopy stopped with code 16 and copied nothing. Hence the guards.
  - `Tee-Object` writes UTF-16 in 5.1, so the script now logs through one UTF-8 helper.
  - `Start-Process -PassThru` reports no exit code unless the process handle is opened while it runs.
- **Still open:** Unity Accelerator (D5). SIX installs it; then both projects share one import and shader cache.

### E3: compile the slowest `HDRP/Lit` variants first (dead end)

*Seat: Kent Beck (The Tester): one cheap experiment before building the machinery.*

- **The question:** does `ShaderData.Pass.CompileVariant` leave anything a later player build can reuse? If not, pre-compiling can't fill the build's idle cores.
- **Where builds keep compiled shaders in 6000.4:**
  - `Library/ShaderCache/` holds folders for compute, ray-tracing and built-in shaders only.
  - `ShaderCache.db` (SQLite) holds only shader errors (37 rows).
  - Regular shaders' bytecode lives inside the built `globalgamemanagers.assets` in `Library/PlayerDataCache`, reused whole when nothing changed.
- **The experiment:** `HDRP/Lit`, D3D, `StandaloneWindows64`, the same keywords twice. The files under `Library` were listed before and after.

| Variant | 1st compile | 2nd, identical | Persisted |
|---|---|---|---|
| `Forward` fragment, 74 KB | 6.31 s | 4.64 s | nothing (only the Editor's `SourceAssetDB` and undo files changed) |
| `GBuffer` fragment, 6.8 KB | 0.32 s | 0.30 s | nothing |

- **Verdict: FAILED.** The API returns bytecode and keeps nothing, in memory or on disk. A build would compile the same variant again.
  - What does carry compiled shaders forward: the seeded mirror of E2, and a shared cache (D5).
  - A side finding: `Forward` refuses a variant without one keyword from each of HDRP's required groups ("Undefined punctual shadow filter algorithm").

### E4: hot reload with FastScriptReload (tried, dropped)

*Seat: Nadia Eghbal (The Resilient): a tool that can hang the Editor, with no timeout and no fallback, costs more than the reload it saves.*

- **What it is:** FastScriptReload 1.8.0 (MIT), added as a git package (`com.handzlikchris.fastscriptreload`, commit `f852ac4f`).
  - In play mode it compiles each changed file with Roslyn and redirects the old methods to the new ones, with no domain reload.
  - It compiled cleanly with the project (0 errors).
- **First load: a modal dialog held the main thread for about 40 minutes.**
  - Its `[InitializeOnLoad]` code asked about Auto Refresh in a native modal dialog. The MCP bridge went silent until the dialog was answered.
  - Recorded as trap 13 in the `unity-mcp-workflow` skill.
  - Auto Refresh was set from Enabled (1) to Enabled Outside Playmode (2), as hot reload needs.
- **Play entry hung twice, in GQ_Range.** Before FSR, play entry here took 0.9 s (round 6, no domain reload).

| Try | Editor | What the Editor did | Ended |
|---|---|---|---|
| 1 (19:54) | The session that installed FSR, just after the dialog | Log stops at "Entering Playmode with Reload Domain disabled". Not Responding; 0.17 s of CPU in 5 s | Killed after ~30 min |
| 2 (20:29) | A fresh Editor start (FSR loaded with no dialog) | Not Responding from the first sample, every 2 s over 150 s. 10–20 s of CPU per 2 s (5–10 cores) for ~2 min, then 0.05–0.11 s per 2 s, still not responding at 20:36 | Killed |

- **Cause: not diagnosed.**
  - From outside there is no managed stack.
  - The second Editor's log never reached `Editor.log`, because a Unity 7 Editor creating another project had taken the file over.
  - Between the 0.9 s play entries and the hangs, FSR is the only change that runs code at play entry. The other two are the Auto Refresh preference and `BackgroundBuild.cs`, which is a menu item only.
  - Verdict: **FAILED** in this project (Unity 6000.4.11f1, HDRP 17.4, play mode without a domain reload).
- **Removed:**
  - `Packages/manifest.json` and `packages-lock.json` were restored to `perf/build-speed`'s versions.
  - FSR left no other files and no scripting defines. Its own EditorPrefs remain, unused.
  - The FSR versions of both files are kept in the session scratchpad.
  - Auto Refresh was put back to Enabled (1).
- **After removal (fresh Editor start, GQ_Range):** play entry works again, with no hang.
  - Timed with a `playModeStateChanged` hook, from the request to `EnteredPlayMode`: **7.4 s** for the first play after the start (cold), **2.5 s** for the second (warm).
  - The round-6 0.9 s came from a different timer, so these two numbers aren't like for like.
- **Revisit when:** an FSR release states Unity 6.4 support, or Unity's CoreCLR Editor (E6) makes the question moot.

### E5: Release and Iterate profiles, and scripts-only builds (built)

*Seat: Martin Fowler (The Extender): a new kind of build should be a new asset, not a new code path.*

- **What it is (SIX's picks D2 and D3, 2026-10-27):**
  - `LeanBuildProfile` assets in `Assets/Lean Build/Profiles`:
    - `Release`: a full build, as a player ships.
    - `Iterate`: a development build that builds only the scripts when nothing but code changed.
  - `BuildPlan` and `ChangeSet` (pure, tested): they turn a profile and a change list into a full or a scripts-only build, and say why.
  - Menu items Deep Water > Build > Build Active Scene In Background (Release) and (Iterate), and `bg_build.ps1 -Profile`.
  - **Per-profile change lists:**
    - Each profile keeps the files changed since its own last successful build, in `<mirror>/Library/LeanBuild/pending-<profile>.txt`.
    - A mirror copy appends to every list, and a build empties only its own. So a Release build in between can't hide a texture change from Iterate.
    - A file counts as changed only if its content differs (SHA1). Robocopy goes by size and time, and the Editor re-saves settings files with identical content: 10 such files in each run below. Without the hash check, scripts-only would almost never fire.
  - **Mirror lock:** another session ran headless tests in `Deep Water BG` (2026-10-27). The script now stops while a Unity holds the mirror's `Temp\UnityLockfile`, and these runs used a second mirror, `Deep Water BG2`.
  - The output is fixed per profile and scene (`Builds/Background/<profile>/<scene>/`), because a scripts-only build patches the last build in place.
- **Runs, GQ_Range, in `Deep Water BG2`, Editor not involved. Other sessions' edits kept landing in the working tree:**

| Run | What changed since the profile's last build | Plan | Start to player | Build | Scripts | Data | Post-process (Burst) |
|---|---|---|---|---|---|---|---|
| R0 Release | first build in a new mirror | full | 631 s | 567.2 s | 60.0 s | 212.2 s | 273.2 s (cold) |
| I0 Iterate | first Iterate build | full: no earlier build | 260 s | 225.4 s | 128.9 s | 4.8 s | 82.6 s |
| R1 Release | 1 code edit, 10 settings files | full | 103 s | 73.4 s | 9.8 s | 4.5 s | 51.8 s (48.1) |
| I1 Iterate | 199 files (another session's archive clean-up) | full: `README.md` is not code | 132 s | 94.8 s | 12.9 s | 4.4 s | 70.5 s |
| **I2 Iterate** | **1 code edit** | **scripts only** | **49 s** | **19.6 s** | 7.5 s | skipped | 9.5 s (5.0) |
| R2 Release | 40 files | full | 173 s | 125.4 s | 23.3 s | 5.0 s | 73.4 s (63.3) |
| **R3 Release** | **1 code edit** | full | **84 s** | **39.5 s** | 10.0 s | 6.8 s | 12.0 s (6.8) |

- **Result:** after a one-line code edit, Iterate gives a player in 49 s against Release's 84 s, start to player. The build itself takes 19.6 s against 39.5 s (×2.0).
- **Checks:**
  - Burst ran in the scripts-only build (`bcl.exe`, 5.0 s). It found nothing to recompile, since the edited assembly has no Burst jobs.
  - Both players run 15 s headless. Each logs the same one exception (no compute shaders under `-nographics`), and the Iterate player logs its development-build markers. That's a start-up check, not a gameplay check.
  - Tests: `BuildPlanTests` 3/3.
- **Not built: Burst fast compilation for Iterate.** On a code-only edit Burst took 5.0–6.8 s, so its fast mode has little left to save. Whether it changes results in `FloatMode.Deterministic` jobs is unproven.
- **Next target, not built:** about 29 s of every run is the headless Unity starting, refreshing and reloading. A Unity kept warm between builds would remove most of it.

### Incident: a package made App UI editor-only, and every player build failed (fixed, 2026-10-27)

*Seat: Nadia Eghbal (The Resilient): a dependency's default must not decide what ships.*

- **Symptom:** the first R0 failed in the player-script compile. GQ_Range's `RangeScreen.cs` and `DamageBoard.cs` gave CS0234: `Unity.AppUI` does not exist. A player build from the Editor would have failed the same way.
- **Cause:**
  1. The ML-Agents 2.0.2 → 4.1.0 upgrade that morning (`6459f22b`) brought the Inference Engine (`com.unity.ai.inference`).
  2. Its model visualizer ships `App UI Settings.asset` with `editorOnly: 1`.
  3. App UI takes the settings registered in `EditorBuildSettings`, or else the first `AppUISettings` asset it finds, packages included. So it took that one, registered it, and added `APP_UI_EDITOR_ONLY` to the Standalone defines, which strips App UI from players.
  4. Before the upgrade there was no such asset, and App UI ran on its defaults (`editorOnly` false).
- **Fix:**
  - `Assets/Settings/App UI Settings.asset`, with App UI's defaults, registered as `com.unity.dt.app-ui` in `EditorBuildSettings`.
  - The define removed from Standalone, as App UI does itself on its next delayed call.
  - `ProjectSettings.asset` is not committed: its only remaining difference from `main`, `SENTIS_ANALYTICS_ENABLED`, comes from the Inference Engine package.
- **Verified:** R0 and every later build compiled the player scripts, and the Standalone defines no longer list `APP_UI_EDITOR_ONLY`.
