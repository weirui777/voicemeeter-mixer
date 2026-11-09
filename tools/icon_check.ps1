$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
# render the icon at several sizes exactly as the tray would pick them
$src = 'F:\miniMAxWork\VMixer\dist\icon_preview.png'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Runtime.InteropServices;
using System.Drawing;
public class IconSizes {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  public static void Save(string path, int size, string srcPath, Color fg) {
    using (Bitmap b = new Bitmap(size, size))
    using (Graphics g = Graphics.FromImage(b)) {
      g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
      g.Clear(Color.Transparent);
      VMixer.GlyphsProbe.Draw(g, (float)size, fg);
      b.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    } }
}
"@
