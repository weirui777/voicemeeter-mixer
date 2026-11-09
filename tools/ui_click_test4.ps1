param([switch]$NoExternalRead)
$ErrorActionPreference = 'Stop'

Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class Cli4 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static List<IntPtr> FindWindows(uint pid) {
    List<IntPtr> list = new List<IntPtr>();
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) list.Add(h);
      return true;
    }, IntPtr.Zero);
    return list;
  }
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(200);
    mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(300);
  }
}
"@
[Cli4]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\dist\VMixer.exe'

Get-Process VMixer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Start-Process -FilePath $exe -ArgumentList '--show' -WindowStyle Hidden
Start-Sleep -Seconds 3

$proc = Get-Process VMixer | Select-Object -First 1
$wins = [Cli4]::FindWindows([uint32]$proc.Id)
$r = New-Object Cli4+RECT
[Cli4]::GetWindowRect($wins[0], [ref]$r) | Out-Null

$cx = $r.L + 90
$trackTop = $r.T + 177
$trackH = 101
function YFor([double]$db) {
    $p = (12.0 - $db) / 72.0
    return [int]($trackTop + $p * $trackH)
}

Write-Output '1) click to -33 (no foreign client yet)'
[Cli4]::Click($cx, (YFor -33))
Start-Sleep -Seconds 2
Write-Output '   external read:'
python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain'

Write-Output '2) foreign client login+read+logout'
python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain'
Start-Sleep -Seconds 1

Write-Output '3) click to -44 (after foreign client)'
[Cli4]::Click($cx, (YFor -44))
Start-Sleep -Seconds 3
Write-Output '   external read (truth):'
python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain'

Write-Output '4) restore to -9.8'
[Cli4]::Click($cx, (YFor -9.8))
Start-Sleep -Seconds 3
Write-Output '   external read (truth):'
python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain'
