$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
public class MenuShot2 {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int d, IntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr e);
  public static void RightClick(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(300); mouse_event(0x0008,0,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(90); mouse_event(0x0010,0,0,0,IntPtr.Zero); }
  public static void LeftClick(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(250); mouse_event(0x0002,0,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(80); mouse_event(0x0004,0,0,0,IntPtr.Zero); }
  public static void Shot(int x, int y, int w, int h, string path) {
    using (Bitmap bmp = new Bitmap(w, h))
    using (Graphics g = Graphics.FromImage(bmp)) {
      g.CopyFromScreen(x, y, 0, 0, bmp.Size);
      bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png); } }
}
"@
[MenuShot2]::SetProcessDPIAware() | Out-Null

$tx = 2330; $ty = 1420
[MenuShot2]::RightClick($tx, $ty)
Start-Sleep -Seconds 2
# hover over the "prompt style" submenu entry (5th item of the popup)
[MenuShot2]::LeftClick(2065, 1327)
Start-Sleep -Milliseconds 200
[MenuShot2]::Shot(1980, 1020, 700, 420, 'F:\miniMAxWork\VMixer\dist\tray_menu_sub.png')
Write-Output 'submenu shot saved'
