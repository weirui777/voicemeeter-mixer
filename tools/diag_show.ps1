$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
public class Diag1 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public static List<IntPtr> All(uint pid) {
    List<IntPtr> list = new List<IntPtr>();
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid) list.Add(h);
      return true;
    }, IntPtr.Zero);
    return list;
  }
  public static string Info(uint pid) {
    System.Text.StringBuilder sb = new System.Text.StringBuilder();
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid) {
        RECT r; GetWindowRect(h, out r);
        sb.AppendLine(string.Format("hwnd={0} vis={1} rect={2},{3},{4}x{5}", h, IsWindowVisible(h), r.L, r.T, r.R-r.L, r.B-r.T));
      }
      return true;
    }, IntPtr.Zero);
    sb.AppendLine("foreground=" + GetForegroundWindow());
    return sb.ToString();
  }
}
"@
[Diag1]::SetProcessDPIAware() | Out-Null
$proc = Get-Process VMixer | Select-Object -First 1
Write-Output "pid=$($proc.Id)"
Write-Output '--- before trigger ---'
Write-Output ([Diag1]::Info([uint32]$proc.Id))
Start-Process -FilePath 'F:\miniMAxWork\VMixer\release\VMixer.exe' -ArgumentList '--show' -WindowStyle Hidden
Start-Sleep -Milliseconds 800
Write-Output '--- after trigger (0.8s) ---'
Write-Output ([Diag1]::Info([uint32]$proc.Id))
Start-Sleep -Seconds 2
Write-Output '--- after 2.8s ---'
Write-Output ([Diag1]::Info([uint32]$proc.Id))
