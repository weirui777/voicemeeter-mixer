$ErrorActionPreference = 'Stop'
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Mouse {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(250);
    mouse_event(LEFTDOWN, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(120);
    mouse_event(LEFTUP, 0, 0, 0, IntPtr.Zero);
  }
}
"@
try { [Mouse]::SetProcessDPIAware() | Out-Null } catch { }
$x = [int]$args[0]; $y = [int]$args[1]
[Mouse]::Click($x, $y)
Write-Output "clicked $x,$y"
