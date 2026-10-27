# Kill log: autoroutines

**Job of this document:** record why the routines in this folder were retired, so nobody revives them as written.

| Date | Killed | By | Reason | Rule it produced |
|---|---|---|---|---|
| 2026-10-27 | `missile.md`, `radar.md`, `aviator.md` and their `_common.md` | SIX (overnight handoff decision 5) | Never ran in seven weeks: no scheduled task, no `auto/*` branches. Unsafe as written for this project: they `git checkout` in a shared checkout, push to branches outside the branch table, and the radar routine targets the struck `Assets/RH Radar` plan | Doctrine guard "a routine without a scheduler is a wish" applied; a future routine needs a worktree, the current branch table, a hard gate, a revert path, an escalation and a real schedule before it is written |
