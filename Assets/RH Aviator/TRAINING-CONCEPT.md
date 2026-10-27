# Pilot training: learned AI pilots for RH Aviator

> **THEORETICAL — NOTHING BUILT**, with one exception, measured: the ML-Agents upgrade (M9, T0), 2.0.2 → 4.1.0, which
> pulls in `com.unity.ai.inference` 2.6.1 and drops Barracuda. It compiles on 6000.4.11; whole project EditMode
> 423 pass, 3 fail (the same three as before the upgrade), 2 explicit skipped. Python's `mlagents` is not installed:
> this machine has Python 3.13, and the trainer's supported Python version is unverified.
> Written 2026-10-27 from SIX's talk-through with Claude: 16 questions in four rounds (M1–M16). Nothing here is a
> plan until SIX says so, except the package upgrade (M9), which SIX asked for "now".

**This document's one job:** record what training AI pilots with ML-Agents means for Deep Water, and every decision
SIX has made about it, so the build can start later without re-asking. It is not the roadmap (RH Aviator's
`ROADMAP.md`) and it is not a spec.

---

## 1. The idea in plain language

**The ask.** SIX, 2026-10-27:

> "I want you to think about how to utilize ML agents for the training of the planes in the dogfighting, like all of
> their behaviors, when to gauge fuel, how to avoid collisions, how to chase and attack, etc."

Today every AI pilot follows hand-written rules: when to break, how hard to pull, when to go home. A *learned* pilot
picks up those judgements by flying many thousands of fights with no screen. Each result earns a reward or a
penalty, and the pilot gradually favours what earned more. Deep Water is a good fit for this. The flight core is pure
C# on a fixed 50 Hz tick and runs headless far faster than real time. Every AI pilot already sees the world through
the same `Perception` (field of view, reaction delay, memory), so a learned pilot can be held to the same eyes.

**The test.**
1. On a fixed set of 20 merges it never trained on, the learned pilot meets the ace (the best rule pilot).
2. It is ahead in at least 12, by kills first and then by pointed cone time.
3. In some of those fights it does something nobody wrote, such as using a ridge to break the ace's sight or
   trading height for a shot.
4. It crashes and collides no more often than the ace, and it gets home with fuel in at least 19 of 20.

**Wording.** "Machine learning" here means *reinforcement learning*: learning from rewards earned by acting, not
from labelled examples. The trained result is a *policy*, a small neural network that maps what the pilot sees to
what it asks for. The rules stay; the policy is a fourth kind of pilot.

**Honest limits.**
- **Learned behaviour is harder to explain** than a rule log. The rule pilots name the rule behind every mode
  change; a policy only shows what it saw and what it asked for.
- **Rewards get gamed.** Tonight's survey proved it in advance: with the score by position alone, equal rule pilots
  "won" every merge on head-on-pass time (BLUEPRINT A11). A policy would farm that within an hour.
- **Learned fights need not replay exactly** (M3). That sets aside a contract Air Wing and Reckoning (R2) hold;
  §4.5 says how the theatre still shows exactly what happened.
- **Fuel does not exist yet.** Bingo is a flight-time stand-in (W47) until Air Wing's fuel burn lands. "When to gauge
  fuel" can only be learned after fuel exists.
- **Training needs Python** (`mlagents-learn`, PyTorch) on this machine, and CPU hours.

## 2. Vocabulary (proposed 2026-10-27; naming is open question 1)

| Word | What it is | In code (proposed) | Why the word fits |
|---|---|---|---|
| **Learned pilot** | The fourth skill level, beside rookie, line and ace (M2) | `PilotSkill.Learned`, `LearnedSeat` | Says how it got its judgement, not that it is "smart" |
| **Policy** | One trained network, frozen, with its training step | `.onnx` + `PolicySO` | The ML word; the debrief uses "the learned pilot" |
| **Training round** | One run of `mlagents-learn` from a starting policy to a stopping point | — | |
| **Training theatre** | The Editor tool that lists and plays back recordings (M12) | `TrainingTheatreWindow` | SIX's word |
| **Training arena** | The 64 km box of terrain where training fights happen (M13, M14) | a Theatre `TheatreSO` | |
| **Director** | Picks the next fight's scenario, harder as the pilot's rating rises (M15) | `ScenarioDirector` | |
| **Scenario** | A type of fight with seeded variations: "bounced from above", "valley fight low" | `ScenarioSO` | |
| **Recording** | Every aircraft's state on every tick of one training fight, with the reward terms | `FightRecording` | Not "replay": a learned fight cannot be re-run exactly (M3) |
| **Scorecard** | The gate measurement (M11) on the held-out set | `GateScorecard` | |

