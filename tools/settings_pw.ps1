$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
public class PWShot {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }

  public static IntPtr Find(uint pid, int minW, int minH) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        if (r.R - r.L >= minW && r.B - r.T >= minH) { found = h; return false; } }
      return true; }, IntPtr.Zero);
    return found; }

  // 直接把窗口内容画进位图，不受遮挡影响
  public static bool Capture(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r);
    int w = r.R - r.L, ht = r.B - r.T;
    if (w <= 0 || ht <= 0) return false;
    using (Bitmap bmp = new Bitmap(w, ht))
    using (Graphics g = Graphics.FromImage(bmp)) {
      IntPtr hdc = g.GetHdc();
      bool ok = PrintWindow(h, hdc, 2);
      g.ReleaseHdc(hdc);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
      return ok; } }
}
"@
[PWShot]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\release\VMixer.exe'
$proc = Start-Process -FilePath $exe -ArgumentList '--settings' -PassThru
$deadline = (Get-Date).AddSeconds(15)
$hwnd = [IntPtr]::Zero
while ((Get-Date) -lt $deadline) {
  $hwnd = [PWShot]::Find([uint32]$proc.Id, 400, 300)
  if ($hwnd -ne [IntPtr]::Zero) { break }
  Start-Sleep -Milliseconds 250
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Output 'dialog not found'; Stop-Process -Id $proc.Id -Force; exit }

Start-Sleep -Milliseconds 1200
[PWShot]::Capture($hwnd, 'F:\miniMAxWork\VMixer\dist\settings_new.png') | Out-Null
Write-Output 'normal captured'

[PWShot]::ShowWindow($hwnd, 3) | Out-Null     # SW_MAXIMIZE
Start-Sleep -Milliseconds 1200
[PWShot]::Capture($hwnd, 'F:\miniMAxWork\VMixer\dist\settings_max.png') | Out-Null
Write-Output 'maximized captured'

Stop-Process -Id $proc.Id -Force
Write-Output 'done'
