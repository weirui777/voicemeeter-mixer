$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
public class TrayShot2 {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
  public static void Shot(int x, int y, int w, int h, string path) {
    using (Bitmap bmp = new Bitmap(w, h))
    using (Graphics g = Graphics.FromImage(bmp)) {
      g.CopyFromScreen(x, y, 0, 0, bmp.Size);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
  }
  public static void Zoom(string src, string dst, int x, int y, int w, int h, int k) {
    using (Bitmap bmp = new Bitmap(src))
    using (Bitmap z = new Bitmap(w * k, h * k))
    using (Graphics g = Graphics.FromImage(z)) {
      g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
      g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
      g.DrawImage(bmp, new Rectangle(0, 0, w * k, h * k), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
      z.Save(dst, System.Drawing.Imaging.ImageFormat.Png);
    }
  }
}
"@
[TrayShot2]::SetProcessDPIAware() | Out-Null

$sw = [int][TrayShot2]::GetSystemMetrics(0)
$sh = [int][TrayShot2]::GetSystemMetrics(1)
Write-Output ("screen: " + $sw + " x " + $sh)

# hover over the notification area so any stale (ghost) tray icons get refreshed away
[TrayShot2]::SetCursorPos($sw - 120, $sh - 30) | Out-Null
Start-Sleep -Seconds 2
[TrayShot2]::SetCursorPos($sw - 40, $sh - 20) | Out-Null
Start-Sleep -Seconds 2

$w = 300; $h = 56
[TrayShot2]::Shot($sw - $w, $sh - $h, $w, $h, 'F:\miniMAxWork\VMixer\dist\tray_final.png')
Write-Output 'saved tray_final.png'

$proc = Get-Process VMixer -ErrorAction SilentlyContinue
Write-Output ("VMixer processes: " + ($proc | Measure-Object).Count)
