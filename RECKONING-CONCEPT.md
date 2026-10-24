# Reckoning — the DGPR concept for Deep Water

> **THEORETICAL — NOTHING BUILT**, with two exceptions, both measured:
> - the physics spike (`feat/reckoning-physics-spike`: `fb25675` + `85cae2b`, local, not pushed);
> - the Sea State buoyancy fix that spike surfaced (`8c82dd9` on `feat/environment-ocean`, local, not pushed).
>
> Written 2026-10-23 from SIX's talk-through with Claude: 20 structural questions, then five follow-up threads and a spike. Nothing here is a plan until SIX says so.

**This document's one job:** record what DGPR means for Deep Water and every decision SIX has made about it, so a build can start from it later without re-asking. It is not a roadmap, and it is not a spec.

---

## 1. The idea in plain language

SIX's definition, verbatim (2026-10-23):

> **Deterministically Governed Procedural Responsiveness (DGPR):** A simulation architecture in which observable behaviors and environmental responses are generated dynamically from deterministic state transitions, enabling complex, causally coherent, and reproducible interactions without requiring explicit authoring of every possible outcome.
>
> "Like it's a sim. But more like a game. Or sim Playground… The rules of the environment should be respectively physics based. But it should be light and procedural where players can interact with the world's dynamic ecosystem."

In one paragraph: the world is a small set of physics-based rules running on one deterministic clock. Nothing that happens is written as an outcome. Players, AI captains and a sandbox "god" all change the world the same way, by issuing recorded orders. The rules carry the consequences forward, and everything you see is a readout of that state. The same seed and the same orders give the same world, every run. You can always ask "why did that happen?" and get an answer built from the rules' own numbers.

**The test of a DGPR world: something interesting happens that nobody wrote.** Here is the example that set the direction:

1. A destroyer runs at 30 knots.
2. Its hull churns the water. Churned water is just another source of foam, in the same foam field the wind streaks already use.
3. The wake lingers for minutes and drifts into the streaks.
4. A patrol aircraft spots the wake before it spots the ship.
5. The player learns to slow down in a calm sea to hide.

No one authored "wakes give you away". It falls out of three rules: hulls break water, foam persists, sensors see foam. In a rough sea the background whitecaps hide the wake. That is a second lesson nobody wrote.

A wording note: "procedural" usually means generated content such as terrain or levels. What DGPR describes is what game designers call **systemic** design: rules producing behaviour, as in Breath of the Wild's chemistry, Teardown or Kerbal, with determinism as the backbone. DGPR stays the doctrine's name; "systemic" is the design term it belongs to.

## 2. Vocabulary (locked 2026-10-23)

Family 1, navigation. Every word is literally true of what the part does.

