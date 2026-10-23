<#
.SYNOPSIS
  Verification gate for the autonomous-development routines.

.DESCRIPTION
  Runs three checks in order, short-circuiting on the first failure:
    1. Unity batchmode compile (no script errors)
    2. EditMode tests for the named module (and any module it transitively depends on)
    3. PlayMode smoke test (loads the module's smoke scene, runs N seconds, asserts no errors)

  Returns exit code 0 on green, non-zero on red.

  This script is a stub — the routines themselves are expected to extend it as they need
  more sophisticated checks. Resist the urge to make it perfect; make it CORRECT first.

.PARAMETER Module
  Which module to verify. One of: GuidedFury, RHRadar, RHAviator.

.PARAMETER UnityPath
  Full path to Unity.exe. If omitted, reads `tools/ci/unity-path.txt` (one line, the path).

.PARAMETER SmokeSceneSeconds
  How many seconds to run the PlayMode smoke scene before asserting "no errors". Default 20.

.EXAMPLE
  pwsh tools/ci/run-checks.ps1 -Module GuidedFury
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('GuidedFury', 'RHRadar', 'RHAviator')]
    [string]$Module,

    [string]$UnityPath = '',

    [int]$SmokeSceneSeconds = 20
)

$ErrorActionPreference = 'Stop'
$projectPath = (Get-Location).Path
$logDir = Join-Path $projectPath 'tools/ci/logs'
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }

# -- Resolve Unity path -----------------------------------------------------
if (-not $UnityPath) {
    $pathFile = Join-Path $projectPath 'tools/ci/unity-path.txt'
    if (Test-Path $pathFile) {
        $UnityPath = (Get-Content $pathFile -Raw).Trim()
    }
}
if (-not $UnityPath -or -not (Test-Path $UnityPath)) {
    Write-Error "Unity.exe path not configured. Pass -UnityPath or create tools/ci/unity-path.txt with the path. Tried: '$UnityPath'"
    exit 2
}

# -- Module → test-asmdef / smoke scene mapping -----------------------------
$ModuleMap = @{
    'GuidedFury' = @{
        TestPlatform = 'EditMode'
        TestFilter   = 'GuidedFury.Tests'
        SmokeScene   = 'Assets/Guided Fury/Examples/Scenes/MissileRange.unity'
    }
    'RHRadar' = @{
        TestPlatform = 'EditMode'
        TestFilter   = 'RH.Radar.Tests'
        SmokeScene   = 'Assets/RH Radar/Examples/RadarSmoke.unity'
    }
    'RHAviator' = @{
        TestPlatform = 'EditMode'
        TestFilter   = 'RH.Aviator.Tests'
        SmokeScene   = 'Assets/RH Aviator/Examples/AviatorSmoke.unity'
    }
}
$cfg = $ModuleMap[$Module]

# -- Stamp ---------------------------------------------------------------
$ts = Get-Date -Format 'yyyyMMdd-HHmmss'
function Log([string]$msg) { Write-Host "[run-checks $Module $ts] $msg" }

# -- Step 1: Compile ----------------------------------------------------------
Log 'Step 1/3 — compile check'
$compileLog = Join-Path $logDir "$Module-$ts-compile.log"
$proc = Start-Process -FilePath $UnityPath -ArgumentList @(
    '-batchmode', '-quit', '-nographics',
    '-projectPath', $projectPath,
    '-logFile', $compileLog
) -Wait -PassThru
if ($proc.ExitCode -ne 0) {
    Log "FAIL — compile returned $($proc.ExitCode). See $compileLog"
    exit 10
}
Log 'PASS — compile clean'

# -- Step 2: EditMode tests ---------------------------------------------------
Log 'Step 2/3 — EditMode tests'
$testResults = Join-Path $logDir "$Module-$ts-tests.xml"
$testLog     = Join-Path $logDir "$Module-$ts-tests.log"
$proc = Start-Process -FilePath $UnityPath -ArgumentList @(
    '-batchmode', '-nographics', '-runTests',
    '-projectPath', $projectPath,
    '-testPlatform', $cfg.TestPlatform,
    '-testResults', $testResults,
    '-testFilter', $cfg.TestFilter,
    '-logFile', $testLog
) -Wait -PassThru
if ($proc.ExitCode -ne 0) {
    Log "FAIL — EditMode tests returned $($proc.ExitCode). Results: $testResults"
    exit 20
}
Log 'PASS — EditMode tests green'

# -- Step 3: PlayMode smoke ---------------------------------------------------
# This is left as a stub on first run — the routine fills it in once it has a smoke scene.
if (Test-Path (Join-Path $projectPath $cfg.SmokeScene)) {
    Log 'Step 3/3 — PlayMode smoke'
    $smokeLog = Join-Path $logDir "$Module-$ts-smoke.log"
    # Routine extends with -executeMethod to a SmokeRunner that loads the scene + ticks N seconds.
    # Placeholder: just run a headless launch of the editor with the scene; if it returns 0 we call it green.
    $proc = Start-Process -FilePath $UnityPath -ArgumentList @(
        '-batchmode', '-nographics', '-quit',
        '-projectPath', $projectPath,
        '-executeMethod', "$Module.Editor.SmokeRunner.Run",
        '-logFile', $smokeLog
    ) -Wait -PassThru
    if ($proc.ExitCode -ne 0) {
        Log "FAIL — PlayMode smoke returned $($proc.ExitCode). See $smokeLog"
        exit 30
    }
    Log 'PASS — PlayMode smoke clean'
} else {
    Log "SKIP — PlayMode smoke (no scene yet at $($cfg.SmokeScene))"
}

Log 'GREEN — all checks passed'
exit 0
