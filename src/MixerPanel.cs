using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VMixer
{
    /// <summary>
    /// Win10/11 风格的悬浮混音卡片：圆角、半透明面板、柔和投影，
    /// 全部内容自绘（分层窗口），因此不受子窗口限制。
    /// </summary>
    public class MixerPanel : LayeredSurface
    {
        private VmApi _vm;
        private VmConfig _cfg;
        private readonly List<VmChannel> _channels = new List<VmChannel>();
        private readonly List<ChannelLayout> _layout = new List<ChannelLayout>();

        private string _statusText = "未连接";
        private string _statusSuffix = "";  // 跟在状态后面的补充提示（如「· 未运行」）
        private string _tipInfo = "";       // 与托盘气泡同款信息（通道名 + 音量）
        private bool _statusOk = true;
        private bool _transparent = true;
        private Bitmap _backdrop;

        private int _dragIndex = -1;
        private int _hoverIndex = -1;
        private int _hoverButton = -1;   // 0=设置 1=关闭 2=打开Voicemeeter 3=通道静音
        private string _lastRenderKey = "";

        private DateTime _lastTone = DateTime.MinValue;
        private double _wheelToneGain = 0;
        private System.Windows.Forms.Timer _wheelToneTimer;

        private Font _fontTitle;
        private Font _fontName;
        private Font _fontParam;
        private Font _fontValue;
        private Font _fontStatus;
        private Font _fontButton;

        public event EventHandler SettingsRequested;
        public event EventHandler OpenVoicemeeterRequested;

        /// <summary>弹出面板之前的前台窗口：指针不在面板上时把滚轮转还给它。</summary>
        public IntPtr PreviousFocus { get; set; }

        /// <summary>调试用：直接指定悬停的通道（离屏渲染时用来检查提示效果）。</summary>
        public void SetHoverIndex(int index)
        {
            _hoverIndex = index;
        }

        private class ChannelLayout
        {
            public VmChannel Channel;
            public Rectangle Tile;
            public Rectangle Name;
            public Rectangle Param;
            public Rectangle Slider;
            public Rectangle Value;
            public double Gain;
            public bool Muted;
        }

        public MixerPanel(VmApi vm, VmConfig cfg)
        {
            _vm = vm;
            _cfg = cfg;
            _transparent = !cfg.OpaquePanel;

            _fontTitle = Ui.Font(10f, FontStyle.Bold);
            _fontName = Ui.Font(8.5f, FontStyle.Regular);
            _fontParam = Ui.Font(6.5f, FontStyle.Regular);
            _fontValue = Ui.Font(8.5f, FontStyle.Regular);
            _fontStatus = Ui.Font(8.5f, FontStyle.Regular);
            _fontButton = Ui.Font(8.5f, FontStyle.Regular);

            SurfaceMargin = Ui.S(16);
        }

        // ---------------- 布局 ----------------

        private int HeaderHeight { get { return Ui.S(44); } }
        private int FooterHeight { get { return Ui.S(38); } }
        private int ColumnWidth { get { return Ui.S(364); } }   // 面板内容宽度（竖排一列，每行一个通道）

        private int _cellX;
        private int _cellW;

        private Rectangle CardRect
        {
            get
            {
                int m = SurfaceMargin;
                return new Rectangle(m, m, Math.Max(10, Width - m * 2), Math.Max(10, Height - m * 2));
            }
        }

        private Rectangle SettingsButton
        {
            get
            {
                Rectangle c = CardRect;
                Rectangle close = CloseButton;
                int w = Ui.S(44);
                return new Rectangle(close.X - Ui.S(8) - w, c.Y + Ui.S(10), w, Ui.S(24));
            }
        }

        /// <summary>
        /// 关闭按钮（叉号）：在卡片右边留出和标题左边一样的边距，
        /// 垂直方向和标题文字居中对齐，不再贴着圆角边缘。
        /// </summary>
        private Rectangle CloseButton
        {
            get
            {
                Rectangle c = CardRect;
                int w = Ui.S(24);
                int h = Ui.S(24);
                return new Rectangle(c.Right - Ui.S(10) - w, c.Y + (HeaderHeight - h) / 2, w, h);
            }
        }

        /// <summary>「打开 Voicemeeter」按钮放在头部（面板变窄后底部放不下了）。</summary>
        private Rectangle OpenVmButton
        {
            get
            {
                Rectangle c = CardRect;
                Rectangle set = SettingsButton;
                int w = Ui.S(68);
                return new Rectangle(set.X - Ui.S(8) - w, c.Y + Ui.S(10), w, Ui.S(24));
            }
        }

        private void ComputeLayout()
        {
            _layout.Clear();
            Rectangle c = CardRect;
            int top = c.Y + HeaderHeight + Ui.S(6);

            _cellX = c.X + Ui.S(10);
            _cellW = ColumnWidth - Ui.S(20);

            for (int i = 0; i < _channels.Count; i++)
            {
                VmChannel ch = _channels[i];
                ChannelLayout cl = new ChannelLayout();
                cl.Channel = ch;

                // 每行一个通道：左侧图标磁贴 + 右侧横向滑条（同一行），
                // 下面一行是 名称 / Voicemeeter 对象 / 数值
                int tileSize = Ui.S(34);
                int sliderH = Ui.S(20);
                int gap = Ui.S(10);
                cl.Tile = new Rectangle(_cellX, top, tileSize, tileSize);
                int sliderX = _cellX + tileSize + gap;
                cl.Slider = new Rectangle(sliderX, top + (tileSize - sliderH) / 2,
                                          _cellX + _cellW - sliderX, sliderH);

                int lineY = cl.Tile.Bottom + Ui.S(2);
                int valueW = Ui.S(44);
                int paramW = Ui.S(50);
                cl.Value = new Rectangle(_cellX + _cellW - valueW, lineY, valueW, Ui.S(16));
                cl.Param = new Rectangle(cl.Value.X - paramW, lineY, paramW, Ui.S(16));
                cl.Name = new Rectangle(_cellX, lineY, cl.Param.X - _cellX - Ui.S(4), Ui.S(16));

                cl.Gain = _vm != null ? _vm.GetGain(ch) : 0f;
                bool muted = false;
                if (_vm != null) _vm.GetMute(ch, out muted);
                cl.Muted = muted;

                _layout.Add(cl);
                top = cl.Value.Bottom + Ui.S(6);
            }
        }

        private int ContentHeight
        {
            get
            {
                int cellH = Ui.S(34) + Ui.S(2) + Ui.S(16) + Ui.S(6);
                int n = _channels.Count > 0 ? _channels.Count : 1;
                return SurfaceMargin * 2 + HeaderHeight + Ui.S(6) + n * cellH + Ui.S(4) + FooterHeight;
            }
        }

        private int ContentWidth
        {
            get
            {
                return SurfaceMargin * 2 + ColumnWidth;
            }
        }

        // ---------------- 渲染 ----------------

        protected override void RenderSurface(Graphics g, int width, int height)
        {
            ComputeLayout();

            Rectangle card = CardRect;
            int radius = Ui.S(10);
            Theme th = Theme.Current;

            // 柔和投影
            LayeredSurface.DrawSoftShadow(g, card, radius, Ui.S(12), Ui.S(4), th.Dark ? (byte)30 : (byte)16);

            // 半透明卡片（亚克力观感）
            RectangleF grad = new RectangleF(card.X, card.Y, card.Width, card.Height);
            if (_transparent && _backdrop != null)
            {
                // 真实亚克力：背后画面抓下来做模糊，再叠一层主题蒙版
                using (GraphicsPath path = LayeredSurface.RoundRect(card, radius))
                {
                    GraphicsState st = g.Save();
                    g.SetClip(path);
                    g.DrawImage(_backdrop, card);
                    g.Restore(st);
                }
                using (GraphicsPath path = LayeredSurface.RoundRect(card, radius))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(th.OverlayAlpha, th.Overlay.R, th.Overlay.G, th.Overlay.B)))
                    g.FillPath(b, path);
                using (GraphicsPath path = LayeredSurface.RoundRect(card, radius))
                using (LinearGradientBrush lg = new LinearGradientBrush(grad,
                    Color.FromArgb(th.SheenAlpha, th.SheenTop.R, th.SheenTop.G, th.SheenTop.B),
                    Color.FromArgb(th.SheenBottomAlpha, th.SheenBottom.R, th.SheenBottom.G, th.SheenBottom.B),
                    LinearGradientMode.Vertical))
                    g.FillPath(lg, path);
            }
            else
            {
                // 没有抓屏背景时的兜底（不透明模式 / 离屏渲染）：颜色必须跟随主题，
                // 否则深色主题下会出现「浅底白字」看不清。
                byte alpha = _transparent ? (byte)232 : (byte)252;
                Color c1 = th.Dark ? Color.FromArgb(44, 44, 48) : Color.FromArgb(253, 253, 255);
                Color c2 = th.Dark ? Color.FromArgb(26, 26, 30) : Color.FromArgb(235, 236, 243);
                using (GraphicsPath path = LayeredSurface.RoundRect(card, radius))
                using (LinearGradientBrush lg = new LinearGradientBrush(grad,
                    Color.FromArgb(alpha, c1.R, c1.G, c1.B),
                    Color.FromArgb(alpha, c2.R, c2.G, c2.B), LinearGradientMode.Vertical))
                    g.FillPath(lg, path);

                if (_transparent)
                {
                    Color s1 = th.Dark ? Color.FromArgb(255, 255, 255) : Color.FromArgb(255, 255, 255);
                    Color s2 = th.Dark ? Color.FromArgb(190, 205, 235) : Color.FromArgb(190, 205, 235);
                    byte sa = th.Dark ? (byte)26 : (byte)46;
                    byte sb = th.Dark ? (byte)18 : (byte)30;
                    using (GraphicsPath path = LayeredSurface.RoundRect(card, radius))
                    using (LinearGradientBrush lg = new LinearGradientBrush(grad,
                        Color.FromArgb(sa, s1.R, s1.G, s1.B),
                        Color.FromArgb(sb, s2.R, s2.G, s2.B),
                        LinearGradientMode.ForwardDiagonal))
                        g.FillPath(lg, path);
                }
            }

            // 细边框
            LayeredSurface.StrokeRounded(g, card, radius, th.Border, 1f);

            DrawHeader(g, card);
            DrawChannels(g);
            DrawFooter(g, card);
        }

        private void DrawHeader(Graphics g, Rectangle card)
        {
            Theme th = Theme.Current;
            LayeredSurface.DrawTextEllipsis(g, "Voicemeeter 混音器", _fontTitle,
                th.Title,
                new Rectangle(card.X + Ui.S(16), card.Y,
                             OpenVmButton.X - card.X - Ui.S(16), HeaderHeight),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            ChannelRender.DrawFlatButton(g, OpenVmButton, "打开 VM", _fontButton, _hoverButton == 2, true);
            ChannelRender.DrawFlatButton(g, SettingsButton, "设置", _fontButton, _hoverButton == 0, false);

            // 关闭按钮（叉号）
            Rectangle close = CloseButton;
            if (_hoverButton == 1)
            {
                using (GraphicsPath p = LayeredSurface.RoundRect(close, 4))
                using (SolidBrush b = new SolidBrush(th.ButtonBgHover))
                    g.FillPath(b, p);
            }
            int arm = Ui.S(4);
            using (Pen pen = new Pen(th.Dark ? Color.FromArgb(215, 235, 235, 240) : Color.FromArgb(95, 95, 104), 1.6f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                int cx = close.X + close.Width / 2, cy = close.Y + close.Height / 2;
                g.DrawLine(pen, cx - arm, cy - arm, cx + arm, cy + arm);
                g.DrawLine(pen, cx + arm, cy - arm, cx - arm, cy + arm);
            }

            using (Pen pen = new Pen(th.Separator, 1f))
                g.DrawLine(pen, card.X + Ui.S(12), card.Y + HeaderHeight, card.Right - Ui.S(12), card.Y + HeaderHeight);
        }

        private void DrawChannels(Graphics g)
        {
            for (int i = 0; i < _layout.Count; i++)
            {
                ChannelLayout cl = _layout[i];

                // 悬停时给整行一个淡淡的高亮（Win10 磁贴感）
                if (_hoverIndex == i)
                {
                    Rectangle row = new Rectangle(_cellX - Ui.S(8), cl.Tile.Y - Ui.S(4),
                                                  _cellW + Ui.S(16), cl.Value.Bottom - cl.Tile.Y + Ui.S(8));
                    using (GraphicsPath p = LayeredSurface.RoundRect(row, Ui.S(8)))
                    using (SolidBrush b = new SolidBrush(Theme.Current.HoverBg))
                        g.FillPath(b, p);
                }

                // 图标：用户指定的内置字形优先，没有才按通道类型自动挑
                GlyphKind kind = IconStore.ResolveKind(_cfg, cl.Channel);

                ChannelRender.DrawIconTile(g, cl.Tile, kind, cl.Channel.IsMaster, cl.Muted, _hoverButton == 3);

                // 横向音量条
                FaderControl.Draw(g, cl.Slider, cl.Gain, cl.Muted);

                // 名称
                Theme th = Theme.Current;
                Color nameColor = cl.Muted ? th.TileGlyphMuted
                               : cl.Channel.IsMaster ? th.Accent
                               : th.Text;
                LayeredSurface.DrawTextEllipsis(g, cl.Channel.DisplayName, _fontName, nameColor,
                    cl.Name, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                // 悬停提示：只在通道名后面补一句「滚轮±1dB」，
                // 字号和右边的对象名（Strip[0] / Bus[0]）一样，不再重复通道名、对象和音量。
                if (_hoverIndex == i)
                {
                    float nameW = g.MeasureString(cl.Channel.DisplayName, _fontName).Width;
                    int hx = cl.Name.X + (int)nameW + Ui.S(8);
                    int hw = cl.Param.X - Ui.S(6) - hx;
                    if (hw > Ui.S(30))
                    {
                        LayeredSurface.DrawTextEllipsis(g, "滚轮±1dB", _fontParam, th.Status,
                            new Rectangle(hx, cl.Name.Y, hw, cl.Name.Height),
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    }
                }

                // 这一条通道实际控制的是 Voicemeeter 里的哪个对象（Bus[0] / Strip[3] …）
                LayeredSurface.DrawTextEllipsis(g, cl.Channel.Param, _fontParam, th.Status,
                    cl.Param, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                // dB 数值
                string txt = cl.Gain.ToString("0.0");
                Color valueColor = cl.Muted ? th.TileGlyphMuted : th.SubText;
                LayeredSurface.DrawTextEllipsis(g, txt, _fontValue, valueColor, cl.Value,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        /// <summary>
        /// 底部状态栏：左边是连接状态，右边是与托盘气泡完全一致的信息
        /// （托盘提示里指定的那条通道：名称 + 音量）。
        /// 悬停通道的详细信息放在通道自己那一行下面，不在这里挤。
        /// </summary>
        private void DrawFooter(Graphics g, Rectangle card)
        {
            Theme th = Theme.Current;
            int fy = card.Bottom - FooterHeight;
            using (Pen pen = new Pen(th.Separator, 1f))
                g.DrawLine(pen, card.X + Ui.S(12), fy, card.Right - Ui.S(12), fy);

            int left = card.X + Ui.S(16);
            int right = card.Right - Ui.S(16);
            int h = FooterHeight;

            // 右边：与托盘气泡完全一致的信息（托盘提示里指定的那条通道：名称 + 音量）
            string tip = _tipInfo;
            int tipW = 0;
            if (tip.Length > 0)
            {
                SizeF sz = g.MeasureString(tip, _fontStatus);
                tipW = (int)sz.Width + Ui.S(10);
                LayeredSurface.DrawTextEllipsis(g, tip, _fontStatus, th.SubText,
                    new Rectangle(right - tipW, fy, tipW, h),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            // 左边：连接状态。后面可以跟一句补充提示（例如 VM 没起来时的「· 未运行」），
            // 提示单独用警示色画，主体「已连接 xxx」保持正常颜色，不做成整行报错的样子。
            int leftW = right - left - tipW;
            if (leftW < Ui.S(60)) leftW = right - left;

            int suffixW = 0;
            if (_statusSuffix.Length > 0)
                suffixW = (int)g.MeasureString(_statusSuffix, _fontStatus).Width + Ui.S(4);

            int baseW = leftW - suffixW;
            if (baseW < Ui.S(40)) baseW = leftW;
            if (_statusText.Length > 0)
            {
                LayeredSurface.DrawTextEllipsis(g, _statusText, _fontStatus, th.Status,
                    new Rectangle(left, fy, baseW, h),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            if (_statusSuffix.Length > 0)
            {
                int sx = left + (int)g.MeasureString(_statusText, _fontStatus).Width + Ui.S(4);
                int sw = right - sx;
                if (sw > Ui.S(20))
                {
                    Color sc = _statusOk ? th.SubText : Color.FromArgb(255, 232, 110, 100);
                    LayeredSurface.DrawTextEllipsis(g, _statusSuffix, _fontStatus, sc,
                        new Rectangle(sx, fy, sw, h),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            }
        }

        // ---------------- 交互 ----------------

        private int ChannelAt(Point p)
        {
            for (int i = 0; i < _layout.Count; i++)
            {
                ChannelLayout cl = _layout[i];
                if (cl.Tile.Contains(p) || cl.Name.Contains(p) || cl.Param.Contains(p) ||
                    cl.Slider.Contains(p) || cl.Value.Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.Button == MouseButtons.Right)
            {
                ShowChannelMenu(e.Location);
                return;
            }

            if (SettingsButton.Contains(e.Location))
            {
                if (SettingsRequested != null) SettingsRequested(this, EventArgs.Empty);
                return;
            }
            if (CloseButton.Contains(e.Location))
            {
                Hide();
                return;
            }
            if (OpenVmButton.Contains(e.Location))
            {
                if (OpenVoicemeeterRequested != null) OpenVoicemeeterRequested(this, EventArgs.Empty);
                return;
            }

            int idx = ChannelAt(e.Location);
            if (idx < 0) return;

            ChannelLayout cl = _layout[idx];
            if (cl.Tile.Contains(e.Location))
            {
                // 点击图标磁贴 = 静音 / 取消静音
                ToggleMute(idx);
                return;
            }
            if (cl.Slider.Contains(e.Location) || cl.Value.Contains(e.Location))
            {
                _dragIndex = idx;
                SetChannelGain(idx, FaderControl.ValueFromPoint(cl.Slider, e.X), true);
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int idx = ChannelAt(e.Location);
            int btn = -1;
            if (SettingsButton.Contains(e.Location)) btn = 0;
            else if (CloseButton.Contains(e.Location)) btn = 1;
            else if (OpenVmButton.Contains(e.Location)) btn = 2;
            else if (idx >= 0 && _layout[idx].Tile.Contains(e.Location)) btn = 3;

            if (idx != _hoverIndex || btn != _hoverButton)
            {
                _hoverIndex = idx;
                _hoverButton = btn;
                RefreshSurface();
            }

            if (_dragIndex >= 0 && _dragIndex < _layout.Count)
            {
                SetChannelGain(_dragIndex, FaderControl.ValueFromPoint(_layout[_dragIndex].Slider, e.X), true);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragIndex = -1;
            Capture = false;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            int idx = ChannelAt(e.Location);
            if (idx >= 0 && _layout[idx].Slider.Contains(e.Location))
            {
                SetChannelGain(idx, 0.0, true);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            // WM_MOUSEWHEEL 的坐标在不同路径下可能是屏幕坐标也可能是客户区坐标，
            // 不可靠；直接取光标当前的屏幕位置最稳。
            Point screen = Cursor.Position;
            Point p = PointToClient(screen);
            int idx = ChannelAt(p);
            VmApi.LogWrite("WHEEL screen=" + screen + " delta=" + e.Delta +
                           " arg=" + e.Location + " local=" + p + " hit=" + idx);

            if (idx < 0)
            {
                // 指针不在面板上：把滚轮转交回弹出面板之前的那个窗口，
                // 免得面板开着的时候把别的程序的滚动吃掉
                if (!ClientRectangle.Contains(p) && PreviousFocus != IntPtr.Zero &&
                    PreviousFocus != Handle && Native.IsWindow(PreviousFocus))
                {
                    int lp = (screen.Y << 16) | (screen.X & 0xFFFF);
                    Native.PostMessage(PreviousFocus, Native.WM_MOUSEWHEEL,
                        (IntPtr)(e.Delta << 16), (IntPtr)lp);
                }
                return;
            }

            double step = e.Delta > 0 ? 1.0 : -1.0;
            double v = _layout[idx].Gain + step;
            if (v > FaderControl.MaxDb) v = FaderControl.MaxDb;
            if (v < FaderControl.MinDb) v = FaderControl.MinDb;
            SetChannelGain(idx, v, false);

            // 滚轮停下来之后再响一声（像 Win10 那样一格一格地听）
            _wheelToneGain = v;
            if (_wheelToneTimer == null)
            {
                _wheelToneTimer = new System.Windows.Forms.Timer();
                _wheelToneTimer.Interval = 280;
                _wheelToneTimer.Tick += delegate
                {
                    _wheelToneTimer.Stop();
                    PlayTone(_wheelToneGain, false);
                };
            }
            _wheelToneTimer.Stop();
            _wheelToneTimer.Start();
        }

        private void ShowChannelMenu(Point location)
        {
            int idx = ChannelAt(location);
            if (idx < 0) return;

            VmChannel ch = _layout[idx].Channel;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = Ui.Font(9f, FontStyle.Regular);
            menu.Items.Add("重命名…", null, delegate
            {
                string result;
                if (PromptDialog.Show("重命名通道", "通道名称", ch.DisplayName, out result))
                {
                    _cfg.Names[ch.Key] = result;
                    ch.CustomName = result;
                    _cfg.Save();
                    RefreshSurface();
                }
            });
            menu.Items.Add(_layout[idx].Muted ? "取消静音" : "静音", null, delegate { ToggleMute(idx); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("通道设置…", null, delegate
            {
                if (SettingsRequested != null) SettingsRequested(this, EventArgs.Empty);
            });
            menu.Show(this, location);
        }

        private void ToggleMute(int idx)
        {
            if (idx < 0 || idx >= _layout.Count) return;
            ChannelLayout cl = _layout[idx];
            bool muted = !cl.Muted;
            if (_vm != null) _vm.SetMute(cl.Channel, muted);
            cl.Muted = muted;
            RefreshSurface();
        }

        private void SetChannelGain(int idx, double db, bool tone)
        {
            if (idx < 0 || idx >= _layout.Count) return;
            if (db > FaderControl.MaxDb) db = FaderControl.MaxDb;
            if (db < FaderControl.MinDb) db = FaderControl.MinDb;
            ChannelLayout cl = _layout[idx];
            cl.Gain = db;
            if (_vm != null) _vm.SetGain(cl.Channel, (float)db);
            if (tone) PlayTone(db, true);
            RefreshSurface();
        }

        /// <summary>
        /// 调节音量时的提示音：音量按所调通道的增益缩放，
        /// 拖动时做节流（避免变成机关枪），滚轮则等停下来再响。
        /// </summary>
        private void PlayTone(double db, bool throttle)
        {
            if (_cfg != null && !_cfg.SoundFeedback) return;
            DateTime now = DateTime.Now;
            if (throttle && (now - _lastTone).TotalMilliseconds < 120) return;
            _lastTone = now;
            int style = (_cfg != null) ? _cfg.ToneStyle : 0;
            VmApi.LogWrite("TONE db=" + db.ToString("0.0") + " style=" + SoundFx.StyleName(style));
            SoundFx.Play(db, style);
        }

        // ---------------- 对外接口 ----------------

        public void Rebuild(VmApi vm, VmConfig cfg)
        {
            _vm = vm;
            _cfg = cfg;
            _channels.Clear();

            for (int i = 0; i < vm.Channels.Count; i++)
            {
                VmChannel c = vm.Channels[i];
                if (cfg.Visible.Count == 0)
                {
                    if (c.IsMaster || (c.IsInput && c.Index <= 4)) _channels.Add(c);
                }
                else if (cfg.Visible.Contains(c.Key)) _channels.Add(c);
            }

            // 自然顺序：主音量在前、输入条在前、按序号
            _channels.Sort(delegate (VmChannel a, VmChannel b)
            {
                if (a.IsMaster && !b.IsMaster) return -1;
                if (!a.IsMaster && b.IsMaster) return 1;
                if (a.Kind != b.Kind) return a.Kind == VmChannelKind.Strip ? -1 : 1;
                return a.Index.CompareTo(b.Index);
            });

            // 顺序：优先按设置里保存的列表顺序（点列头排序后的结果），
            // 列表里没有的通道接在后面，保持自然顺序
            List<VmChannel> ordered = new List<VmChannel>();
            if (_cfg != null && _cfg.Order.Count > 0)
            {
                for (int i = 0; i < _cfg.Order.Count; i++)
                {
                    for (int k = 0; k < _channels.Count; k++)
                    {
                        // 跳过重复项：Order 里同一个通道出现两次时只排一次
                        if (_channels[k].Key == _cfg.Order[i] && !ordered.Contains(_channels[k]))
                        {
                            ordered.Add(_channels[k]);
                            break;
                        }
                    }
                }
            }
            if (ordered.Count != _channels.Count)
            {
                for (int i = 0; i < _channels.Count; i++)
                    if (!ordered.Contains(_channels[i])) ordered.Add(_channels[i]);
            }
            _channels.Clear();
            _channels.AddRange(ordered);

            for (int i = 0; i < _channels.Count; i++)
            {
                VmChannel c = _channels[i];
                c.CustomName = cfg.Names.ContainsKey(c.Key) ? cfg.Names[c.Key] : null;
            }

            ResizeToContent();
        }

        private void ResizeToContent()
        {
            int w = ContentWidth;
            int h = ContentHeight;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            if (w > wa.Width - Ui.S(20)) w = wa.Width - Ui.S(20);
            if (h > wa.Height - Ui.S(20)) h = wa.Height - Ui.S(20);
            if (w != Width || h != Height)
            {
                Size = new Size(w, h);
            }
            PositionNearTray();
            RefreshSurface();
        }

        public void PositionNearTray()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int x = wa.Right - Width + Ui.S(8);
            int y = wa.Bottom - Height + Ui.S(4);
            if (x + Width > wa.Right) x = wa.Right - Width;
            if (x < wa.Left) x = wa.Left;
            if (y + Height > wa.Bottom) y = wa.Bottom - Height;
            if (y < wa.Top) y = wa.Top;
            Location = new Point(x, y);
        }

        public void RefreshValues()
        {
            if (_vm == null || !_vm.Connected) return;
            string key = BuildStateKey();
            if (key == _lastRenderKey) return;
            _lastRenderKey = key;
            RefreshSurface();
        }

        private string BuildStateKey()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _channels.Count; i++)
            {
                VmChannel c = _channels[i];
                sb.Append(_vm.GetGain(c).ToString("0.0")).Append(',');
                bool muted = false;
                _vm.GetMute(c, out muted);
                sb.Append(muted ? '1' : '0').Append(';');
            }
            sb.Append(_statusText).Append('|').Append(_tipInfo).Append('|')
              .Append(_transparent ? '1' : '0');
            return sb.ToString();
        }

        public void SetStatus(string text, string tipInfo, bool ok)
        {
            SetStatus(text, "", tipInfo, ok);
        }

        /// <summary>
        /// 设置底部状态栏。suffix 是紧跟在状态文字后面的补充提示（例如「· 未运行」），
        /// 用单独的警示色画，不会把整行都变成报错的样子。
        /// </summary>
        public void SetStatus(string text, string suffix, string tipInfo, bool ok)
        {
            _statusText = text == null ? "" : text;
            _statusSuffix = suffix == null ? "" : suffix;
            _tipInfo = tipInfo == null ? "" : tipInfo;
            _statusOk = ok;
        }

        public void SetTransparent(bool transparent)
        {
            _transparent = transparent;
            _cfg.OpaquePanel = !transparent;
            _cfg.Save();
            RefreshBackdrop();
            RefreshSurface();
        }

        /// <summary>
        /// 重新抓取背景：先把自己藏起来，截取将要显示区域的屏幕画面并做模糊，
        /// 这样卡片背后就是「毛玻璃」，与 Windows 10/11 的亚克力接近。
        /// </summary>
        public void RefreshBackdrop()
        {
            bool wasVisible = Visible;
            if (wasVisible) Hide();
            try { PrepareBackdrop(); }
            catch { DisposeBackdrop(); }
            if (wasVisible) { Show(); BringToFront(); }
        }

        private void DisposeBackdrop()
        {
            if (_backdrop != null)
            {
                _backdrop.Dispose();
                _backdrop = null;
            }
        }

        private void PrepareBackdrop()
        {
            DisposeBackdrop();
            if (!_transparent) return;

            Rectangle card = CardRect;
            int sw = card.Width, sh = card.Height;
            if (sw < 16 || sh < 16) return;

            int sx = Left + card.X;
            int sy = Top + card.Y;

            Rectangle bounds = Screen.FromPoint(new Point(sx + sw / 2, sy + sh / 2)).Bounds;
            int x = Math.Max(sx, bounds.Left);
            int y = Math.Max(sy, bounds.Top);
            int r = Math.Min(sx + sw, bounds.Right);
            int b = Math.Min(sy + sh, bounds.Bottom);
            if (r - x < 16 || b - y < 16) return;

            using (Bitmap raw = new Bitmap(r - x, b - y))
            {
                using (Graphics g = Graphics.FromImage(raw))
                    g.CopyFromScreen(x, y, 0, 0, new Size(r - x, b - y));
                _backdrop = BlurBitmap(raw);
            }
        }

        /// <summary>两级降采样 + 高质量插值回放大，得到接近高斯模糊的效果。</summary>
        private static Bitmap BlurBitmap(Bitmap src)
        {
            int w = src.Width, h = src.Height;
            int sw = Math.Max(1, w / 10), sh = Math.Max(1, h / 10);
            int mw = Math.Max(1, w / 3), mh = Math.Max(1, h / 3);

            using (Bitmap small = new Bitmap(sw, sh))
            {
                using (Graphics g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, new Rectangle(0, 0, sw, sh));
                }
                using (Bitmap mid = new Bitmap(mw, mh))
                {
                    using (Graphics g = Graphics.FromImage(mid))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(small, new Rectangle(0, 0, mw, mh));
                    }
                    Bitmap big = new Bitmap(w, h);
                    using (Graphics g = Graphics.FromImage(big))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(mid, new Rectangle(0, 0, w, h));
                    }
                    return big;
                }
            }
        }

        public bool Transparent { get { return _transparent; } }

        /// <summary>清掉悬浮高亮（重新弹出面板时用，避免残留上一次的高亮）。</summary>
        public void ClearHover()
        {
            if (_hoverIndex == -1 && _hoverButton == -1) return;
            _hoverIndex = -1;
            _hoverButton = -1;
            RefreshSurface();
        }

        /// <summary>通道数量（调试用）。</summary>
        public int ChannelCount { get { return _layout.Count; } }

        /// <summary>通道显示名（调试用）。</summary>
        public string ChannelName(int i)
        {
            if (i < 0 || i >= _layout.Count) return "";
            return _layout[i].Channel.DisplayName;
        }

        /// <summary>通道对应的 Voicemeeter 参数（调试用）。</summary>
        public string ChannelParam(int i)
        {
            if (i < 0 || i >= _layout.Count) return "";
            return _layout[i].Channel.Param;
        }

        /// <summary>
        /// 把面板内容离屏渲染成 PNG（调试用，不依赖屏幕）。
        /// </summary>
        public void RenderToPng(string path)
        {
            int w = Width, h = Height;
            using (Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(40, 40, 44));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                RenderSurface(g, w, h);
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        /// <summary>输出每个通道滑块的屏幕坐标（供自动化测试定位用）。</summary>
        public string DescribeLayout()
        {
            ComputeLayout();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("window|" + Left + "," + Top + "," + Width + "," + Height);
            for (int i = 0; i < _layout.Count; i++)
            {
                ChannelLayout cl = _layout[i];
                sb.AppendLine(string.Format("{0}|{1}|{2},{3},{4},{5}|{6},{7},{8},{9}|{10}",
                    i, cl.Channel.DisplayName,
                    Left + cl.Slider.X, Top + cl.Slider.Y, cl.Slider.Width, cl.Slider.Height,
                    Left + cl.Tile.X, Top + cl.Tile.Y, cl.Tile.Width, cl.Tile.Height,
                    cl.Gain.ToString("0.00")));
            }
            return sb.ToString();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ResizeToContent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) DisposeBackdrop();
            base.Dispose(disposing);
        }
    }
}
