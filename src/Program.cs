using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace VMixer
{
    internal static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const int WM_MOUSEWHEEL = 0x020A;
        public const int HWND_MESSAGE = -3;
        public const uint MOD_NOREPEAT = 0x4000;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_ALT = 0x0001;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int pid);
        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);
    }

    /// <summary>消息窗口，用于接收全局热键。</summary>
    internal class HotkeyWindow : NativeWindow
    {
        public event Action<int> HotkeyPressed;

        public HotkeyWindow()
        {
            CreateHandle(new CreateParams
            {
                Parent = new IntPtr(Native.HWND_MESSAGE),
                Style = 0,
                ExStyle = 0
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                if (HotkeyPressed != null) HotkeyPressed(m.WParam.ToInt32());
                return;
            }
            base.WndProc(ref m);
        }
    }

    internal class TrayContext : ApplicationContext
    {
        private VmApi _vm;
        private VmConfig _cfg;
        private NotifyIcon _tray;
        private Icon _trayIcon;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _miSound;
        private ToolStripMenuItem[] _styleItems;
        private MixerPanel _panel;
        private System.Windows.Forms.Timer _timer;
        private HotkeyWindow _hotkeys;
        private DateTime _lastConnectTry = DateTime.MinValue;
        private DateTime _panelShownAt = DateTime.MinValue;
        private bool _quitting;
        private string _vmName = "";      // 最近一次连上的 Voicemeeter 名字（断开后仍保留）
        private EventWaitHandle _showEvent;
        private volatile bool _showRequested;

        private const int HK_PANEL = 1;
        private const int HK_VOL_UP = 2;
        private const int HK_VOL_DOWN = 3;
        private const int HK_VOL_MUTE = 4;

        public TrayContext(bool showOnStart)
        {
            _cfg = new VmConfig();
            _cfg.Load();
            if (showOnStart) _cfg.ShowOnStart = true;

            Theme.Refresh();

            // 配置里开着开机自启就补写一次注册表（换了目录也能自愈）
            if (_cfg.AutoStart) SetAutoStart(true);

            _vm = new VmApi();

            BuildTrayIcon();
            BuildPanel();
            RegisterHotkeys();
            StartShowEventListener();

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 250;
            _timer.Tick += OnTick;
            _timer.Start();

            if (_cfg.ShowOnStart) ShowPanel();

            Application.ApplicationExit += delegate { Cleanup(); };
        }

        private void Cleanup()
        {
            try { _timer.Stop(); } catch { }
            try { _vm.Disconnect(); } catch { }
            try { if (_hotkeys != null && _hotkeys.Handle != IntPtr.Zero) _hotkeys.DestroyHandle(); } catch { }
            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
            try { if (_showEvent != null) _showEvent.Dispose(); } catch { }
        }

        /// <summary>
        /// 监听「重复启动」时第二个实例发来的显示面板事件。
        /// 事件线程只置标志，真正的 UI 操作交给 Forms.Timer 在 UI 线程执行。
        /// </summary>
        private void StartShowEventListener()
        {
            try
            {
                _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Global\\VMixer_ShowPanel");
            }
            catch { return; }

            ThreadPool.QueueUserWorkItem(delegate
            {
                while (true)
                {
                    try { if (!_showEvent.WaitOne()) return; }
                    catch { return; }
                    _showRequested = true;
                }
            });
        }

        private void BuildTrayIcon()
        {
            _trayIcon = Glyphs.CreateAppIcon(Theme.Current.TraySpeaker, Theme.Current.TrayWave, Theme.Current.TrayMark);
            _menu = new ContextMenuStrip();
            _menu.Font = Ui.Font(9f, FontStyle.Regular);

            ToolStripMenuItem miPanel = new ToolStripMenuItem("显示混音器");
            miPanel.Click += delegate { TogglePanel(); };
            _menu.Items.Add(miPanel);

            ToolStripMenuItem miSettings = new ToolStripMenuItem("通道设置…");
            miSettings.Click += delegate { OpenSettings(); };
            _menu.Items.Add(miSettings);

            ToolStripMenuItem miOpen = new ToolStripMenuItem("打开 Voicemeeter");
            miOpen.Click += delegate { OpenVoicemeeter(); };
            _menu.Items.Add(miOpen);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem miAuto = new ToolStripMenuItem("开机自动启动");
            miAuto.Checked = _cfg.AutoStart;
            miAuto.Click += delegate
            {
                _cfg.AutoStart = !_cfg.AutoStart;
                miAuto.Checked = _cfg.AutoStart;
                SetAutoStart(_cfg.AutoStart);
                _cfg.Save();
            };
            _menu.Items.Add(miAuto);

            ToolStripMenuItem miKeys = new ToolStripMenuItem("用系统音量键控制主音量");
            miKeys.Checked = _cfg.VolumeKeys;
            miKeys.Click += delegate
            {
                _cfg.VolumeKeys = !_cfg.VolumeKeys;
                miKeys.Checked = _cfg.VolumeKeys;
                RegisterHotkeys();
                _cfg.Save();
            };
            _menu.Items.Add(miKeys);

            ToolStripMenuItem miSound = new ToolStripMenuItem("调节音量时播放提示音");
            miSound.Checked = _cfg.SoundFeedback;
            miSound.Click += delegate
            {
                _cfg.SoundFeedback = !miSound.Checked;
                miSound.Checked = _cfg.SoundFeedback;
                _cfg.Save();
            };
            _menu.Items.Add(miSound);
            _miSound = miSound;

            // 提示音风格：选中即试听，方便一次挑到顺耳的那个
            ToolStripMenuItem miStyle = new ToolStripMenuItem("提示音风格");
            ToolStripMenuItem[] styleItems = new ToolStripMenuItem[SoundFx.StyleCount];
            for (int i = 0; i < SoundFx.StyleCount; i++)
            {
                int style = i;
                ToolStripMenuItem item = new ToolStripMenuItem(SoundFx.StyleName(style));
                item.Checked = (_cfg.ToneStyle == style);
                item.Click += delegate
                {
                    _cfg.ToneStyle = style;
                    for (int k = 0; k < styleItems.Length; k++)
                        if (styleItems[k] != null) styleItems[k].Checked = (k == style);
                    _cfg.Save();
                    SoundFx.Play(-3.0, style);   // 试听
                };
                styleItems[i] = item;
                miStyle.DropDownItems.Add(item);
            }
            _menu.Items.Add(miStyle);
            _styleItems = styleItems;

            ToolStripMenuItem miGlass = new ToolStripMenuItem("半透明亚克力面板");
            miGlass.Checked = !_cfg.OpaquePanel;
            miGlass.Click += delegate
            {
                bool next = !miGlass.Checked;
                miGlass.Checked = next;
                if (_panel != null) _panel.SetTransparent(next);
                else { _cfg.OpaquePanel = !next; _cfg.Save(); }
            };
            _menu.Items.Add(miGlass);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem miExit = new ToolStripMenuItem("退出");
            miExit.Click += delegate { ExitApp(); };
            _menu.Items.Add(miExit);

            _tray = new NotifyIcon();
            _tray.Icon = _trayIcon;
            _tray.Text = "Voicemeeter 混音器";
            _tray.ContextMenuStrip = _menu;
            _tray.MouseClick += OnTrayClick;
            _tray.Visible = true;
        }

        private void BuildPanel()
        {
            _panel = new MixerPanel(_vm, _cfg);
            _panel.SettingsRequested += delegate { OpenSettings(); };
            _panel.OpenVoicemeeterRequested += delegate { OpenVoicemeeter(); };
            _panel.FormClosed += delegate { _panel = null; };
        }

        /// <summary>
        /// 按 key 找通道（重新扫描后旧的 VmChannel 对象会失效，所以每次都重新找）。
        /// </summary>
        private VmChannel FindChannel(string key)
        {
            if (string.IsNullOrEmpty(key) || _vm == null) return null;
            for (int i = 0; i < _vm.Channels.Count; i++)
                if (_vm.Channels[i].Key == key) return _vm.Channels[i];
            return null;
        }

        private void RegisterHotkeys()
        {
            if (_hotkeys == null)
            {
                _hotkeys = new HotkeyWindow();
                _hotkeys.HotkeyPressed += OnHotkey;
            }
            try
            {
                Native.UnregisterHotKey(_hotkeys.Handle, HK_PANEL);
                Native.RegisterHotKey(_hotkeys.Handle, HK_PANEL, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (uint)Keys.V);
            }
            catch { }

            try
            {
                Native.UnregisterHotKey(_hotkeys.Handle, HK_VOL_UP);
                Native.UnregisterHotKey(_hotkeys.Handle, HK_VOL_DOWN);
                Native.UnregisterHotKey(_hotkeys.Handle, HK_VOL_MUTE);
                if (_cfg.VolumeKeys)
                {
                    Native.RegisterHotKey(_hotkeys.Handle, HK_VOL_UP, Native.MOD_NOREPEAT, 0xAF);
                    Native.RegisterHotKey(_hotkeys.Handle, HK_VOL_DOWN, Native.MOD_NOREPEAT, 0xAE);
                    Native.RegisterHotKey(_hotkeys.Handle, HK_VOL_MUTE, Native.MOD_NOREPEAT, 0xAD);
                }
            }
            catch { }
        }

        private void OnHotkey(int id)
        {
            if (id == HK_PANEL)
            {
                ShowPanel();
                return;
            }
            VmChannel master = FindMaster();
            if (master == null) return;

            if (id == HK_VOL_UP) SetMasterGain(master, GetMasterGain(master) + 2.0);
            else if (id == HK_VOL_DOWN) SetMasterGain(master, GetMasterGain(master) - 2.0);
            else if (id == HK_VOL_MUTE)
            {
                bool muted;
                if (_vm.GetMute(master, out muted)) _vm.SetMute(master, !muted);
            }
        }

        private VmChannel FindMaster()
        {
            for (int i = 0; i < _vm.Channels.Count; i++)
                if (_vm.Channels[i].IsMaster) return _vm.Channels[i];
            return null;
        }

        private double GetMasterGain(VmChannel c)
        {
            return _vm.GetGain(c);
        }

        private void SetMasterGain(VmChannel c, double db)
        {
            if (db > FaderControl.MaxDb) db = FaderControl.MaxDb;
            if (db < FaderControl.MinDb) db = FaderControl.MinDb;
            _vm.SetGain(c, (float)db);
        }

        private void OnTrayClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) TogglePanel();
        }

        public void ShowPanel()
        {
            if (_panel == null) BuildPanel();
            EnsureConnected(true);
            Theme.Refresh();          // 跟随系统深浅色
            UpdateTrayIcon();
            _panel.Rebuild(_vm, _cfg);
            _panel.ClearHover();
            _panel.PositionNearTray();

            // 记下弹出前的那个窗口，指针不在面板上时滚轮要还给它
            IntPtr prev = Native.GetForegroundWindow();
            if (prev != _panel.Handle) _panel.PreviousFocus = prev;

            _panel.RefreshBackdrop();   // 显示前先抓取背景做亚克力模糊
            _panel.Show();
            _panel.Activate();
            ForceForeground(_panel.Handle);
            _panel.BringToFront();
            try { _panel.Focus(); } catch { }   // 拿到键盘焦点，滚轮事件才会送到面板
            _panelShownAt = DateTime.Now;
            UpdateStatus();
        }

        /// <summary>按当前主题重建托盘图标（深/浅色任务栏下用不同配色）。</summary>
        private void UpdateTrayIcon()
        {
            Icon old = null;
            try
            {
                Icon fresh = Glyphs.CreateAppIcon(Theme.Current.TraySpeaker, Theme.Current.TrayWave, Theme.Current.TrayMark);
                old = _trayIcon;
                _trayIcon = fresh;
                if (_tray != null) _tray.Icon = fresh;
            }
            catch { return; }
            try { if (old != null) old.Dispose(); } catch { }
        }

        /// <summary>
        /// 把面板抢成前台窗口。后台程序直接调 SetForegroundWindow 会被系统的前台锁挡住，
        /// 这里借用 AttachThreadInput 临时接入前台线程再设置，能稳定拿到焦点，
        /// 否则「失焦自动隐藏」会在 0.6 秒后立刻把面板收掉。
        /// </summary>
        private static void ForceForeground(IntPtr hWnd)
        {
            try
            {
                if (hWnd == IntPtr.Zero) return;
                IntPtr fg = Native.GetForegroundWindow();
                uint fgPid;
                uint fgThread = Native.GetWindowThreadProcessId(fg, out fgPid);
                uint myThread = Native.GetCurrentThreadId();
                bool attached = false;
                if (fgThread != 0 && fgThread != myThread)
                {
                    attached = Native.AttachThreadInput(myThread, fgThread, true);
                }
                Native.SetForegroundWindow(hWnd);
                Native.SetFocus(hWnd);
                if (attached) Native.AttachThreadInput(myThread, fgThread, false);
            }
            catch { }
        }

        private void TogglePanel()
        {
            if (_panel != null && _panel.Visible) _panel.Hide();
            else ShowPanel();
        }

        private void OpenSettings()
        {
            EnsureConnected(true);
            if (!_vm.Connected)
            {
                MessageBox.Show("未检测到正在运行的 Voicemeeter，无法读取通道列表。", "通道设置",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string[] keys = new string[_vm.Channels.Count];
            for (int i = 0; i < _vm.Channels.Count; i++) keys[i] = _vm.Channels[i].Key;
            if (ChannelPicker.Edit(_vm, _cfg, keys))
            {
                if (_panel != null) _panel.Rebuild(_vm, _cfg);
                SyncTrayToneState();
                UpdateStatus();
            }
        }

        /// <summary>设置窗口里改了提示音后，把托盘菜单的勾选状态同步过来。</summary>
        private void SyncTrayToneState()
        {
            try
            {
                if (_miSound != null) _miSound.Checked = _cfg.SoundFeedback;
                if (_styleItems != null)
                {
                    for (int i = 0; i < _styleItems.Length; i++)
                        if (_styleItems[i] != null) _styleItems[i].Checked = (i == _cfg.ToneStyle);
                }
            }
            catch { }
        }

        private void OpenVoicemeeter()
        {
            try
            {
                Process[] procs = Process.GetProcessesByName("voicemeeterpro_x64");
                if (procs.Length == 0) procs = Process.GetProcessesByName("voicemeeter_x64");
                if (procs.Length == 0) procs = Process.GetProcessesByName("voicemeeter");
                if (procs.Length > 0)
                {
                    IntPtr h = procs[0].MainWindowHandle;
                    if (h != IntPtr.Zero)
                    {
                        Native.SetForegroundWindow(h);
                        return;
                    }
                }
                string dir = Path.GetDirectoryName(_vm.DllPath ?? "");
                string[] names = { "voicemeeterpro_x64.exe", "voicemeeterpotato_x64.exe", "voicemeeter_x64.exe", "voicemeeter.exe" };
                if (dir != null)
                {
                    for (int i = 0; i < names.Length; i++)
                    {
                        string exe = Path.Combine(dir, names[i]);
                        if (File.Exists(exe))
                        {
                            Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = dir });
                            return;
                        }
                    }
                }
            }
            catch { }
        }

        private static void SetAutoStart(bool enable)
        {
            try
            {
                string key = @"Software\Microsoft\Windows\CurrentVersion\Run";
                if (enable)
                {
                    string exe = Application.ExecutablePath;
                    Microsoft.Win32.Registry.CurrentUser.CreateSubKey(key).SetValue("VMixer", "\"" + exe + "\"");
                }
                else
                {
                    Microsoft.Win32.Registry.CurrentUser.CreateSubKey(key).DeleteValue("VMixer", false);
                }
            }
            catch { }
        }

        private bool EnsureConnected(bool interactive)
        {
            if (_vm.Connected) return true;
            if ((DateTime.Now - _lastConnectTry).TotalSeconds < 1.0) return false;
            _lastConnectTry = DateTime.Now;

            if (_vm.Connect(_cfg.DllPath))
            {
                _vmName = _vm.VmShortName;
                if (_panel != null) _panel.Rebuild(_vm, _cfg);
                return true;
            }
            else
            {
                // VM 没起来的时候不再弹气泡提示：状态直接写在面板状态栏里
                // （「已连接 xxx · 未运行」），不打扰用户。
                if (interactive)
                {
                    MessageBox.Show(_vm.LastError, "Voicemeeter 混音器",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            // 第二个实例请求显示面板（在本实例 UI 线程执行）
            if (_showRequested)
            {
                _showRequested = false;
                ShowPanel();
                return;
            }

            bool connected = EnsureConnected(false);

            if (connected)
            {
                UpdateStatus();
                if (_panel != null && _panel.Visible) _panel.RefreshValues();
            }
            else if (_panel != null && _panel.Visible)
            {
                // VM 没起来时只在状态栏末尾补一句，不弹提示框
                _panel.SetStatus(StatusText(), "· 未运行", "", false);
            }

            // 失焦自动隐藏
            if (_panel != null && _panel.Visible && (DateTime.Now - _panelShownAt).TotalSeconds > 0.6)
            {
                IntPtr fg = Native.GetForegroundWindow();
                if (fg != _panel.Handle && fg != IntPtr.Zero)
                {
                    bool isMenu = _menu != null && _menu.Visible;
                    if (!isMenu)
                    {
                        _panel.Hide();
                    }
                }
            }
        }

        /// <summary>
        /// 托盘气泡（也就是托盘图标的悬停提示）里显示的那条通道信息。
        /// 面板状态栏右边显示同样的内容，两处保持一致。
        /// </summary>
        private string BuildTipInfo()
        {
            return Program.BuildTipInfo(_vm, _cfg);
        }

        /// <summary>
        /// 状态栏左边那句连接状态。断开后仍保留上次连上的 VM 名字，
        /// 这样 VM 没运行的时候显示的是「已连接 Banana · 未运行」，
        /// 不用另外弹提示框告诉用户。
        /// </summary>
        private string StatusText()
        {
            if (_vm.Connected) return "已连接 " + _vm.VmShortName;
            if (_vmName.Length > 0) return "已连接 " + _vmName;
            return "未连接";
        }

        private void UpdateStatus()
        {
            string tipInfo = BuildTipInfo();

            if (_vm.Connected) _vmName = _vm.VmShortName;
            string text = StatusText();
            if (_panel != null) _panel.SetStatus(text, "", tipInfo, true);

            // 托盘气泡：和面板状态栏右边显示完全一致的内容
            string tip = tipInfo.Length > 0
                ? tipInfo + "\nVoicemeeter 混音器"
                : "Voicemeeter 混音器";
            if (tip.Length > 127) tip = tip.Substring(0, 127);
            try { if (_tray.Text != tip) _tray.Text = tip; } catch { }
        }

        private void ExitApp()
        {
            _quitting = true;
            _cfg.Save();
            Cleanup();
            ExitThread();
        }
    }

    public static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int pid);

        /// <summary>
        /// 托盘气泡（托盘图标的悬停提示）里显示的那条通道信息。
        /// 面板状态栏右侧显示完全相同的内容——两处共用这一个数据源，永远一致。
        /// </summary>
        public static string BuildTipInfo(VmApi vm, VmConfig cfg)
        {
            if (vm == null || cfg == null) return "";
            VmChannel focus = null;
            if (!string.IsNullOrEmpty(cfg.TipChannel))
            {
                for (int i = 0; i < vm.Channels.Count; i++)
                    if (vm.Channels[i].Key == cfg.TipChannel) { focus = vm.Channels[i]; break; }
            }
            if (focus == null)
            {
                for (int i = 0; i < vm.Channels.Count; i++)
                    if (vm.Channels[i].IsMaster) { focus = vm.Channels[i]; break; }
            }
            if (focus == null) return "";
            return focus.DisplayName + "  " + FaderControl.FormatDb(vm.GetGain(focus));
        }

        [STAThread]
        public static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }

            try
            {
                Run(args);
            }
            catch (Exception ex)
            {
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VMixer");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "error.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + ex.ToString());
                }
                catch { }
                MessageBox.Show("程序启动失败：\r\n" + ex.Message, "Voicemeeter 混音器",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Run(string[] args)
        {
            bool testMode = false;
            bool showMode = false;
            bool layoutMode = false;
            bool iconMode = false;
            bool toneMode = false;
            bool settingsMode = false;
            bool dragTestMode = false;
            bool glyphsMode = false;
            string wavPath = null;
            string renderPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--test", StringComparison.OrdinalIgnoreCase)) testMode = true;
                if (string.Equals(args[i], "--show", StringComparison.OrdinalIgnoreCase)) showMode = true;
                if (string.Equals(args[i], "--layout", StringComparison.OrdinalIgnoreCase)) layoutMode = true;
                if (string.Equals(args[i], "--icon", StringComparison.OrdinalIgnoreCase)) iconMode = true;
                if (string.Equals(args[i], "--tone", StringComparison.OrdinalIgnoreCase)) toneMode = true;
                if (string.Equals(args[i], "--settings", StringComparison.OrdinalIgnoreCase)) settingsMode = true;
                if (string.Equals(args[i], "--dragtest", StringComparison.OrdinalIgnoreCase)) dragTestMode = true;
                if (string.Equals(args[i], "--glyphs", StringComparison.OrdinalIgnoreCase)) glyphsMode = true;
                if (string.Equals(args[i], "--towav", StringComparison.OrdinalIgnoreCase))
                {
                    wavPath = (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                        ? args[i + 1]
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VMixer", "tone.wav");
                }
                if (string.Equals(args[i], "--render", StringComparison.OrdinalIgnoreCase))
                {
                    renderPath = (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                        ? args[i + 1]
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VMixer", "panel_render.png");
                }
            }

            if (wavPath != null)
            {
                AttachConsole(-1);
                int st = 0;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    int n;
                    if (int.TryParse(args[i + 1], out n) && n >= 0 && n < SoundFx.StyleCount) { st = n; break; }
                    string a = args[i + 1];
                    if (a.Equals("bright", StringComparison.OrdinalIgnoreCase)) { st = SoundFx.StyleBright; break; }
                    if (a.Equals("soft", StringComparison.OrdinalIgnoreCase)) { st = SoundFx.StyleSoft; break; }
                    if (a.Equals("blip", StringComparison.OrdinalIgnoreCase)) { st = SoundFx.StyleBlip; break; }
                    if (a.Equals("clear", StringComparison.OrdinalIgnoreCase)) { st = SoundFx.StyleClear; break; }
                }
                try
                {
                    string dir = Path.GetDirectoryName(wavPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllBytes(wavPath, SoundFx.RenderWav(st));
                    Console.WriteLine("tone wav saved: " + wavPath + " (style " + st + " " + SoundFx.StyleName(st) + ")");
                }
                catch (Exception ex) { Console.WriteLine("tone wav failed: " + ex.Message); }
                return;
            }

            if (iconMode)
            {
                string outPath = args.Length > 1 && !args[1].StartsWith("--") ? args[1]
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VMixer", "icon.png");
                try
                {
                    Theme.Refresh();
                    string dir = Path.GetDirectoryName(outPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    if (outPath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                    {
                        // exe 图标固定用强调色，浅色/深色任务栏上都能看清
                        Glyphs.SaveIconIco(outPath, Color.FromArgb(0, 120, 212));
                    }
                    else
                    {
                        Glyphs.SaveIconPng(outPath, Theme.Current.TraySpeaker, Theme.Current.TrayWave, Theme.Current.TrayMark);
                    }
                    Console.WriteLine("icon saved: " + outPath);
                }
                catch (Exception ex) { Console.WriteLine("icon failed: " + ex.Message); }
                return;
            }

            if (glyphsMode)
            {
                // 把 8 个内置图标字形画成一张对照图（调试用）
                AttachConsole(-1);
                string outPath = args.Length > 1 && !args[1].StartsWith("--") ? args[1]
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                   "VMixer", "glyphs.png");
                try
                {
                    Glyphs.SaveGlyphSheet(outPath);
                    Console.WriteLine("glyph sheet saved: " + outPath);
                }
                catch (Exception ex) { Console.WriteLine("glyph sheet failed: " + ex.Message); }
                return;
            }

            if (dragTestMode)
            {
                // 拖动排序自测：模拟两次拖动并把顺序写回配置
                AttachConsole(-1);
                VmConfig cfg = new VmConfig();
                cfg.Load();
                VmApi vm = new VmApi();
                if (!vm.Connect(cfg.DllPath))
                {
                    Console.WriteLine("connect failed: " + vm.LastError);
                    return;
                }
                string[] keys = new string[vm.Channels.Count];
                for (int i = 0; i < vm.Channels.Count; i++) keys[i] = vm.Channels[i].Key;
                try
                {
                    Console.WriteLine(ChannelPicker.RunDragSelfTest(vm, cfg, keys));
                }
                catch (Exception ex) { Console.WriteLine("dragtest failed: " + ex.Message); }
                vm.Disconnect();
                return;
            }

            if (settingsMode)
            {
                // 单独打开通道设置窗口（调试 / 独立使用）
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                VmConfig cfg = new VmConfig();
                cfg.Load();
                VmApi vm = new VmApi();
                if (!vm.Connect(cfg.DllPath))
                {
                    MessageBox.Show(vm.LastError, "通道设置",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string[] keys = new string[vm.Channels.Count];
                for (int i = 0; i < vm.Channels.Count; i++) keys[i] = vm.Channels[i].Key;
                ChannelPicker.Edit(vm, cfg, keys);
                vm.Disconnect();
                return;
            }

            if (toneMode)
            {
                AttachConsole(-1);
                int probe = SoundFx.Probe();
                Console.WriteLine("waveOutOpen result = " + probe + (probe == 0 ? " (ok)" : " (failed)"));

                // --tone [音色] [dB]；音色可以是 0-3 或 clear/bright/soft/blip
                int style = 0;
                double db = 0;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    string a = args[i + 1];
                    if (a.StartsWith("--")) continue;
                    double d;
                    int n;
                    if (int.TryParse(a, out n) && n >= 0 && n < SoundFx.StyleCount)
                    {
                        style = n;
                        continue;
                    }
                    if (a.Equals("clear", StringComparison.OrdinalIgnoreCase)) { style = SoundFx.StyleClear; continue; }
                    if (a.Equals("bright", StringComparison.OrdinalIgnoreCase)) { style = SoundFx.StyleBright; continue; }
                    if (a.Equals("soft", StringComparison.OrdinalIgnoreCase)) { style = SoundFx.StyleSoft; continue; }
                    if (a.Equals("blip", StringComparison.OrdinalIgnoreCase)) { style = SoundFx.StyleBlip; continue; }
                    if (double.TryParse(a, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out d))
                    {
                        db = d;
                        continue;
                    }
                }

                Console.WriteLine("style = " + style + " (" + SoundFx.StyleName(style) + ")");
                SoundFx.Play(db, style);
                System.Threading.Thread.Sleep(800);
                Console.WriteLine("tone played at " + db.ToString("0.0") + " dB");
                return;
            }

            if (testMode)
            {
                AttachConsole(-1);
                RunSelfTest();
                return;
            }

            if (layoutMode)
            {
                AttachConsole(-1);
                RunLayoutDump();
                return;
            }

            if (renderPath != null)
            {
                AttachConsole(-1);
                // 可选的第二个参数：指定第几条通道处于「悬停」状态（调试用）
                // 可选的第三个参数：off = 模拟「VM 没运行」时的状态栏
                int hover = -1;
                bool offline = false;
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == renderPath)
                    {
                        int h;
                        if (int.TryParse(args[i + 1], out h)) hover = h;
                        if (i + 2 < args.Length &&
                            string.Equals(args[i + 2], "off", StringComparison.OrdinalIgnoreCase))
                            offline = true;
                        break;
                    }
                }
                RunRenderDump(renderPath, hover, offline);
                return;
            }

            bool created = false;
            using (Mutex m = new Mutex(true, "Global\\VMixer_SingleInstance", out created))
            {
                if (!created)
                {
                    // 已有实例在运行：通知它显示面板
                    try
                    {
                        using (EventWaitHandle ev = new EventWaitHandle(false, EventResetMode.AutoReset, "Global\\VMixer_ShowPanel"))
                        {
                            ev.Set();
                        }
                    }
                    catch { }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayContext(showMode));
            }
        }

        /// <summary>把面板离屏渲染成 PNG（调试用）。</summary>
        private static void RunRenderDump(string outPath)
        {
            RunRenderDump(outPath, -1, false);
        }

        private static void RunRenderDump(string outPath, int hoverIndex, bool offline)
        {
            Theme.Refresh();
            VmApi vm = new VmApi();
            if (!vm.Connect(null))
            {
                Console.WriteLine("connect failed: " + vm.LastError);
                return;
            }
            VmConfig cfg = new VmConfig();
            cfg.Load();
            MixerPanel panel = new MixerPanel(vm, cfg);
            panel.Rebuild(vm, cfg);
            panel.PositionNearTray();
            if (offline)
            {
                // 模拟 VM 没运行：状态栏在「已连接 xxx」后面补一句「· 未运行」
                panel.SetStatus("已连接 " + vm.VmShortName, "· 未运行", "", false);
            }
            else
            {
                panel.SetStatus("已连接 " + vm.VmShortName, BuildTipInfo(vm, cfg), true);
            }
            if (hoverIndex >= 0) panel.SetHoverIndex(hoverIndex);
            Console.WriteLine(panel.DescribeLayout());
            for (int i = 0; i < panel.ChannelCount; i++)
            {
                Console.WriteLine("ch" + i + " name='" + panel.ChannelName(i) + "' param='" + panel.ChannelParam(i) + "'");
            }
            try
            {
                panel.RenderToPng(outPath);
                Console.WriteLine("render saved: " + outPath);
            }
            catch (Exception ex) { Console.WriteLine("render failed: " + ex.Message); }
            vm.Disconnect();
        }

        /// <summary>输出面板各通道滑块的屏幕坐标，便于自动化点击测试（写入 layout.txt）。</summary>
        private static void RunLayoutDump()
        {
            VmApi vm = new VmApi();
            if (!vm.Connect(null))
            {
                WriteLayoutFile("connect failed: " + vm.LastError);
                return;
            }
            VmConfig cfg = new VmConfig();
            cfg.Load();
            MixerPanel panel = new MixerPanel(vm, cfg);
            panel.Rebuild(vm, cfg);
            panel.PositionNearTray();
            WriteLayoutFile(panel.DescribeLayout());
            vm.Disconnect();
        }

        private static void WriteLayoutFile(string text)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VMixer");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "layout.txt"), text ?? "");
            }
            catch { }
        }

        /// <summary>命令行自检：连接 Voicemeeter，打印通道列表，并做一次写入/回读测试。</summary>
        private static void RunSelfTest()
        {
            Console.WriteLine("== VMixer self test ==");
            VmApi vm = new VmApi();
            if (!vm.Connect(null))
            {
                Console.WriteLine("连接失败：" + vm.LastError);
                return;
            }
            Console.WriteLine("已连接：" + vm.VmTypeName + " " + vm.VmVersionText + "  (" + vm.DllPath + ")");
            Console.WriteLine("发现通道 " + vm.Channels.Count + " 个：");

            VmChannel master = null;
            for (int i = 0; i < vm.Channels.Count; i++)
            {
                VmChannel c = vm.Channels[i];
                float g = vm.GetGain(c);
                bool muted;
                vm.GetMute(c, out muted);
                if (c.IsMaster) master = c;
                Console.WriteLine(string.Format("  {0,-6} {1,-10} {2,-14} gain={3,7:0.00} dB  mute={4}",
                    c.Key, c.Kind == VmChannelKind.Strip ? "输入" : "总线", c.DefaultName, g, muted ? "ON" : "off"));
            }

            if (master != null)
            {
                float original = vm.ReadActual(master);
                Console.WriteLine("主音量写入测试：" + original.ToString("0.00") + " -> -12.00");
                vm.SetGain(master, -12f);
                vm.WaitApplied(400);
                float after = vm.ReadActual(master);
                Console.WriteLine("回读 = " + after.ToString("0.00") + " dB  " + (Math.Abs(after + 12f) < 0.01f ? "✓ 通过" : "✗ 失败"));
                vm.SetGain(master, original);
                vm.WaitApplied(400);
                float restored = vm.ReadActual(master);
                Console.WriteLine("已恢复为 " + restored.ToString("0.00") + " dB  " + (Math.Abs(restored - original) < 0.01f ? "✓" : "✗"));
            }
            vm.Disconnect();
            Console.WriteLine("== 测试结束 ==");
            Console.Out.Flush();
        }
    }
}
