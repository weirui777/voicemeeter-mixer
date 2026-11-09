$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class Shot9 {
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
[Shot9]::SetProcessDPIAware() | Out-Null

Get-Process VMixer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700
Start-Process -FilePath 'F:\miniMAxWork\VMixer\dist\VMixer.exe' -ArgumentList '--show' -WindowStyle Hidden
Start-Sleep -Seconds 3

$proc = Get-Process VMixer | Select-Object -First 1
$wins = [Shot9]::FindWindows([uint32]$proc.Id)
$r = New-Object Shot9+RECT
[Shot9]::GetWindowRect($wins[0], [ref]$r) | Out-Null

# park the pointer away from the panel so no hover highlight is captured
[Shot9]::SetCursorPos(400, 300) | Out-Null
Start-Sleep -Milliseconds 600

$pad = 26
[Shot9]::Shot($r.L - $pad, $r.T - $pad, ($r.R - $r.L) + 2 * $pad, ($r.B - $r.T) + 2 * $pad, 'F:\miniMAxWork\VMixer\release\panel_real.png')
Write-Output 'saved release\panel_real.png'