## 3. What makes a learned pilot "kept"

A policy is **kept** when all four hold on the held-out set of 20 merges against the ace (M11):
1. ahead in at least 12, by kills, then by pointed cone time (never position alone);
2. crashes per fight at or below the ace's;
3. collisions with friends at or below the ace's;
4. home with fuel in at least 19 of 20.

Otherwise its verdict is **promising, not proven** (doctrine 14), and the rule pilots stay the default. The
held-out set is frozen when it is made. A policy is never tuned against it; that would be variant-shopping a
validation gate (doctrine 13).

## 4. How it is put together

### 4.1 What the policy controls (M1)

The policy does not move the stick. It fills in the **hands' request**: an aim point, an altitude, a speed and a G,
which `FlightPathHands` then flies with their bank caps and their ground guard. It also gets the trigger and the
missile pickle once the weapons exist. This has three consequences:
- **A fair comparison.** All four skill levels fly through the same hands, so the comparison is about judgement only.
- **It carries over between fidelity levels.** A policy trained at Airframe L0 asks the same things at L1.
- **It can still crash.** The ground guard only shapes the request. Since behaviour is learned rather than guarded
  (M6), the policy can ask for a dive the guard can't save.

### 4.2 What it sees (M7)

What it sees, in its own body frame:
- its own state: speed, height above the terrain, attitude, G, fuel, rounds and missiles left;
- up to N nearest enemies and N nearest friends *as `Perception` delivers them* (field of view, reaction delay,
  memory of the last sighting), each with relative position, velocity and aspect;
- the terrain ahead as a few height samples along and either side of its path (needed for M16's valleys and ridges);
- **later, a radar picture:** tracks from a jet radar, once RH Radar Suite's core is on the jet (GQ G61 splits it
  out). Until then the eyes alone.

It never sees truth. A policy that learned on truth would cheat, and wouldn't hold up in a fair fight.

### 4.3 What it is rewarded for (M5)

Outcomes, lightly shaped. Every weight lives in a `RewardWeightsSO`, AUTHORED and logged with each policy.

