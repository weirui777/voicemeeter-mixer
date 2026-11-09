$ErrorActionPreference = 'Stop'
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class WinTree {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int max);
  [DllImport("user32.dll")] public static extern int GetClassName(IntPtr h, StringBuilder s, int max);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

  public static void Dump(uint targetPid) {
    List<IntPtr> tops = new List<IntPtr>();
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == targetPid && IsWindowVisible(h)) tops.Add(h);
      return true;
    }, IntPtr.Zero);
    foreach (IntPtr top in tops) {
      RECT tr; GetWindowRect(top, out tr);
      StringBuilder tt = new StringBuilder(256); GetWindowText(top, tt, 256);
      Console.WriteLine("TOP hwnd={0} class={1} text='{2}' rect={3},{4} {5}x{6}", top, Class(top), tt, tr.L, tr.T, tr.R - tr.L, tr.B - tr.T);
      EnumChildWindows(top, delegate(IntPtr h, IntPtr l) {
        RECT r; GetWindowRect(h, out r);
        StringBuilder t = new StringBuilder(256); GetWindowText(h, t, 256);
        Console.WriteLine("   child hwnd={0} class={1} text='{2}' visible={3} rect={4},{5} {6}x{7}", h, Class(h), t, IsWindowVisible(h), r.L, r.T, r.R - r.L, r.B - r.T);
        return true;
      }, IntPtr.Zero);
    }
  }
  static string Class(IntPtr h) {
    StringBuilder s = new StringBuilder(128); GetClassName(h, s, 128); return s.ToString();
  }
}
"@
$proc = Get-Process VMixer -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output 'VMixer not running'; exit 1 }
[WinTree]::Dump([uint32]$proc.Id)
