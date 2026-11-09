using System;
using System.Drawing;
using Microsoft.Win32;

namespace VMixer
{
    /// <summary>
    /// 界面配色：跟随 Windows 的深色 / 浅色主题（AppsUseLightTheme）。
    /// </summary>
    public class Theme
    {
        public bool Dark;

        // 卡片
        public Color Overlay;         // 盖在模糊背景上的蒙版
        public byte OverlayAlpha;
        public Color SheenTop;        // 顶部高光
        public byte SheenAlpha;
        public Color SheenBottom;
        public byte SheenBottomAlpha;
        public Color Border;
        public Color BorderInner;

        // 文字
        public Color Title;
        public Color Text;
        public Color SubText;
        public Color Status;
        public Color Accent;

        // 结构线
        public Color Separator;

        // 磁贴
        public Color Tile;
        public Color TileBorder;
        public Color TileGlyph;
        public Color TileGlyphMuted;

        // 音量条
        public Color Track;
        public Color TrackFill;
        public Color TrackFillEnd;
        public Color Knob;
        public Color KnobRing;

        // 交互
        public Color HoverBg;
        public Color ButtonBg;
        public Color ButtonBgHover;
        public Color ButtonText;
        public Color ButtonTextAccent;

        // 电平条
        public Color LevelIdle;

        // 托盘图标（Win10 扁平音量图标风格）
        public Color TraySpeaker;
        public Color TrayWave;
        public Color TrayMark;

        public static Theme Current = new Theme();

        public static void Refresh()
        {
            bool dark = true;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("AppsUseLightTheme");
                        if (v != null) dark = (Convert.ToInt32(v) == 0);
                    }
                }
            }
            catch { }
            Current = dark ? new Theme() : CreateLight();
            Current.Dark = dark;
        }

        public Theme()
        {
            Dark = true;

            Overlay = Color.FromArgb(190, 32, 32, 36);
            OverlayAlpha = 190;
            SheenTop = Color.FromArgb(38, 255, 255, 255);
            SheenAlpha = 38;
            SheenBottom = Color.FromArgb(18, 0, 0, 0);
            SheenBottomAlpha = 18;
            Border = Color.FromArgb(70, 255, 255, 255);
            BorderInner = Color.FromArgb(40, 0, 0, 0);

            Title = Color.FromArgb(255, 255, 255, 255);
            Text = Color.FromArgb(238, 240, 240, 245);
            SubText = Color.FromArgb(200, 202, 204, 214);
            Status = Color.FromArgb(160, 162, 166, 176);
            Accent = Color.FromArgb(255, 96, 186, 255);

            Separator = Color.FromArgb(46, 255, 255, 255);

            Tile = Color.FromArgb(255, 58, 58, 62);
            TileBorder = Color.FromArgb(255, 74, 74, 78);
            TileGlyph = Color.FromArgb(255, 245, 245, 248);
            TileGlyphMuted = Color.FromArgb(255, 138, 140, 148);

            Track = Color.FromArgb(255, 88, 88, 96);
            TrackFill = Color.FromArgb(255, 96, 186, 255);
            TrackFillEnd = Color.FromArgb(255, 40, 140, 235);
            Knob = Color.FromArgb(255, 236, 236, 240);
            KnobRing = Color.FromArgb(255, 96, 186, 255);

            HoverBg = Color.FromArgb(38, 255, 255, 255);
            ButtonBg = Color.FromArgb(0, 0, 0, 0);
            ButtonBgHover = Color.FromArgb(46, 255, 255, 255);
            ButtonText = Color.FromArgb(235, 238, 238, 242);
            ButtonTextAccent = Color.FromArgb(255, 96, 186, 255);

            LevelIdle = Color.FromArgb(70, 255, 255, 255);

            TraySpeaker = Color.FromArgb(255, 255, 255, 255);
            TrayWave = Color.FromArgb(255, 200, 200, 205);
            TrayMark = Color.FromArgb(255, 214, 214, 220);
        }

        private static Theme CreateLight()
        {
            Theme t = new Theme();
            t.Dark = false;

            t.Overlay = Color.FromArgb(156, 249, 250, 253);
            t.OverlayAlpha = 156;
            t.SheenTop = Color.FromArgb(66, 255, 255, 255);
            t.SheenAlpha = 66;
            t.SheenBottom = Color.FromArgb(40, 206, 216, 238);
            t.SheenBottomAlpha = 40;
            t.Border = Color.FromArgb(120, 210, 210, 220);
            t.BorderInner = Color.FromArgb(0, 0, 0, 0);

            t.Title = Color.FromArgb(255, 28, 28, 32);
            t.Text = Color.FromArgb(255, 52, 52, 58);
            t.SubText = Color.FromArgb(255, 105, 105, 113);
            t.Status = Color.FromArgb(255, 112, 112, 120);
            t.Accent = Color.FromArgb(255, 0, 120, 212);

            t.Separator = Color.FromArgb(46, 225, 225, 232);

            t.Tile = Color.FromArgb(255, 255, 255, 255);
            t.TileBorder = Color.FromArgb(255, 226, 226, 232);
            t.TileGlyph = Color.FromArgb(255, 96, 98, 106);
            t.TileGlyphMuted = Color.FromArgb(255, 168, 170, 178);

            t.Track = Color.FromArgb(255, 194, 195, 202);
            t.TrackFill = Color.FromArgb(255, 0, 120, 212);
            t.TrackFillEnd = Color.FromArgb(255, 42, 139, 224);
            t.Knob = Color.FromArgb(255, 255, 255, 255);
            t.KnobRing = Color.FromArgb(255, 0, 120, 212);

            t.HoverBg = Color.FromArgb(64, 255, 255, 255);
            t.ButtonBg = Color.FromArgb(0, 0, 0, 0);
            t.ButtonBgHover = Color.FromArgb(60, 0, 0, 0);
            t.ButtonText = Color.FromArgb(255, 64, 64, 72);
            t.ButtonTextAccent = Color.FromArgb(255, 0, 120, 212);

            t.LevelIdle = Color.FromArgb(46, 120, 124, 134);

            t.TraySpeaker = Color.FromArgb(255, 43, 43, 47);
            t.TrayWave = Color.FromArgb(255, 122, 122, 128);
            t.TrayMark = Color.FromArgb(255, 92, 92, 98);
            return t;
        }
    }
}
