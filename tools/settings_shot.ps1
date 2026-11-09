$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
public class DlgShot {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }
  public static bool TryFind(uint pid, out RECT rect) {
    RECT f = new RECT(); bool ok = false;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        if (r.R - r.L > 200 && r.B - r.T > 150) { f = r; ok = true; return false; } }
      return true; }, IntPtr.Zero);
    rect = f; return ok; }
  public static IntPtr FindHwnd(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        if (r.R - r.L > 200 && r.B - r.T > 150) { found = h; return false; } }
      return true; }, IntPtr.Zero);
    return found; }
  public static void Shot(int x, int y, int w, int h, string path) {
    using (Bitmap bmp = new Bitmap(w, h))
    using (Graphics g = Graphics.FromImage(bmp)) {
      g.CopyFromScreen(x, y, 0, 0, bmp.Size);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png); } }
}
"@
[DlgShot]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\release\VMixer.exe'
$proc = Start-Process -FilePath $exe -ArgumentList '--settings' -PassThru
$deadline = (Get-Date).AddSeconds(12)
$r = New-Object DlgShot+RECT
while ((Get-Date) -lt $deadline) {
  if ([DlgShot]::TryFind([uint32]$proc.Id, [ref]$r)) { break }
  Start-Sleep -Milliseconds 200
}
if ($r.R - $r.L -le 0) { Write-Output 'dialog not found'; exit }

# 把窗口挪到主屏的固定位置，避免落在别的显示器上截不到
$hwnd = [DlgShot]::FindHwnd([uint32]$proc.Id)
[DlgShot]::SetWindowPos($hwnd, [IntPtr]::Zero, 420, 140, 843, 647, 0x0040) | Out-Null
Start-Sleep -Milliseconds 800
$r = New-Object DlgShot+RECT
[DlgShot]::TryFind([uint32]$proc.Id, [ref]$r) | Out-Null

Write-Output ("dialog: " + $r.L + "," + $r.T + "," + ($r.R-$r.L) + "x" + ($r.B-$r.T))
[DlgShot]::Shot($r.L, $r.T, ($r.R-$r.L), ($r.B-$r.T), 'F:\miniMAxWork\VMixer\dist\settings_new.png')

# maximize to check the responsive layout
[DlgShot]::ShowWindow($hwnd, 3) | Out-Null   # SW_MAXIMIZE
Start-Sleep -Milliseconds 900
$r2 = New-Object DlgShot+RECT
[DlgShot]::TryFind([uint32]$proc.Id, [ref]$r2) | Out-Null
Write-Output ("maximized: " + $r2.L + "," + $r2.T + "," + ($r2.R-$r2.L) + "x" + ($r2.B-$r2.T))
[DlgShot]::Shot($r2.L, $r2.T, ($r2.R-$r2.L), ($r2.B-$r2.T), 'F:\miniMAxWork\VMixer\dist\settings_max.png')

# restore, then resize by dragging the bottom-right corner
[DlgShot]::ShowWindow($hwnd, 9) | Out-Null   # SW_RESTORE
Start-Sleep -Milliseconds 700
[DlgShot]::Shot($r.L, $r.T, ($r.R-$r.L), ($r.B-$r.T), 'F:\miniMAxWork\VMixer\dist\settings_new.png')

Stop-Process -Id $proc.Id -Force
Write-Output 'done'
