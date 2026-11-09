$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
public class SortTest {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int d, IntPtr e);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, System.Text.StringBuilder sb, int max);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }
  // find a child button whose text matches, return its screen rect center
  public static bool FindButton(IntPtr parent, string text, out int cx, out int cy) {
    IntPtr target = IntPtr.Zero;
    EnumChildWindows(parent, delegate(IntPtr h, IntPtr l) {
      System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
      GetWindowTextW(h, sb, 256);
      if (sb.ToString() == text) { target = h; return false; }
      return true; }, IntPtr.Zero);
    if (target == IntPtr.Zero) { cx = 0; cy = 0; return false; }
    RECT r; GetWindowRect(target, out r);
    cx = (r.L + r.R) / 2; cy = (r.T + r.B) / 2;
    return true; }
  public static void LeftDown() { mouse_event(0x0002, 0, 0, 0, IntPtr.Zero); }
  public static void LeftUp()   { mouse_event(0x0004, 0, 0, 0, IntPtr.Zero); }
  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(200); LeftDown(); System.Threading.Thread.Sleep(60); LeftUp(); }
  public static IntPtr Find(uint pid, int minW, int minH) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        if (r.R - r.L >= minW && r.B - r.T >= minH) { found = h; return false; } }
      return true; }, IntPtr.Zero);
    return found; }
  public static bool Capture(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r);
    int w = r.R - r.L, ht = r.B - r.T;
    using (Bitmap bmp = new Bitmap(w, ht))
    using (Graphics g = Graphics.FromImage(bmp)) {
      IntPtr hdc = g.GetHdc();
      bool ok = PrintWindow(h, hdc, 2);
      g.ReleaseHdc(hdc);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
      return ok; } }
}
"@
[SortTest]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\release\VMixer.exe'
$proc = Start-Process -FilePath $exe -ArgumentList '--settings' -PassThru
$deadline = (Get-Date).AddSeconds(15)
$hwnd = [IntPtr]::Zero
while ((Get-Date) -lt $deadline) {
  $hwnd = [SortTest]::Find([uint32]$proc.Id, 400, 300)
  if ($hwnd -ne [IntPtr]::Zero) { break }
  Start-Sleep -Milliseconds 250
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Output 'dialog not found'; Stop-Process -Id $proc.Id -Force; exit }

Start-Sleep -Milliseconds 1200
$r = New-Object SortTest+RECT
[SortTest]::GetWindowRect($hwnd, [ref]$r) | Out-Null
Write-Output ("window at " + $r.L + "," + $r.T + " " + ($r.R-$r.L) + "x" + ($r.B-$r.T))

# click the "当前音量" column header (relative ~x=640, y=63 in a 825-wide window)
$hx = $r.L + 762
$hy = $r.T + 63
[SortTest]::Click($hx, $hy)
Start-Sleep -Milliseconds 900
[SortTest]::Capture($hwnd, 'F:\miniMAxWork\VMixer\dist\sort_asc.png') | Out-Null
Write-Output 'sorted asc captured'

# click the OK button (relative ~x=745, y=617)
$okx = 0; $oky = 0
if ([SortTest]::FindButton($hwnd, '确定', [ref]$okx, [ref]$oky)) {
  Write-Output ("OK button at " + $okx + "," + $oky)
  [SortTest]::Click($okx, $oky)
} else {
  Write-Output 'OK button not found'
}
Start-Sleep -Milliseconds 1500
$still = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
if ($still) { Write-Output 'dialog still open (OK button missed?)' } else { Write-Output 'dialog closed, OK clicked' }
[SortTest]::Capture($hwnd, 'F:\miniMAxWork\VMixer\dist\sort_after_ok.png') | Out-Null

Start-Sleep -Milliseconds 500
Get-Content "$env:APPDATA\VMixer\config.ini" | Select-String -Pattern 'Order'
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Output 'done'
