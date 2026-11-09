$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class Cli5 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int max);
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
  public static string Title(IntPtr h) {
    System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
    GetWindowText(h, sb, 256);
    return sb.ToString();
  }
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(200);
    mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(80);
    mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(600);
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
[Cli5]::SetProcessDPIAware() | Out-Null

$proc = Get-Process VMixer | Select-Object -First 1
$wins = [Cli5]::FindWindows([uint32]$proc.Id)
foreach ($w in $wins) {
  $r = New-Object Cli5+RECT
  [Cli5]::GetWindowRect($w, [ref]$r) | Out-Null
  Write-Output ("hwnd={0} title='{1}' rect={2},{3} {4}x{5}" -f $w, [Cli5]::Title($w), $r.L, $r.T, ($r.R-$r.L), ($r.B-$r.T))
}

# click the settings button (top-right of the card)
[Cli5]::Click(2482, 1049)
Start-Sleep -Seconds 1

$wins2 = [Cli5]::FindWindows([uint32]$proc.Id)
foreach ($w in $wins2) {
  $r = New-Object Cli5+RECT
  [Cli5]::GetWindowRect($w, [ref]$r) | Out-Null
  Write-Output ("after click: hwnd={0} title='{1}' rect={2},{3} {4}x{5}" -f $w, [Cli5]::Title($w), $r.L, $r.T, ($r.R-$r.L), ($r.B-$r.T))
  if ([Cli5]::Title($w) -ne '') {
    [Cli5]::Shot($r.L - 5, $r.T - 5, ($r.R-$r.L) + 10, ($r.B-$r.T) + 10, 'F:\miniMAxWork\VMixer\dist\settings_new.png')
    Write-Output '  saved settings_new.png'
  }
}
