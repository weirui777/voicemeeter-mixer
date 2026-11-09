$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class LvMsg {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }

  public static string Probe(uint pid) {
    StringBuilder sb = new StringBuilder();
    IntPtr top = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) { RECT r; GetWindowRect(h, out r);
        if (r.R - r.L >= 500 && r.B - r.T >= 400) { top = h; return false; } }
      return true; }, IntPtr.Zero);
    IntPtr lv = IntPtr.Zero;
    EnumChildWindows(top, delegate(IntPtr h, IntPtr l) {
      StringBuilder c = new StringBuilder(256); GetClassNameW(h, c, 256);
      if (c.ToString().Contains("SysListView32")) { lv = h; return false; }
      return true; }, IntPtr.Zero);
    sb.AppendLine("top=" + top + " lv=" + lv);

    int count = (int)SendMessage(lv, 0x1004, IntPtr.Zero, IntPtr.Zero);
    sb.AppendLine("count=" + count);

    for (int i = 0; i < 3; i++) {
      IntPtr buf = Marshal.AllocHGlobal(16);
      for (int k = 0; k < 16; k++) Marshal.WriteByte(buf, k, 0);
      IntPtr res = SendMessage(lv, 0x100E, (IntPtr)i, buf);
      int a = Marshal.ReadInt32(buf, 0);
      int b = Marshal.ReadInt32(buf, 4);
      int c = Marshal.ReadInt32(buf, 8);
      int d = Marshal.ReadInt32(buf, 12);
      Marshal.FreeHGlobal(buf);
      sb.AppendLine("rect" + i + " res=" + res + " raw=" + a + "," + b + "," + c + "," + d);
    }
    return sb.ToString(); }
}
"@
$proc = Start-Process -FilePath 'F:\miniMAxWork\VMixer\release\VMixer.exe' -ArgumentList '--settings' -PassThru
Start-Sleep -Seconds 4
[LvMsg]::Probe([uint32]$proc.Id)
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
