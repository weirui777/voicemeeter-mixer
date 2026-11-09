using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VMixer
{
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
    }

    internal static class TextTest
    {
        [STAThread]
        public static void Main()
        {
            try { NativeMethods.SetProcessDPIAware(); } catch { }
            Theme.Refresh();
            Theme th = Theme.Current;

            using (Bitmap bmp = new Bitmap(400, 260))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(40, 40, 44));
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                Font f = Ui.Font(8.5f, FontStyle.Regular);
                string s = "主音量 A1";

                // 完全复刻面板里的调用：118x22 的框
                LayeredSurface.DrawTextEllipsis(g, s, f, th.Accent,
                    new Rectangle(32, 140, 118, 22),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                // 同样内容，框高 30
                LayeredSurface.DrawTextEllipsis(g, s, f, th.Accent,
                    new Rectangle(160, 140, 118, 30),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                // 同样内容，框高 22 但不设 LineLimit
                using (StringFormat sf = new StringFormat())
                using (SolidBrush b = new SolidBrush(Color.Lime))
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(s, f, b, new Rectangle(290, 140, 100, 22), sf);
                }

                // 框画出来看看位置对不对
                using (Pen p = new Pen(Color.Red, 1f)) g.DrawRectangle(p, 32, 140, 118, 22);
                using (Pen p = new Pen(Color.Yellow, 1f)) g.DrawRectangle(p, 160, 140, 118, 30);

                bmp.Save(@"F:\miniMAxWork\VMixer\dist\texttest2.png", System.Drawing.Imaging.ImageFormat.Png);
            }
            Console.WriteLine("saved dist\\texttest2.png");
        }
    }
}
