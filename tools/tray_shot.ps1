$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class TrayShot {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
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
[TrayShot]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\release\VMixer.exe'
Get-Process VMixer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700
Start-Process -FilePath $exe -WindowStyle Hidden
Start-Sleep -Seconds 4

# 托盘区域（屏幕右下角）
$sw = [int][TrayShot]::GetSystemMetrics(0)
$sh = [int][TrayShot]::GetSystemMetrics(1)
Write-Output ("screen: " + $sw + " x " + $sh)
$w = 520; $h = 64
[TrayShot]::Shot($sw - $w, $sh - $h, $w, $h, 'F:\miniMAxWork\VMixer\dist\tray_new.png')
Write-Output 'saved tray_new.png'

# 面板
$proc = Get-Process VMixer | Select-Object -First 1
$wins = [TrayShot]::FindWindows([uint32]$proc.Id)
Write-Output ("visible windows: " + $wins.Count)
