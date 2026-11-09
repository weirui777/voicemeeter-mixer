$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class WheelTest3 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("user32.dll", EntryPoint="PostMessage")] public static extern bool PostMsg(IntPtr h, int msg, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }

  public static IntPtr Panel(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        if (r.R - r.L > 300 && r.B - r.T > 200) { found = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }
  public static void ForceFg(IntPtr h) {
    IntPtr fg = GetForegroundWindow();
    uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
    uint my = GetCurrentThreadId();
    bool att = false;
    if (fgThread != 0 && fgThread != my) att = AttachThreadInput(my, fgThread, true);
    SetForegroundWindow(h);
    if (att) AttachThreadInput(my, fgThread, false);
  }
  public static void PostWheel(IntPtr h, int delta, int x, int y) {
    int lp = (y << 16) | (x & 0xFFFF);
    int wp = (delta & 0xFFFF) << 16;
    PostMsg(h, 0x020A, (IntPtr)wp, (IntPtr)lp);
  }
  public static void Wheel(int delta) {
    mouse_event(0x0800, 0, 0, delta, IntPtr.Zero);
  }
  public static string CurPos() { POINT p; GetCursorPos(out p); return p.X + "," + p.Y; }
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int d, IntPtr e);
}
"@
[WheelTest3]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\release\VMixer.exe'
$proc = Get-Process VMixer | Select-Object -First 1
$pid32 = [uint32]$proc.Id

Write-Output ("master gain before: " + (& python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain' | Out-String).Trim())

Start-Process -FilePath $exe -ArgumentList '--show' -WindowStyle Hidden
$h = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds(8)
while ((Get-Date) -lt $deadline) {
  $h = [WheelTest3]::Panel($pid32)
  if ($h -ne [IntPtr]::Zero) { break }
  Start-Sleep -Milliseconds 50
}
if ($h -eq [IntPtr]::Zero) { throw 'panel not visible' }
Write-Output ("panel hwnd=" + $h)

[WheelTest3]::ForceFg($h)
Start-Sleep -Milliseconds 300

$hx = 1877
$hy = 1210
[WheelTest3]::SetCursorPos($hx, $hy) | Out-Null
Start-Sleep -Milliseconds 400
Write-Output ("cursor now = " + [WheelTest3]::CurPos())

[WheelTest3]::PostWheel($h, 120, $hx, $hy)
Start-Sleep -Milliseconds 900
Write-Output ("after post wheel up:   " + (& python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain' | Out-String).Trim())

[WheelTest3]::PostWheel($h, -120, $hx, $hy)
Start-Sleep -Milliseconds 900
Write-Output ("after post wheel down: " + (& python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain' | Out-String).Trim())

[WheelTest3]::PostWheel($h, -120, $hx, $hy)
Start-Sleep -Milliseconds 900
Write-Output ("after post wheel down2: " + (& python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain' | Out-String).Trim())
