$ErrorActionPreference = 'Stop'

Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class Clicker {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e);
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

  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(150);
    mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(60);
    mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(250);
  }
}
"@
[Clicker]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\dist\VMixer.exe'

# 1. 读取布局
$layoutFile = Join-Path $env:APPDATA 'VMixer\layout.txt'
if (Test-Path $layoutFile) { Remove-Item -LiteralPath $layoutFile -Force }
& $exe --layout | Out-Null
Start-Sleep -Milliseconds 800
if (-not (Test-Path $layoutFile)) { throw 'layout dump failed' }
$layout = Get-Content -LiteralPath $layoutFile -Encoding UTF8
$rows = @{}
foreach ($line in $layout) {
    $p = $line -split '\|'
    if ($p.Count -ge 5 -and $p[0] -ne 'window') { $rows[[int]$p[0]] = $p }
}
Write-Output ('channels: ' + $rows.Count)

# 2. 启动面板
Get-Process VMixer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Start-Process -FilePath $exe -ArgumentList '--show' -WindowStyle Hidden
Start-Sleep -Seconds 3

$proc = Get-Process VMixer | Select-Object -First 1
$wins = [Clicker]::FindWindows([uint32]$proc.Id)
if ($wins.Count -lt 1) { throw 'panel window not found' }
$best = $wins[0]
$r = New-Object Clicker+RECT
[Clicker]::GetWindowRect($best, [ref]$r) | Out-Null
Write-Output ("panel rect: {0},{1} {2}x{3}" -f $r.L, $r.T, ($r.R-$r.L), ($r.B-$r.T))

$row = $rows[0]
$s = $row[2] -split ','          # slider x,y,w,h
$t = $row[3] -split ','          # tile   x,y,w,h
$trackTop = [int]$s[1] + 7
$trackH = [int]$s[3] - 14
$cx = [int]$s[0] + [int]$s[2] / 2

function YFor([double]$db) {
    $p = (12.0 - $db) / 72.0
    return [int]($trackTop + $p * $trackH)
}

Write-Output ("slider x=$cx trackTop=$trackTop trackH=$trackH")

# 3. 点到 -30 dB
$y30 = YFor -30
Write-Output "click at ($cx, $y30) expecting -30 dB"
[Clicker]::Click($cx, $y30)
Start-Sleep -Milliseconds 700
Write-Output '--- vm state after click ---'
python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain'

# 4. 点回原值 -9.8
$y98 = YFor -9.8
Write-Output "click at ($cx, $y98) expecting -9.8 dB"
[Clicker]::Click($cx, $y98)
Start-Sleep -Milliseconds 700
Write-Output '--- vm state after restore click ---'
python F:\miniMAxWork\VMixer\tools\read_one.py 'Bus[0].Gain'

# 5. 静音开关测试（点击硬件输入 1 的图标磁贴）
$t1 = $rows[1][3] -split ','
$tx = [int]$t1[0] + [int]$t1[2] / 2
$ty = [int]$t1[1] + [int]$t1[3] / 2
Write-Output "click tile of channel 1 at ($tx, $ty) expecting mute=1"
[Clicker]::Click($tx, $ty)
Start-Sleep -Milliseconds 700
python F:\miniMAxWork\VMixer\tools\read_one.py 'Strip[0].Mute'
Write-Output 'click again -> expect mute=0'
[Clicker]::Click($tx, $ty)
Start-Sleep -Milliseconds 700
python F:\miniMAxWork\VMixer\tools\read_one.py 'Strip[0].Mute'

Write-Output '--- final full state ---'
python F:\miniMAxWork\VMixer\tools\dump_all.py
