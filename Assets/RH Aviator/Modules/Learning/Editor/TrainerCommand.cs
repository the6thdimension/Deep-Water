using System;
using System.IO;
using System.Text.RegularExpressions;

namespace DeepWater.Aviator.Learning.Editor
{
    /// <summary>
    /// The pure parts of the auto-trainer (A18, M31–M34): which config a scene's brains need, the trainer's command line,
    /// what the trainer's log says, and how many steps a run has trained. No Editor state, so the tests reach it.
    /// </summary>
    public static class TrainerCommand
    {
        /// <summary>What the open scene needs from the trainer: nothing, self-play (both sides learn) or one side against the rest.</summary>
        public enum Mode { None = 0, SelfPlay = 1, VsRest = 2 }

        /// <summary>A run's results folder: none yet, one the trainer can resume (it has a checkpoint), or one it cannot.</summary>
        public enum History { None, Resumable, Unusable }

        public const string ResultsDir = "Training/results";
        public const string LogsDir = ResultsDir + "/auto-trainer-logs";
        public const string SelfPlayConfig = "Training/config/fighter_selfplay.yaml";
        public const string VsRestConfig = "Training/config/fighter_vs_rules.yaml";
        /// <summary>One rolling run per config (M34): every session resumes it, so the brain keeps learning.</summary>
        public const string SelfPlayRun = "auto-selfplay";
        public const string VsRestRun = "auto-vs-rest";
        /// <summary>The trainer's line when it is ready for Play (mlagents_envs, environment.py).</summary>
        public const string ReadyMarker = "Listening on port";
        /// <summary>The trainer's line when Play connected to it.</summary>
        public const string ConnectedMarker = "Connected to Unity environment";
        /// <summary>How long a ready trainer waits for Play before it gives up: a week, so it waits as long as the Editor is open.</summary>
        public const int WaitForPlayS = 7 * 24 * 3600;

        /// <summary>Self-play needs learning jets on both sides (ML-Agents' ghost trainer swaps which side learns); one side
        /// alone trains against whatever flies the other, with the config that has no self-play.</summary>
        public static Mode ModeFor(int blueLearning, int redLearning)
        {
            if (blueLearning > 0 && redLearning > 0) return Mode.SelfPlay;
            return blueLearning > 0 || redLearning > 0 ? Mode.VsRest : Mode.None;
        }

        public static string ConfigOf(Mode mode) => mode == Mode.SelfPlay ? SelfPlayConfig : VsRestConfig;
        public static string RunOf(Mode mode) => mode == Mode.SelfPlay ? SelfPlayRun : VsRestRun;

        /// <summary>What a run's folder holds: the trainer resumes from <c>Fighter/checkpoint.pt</c>; a folder without it
        /// (a session that never trained a step) would stop the trainer either way, so it is set aside.</summary>
        public static History HistoryOf(string runDir)
        {
            if (!Directory.Exists(runDir)) return History.None;
            return File.Exists(Path.Combine(runDir, "Fighter", "checkpoint.pt")) ? History.Resumable : History.Unusable;
        }

        /// <summary>
        /// mlagents-learn's arguments: the config, the run, where results go, a week's wait for Play, no restarts, and
        /// resume. <c>--max-lifetime-restarts=0</c>: by default mlagents 1.1.0 treats Play stopping as a crashed
        /// environment, starts listening again and saves nothing until its next checkpoint (500,000 steps); with no restarts
        /// allowed it ends the run instead, which saves the brain and exits (found 2026-10-29: a session's 20,000 steps
        /// were lost to the restart).
        /// </summary>
        public static string Arguments(string config, string runId, string resultsDir, bool resume)
        {
            return $"\"{config}\" --run-id={runId} --results-dir=\"{resultsDir}\" --timeout-wait={WaitForPlayS} --max-lifetime-restarts=0" + (resume ? " --resume" : "");
        }

        /// <summary>
        /// The whole command for cmd.exe: the trainer's output, both streams, into the log file, so it outlives a domain
        /// reload (a pipe read by the Editor would not: the trainer blocks when nobody reads it).
        /// </summary>
        public static string CmdLine(string exe, string arguments, string logPath)
        {
            return $"/d /s /c \"\"{exe}\" {arguments} > \"{logPath}\" 2>&1\"";
        }

        /// <summary>The steps of a run's final checkpoint, from its <c>run_logs/training_status.json</c>; −1 when it has none.</summary>
        public static int FinalSteps(string trainingStatusJson)
        {
            if (string.IsNullOrEmpty(trainingStatusJson)) return -1;
            int final = trainingStatusJson.IndexOf("\"final_checkpoint\"", StringComparison.Ordinal);
            if (final < 0) return -1;
            Match m = Regex.Match(trainingStatusJson.Substring(final), "\"steps\"\\s*:\\s*(\\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out int steps) ? steps : -1;
        }

        /// <summary>The last <paramref name="lines"/> non-empty lines of a log, for a failure message.</summary>
        public static string Tail(string log, int lines)
        {
            if (string.IsNullOrEmpty(log)) return "";
            string[] all = log.Replace("\r", "").Split('\n');
            int end = all.Length;
            while (end > 0 && all[end - 1].Trim().Length == 0) end--;
            int start = Math.Max(0, end - lines);
            return string.Join("\n", all, start, end - start);
        }

        /// <summary>A name that is safe as a file name: anything else becomes '-'.</summary>
        public static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            char[] chars = name.Trim().ToCharArray();
            char[] bad = Path.GetInvalidFileNameChars();
            for (int i = 0; i < chars.Length; i++) if (Array.IndexOf(bad, chars[i]) >= 0 || chars[i] == ' ') chars[i] = '-';
            return new string(chars);
        }
    }
}
