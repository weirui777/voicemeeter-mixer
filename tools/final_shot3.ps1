$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class Shot11 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static bool TryFind(uint pid, out RECT rect) {
    RECT found = new RECT();
    bool ok = false;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        if (r.R - r.L > 200 && r.B - r.T > 100) { found = r; ok = true; return false; }
      }
      return true;
    }, IntPtr.Zero);
    rect = found;
    return ok;
  }
  public static void Shot(int x, int y, int w, int h, string path) {
    using (Bitmap bmp = new Bitmap(w, h))
    using (Graphics g = Graphics.FromImage(bmp)) {
      g.CopyFromScreen(x, y, 0, 0, bmp.Size);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
  }
}
"@
[Shot11]::SetProcessDPIAware() | Out-Null

$proc = Get-Process VMixer | Select-Object -First 1
$pid32 = [uint32]$proc.Id

# dismiss any IME candidate window, park the pointer far from the panel
[Shot11]::keybd_event(0x1B, 0, 0, [IntPtr]::Zero) 2>$null
[Shot11]::keybd_event(0x1B, 0, 2, [IntPtr]::Zero) 2>$null
[Shot11]::SetCursorPos(200, 200) | Out-Null
Start-Sleep -Milliseconds 600

Start-Process -FilePath 'F:\miniMAxWork\VMixer\release\VMixer.exe' -ArgumentList '--show' -WindowStyle Hidden

$deadline = (Get-Date).AddSeconds(6)
$r = New-Object Shot11+RECT
$found = $false
while ((Get-Date) -lt $deadline) {
  if ([Shot11]::TryFind($pid32, [ref]$r)) { $found = $true; break }
  Start-Sleep -Milliseconds 20
}
if (-not $found) { throw 'panel window never became visible' }

$pad = 26
[Shot11]::Shot($r.L - $pad, $r.T - $pad, ($r.R - $r.L) + 2 * $pad, ($r.B - $r.T) + 2 * $pad, 'F:\miniMAxWork\VMixer\release\panel_real.png')
Write-Output ("saved release\panel_real.png  rect={0},{1},{2}x{3}" -f $r.L, $r.T, ($r.R - $r.L), ($r.B - $r.T))
