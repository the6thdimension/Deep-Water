#!/usr/bin/env bash
# wait_artifact.sh <Assembly> <edited source file> [timeout s]
#
# Waits until Unity's Bee build artifact for <Assembly> is at least as new as the edited source, for edits that add
# no new symbol (wait_build.sh needs one). The Library/ScriptAssemblies copy keeps its old time when a rebuild is
# byte-identical; the Bee artifact under Library/Bee/artifacts/*Dbg.dag/ does not. Prints one outcome:
#   BUILT    the artifact is newer than the source
#   ERRORS   a compile error was logged after the wait began (listed below the line)
#   TIMEOUT  neither happened in time
# Exit codes: 0 built, 1 errors, 2 timeout, 3 usage.
set -uo pipefail
[ $# -ge 2 ] || { echo "usage: wait_artifact.sh <Assembly> <edited source file> [timeout s]"; exit 3; }
ROOT="${UNITY_PROJECT_ROOT:-$(cd "$(dirname "$0")/../../../.." && pwd)}"
ASM="$1"; SRC="$2"; TIMEOUT="${3:-240}"
cd "$ROOT" || exit 3
[ -f "$SRC" ] || { echo "no such source: $SRC"; exit 3; }
LOG="${LOCALAPPDATA:-$HOME/AppData/Local}/Unity/Editor/Editor.log"
start_lines=$(wc -l < "$LOG" 2>/dev/null || echo 0)
src_time=$(stat -c %Y "$SRC")
for ((i = 0; i < TIMEOUT; i += 2)); do
  newest=0
  for dll in Library/Bee/artifacts/*Dbg.dag/"$ASM".dll; do
    [ -f "$dll" ] || continue
    t=$(stat -c %Y "$dll"); [ "$t" -gt "$newest" ] && newest=$t
  done
  if [ "$newest" -ge "$src_time" ]; then
    echo "BUILT: $ASM artifact at $(date -d @"$newest" +%H:%M:%S), source $(date -d @"$src_time" +%H:%M:%S)"
    exit 0
  fi
  errs=$(tail -n +"$((start_lines + 1))" "$LOG" 2>/dev/null | grep -a "error CS" | sort -u | head -10)
  if [ -n "$errs" ]; then echo "ERRORS: compile errors after the wait began"; echo "$errs"; exit 1; fi
  sleep 2
done
echo "TIMEOUT: no $ASM artifact newer than $SRC after ${TIMEOUT}s (is Unity in play mode? trap 1; or was nothing asked to compile? templates/recompile.cs)"
exit 2
