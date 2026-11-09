using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;

namespace VMixer
{
    public enum GlyphKind { Microphone, Wave, Speaker, Bus, Headphones, Music, Monitor, Disc }

    /// <summary>程序内所有图标的绘制代码（托盘图标 + 通道图标）。</summary>
    public static class Glyphs
    {
        public static void Draw(Graphics g, Rectangle r, GlyphKind kind, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = r.Width, h = r.Height;
            float cx = r.X + w / 2f, cy = r.Y + h / 2f;
            float s = Math.Min(w, h) / 32f;
            if (s <= 0) s = 1f;

            using (SolidBrush b = new SolidBrush(color))
            using (Pen p = new Pen(color, Math.Max(1.6f, 1.8f * s)))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                switch (kind)
                {
                    case GlyphKind.Microphone:
                        {
                            float bw = 8 * s, bh = 14 * s;
                            float bx = cx - bw / 2, by = cy - bh / 2 - 2 * s;
                            using (GraphicsPath path = new GraphicsPath())
                            {
                                path.AddArc(bx, by, bw, bw, 180, 180);
                                path.AddArc(bx, by + bh - bw, bw, bw, 0, 180);
                                path.CloseFigure();
                                g.FillPath(b, path);
                            }
                            g.DrawArc(p, cx - 8 * s, cy - 7 * s, 16 * s, 16 * s, 0, 180);
                            g.DrawLine(p, cx, cy + 9 * s, cx, cy + 13 * s);
                            g.DrawLine(p, cx - 5 * s, cy + 13 * s, cx + 5 * s, cy + 13 * s);
                            break;
                        }
                    case GlyphKind.Wave:
                        {
                            float[] hs = { 8, 16, 11, 20, 13 };
                            float bw = 3 * s;
                            float gap = 3.4f * s;
                            float total = hs.Length * bw + (hs.Length - 1) * gap;
                            float x = cx - total / 2;
                            for (int i = 0; i < hs.Length; i++)
                            {
                                float hh = hs[i] * s;
                                using (GraphicsPath path = new GraphicsPath())
                                {
                                    path.AddArc(x, cy - hh / 2, bw, bw, 180, 180);
                                    path.AddArc(x, cy + hh / 2 - bw, bw, bw, 0, 180);
                                    path.CloseFigure();
                                    g.FillPath(b, path);
                                }
                                x += bw + gap;
                            }
                            break;
                        }
                    case GlyphKind.Music:
                        {
                            // 八分音符：符头（斜着的椭圆）+ 符干（竖线）+ 符尾（弧线）
                            float hw = 9 * s, hh = 7 * s;
                            float hcx = cx - 3.5f * s, hcy = cy + 5.5f * s;
                            GraphicsState st = g.Save();
                            g.TranslateTransform(hcx, hcy);
                            g.RotateTransform(-18f);
                            g.FillEllipse(b, -hw / 2f, -hh / 2f, hw, hh);
                            g.Restore(st);
                            float stemX = hcx + hw / 2f - 0.8f * s;
                            g.DrawLine(p, stemX, hcy, stemX, cy - 10.5f * s);
                            using (GraphicsPath flag = new GraphicsPath())
                            {
                                flag.AddBezier(stemX, cy - 10.5f * s,
                                               stemX + 6.5f * s, cy - 8.5f * s,
                                               stemX + 7.5f * s, cy - 4.5f * s,
                                               stemX + 4.0f * s, cy - 1.5f * s);
                                g.DrawPath(p, flag);
                            }
                            break;
                        }
                    case GlyphKind.Speaker:
                        {
                            g.FillPolygon(b, new PointF[]
                            {
                                new PointF(cx - 9 * s, cy - 4 * s),
                                new PointF(cx - 4 * s, cy - 4 * s),
                                new PointF(cx + 1 * s, cy - 10 * s),
                                new PointF(cx + 1 * s, cy + 10 * s),
                                new PointF(cx - 4 * s, cy + 4 * s),
                                new PointF(cx - 9 * s, cy + 4 * s)
                            });
                            g.DrawArc(p, cx - 1 * s, cy - 8 * s, 10 * s, 16 * s, -55, 110);
                            g.DrawArc(p, cx - 1 * s, cy - 13 * s, 18 * s, 26 * s, -50, 100);
                            break;
                        }
                    case GlyphKind.Bus:
                        {
                            using (GraphicsPath path = new GraphicsPath())
                            {
                                path.AddArc(cx - 10 * s, cy - 10 * s, 20 * s, 20 * s, 180, 180);
                                path.AddArc(cx - 10 * s, cy - 10 * s, 20 * s, 20 * s, 0, 180);
                                path.CloseFigure();
                                g.DrawPath(p, path);
                            }
                            g.DrawLine(p, cx - 5 * s, cy, cx + 5 * s, cy);
                            g.DrawLine(p, cx, cy - 5 * s, cx, cy + 5 * s);
                            break;
                        }
                    case GlyphKind.Headphones:
                        {
                            g.DrawArc(p, cx - 11 * s, cy - 10 * s, 22 * s, 22 * s, 180, 180);
                            using (GraphicsPath l = new GraphicsPath()) { l.AddArc(cx - 12 * s, cy - 6 * s, 7 * s, 14 * s, 90, 180); g.FillPath(b, l); }
                            using (GraphicsPath rr = new GraphicsPath()) { rr.AddArc(cx + 5 * s, cy - 6 * s, 7 * s, 14 * s, 270, 180); g.FillPath(b, rr); }
                            break;
                        }
                    case GlyphKind.Monitor:
                        {
                            // 小屏幕：圆角矩形的屏 + 中间支柱 + 底座
                            float sw = 22 * s, sh = 14 * s;
                            float sx = cx - sw / 2f, sy = cy - 12 * s;
                            float rr = 2.5f * s;
                            using (GraphicsPath scr = new GraphicsPath())
                            {
                                scr.AddArc(sx, sy, rr, rr, 180, 90);
                                scr.AddArc(sx + sw - rr, sy, rr, rr, 270, 90);
                                scr.AddArc(sx + sw - rr, sy + sh - rr, rr, rr, 0, 90);
                                scr.AddArc(sx, sy + sh - rr, rr, rr, 90, 90);
                                scr.CloseFigure();
                                g.DrawPath(p, scr);
                            }
                            g.DrawLine(p, cx, sy + sh, cx, cy + 7 * s);
                            g.DrawLine(p, cx - 5 * s, cy + 8 * s, cx + 5 * s, cy + 8 * s);
                            break;
                        }
                    case GlyphKind.Disc:
                        {
                            // 唱片：外圈实心 + 中间挖空（EvenOdd）
                            using (GraphicsPath ring = new GraphicsPath())
                            {
                                ring.FillMode = FillMode.Alternate;   // 内外两圈相减 = 唱片中间的孔
                                ring.AddEllipse(cx - 11 * s, cy - 11 * s, 22 * s, 22 * s);
                                ring.AddEllipse(cx - 3 * s, cy - 3 * s, 6 * s, 6 * s);
                                g.FillPath(b, ring);
                            }
                            using (GraphicsPath inner = new GraphicsPath())
                            {
                                inner.AddEllipse(cx - 6 * s, cy - 6 * s, 12 * s, 12 * s);
                                g.DrawPath(p, inner);
                            }
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// 生成托盘用的应用图标：波形（4 根圆头竖条）+ 右下角小 v，配色跟随任务栏深/浅色。
        /// 做成多尺寸（16/20/24/32/48），任务栏按自己的 DPI 取最合适的那一档，不靠缩放。
        /// </summary>
        public static Icon CreateAppIcon(Color speaker, Color wave, Color mark)
        {
            using (MemoryStream ms = BuildIconStream(speaker, wave))
            {
                return new Icon(ms);
            }
        }

        /// <summary>
        /// 用某个内置字形生成窗口标题栏用的图标（设置窗口用波形图标）。
        /// </summary>
        public static Icon CreateGlyphIcon(GlyphKind kind, Color color)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    Draw(g, new Rectangle(0, 0, 32, 32), kind, color);
                }

                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(h))
                    {
                        return (Icon)temp.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(h);
                }
            }
        }

