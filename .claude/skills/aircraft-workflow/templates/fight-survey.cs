// Unity_RunCommand body: the fight survey. N neutral merges per matchup, then the 4 v 4, headless, every fight scored
// two ways: rear-cone seconds by POSITION (the score, W46; A9 declined) and POINTED (in the cone AND the attacker's
// flight path within the cone's angle of the target: the check A9 proposed, removed in c1501942, kept here as slice
// 3f's diagnostic, A11). Modelled on FightTests.Survey and FightTests.Fly; same starts, same scoring order.
// One fight per Editor update, so the main thread is never held for long (unity-mcp-workflow trap 12); the report is
// written to __OUT__ (absolute, forward slashes) at the end and finishes with "# end".
//   Before: rm the old report in its own shell command (aircraft-workflow trap 1). No Test Runner: a dirty scene is fine.
//   Wait:   wait_file.sh <out> 900 '^# end'      (about a minute for the default list; a 4 v 4 is a few seconds)
//   A compile or a domain reload during the run kills it (no "# end"): wait 30 s and send it again.
// Calibration: with Starts 0 and guns safe, the position columns must equal FightTests.Survey's [Aviator survey]
// lines on the same code. If they don't, this file has drifted from the test; fix it before reading the pointed ones.
// Edit the marked parts: Starts, N, GunsHot, SinkMarginM and the matchups.
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DeepWater.AirWing.Core;
using DeepWater.AirWing.Core.Fidelity;
using DeepWater.AirWing.Core.Inputs;
using DeepWater.AirWing.Core.Profiles;
using DeepWater.AirWing.Core.Seats;
using DeepWater.AirWing.Core.Simulation;
using DeepWater.AirWing.Core.State;
using DeepWater.AirWing.ScriptableObjects;
using DeepWater.Aviator.Core;
using DeepWater.Aviator.Editor;
using DeepWater.Aviator.ScriptableObjects;

// Top-level helpers, not nested: RunCommand copies nested types to namespace scope (unity-mcp-workflow trap 16).
internal sealed class SurveyLine
{
    public string Name;
    public bool Brawl;
    public int Fights, BlueAhead, RedAhead, BluePointedAhead, RedPointedAhead, UnderFloor, Crashes, BlueKills, RedKills;
    public double Blue, Red, BluePointed, RedPointed;
    public float LowestFt = float.MaxValue, ClosestM = float.MaxValue;
    public string FirstCrash;
}

internal sealed class SurveyJob
{
    public SurveyLine Line;
    public PilotProfile Blue, Red;
    public int Seed;
    public bool HandsOff;
}

internal class CommandScript : IRunCommand
{
    // ── EDIT ──
    const string Out = "__OUT__";
    // 0: FightTests.Survey's merges (seed 7000 + i; 400 kt; blue from the south).
    // 1: the 3d pass test's (FightTests.Fly: seed 5000 + i; 380–420 kt; the side drawn). The ROADMAP's ace v rookie quotes these.
    // 2: GunFightTests' (seed 5000 + i; 380–420 kt; blue from the south).
    const int Starts = 0;
    const int N = 20;
    const bool GunsHot = false;        // false: TriggerMils 0, the position fight (as FightTests). true: armed (A12), kills first
    const float SinkMarginM = 0f;      // "under floor" = a jet below its FightFloorM + this. 3f fixes its margin before building
    const string Key = "DeepWater.Aviator.FightSurvey.Heartbeat";
    // Units and Aircraft spelled in full or var: FlightSimLite's global-namespace Units and Aircraft win over `using` here
    // (aircraft-workflow trap 12).
    const float Kt = (float)DeepWater.AirWing.Core.Units.KnotToMetrePerSecond, Ft = 0.3048f;

    static FidelitySheet s_l1;
    static AircraftProfile s_e;
    static PilotProfile s_rookie, s_line, s_ace;
    static List<SurveyLine> s_lines;
    static List<SurveyJob> s_jobs;
    static int s_next;
    static double s_t0;

