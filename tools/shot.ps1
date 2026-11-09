$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
"@

$proc = Get-Process voicemeeterpro_x64 -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { $proc = Get-Process | Where-Object { $_.ProcessName -like 'voicemeeter*' } | Select-Object -First 1 }
if (-not $proc) { Write-Output 'NO VM PROCESS'; exit 1 }
$h = $proc.MainWindowHandle
Write-Output "pid=$($proc.Id) hwnd=$h title=$($proc.MainWindowTitle)"

[Win]::ShowWindow($h, 9) | Out-Null   # SW_RESTORE
[Win]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 700

$r = New-Object Win+RECT
[Win]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.R - $r.L; $ht = $r.B - $r.T
Write-Output "window rect: $($r.L),$($r.T) ${w}x${ht}"
if ($w -le 0 -or $ht -le 0) { Write-Output 'bad rect'; exit 1 }

$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
$out = 'F:\miniMAxWork\VMixer\tools\vm_window.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "saved $out"

$sb = New-Object System.Drawing.Bitmap ([System.Windows.Forms.SystemInformation]::VirtualScreen.Width), ([System.Windows.Forms.SystemInformation]::VirtualScreen.Height)
$g2 = [System.Drawing.Graphics]::FromImage($sb)
$g2.CopyFromScreen([System.Windows.Forms.SystemInformation]::VirtualScreen.Left, [System.Windows.Forms.SystemInformation]::VirtualScreen.Top, 0, 0, $sb.Size)
$out2 = 'F:\miniMAxWork\VMixer\tools\desktop.png'
$sb.Save($out2, [System.Drawing.Imaging.ImageFormat]::Png)
$g2.Dispose(); $sb.Dispose()
Write-Output "saved $out2"