        /// <summary>把托盘图标画成 PNG，便于预览。</summary>
        public static void SaveIconPng(string path, Color speaker, Color wave, Color mark)
        {
            using (Bitmap bmp = new Bitmap(64, 64))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                DrawAppGlyph(g, 64f, speaker, wave);
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        /// <summary>
        /// 把所有内置字形画成一张对照图（调试用）：
        /// 上下两排，分别用大尺寸和小尺寸，方便检查每个字形认不认得出。
        /// </summary>
        public static void SaveGlyphSheet(string path)
        {
            int n = IconStore.BuiltinKeys.Length;
            int cell = 56;
            using (Bitmap bmp = new Bitmap(n * cell, cell * 2 + 12))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.FromArgb(32, 32, 36));
                Color fg = Color.FromArgb(235, 238, 238, 242);
                using (SolidBrush br = new SolidBrush(fg))
                using (Font fnt = new Font("Microsoft YaHei UI", 9f))
                {
                    for (int i = 0; i < n; i++)
                    {
                        GlyphKind kind;
                        if (!IconStore.TryBuiltin(IconStore.BuiltinKeys[i], out kind)) continue;
                        Draw(g, new Rectangle(i * cell + 12, 8, 32, 32), kind, fg);
                        Draw(g, new Rectangle(i * cell + 20, cell + 2, 18, 18), kind, fg);
                        g.DrawString(IconStore.BuiltinNames[i], fnt, br,
                            i * cell + 4, cell + 24);
                    }
                }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        /// <summary>
        /// 生成多尺寸 .ico（16/20/24/32/48/64/128/256），用于 exe 的窗口/资源管理器图标，
        /// 也用来给托盘提供多尺寸图标（任务栏按自己的 DPI 取最合适的一档，不靠缩放）。
        ///
        /// 条目用经典的 BMP（DIB）格式而不是 PNG：Windows 两种都认，
        /// 但 System.Drawing.Icon 读不了 PNG 条目，托盘那边必须用 BMP。
        /// </summary>
        public static void SaveIconIco(string path, Color speaker)
        {
            // 注意：MemoryStream 被 Dispose 后 ToArray() 会抛异常，所以这里不用 using
            MemoryStream ms = BuildIconStream(speaker, speaker);
            try { File.WriteAllBytes(path, ms.ToArray()); }
            finally { ms.Dispose(); }
        }

        /// <summary>在内存里拼一个多尺寸 ico（BMP 条目）。</summary>
        private static MemoryStream BuildIconStream(Color barColor, Color vColor)
        {
            int[] sizes = new int[] { 16, 20, 24, 32, 48, 64 };
            List<byte[]> images = new List<byte[]>();
            for (int i = 0; i < sizes.Length; i++)
                images.Add(BuildDibEntry(sizes[i], barColor, vColor));

            MemoryStream outMs = new MemoryStream();
            BinaryWriter w = new BinaryWriter(outMs);
            w.Write((ushort)0);            // reserved
            w.Write((ushort)1);            // type = icon
            w.Write((ushort)sizes.Length);

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                w.Write((byte)(s >= 256 ? 0 : s));   // width，0 表示 256
                w.Write((byte)(s >= 256 ? 0 : s));   // height
                w.Write((byte)0);                   // 颜色数
                w.Write((byte)0);                   // reserved
                w.Write((ushort)1);                 // planes
                w.Write((ushort)32);                // bpp
                w.Write(images[i].Length);          // 数据长度
                w.Write(offset);                    // 偏移
                offset += images[i].Length;
            }
            for (int i = 0; i < sizes.Length; i++) w.Write(images[i]);
            w.Flush();
            outMs.Position = 0;
            return outMs;
        }