    public void Execute(ExecutionResult result)
    {
        // Idempotent (trap 12): a re-sent copy must not start a second survey. Statics belong to each command's own
        // assembly, so the guard is a heartbeat in SessionState, beaten by the running survey every update.
        float beat = SessionState.GetFloat(Key, -1f);
        if (beat >= 0f && EditorApplication.timeSinceStartup - beat < 30.0) { result.Log("a fight survey is already running: not starting another"); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) { result.LogError("stop play mode first: the survey is headless"); return; }

        AviatorTools.ApplyPilotProfiles();   // the assets match the tools' numbers
        s_l1 = new FidelitySheet(FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L1, FidelityLevel.L0, 4, 1442);
        s_e = AssetDatabase.LoadAssetAtPath<AircraftProfileSO>("Assets/Air Wing/ScriptableObjects/FA18E_AirWing.asset").Bake();
        s_rookie = Pilot(AviatorTools.RookiePilotPath);
        s_line = Pilot(AviatorTools.LinePilotPath);
        s_ace = Pilot(AviatorTools.AcePilotPath);
        s_lines = new List<SurveyLine>();
        s_jobs = new List<SurveyJob>();
        s_next = 0;

        // ── EDIT: the matchups, blue first. 3f's criteria name line v line and ace v line ──
        Merges("ace v rookie", s_ace, s_rookie);
        Merges("line v line", s_line, s_line);
        Merges("ace v line", s_ace, s_line);
        FourVersusFour("4 v 4, the player's jet fighting (line)", false);
        // FourVersusFour("4 v 4, the player's jet hands off", true);

        s_t0 = EditorApplication.timeSinceStartup;
        SessionState.SetFloat(Key, (float)s_t0);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        result.Log($"fight survey started: {s_jobs.Count} fights, starts {Starts}, guns {(GunsHot ? "hot" : "safe")}; report to {Out}");
    }

    static PilotProfile Pilot(string path)
    {
        PilotProfile p = AssetDatabase.LoadAssetAtPath<PilotProfileSO>(path).Bake();
        if (!GunsHot) p.TriggerMils = 0f;   // guns safe: the position fight (FightTests.Pilot)
        return p;
    }

    static void Merges(string name, PilotProfile blue, PilotProfile red)
    {
        var line = new SurveyLine { Name = name };
        s_lines.Add(line);
        for (int i = 0; i < N; i++) s_jobs.Add(new SurveyJob { Line = line, Blue = blue, Red = red, Seed = i });
    }

