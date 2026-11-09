$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class Shot10 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
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
  public static void Shot(int x, int y, int w, int h, string path) {
    using (Bitmap bmp = new Bitmap(w, h))
    using (Graphics g = Graphics.FromImage(bmp)) {
      g.CopyFromScreen(x, y, 0, 0, bmp.Size);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
  }
}
"@
[Shot10]::SetProcessDPIAware() | Out-Null

# ask the already running instance to show its panel (no kill => no ghost tray icons)
Start-Process -FilePath 'F:\miniMAxWork\VMixer\release\VMixer.exe' -ArgumentList '--show' -WindowStyle Hidden
Start-Sleep -Seconds 3

$proc = Get-Process VMixer | Select-Object -First 1
$wins = [Shot10]::FindWindows([uint32]$proc.Id)
if ($wins.Count -eq 0) { throw 'no visible window for VMixer' }
$r = New-Object Shot10+RECT
[Shot10]::GetWindowRect($wins[0], [ref]$r) | Out-Null

# park the pointer away from the panel so no hover highlight is captured
[Shot10]::SetCursorPos(400, 300) | Out-Null
Start-Sleep -Milliseconds 700

$pad = 26
[Shot10]::Shot($r.L - $pad, $r.T - $pad, ($r.R - $r.L) + 2 * $pad, ($r.B - $r.T) + 2 * $pad, 'F:\miniMAxWork\VMixer\release\panel_real.png')
Write-Output ("saved release\panel_real.png  rect={0},{1},{2}x{3}" -f $r.L, $r.T, ($r.R - $r.L), ($r.B - $r.T))