        /// <summary>
        /// 生成一个 BMP 形式的 ico 条目：BITMAPINFOHEADER + 像素（BGRA，自下而上）+ AND 掩码。
        /// </summary>
        private static byte[] BuildDibEntry(int size, Color barColor, Color vColor)
        {
            byte[] pixels = new byte[size * size * 4];
            byte[] mask = new byte[((size + 31) / 32) * 4 * size];

            using (Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    DrawAppGlyph(g, (float)size, barColor, vColor);
                }

                System.Drawing.Imaging.BitmapData data =
                    bmp.LockBits(new Rectangle(0, 0, size, size),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    for (int row = 0; row < size; row++)
                    {
                        IntPtr src = data.Scan0 + row * data.Stride;
                        int dst = (size - 1 - row) * size * 4;   // ico 里像素自下而上
                        for (int x = 0; x < size; x++)
                        {
                            byte b = Marshal.ReadByte(src, x * 4 + 0);
                            byte gch = Marshal.ReadByte(src, x * 4 + 1);
                            byte r = Marshal.ReadByte(src, x * 4 + 2);
                            byte a = Marshal.ReadByte(src, x * 4 + 3);
                            int o = dst + x * 4;
                            pixels[o + 0] = b;
                            pixels[o + 1] = gch;
                            pixels[o + 2] = r;
                            pixels[o + 3] = a;

                            // AND 掩码：alpha 为 0 的位置置 1（老渲染器靠它判透明）
                            if (a == 0)
                            {
                                int maskRow = size - 1 - row;
                                int bit = x & 31;
                                int maskByte = maskRow * ((size + 31) / 32) * 4 + (x >> 5);
                                mask[maskByte] |= (byte)(0x80 >> bit);
                            }
                        }
                    }
                }
                finally { bmp.UnlockBits(data); }
            }

