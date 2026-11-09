$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class Diag {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int max);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, IntPtr e);
  public static List<IntPtr> FindWindows(uint pid) {
    List<IntPtr> list = new List<IntPtr>();
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid) list.Add(h);
      return true;
    }, IntPtr.Zero);
    return list;
  }
  public static string Title(IntPtr h) {
    System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
    GetWindowText(h, sb, 256);
    return sb.ToString();
  }
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(150);
    mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(60);
    mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(200);
  }
}
"@
[Diag]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\dist\VMixer.exe'
Get-Process VMixer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Start-Process -FilePath $exe -ArgumentList '--show' -WindowStyle Hidden
Start-Sleep -Seconds 3

$proc = Get-Process VMixer | Select-Object -First 1
$wins = [Diag]::FindWindows([uint32]$proc.Id)
foreach ($w in $wins) {
  Write-Output ("hwnd={0} visible={1} title='{2}'" -f $w, [Diag]::IsWindowVisible($w), [Diag]::Title($w))
}
$fg = [Diag]::GetForegroundWindow()
Write-Output ("foreground={0} title='{1}'" -f $fg, [Diag]::Title($fg))

Write-Output '--- click on slider (1877,1238) ---'
[Diag]::Click(1877, 1238)
$fg = [Diag]::GetForegroundWindow()
Write-Output ("after click foreground={0} title='{1}'" -f $fg, [Diag]::Title($fg))
foreach ($w in $wins) {
  Write-Output ("  hwnd={0} visible={1}" -f $w, [Diag]::IsWindowVisible($w))
}
Start-Sleep -Seconds 2
Write-Output '--- after 2s ---'
foreach ($w in $wins) {
  Write-Output ("  hwnd={0} visible={1}" -f $w, [Diag]::IsWindowVisible($w))
}
$fg = [Diag]::GetForegroundWindow()
Write-Output ("foreground={0} title='{1}'" -f $fg, [Diag]::Title($fg))
