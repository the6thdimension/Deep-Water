#!/usr/bin/env bash
# Wait for a results file that an in-Editor command writes (test results, a play-mode recording,
# a screenshot), then print it. Use instead of chained sleeps.
#
# usage: wait_file.sh <file> [timeout_s] [until_regex]
#   Waits until <file> exists and, if <until_regex> is given, contains a matching line
#   (e.g. "^FINISHED"). Text files are printed; binary files (png) only get their size.
#   Exit code 0 on success, 1 on timeout.
set -u
F="$1"; TIMEOUT="${2:-240}"; UNTIL="${3:-}"
for ((t = 0; t < TIMEOUT; t += 2)); do
  if [ -f "$F" ] && { [ -z "$UNTIL" ] || grep -aqE "$UNTIL" "$F"; }; then
    case "$F" in
      *.png|*.jpg|*.exr) echo "READY after ${t}s: $(wc -c < "$F") bytes";;
      *) echo "READY after ${t}s"; cat "$F";;
    esac
    exit 0
  fi
  sleep 2
done
echo "TIMEOUT after ${TIMEOUT}s waiting for $F${UNTIL:+ (until /$UNTIL/)}"
[ -f "$F" ] && { echo "--- partial ---"; tail -20 "$F"; }
exit 1