            MemoryStream ms = new MemoryStream();
            BinaryWriter w = new BinaryWriter(ms);
            // BITMAPINFOHEADER
            w.Write((uint)40);                 // biSize
            w.Write(size);                     // biWidth
            w.Write(size * 2);                 // biHeight（XOR + AND）
            w.Write((ushort)1);                // biPlanes
            w.Write((ushort)32);               // biBitCount
            w.Write((uint)0);                  // biCompression = BI_RGB
            w.Write((uint)(size * size * 4));  // biSizeImage
            w.Write((int)0);                   // biXPelsPerMeter
            w.Write((int)0);                   // biYPelsPerMeter
            w.Write((uint)0);                  // biClrUsed
            w.Write((uint)0);                  // biClrImportant
            w.Write(pixels);
            w.Write(mask);
            w.Flush();
            byte[] result = ms.ToArray();
            ms.Dispose();
            return result;
        }

        /// <summary>
        /// 托盘图标本体：左上几根圆头竖条（波形），右下角一个小 v（Voicemeeter）。
        ///
        /// 小尺寸（≤20px）单独走简化版：条更少更粗、v 更大更实，
        /// 否则缩到托盘尺寸会糊成一团白块。
        /// </summary>
        private static void DrawAppGlyph(Graphics g, float size, Color barColor, Color vColor)
        {
            bool tiny = size <= 20.5f;

            // ---- 波形：竖条，高低错落 ----
            float[] ratios;
            if (tiny) ratios = new float[] { 0.62f, 1.00f, 0.78f };
            else ratios = new float[] { 0.42f, 0.75f, 0.55f, 1.00f };

            float bx0 = size * 0.07f;
            float bx1 = size * (tiny ? 0.60f : 0.56f);
            float areaW = bx1 - bx0;
            float bw = areaW * (tiny ? 0.27f : 0.19f);
            float gap = areaW * (tiny ? 0.09f : 0.08f);
            float cy = size * (tiny ? 0.36f : 0.40f);
            float maxH = size * (tiny ? 0.62f : 0.58f);

            using (SolidBrush b = new SolidBrush(barColor))
            {
                float x = bx0;
                for (int i = 0; i < ratios.Length; i++)
                {
                    float hh = maxH * ratios[i];
                    float y = cy - hh / 2f;
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddArc(x, y, bw, bw, 180, 180);
                        path.AddArc(x, y + hh - bw, bw, bw, 0, 180);
                        path.CloseFigure();
                        g.FillPath(b, path);
                    }
                    x += bw + gap;
                }
            }

            // ---- 右下角小 v ----
            float vPen = tiny ? Math.Max(1.5f, size * 0.10f) : Math.Max(1.2f, size * 0.055f);
            float vTop = size * (tiny ? 0.56f : 0.68f);
            float vBot = size * (tiny ? 0.90f : 0.87f);
            using (Pen p = new Pen(vColor, vPen))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                g.DrawLines(p, new PointF[]
                {
                    new PointF(size * (tiny ? 0.60f : 0.715f), vTop),
                    new PointF(size * (tiny ? 0.755f : 0.795f), vBot),
                    new PointF(size * (tiny ? 0.91f : 0.875f), vTop)
                });
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
