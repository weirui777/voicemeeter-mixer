$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
public class IcoDump {
  // Extract every BMP entry of an .ico into one PNG strip.
  public static string Dump(string icoPath, string pngPath) {
    byte[] data = File.ReadAllBytes(icoPath);
    int count = BitConverter.ToUInt16(data, 4);
    int offset = 6 + 16 * count;
    int maxSize = 0;
    int[] sizes = new int[count];
    for (int i = 0; i < count; i++) {
      int w = data[6 + 16 * i];
      int h = data[6 + 16 * i + 1];
      if (w == 0) w = 256;
      if (h == 0) h = 256;
      sizes[i] = w;
      if (w > maxSize) maxSize = w;
    }
    int pad = 12;
    int stripW = count * (maxSize + pad) + pad;
    int stripH = maxSize + pad * 2;
    using (Bitmap strip = new Bitmap(stripW, stripH, PixelFormat.Format32bppArgb))
    using (Graphics g = Graphics.FromImage(strip)) {
      g.Clear(Color.FromArgb(255, 40, 40, 44));
      g.InterpolationMode = InterpolationMode.NearestNeighbor;
      for (int i = 0; i < count; i++) {
        int len = BitConverter.ToInt32(data, 6 + 16 * i + 8);
        int dataOff = BitConverter.ToInt32(data, 6 + 16 * i + 12);
        int size = sizes[i];
        int bpp = BitConverter.ToInt16(data, dataOff + 14);
        int xorSize = size * size * 4;
        int xorOff = dataOff + 40;
        using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
          for (int y = 0; y < size; y++) {
            int src = xorOff + (size - 1 - y) * size * 4;
            for (int x = 0; x < size; x++) {
              int o = src + x * 4;
              bmp.SetPixel(x, y, Color.FromArgb(data[o + 3], data[o + 2], data[o + 1], data[o + 0]));
            }
          }
          int dx = pad + i * (maxSize + pad);
          int dy = pad + (maxSize - size);
          g.DrawImage(bmp, new Rectangle(dx, dy, size, size));
        }
      }
      strip.Save(pngPath, ImageFormat.Png);
    }
    return "count=" + count;
  }
}
"@
[IcoDump]::Dump($args[0], $args[1])
