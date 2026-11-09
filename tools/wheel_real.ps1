$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class WheelTest4 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int d, IntPtr e);
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
  public static void Wheel(int delta) { mouse_event(0x0800, 0, 0, delta, IntPtr.Zero); }
  public static string CurPos() { POINT p; GetCursorPos(out p); return p.X + "," + p.Y; }
  public static string FgTitle() {
    IntPtr fg = GetForegroundWindow();
    return fg.ToInt64().ToString();
  }
}
"@
[WheelTest4]::SetProcessDPIAware() | Out-Null

$log = 'F:\miniMAxWork\VMixer\dist\wheel_real.log'
"start" | Out-File $log -Encoding utf8

$exe = 'F:\miniMAxWork\VMixer\release\VMixer.exe'
$proc = Get-Process VMixer | Select-Object -First 1
$pid32 = [uint32]$proc.Id

Start-Process -FilePath $exe -ArgumentList '--show' -WindowStyle Hidden
$h = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds(8)
while ((Get-Date) -lt $deadline) {
  $h = [WheelTest4]::Panel($pid32)
  if ($h -ne [IntPtr]::Zero) { break }
  Start-Sleep -Milliseconds 50
}
if ($h -eq [IntPtr]::Zero) { "panel not visible" | Out-File $log -Append -Encoding utf8; exit 1 }
("panel hwnd=" + $h) | Out-File $log -Append -Encoding utf8

$hx = 1877
$hy = 1210
[WheelTest4]::SetCursorPos($hx, $hy) | Out-Null
Start-Sleep -Milliseconds 500
("cursor=" + [WheelTest4]::CurPos() + " foreground=" + [WheelTest4]::FgTitle()) | Out-File $log -Append -Encoding utf8

# real wheel event (goes to the focused window, exactly like a user scrolling)
[WheelTest4]::Wheel(120)
Start-Sleep -Milliseconds 700
("after real wheel up: " + (& python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain' | Out-String).Trim()) | Out-File $log -Append -Encoding utf8

[WheelTest4]::Wheel(-120)
Start-Sleep -Milliseconds 700
("after real wheel down: " + (& python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain' | Out-String).Trim()) | Out-File $log -Append -Encoding utf8

("done foreground=" + [WheelTest4]::FgTitle()) | Out-File $log -Append -Encoding utf8
