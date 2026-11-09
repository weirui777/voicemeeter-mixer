using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VMixer
{
    /// <summary>
    /// 分层（透明）窗口基类：整块内容绘制到一张 32 位 ARGB 位图，
    /// 再用 UpdateLayeredWindow 贴到屏幕上，因此可以实现圆角 + 柔和投影 + 半透明，
    /// 效果与 Windows 10/11 的悬浮卡片一致。
    /// </summary>
    public class LayeredSurface : Form
    {
        public const int ULW_ALPHA = 0x02;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int CX; public int CY; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);
        [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey,
            ref BLENDFUNCTION pblend, int dwFlags);

        private Bitmap _surface;
        private int _surfaceW, _surfaceH;

        /// <summary>阴影/圆角预留的边距（像素）。</summary>
        public int SurfaceMargin { get; set; }

        protected LayeredSurface()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            SurfaceMargin = 18;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000;   // WS_EX_LAYERED
                cp.ExStyle |= 0x00000080;   // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        /// <summary>绘制内容（坐标原点为窗口左上角）。</summary>
        protected virtual void RenderSurface(Graphics g, int width, int height) { }

        /// <summary>重新渲染并贴到屏幕。</summary>
        public void RefreshSurface()
        {
            if (!IsHandleCreated) return;
            int w = Width, h = Height;
            if (w <= 0 || h <= 0) return;

            if (_surface == null || _surfaceW != w || _surfaceH != h)
            {
                if (_surface != null) { _surface.Dispose(); _surface = null; }
                _surface = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                _surfaceW = w;
                _surfaceH = h;
            }

            using (Graphics g = Graphics.FromImage(_surface))
            {
                g.Clear(Color.FromArgb(0, 0, 0, 0));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                // 透明层上不能用 ClearType（会产生红蓝描边），改用灰度抗锯齿
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                RenderSurface(g, w, h);
            }

            ApplyLayered(_surface);
        }

        private void ApplyLayered(Bitmap bmp)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);

                SIZE size = new SIZE();
                size.CX = bmp.Width;
                size.CY = bmp.Height;
                POINT src = new POINT();
                src.X = 0; src.Y = 0;
                POINT dst = new POINT();
                dst.X = Left;
                dst.Y = Top;
                BLENDFUNCTION blend = new BLENDFUNCTION();
                blend.BlendOp = 0;              // AC_SRC_OVER
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = 1;          // AC_SRC_ALPHA

                UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) SelectObject(memDc, oldBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshSurface();
        }

        protected override void OnPaint(PaintEventArgs e) { }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _surface != null)
            {
                _surface.Dispose();
                _surface = null;
            }
            base.Dispose(disposing);
        }

        // ---------- 绘制辅助 ----------

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) { path.AddRectangle(r); return path; }
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>柔和投影（多层递减 alpha 叠加模拟高斯模糊）。</summary>
        public static void DrawSoftShadow(Graphics g, Rectangle card, int radius, int spread, int offsetY, byte strength)
        {
            for (int i = spread; i >= 1; i--)
            {
                float t = (float)i / spread;
                int alpha = (int)(strength * (1f - t) + 3);
                Rectangle r = new Rectangle(card.X - i, card.Y - i + offsetY, card.Width + i * 2, card.Height + i * 2);
                using (GraphicsPath p = RoundRect(r, radius + i))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                    g.FillPath(b, p);
            }
        }

        public static void FillRounded(Graphics g, Rectangle r, int radius, Color fill)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (SolidBrush b = new SolidBrush(fill))
                g.FillPath(b, p);
        }

        public static void StrokeRounded(Graphics g, Rectangle r, int radius, Color stroke, float width)
        {
            RectangleF rr = new RectangleF(r.X + width / 2f, r.Y + width / 2f, r.Width - width, r.Height - width);
            using (GraphicsPath p = RoundRect(Rectangle.Round(rr), radius))
            using (Pen pen = new Pen(stroke, width))
                g.DrawPath(pen, p);
        }

        /// <summary>
        /// 单行文字（超出宽度自动加省略号）。
        /// 用 GDI+ DrawString 而非 TextRenderer：后者在带 alpha 的位图上走 ClearType，
        /// 中文会出现彩色描边。
        ///
        /// 注意：StringFormatFlags.LineLimit 在「文字行高 &gt; 框高」时会把整行直接丢掉
        /// （不是裁剪），所以这里先量一次，放不下就临时缩小字号，保证文字永远可见。
        /// </summary>
        public static void DrawTextEllipsis(Graphics g, string text, Font font, Color color, Rectangle bounds,
            TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(text) || font == null) return;

            StringAlignment ha = StringAlignment.Near;
            StringAlignment va = StringAlignment.Near;
            if ((flags & TextFormatFlags.HorizontalCenter) != 0) ha = StringAlignment.Center;
            else if ((flags & TextFormatFlags.Right) != 0) ha = StringAlignment.Far;
            if ((flags & TextFormatFlags.VerticalCenter) != 0) va = StringAlignment.Center;
            else if ((flags & TextFormatFlags.Bottom) != 0) va = StringAlignment.Far;

            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = ha;
                sf.LineAlignment = va;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.LineLimit;

                using (SolidBrush b = new SolidBrush(color))
                {
                    float need = g.MeasureString(text, font, new PointF(0, 0), sf).Height;
                    if (need > bounds.Height && bounds.Height > 4)
                    {
                        // 框太矮：按比例缩小字号再画，否则 LineLimit 会整行丢弃
                        float ratio = (float)(bounds.Height - 1) / need;
                        if (ratio > 0.3f && ratio < 1f)
                        {
                            using (Font small = new Font(font.FontFamily, font.Size * ratio, font.Style))
                            {
                                g.DrawString(text, small, b, bounds, sf);
                            }
                            return;
                        }
                    }
                    g.DrawString(text, font, b, bounds, sf);
                }
            }
        }
    }
}