    static void FourVersusFour(string name, bool handsOff)
    {
        var line = new SurveyLine { Name = name, Brawl = true };
        s_lines.Add(line);
        s_jobs.Add(new SurveyJob { Line = line, HandsOff = handsOff });
    }

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetFloat(Key, (float)EditorApplication.timeSinceStartup);
        if (s_next < s_jobs.Count)
        {
            SurveyJob j = s_jobs[s_next++];
            if (j.Line.Brawl) FlyBrawl(j); else FlyMerge(j);
            return;
        }
        EditorApplication.update -= Tick;
        Write();
        SessionState.EraseFloat(Key);
    }

    static AirframeState Start(Vector3 at, float headingDeg, float kt) =>
        AircraftFactory.LevelStart(s_e, s_l1, at, headingDeg, kt * Kt, 0.6f * s_e.Airframe.InternalFuelKg, 0.7f);

    /// <summary>A9's pointing test on top of the rear cone (the lines c1501942 took out of RearCone.Inside).</summary>
    static bool Pointed(AirframeState attacker, AirframeState target, float coneDeg, float rangeM)
    {
        if (!RearCone.Inside(attacker, target, coneDeg, rangeM)) return false;
        Vector3 heading = attacker.Velocity.sqrMagnitude > 1f ? attacker.Velocity : attacker.Attitude * Vector3.forward;
        return Vector3.Angle(heading, target.Position - attacker.Position) <= coneDeg;
    }

    /// <summary>A neutral merge: head on, 6 km apart, offset to one side; blue is aircraft 1, red 2; 180 s.</summary>
    static void FlyMerge(SurveyJob j)
    {
        var rng = new DeterministicRng((ulong)((Starts == 0 ? 7000 : 5000) + j.Seed));
        float h = (9000f + 2000f * rng.NextFloat01()) * Ft;
        float off = (rng.NextFloat01() < 0.5f ? -1f : 1f) * (200f + 300f * rng.NextFloat01());
        float kt = Starts == 0 ? 400f : 380f + 40f * rng.NextFloat01();
        bool blueSouth = Starts != 1 || rng.NextFloat01() < 0.5f;
        Vector3 south = new Vector3(0f, h, -3000f), north = new Vector3(off, h, 3000f);

        var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: GunsHot ? (ulong)(900 + j.Seed) : 1UL);
        var fight = new Engagement();
        var blue = new AviatorSeat(core, s_e, j.Blue, new RuleLog());
        var red = new AviatorSeat(core, s_e, j.Red, new RuleLog());
        core.Add(AircraftFactory.Build(1, s_e, s_l1, Start(blueSouth ? south : north, blueSouth ? 0f : 180f, kt)), blue);
        core.Add(AircraftFactory.Build(2, s_e, s_l1, Start(blueSouth ? north : south, blueSouth ? 180f : 0f, kt)), red);
        fight.Join(1, Side.Blue);
        fight.Join(2, Side.Red);
        blue.FightIn(1, fight);
        red.FightIn(2, fight);

        double dt = System.Math.Round(core.Clock.TickSeconds, 6), bp = 0, rp = 0;
        float lowest = float.MaxValue;
        bool under = false;
        string crash = null;
        while (core.Tick < 180 * 50 && crash == null && core.Kills.Count == 0)   // guns hot: to the first kill (GunFightTests)
        {
            core.Step();
            fight.Score(core);
            var a1 = core.Find(1);
            var a2 = core.Find(2);
            if (Pointed(a1.State, a2.State, fight.ConeDeg, fight.RangeM)) bp += dt;
            if (Pointed(a2.State, a1.State, fight.ConeDeg, fight.RangeM)) rp += dt;
            foreach (var a in core.Aircraft)
            {
                if (a.KilledBy != 0) continue;   // a jet shot down falls by design: not a sink, not a crash
                if (a.State.Status == FlightStatus.Crashed) crash = $"aircraft {a.Id}: {a.CrashReason}";
                lowest = Mathf.Min(lowest, a.State.Position.y / Ft);
                float floor = (a.Id == 1 ? j.Blue.FightFloorM : j.Red.FightFloorM) + SinkMarginM;
                if (a.State.Position.y < floor) under = true;
            }
        }

        SurveyLine l = j.Line;
        double x = fight.SecondsInCone(1), y = fight.SecondsInCone(2);
        l.Fights++;
        l.Blue += x; l.Red += y; l.BluePointed += bp; l.RedPointed += rp;
        if (x > y) l.BlueAhead++; else if (y > x) l.RedAhead++;
        if (bp > rp) l.BluePointedAhead++; else if (rp > bp) l.RedPointedAhead++;
        l.BlueKills += fight.SideKills(Side.Blue);
        l.RedKills += fight.SideKills(Side.Red);
        l.LowestFt = Mathf.Min(l.LowestFt, lowest);
        if (under) l.UnderFloor++;
        if (crash != null) { l.Crashes++; if (l.FirstCrash == null) l.FirstCrash = $"merge {j.Seed}, {crash}"; }
    }

    /// <summary>FightTests.Survey's 4 v 4: four line blue (aircraft 1 is the player's), red rookie, line, line, ace; red hunts 1; 300 s.</summary>
    static void FlyBrawl(SurveyJob j)
    {
        var core = new FlightCore(new SubstepClock(0.02f, 4), new FlatSurface(0f), gunnerySeed: GunsHot ? 4242UL : 1UL);
        var fight = new Engagement { Hunted = () => 1 };
        PilotProfile[] reds = { s_rookie, s_line, s_line, s_ace };
        var floors = new float[9];
        var seats = new AviatorSeat[9];
        for (int id = 1; id <= 8; id++)
        {
            bool isBlue = id <= 4;
            PilotProfile p = isBlue ? s_line : reds[id - 5];
            Vector3 at = isBlue ? new Vector3(60f * (id - 1), 3048f, -60f * (id - 1)) : new Vector3(-300f + 200f * (id - 5), 3500f, 20000f);
            ISeat seat;
            if (id == 1 && j.HandsOff) { seat = new HoldSeat(new PilotInputs { Throttle = 0.8f }); floors[id] = float.MinValue; }
            else { seats[id] = new AviatorSeat(core, s_e, p, new RuleLog()); seat = seats[id]; floors[id] = p.FightFloorM + SinkMarginM; }
            core.Add(AircraftFactory.Build(id, s_e, s_l1, Start(at, isBlue ? 0f : 180f, 400f)), seat);
            fight.Join(id, isBlue ? Side.Blue : Side.Red);
        }
        // Every side joins first, then the pilots are told to fight (as FightTests.Fly does).
        for (int id = 1; id <= 8; id++)
            if (seats[id] != null) seats[id].FightIn(id, fight);

        double dt = System.Math.Round(core.Clock.TickSeconds, 6), bp = 0, rp = 0;
        SurveyLine l = j.Line;
        string crash = null;
        bool under = false;
        while (core.Tick < 300 * 50 && crash == null)
        {
            core.Step();
            fight.Score(core);
            var all = core.Aircraft;
            for (int i = 0; i < all.Count; i++)
            {
                for (int k = 0; k < all.Count; k++)
                {
                    if (i == k || !fight.Enemies(all[i].Id, all[k].Id)) continue;
                    if (!Pointed(all[i].State, all[k].State, fight.ConeDeg, fight.RangeM)) continue;
                    if (fight.SideOf(all[i].Id) == Side.Blue) bp += dt; else rp += dt;
                }
                if (all[i].KilledBy != 0) continue;
                if (all[i].State.Status == FlightStatus.Crashed) crash = $"aircraft {all[i].Id}: {all[i].CrashReason}";
                l.LowestFt = Mathf.Min(l.LowestFt, all[i].State.Position.y / Ft);
                if (all[i].State.Position.y < floors[all[i].Id]) under = true;
                for (int k = i + 1; k < all.Count; k++) l.ClosestM = Mathf.Min(l.ClosestM, (all[i].State.Position - all[k].State.Position).magnitude);
            }
        }
        l.Fights++;
        l.Blue += fight.SideSeconds(Side.Blue); l.Red += fight.SideSeconds(Side.Red);
        l.BluePointed += bp; l.RedPointed += rp;
        l.BlueKills += fight.SideKills(Side.Blue);
        l.RedKills += fight.SideKills(Side.Red);
        if (under) l.UnderFloor++;
        if (crash != null) { l.Crashes++; l.FirstCrash = crash; }
    }

    static void Write()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(System.FormattableString.Invariant($"# fight survey {System.DateTime.Now:yyyy-MM-dd HH:mm}: starts {Starts}, guns {(GunsHot ? "hot" : "safe")}, {N} merges of 180 s a matchup, sink margin {SinkMarginM:F0} m; {EditorApplication.timeSinceStartup - s_t0:F0} s"));
        sb.AppendLine("| Matchup (blue v red) | Fights | Ahead by position (blue–red–even) | Position s a fight (blue / red) | Ahead by pointed | Pointed s a fight (blue / red) | Pointed share of position | Kills (blue / red) | Lowest ft | Under floor | Crashes |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (SurveyLine l in s_lines)
        {
            int n = Mathf.Max(1, l.Fights);
            double pos = l.Blue + l.Red, pointed = l.BluePointed + l.RedPointed;
            string share = pos > 0 ? System.FormattableString.Invariant($"{100.0 * pointed / pos:F0} %") : "—";
            string ahead = l.Brawl ? "—" : $"{l.BlueAhead}–{l.RedAhead}–{l.Fights - l.BlueAhead - l.RedAhead}";
            string pAhead = l.Brawl ? "—" : $"{l.BluePointedAhead}–{l.RedPointedAhead}–{l.Fights - l.BluePointedAhead - l.RedPointedAhead}";
            string closest = l.Brawl ? System.FormattableString.Invariant($"; closest {l.ClosestM:F0} m") : "";
            string row = System.FormattableString.Invariant(
                $"| {l.Name} | {l.Fights} | {ahead} | {l.Blue / n:F1} / {l.Red / n:F1} | {pAhead} | {l.BluePointed / n:F1} / {l.RedPointed / n:F1} | {share} | {l.BlueKills} / {l.RedKills} | {l.LowestFt:F0}{closest} | {l.UnderFloor} | {l.Crashes}{(l.FirstCrash != null ? " (" + l.FirstCrash + ")" : "")} |");
            sb.AppendLine(row);
            Debug.Log("[Aviator fight survey] " + row);
        }
        sb.AppendLine("# end");
        System.IO.File.WriteAllText(Out, sb.ToString());
    }
}
