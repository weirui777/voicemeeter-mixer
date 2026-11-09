$ErrorActionPreference = 'Stop'
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class DpiHelper {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
}
"@
try { [DpiHelper]::SetProcessDpiAwarenessContext([IntPtr](-4)) } catch { }
try { [DpiHelper]::SetProcessDPIAware() | Out-Null } catch { }

Add-Type -AssemblyName System.Drawing
$rect = $args[0] -split ',' | ForEach-Object { [int]$_ }
$x = $rect[0]; $y = $rect[1]; $w = $rect[2]; $h = $rect[3]
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($x, $y, 0, 0, $bmp.Size)
$out = $args[1]
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "saved $out ($w x $h at $x,$y)"
