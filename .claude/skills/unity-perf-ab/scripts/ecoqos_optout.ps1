# Opt the Unity Editor and every process it starts out of Windows power throttling (EcoQoS), every 2 s.
# Per-process and reversible (ends with each process); the same switch as Task Manager's "Efficiency mode".
param([int]$EditorPid, [string]$Log, [int]$Seconds = 1800)
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class PT {
  [StructLayout(LayoutKind.Sequential)] public struct S { public uint V; public uint C; public uint M; }
  [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint a, bool i, int p);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool SetProcessInformation(IntPtr h, int c, ref S s, uint n);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
  public static bool FullSpeed(int pid) { IntPtr h = OpenProcess(0x0200, false, pid); if (h == IntPtr.Zero) return false;
    var s = new S { V = 1, C = 1, M = 0 }; bool ok = SetProcessInformation(h, 4, ref s, 12); CloseHandle(h); return ok; }
}
'@
$done = @{}
$end = (Get-Date).AddSeconds($Seconds)
while ((Get-Date) -lt $end) {
  $all = Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, Name
  $tree = @{ $EditorPid = $true }; $grew = $true
  while ($grew) { $grew = $false; foreach ($p in $all) { if ($tree.ContainsKey([int]$p.ParentProcessId) -and -not $tree.ContainsKey([int]$p.ProcessId)) { $tree[[int]$p.ProcessId] = $true; $grew = $true } } }
  foreach ($p in $all) {
    $id = [int]$p.ProcessId
    if ($tree.ContainsKey($id) -and -not $done.ContainsKey($id)) {
      $ok = [PT]::FullSpeed($id); $done[$id] = $true
      "{0:HH:mm:ss} {1} {2} {3}" -f (Get-Date), $id, $p.Name, $(if ($ok) { 'full-speed' } else { 'failed' }) | Out-File -FilePath $Log -Append -Encoding utf8
    }
  }
  Start-Sleep -Seconds 2
}
