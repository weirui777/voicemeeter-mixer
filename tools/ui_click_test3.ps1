param([switch]$NoExternalRead)
$ErrorActionPreference = 'Stop'

Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class Cli3 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e);
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
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(200);
    mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(300);
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
[Cli3]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\dist\VMixer.exe'
$outDir = 'F:\miniMAxWork\VMixer\dist'

Get-Process VMixer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Start-Process -FilePath $exe -ArgumentList '--show' -WindowStyle Hidden
Start-Sleep -Seconds 3

$proc = Get-Process VMixer | Select-Object -First 1
$wins = [Cli3]::FindWindows([uint32]$proc.Id)
$h = $wins[0]
$r = New-Object Cli3+RECT
[Cli3]::GetWindowRect($h, [ref]$r) | Out-Null
Write-Output ("panel rect {0},{1} {2}x{3}" -f $r.L, $r.T, ($r.R-$r.L), ($r.B-$r.T))

# master column: slider center x = panel left + 90, track top = panel top + 177
$cx = $r.L + 90
$trackTop = $r.T + 177
$trackH = 101

function YFor([double]$db) {
    $p = (12.0 - $db) / 72.0
    return [int]($trackTop + $p * $trackH)
}

# capture only the master dB value area
function ShotValue([string]$name) {
    [Cli3]::Shot($r.L - 10, $r.T - 10, ($r.R - $r.L) + 20, ($r.B - $r.T) + 20, (Join-Path $outDir $name))
}

Write-Output '--- step 1: click -30 (no external API access) ---'
[Cli3]::Click($cx, (YFor -30))
Start-Sleep -Seconds 2
ShotValue 'v1_after_-30.png'
Write-Output ('clicked at y=' + (YFor -30))

Write-Output '--- step 2: external reader WITH Logout (skipped when -NoExternalRead) ---'
if (-not $NoExternalRead) {
    python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain'
    Start-Sleep -Milliseconds 500
}

Write-Output '--- step 3: click -20 again ---'
[Cli3]::Click($cx, (YFor -20))
Start-Sleep -Seconds 4
ShotValue 'v2_after_-20.png'
Write-Output ('clicked at y=' + (YFor -20))

Write-Output '--- step 4: click back to -9.8 (original) ---'
[Cli3]::Click($cx, (YFor -9.8))
Start-Sleep -Seconds 4
ShotValue 'v3_back_to_-9.8.png'
Write-Output ('clicked at y=' + (YFor -9.8))

Write-Output 'done'
