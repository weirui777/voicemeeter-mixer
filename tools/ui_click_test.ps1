$ErrorActionPreference = 'Stop'
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class UiTest {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int max);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;

  public static IntPtr FindPanel(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) { found = h; return false; }
      return true;
    }, IntPtr.Zero);
    return found;
  }
  public static IntPtr FindChildByText(IntPtr parent, string text) {
    IntPtr found = IntPtr.Zero;
    EnumChildWindows(parent, delegate(IntPtr h, IntPtr l) {
      System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
      GetWindowText(h, sb, 128);
      if (sb.ToString() == text) { found = h; return false; }
      return true;
    }, IntPtr.Zero);
    return found;
  }
  public static void Click(int x, int y) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(300);
    mouse_event(LEFTDOWN, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(150);
    mouse_event(LEFTUP, 0, 0, 0, IntPtr.Zero);
  }
}
"@
try { [UiTest]::SetProcessDpiAwarenessContext([IntPtr](-4)) } catch { }
try { [UiTest]::SetProcessDPIAware() | Out-Null } catch { }

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class VmDll {
  [DllImport(@"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")]
  public static extern int VBVMR_Login();
  [DllImport(@"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")]
  public static extern int VBVMR_Logout();
  [DllImport(@"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")]
  public static extern int VBVMR_GetParameterFloat(string name, out float value);
}
"@

function Get-VmGain([string]$name) {
  $v = 0.0
  [VmDll]::VBVMR_GetParameterFloat($name, [ref]$v) | Out-Null
  return [math]::Round($v, 2)
}

$proc = Get-Process VMixer -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Output 'VMixer not running'; exit 1 }

$h = [UiTest]::FindPanel([uint32]$proc.Id)
if ($h -eq [IntPtr]::Zero) { Write-Output 'panel window not found'; exit 1 }
[UiTest]::ShowWindow($h, 5) | Out-Null   # SW_SHOW
[UiTest]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 800

$r = New-Object UiTest+RECT
[UiTest]::GetWindowRect($h, [ref]$r) | Out-Null
Write-Output ("panel rect: {0},{1} {2}x{3}" -f $r.L, $r.T, ($r.R - $r.L), ($r.B - $r.T))

[VmDll]::VBVMR_Login() | Out-Null
$before = Get-VmGain 'Bus[0].Gain'
Write-Output "A1 before = $before dB"

# A1 音量条中心（相对窗口，物理像素）：逻辑 x≈44, y(轨道顶 74..174) → 物理 1.25x
$scale = 1.25
$x = $r.L + [int](44 * $scale)
$y = $r.T + [int](169 * $scale)
Write-Output "click at $x,$y (期望 -20 dB 左右)"
[UiTest]::Click($x, $y)
Start-Sleep -Milliseconds 1200

$after = Get-VmGain 'Bus[0].Gain'
Write-Output "A1 after  = $after dB"
if ([math]::Abs($before - $after) -gt 0.5) {
  Write-Output "RESULT: PASS - 面板拖动真实写入了 Voicemeeter"
} else {
  Write-Output "RESULT: FAIL - 面板没有生效"
}
[VmDll]::VBVMR_Logout() | Out-Null
