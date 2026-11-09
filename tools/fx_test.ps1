$ErrorActionPreference = 'Stop'
$out = 'F:\miniMAxWork\VMixer\dist\fx_test_result.txt'
$log = "$env:APPDATA\VMixer\vm.log"
$py = 'E:\it_soft\python3\python.exe'
$lines = New-Object System.Collections.ArrayList
function W($s) { [void]$lines.Add([string]$s) }

Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
public class FxTest2 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int d, IntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }
  public static void LeftDown() { mouse_event(0x0002, 0, 0, 0, IntPtr.Zero); }
  public static void LeftUp()   { mouse_event(0x0004, 0, 0, 0, IntPtr.Zero); }
  public static void Wheel(int d){ mouse_event(0x0800, 0, 0, d, IntPtr.Zero); }
  public static bool TryFind(uint pid, out RECT rect) {
    RECT found = new RECT(); bool ok = false;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        if (r.R - r.L > 300 && r.B - r.T > 200) { found = r; ok = true; return false; }
      }
      return true;
    }, IntPtr.Zero);
    rect = found; return ok;
  }
}
"@
[FxTest2]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\release\VMixer.exe'
$proc = Get-Process VMixer | Select-Object -First 1
$pid32 = [uint32]$proc.Id

function Show-Panel {
  Start-Process -FilePath $exe -ArgumentList '--show' -WindowStyle Hidden
  $deadline = (Get-Date).AddSeconds(8)
  while ((Get-Date) -lt $deadline) {
    $rr = New-Object FxTest2+RECT
    if ([FxTest2]::TryFind($pid32, [ref]$rr)) { return $rr }
    Start-Sleep -Milliseconds 40
  }
  throw 'panel not visible'
}

function Read-Gain([string]$name) {
  $o = & $py F:\miniMAxWork\VMixer\tools\read_one.py $name 2>&1
  $txt = ($o | Out-String)
  $m = [regex]::Match($txt, '=\s*(-?[0-9]+\.?[0-9]*)')
  if ($m.Success) { return [double]$m.Groups[1].Value }
  W ("read failed for " + $name + " raw=" + $txt.Trim())
  return $null
}

function Set-Gain([string]$name, [double]$v) {
  if ($null -eq $v) { return }
  $o = & $py F:\miniMAxWork\VMixer\tools\set_one.py $name $v 2>&1
  W ("restore: " + ($o | Out-String).Trim())
}

$before = @(Get-Content $log -ErrorAction SilentlyContinue).Count

$r = Show-Panel
Start-Sleep -Milliseconds 700
W ("window rect: " + $r.L + "," + $r.T + "," + ($r.R - $r.L) + "x" + ($r.B - $r.T))

# ---- test 1: click the AUX slider (last row) -> expect immediate TONE ----
$target = 'Strip[4]'
$v0 = Read-Gain $target
W ("baseline " + $target + " = " + $v0)

$auxY = $r.T + 646
$auxX = $r.L + 250
[FxTest2]::SetCursorPos($auxX, $auxY) | Out-Null
Start-Sleep -Milliseconds 400
[FxTest2]::LeftDown()
Start-Sleep -Milliseconds 80
[FxTest2]::LeftUp()
Start-Sleep -Milliseconds 800
$v1 = Read-Gain $target
W ("after click " + $target + " = " + $v1)

# ---- test 2: wheel over the master row -> expect TONE after the wheel stops ----
$mTarget = 'Bus[0]'
$m0 = Read-Gain $mTarget
W ("baseline " + $mTarget + " = " + $m0)
$whY = $r.T + 141
[FxTest2]::SetCursorPos($r.L + 250, $whY) | Out-Null
Start-Sleep -Milliseconds 400
[FxTest2]::Wheel(120)
Start-Sleep -Milliseconds 150
[FxTest2]::Wheel(120)
Start-Sleep -Milliseconds 1200
$m1 = Read-Gain $mTarget
W ("after wheel " + $mTarget + " = " + $m1)

# ---- restore ----
Set-Gain $target $v0
Set-Gain $mTarget $m0
Start-Sleep -Milliseconds 600
W ("restored " + $target + " = " + (Read-Gain $target))
W ("restored " + $mTarget + " = " + (Read-Gain $mTarget))

W '--- new log lines ---'
$all = @(Get-Content $log -ErrorAction SilentlyContinue)
for ($i = $before; $i -lt $all.Count; $i++) { W $all[$i] }

[System.IO.File]::WriteAllLines($out, $lines, (New-Object System.Text.UTF8Encoding($false)))
