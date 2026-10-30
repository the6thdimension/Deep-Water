# Headless training for RH Aviator's learned fighter pilots (TRAINING-CONCEPT M28; BLUEPRINT A19).
#
#   -Action start    Start the supervisor, a hidden PowerShell that runs mlagents-learn on headless players of
#                    Dogfight Training (-Envs of them, no graphics). It resumes the rolling run the Editor's auto-train
#                    uses (auto-selfplay), so both train one brain, never at the same time. If the trainer ends without
#                    a stop being asked for (a player crashed), it starts it again, resuming; three quick failures in a
#                    row stop it.
#   -Action stop     Ask it to stop: it ends the players, so the trainer saves the brain and exits (mlagents with
#                    --max-lifetime-restarts=0 ends the run when its environment goes, as when Play stops).
#   -Action status   The supervisor, the trainer, the players and the last line of progress.
#
# The player is built by Lean Build: tools/background-build/bg_build.ps1 -Scenes "Assets/_SCENES/Dogfight Training.unity"
# -Profile Release. To fly the result in the Editor: Deep Water > RH Aviator > Take the Newest Headless Brain.
# Everything it writes goes to Training/results (git-ignored): headless/ holds its state, its log and the trainer logs.
param(
    [ValidateSet("start", "stop", "status", "supervise")][string]$Action = "status",
    [int]$Envs = 4,
    [string]$Run = "auto-selfplay",
    [string]$Config = "Training/config/fighter_selfplay.yaml",
    [string]$Player = "Builds/Background/Release/Dogfight_Training/Dogfight_Training.exe",
    [string]$Venv = ""
)
$ErrorActionPreference = "Stop"
if ($Venv -eq "") { $Venv = Join-Path $env:USERPROFILE "venvs\mlagents-1.1.0" }
$Root = (Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..\..")).Path
Set-Location $Root
$Results = Join-Path $Root "Training\results"
$StateDir = Join-Path $Results "headless"
$StateFile = Join-Path $StateDir "state.json"
$StopFile = Join-Path $StateDir "STOP"
$SupervisorLog = Join-Path $StateDir "supervisor.log"
$PlayerName = [IO.Path]::GetFileNameWithoutExtension($Player)
$Trainer = Join-Path $Venv "Scripts\mlagents-learn.exe"
New-Item -ItemType Directory -Force -Path $StateDir | Out-Null

function Log([string]$m) {
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $m"
    Add-Content -Path $SupervisorLog -Value $line -Encoding UTF8
    Write-Output $line
}
function Alive($id) { if (-not $id) { return $false }; return [bool](Get-Process -Id $id -ErrorAction SilentlyContinue) }
function ReadState { if (Test-Path $StateFile) { return Get-Content $StateFile -Raw | ConvertFrom-Json }; return $null }
function WriteState($s) { $s | ConvertTo-Json | Set-Content -Path $StateFile -Encoding UTF8 }
# Any mlagents-learn on this run: ours, or the Editor's auto-train (two trainers on one run overwrite each other).
function TrainersOnRun {
    return @(Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
        Where-Object { $_.CommandLine -match "mlagents-learn" -and $_.CommandLine -match "--run-id=$Run(\s|$)" })
}
function LastProgress([string]$log) {
    if (-not $log -or -not (Test-Path $log)) { return "no log yet" }
    $l = Select-String -Path $log -Pattern "Step: \d+" | Select-Object -Last 1
    if ($l) { return $l.Line.Trim() }
    return "no steps yet"
}

switch ($Action) {
    "start" {
        $s = ReadState
        if ($s -and (Alive $s.supervisor)) { Write-Output "Already running: supervisor $($s.supervisor). -Action status or -Action stop."; exit 0 }
        if (-not (Test-Path $Player)) { Write-Error "No player at $Player. Build it: tools/background-build/bg_build.ps1 -Scenes 'Assets/_SCENES/Dogfight Training.unity' -Profile Release"; exit 2 }
        if (-not (Test-Path $Trainer)) { Write-Error "No trainer at $Trainer (TRAINING-CONCEPT M28)"; exit 2 }
        if ((TrainersOnRun).Count -gt 0) { Write-Error "A trainer is already on $Run (the Editor's auto-train?). Switch auto-train off first."; exit 3 }
        if (Test-Path $StopFile) { Remove-Item $StopFile }   # the last run's stop request, done with
        # Created through WMI, so it is nobody's child: it outlives the shell (and the session) that started it.
        $cmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$PSCommandPath`" -Action supervise -Envs $Envs -Run $Run -Config `"$Config`" -Player `"$Player`" -Venv `"$Venv`""
        $r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{ CommandLine = $cmd; CurrentDirectory = $Root }
        if ($r.ReturnValue -ne 0) { Write-Error "Could not start the supervisor (Win32_Process.Create returned $($r.ReturnValue))"; exit 2 }
        WriteState @{ supervisor = $r.ProcessId; trainer = $null; log = $null; run = $Run; envs = $Envs; started = (Get-Date).ToString("u") }
        Write-Output "Headless training started: supervisor $($r.ProcessId), $Envs players, run $Run. Log: $SupervisorLog"
    }
    "supervise" {
        $env:PYTHONUNBUFFERED = "1"      # progress reaches the log as it is said
        $env:PYTHONIOENCODING = "utf-8"  # the banner does not stop it on a cp1252 console
        $resultsArg = $Results -replace '\\', '/'
        $quick = 0
        Log "supervisor ${PID}: $Envs players of $Player, run $Run, $Config"
        while (-not (Test-Path $StopFile)) {
            $runDir = Join-Path $Results $Run
            $resume = Test-Path (Join-Path $runDir "Fighter\checkpoint.pt")
            if ((Test-Path $runDir) -and -not $resume) {
                $aside = "$Run.pre-unusable-backup-$(Get-Date -Format yyyyMMdd-HHmm)"
                Rename-Item -Path $runDir -NewName $aside
                Log "$Run had no checkpoint to resume: set aside as $aside (nothing deleted)"
            }
            # The trainer writes each player's log afresh to run_logs/Player-N.log on every start, so a restart used to lose
            # the who-shot-whom counts before it (sensors-bc, 2026-10-30): move the last start's logs aside first.
            $playerLogs = @(Get-ChildItem -Path (Join-Path $runDir "run_logs") -Filter "Player-*.log" -ErrorAction SilentlyContinue)
            if ($playerLogs.Count -gt 0) {
                $kept = Join-Path $runDir ("run_logs\players-" + ($playerLogs | Sort-Object LastWriteTime | Select-Object -Last 1).LastWriteTime.ToString("yyyyMMdd-HHmmss"))
                New-Item -ItemType Directory -Force -Path $kept | Out-Null
                $playerLogs | Move-Item -Destination $kept
                Log "the last start's $($playerLogs.Count) player logs kept in $kept"
            }
            $log = Join-Path $StateDir ("trainer-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".log")
            $flags = "`"$Config`" --run-id=$Run --results-dir=`"$resultsArg`" --env=`"$Player`" --num-envs=$Envs --no-graphics --timeout-wait=600 --max-lifetime-restarts=0"
            if ($resume) { $flags += " --resume" }
            $t0 = Get-Date
            $p = Start-Process cmd.exe -ArgumentList "/d /s /c `"`"$Trainer`" $flags > `"$log`" 2>&1`"" -WindowStyle Hidden -PassThru
            $st = ReadState; $st.trainer = $p.Id; $st.log = $log; WriteState $st
            Log "trainer $($p.Id): $(if ($resume) { 'resuming' } else { 'a new brain' }); log $log"
            $p.WaitForExit()
            $minutes = [Math]::Round(((Get-Date) - $t0).TotalMinutes, 1)
            Log "trainer ended after $minutes min; last: $(LastProgress $log)"
            if (Test-Path $StopFile) { break }
            if ($minutes -lt 3) { $quick++ } else { $quick = 0 }
            if ($quick -ge 3) { Log "STOPPED: the trainer ended within 3 minutes three times in a row; see $log"; break }
            Start-Sleep -Seconds 10
        }
        Log "supervisor done"
    }
    "stop" {
        $s = ReadState
        New-Item -ItemType File -Force -Path $StopFile | Out-Null
        $players = @(Get-Process -Name $PlayerName -ErrorAction SilentlyContinue)
        if ($players.Count -gt 0) {
            $players | Stop-Process -Force
            Log "stop: ended $($players.Count) players; the trainer saves the brain and exits"
        }
        $deadline = (Get-Date).AddMinutes(5)
        while ((Get-Date) -lt $deadline -and $s -and ((Alive $s.supervisor) -or (Alive $s.trainer))) { Start-Sleep -Seconds 2 }
        if ($s -and ((Alive $s.supervisor) -or (Alive $s.trainer))) { Write-Output "Still saving after 5 minutes: -Action status, and the trainer log $($s.log)." }
        else { Write-Output "Stopped. $(if ($s) { LastProgress $s.log })" }
    }
    "status" {
        $s = ReadState
        if (-not $s) { Write-Output "Never started."; exit 0 }
        $players = @(Get-Process -Name $PlayerName -ErrorAction SilentlyContinue).Count
        Write-Output "Supervisor $($s.supervisor): $(if (Alive $s.supervisor) { 'running' } else { 'not running' }); trainer $($s.trainer): $(if (Alive $s.trainer) { 'running' } else { 'not running' }); players $players of $($s.envs); run $($s.run), started $($s.started)."
        Write-Output (LastProgress $s.log)
    }
}
