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

**Where the day stands (Editor in the background, the state every MCP session works in):** domain reload 46.5 → 7.4 s. Private-edit compile 4.7 → 1.2–1.6 s; public-edit compile 13.5 → 3.1–3.4 s. Cold build 1,313 s, with ~5 min of it lost to the efficiency cores.

### A. The machine (biggest wins, mostly settings)

| # | What | Gain | Status | Who |
|---|---|---|---|---|
| A1 | **Run Unity's process tree at full speed, not on the E-cores.** Windows 11 power throttling holds a background Unity on the 8 efficiency cores | Reload ×2.2 (16.3 → 7.4 s). Shader workers 5.6 → 13 cores. Cold build ~−5 min | measured (reload, cores); estimated (build) | see A1a–d |
| A1a | Build it in: a first-party Editor tool, "Full Speed". On load it opts `Unity.exe` out, and every few seconds every child process (shader workers, Bee, `csc`, ILPP runner, Burst). Windows-only, no admin, a menu toggle, logged | As A1, automatically, in every session | prototype proven (`ecoqos_optout.ps1`) | Claude |
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
| B1 | **Enter play mode without a domain reload** (the scene still reloads). Needs about 23 first-party static fields reset at play start, and a test that a seeded run gives the same Fix twice | Play entry ~9 s (est. after A1) → ~2 s | approved (round 5), not built | Claude |
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
| C3 | Remove unused packages | Fewer of the 215 assemblies per reload | Visual Scripting **done**. ML-Agents, Cesium (also −245 s of compile per cold build), PostProcessing v2 parked | Claude |
| C4 | Asmdefs inside Obi and HurricaneVR (a vendor change) | Legacy-edit compile 6.6 → ~2.5 s | approved, not built | Claude |
| C5 | **Freeze vendor code into precompiled DLLs** (Obi, HurricaneVR, RootMotion, RCC, NWH): built once, then never recompiled or post-processed | `Assembly-CSharp` 763 → ~90 files; firstpass emptied | untested | SIX decides |
| C6 | Move unused vendor demo content out of `Assets/` (or into `Name~` folders, which Unity ignores): Obi samples, RCC demo, NWH demo, HurricaneVR examples, VFX Arsenal examples | Less to scan, import, compile and ship. Needs a usage audit first | untested | SIX decides |
| C7 | Audit every `Resources/` folder (RCC, Obi, NWH, Cesium): all of it ships, and its shaders compile in every build | `resources.assets` packaging took 140 s cold | measured cost | Claude |
| C8 | Startup hooks per reload: the AI Assistant bridge 1.9 s (vendor; report to Unity), Entities 0.55 s (locked by Reckoning) | Small | measured cost | — |

### D. Player builds

| # | What | Gain | Status | Who |
|---|---|---|---|---|
| D1 | **Burst AOT for one CPU target (AVX2) instead of SSE2 + SSE4.** Burst compiles every job once per target | Burst step ~−50 % (66 s → ~33 s per code-changed build), faster code. Needs an AVX2 CPU (any since 2013) | estimated | Claude |
| D2 | Two build profiles. "Iterate": development build, Burst *Fast Compilation*, one CPU target, one quality level, no compression. "Release": as now | Iteration builds much faster; release unchanged | untested | Claude |
| D3 | Scripts-only player builds when only code changed (`BuildOptions.BuildScriptsOnly`, development builds) | Skips data packaging | untested | Claude |
| D4 | **Fewer shader variants**, for cold builds | Lower the 950 s shader phase | Claude, with SIX's look at visuals | |
| D4a | Turn off ray tracing in "HDRP High Fidelity" if it isn't wanted | `HDRP/Lit`'s DXR passes, ~290 s of compile time | estimated | SIX decides |
| D4b | 6000.4 **Shader Build Settings, keyword declaration overrides**: turn chosen HDRP keywords into runtime branching or material-usage variants, without editing HDRP | `TemporalAA` alone is 1,284 variants and 768 s | untested | Claude |
| D4c | A logged stripper for shaders HDRP can't render (built-in `Standard`, legacy PostProcessing) | ~100 s of compile time | estimated | Claude |
| D4d | Make the four HDRP assets agree, or ship one quality level in development builds (HDRP compiles the union of their features) | Unknown | untested | SIX decides |
| D5 | Unity Accelerator (free cache service) for imports and shaders, shared by the laptop and any second checkout | A fresh `Library` or another machine compiles nothing twice | untested | SIX installs; Claude configures |
| D6 | DXC, the faster shader compiler: 6000.4 has no project switch (only per-pass pragmas inside HDRP). A later Unity adds "Shader Compiler Backend Selection", which needs a DX12-only player | Faster per variant (Unity's docs) | blocked on a Unity upgrade | SIX decides |
| D7 | Keep Mono for development builds (IL2CPP adds minutes) | — | already so | — |

### E. Novel and experimental

| # | What | Gain | Status | Who |
|---|---|---|---|---|
| E1 | **"Full Speed" (A1a), as a project tool.** Unity tooling that manages Windows power throttling for its own process tree | ×2 on everything done in the background | prototype proven | Claude |
| E2 | **A background cache warmer.** A second checkout (`git worktree`) with its own `Library` builds headless overnight, or when HDRP or shader files change, and feeds Accelerator. The Editor's cold builds become cache hits, and the Editor is never locked by a build | A cold build becomes ~1–2 min | untested; needs D5 | SIX and Claude |
| E3 | **Slowest-variant-first pre-compile.** Unity compiles one shader at a time, and `HDRP/Lit`'s slowest variants (~48 s each) leave cores idle at the end. Compile them in parallel ahead of the build through `ShaderData.Pass.CompileVariant` | Removes the idle tails (780–900 s, 1,050–1,200 s) | research: unknown whether it fills the build's cache | Claude |
| E4 | **Hot reload** with open-source FastScriptReload (MIT): apply C# edits without a domain reload, even in play mode | Edit → running ~1 s for supported changes | untested; determinism risk for sim code | SIX decides |
| E5 | `Library/Bee` and `Temp` on a RAM disk or Dev Drive | Small on NVMe | untested | SIX |
| E6 | Unity on CoreCLR (a future Unity 6.x): domain reloads go away | The reload, entirely | watch item | — |

## Rounds

*Each round records the change, its A/B numbers, the tests run, the decision and the seat. SIX picked every item on the menu on 2026-10-25.*

**Behaviour pin (before any change):** EditMode, all seven first-party test assemblies: **301 pass, 3 fail, of 304**. The 3 failures were there before this work:
- `ReckoningSpikeTests.Criterion2_RollPeriod_WithinTenPercentOfImo(A)`: variant A fails by design (R35).
- `GuidedFury.Tests.L3IntegratorTests.L3_AoaProducesLift_PitchedBodyPullsToward`
- `GuidedFury.Tests.SeekerTests.EndToEnd_L2WithConeSeekerAndProNav_AcquiresAndCloses`

Every round must leave exactly this result.

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
- **Parked (SIX approved them, then redirected):** ML-Agents, Cesium and PostProcessing v2.
  - ML-Agents: the legacy `SENTINAL.unity` holds 3 `BehaviorParameters`, with no Agent script anywhere.
  - Cesium: `Mapping.unity` and `Assets/CesiumSettings` use it, and its two tileset shaders cost 245 s of compile time in every cold build.
  - PostProcessing v2: NWH's `PostProcessingController.cs` imports it without a guard. The fix is `#if UNITY_POST_PROCESSING_STACK_V2` around the file, plus dropping that stale define.

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

### Round 4: run the Editor's process tree at full speed (measured; the tool is not built yet)

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