| Term | Sign | Note |
|---|---|---|
| A kill | large + | Needs guns or the IR missile and hit points (the weapon slices) |
| Surviving the fight | + | |
| Pointed rear-cone time | small +, per second | Pointed, never position alone (A11's finding) |
| Being killed | large − | |
| Crash (ground, sea or terrain) | large − | M6: the only thing that teaches it the ground |
| Collision with any aircraft | large − | M6: the same for friends |
| Landing or ending the fight short of fuel | − | After fuel burn exists |

Shaping is kept small on purpose, so the policy learns the fight and not the shaping.

### 4.4 What it learns, and what stays a rule (M6)

**Everything is learned:** chase, attack, defence, energy, when to shoot, when to go home, and how to stay clear of
the ground and of other aircraft. No hard rule overrides it. So "never crashes" isn't guaranteed; it's measured,
and it's two lines of the scorecard (§3). In normal play the rule pilots stay the default until a policy is kept.

### 4.5 Determinism and recordings (M3, M8)

Learned fights **need not replay exactly** (M3). The exception is narrow: rule-pilot fights still replay bit for bit,
and the debrief labels a fight with a learned seat "not replayable".

What lets SIX still watch exactly what happened is the **recording**. During training, chosen fights write every
aircraft's state on every tick, plus the reward terms as they were earned. The theatre plays a recording back as a
film, with no inference involved, so a recording is always exact even when a re-run wouldn't be.

### 4.6 Who it learns against (M4, M10)

1. **A 1 v 1 curriculum against the rule pilots:** rookie, then line, then ace, moving up when it wins most fights
   at a rung. The trainer is PPO.
2. **Then self-play,** against frozen older copies of itself, rated by Elo, **with the rule pilots kept in the
   opponent pool**, so it can't forget how to beat them (M4 as SIX wrote it).
3. **Then teams:** 2 v 2, then 4 v 4, with ML-Agents' cooperative trainer (MA-POCA), where a side shares a reward,
   so wingmen learn to support each other (M10).

### 4.7 The training arena (M13–M16)

SIX: "a much bigger better more orchestrated more challenging training theater for the planes; obstacles like
mountains or land masses might be a good addition."

- **Terrain comes from the Theatre module, heights first (M13).** It is a fictional mountainous theatre (fjords,
  islands, ridges, a coast and a home airfield) baked from a theatre type and a seed. The flight core samples its
  heights for ground contact and line of sight, and HDRP draws the same heights. That keeps the Theatre blueprint's
  one-source rule and its E1–E25. Curved earth, seabed and the sea's coast effects come later. *This reorders the
  Theatre plan:* its slice 1 was a curved-earth spike (open question 5).
- **One 64 km box (M14):** room for a merge, an extension, a valley fight and the run home.
- **All four terrain challenges (M16):**
  - **terrain masking:** `Perception` gains line of sight, so a ridge hides an enemy (and later the radar);
  - **low-level valley fights:** scenarios that start or force the fight into valleys and fjords;
  - **ridges and peaks to avoid:** peaks in the merge area;
  - **land, sea and an airfield:** a home airfield to reach at bingo, over land or water.

### 4.8 The Director and its scenarios (M15)

A library of `ScenarioSO` types, each with seeded variations:

| Scenario | The fight |
|---|---|
| Neutral merge | Head-on at equal height and speed |
| Bounced from above | An enemy starts high in the rear quarter |
| Ambush from behind a ridge | An enemy masked by terrain until close |
| Valley fight low | Both start below the ridgelines |
| Outnumbered | 1 v 2, 2 v 3 |
| Short of fuel | Starts near bingo, far from home |
| Defend the airfield | Red attacks toward blue's home |

The Director picks harder scenarios as the policy's rating rises, and picks the ones it loses most often. Every
recording carries the name of the scenario, its seed and its rung.

### 4.9 Where it runs, and what SIX watches (M8, M12)

- **Training runs headless:** a batch-mode player build with no graphics. ML-Agents' Academy is stepped by hand
  from the flight core's tick, as fast as the CPU allows, with several builds in parallel. The Editor stays free.
- **SIX watches in the Editor**, not every run: **the very first run, a few runs from the middle, and the very last
  run** (SIX, M8). The trainer saves a recording of the first fight of the round, one at each checkpoint (proposed:
  every 10 % of the round) and the last fight.
- **The training theatre (M12)** is a tool in the Dogfight scene. It lists the round's recordings with each one's
  step, scenario and scorecard. Pick one and it plays back with the chase camera, a timeline scrub, and the reward
  terms drawn as they were earned. A graph shows the round's progress: rating, kills, crashes and fuel per checkpoint.

### 4.10 The package (M9)

`com.unity.ml-agents` is 2.0.2 in `Packages/manifest.json`. SIX: upgrade it now. The newest version found on the
web is 4.1.0 (a September 2026 guide reports it published 2026-07-29 and requiring Unity 6). The source is thin, and a
Unity forum thread asks whether Package Manager shows the package as deprecated on 6000.1+. **Unverified** until
the upgrade is tried. *Update the same day:* Unity's registry lists 4.1.0 as the newest; installed, compiles, no test lost. The Python side (`mlagents`, PyTorch) must match the package's release, and is not installed yet (T3).

## 5. Decision register

Locked = SIX's pick. Proposed = Claude's recommended default, open to SIX. Open = needs SIX.

### Round 1: shape

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| M1 | What the network controls | **The hands' request** (aim, altitude, speed, G; plus trigger and pickle once weapons exist) | Same | Locked |
| M2 | Its place beside the rule pilots | **A fourth skill level;** the rules stay the baseline | Same | Locked |
| M3 | Exact replay | **Not required** for learned fights | Log the outputs so replays stay exact | Locked (a scoped exception: §4.5) |
| M4 | Who it learns against | **A curriculum against the rule pilots, then self-play, with the rule pilots kept in the pool** | Curriculum then self-play | Locked |

### Round 2: what it learns from

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| M5 | Reward | **Outcomes, lightly shaped;** pointed cone time only; weights in an SO | Same | Locked |
| M6 | Learned or ruled | **Everything learned,** collision avoidance and fuel too; no hard-rule override | Learn the fight, rules guard | Locked |
| M7 | Observations | **Perception's eyes, plus a jet radar picture** (once the radar core is on the jet) | Eyes only | Locked |
| M8 | Training rig | **Headless training rounds; SIX watches the first, some middle and the last run in the Editor** | Headless built player | Locked |

### Round 3: the package, teams, the gate, viewing

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| M9 | ML-Agents 2.0.2 | **Upgrade now** | Upgrade after a spike | Locked |
| M10 | Teams | **1 v 1 first (PPO), then 2 v 2 and 4 v 4 (MA-POCA)** | Same | Locked |
| M11 | When a policy is kept | **Beats the ace on a fair held-out set** (§3) | Same | Locked |
| M12 | Watching | **A training theatre, and a much bigger, more orchestrated, more challenging arena with mountains and land** | A training theatre | Locked |

### Round 4: the arena

| # | Question | SIX's pick | Leaning | Status |
|---|---|---|---|---|
| M13 | Where the terrain comes from | **The Theatre module, heights first** | Same | Locked |
| M14 | Arena size | **One 64 km box** | Same | Locked |
| M15 | "Orchestrated" | **A scenario Director** | Same | Locked |
| M16 | Terrain challenges | **All four:** masking, valley fights, ridges and peaks, land + sea + airfield | — | Locked |

### Proposed by Claude while writing

| # | Proposal | Why | Status |
|---|---|---|---|
| M17 | A policy flies under the same `PilotProfile` caps as an ace (G, bank, field of view, reaction delay) | Otherwise it wins by being a better airframe, not a better pilot | Proposed |
| M18 | Recordings save at the first fight, every 10 % of a round, and the last fight | SIX's "first, middle, last", made countable | Proposed |
| M19 | Each kept policy is committed with its reward weights, scenario mix, rung, step count and scorecard; failed ones are logged, not kept | Doctrine: label AI output with inputs and version | Proposed |
| M20 | A policy never leaves the game: no export to drones, model aircraft or real hardware | Safe limits (Air Wing constraint 8) | Proposed (a standing constraint, §6) |

## 6. Standing constraints

1. **Safe limits apply in full** (Air Wing BLUEPRINT constraint 8; General Quarters §6.8). The policies are game AI.
   No flight-control or guidance code meant to run on real hardware, and opponents stay generic. *Why:* SIX,
   2026-10-24.
2. **The policy sees only what a rule pilot sees** (M7, M17). *Why:* a learned pilot that cheats proves nothing.
3. **The rule pilots stay the default until a policy is kept** (M2, M11). *Why:* complexity must earn its keep.
4. **The held-out set is frozen when made** and is never trained or tuned on. *Why:* doctrine 13.
5. **Rule-pilot fights keep exact replay;** only fights with a learned seat are exempt (M3).
6. **No paid compute without SIX's go.** Training runs on this machine. *Why:* doctrine 22.

## 7. What is deliberately not here

| Not doing | Why, and the revisit trigger |
|---|---|
| Training before kills exist | The reward's main term needs guns or the IR missile and hit points: after those slices |
| Fuel judgement before fuel burn | Bingo is a flight-time stand-in (W47) until Air Wing's fuel burn lands |
| The radar picture before the radar slice | M7's second half needs RH Radar Suite's core on the jet (GQ G61) |
| Learning from recorded human play (imitation) | Not asked for; a later option to speed up the first rounds |
| Cloud training | Paid; only on SIX's go |
| Countermeasures | Not in this push (SIX, 2026-10-27) |

## 8. Risks

| Risk | Danger | Mitigation |
|---|---|---|
| Reward hacking | It learns the shaping, not the fight (A11 already shows how) | Small shaping, pointed cone time only, the outcome gate, recordings watched by SIX |
| No rule guard (M6) | A shipped policy flies into a mountain or a wingman | Two scorecard lines; the rules stay the default until kept |
| The package's status on Unity 6 | Package Manager may show it as deprecated on 6000.1+ (unverified forum report) | The upgrade (M9) is tried first and reported honestly; a failed upgrade stops the training plan, not the weapons work |
| Training speed | Too slow on one CPU to reach the ace | Measured in the first round (steps per second); several headless builds in parallel |
| Terrain cost in the pure core | Height sampling and line of sight slow every tick | The budget is measured (W21); a coarse heightfield first |
| Transfer to the play scene | A policy trained headless at L0 behaves oddly at L1 | Train at the fidelity sheet SIX plays; the gate is run at that sheet |
| Non-replayable bugs | A learned fight that went wrong can't be re-run | The recording shows it exactly; the policy and seed are logged |

## 9. Proposed order of work (proposed, not agreed)

SIX's order for this push: this doc, then guns, then the IR missile, then slice 3f, then radar.

| Phase | What | Done when |
|---|---|---|
| T0 | **Upgrade ML-Agents** (M9, now) | It compiles on 6000.4.11 and every existing test still passes (**done 2026-10-27**); one PPO step with manual stepping waits for Python (T3) |
| T1 | *(the weapon slices)* Guns L1, then the IR missile, with hit points and kills | Kills exist in headless fights |
| T2 | Theatre heights: a fictional mountainous 64 km theatre, sampled by the flight core; `Perception` line of sight | A rule pilot is masked behind a ridge in a test; a terrain crash is a crash |
| T3 | The training rig: `LearnedSeat`, observations, rewards SO, the headless build, recordings | One training round runs end to end and saves its first, middle and last recordings |
| T4 | The training theatre and the Director | SIX watches the first and last recordings in the Editor |
| T5 | 1 v 1 curriculum, then self-play | The scorecard against the ace on the held-out set: kept, or "promising, not proven" |
| T6 | Teams (MA-POCA), then fuel and the radar picture as those land | The same gate at 2 v 2 |

## 10. Open questions for SIX

| # | Question | Cost |
|---|---|---|
| 1 | The fourth skill level's name: "learned", or another word? | 1 min |
| 2 | How many CPU hours a training round may take on this machine (it ties the machine up)? | 1 min |
| 3 | Until a policy is kept, may learned pilots fly in normal play, or only in the training theatre? | 1 min |
| 4 | How long to keep recordings and failed policies (disk)? | 1 min |
| 5 | M13 reorders the Theatre plan: heights first, before its curved-earth spike (slice 1). Confirm? | 2 min |

## 11. Verified facts this doc rests on

| Fact | Label |
|---|---|
| `com.unity.ml-agents` 2.0.2 is in `Packages/manifest.json` | verified 2026-10-27 |
| 4.1.0 is the newest in Unity's registry; installed, compiles on 6000.4.11 | verified 2026-10-27 |
| Unity 6 required; a "deprecated" label on 6000.1+ | unverified (web, 2026-10-27) |
| Bingo is a flight-time stand-in; no fuel burn yet | verified in `AviatorSeat.cs`, `PilotProfile.cs` |
| With position-only scoring, line v line "wins" every merge on head-on-pass time (6.1 / 5.6 s a fight) | build-verified, the survey of 2026-10-27 (BLUEPRINT A11) |
| The flight core is pure, 50 Hz, headless and deterministic | verified (Air Wing tests, 135/135) |
| The Theatre blueprint (E1–E25) is locked and unbuilt | verified 2026-10-27 |

Sources: [issoh.co.jp guide](https://www.issoh.co.jp/md/tech/4905);
[Unity forum: Is ML-Agents being deprecated?](https://discussions.unity.com/t/is-ml-agents-being-deprecated/1643681).

## 12. Methodology hooks

- *Candidate:* **a score that a rule pilot can farm, a learner will farm faster.** Check every reward term against
  the rule pilots' survey before training. Evidence: A11 (position-only scoring, 2026-10-27).
- *Candidate:* **record, don't re-run, when inference can't replay.** A per-tick state recording gives exact
  playback without exact inference (§4.5).

## 13. Glossary

- **Reinforcement learning:** learning by trial: actions earn rewards or penalties, and the learner favours what
  paid.
- **Policy:** the trained network that turns what the pilot sees into what it asks for.
- **PPO:** the standard ML-Agents trainer for a single agent.
- **MA-POCA:** ML-Agents' trainer for teams that share a reward.
- **Self-play:** training against older copies of yourself.
- **Elo:** a rating that rises when you beat someone rated higher.
- **Curriculum:** opponents or tasks that get harder as the learner improves.
- **Shaping:** small rewards for steps toward the goal, to speed learning.
- **Reward hacking:** a learner finding a way to score that isn't what was meant.
- **Headless / batch mode:** a build that runs with no graphics or window.
- **ONNX:** the file format the trained network is saved in.
- **Held-out set:** test fights never used in training, so the score is fair.
- **Terrain masking:** hiding behind ground so you can't be seen.

## Change log
- **2026-10-27** — Written from SIX's 16 answers (M1–M16); M17–M20 proposed. Nothing built.
- **2026-10-27** — T0: ML-Agents 2.0.2 → 4.1.0 installed and verified (compile, whole-project tests unchanged).
