# Build a player without locking the Unity Editor (Deep Water, BUILD-SPEED-LEDGER: background builds).
#
# 1. Mirror Assets, Packages and ProjectSettings into a sibling project with robocopy /MIR. It copies what is on disk,
#    so uncommitted edits and the untracked DAVFX pack come too. Library, Temp, Logs and Builds stay separate.
# 2. Run a headless Unity on the mirror at below-normal priority: DeepWater.LeanBuild.Editor.BackgroundBuild.Run.
# 3. Copy the player and its report back to Builds/Background/ of this project.
#
# The first run seeds the mirror's Library from this project's (about 10 GB), so the mirror skips a full reimport.
# The seed leaves out EditorInstance.json (it would make the mirror think the project is already open), database
# lock files, and Bee (its build graph holds absolute paths, so the mirror recompiles scripts once). If a database
# was mid-write during the copy, Unity rebuilds it, which costs a reimport and nothing else. Later runs reuse the
# mirror's own Library. With Unity Accelerator configured in both projects, they also share imports and shader variants.
param(
    [Parameter(Mandatory = $true)][string]$Scenes,                 # "Assets/a.unity;Assets/b.unity"
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.4.11f1\Editor\Unity.exe",
    [string]$Source = "",                                          # default: this project (two folders up)
    [string]$Mirror = ""                                           # default: "<this project> BG" next to it
)
$ErrorActionPreference = "Stop"
# Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param() defaults, so resolve here.
if ($Source -eq "") { $Source = (Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "..\..")).Path }
$Source = $Source.TrimEnd('\')
if ($Mirror -eq "") { $Mirror = "$Source BG" }
# /MIR deletes whatever the destination has that the source lacks, so check both ends before copying anything.
if (-not (Test-Path (Join-Path $Source "ProjectSettings\ProjectVersion.txt")) -or -not (Test-Path (Join-Path $Source "Assets"))) {
    Write-Error "Source '$Source' is not a Unity project"; exit 2 }
$fullMirror = [IO.Path]::GetFullPath($Mirror).TrimEnd('\')
if ($fullMirror -eq $Source -or $fullMirror.StartsWith("$Source\")) { Write-Error "Mirror '$fullMirror' must be outside '$Source'"; exit 2 }
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logDir = Join-Path $Mirror "Logs"
New-Item -ItemType Directory -Force -Path $Mirror, $logDir | Out-Null
$log = Join-Path $logDir "background-build-$stamp.log"
function Log([string]$m) { $line = "[$(Get-Date -Format T)] $m"; Write-Output $line; Add-Content -Path $log -Value $line -Encoding UTF8 }
Log "mirror $Source -> $Mirror"

# 1. Mirror. Robocopy exit codes 0-7 are success (8 and up are failures).
foreach ($dir in "Assets", "Packages", "ProjectSettings") {
    robocopy (Join-Path $Source $dir) (Join-Path $Mirror $dir) /MIR /NFL /NDL /NJH /NP /R:1 /W:1 /XF "UnityMcpUnlock.cs*" | Out-Null
    if ($LASTEXITCODE -ge 8) { Log "robocopy $dir failed with $LASTEXITCODE"; exit 2 }
}
Log "mirrored"

# First run only: seed the Library. A file the Editor holds locked is skipped (robocopy 8+); Unity regenerates it.
$mirrorLibrary = Join-Path $Mirror "Library"
if (-not (Test-Path $mirrorLibrary)) {
    robocopy (Join-Path $Source "Library") $mirrorLibrary /E /NFL /NDL /NJH /NP /R:0 /W:0 /XD "Bee" /XF "EditorInstance.json" "*-lock" "*.lock" | Out-Null
    Log "Library seeded from $Source (robocopy $LASTEXITCODE)"
}

# 2. Headless build at below-normal priority, so the interactive Editor keeps the CPU it needs.
$out = Join-Path $Mirror "Builds\Background\$stamp"
$unityLog = Join-Path $logDir "unity-$stamp.log"
$unityArgs = @("-batchmode", "-quit", "-projectPath", "`"$Mirror`"", "-buildTarget", "Win64",
               "-executeMethod", "DeepWater.LeanBuild.Editor.BackgroundBuild.Run", "-bgScenes", "`"$Scenes`"", "-bgOut", "`"$out`"", "-logFile", "`"$unityLog`"")
$p = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru
$null = $p.Handle   # PowerShell 5.1 reports no ExitCode unless the handle is opened while the process runs
$p.PriorityClass = "BelowNormal"
$p.WaitForExit()
Log "Unity exited with $($p.ExitCode); log $unityLog"

# 3. Bring the player back next to the project's other builds.
if ($p.ExitCode -eq 0 -and (Test-Path $out)) {
    $dest = Join-Path $Source "Builds\Background\$stamp"
    robocopy $out $dest /E /NFL /NDL /NJH /NP | Out-Null
    Log "player at $dest"
}
exit $p.ExitCode
