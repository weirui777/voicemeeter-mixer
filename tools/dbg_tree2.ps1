$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class Dbg {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }

  static StringBuilder sb = new StringBuilder();
  static uint targetPid = 0;
  public static string Dump(uint pid) {
    sb.Length = 0;
    targetPid = pid;
    EnumWindows(new EnumProc(TopProc), IntPtr.Zero);
    return sb.ToString(); }

  static bool TopProc(IntPtr h, IntPtr l) {
    uint p; GetWindowThreadProcessId(h, out p);
    if (p == targetPid) {
      RECT r; GetWindowRect(h, out r);
      StringBuilder cn = new StringBuilder(256); GetClassNameW(h, cn, 256);
      StringBuilder tt = new StringBuilder(256); GetWindowTextW(h, tt, 256);
      sb.AppendLine("TOP " + h + " cls=" + cn + " txt=" + tt + " vis=" + IsWindowVisible(h) + " rect=" + r.L + "," + r.T + "," + r.R + "," + r.B);
      EnumChildWindows(h, new EnumProc(ChildProc), IntPtr.Zero);
    }
    return true; }

  static bool ChildProc(IntPtr c, IntPtr l) {
    StringBuilder c2 = new StringBuilder(256); GetClassNameW(c, c2, 256);
    StringBuilder t2 = new StringBuilder(256); GetWindowTextW(c, t2, 256);
    RECT r2; GetWindowRect(c, out r2);
    sb.AppendLine("   CHILD " + c + " cls=" + c2 + " txt=" + t2 + " rect=" + r2.L + "," + r2.T + "," + r2.R + "," + r2.B);
    return true; }
}
"@
$proc = Start-Process -FilePath 'F:\miniMAxWork\VMixer\release\VMixer.exe' -ArgumentList '--settings' -PassThru
Start-Sleep -Seconds 4
[Dbg]::Dump([uint32]$proc.Id)
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
