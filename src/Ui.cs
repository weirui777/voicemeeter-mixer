using System;
using System.Drawing;
using System.Windows.Forms;

namespace VMixer
{
    /// <summary>界面缩放辅助（按系统 DPI）。</summary>
    public static class Ui
    {
        private static float _scale = 1f;

        static Ui()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    if (g.DpiX > 0) _scale = g.DpiX / 96f;
                }
            }
            catch { _scale = 1f; }
            if (_scale < 1f) _scale = 1f;
            if (_scale > 3f) _scale = 3f;
        }

        public static float Scale { get { return _scale; } }
        public static int S(int v) { return (int)Math.Round(v * _scale); }
        public static float F(float pt) { return pt * _scale; }
        public static Font Font(float pt, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", F(pt), style);
        }
    }
}
