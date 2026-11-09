$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class WinCap3 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int max);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

  public static List<IntPtr> FindWindows(uint targetPid) {
    List<IntPtr> list = new List<IntPtr>();
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == targetPid && IsWindowVisible(h)) list.Add(h);
      return true;
    }, IntPtr.Zero);
    return list;
  }
  public static void Capture(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r);
    int w = r.R - r.L, ht = r.B - r.T;
    if (w <= 0 || ht <= 0) throw new Exception("bad rect " + w + "x" + ht);
    using (Bitmap bmp = new Bitmap(w, ht))
    using (Graphics g = Graphics.FromImage(bmp)) {
      IntPtr hdc = g.GetHdc();
      PrintWindow(h, hdc, 2);
      g.ReleaseHdc(hdc);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
  }
  public static void CaptureRegion(int x, int y, int w, int ht, string path) {
    using (Bitmap bmp = new Bitmap(w, ht))
    using (Graphics g = Graphics.FromImage(bmp)) {
      g.CopyFromScreen(x, y, 0, 0, bmp.Size);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
  }
}
"@
[WinCap3]::SetProcessDPIAware() | Out-Null

$proc = Get-Process VMixer -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output 'VMixer not running'; exit 1 }
$wins = [WinCap3]::FindWindows([uint32]$proc.Id)
Write-Output "windows found: $($wins.Count)"
$best = [IntPtr]::Zero
$bestArea = 0
$bestRect = $null
foreach ($w in $wins) {
  $r = New-Object WinCap3+RECT
  [WinCap3]::GetWindowRect($w, [ref]$r) | Out-Null
  $area = ($r.R - $r.L) * ($r.B - $r.T)
  Write-Output ("  hwnd={0} rect={1},{2} {3}x{4}" -f $w, $r.L, $r.T, ($r.R - $r.L), ($r.B - $r.T))
  if ($area -gt $bestArea) { $bestArea = $area; $best = $w; $bestRect = $r }
}
if ($best -eq [IntPtr]::Zero) { Write-Output 'no window'; exit 1 }

$out = 'F:\miniMAxWork\VMixer\dist\panel.png'
[WinCap3]::Capture($best, $out)
Write-Output "saved $out"

# 同时抓一张屏幕区域图（面板是置顶的）
$pad = 20
[WinCap3]::CaptureRegion($bestRect.L - $pad, $bestRect.T - $pad,
    ($bestRect.R - $bestRect.L) + 2 * $pad, ($bestRect.B - $bestRect.T) + 2 * $pad,
    'F:\miniMAxWork\VMixer\dist\panel_screen.png')
Write-Output 'saved panel_screen.png'
