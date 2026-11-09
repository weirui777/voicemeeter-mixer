$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class WheelTest {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int d, IntPtr e);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

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
  public static void Wheel(int delta) {
    mouse_event(0x0800, 0, 0, delta, IntPtr.Zero);   // MOUSEEVENTF_WHEEL
  }
}
"@
[WheelTest]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\release\VMixer.exe'
$proc = Get-Process VMixer | Select-Object -First 1
$pid32 = [uint32]$proc.Id

Write-Output ("master gain before: " + (& python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain' | Out-String).Trim())

# show panel
Start-Process -FilePath $exe -ArgumentList '--show' -WindowStyle Hidden
$h = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds(8)
while ((Get-Date) -lt $deadline) {
  $h = [WheelTest]::Panel($pid32)
  if ($h -ne [IntPtr]::Zero) { break }
  Start-Sleep -Milliseconds 50
}
if ($h -eq [IntPtr]::Zero) { throw 'panel not visible' }
Write-Output ("panel hwnd=" + $h)

[WheelTest]::ForceFg($h)
Start-Sleep -Milliseconds 400

# hover over the master channel fader (layout: slider 1831,1167,93,115)
$hx = 1877
$hy = 1210
[WheelTest]::SetCursorPos($hx, $hy) | Out-Null
Start-Sleep -Milliseconds 600
Write-Output ("foreground is panel: " + ([WheelTest]::GetForegroundWindow() -eq $h))

[WheelTest]::Wheel(120)     # scroll up => +1 dB
Start-Sleep -Milliseconds 800
Write-Output ("after wheel up:   " + (& python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain' | Out-String).Trim())

[WheelTest]::Wheel(-120)    # scroll down => -1 dB
Start-Sleep -Milliseconds 800
Write-Output ("after wheel down: " + (& python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain' | Out-String).Trim())

[WheelTest]::Wheel(-120)
Start-Sleep -Milliseconds 800
Write-Output ("after wheel down2:" + (& python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain' | Out-String).Trim())
