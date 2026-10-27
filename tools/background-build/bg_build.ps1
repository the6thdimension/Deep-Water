# Build a player without locking the Unity Editor (Deep Water, BUILD-SPEED-LEDGER: background builds).
#
# 1. Mirror Assets, Packages and ProjectSettings into a sibling project with robocopy /MIR. It copies what is on disk,
#    so uncommitted edits and the untracked DAVFX pack come too. Library, Temp, Logs and Builds stay separate.
# 2. Run a headless Unity on the mirror at below-normal priority: DeepWater.LeanBuild.Editor.BackgroundBuild.Run,
#    with a Lean Build profile (Assets/Lean Build/Profiles: Release, Iterate).
# 3. Copy the player and its report back to Builds/Background/<profile>/<scene>/ of this project.
#
# The first run seeds the mirror's Library from this project's (about 10 GB), so the mirror skips a full reimport.
# The seed leaves out EditorInstance.json (it would make the mirror think the project is already open), database
# lock files, and Bee (its build graph holds absolute paths, so the mirror recompiles scripts once). If a database
# was mid-write during the copy, Unity rebuilds it, which costs a reimport and nothing else. Later runs reuse the
# mirror's own Library. With Unity Accelerator configured in both projects, they also share imports and shader variants.
#
# Scripts-only builds: each profile keeps the files changed since its own last successful build in
# <mirror>/Library/LeanBuild/pending-<profile>.txt. Every mirror copy appends what it copied or deleted to each
# existing list; a successful build of a profile empties its own. The headless build reads the list and builds only
# the scripts when the profile allows it and nothing but code changed (BuildPlan in Assets/Lean Build/Editor).
param(
    [Parameter(Mandatory = $true)][string]$Scenes,                 # "Assets/a.unity;Assets/b.unity"
    [string]$Profile = "Release",                                  # a profile asset in Assets/Lean Build/Profiles
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
$Mirror = [IO.Path]::GetFullPath($Mirror).TrimEnd('\')
if ($Mirror -eq $Source -or $Mirror.StartsWith("$Source\")) { Write-Error "Mirror '$Mirror' must be outside '$Source'"; exit 2 }
if (-not (Test-Path (Join-Path $Source "Assets\Lean Build\Profiles\$Profile.asset"))) { Write-Error "No profile '$Profile' in Assets\Lean Build\Profiles"; exit 2 }
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logDir = Join-Path $Mirror "Logs"
New-Item -ItemType Directory -Force -Path $Mirror, $logDir | Out-Null
$log = Join-Path $logDir "background-build-$stamp.log"
function Log([string]$m) { $line = "[$(Get-Date -Format T)] $m"; Write-Output $line; Add-Content -Path $log -Value $line -Encoding UTF8 }

# A Unity holds Temp\UnityLockfile open while it has the project open. Mirroring under it would change files that
# another build or test run is using, so stop instead. (A stale lock file from a crashed Unity opens fine.)
$lockFile = Join-Path $Mirror "Temp\UnityLockfile"
if (Test-Path $lockFile) {
    try { $fs = [IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None'); $fs.Close() }
    catch { Log "STOP: a Unity has $Mirror open (another build or test run). Try again when it ends, or pass -Mirror <another folder>."; exit 3 }
}
Log "$Profile build: mirror $Source -> $Mirror"

# 1. Mirror. First list what robocopy would copy or delete (/L; full paths, no sizes or classes). Robocopy goes by
#    size and time, and the Editor re-saves settings files with the same content, so a file in both places counts as
#    changed only if its content differs. Then copy. Robocopy exit codes 0-7 are success (8 and up are failures).
$changed = New-Object System.Collections.Generic.List[string]
$touched = 0
foreach ($dir in "Assets", "Packages", "ProjectSettings") {
    $src = Join-Path $Source $dir; $dst = Join-Path $Mirror $dir
    $lines = robocopy $src $dst /MIR /L /NDL /NJH /NJS /NP /NS /NC /FP /R:1 /W:1 /XF "UnityMcpUnlock.cs*"
    if ($LASTEXITCODE -ge 8) { Log "robocopy $dir (list) failed with $LASTEXITCODE"; exit 2 }
    foreach ($l in $lines) {
        $path = $l.Trim()
        if ($path -eq "") { continue }
        $rel = $null
        foreach ($root in $Source, $Mirror) { if ($path.StartsWith("$root\")) { $rel = $path.Substring($root.Length + 1); break } }
        if ($rel -eq $null) { continue }
        $touched++
        $a = Join-Path $Source $rel; $b = Join-Path $Mirror $rel
        if ((Test-Path -LiteralPath $a) -and (Test-Path -LiteralPath $b) -and
            (Get-FileHash -LiteralPath $a -Algorithm SHA1).Hash -eq (Get-FileHash -LiteralPath $b -Algorithm SHA1).Hash) { continue }
        $changed.Add($rel.Replace('\', '/'))
    }
    robocopy $src $dst /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 /XF "UnityMcpUnlock.cs*" | Out-Null
    if ($LASTEXITCODE -ge 8) { Log "robocopy $dir failed with $LASTEXITCODE"; exit 2 }
}
Log "mirrored: $touched files copied or deleted, $($changed.Count) with new content"

# First run only: seed the Library. A file the Editor holds locked is skipped (robocopy 8+); Unity regenerates it.
$mirrorLibrary = Join-Path $Mirror "Library"
if (-not (Test-Path $mirrorLibrary)) {
    robocopy (Join-Path $Source "Library") $mirrorLibrary /E /NFL /NDL /NJH /NP /R:0 /W:0 /XD "Bee" /XF "EditorInstance.json" "*-lock" "*.lock" | Out-Null
    Log "Library seeded from $Source (robocopy $LASTEXITCODE)"
}

# Every profile that has built here before learns what changed since.
$pendingDir = Join-Path $mirrorLibrary "LeanBuild"
New-Item -ItemType Directory -Force -Path $pendingDir | Out-Null
if ($changed.Count -gt 0) {
    # .NET writes UTF-8 without a byte-order mark (Add-Content and Set-Content in 5.1 add one).
    Get-ChildItem -Path $pendingDir -Filter "pending-*.txt" | ForEach-Object { [IO.File]::AppendAllLines($_.FullName, [string[]]$changed) }
}
$pending = Join-Path $pendingDir "pending-$Profile.txt"

# 2. Headless build at below-normal priority, so the interactive Editor keeps the CPU it needs. The output folder is
#    fixed per profile and scene, because a scripts-only build patches the last build in place.
$sceneName = [IO.Path]::GetFileNameWithoutExtension(($Scenes -split ';')[0]).Replace(' ', '_')
$out = Join-Path $Mirror "Builds\Background\$Profile\$sceneName"
$unityLog = Join-Path $logDir "unity-$stamp.log"
$unityArgs = @("-batchmode", "-quit", "-projectPath", "`"$Mirror`"", "-buildTarget", "Win64",
               "-executeMethod", "DeepWater.LeanBuild.Editor.BackgroundBuild.Run", "-bgScenes", "`"$Scenes`"", "-bgOut", "`"$out`"",
               "-bgProfile", $Profile, "-logFile", "`"$unityLog`"")
if (Test-Path $pending) { $unityArgs += @("-bgChanged", "`"$pending`"") }
$p = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru
$null = $p.Handle   # PowerShell 5.1 reports no ExitCode unless the handle is opened while the process runs
$p.PriorityClass = "BelowNormal"
$p.WaitForExit()
Log "Unity exited with $($p.ExitCode); log $unityLog"

# 3. On success this profile is up to date: empty its list, and bring the player back next to the project's other builds.
if ($p.ExitCode -eq 0 -and (Test-Path $out)) {
    [IO.File]::WriteAllText($pending, "")
    $dest = Join-Path $Source "Builds\Background\$Profile\$sceneName"
    robocopy $out $dest /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    Log "player at $dest"
    Get-Content (Join-Path $out "background-build-report.txt") | Select-Object -First 3 | ForEach-Object { Log $_ }
}
exit $p.ExitCode
