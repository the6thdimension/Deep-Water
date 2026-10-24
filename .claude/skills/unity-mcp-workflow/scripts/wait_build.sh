#!/usr/bin/env bash
# Wait for Unity to rebuild an assembly after a source edit, or report why it didn't.
#
# usage:
#   wait_build.sh mark
#       Record the Editor.log length and a timestamp. Run this BEFORE asking Unity to compile.
#   wait_build.sh <Assembly> <NewSymbol> [timeout_s]
#       Wait until Library/ScriptAssemblies/<Assembly>.dll is newer than the mark AND contains
#       <NewSymbol> (searched as UTF-8 and UTF-16), or until an "error CS" appears in Editor.log
#       after the mark. Always prints exactly one outcome line, then any CS errors and build lines.
#
# Pick a symbol that only exists after your edit (a new method or test name). An old symbol
# proves nothing, which is why the DLL must also be newer than the mark.
#
# env: UNITY_PROJECT_ROOT (default: the Unity project this skill lives in)
#      UNITY_EDITOR_LOG   (default: %LOCALAPPDATA%/Unity/Editor/Editor.log)
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="${UNITY_PROJECT_ROOT:-$(cd "$HERE/../../../.." && pwd)}"
LOG="${UNITY_EDITOR_LOG:-${LOCALAPPDATA:-$HOME/AppData/Local}/Unity/Editor/Editor.log}"
mkdir -p "$ROOT/Temp"
MARK="$ROOT/Temp/wait_build.logmark"

if [ "${1:-}" = "mark" ]; then
  wc -l < "$LOG" > "$MARK"
  echo "marked $(cat "$MARK") lines of $LOG"
  exit 0
fi
if [ $# -lt 2 ]; then
  sed -n '2,15p' "$0"; exit 2
fi

ASM="$1"; SYM="$2"; TIMEOUT="${3:-240}"
DLL="$ROOT/Library/ScriptAssemblies/$ASM.dll"
START=$(cat "$MARK" 2>/dev/null || echo 1)
OUTCOME="TIMEOUT after ${TIMEOUT}s: $ASM did not rebuild with '$SYM'. Check Unity_ManageEditor GetState: IsPlaying=true defers every compile (Stop play), IsCompiling stuck usually means the same."
for ((t = 0; t < TIMEOUT; t += 2)); do
  if [ -f "$DLL" ] && [ "$DLL" -nt "$MARK" ] && python -I -c "import sys;d=open(sys.argv[1],'rb').read();s=sys.argv[2];sys.exit(0 if (s.encode() in d or s.encode('utf-16-le') in d) else 1)" "$DLL" "$SYM"; then
    OUTCOME="BUILT: $ASM contains '$SYM' after ${t}s"
    break
  fi
  if tail -n +"$START" "$LOG" | grep -aq "error CS"; then
    OUTCOME="ERRORS after ${t}s: compile errors in the Editor log (Unity keeps running the last good DLL)"
    break
  fi
  sleep 2
done
echo "$OUTCOME"
tail -n +"$START" "$LOG" | grep -aE "error CS|Tundra build" | sort -u | head -20
