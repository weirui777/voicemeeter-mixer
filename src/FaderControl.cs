using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VMixer
{
    /// <summary>横向音量条的绘制与坐标换算（静态工具，不占用子窗口）。</summary>
    public static class FaderControl
    {
        public const double MinDb = -60.0;
        public const double MaxDb = 12.0;

        public static readonly Color Accent = Color.FromArgb(0, 120, 212);
        public static readonly Color AccentSoft = Color.FromArgb(42, 139, 224);
        public static readonly Color TrackColor = Color.FromArgb(146, 148, 156);
        public static readonly Color MutedColor = Color.FromArgb(170, 170, 178);

        private const int KnobRadius = 7;

        /// <summary>把 dB 数值格式化成显示文本。</summary>
        public static string FormatDb(double db)
        {
            return db.ToString("0.0") + " dB";
        }

        /// <summary>音量条有效轨道区域（横向）。</summary>
        public static Rectangle TrackArea(Rectangle area)
        {
            int pad = KnobRadius + 2;
            int h = 4;
            int w = Math.Max(20, area.Width - pad * 2);
            return new Rectangle(area.X + pad, area.Y + (area.Height - h) / 2, w, h);
        }

        /// <summary>由鼠标横坐标算出 dB（左边最小 -60，右边最大 +12，跟 Win10 一致）。</summary>
        public static double ValueFromPoint(Rectangle area, int x)
        {
            Rectangle t = TrackArea(area);
            double p = (x - t.X) / (double)t.Width;
            if (p < 0) p = 0;
            if (p > 1) p = 1;
            return MinDb + p * (MaxDb - MinDb);
        }

        /// <summary>由 dB 算出旋钮中心的横坐标（右边更响）。</summary>
        public static int XFromValue(Rectangle area, double db)
        {
            Rectangle t = TrackArea(area);
            double p = (db - MinDb) / (MaxDb - MinDb);
            if (p < 0) p = 0;
            if (p > 1) p = 1;
            return t.X + (int)Math.Round(p * t.Width);
        }

        /// <summary>Win10 音量合成器风格的横向滑条：细横条 + 小圆钮。</summary>
        public static void Draw(Graphics g, Rectangle area, double db, bool muted)
        {
            Theme th = Theme.Current;
            Rectangle t = TrackArea(area);
            float cy = area.Y + area.Height / 2f;
            float th2 = 4f;

            Color track = muted ? th.TileGlyphMuted : th.Track;
            Color fillStart = muted ? th.TileGlyphMuted : th.TrackFill;
            Color fillEnd = muted ? th.TileGlyphMuted : th.TrackFillEnd;
            Color knob = muted ? th.TileGlyphMuted : th.Knob;
            Color ring = muted ? th.TileGlyphMuted : th.KnobRing;

            // 轨道（横向跑道形：左端取左半圆、右端取右半圆）
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(t.X, cy - th2 / 2, th2, th2, 90, 180);
                path.AddArc(t.Right - th2, cy - th2 / 2, th2, th2, 270, 180);
                path.CloseFigure();
                using (SolidBrush b = new SolidBrush(track)) g.FillPath(b, path);
            }

            // 已填充部分（从左端到旋钮）
            int knobX = XFromValue(area, db);
            if (knobX > t.X)
            {
                RectangleF fill = new RectangleF(t.X, cy - th2 / 2, knobX - t.X, th2);
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddArc(fill.X, fill.Y, th2, th2, 90, 180);
                    path.AddArc(fill.Right - th2, fill.Y, th2, th2, 270, 180);
                    path.CloseFigure();
                    using (LinearGradientBrush lg = new LinearGradientBrush(fill, fillStart, fillEnd,
                        LinearGradientMode.Horizontal))
                        g.FillPath(lg, path);
                }
            }

            // 旋钮
            float r = KnobRadius;
            using (GraphicsPath shadow = new GraphicsPath())
            {
                shadow.AddEllipse(knobX - r - 1, cy - r - 1, (r + 1) * 2, (r + 1) * 2);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(38, 0, 0, 0))) g.FillPath(b, shadow);
            }
            using (GraphicsPath knobPath = new GraphicsPath())
            {
                knobPath.AddEllipse(knobX - r, cy - r, r * 2, r * 2);
                using (SolidBrush b = new SolidBrush(knob)) g.FillPath(b, knobPath);
                using (Pen p = new Pen(ring, 2f)) g.DrawPath(p, knobPath);
            }
            using (Pen p = new Pen(ring, 1.4f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, knobX, cy - 3, knobX, cy + 3);
            }
        }
    }
}
