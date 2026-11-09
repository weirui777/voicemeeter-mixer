using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace VMixer
{
    public enum VmChannelKind { Strip, Bus }

    /// <summary>一个可调音量的 Voicemeeter 通道（输入条 strip 或 输出总线 bus）。</summary>
    public class VmChannel
    {
        public VmChannelKind Kind;
        public int Index;
        public string Param;            // 例如 "Strip[0]" / "Bus[0]"
        public string DefaultName;      // Voicemeeter 界面上的默认名称
        public bool IsMaster;            // 主输出（Bus A1）
        public bool IsInput;             // strip 为输入，bus 为输出
        public string CustomName;        // 用户自定义名称（保存在配置文件里）

        public string Key
        {
            get { return (Kind == VmChannelKind.Strip ? "S" : "B") + Index.ToString(); }
        }

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(CustomName) ? DefaultName : CustomName; }
        }

        public string GainParam { get { return Param + ".Gain"; } }
        public string MuteParam { get { return Param + ".Mute"; } }
    }

    /// <summary>
    /// Voicemeeter Remote API (VoicemeeterRemote64.dll) 封装。
    ///
    /// 重要：所有 API 调用都在同一条后台线程上完成。该线程每 10ms 调用一次
    /// VBVMR_IsParametersDirty()——官方 SDK 文档明确说明：这个调用会让 Voicemeeter
    /// 真正处理客户端推送过去的参数值，不调用的话写入会一直挂起不生效。
    /// </summary>
    public class VmApi
    {
        private const int MaxProbe = 24;

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int LoginFn();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int LogoutFn();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetTypeFn(out int type);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetVersionFn(out int version);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetFloatFn(IntPtr name, out float value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetFloatFn(IntPtr name, float value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetStringFn(IntPtr name, IntPtr buffer);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetLevelFn(int type, int channel, out float value);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DirtyFn();

        private IntPtr _dll = IntPtr.Zero;
        private LoginFn _login;
        private LogoutFn _logout;
        private GetTypeFn _getType;
        private GetVersionFn _getVersion;
        private GetFloatFn _getFloat;
        private SetFloatFn _setFloat;
        private GetStringFn _getString;
        private GetLevelFn _getLevel;
        private DirtyFn _dirty;

        private readonly object _sync = new object();
        private readonly Queue<Action> _commands = new Queue<Action>();

        // 待校验写入（只在后台线程访问）
        private string _pendingName;
        private float _pendingValue;
        private DateTime _pendingSince = DateTime.MinValue;
        private int _writeFails;
        private readonly Dictionary<string, float> _gainCache = new Dictionary<string, float>();
        private readonly Dictionary<string, bool> _muteCache = new Dictionary<string, bool>();

        private Thread _worker;
        private volatile bool _quit;

        public bool Connected { get; private set; }
        public string LastError { get; private set; }
        public int VmType { get; private set; }
        public int VmVersion { get; private set; }
        public string DllPath { get; private set; }
        public List<VmChannel> Channels { get; private set; }

        public string VmShortName
        {
            get
            {
                if (VmType == 1) return "Voicemeeter";
                if (VmType == 2) return "Banana";
                if (VmType == 3) return "Potato";
                if (VmType == 6) return "Potato x64";
                return "Voicemeeter";
            }
        }

        public string VmTypeName
        {
            get
            {
                if (VmType == 1) return "Voicemeeter";
                if (VmType == 2) return "Voicemeeter Banana";
                if (VmType == 3) return "Voicemeeter Potato";
                if (VmType == 6) return "Voicemeeter Potato x64";
                return "Voicemeeter";
            }
        }

        public string VmVersionText
        {
            get
            {
                if (VmVersion == 0) return "";
                int v1 = (VmVersion & unchecked((int)0xFF000000)) >> 24;
                int v2 = (VmVersion & 0x0000FF00) >> 8;
                int v3 = (VmVersion & 0x000000FF) >> 0;
                return "v" + v1 + "." + v2 + "." + v3;
            }
        }

        public VmApi()
        {
            Channels = new List<VmChannel>();
            LastError = "";
        }

        public static string FindDll(string overridePath)
        {
            List<string> candidates = new List<string>();
            if (!string.IsNullOrEmpty(overridePath)) candidates.Add(overridePath);
            candidates.Add(Path.Combine(ProgramFilesX86, "VB", "Voicemeeter", "VoicemeeterRemote64.dll"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VB", "Voicemeeter", "VoicemeeterRemote64.dll"));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VoicemeeterRemote64.dll"));

            for (int i = 0; i < candidates.Count; i++)
                if (!string.IsNullOrEmpty(candidates[i]) && File.Exists(candidates[i])) return candidates[i];
            return null;
        }

        private static string ProgramFilesX86
        {
            get
            {
                string p = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (string.IsNullOrEmpty(p)) p = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                return p;
            }
        }

        public bool Connect(string dllOverride)
        {
            Disconnect();

            string path = FindDll(dllOverride);
            if (path == null)
            {
                LastError = "找不到 VoicemeeterRemote64.dll（请确认已安装 Voicemeeter）";
                return false;
            }
            DllPath = path;

            _dll = LoadLibrary(path);
            if (_dll == IntPtr.Zero)
            {
                LastError = "无法加载 " + path;
                return false;
            }

            _login = Bind<LoginFn>("VBVMR_Login");
            _logout = Bind<LogoutFn>("VBVMR_Logout");
            _getType = Bind<GetTypeFn>("VBVMR_GetVoicemeeterType");
            _getVersion = Bind<GetVersionFn>("VBVMR_GetVoicemeeterVersion");
            _getFloat = Bind<GetFloatFn>("VBVMR_GetParameterFloat");
            _setFloat = Bind<SetFloatFn>("VBVMR_SetParameterFloat");
            _getString = Bind<GetStringFn>("VBVMR_GetParameterStringA");
            _getLevel = Bind<GetLevelFn>("VBVMR_GetLevel");
            _dirty = Bind<DirtyFn>("VBVMR_IsParametersDirty");

            if (_login == null || _getFloat == null || _setFloat == null)
            {
                LastError = "VoicemeeterRemote64.dll 接口不完整";
                FreeLibrary(_dll);
                _dll = IntPtr.Zero;
                return false;
            }

            int rc = _login();
            if (rc != 0)
            {
                LastError = (rc == 1) ? "Voicemeeter 未运行" : ("连接 Voicemeeter 失败（错误码 " + rc + "）");
                FreeLibrary(_dll);
                _dll = IntPtr.Zero;
                return false;
            }

            Connected = true;
            LastError = "";
            Discover();
            RefreshCache();
            StartWorker();
            return true;
        }

        /// <summary>
        /// 重新探测通道列表（设置里的「重新扫描」）。
        /// 所有 API 调用都必须回到那条后台线程上做，所以排进命令队列再等它完成。
        /// </summary>
        public bool Rescan()
        {
            if (!Connected) return false;
            bool done = false;
            ManualResetEvent evt = new ManualResetEvent(false);
            Enqueue(delegate
            {
                try { Discover(); RefreshCache(); done = true; }
                catch { }
                finally { evt.Set(); }
            });
            evt.WaitOne(3000);
            try { evt.Close(); } catch { }
            return done;
        }

        public void Disconnect()        {
            _quit = true;
            Thread w = _worker;
            _worker = null;
            if (w != null)
            {
                try { w.Join(800); } catch { }
            }
            _quit = false;

            if (Connected && _logout != null)
            {
                try { _logout(); } catch { }
            }
            Connected = false;
            if (_dll != IntPtr.Zero)
            {
                try { FreeLibrary(_dll); } catch { }
                _dll = IntPtr.Zero;
            }
        }

        private T Bind<T>(string name) where T : class
        {
            IntPtr p = GetProcAddress(_dll, name);
            if (p == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        /// <summary>
        /// 重建 API 会话：重新加载 DLL 并重新登录。
        /// 当别的程序调用过 VBVMR_Logout() 时，本进程的会话会变得「迟钝」——
        /// 写入其实生效了，但本进程读回来的却是旧值（面板显示会慢一拍）。
        /// 重新登录一次即可恢复同步。通道对象不重建，只刷新数值。
        /// </summary>
        private bool ReopenSession()
        {
            try
            {
                if (_dll != IntPtr.Zero)
                {
                    try { FreeLibrary(_dll); } catch { }
                    _dll = IntPtr.Zero;
                }

                string path = DllPath;
                if (string.IsNullOrEmpty(path)) return false;
                _dll = LoadLibrary(path);
                if (_dll == IntPtr.Zero) return false;

                _login = Bind<LoginFn>("VBVMR_Login");
                _logout = Bind<LogoutFn>("VBVMR_Logout");
                _getType = Bind<GetTypeFn>("VBVMR_GetVoicemeeterType");
                _getVersion = Bind<GetVersionFn>("VBVMR_GetVoicemeeterVersion");
                _getFloat = Bind<GetFloatFn>("VBVMR_GetParameterFloat");
                _setFloat = Bind<SetFloatFn>("VBVMR_SetParameterFloat");
                _getString = Bind<GetStringFn>("VBVMR_GetParameterStringA");
                _getLevel = Bind<GetLevelFn>("VBVMR_GetLevel");
                _dirty = Bind<DirtyFn>("VBVMR_IsParametersDirty");

                if (_login == null || _getFloat == null || _setFloat == null) return false;
                if (_login() != 0) { Connected = false; return false; }

                Connected = true;
                ReopenCount++;
                RefreshCache();
                return true;
            }
            catch { return false; }
        }

        private void StartWorker()
        {
            _quit = false;
            _worker = new Thread(WorkerLoop);
            _worker.IsBackground = true;
            _worker.Priority = ThreadPriority.AboveNormal;
            _worker.Start();
        }

        /// <summary>唯一接触 API 的线程：泵参数 + 刷新缓存 + 执行写操作。</summary>
        private void WorkerLoop()
        {
            DateTime lastRefresh = DateTime.MinValue;
            while (!_quit)
            {
                Action cmd = null;
                lock (_sync)
                {
                    if (_commands.Count > 0) cmd = _commands.Dequeue();
                }
                if (cmd != null)
                {
                    try { cmd(); } catch { }
                }

                // 关键：这一步让 Voicemeeter 真正处理推送过去的参数
                try { if (_dirty != null) _dirty(); } catch { }

                // 校验上一次写入是否生效，必要时重建会话
                try { VerifyPendingWrite(); } catch { }

                if ((DateTime.Now - lastRefresh).TotalMilliseconds >= 200)
                {
                    lastRefresh = DateTime.Now;
                    try { RefreshCache(); } catch { }
                }

                Thread.Sleep(10);
            }
        }

        private void Enqueue(Action a)
        {
            lock (_sync) { _commands.Enqueue(a); }
        }

        /// <summary>
        /// 写入一个浮点参数并登记为「待校验写入」。
        /// Voicemeeter 的 Remote API 是共享会话：若有别的程序调用了 VBVMR_Logout()，
        /// 我们推送的值会被丢弃且不会报错。这里记录下来，稍后回读校验，必要时重新登录。
        /// </summary>
        private void PushFloat(string name, float value)
        {
            bool ok = SetFloat(name, value);
            _pendingName = name;
            _pendingValue = value;
            _pendingSince = DateTime.Now;
            if (!ok) _writeFails++;
        }

        /// <summary>重建会话次数（诊断用）。</summary>
        public int ReopenCount { get; private set; }

        /// <summary>
        /// 校验上一次写入是否真的生效。
        /// Voicemeeter 的 Remote API 是共享会话：别的程序读写一次参数后，
        /// 我们读回来的值会停在旧数据上（写入其实生效了，但显示会慢一拍）。
        /// 这里回读校验，必要时重建会话并重发。
        /// </summary>
        private void VerifyPendingWrite()
        {
            if (_pendingName == null) return;
            if ((DateTime.Now - _pendingSince).TotalMilliseconds < 400) return;

            float cur;
            bool ok = TryGetFloat(_pendingName, out cur);
            if (ok && Math.Abs(cur - _pendingValue) <= 0.25f)
            {
                _pendingName = null;
                _writeFails = 0;
                return;
            }

            _writeFails++;
            string nm = _pendingName;
            float vv = _pendingValue;

            if (_writeFails == 2)
            {
                // 连续两次读不到新值：重建会话，让回读重新同步
                ReopenSession();
            }
            SetFloat(nm, vv);
            _pendingName = nm;
            _pendingValue = vv;
            _pendingSince = DateTime.Now;
        }

        private void RefreshCache()
        {
            for (int i = 0; i < Channels.Count; i++)
            {
                VmChannel c = Channels[i];
                float g;
                if (TryGetFloat(c.GainParam, out g))
                {
                    lock (_sync) { _gainCache[c.GainParam] = g; }
                }
                float m;
                if (TryGetFloat(c.MuteParam, out m))
                {
                    lock (_sync) { _muteCache[c.MuteParam] = m >= 0.5f; }
                }
                // 注意：这里刻意不调用 VBVMR_GetLevel()。
                // 实测该调用会把电平值写进共享缓冲区的「增益」槽位
                // （主总线电平会出现在 Bus[0].Gain 上），而本程序每 10ms 泵一次参数，
                // 于是电平值会被当成增益提交给 Voicemeeter，直接损坏用户的音量设置。
                // 因此面板不显示实时电平条。
            }
        }

        /// <summary>探测当前 Voicemeeter 里实际存在的 strip / bus。</summary>
        private void Discover()
        {
            Channels = new List<VmChannel>();
            VmType = 0;
            VmVersion = 0;
            int vmType = 0, vmVersion = 0;
            if (_getType != null) _getType(out vmType);
            if (_getVersion != null) _getVersion(out vmVersion);
            VmType = vmType;
            VmVersion = vmVersion;

            for (int i = 0; i < MaxProbe; i++)
            {
                float v;
                if (!TryGetFloat("Strip[" + i + "].Gain", out v)) break;
                VmChannel c = new VmChannel();
                c.Kind = VmChannelKind.Strip;
                c.Index = i;
                c.Param = "Strip[" + i + "]";
                c.IsInput = true;
                c.DefaultName = StripName(i);
                string label = GetLabel(c.Param);
                // Voicemeeter 对没接设备的条返回「未使用 / unused」这类占位标签，
                // 这种情况下还是用我们自己的「硬件输入 1 / 虚拟输入 / 虚拟 AUX」更好认。
                if (!IsPlaceholderLabel(label)) c.DefaultName = label;
                Channels.Add(c);
            }

            for (int i = 0; i < MaxProbe; i++)
            {
                float v;
                if (!TryGetFloat("Bus[" + i + "].Gain", out v)) break;
                VmChannel c = new VmChannel();
                c.Kind = VmChannelKind.Bus;
                c.Index = i;
                c.Param = "Bus[" + i + "]";
                c.IsInput = false;
                c.DefaultName = BusName(i);
                c.IsMaster = (i == 0);
                Channels.Add(c);
            }

            lock (_sync)
            {
                _gainCache.Clear();
                _muteCache.Clear();
            }
        }

        /// <summary>判断 Voicemeeter 返回的标签是不是「未使用」这类占位符。</summary>
        private static bool IsPlaceholderLabel(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            string t = s.Trim().ToLowerInvariant();
            if (t.Length == 0) return true;
            if (t == "未使用" || t == "未使用 " || t == "unused" || t == "not used") return true;
            if (t == "n/a" || t == "none") return true;
            return false;
        }

        private string StripName(int i)        {
            if (VmType == 3)
            {
                if (i <= 4) return "硬件输入 " + (i + 1);
                if (i == 5) return "虚拟输入";
                if (i == 6) return "虚拟 AUX";
                if (i == 7) return "VAIO3";
            }
            else if (VmType == 1)
            {
                if (i == 0) return "硬件输入 1";
                if (i == 1) return "硬件输入 2";
                return "虚拟输入";
            }
            else
            {
                if (i <= 2) return "硬件输入 " + (i + 1);
                if (i == 3) return "虚拟输入";
                if (i == 4) return "虚拟 AUX";
            }
            return "扩展输入 " + (i + 1);
        }

        private string BusName(int i)
        {
            if (VmType == 1) return i == 0 ? "主音量 A1" : "B1";
            if (VmType == 3)
            {
                if (i == 0) return "主音量 A1";
                if (i <= 4) return "A" + (i + 1);
                return "B" + (i - 3);
            }
            // Banana / Pro: A1 A2 A3 B1 B2
            if (i == 0) return "主音量 A1";
            if (i <= 2) return "A" + (i + 1);
            if (i <= 4) return "B" + (i - 2);
            return "扩展总线 " + (i + 1);
        }

        public bool TryGetFloat(string name, out float value)
        {
            value = 0f;
            if (!Connected || _getFloat == null) return false;
            IntPtr p = Marshal.StringToHGlobalAnsi(name);
            try { return _getFloat(p, out value) == 0; }
            catch { return false; }
            finally { Marshal.FreeHGlobal(p); }
        }

        public bool SetFloat(string name, float value)
        {
            if (!Connected || _setFloat == null) return false;
            IntPtr p = Marshal.StringToHGlobalAnsi(name);
            try { return _setFloat(p, value) == 0; }
            catch { return false; }
            finally { Marshal.FreeHGlobal(p); }
        }

        public string GetLabel(string paramName)
        {
            if (!Connected || _getString == null) return "";
            IntPtr name = Marshal.StringToHGlobalAnsi(paramName + ".Label");
            IntPtr buf = Marshal.AllocHGlobal(512);
            try
            {
                for (int i = 0; i < 512; i++) Marshal.WriteByte(buf, i, 0);
                if (_getString(name, buf) != 0) return "";
                string s = Marshal.PtrToStringAnsi(buf);
                return s == null ? "" : s.Trim();
            }
            catch { return ""; }
            finally
            {
                Marshal.FreeHGlobal(name);
                Marshal.FreeHGlobal(buf);
            }
        }

        public bool TryGetLevel(int type, int channel, out float value)
        {
            value = 0f;
            if (!Connected || _getLevel == null) return false;
            try { return _getLevel(type, channel, out value) == 0; }
            catch { return false; }
        }

        /// <summary>读取当前音量（来自后台线程维护的缓存）。</summary>
        public float GetGain(VmChannel c)
        {
            if (c == null) return 0f;
            lock (_sync)
            {
                float v;
                if (_gainCache.TryGetValue(c.GainParam, out v)) return v;
            }
            return 0f;
        }

        /// <summary>
        /// 写入审计日志：记录每一次真正写进 Voicemeeter 的参数。
        /// 出问题时（例如「我的音量怎么变了」）可以直接看这个文件对账。
        /// </summary>
        public static void LogWrite(string text)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VMixer");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "vm.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text + Environment.NewLine);
            }
            catch { }
        }

        /// <summary>设置音量（写入会排队，由后台线程调用 API 完成）。</summary>
        public bool SetGain(VmChannel c, float db)
        {
            if (c == null || !Connected) return false;
            if (db < (float)FaderControl.MinDb) db = (float)FaderControl.MinDb;
            if (db > (float)FaderControl.MaxDb) db = (float)FaderControl.MaxDb;
            lock (_sync) { _gainCache[c.GainParam] = db; }
            LogWrite("SET " + c.GainParam + " = " + db.ToString("0.00"));
            Enqueue(delegate { PushFloat(c.GainParam, db); });
            return true;
        }

        public bool GetMute(VmChannel c, out bool muted)
        {
            muted = false;
            if (c == null) return false;
            lock (_sync)
            {
                bool v;
                if (_muteCache.TryGetValue(c.MuteParam, out v)) { muted = v; return true; }
            }
            return false;
        }

        public bool SetMute(VmChannel c, bool muted)
        {
            if (c == null || !Connected) return false;
            lock (_sync) { _muteCache[c.MuteParam] = muted; }
            LogWrite("SET " + c.MuteParam + " = " + (muted ? "1 (mute)" : "0 (unmute)"));
            Enqueue(delegate { PushFloat(c.MuteParam, muted ? 1f : 0f); });
            return true;
        }

        /// <summary>
        /// 通道电平。刻意不实现：VBVMR_GetLevel 会把电平值写进共享缓冲区的增益槽位，
        /// 而本程序需要周期性泵参数，电平值会被当成增益提交，损坏用户的音量设置。
        /// </summary>
        public float GetLevel(VmChannel c)
        {
            return 0f;
        }

        /// <summary>在工作线程上直接读取 Voicemeeter 里的真实值（用于自检）。</summary>
        public float ReadActual(VmChannel c)
        {
            if (c == null) return 0f;
            float result = 0f;
            using (ManualResetEventSlim done = new ManualResetEventSlim(false))
            {
                Enqueue(delegate
                {
                    float v;
                    if (TryGetFloat(c.GainParam, out v)) result = v;
                    done.Set();
                });
                done.Wait(2000);
            }
            return result;
        }

        /// <summary>等待后台线程把参数真正写入 Voicemeeter。</summary>
        public void WaitApplied(int milliseconds)
        {
            Thread.Sleep(milliseconds);
        }
    }
}