| Word | What it is | In code | Why the word fits |
|---|---|---|---|
| **DGPR** | The formal doctrine | — | SIX's coinage; used in the methodology and in this doc |
| **reckoned** | The everyday word for a system that meets the bar in §3 | — | "Is the radar reckoned yet?" |
| **Reckoning** | The core module | `DeepWater.Reckoning` | Dead reckoning computes where you are now from the recorded past alone. "Reckoning" also means consequences arriving |
| **Chronometer** | The single world tick | `Chronometer` | The clock that made reliable navigation possible; one clock everyone trusts |
| **Watch** | A system's rate: every Nth tick | `Watch` | How a ship divides time |
| **Order** | A command: the only way anything changes the world | `Order` | Captains, players and the god all give Orders |
| **Log** | The recording of every Order | `OrderLog` (avoids Unity's `Debug.Log`) | Everything is in the log |
| **Fix** | The fingerprint of world state each tick | `Fix`, `FixCheck` | A navigator's fix checks the reckoning against reality: two runs, same Fix |
| **Inquiry** | "Show me why", in play | `Inquiry` | The navy's own tradition for asking what happened |
| **Board of Inquiry** | The full after-action replay with every cause | — | Where every gap is filled in |

## 3. What "reckoned" means

**A system is reckoned when all three hold:**
1. **It acts only through Orders on its Watch.** Nothing reaches into the world from anywhere else.
2. **Everything it changes is in the Log and covered by the Fix.** Run it twice and the fingerprints match.
3. **Every notable event it causes has an Inquiry chain with numbers.** It can answer "why".

**Unreckoned is allowed for anything that changes no outcome:** GPU foam, particles, audio, camera shake. The sea already splits this way: its physics is reckoned in spirit (deterministic, CPU), and the foam you see is presentation drawn from it.

**Two numbers keep it honest** (both are proposed metrics, not yet measured):
- the share of outcome-changing systems that are reckoned;
- the share of notable events whose Inquiry chain is complete ("causal fidelity").

## 4. How the world is put together

### 4.1 The core is domain-free; domains are rule packs
Reckoning knows nothing about water. The Chronometer, Watches, Orders, Log, Fix and Inquiry work for any domain. Each domain plugs in as a pack of rules:

| Pack | State today |
|---|---|
| Sea (spectral sea, buoyancy, foam) | **Exists** (`Assets/Sea State/`); deterministic sea, `FloatMode.Deterministic` Burst kernels |
| Vessels (six-axis ship motion) | To build; the spike's variant B is the prototype |
| Local wave sources (dropped waves, wakes, later splashes) | To build |
| Weapons | Guided Fury was built in this spirit (pure core, fixed step); reckoned status unaudited |
| Sensors (radar, EO/IR) | RH Radar Suite and JMAC; determinism unaudited |
| Captains (AI) | To build: simple deterministic captains |
| Atmosphere (wind field, fronts, gusts) | Later (R21); the sea keeps a socket for it |

### 4.2 The design surface is shared state, not behaviours
Rules stay small and talk only through shared quantities: the wave field, foam coverage, sea conditions, wind, contacts. The sea already publishes one (`SeaConditions`), read by buoyancy, foam and radar clutter. Every rule that reads and writes shared state multiplies the possible interactions; a rule that calls another rule directly does not.

Playing a minute of each toy in our heads gave a rule list of about eight: sea, wave sources, ship motion, buoyancy, foam and wakes, sensors, captains, weapons. Everything else is scenario data. **"Light" means few rules and deep combinations.**

### 4.3 One heartbeat, a written order
The Chronometer calls every system in a written-down order: sea, then ships, then sensors, then captains. Each system runs on its own Watch, a whole-number multiple of the base tick: physics every tick at 50 Hz, sensors every 2nd, captains every 5th (10 Hz). The order is part of the rules, and the Fix test enforces it. This replaces today's reliance on `FixedUpdate` order attributes. Proposed: one `FixedUpdate` driver steps the Chronometer.

### 4.4 Everyone is a player
Captain, commander, god and every AI captain act the same way: by issuing Orders on the tick ("course 270", "speed 30 kn", "fire"; in god mode "sea state 6", "spawn frigate", "drop a wave here"). Switching roles changes only which Orders you may give, and to what. The world never knows who is human.

A non-deterministic mind (a human, or later an LLM-driven captain) is allowed as an input. It decides, the decision is logged as an Order, and replay plays the Order back.

### 4.5 Determinism, and what it buys
- **Replay** of a whole engagement from one Log. **Rewind** later at no redesign cost.
- **Multiplayer later, nearly free:** only Orders travel, and a Fix mismatch shows the exact tick two machines diverged.
- **Every bug reproduces.** AI becomes testable the way the sea is.
- **Speed changes how fast you watch, never what happens.** At 4× the simulation runs four times as many identical steps per second. The ceiling is CPU: max fast-forward ≈ 1 ÷ (50 × tick cost).
- **Save = snapshot + Log.**

### 4.6 Fog of war: one true world, one picture per role
Each role sees its own picture of the world, produced by its sensors. The picture is itself made by deterministic rules: a detection is a rule firing, with numbers, not a flag. Captains act on their picture, not on the truth, so they play fair and weather can fool them the same way it fools you. The god sees all.

### 4.7 Local wave sources: one mechanism, three features
"Drop a wave" (god mode) and "a wake rocks the next ship" are both local wave sources. These are waves that start somewhere specific, as opposed to the ocean-wide sea. They are summed with the spectral sea in the same deterministic code, so buoyancy feels them and the renderer draws them. A dropped wave spreads as a real expanding ring, with longer waves outrunning shorter ones, so it reaches distant ships at physically correct times. The same mechanism later carries shell splashes and blasts.

### 4.8 Wakes all the way
- **Breadcrumbs:** each hull drops position, width and strength every tick. They age, spread, drift with the surface drift Sea State already simulates, and fade. Rough seas hide them behind background whitecaps.
- **Sensors read the breadcrumbs; the renderer draws them** into the foam field.
- **The visible V:** the Kelvin wake has a fixed 19.5° half-angle in deep water for any ship.
- **Wake waves rock other ships** as local wave sources.
- **Fidelity:** plausible first (a known shape scaled by an authored strength, flagged AUTHORED). Derived later from hull geometry with Michell's thin-ship integral.

### 4.9 Ship motion
Six-axis motion (surge, sway, heave, roll, pitch, yaw) in our own deterministic code on the existing sea. Added mass and wave damping give realistic roll and pitch periods, checked against the IMO roll-period formula. Unity objects follow the reckoned state, smoothed for display. Small stiff bodies (a 2 m buoy) get extra sub-steps inside their Watch. The DDG sway deferred from the Sea State work becomes its first user.

### 4.10 Inquiry, "show me why"
**A cause record** holds four things:
- what happened, to what, at which tick;
- which rule produced it;
- the numbers that rule used, with its thresholds;
- links to the events or Orders behind those numbers.

The links form a cause graph. Every chain ends at one of three roots:
- an **Order** (who gave it);
- the **scenario** (how the world started);
- **chance** (which seeded draw).

Luck is named, never hidden: "gust, chance (seed 1442, draw 88)".

**Only notable events are recorded:** thresholds crossed and discrete happenings. Examples: a detection gained or lost, a hit or miss, a limit exceeded, an Order issued or completed. That is a few hundred small records a minute. Live play keeps a rolling window; a replay keeps the whole round.

**How it reads.** One plain sentence built from the rule's own numbers, never a story written to sound good. This example is illustrative:

> Spotted at 3.1 km: wake contrast 0.42 > threshold 0.30, because sea state 3 whitecaps cover only 1.2 %. Wake laid down 94 s ago at 28 kn.

- Every number is a link to its own cause; hovering shows its formula.
- In the world, the things involved light up.
- A rule that recorded no reasons gets "unrecorded", never a guess. That gap is exactly what causal fidelity counts.

**Under fog of war:**
- In play, Inquiry answers only from what your role knows. Your own events are fully explained.
- Other sides' events show only what you observed, with the gap marked: "aircraft turned toward you at 14:02, reason unknown to you".
- The Board of Inquiry fills every gap after the round.

**Captains are legible.** A captain is a small set of named rules ("hide the wake", "close to gun range", "turn into the sea"). Each has named inputs and thresholds, recorded when it fires. Illustrative:

> Captain Vega slowed to 8 kn because: aircraft contact at 9 km on radar; sea state 2, so the wake is visible; rule *hide the wake*.

### 4.11 Beyond the sea (direction, later)
The atmosphere becomes the upstream cause of almost everything, kept light:
- a coarse wind field (kilometre cells, about 1 Hz);
- fronts as moving lines;
- seeded Dryden or von Kármán gusts;
- visibility, cloud, rain, near-surface temperature and humidity.

Sea state then stops being a dial and becomes an output: wind over fetch and duration grows the sea, and what is left becomes swell. The god keeps a labelled override.

Cross-domain chains this enables (illustrative):
- **A veering wind.** The wind veers 60°; forty minutes later a cross sea has built, the destroyer rolls 6°, and the helicopter cannot land because its deck-motion limit is 5°.
- **The evaporation duct.** A warm, calm day thickens it, and radar range on a surface ship goes from 25 to 40 km.
- **A sea-skimming missile.** In sea state 6 it must fly higher to clear the crests, so it is easier to detect.
- **Carrier landings.** Deterministic deck motion, plus wind over the deck and the island's turbulence, plugs into Carrier Ops' landing-mirror (FLOLS) work.

Land works the same way: rain makes mud, mud slows vehicles, wind raises dust, dust cuts visibility.

## 5. The three toys

The toys are scenarios on one world, not separate games: same rules, different starting state, roles and win condition. Each toy is a data asset, built in this order because each leans on the last.

| Toy | The game | What it adds | Feel |
|---|---|---|---|
| **1. Sandbox** | Curiosity: "what happens if…" | Sea, wave sources, ship motion, buoyancy, cause records, god tools | Free camera; time bar (pause, 1×, 4×, 16×); tool palette (spawn, weather brush, push, drop a wave, "why" on anything). Experiments save as seed + Log and replay exactly. Light challenges: "capsize the RHIB using only waves" |
| **2. Hide-and-seek** | Reading the environment | Foam breadcrumbs, a wake-contrast sensor rule, an aircraft captain, the replay viewer | Asymmetric: the hunter has fuel and sensor modes; the hunted trades speed for wake and uses weather and darkness. Each side sees only its sensors. The payoff is replaying the round from the other side with Inquiry on. New seeds change the right tactics every round |
| **3. Engagement** | Timing and position in a seaway | Fire control, Guided Fury missiles, radar clutter from sea state | Command from a tactical map, or drop into any bridge. Fight the sea as much as the enemy: fire on the roll, choose a heading that steadies the ship. The after-action review explains every miss |

**The common thread:** deterministic replay from any side, with Inquiry on. That one feature makes all three toys replayable and turns each loss into a lesson.

## 6. What the spike settled (verified, 2026-10-23 20:14)

Two tensions in SIX's picks needed an experiment, not a debate. The first was Unity Physics against the strict compile guard (R30 vs R32). The second was Unity Physics' single scalar mass against direction-dependent added mass (R30 vs R13). The spike built the same world two ways:
- **A:** Unity Physics moves the ships, with averaged added mass.
- **B:** our own Burst integrator moves them, with per-axis added mass; Unity Physics only finds contacts.

Full report: `Assets/Reckoning Spike/SPIKE-REPORT.md` on the spike branch.

| Criterion | A | B |
|---|---|---|
| Same Orders → same Fix, 10,000 ticks × 2 runs, rough sea, a collision | Pass | Pass (soft contact overlapped 0.111 m) |
| Roll period within 10 % of IMO (9.72 s) | **Fail:** 10.73 s, +10.4 % | Pass: 9.46 s, −2.7 % |
| Tick ≤ 0.6 ms (4 ships, 414 probes) | 0.204 ms | 0.189 ms |
| Compiles engine-free | Impossible for any job code | Impossible for any job code |

**What it changed:**
- **Variant B is the recommendation (R35, Proposed).** A's miss is structural: one scalar mass cannot be right for roll and heave at once. B's weak spot is contact response, the first design question for the real core.
- **"Engine-free" cannot be the guard (R32 needs amending).** `noEngineReferences` failed with 75 errors, all Unity's own `NativeArray`, `IJob` and `[ReadOnly]`, which live in `UnityEngine.CoreModule`. Proposed: ban the APIs that break determinism instead.
- **Sea State's buoyancy was wrong (fixed, 8c82dd9).** Forces acted at column centres, so every hull rolled about 1.5× too fast. They now act at the submerged centroid, and hulls can set a centre of mass. All 46 Sea State tests pass, and demo roll periods match box theory.

## 7. Decision register

Status: **Locked** is SIX's pick. **Proposed** is Claude's recommended default; override any time. **Open** needs SIX's call. **Parked** has a revisit trigger. All dated 2026-10-23 and attributed to SIX unless marked. "Leaning" is what Claude recommended before SIX picked, kept so divergences are visible.

### Foundations (round 1–5, the 20 questions)

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| R1 | Who is the player? | **D:** switchable (captain / commander / god) | — | Locked |
| R2 | How far does determinism reach? | **C:** everything, AI decisions included | A now, B later | Locked |
| R3 | Multiplayer? | **C:** not now, keep the door open | C | Locked |
| R4 | First player→world link | **A:** wakes | A | Locked |
| R5 | Scale | **A:** a few ships in ~20 km | B | Locked |
| R6 | Time control | **B to start:** pause and speed; rewind and branch stay possible | C | Locked |
| R7 | God-mode powers | **C:** spawn, weather and sea, and direct forces (push a ship, drop a wave) | B | Locked |
| R8 | Wake depth | **C:** foam trail and detection, the visible V, and wake waves that rock ships | A then B | Locked |
| R9 | First playable toy | **All three**, as scenarios: sandbox → hide-and-seek → engagement | A | Locked |
| R10 | AI in the first toy | **B:** simple deterministic captains (patrol, evade) | B | Locked |
| R11 | Order of work | **B:** deterministic ship motion first, wakes on top | B | Locked |
| R12 | Wake fidelity | **C:** plausible (flagged) first, hull-derived (Michell) later | C | Locked |
| R13 | Ship motion fidelity | **B first:** probes + added mass + wave damping; seakeeping-grade later | B | Locked |
| R14 | Determinism across machines | **B first:** same build, Windows PCs; cross-platform later if ever | B | Locked |
| R15 | Tick rates | **C:** per-system Watches on whole-number multiples, fixed order | C | Locked |
| R16 | "Show me why" | **C:** a player-facing feature | C | Locked |
| R17 | Input first | **A:** desktop keyboard and mouse (VR not in scope yet) | C | Locked |
| R18 | Where the core lives | **A:** a new top-level module that Sea State, Guided Fury and JMAC plug into | A | Locked |
| R19 | Saving | **B:** snapshot + Log | B | Locked |
| R20 | Realism vs fun | **B:** per-scenario assist dials, always labelled ("training aid") | B | Locked |

### Beyond the sea, and the toys' feel

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| R21 | Weather as driver | **C:** the Douglas dial stays the input for now, behind a seam the atmosphere can take over later | C | Locked |
| R22 | Fog of war | **A:** each role sees only its sensors; the god sees all | A | Locked |
| R23 | Replay from either side | **B:** arrives with toy 2; the recording exists from day one | B | Locked |

### Naming

| # | Question | SIX's pick | Status |
|---|---|---|---|
| R24 | Name family | **1 (Reckoning):** Chronometer, Watches, Orders, Log, Fix, Inquiry / Board of Inquiry | Locked |
| R25 | Everyday doctrine word | **DGPR formally, "reckoned" day to day.** *Reversal:* SIX first picked C ("something new"), saw four candidates (earned, causal fidelity, swell, consequence-first), then chose the earlier option B | Locked |

### Inquiry

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| R26 | Default depth | **C:** one line plus "show the chain" | C | Locked |
| R27 | Counterfactual "what if" in the Board of Inquiry | Torn between B (later, with rewind and branch) and C (never); decide late | B | **Parked** (see §9) |
| R28 | Continuous attribution ("roll: 60 % wind sea…") | **B for now** (events only); C (live overlay) maybe later | A | Locked (B); C **Parked** |
| R29 | Captains' reasons | **A:** always visible, within fog of war | A | Locked |

### Risky technology

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| R30 | Collisions for the first toy | **C:** Unity Physics (DOTS) | B (own soft contact) | Locked, refined by R35 |
| R31 | How the R30 / R32 / R13 tensions get resolved | **A:** spike first; then "run it inside Deep Water, on its own branch". *Reversal* of Claude's throwaway-project plan | — | Done (§6) |
| R32 | Compile guard | **A:** strict from day one, engine-free assemblies | C | **Open:** the spike shows the literal form is impossible (R36) |
| R33 | Fast-forward target | **B:** 16×, so a tick budget of ~0.6 ms at sandbox scale | B | Locked |
| R34 | Fix-diff tool | **A:** built with the core: per-system fingerprints every tick, so a desync names the tick and the system | A | Locked |

### From the spike (awaiting SIX)

| # | Decision | Status |
|---|---|---|
| R35 | **Variant B:** our own integrator with per-axis added mass moves ships; Unity Physics is the collision detector only (broadphase and distance queries) | **Proposed** (spike recommendation) |
| R36 | **Banned-API guard** replaces engine-free assemblies. A test first (reflect over each reckoned assembly and fail on banned members); a Roslyn analyzer later, once it is verified that Unity can feed it a `BannedSymbols.txt` | **Proposed** |
| R37 | Sea State buoyancy at the submerged centroid; hulls may set their centre of mass | Locked by SIX's go; **built** (`8c82dd9`) |

## 8. Standing constraints

Restate these wherever a future session lands.

1. **Reckoned code never uses:**
   - `UnityEngine.Time` or `UnityEngine.Random`;
   - `UnityEngine.Physics` queries for outcomes;
   - `Object.Find*`, `DateTime.Now` or `Environment.TickCount`;
   - an unseeded `System.Random`;
   - iteration over a `Dictionary` or `HashSet` that decides an outcome;
   - sorting by hash code;
   - anything whose result depends on which thread finishes first.

   *Why:* each is a known way determinism dies.
2. **Nothing that changes an outcome runs on the GPU.** *Why:* GPU results agree with the CPU (the foam twin to 1e-5) but are not bit-identical.
3. **Line-of-sight, hit tests and waterline queries ask reckoned geometry,** never Unity physics raycasts. *Why:* the same reason. Guided Fury's and JMAC's current tests are **unaudited**.
4. **Every change goes through a logged Order, and every tick has a Fix.** *Why:* that keeps the multiplayer door open (R3).
5. **Authored numbers say so, and assists are labelled** (R20; the project's honesty rules).
6. **Cause records are in the core from day one.** *Why:* bolting "why" onto rules that never recorded their reasons is miserable work.
7. **Tuning values live in ScriptableObjects; physics runs on the fixed tick** (project CLAUDE.md).

## 9. What is deliberately not here

| Not doing | Why, and the revisit trigger |
|---|---|
| Multiplayer | R3. Revisit when two toys are playable; the Orders + Fix rules keep it cheap |
| Rewind and branch | R6 starts with pause and speed. Revisit with the Board of Inquiry |
| Counterfactual "what if" | **PARKED** (R27). SIX, decide B or C when rewind is designed. First three tickets then: snapshot cadence, branch-from-tick, an Inquiry "re-run with this Order changed" view |
| Live attribution overlay | **PARKED** (R28 C). Revisit once the sandbox is playable. First tickets: per-component wave height per hull, a labelled approximate split for forces |
| VR | R17. HurricaneVR is in the project; not in scope yet |
| Cross-platform determinism | R14. Windows PCs, same build, first |
| Seakeeping-grade motion (strip theory, RAOs) | R13. Probes + added mass + wave damping first |
| Atmosphere pack | R21. The sea keeps a seam (its conditions come from one place) so the atmosphere can drive it later |
| Theatre scale | R5. A few ships in 20 km; distant-tier rules come later |
| A persistent world | R19. Snapshot + Log only |

## 10. Risks

| Risk | Danger | Mitigation |
|---|---|---|
| **Scope.** Three toys, god forces, full wakes, total determinism | Highest | Each phase has a definition of done you can play, or it becomes an engine with no game |
| **Contacts.** Variant B owns the contact response; the spike's soft contact overlapped 0.111 m | High | First design question for the core: an impulse solver, or Unity Physics' solver for contact pairs only |
| **AI determinism** dies quietly (see §8.1) | High | Banned-API guard (R36) plus the Fix-diff tool (R34) built with the core |
| **CPU budget.** Wakes are the likely hog: every probe summing every wake segment | Medium | 0.6 ms tick budget (R33), distance and age culling, a cheaper wake far away, a benchmark like the sea's. Spike baseline 0.19 ms for 4 ships |
| **Cross-machine determinism** is unverified; the spike ran on one machine, Editor only | Medium | A two-PC Fix comparison is an early milestone (R14) |
| **Existing systems.** Guided Fury, JMAC and Radar Suite reckoned status is unaudited | Medium | Audit each against §3 before it joins a toy |

## 11. Proposed order of work

Proposed, not agreed. Each phase ends playable or measurable.

1. **Foundations.**
   - Split Sea State's core from its MonoBehaviour adapters, so the guard can apply to the core.
   - Build the Reckoning core: Chronometer, Watches, Orders, OrderLog, Fix with per-system fingerprints, Fix-diff, cause records, snapshot + Log.
   - *Done when* a scripted Log replays to identical Fixes twice and Fix-diff names a planted desync.
2. **Ship motion (variant B)** on the existing sea, with the DDG sway as first user.
   - *Done when* hulls float at design draft and roll within 10 % of IMO.
   - *Also:* two runs give identical Fixes through a collision, and the tick is ≤ 0.6 ms at sandbox scale.
3. **Local wave sources.** Drop-a-wave first, then hull wakes with foam breadcrumbs.
   - *Done when* a dropped ring reaches a distant hull at the dispersion-correct time, and a wake pitches the RHIB with an Inquiry line explaining it.
4. **Toy 1: sandbox.**
5. **Toy 2: hide-and-seek,** with the replay viewer.
6. **Toy 3: engagement.**

## 12. Open questions for SIX

Priced by cost to answer. Capped at five; one weekly batch is fine.

| # | Question | Cost |
|---|---|---|
| Q1 | Confirm **variant B** (R35): our integrator moves ships, Unity Physics detects contacts only? | 1 min, yes/no |
| Q2 | Amend R32 to the **banned-API guard** (R36): a test first, an analyzer later? | 1 min, yes/no |
| Q3 | The **spike branch**: keep as a record, merge into the Reckoning work when it starts, or discard? | 1 min, pick one |
| Q4 | Does **Unity Physics 1.4.2 (with Entities)** become a project dependency on the main line? Variant B still uses it for contacts | 2 min |
| Q5 | Is this doc the **locked blueprint**, or do any picks need revisiting before phase 1 is planned? | 5 min read |

## 13. Verified facts this doc rests on

Verified 2026-10-23 unless noted.
- Deep Water ships **Entities 6.4.0** built in. **Unity Physics 1.4.2** is bundled with the 6000.4.11f1 install. It resolves and compiles against the built-in Entities (the spike compiled and ran).
- `Unity.Physics.Simulation.StepImmediate` steps a world we build from our own tick, so the game does not have to become ECS. **Verified** by the spike.
- **Same-machine determinism.** Unity Physics immediate mode is bit-identical run to run on one machine, Editor, through a collision (10,000 ticks × 2). **Cross-machine is unverified.**
- **What reckoned job code needs from the engine:** `UnityEngine.CoreModule` only. The compiled spike also references `UnityEditor.CoreModule`, through Unity's generated job registration (build-verified; that it disappears in a Player build is unverified).
- **`FloatMode.Deterministic` costs 16–21 %** on the sea's hot path (measured 2026-10-22, kept for reliability).
- **The sea is deterministic and publishes `SeaConditions`.** Foam drift and windrows conserve mass exactly. GPU foam matches its CPU twin to 1e-5.
- **Buoyancy (after 8c82dd9).** Righting moment within 0.7 % of wall-sided theory at 5°. Demo roll periods match box theory: 9.42 / 3.98 / 1.78 / 3.36 s against 9.04 (undamped) / 4.0 / 1.76 / 3.36 s.

## 14. Methodology hooks

Candidates from this work, for `METHODOLOGY.md` once SIX agrees:
- **A physics engine's mass model is a design constraint.** If the domain needs direction-dependent inertia, a scalar-mass engine can detect collisions but cannot integrate the motion. Evidence: spike C2, A +10.4 % vs B −2.7 %.
- **Guard what is banned, not where it lives.** An assembly-level "no engine" switch fails for any Unity job code. Evidence: spike C4, 75 CS0246 errors.
- **Reproduce the incumbent behind a switch before calling it wrong.** Evidence: spike F1, 1.49 measured vs 1.47 predicted. Now AP15 for the buoyancy case.

## 15. Glossary

- **Added mass:** the water a hull has to push aside as it accelerates, which acts like extra mass. It is large in heave (up and down) and sway, small in surge (fore and aft). So one mass for all directions is wrong for ships.
- **Broadphase:** the quick first pass of collision detection that finds which pairs of objects might be touching.
- **Burst:** Unity's compiler for high-performance C#. Its `Deterministic` float mode is designed to give the same results across machines.
- **DOTS / ECS / Entities:** Unity's data-oriented stack. ECS is the entity-component-system way of organising a game; Entities is the package. Unity Physics belongs to it.
- **Dryden / von Kármán:** standard military models of atmospheric turbulence, used for seeded gusts.
- **Evaporation duct:** a moist layer just above the sea that bends radar along the surface, extending its range.
- **FNV-1a:** the simple hash the spike uses to fingerprint world state (the Fix).
- **GM (metacentric height):** a ship's roll stability. It is the height of the metacentre above the centre of gravity, GM = KB + BM − KG. Larger GM rolls back faster.
- **Immediate mode:** using Unity Physics by building and stepping its world yourself, without converting the game to ECS.
- **IMO roll period:** the International Maritime Organization's empirical formula for a ship's natural roll period, T = 2CB/√GM. Here it is a "does this roll like a ship" yardstick.
- **Kelvin wake:** the V-shaped wake of any ship in deep water, with a 19.5° half-angle.
- **Lockstep multiplayer:** each machine runs the same deterministic simulation, and only the players' Orders travel over the network.
- **Michell's integral:** classic "thin ship" theory that computes a hull's wave pattern from its shape.
- **Strip theory / RAO:** seakeeping methods that slice a hull into sections to predict its motion in waves. An RAO (response amplitude operator) is the resulting motion-per-wave table.
- **Systemic design:** rules producing behaviour, as opposed to authored outcomes.

## Change log

- **2026-10-23** — Written from the session's talk-through:
  - the 20 questions;
  - weather, fog of war and replay;
  - naming, Inquiry and the risky-tech picks;
  - the physics spike and the Sea State buoyancy fix it surfaced.

  Stamped THEORETICAL apart from the spike and the fix. Five open questions for SIX.
