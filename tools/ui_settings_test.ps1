$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
public class UiAct {
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

  public static IntPtr FindTop(uint pid) {
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
  public static void ClickWindow(IntPtr h) {
    RECT r; GetWindowRect(h, out r);
    Click((r.L + r.R) / 2, (r.T + r.B) / 2);
  }
  public static void Capture(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r);
    int w = r.R - r.L, ht = r.B - r.T;
    if (w <= 0 || ht <= 0) throw new Exception("bad rect");
    using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(w, ht))
    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp)) {
      IntPtr hdc = g.GetHdc();
      PrintWindow(h, hdc, 2);
      g.ReleaseHdc(hdc);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
  }
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
}
"@
try { [UiAct]::SetProcessDpiAwarenessContext([IntPtr](-4)) } catch { }
try { [UiAct]::SetProcessDPIAware() | Out-Null } catch { }
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class VmDll2 {
  [DllImport(@"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")]
  public static extern int VBVMR_Login();
  [DllImport(@"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")]
  public static extern int VBVMR_Logout();
  [DllImport(@"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")]
  public static extern int VBVMR_SetParameterFloat(string name, float value);
  [DllImport(@"C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll")]
  public static extern int VBVMR_GetParameterFloat(string name, out float value);
}
"@

# 1) 恢复 A1 原始音量
[VmDll2]::VBVMR_Login() | Out-Null
[VmDll2]::VBVMR_SetParameterFloat('Bus[0].Gain', [single](-9.8)) | Out-Null
Start-Sleep -Milliseconds 600
$v = 0.0
[VmDll2]::VBVMR_GetParameterFloat('Bus[0].Gain', [ref]$v) | Out-Null
Write-Output "A1 restored to $v dB"

# 2) 打开通道设置对话框
$proc = Get-Process VMixer -ErrorAction SilentlyContinue | Select-Object -First 1
$panel = [UiAct]::FindTop([uint32]$proc.Id)
[UiAct]::ShowWindow($panel, 5) | Out-Null
[UiAct]::SetForegroundWindow($panel) | Out-Null
Start-Sleep -Milliseconds 700
$btnText = [string]([char]0x8BBE) + [char]0x7F6E   # 设置
$dlgText = [string]([char]0x901A) + [char]0x9053 + [char]0x8BBE + [char]0x7F6E  # 通道设置
$btn = [UiAct]::FindChildByText($panel, $btnText)
if ($btn -ne [IntPtr]::Zero) {
  Write-Output "clicking settings button"
  [UiAct]::ClickWindow($btn)
  Start-Sleep -Milliseconds 1500
  $dlg = [UiAct]::FindChildByText([IntPtr]::Zero, $dlgText)
  if ($dlg -ne [IntPtr]::Zero) {
    [UiAct]::Capture($dlg, 'F:\miniMAxWork\VMixer\dist\settings.png')
    Write-Output 'saved settings.png'
  } else { Write-Output 'settings dialog not found' }
} else { Write-Output 'settings button not found' }
[VmDll2]::VBVMR_Logout() | Out-Null
