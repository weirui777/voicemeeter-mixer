$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
public class ShotVert {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
  public static void Esc() { keybd_event(0x1B, 0, 0, IntPtr.Zero); keybd_event(0x1B, 0, 2, IntPtr.Zero); }
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }

  public static bool TryFind(uint pid, out RECT rect) {
    RECT found = new RECT(); bool ok = false;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        if (r.R - r.L > 300 && r.B - r.T > 200) { found = r; ok = true; return false; }
      }
      return true;
    }, IntPtr.Zero);
    rect = found; return ok;
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
[ShotVert]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\release\VMixer.exe'
$proc = Get-Process VMixer | Select-Object -First 1
$pid32 = [uint32]$proc.Id

function Show-Panel {
  Start-Process -FilePath $exe -ArgumentList '--show' -WindowStyle Hidden
  $deadline = (Get-Date).AddSeconds(8)
  while ((Get-Date) -lt $deadline) {
    $r = New-Object ShotVert+RECT
    if ([ShotVert]::TryFind($pid32, [ref]$r)) { return $r }
    Start-Sleep -Milliseconds 40
  }
  throw 'panel not visible'
}

[ShotVert]::Esc() | Out-Null
Start-Sleep -Seconds 3
[ShotVert]::SetCursorPos(600, 400) | Out-Null
Start-Sleep -Milliseconds 600
$r = Show-Panel
Start-Sleep -Milliseconds 400
$pad = 26
[ShotVert]::Shot($r.L - $pad, $r.T - $pad, ($r.R - $r.L) + 2 * $pad, ($r.B - $r.T) + 2 * $pad, 'F:\miniMAxWork\VMixer\release\panel_real.png')
Write-Output ("normal shot: " + $r.L + "," + $r.T + "," + ($r.R - $r.L) + "x" + ($r.B - $r.T))

# hover over the 3rd channel tile (硬件输入 2) to show the hover row + status bar detail
Start-Sleep -Milliseconds 900
[ShotVert]::Esc() | Out-Null
Start-Sleep -Milliseconds 300
$r2 = Show-Panel
$hx = $r2.L + 240
$hy = $r2.T + 320
[ShotVert]::SetCursorPos($hx - 40, $hy - 40) | Out-Null
Start-Sleep -Milliseconds 200
[ShotVert]::SetCursorPos($hx, $hy) | Out-Null
Start-Sleep -Milliseconds 900
[ShotVert]::Shot($r2.L, $r2.T, ($r2.R - $r2.L), ($r2.B - $r2.T), 'F:\miniMAxWork\VMixer\dist\panel_hover.png')
Write-Output ("hover shot: cursor=" + $hx + "," + $hy)
