using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VMixer
{
    /// <summary>简单的文本输入对话框。</summary>
    public class PromptDialog : Form
    {
        private TextBox _input;

        public static bool Show(string caption, string label, string initial, out string result)
        {
            using (PromptDialog dlg = new PromptDialog(caption, label, initial))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    result = dlg._input.Text;
                    return true;
                }
            }
            result = initial;
            return false;
        }

        public PromptDialog(string caption, string label, string initial)
        {
            Text = caption;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;
            Font = Ui.Font(9f, FontStyle.Regular);
            ClientSize = new Size(Ui.S(320), Ui.S(118));

            Label lbl = new Label();
            lbl.Text = label;
            lbl.Location = new Point(Ui.S(14), Ui.S(16));
            lbl.Size = new Size(Ui.S(290), Ui.S(18));
            lbl.ForeColor = Color.FromArgb(60, 60, 66);
            Controls.Add(lbl);

            _input = new TextBox();
            _input.Location = new Point(Ui.S(14), Ui.S(40));
            _input.Size = new Size(Ui.S(292), Ui.S(23));
            _input.Text = initial == null ? "" : initial;
            Controls.Add(_input);

            Button ok = new Button();
            ok.Text = "确定";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(Ui.S(150), Ui.S(74));
            ok.Size = new Size(Ui.S(74), Ui.S(27));
            ok.Click += OnOk;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(Ui.S(232), Ui.S(74));
            cancel.Size = new Size(Ui.S(74), Ui.S(27));
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void OnOk(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>通道图标预览（画在磁贴样式的小方块里）。</summary>
    public class IconPreview : Control
    {
        private GlyphKind _kind = GlyphKind.Speaker;
        private bool _muted;

        public IconPreview()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            // 注意：不能用 Color.Transparent——普通对话框不支持透明背景的子控件
            BackColor = SystemColors.Control;
        }

        public void SetIcon(GlyphKind kind, bool muted)
        {
            _kind = kind;
            _muted = muted;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(0, 0, 0, 0));
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            ChannelRender.DrawIconTile(g, r, _kind, false, _muted, false);
        }
    }

    /// <summary>
    /// 通道设置：勾选要显示的通道、改名、选图标、选托盘提示显示哪条通道、拖动排序。
    ///
    /// 布局是自适应的：列表铺满窗口，底部按功能分成三块
    /// （通道设置 / 提示与声音 / 操作），窗口可以随意拉伸，控件会跟着走
    /// （DPI 缩放由 Ui.S 统一处理）。
    /// </summary>
    public class ChannelPicker : Form
    {
        private class Row
        {
            public string Key;
            public VmChannel Channel;
            public bool Checked;
            public string IconText;
            public string CustomName;
            public string Type;
            public float Gain;
        }

        private VmApi _vm;
        private VmConfig _cfg;
        private List<VmChannel> _all;

        private DragListView _list;
        private Label _sortHint;         // 列表上方的拖动排序提示
        private Panel _dragMark;          // 拖动时的插入指示线
        private IconPreview _preview;
        private Label _iconLabel;
        private ComboBox _iconCombo;
        private Button _clearIcon;
        private Label _nameLabel;
        private TextBox _nameBox;
        private Button _rename;
        private Label _tipLabel;
        private ComboBox _tipCombo;
        private Label _toneLabel;
        private ComboBox _toneCombo;
        private Button _toneTest;
        private CheckBox _soundCheck;
        private Button _defaultOrder;
        private Button _rescan;
        private Button _ok;
        private Button _cancel;

        private GroupBox _gChannel;
        private GroupBox _gHint;
        private GroupBox _gAction;

        private Dictionary<string, string> _icons = new Dictionary<string, string>();
        private Dictionary<string, string> _names = new Dictionary<string, string>();
        private string _tip = "";
        private int _toneStyle = 0;
        private int _bottomHeight;

        private bool _suppressName;
        private bool _suppressTip;

        // 当前列表显示顺序（通道 key 的先后）。拖动排序就是改这个，
        // 保存后主面板按它显示。
        private List<string> _rowOrder = new List<string>();

        public static bool Edit(VmApi vm, VmConfig cfg, string[] availableKeys)
        {
            using (ChannelPicker dlg = new ChannelPicker(vm, cfg, availableKeys))
            {
                if (dlg.ShowDialog() != DialogResult.OK) return false;
                cfg.Visible.Clear();
                cfg.Visible.AddRange(dlg.SelectedKeys());
                cfg.Names.Clear();
                foreach (KeyValuePair<string, string> kv in dlg._names) cfg.Names[kv.Key] = kv.Value;
                cfg.Icons.Clear();
                foreach (KeyValuePair<string, string> kv in dlg._icons) cfg.Icons[kv.Key] = kv.Value;
                cfg.TipChannel = dlg._tip;
                cfg.ToneStyle = dlg._toneStyle;
                cfg.SoundFeedback = dlg._soundCheck.Checked;
                cfg.Order.Clear();
                cfg.Order.AddRange(dlg.CurrentOrder());
                cfg.Save();
                return true;
            }
        }

        /// <summary>
        /// 自测：在设置窗口里模拟几次拖动排序，并按「点确定」的逻辑把顺序写回配置。
        /// 返回拖动前后的顺序文本，方便脚本核对。
        /// </summary>
        public static string RunDragSelfTest(VmApi vm, VmConfig cfg, string[] availableKeys)
        {
            string log = "";
            using (ChannelPicker dlg = new ChannelPicker(vm, cfg, availableKeys))
            {
                dlg.Show();
                dlg.Refresh();
                Application.DoEvents();
                log += "before : " + JoinKeys(dlg.CurrentOrder()) + "\r\n";

                dlg._list.SimulateDrag(1, 0);
                log += "drag 1->0: " + JoinKeys(dlg.CurrentOrder()) + "\r\n";

                dlg._list.SimulateDrag(0, dlg._list.Items.Count - 1);
                log += "drag 0->end: " + JoinKeys(dlg.CurrentOrder()) + "\r\n";

                // 按「确定」按钮的逻辑把顺序写回配置
                cfg.Order.Clear();
                cfg.Order.AddRange(dlg.CurrentOrder());
                cfg.Save();
                log += "saved  : " + JoinKeys(cfg.Order) + "\r\n";

                dlg.Close();
            }
            return log;
        }

        private static string JoinKeys(List<string> keys)
        {
            string[] arr = keys.ToArray();
            return string.Join(",", arr);
        }

        public ChannelPicker(VmApi vm, VmConfig cfg, string[] availableKeys)
        {
            _vm = vm;
            _cfg = cfg;
            _all = vm.Channels;

            Text = "通道设置";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;      // 手动按 DPI 缩放，避免二次放大
            Font = Ui.Font(9f, FontStyle.Regular);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ClientSize = new Size(Ui.S(720), Ui.S(600));
            MinimumSize = new Size(Ui.S(680), Ui.S(520));
            try { Icon = Glyphs.CreateGlyphIcon(GlyphKind.Wave, Color.FromArgb(0, 120, 212)); } catch { }

            _bottomHeight = Ui.S(198);

            // ---------------- 列表 ----------------
            // 列表上方的操作提示：拖动排序不是一眼可见的操作，给一行灰色小字说明
            _sortHint = new Label();
            _sortHint.Text = "提示：按住某一行上下拖动，可调整通道在主面板里的显示顺序";
            _sortHint.AutoSize = false;
            _sortHint.Font = Ui.Font(8f, FontStyle.Regular);
            _sortHint.ForeColor = SystemColors.GrayText;
            _sortHint.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(_sortHint);

            _list = new DragListView();
            _list.Columns.Add("通道", Ui.S(190));
            _list.Columns.Add("图标", Ui.S(96));
            _list.Columns.Add("自定义名称", Ui.S(170));
            _list.Columns.Add("类型", Ui.S(84));
            _list.Columns.Add("当前音量", Ui.S(80));
            _list.SelectedIndexChanged += OnSelectedChanged;
            _list.OrderChanged += delegate { SyncOrderFromList(); };
            Controls.Add(_list);

            // 拖动时的插入指示线（浮在列表上面）
            _dragMark = new Panel();
            _dragMark.BackColor = Color.FromArgb(0, 120, 212);
            _dragMark.Height = Ui.S(3);
            _dragMark.Visible = false;
            _dragMark.TabStop = false;
            Controls.Add(_dragMark);
            _list.Indicator = _dragMark;

            // ---------------- 分组 ----------------
            _gChannel = new GroupBox();
            _gChannel.Text = "通道设置";
            Controls.Add(_gChannel);

            _gHint = new GroupBox();
            _gHint.Text = "提示与声音";
            Controls.Add(_gHint);

            _gAction = new GroupBox();
            _gAction.Text = "操作";
            Controls.Add(_gAction);

            // ---------------- 通道设置组 ----------------
            _preview = new IconPreview();
            _gChannel.Controls.Add(_preview);

            _iconLabel = new Label();
            _iconLabel.Text = "图标：";
            _iconLabel.AutoSize = false;
            _iconLabel.Font = Font;
            _iconLabel.TextAlign = ContentAlignment.MiddleLeft;
            _gChannel.Controls.Add(_iconLabel);

            _iconCombo = new ComboBox();
            _iconCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _iconCombo.Items.Add("自动（按类型）");
            string[] bkeys = IconStore.BuiltinKeys;
            string[] bnames = IconStore.BuiltinNames;
            for (int i = 0; i < bkeys.Length; i++) _iconCombo.Items.Add(bnames[i]);
            _iconCombo.SelectedIndexChanged += OnIconComboChanged;
            _gChannel.Controls.Add(_iconCombo);

            _clearIcon = new Button();
            _clearIcon.Text = "清除";
            _clearIcon.Click += OnClearIcon;
            _gChannel.Controls.Add(_clearIcon);

            _nameLabel = new Label();
            _nameLabel.Text = "自定义名称：";
            _nameLabel.AutoSize = false;
            _nameLabel.Font = Font;
            _nameLabel.TextAlign = ContentAlignment.MiddleLeft;
            _gChannel.Controls.Add(_nameLabel);

            _nameBox = new TextBox();
            _nameBox.TextChanged += OnNameBoxChanged;
            _gChannel.Controls.Add(_nameBox);

            _rename = new Button();
            _rename.Text = "重命名";
            _rename.Click += OnApplyName;
            _gChannel.Controls.Add(_rename);

            // ---------------- 提示与声音组 ----------------
            _tipLabel = new Label();
            _tipLabel.Text = "托盘提示显示设备：";
            _tipLabel.AutoSize = false;
            _tipLabel.Font = Font;
            _tipLabel.TextAlign = ContentAlignment.MiddleLeft;
            _gHint.Controls.Add(_tipLabel);

            _tipCombo = new ComboBox();
            _tipCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _tipCombo.SelectedIndexChanged += OnTipChanged;
            _gHint.Controls.Add(_tipCombo);

            _toneLabel = new Label();
            _toneLabel.Text = "提示音设置：";
            _toneLabel.AutoSize = false;
            _toneLabel.Font = Font;
            _toneLabel.TextAlign = ContentAlignment.MiddleLeft;
            _gHint.Controls.Add(_toneLabel);

            _toneCombo = new ComboBox();
            _toneCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            for (int i = 0; i < SoundFx.StyleCount; i++) _toneCombo.Items.Add(SoundFx.StyleName(i));
            _toneCombo.SelectedIndexChanged += OnToneChanged;
            _gHint.Controls.Add(_toneCombo);

            _toneTest = new Button();
            _toneTest.Text = "试听";
            _toneTest.Click += OnToneTest;
            _gHint.Controls.Add(_toneTest);

            _soundCheck = new CheckBox();
            _soundCheck.Text = "调节音量时播放提示音";
            _soundCheck.Checked = true;
            _soundCheck.CheckedChanged += OnSoundCheckChanged;
            _gHint.Controls.Add(_soundCheck);

            // ---------------- 操作组 ----------------
            _defaultOrder = new Button();
            _defaultOrder.Text = "恢复默认排序";
            _defaultOrder.Click += OnDefaultOrder;
            _gAction.Controls.Add(_defaultOrder);

            _rescan = new Button();
            _rescan.Text = "重新扫描通道";
            _rescan.Click += OnRescan;
            _gAction.Controls.Add(_rescan);

            _ok = new Button();
            _ok.Text = "确定";
            _ok.DialogResult = DialogResult.OK;
            _ok.Click += OnOkClick;
            _gAction.Controls.Add(_ok);

            _cancel = new Button();
            _cancel.Text = "取消";
            _cancel.DialogResult = DialogResult.Cancel;
            _gAction.Controls.Add(_cancel);

            AcceptButton = _ok;
            CancelButton = _cancel;

            LoadInitialValues();
            BuildInitialOrder();
            RebuildList();
            RebuildTipCombo();
            Resize += delegate { LayoutControls(); };
            Shown += delegate { LayoutControls(); };
        }

        private void LoadInitialValues()
        {
            foreach (KeyValuePair<string, string> kv in _cfg.Names) _names[kv.Key] = kv.Value;
            foreach (KeyValuePair<string, string> kv in _cfg.Icons)
                if (IconStore.IsBuiltin(kv.Value)) _icons[kv.Key] = kv.Value;
            _tip = _cfg.TipChannel;
            _toneStyle = _cfg.ToneStyle;
            if (_toneStyle < 0 || _toneStyle >= SoundFx.StyleCount) _toneStyle = 0;
            _toneCombo.SelectedIndex = _toneStyle;
            _soundCheck.Checked = _cfg.SoundFeedback;
        }

        // ---------------- 顺序 ----------------

        /// <summary>
        /// 初始顺序：优先用配置里保存过的顺序，剩下的按默认顺序补在后面。
        /// </summary>
        private void BuildInitialOrder()
        {
            _rowOrder.Clear();
            if (_cfg.Order != null)
            {
                for (int i = 0; i < _cfg.Order.Count; i++)
                {
                    string k = _cfg.Order[i];
                    if (FindChannel(k) != null && !_rowOrder.Contains(k)) _rowOrder.Add(k);
                }
            }
            List<string> def = DefaultOrder();
            for (int i = 0; i < def.Count; i++)
                if (!_rowOrder.Contains(def[i])) _rowOrder.Add(def[i]);
        }

        /// <summary>
        /// 默认顺序：主音量在前，然后输入条、输出总线，各自按序号。
        /// </summary>
        private List<string> DefaultOrder()
        {
            List<VmChannel> tmp = new List<VmChannel>(_all);
            tmp.Sort(delegate (VmChannel a, VmChannel b)
            {
                if (a.IsMaster && !b.IsMaster) return -1;
                if (!a.IsMaster && b.IsMaster) return 1;
                if (a.Kind != b.Kind) return a.Kind == VmChannelKind.Strip ? -1 : 1;
                return a.Index.CompareTo(b.Index);
            });
            List<string> keys = new List<string>();
            for (int i = 0; i < tmp.Count; i++) keys.Add(tmp[i].Key);
            return keys;
        }

        /// <summary>拖动之后把列表里的顺序同步回 _rowOrder。</summary>
        private void SyncOrderFromList()
        {
            List<string> keys = CurrentOrder();
            if (keys.Count == 0) return;
            _rowOrder = keys;
        }

        private VmChannel FindChannel(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].Key == key) return _all[i];
            return null;
        }

        // ---------------- 列表 ----------------

        /// <summary>按当前顺序重建列表（保留勾选状态）。</summary>
        private void RebuildList()
        {
            List<Row> rows = new List<Row>();
            for (int i = 0; i < _all.Count; i++)
            {
                VmChannel c = _all[i];
                Row r = new Row();
                r.Key = c.Key;
                r.Channel = c;
                r.Checked = _cfg.Visible.Contains(c.Key) ||
                            (_cfg.Visible.Count == 0 && (c.IsMaster || (c.IsInput && c.Index <= 4)));
                r.IconText = IconDescription(c.Key);
                r.CustomName = _names.ContainsKey(c.Key) ? _names[c.Key] : "";
                r.Type = c.IsInput ? "输入" : "输出总线";
                r.Gain = _vm.GetGain(c);
                rows.Add(r);
            }

            // 按 _rowOrder 排；不在 _rowOrder 里的接在后面
            List<Row> ordered = new List<Row>();
            for (int i = 0; i < _rowOrder.Count; i++)
            {
                for (int k = 0; k < rows.Count; k++)
                    if (rows[k].Key == _rowOrder[i]) { ordered.Add(rows[k]); break; }
            }
            for (int k = 0; k < rows.Count; k++)
                if (!ordered.Contains(rows[k])) ordered.Add(rows[k]);
            rows = ordered;

            _list.BeginUpdate();
            _list.Items.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                ListViewItem item = new ListViewItem(r.Channel.DefaultName);
                item.SubItems.Add(r.IconText);
                item.SubItems.Add(r.CustomName);
                item.SubItems.Add(r.Type);
                item.SubItems.Add(FaderControl.FormatDb(r.Gain));
                item.Tag = r.Key;
                item.Checked = r.Checked;
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            if (_list.Items.Count > 0 && _list.SelectedItems.Count == 0)
                _list.Items[0].Selected = true;
            LayoutControls();
        }

        private string IconDescription(string key)
        {
            string v;
            if (!_icons.TryGetValue(key, out v)) return "自动";
            if (IconStore.IsBuiltin(v)) return IconStore.BuiltinLabel(v.Substring(IconStore.PrefixBuiltin.Length));
            return "自动";
        }

        /// <summary>当前列表里通道的顺序（保存后主面板按这个顺序显示）。</summary>
        public List<string> CurrentOrder()
        {
            List<string> result = new List<string>();
            for (int i = 0; i < _list.Items.Count; i++)
                result.Add(_list.Items[i].Tag as string);
            return result;
        }

        public List<string> SelectedKeys()
        {
            List<string> result = new List<string>();
            for (int i = 0; i < _list.Items.Count; i++)
            {
                ListViewItem it = _list.Items[i];
                if (it.Checked) result.Add(it.Tag as string);
            }
            return result;
        }

        // ---------------- 托盘提示下拉框 ----------------

        private void RebuildTipCombo()
        {
            int sel = _tipCombo.SelectedIndex;
            _suppressTip = true;
            _tipCombo.Items.Clear();
            _tipCombo.Items.Add("不显示");
            for (int i = 0; i < _rowOrder.Count; i++)
            {
                VmChannel c = FindChannel(_rowOrder[i]);
                if (c == null) continue;
                string label = c.DefaultName;
                if (_names.ContainsKey(c.Key) && !string.IsNullOrEmpty(_names[c.Key])) label = _names[c.Key];
                _tipCombo.Items.Add(label + "  (" + c.Param + ")");
            }
            int idx = 0;
            if (!string.IsNullOrEmpty(_tip))
            {
                for (int i = 0; i < _rowOrder.Count; i++)
                    if (_rowOrder[i] == _tip) { idx = i + 1; break; }
            }
            if (sel >= 0 && sel < _tipCombo.Items.Count) idx = sel;
            _tipCombo.SelectedIndex = idx;
            _suppressTip = false;
        }

        private void OnTipChanged(object sender, EventArgs e)
        {
            if (_suppressTip) return;
            int idx = _tipCombo.SelectedIndex;
            if (idx <= 0) { _tip = ""; return; }
            if (idx - 1 < _rowOrder.Count) _tip = _rowOrder[idx - 1];
        }

        // ---------------- 提示音 ----------------

        private void OnToneChanged(object sender, EventArgs e)
        {
            int idx = _toneCombo.SelectedIndex;
            if (idx < 0) return;
            _toneStyle = idx;
        }

        private void OnToneTest(object sender, EventArgs e)
        {
            SoundFx.Play(-3.0, _toneStyle);   // 试听当前选中的音色
        }

        private void OnSoundCheckChanged(object sender, EventArgs e)
        {
            // 状态存在 _cfg 上，点确定时统一写入
        }

        // ---------------- 图标 / 名称 ----------------

        private VmChannel SelectedChannel
        {
            get
            {
                if (_list.SelectedItems.Count == 0) return null;
                string key = _list.SelectedItems[0].Tag as string;
                return FindChannel(key);
            }
        }

        private string SelectedKey
        {
            get
            {
                if (_list.SelectedItems.Count == 0) return null;
                return _list.SelectedItems[0].Tag as string;
            }
        }

        private void OnSelectedChanged(object sender, EventArgs e)
        {
            VmChannel c = SelectedChannel;
            if (c == null) return;
            _suppressName = true;
            _nameBox.Text = _names.ContainsKey(c.Key) ? _names[c.Key] : c.DisplayName;
            _suppressName = false;
            SyncIconCombo(c.Key);
            UpdatePreview();
        }

        private void SyncIconCombo(string key)
        {
            if (key == null) return;
            string v;
            if (!_icons.TryGetValue(key, out v) || v.Length == 0)
            {
                _iconCombo.SelectedIndex = 0;
                return;
            }
            if (IconStore.IsBuiltin(v))
            {
                string bk = v.Substring(IconStore.PrefixBuiltin.Length);
                string[] keys = IconStore.BuiltinKeys;
                for (int i = 0; i < keys.Length; i++)
                {
                    if (keys[i] == bk) { _iconCombo.SelectedIndex = i + 1; return; }
                }
            }
            _iconCombo.SelectedIndex = 0;
        }

        private void UpdatePreview()
        {
            VmChannel c = SelectedChannel;
            if (c == null) { _preview.Invalidate(); return; }

            GlyphKind kind = IconStore.DefaultKind(c);
            string v;
            if (_icons.TryGetValue(c.Key, out v))
            {
                GlyphKind k;
                if (IconStore.TryGetBuiltinGlyph(v, out k)) kind = k;
            }
            bool muted;
            _vm.GetMute(c, out muted);
            _preview.SetIcon(kind, muted);
        }

        private void OnIconComboChanged(object sender, EventArgs e)
        {
            string key = SelectedKey;
            if (key == null) return;
            int idx = _iconCombo.SelectedIndex;
            if (idx <= 0) { _icons.Remove(key); RefreshRow(key); UpdatePreview(); return; }

            string bk = IconStore.BuiltinKeys[idx - 1];
            _icons[key] = IconStore.PrefixBuiltin + bk;
            RefreshRow(key);
            UpdatePreview();
        }

        private void OnClearIcon(object sender, EventArgs e)
        {
            string key = SelectedKey;
            if (key == null) return;
            _icons.Remove(key);
            _iconCombo.SelectedIndex = 0;
            RefreshRow(key);
            UpdatePreview();
        }

        private void OnNameBoxChanged(object sender, EventArgs e)
        {
            if (_suppressName) return;
            string key = SelectedKey;
            if (key == null) return;
            string t = _nameBox.Text.Trim();
            if (t.Length == 0) _names.Remove(key);
            else _names[key] = t;
            RefreshRow(key);
            RebuildTipCombo();     // 重命名后托盘提示下拉框里的名字要跟着变
        }

        private void OnApplyName(object sender, EventArgs e)
        {
            string key = SelectedKey;
            if (key == null) return;
            string t = _nameBox.Text.Trim();
            if (t.Length == 0) _names.Remove(key);
            else _names[key] = t;
            RefreshRow(key);
            RebuildTipCombo();
        }

        private void RefreshRow(string key)
        {
            for (int i = 0; i < _list.Items.Count; i++)
            {
                ListViewItem it = _list.Items[i];
                if ((it.Tag as string) != key) continue;
                it.SubItems[1].Text = IconDescription(key);
                it.SubItems[2].Text = _names.ContainsKey(key) ? _names[key] : "";
                return;
            }
        }

        private void OnRescan(object sender, EventArgs e)
        {
            _rescan.Enabled = false;
            _rescan.Text = "扫描中…";
            Application.DoEvents();
            try
            {
                if (_vm.Rescan())
                {
                    _all = _vm.Channels;
                    RebuildList();
                    RebuildTipCombo();
                }
            }
            catch { }
            _rescan.Text = "重新扫描";
            _rescan.Enabled = true;
        }

        private void OnDefaultOrder(object sender, EventArgs e)
        {
            _rowOrder = DefaultOrder();
            RebuildList();
            RebuildTipCombo();
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        // ---------------- 布局 ----------------

        /// <summary>
        /// 自适应布局：列表铺满，底部三块按功能分区
        /// （左：通道设置 / 右：提示与声音 / 下：操作）。
        /// </summary>
        private void LayoutControls()
        {
            int pad = Ui.S(10);
            int gap = Ui.S(8);
            int bottomTop = ClientSize.Height - _bottomHeight;

            // 提示行 + 列表
            int hintH = Ui.S(18);
            int listW = Math.Max(Ui.S(240), ClientSize.Width - pad * 2);
            _sortHint.SetBounds(pad, pad, listW, hintH);
            int listH = Math.Max(Ui.S(120), bottomTop - pad - hintH - Ui.S(4));
            _list.SetBounds(pad, pad + hintH, listW, listH);

            int totalW = ClientSize.Width - pad * 2;
            int row1H = Ui.S(112);
            // 右侧「提示与声音」这组的标签比较长（托盘提示显示设备：），给它保底宽度
            int rightW = (int)((totalW - gap) * 0.46);
            if (rightW < Ui.S(300)) rightW = Ui.S(300);
            int maxRight = totalW - gap - Ui.S(240);
            if (rightW > maxRight) rightW = maxRight;
            int leftW = totalW - gap - rightW;

            _gChannel.SetBounds(pad, bottomTop, leftW, row1H);
            _gHint.SetBounds(pad + leftW + gap, bottomTop, rightW, row1H);

            int row2Y = bottomTop + row1H + gap;
            int row2H = Math.Max(Ui.S(46), ClientSize.Height - pad - row2Y);
            _gAction.SetBounds(pad, row2Y, totalW, row2H);

            LayoutListColumns();
            LayoutChannelGroup();
            LayoutHintGroup();
            LayoutActionGroup();
        }

        /// <summary>列表列宽跟着窗口宽度走：通道列吃掉剩余空间，其余固定。</summary>
        private void LayoutListColumns()
        {
            if (_list.Columns.Count < 5) return;
            int fixedW = Ui.S(96) + Ui.S(170) + Ui.S(84) + Ui.S(80);
            int first = _list.Width - fixedW - Ui.S(24);
            if (first < Ui.S(120)) first = Ui.S(120);
            _list.Columns[0].Width = first;
            _list.Columns[1].Width = Ui.S(96);
            _list.Columns[2].Width = Ui.S(170);
            _list.Columns[3].Width = Ui.S(84);
            _list.Columns[4].Width = Ui.S(80);
        }

        /// <summary>把控件摆到 GroupBox 内部（坐标相对分组左上角）。</summary>
        private static void Put(GroupBox g, Control c, int x, int y, int w, int h)
        {
            c.SetBounds(x, y, w, h);
        }

        private void LayoutChannelGroup()
        {
            int cx = Ui.S(12);
            int row1 = Ui.S(24);
            int row2 = Ui.S(58);
            int ctrlH = Ui.S(24);
            int right = _gChannel.Width - Ui.S(12);

            // 第一行：图标：  [预览图] [下拉框] [清除]
            int iconLabelW = FitLabel(_iconLabel, Ui.S(6));
            Put(_gChannel, _iconLabel, cx, row1, iconLabelW, ctrlH);
            int px = cx + iconLabelW + Ui.S(4);
            Put(_gChannel, _preview, px, row1 - Ui.S(1), Ui.S(26), Ui.S(26));
            int comboX = px + Ui.S(30);
            Put(_gChannel, _iconCombo, comboX, row1, Ui.S(110), ctrlH);
            Put(_gChannel, _clearIcon, comboX + Ui.S(116), row1, Ui.S(52), ctrlH);

            // 第二行：自定义名称： [输入框] [重命名]
            int nameLabelW = FitLabel(_nameLabel, Ui.S(6));
            Put(_gChannel, _nameLabel, cx, row2, nameLabelW, ctrlH);
            int renameW = FitButton(_rename, Ui.S(64));
            Put(_gChannel, _rename, right - renameW, row2, renameW, ctrlH);
            int boxX = cx + nameLabelW + Ui.S(4);
            int boxW = right - renameW - Ui.S(10) - boxX;
            if (boxW < Ui.S(90)) boxW = Ui.S(90);
            Put(_gChannel, _nameBox, boxX, row2 + Ui.S(1), boxW, ctrlH);
        }

        private void LayoutHintGroup()
        {
            int cx = Ui.S(12);
            int row1 = Ui.S(24);
            int row2 = Ui.S(52);
            int row3 = Ui.S(78);
            int ctrlH = Ui.S(24);
            int right = _gHint.Width - Ui.S(12);

            // 第一行：托盘提示显示设备： [下拉框]
            int tipLabelW = FitLabel(_tipLabel, Ui.S(6));
            Put(_gHint, _tipLabel, cx, row1, tipLabelW, ctrlH);
            int tipX = cx + tipLabelW + Ui.S(4);
            int tipW = right - tipX;
            if (tipW < Ui.S(110)) tipW = Ui.S(110);
            Put(_gHint, _tipCombo, tipX, row1, tipW, ctrlH);

            // 第二行：提示音设置： [下拉框] [试听]
            int toneLabelW = FitLabel(_toneLabel, Ui.S(6));
            Put(_gHint, _toneLabel, cx, row2, toneLabelW, ctrlH);
            int toneX = cx + toneLabelW + Ui.S(4);
            Put(_gHint, _toneCombo, toneX, row2, Ui.S(96), ctrlH);
            Put(_gHint, _toneTest, toneX + Ui.S(102), row2, Ui.S(52), ctrlH);

            Put(_gHint, _soundCheck, cx, row3, Ui.S(190), Ui.S(20));
        }

        private void LayoutActionGroup()
        {
            int cx = Ui.S(12);
            int y = Ui.S(22);
            int ctrlH = Ui.S(26);
            int w = _gAction.Width;

            // 按钮宽度按文字实测，避免出现「少一个字」的截断
            int wDefault = FitButton(_defaultOrder, Ui.S(88));
            int wRescan = FitButton(_rescan, Ui.S(80));
            Put(_gAction, _defaultOrder, cx, y, wDefault, ctrlH);
            Put(_gAction, _rescan, cx + wDefault + Ui.S(10), y, wRescan, ctrlH);

            int wCancel = FitButton(_cancel, Ui.S(64));
            int wOk = FitButton(_ok, Ui.S(64));
            int right = w - Ui.S(12);
            Put(_gAction, _cancel, right - wCancel, y, wCancel, ctrlH);
            Put(_gAction, _ok, right - wCancel - Ui.S(8) - wOk, y, wOk, ctrlH);
        }

        /// <summary>按按钮文字实测宽度（含边框和内边距），保证文字完整显示。</summary>
        private static int FitButton(Button b, int minWidth)
        {
            int w = minWidth;
            try
            {
                Size sz = TextRenderer.MeasureText(b.Text, b.Font,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                w = sz.Width + Ui.S(18);
            }
            catch { }
            if (w < minWidth) w = minWidth;
            return w;
        }

        /// <summary>
        /// 按实际字体实测标签宽度，保证文字完整显示（不然会被裁掉后半截，
        /// 例如「图标：」只剩一个「图」）。
        /// </summary>
        private static int FitLabel(Label l, int extra)
        {
            int w = Ui.S(24);
            try
            {
                Size sz = TextRenderer.MeasureText(l.Text, l.Font,
                    new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                w = sz.Width + extra;
            }
            catch { }
            if (w < Ui.S(24)) w = Ui.S(24);
            l.Width = w;
            return w;
        }
    }
}
