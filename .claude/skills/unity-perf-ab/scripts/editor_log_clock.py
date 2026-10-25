# Timestamping tail of Unity's Editor.log, for timing the Editor's own loop (compile, domain reload,
# play-mode entry, player builds). The log has no timestamps, so this stamps each new line with the
# wall-clock second it appeared (polled every 20 ms) and keeps only the lines worth timing.
#
# usage:  python -I editor_log_clock.py <Editor.log> <out.txt>      (run in the background)
# Then mark t0 from inside the Editor with Debug.Log("MARK <label> ...") and read the cycle with
# build_cycle.py. Lines look like "1792952841.628 <log line>".
import os, re, sys, time

KEEP = re.compile(
    r"Domain Reload Profiling|Tundra build|Asset Pipeline Refresh|Reloading assemblies|Loaded scene|"
    r"Total Operation Time|Begin MonoManager|Mono: successfully reloaded|Executing tool|MARK|"
    r"Scripting: domain|Build Finished|Starting: |ExitCode|CompilationPipeline", re.I)

def main(src, dst):
    f = open(src, "rb")
    f.seek(0, 2)
    buf = b""
    with open(dst, "a", encoding="utf-8") as out:
        while True:
            chunk = f.read()
            if chunk:
                now = time.time()
                buf += chunk
                *lines, buf = buf.split(b"\n")
                for raw in lines:
                    s = raw.decode("utf-8", "replace").rstrip("\r")
                    if KEEP.search(s):
                        out.write(f"{now:.3f} {s[:220]}\n")
                out.flush()
            else:
                # Unity truncates Editor.log when it restarts: start again from the top.
                if os.path.getsize(src) < f.tell():
                    f.seek(0)
                time.sleep(0.02)

if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
