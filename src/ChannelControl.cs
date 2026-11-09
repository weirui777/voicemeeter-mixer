using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VMixer
{
    /// <summary>通道图标、静音按钮、电平条等自绘元素。</summary>
    public static class ChannelRender
    {
        /// <summary>Win10/11 风格的图标方块（整块即静音开关）。</summary>
        public static void DrawIconTile(Graphics g, Rectangle tile, GlyphKind kind, bool isMaster, bool muted, bool hovered)
        {
            Theme th = Theme.Current;
            int radius = Ui.S(8);
            Color fill = muted ? Color.FromArgb(255, 62, 44, 46) : th.Tile;
            Color border = muted ? Color.FromArgb(255, 132, 62, 64)
                          : hovered ? th.TileBorder
                          : th.TileBorder;

            using (GraphicsPath p = LayeredSurface.RoundRect(tile, radius))
            using (SolidBrush b = new SolidBrush(fill))
                g.FillPath(b, p);
            using (GraphicsPath p = LayeredSurface.RoundRect(tile, radius))
            using (Pen pen = new Pen(border, 1f))
                g.DrawPath(pen, p);

            Color c = muted ? Color.FromArgb(255, 236, 106, 100)
                   : isMaster ? th.Accent
                   : th.TileGlyph;

            int size = (int)(tile.Width * 0.62f);
            if (size < 8) size = 8;
            Rectangle r = new Rectangle(tile.X + (tile.Width - size) / 2, tile.Y + (tile.Height - size) / 2, size, size);
            Glyphs.Draw(g, r, kind, c);

            if (muted) DrawMuteBadge(g, tile);
        }

        /// <summary>静音徽标（磁贴右下角的红点加斜杠）。</summary>
        private static void DrawMuteBadge(Graphics g, Rectangle tile)
        {
            int d = Ui.S(15);
            Rectangle r = new Rectangle(tile.Right - d + Ui.S(4), tile.Bottom - d + Ui.S(4), d, d);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(r);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(214, 48, 48)))
                    g.FillPath(b, p);
            }
            float w = Math.Max(1.4f, Ui.F(1.5f));
            using (Pen p = new Pen(Color.White, w))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, r.X + r.Width * 0.32f, r.Y + r.Height * 0.32f,
                              r.Right - r.Width * 0.32f, r.Bottom - r.Height * 0.32f);
            }
        }

        /// <summary>细电平条。</summary>

        /// <summary>扁平按钮（Win10 风格）。</summary>
        public static void DrawFlatButton(Graphics g, Rectangle r, string text, Font font, bool hovered, bool accent)
        {
            Theme th = Theme.Current;
            Color bg = hovered ? th.ButtonBgHover : th.ButtonBg;
            if (bg.A > 0)
            {
                using (GraphicsPath p = LayeredSurface.RoundRect(r, 4))
                using (SolidBrush b = new SolidBrush(bg))
                    g.FillPath(b, p);
            }

            Color fg = accent ? th.ButtonTextAccent : th.ButtonText;
            LayeredSurface.DrawTextEllipsis(g, text, font, fg, r,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
