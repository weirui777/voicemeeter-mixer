$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Text;
public class DragTest {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr SetActiveWindow(IntPtr h);
  [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
  [DllImport("user32.dll")] public static extern uint GetForegroundWindow();
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
  public static uint GA_ROOT = 2;

  public static void ForceForeground(IntPtr h) {
    IntPtr fg = (IntPtr)GetForegroundWindow();
    uint fgPid; GetWindowThreadProcessId(fg, out fgPid);
    uint fgThread = GetWindowThreadProcessId(fg, out fgPid);
    uint myThread = GetCurrentThreadId();
    AttachThreadInput(myThread, fgThread, true);
    BringWindowToTop(h);
    SetForegroundWindow(h);
    SetActiveWindow(h);
    AttachThreadInput(myThread, fgThread, false);
    ShowWindow(h, 9);
    SetForegroundWindow(h);
  }

  public static string WinAt(int x, int y) {
    POINT p; p.X = x; p.Y = y;
    IntPtr w = WindowFromPoint(p);
    if (w == IntPtr.Zero) return "none";
    IntPtr root = GetAncestor(w, GA_ROOT);
    StringBuilder sb = new StringBuilder(256);
    GetClassNameW(root, sb, 256);
    return sb.ToString() + "|" + root.ToInt64();
  }
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int d, IntPtr e);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder sb, int max);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder sb, int max);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L; public int T; public int R; public int B; }

  public static IntPtr FindTop(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) {
        RECT r; GetWindowRect(h, out r);
        if (r.R - r.L >= 500 && r.B - r.T >= 400) { found = h; return false; } }
      return true; }, IntPtr.Zero);
    return found; }

  public static IntPtr FindListView(IntPtr parent) {
    IntPtr found = IntPtr.Zero;
    EnumChildWindows(parent, delegate(IntPtr h, IntPtr l) {
      StringBuilder sb = new StringBuilder(256);
      GetClassNameW(h, sb, 256);
      if (sb.ToString().Contains("SysListView32")) { found = h; return false; }
      return true; }, IntPtr.Zero);
    return found; }

  public static IntPtr FindHeader(IntPtr parent) {
    IntPtr found = IntPtr.Zero;
    EnumChildWindows(parent, delegate(IntPtr h, IntPtr l) {
      StringBuilder sb = new StringBuilder(256);
      GetClassNameW(h, sb, 256);
      if (sb.ToString() == "SysHeader32") { found = h; return false; }
      return true; }, IntPtr.Zero);
    return found; }

  public static IntPtr FindButton(IntPtr parent, string text) {
    IntPtr target = IntPtr.Zero;
    EnumChildWindows(parent, delegate(IntPtr h, IntPtr l) {
      StringBuilder sb = new StringBuilder(256);
      GetWindowTextW(h, sb, 256);
      if (sb.ToString() == text) { target = h; return false; }
      return true; }, IntPtr.Zero);
    return target; }

  public static bool Capture(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r);
    int w = r.R - r.L, ht = r.B - r.T;
    if (w <= 0 || ht <= 0) return false;
    using (Bitmap bmp = new Bitmap(w, ht))
    using (Graphics g = Graphics.FromImage(bmp)) {
      IntPtr hdc = g.GetHdc();
      bool ok = PrintWindow(h, hdc, 2);
      g.ReleaseHdc(hdc);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
      return ok; } }

  public static void Down(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(250); mouse_event(0x0002,0,0,0,IntPtr.Zero); }
  public static void Move(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(150); }
  public static void Up(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(150); mouse_event(0x0004,0,0,0,IntPtr.Zero); }

  // find the highlighted (selected) row band in a captured list image
  public static string FindSelection(string path) {
    using (Bitmap bmp = new Bitmap(path)) {
      int top = -1, bot = -1;
      for (int y = 0; y < bmp.Height; y++) {
        int cnt = 0;
        for (int x = 0; x < bmp.Width; x += 4) {
          Color c = bmp.GetPixel(x, y);
          if (c.B > 140 && c.B - c.R > 70 && c.B - c.G > 40) cnt++;
        }
        if (cnt > 20) { if (top < 0) top = y; bot = y; }
      }
      return "selTop=" + top + " selBot=" + bot + " h=" + bmp.Height + " w=" + bmp.Width;
    } }

  // row height of a details list view: measured from two text rows in the shot
  public static int RowHeight(string path, int firstRowTop) {
    using (Bitmap bmp = new Bitmap(path)) {
      // look for horizontal grid lines (light grey) below the header
      int last = -1;
      for (int y = firstRowTop + 2; y < Math.Min(bmp.Height, firstRowTop + 120); y++) {
        int grey = 0;
        for (int x = 4; x < Math.Min(bmp.Width, 400); x += 2) {
          Color c = bmp.GetPixel(x, y);
          if (c.R > 200 && c.G > 200 && c.B > 200 && Math.Abs(c.R - c.B) < 12) grey++;
        }
        if (grey > 100) { if (last < 0) last = y; else { return y - last; } }
      }
      return 0; } }
}
"@
[DragTest]::SetProcessDPIAware() | Out-Null

