$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class UiAct2 {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int max);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;

  public static IntPtr FindTopByPid(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumProc cb = delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) { found = h; return false; }
      return true;
    };
    EnumWindows(cb, IntPtr.Zero);
    return found;
  }
  public static IntPtr FindTopByPidAndText(uint pid, string text) {
    IntPtr found = IntPtr.Zero;
    EnumProc cb = delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      StringBuilder sb = new StringBuilder(128); GetWindowText(h, sb, 128);
      if (p == pid && IsWindowVisible(h) && sb.ToString() == text) { found = h; return false; }
      return true;
    };
    EnumWindows(cb, IntPtr.Zero);
    return found;
  }
  public static IntPtr FindChildByText(IntPtr parent, string text) {
    IntPtr found = IntPtr.Zero;
    EnumProc cb = delegate(IntPtr h, IntPtr l) {
      StringBuilder sb = new StringBuilder(128); GetWindowText(h, sb, 128);
      if (sb.ToString() == text) { found = h; return false; }
      return true;
    };
    EnumChildWindows(parent, cb, IntPtr.Zero);
    return found;
  }
  public static void ClickWindow(IntPtr h) {
    RECT r; GetWindowRect(h, out r);
    SetCursorPos((r.L + r.R) / 2, (r.T + r.B) / 2);
    System.Threading.Thread.Sleep(350);
    mouse_event(LEFTDOWN, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(150);
    mouse_event(LEFTUP, 0, 0, 0, IntPtr.Zero);
  }
  public static void Capture(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r);
    int w = r.R - r.L, ht = r.B - r.T;
    using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(w, ht))
    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp)) {
      IntPtr hdc = g.GetHdc();
      PrintWindow(h, hdc, 2);
      g.ReleaseHdc(hdc);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
  }
}
"@
try { [UiAct2]::SetProcessDpiAwarenessContext([IntPtr](-4)) } catch { }
try { [UiAct2]::SetProcessDPIAware() | Out-Null } catch { }

$btnText = [string]([char]0x8BBE) + [char]0x7F6E
$dlgText = [string]([char]0x901A) + [char]0x9053 + [char]0x8BBE + [char]0x7F6E

$proc = Get-Process VMixer -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output 'VMixer not running'; exit 1 }
$panel = [UiAct2]::FindTopByPid([uint32]$proc.Id)
if ($panel -eq [IntPtr]::Zero) { Write-Output 'panel not found'; exit 1 }
[UiAct2]::ShowWindow($panel, 5) | Out-Null
[UiAct2]::SetForegroundWindow($panel) | Out-Null
Start-Sleep -Milliseconds 700

$btn = [UiAct2]::FindChildByText($panel, $btnText)
if ($btn -eq [IntPtr]::Zero) { Write-Output 'settings button not found'; exit 1 }
Write-Output 'clicking settings button...'
[UiAct2]::ClickWindow($btn)
Start-Sleep -Milliseconds 1500

$dlg = [UiAct2]::FindTopByPidAndText([uint32]$proc.Id, $dlgText)
if ($dlg -ne [IntPtr]::Zero) {
  [UiAct2]::Capture($dlg, 'F:\miniMAxWork\VMixer\dist\settings.png')
  Write-Output 'saved settings.png'
} else {
  Write-Output 'settings dialog window not found (may be owned popup)'
}