$exe = 'F:\miniMAxWork\VMixer\release\VMixer.exe'
$proc = Start-Process -FilePath $exe -ArgumentList '--settings' -PassThru
$deadline = (Get-Date).AddSeconds(15)
$hwnd = [IntPtr]::Zero
while ((Get-Date) -lt $deadline) {
  $hwnd = [DragTest]::FindTop([uint32]$proc.Id)
  if ($hwnd -ne [IntPtr]::Zero) { break }
  Start-Sleep -Milliseconds 250
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Output 'dialog not found'; Stop-Process -Id $proc.Id -Force; exit }
Start-Sleep -Milliseconds 1500
$console = [DragTest]::GetConsoleWindow()
if ($console -ne [IntPtr]::Zero) { [DragTest]::ShowWindow($console, 6) | Out-Null }
[DragTest]::ForceForeground($hwnd) | Out-Null
Start-Sleep -Milliseconds 800

$lv = [DragTest]::FindListView($hwnd)
$hd = [DragTest]::FindHeader($hwnd)
$lr = New-Object DragTest+RECT
$hr = New-Object DragTest+RECT
[DragTest]::GetWindowRect($lv, [ref]$lr) | Out-Null
[DragTest]::GetWindowRect($hd, [ref]$hr) | Out-Null
Write-Output ("list screen rect " + $lr.L + "," + $lr.T + "," + $lr.R + "," + $lr.B)
Write-Output ("header screen rect " + $hr.L + "," + $hr.T + "," + $hr.R + "," + $hr.B)

[DragTest]::Capture($lv, 'F:\miniMAxWork\VMixer\dist\lv_shot.png') | Out-Null
$sel = [DragTest]::FindSelection('F:\miniMAxWork\VMixer\dist\lv_shot.png')
Write-Output $sel
# row 0 band = selTop..selBot -> row height; row 0 top in client coords = selTop
$rowH = 20
if ($sel -match 'selTop=(\d+) selBot=(\d+)') {
  $rowH = [int]$Matches[2] - [int]$Matches[1] + 1
}
$row0ClientY = 26
if ($sel -match 'selTop=(\d+)') { $row0ClientY = [int]$Matches[1] }
Write-Output ("rowHeight=" + $rowH + " row0ClientY=" + $row0ClientY)

# drag row 1 (index 1) up above row 0
$row0ScreenY = $lr.T + $row0ClientY
$grabY = $row0ScreenY + $rowH + [int]($rowH / 2)      # middle of row 1
$dropY = $row0ScreenY - 2                                # just above row 0
$grabX = $lr.L + 90
$dropX = $lr.L + 90
Write-Output ("drag from " + $grabX + "," + $grabY + " to " + $dropX + "," + $dropY)
Write-Output ("window at grab point: " + [DragTest]::WinAt($grabX, $grabY))
Write-Output ("dialog hwnd: " + $hwnd.ToInt64())

[DragTest]::Down($grabX, $grabY)
[DragTest]::Move($grabX, [int](($grabY + $dropY) / 2))
[DragTest]::Move($dropX, $dropY)
Start-Sleep -Milliseconds 400
[DragTest]::Capture($hwnd, 'F:\miniMAxWork\VMixer\dist\drag_mid.png') | Out-Null
[DragTest]::Up($dropX, $dropY)
Start-Sleep -Milliseconds 900
[DragTest]::Capture($hwnd, 'F:\miniMAxWork\VMixer\dist\drag_after.png') | Out-Null
[DragTest]::Capture($lv, 'F:\miniMAxWork\VMixer\dist\lv_after.png') | Out-Null

$okBtn = [DragTest]::FindButton($hwnd, '确定')
if ($okBtn -ne [IntPtr]::Zero) {
  $r = New-Object DragTest+RECT
  [DragTest]::GetWindowRect($okBtn, [ref]$r) | Out-Null
  [DragTest]::Down([int](($r.L+$r.R)/2), [int](($r.T+$r.B)/2))
  [DragTest]::Up([int](($r.L+$r.R)/2), [int](($r.T+$r.B)/2))
  Write-Output 'OK clicked'
}
Start-Sleep -Milliseconds 1500
Get-Content "$env:APPDATA\VMixer\config.ini" | Select-String -Pattern 'Order'
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Write-Output 'done'
