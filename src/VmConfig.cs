using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VMixer
{
    /// <summary>极简 INI 配置（保存在 %APPDATA%\VMixer\config.ini）。</summary>
    public class VmConfig
    {
        public string DllPath = "";
        public bool AutoStart = false;
        public bool VolumeKeys = true;
        public bool ShowOnStart = false;
        public bool CloseToTray = true;
        public bool OpaquePanel = false;   // true = 不透明面板；false = 半透明亚克力
        public bool SoundFeedback = true;  // 调节音量时是否播放提示音
        public int ToneStyle = 0;          // 提示音音色：0 清脆叮 / 1 明亮叮 / 2 柔和钟 / 3 短促滴
        public string TipChannel = "";      // 托盘气泡显示哪条通道（空 = 不显示）

        public List<string> Visible = new List<string>();          // 通道 key，例如 S0 / B0
        public List<string> Order = new List<string>();            // 通道显示顺序（主面板按这个排）
        public Dictionary<string, string> Names = new Dictionary<string, string>();
        public Dictionary<string, string> Icons = new Dictionary<string, string>();  // 通道 key -> 图标设置

        public static string FilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VMixer");
                return Path.Combine(dir, "config.ini");
            }
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                Dictionary<string, Dictionary<string, string>> data = ParseIni(FilePath);

                Dictionary<string, string> g;
                if (data.TryGetValue("General", out g))
                {
                    DllPath = Get(g, "DllPath", "");
                    AutoStart = GetBool(g, "AutoStart", false);
                    VolumeKeys = GetBool(g, "VolumeKeys", true);
                    ShowOnStart = GetBool(g, "ShowOnStart", false);
                    CloseToTray = GetBool(g, "CloseToTray", true);
                    OpaquePanel = GetBool(g, "OpaquePanel", false);
                    SoundFeedback = GetBool(g, "SoundFeedback", true);
                    ToneStyle = GetInt(g, "ToneStyle", 0);
                    // 兼容旧配置里的 CornerChannel（右下角浮窗已移除，语义变成托盘气泡）
                    TipChannel = Get(g, "TipChannel", "");
                    if (TipChannel.Length == 0) TipChannel = Get(g, "CornerChannel", "");
                }

                Visible.Clear();
                Dictionary<string, string> c;
                if (data.TryGetValue("Channels", out c))
                {
                    string list = Get(c, "Visible", "");
                    string[] parts = list.Split(',');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string t = parts[i].Trim();
                        if (t.Length > 0) Visible.Add(t);
                    }
                }

                Names.Clear();
                Dictionary<string, string> n;
                if (data.TryGetValue("Names", out n))
                {
                    foreach (KeyValuePair<string, string> kv in n) Names[kv.Key] = kv.Value;
                }

                Icons.Clear();
                Dictionary<string, string> ic;
                if (data.TryGetValue("Icons", out ic))
                {
                    foreach (KeyValuePair<string, string> kv in ic)
                        if (IconStore.IsBuiltin(kv.Value)) Icons[kv.Key] = kv.Value;
                }

                Order.Clear();
                Dictionary<string, string> od;
                if (data.TryGetValue("Order", out od))
                {
                    string list = Get(od, "Order", "");
                    string[] parts = list.Split(',');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string t = parts[i].Trim();
                        if (t.Length > 0) Order.Add(t);
                    }
                    // 旧版本没有拖动排序，保存下来的是「输入条全在前、输出总线全在后」
                    // 这种机器生成的顺序（主音量会掉到最后）。这不算用户的意愿，
                    // 迁移时丢掉，让主音量回到最前面。用户之后拖过的顺序会保留。
                    if (!GetBool(od, "OrderCustom", false) && IsLegacyAutoOrder(Order))
                        Order.Clear();
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[General]");
                sb.AppendLine("DllPath=" + DllPath);
                sb.AppendLine("AutoStart=" + (AutoStart ? "1" : "0"));
                sb.AppendLine("VolumeKeys=" + (VolumeKeys ? "1" : "0"));
                sb.AppendLine("ShowOnStart=" + (ShowOnStart ? "1" : "0"));
                sb.AppendLine("CloseToTray=" + (CloseToTray ? "1" : "0"));
                sb.AppendLine("OpaquePanel=" + (OpaquePanel ? "1" : "0"));
                sb.AppendLine("SoundFeedback=" + (SoundFeedback ? "1" : "0"));
                sb.AppendLine("ToneStyle=" + ToneStyle);
                sb.AppendLine("TipChannel=" + TipChannel);
                sb.AppendLine();
                sb.AppendLine("[Channels]");
                sb.AppendLine("Visible=" + string.Join(",", Visible.ToArray()));
                sb.AppendLine();
                sb.AppendLine("[Order]");
                sb.AppendLine("Order=" + string.Join(",", Order.ToArray()));
                sb.AppendLine("OrderCustom=1");
                sb.AppendLine();
                sb.AppendLine("[Names]");
                foreach (KeyValuePair<string, string> kv in Names)
                    sb.AppendLine(kv.Key + "=" + kv.Value);
                sb.AppendLine();
                sb.AppendLine("[Icons]");
                foreach (KeyValuePair<string, string> kv in Icons)
                    sb.AppendLine(kv.Key + "=" + kv.Value);

                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        /// <summary>
        /// 判断保存的顺序是不是旧版本自动生成的「输入条全在前、输出总线全在后」。
        /// 是的话它只是机器排的（主音量会掉到末尾），不算用户自己拖出来的。
        /// </summary>
        private static bool IsLegacyAutoOrder(List<string> order)
        {
            if (order == null || order.Count < 2) return false;
            bool seenBus = false;
            int lastS = -1, lastB = -1;
            for (int i = 0; i < order.Count; i++)
            {
                string k = order[i];
                if (k.Length < 2) return false;
                int idx;
                if (!int.TryParse(k.Substring(1), out idx)) return false;
                if (k[0] == 'S')
                {
                    if (seenBus) return false;      // 总线之后又出现输入条 = 手动排过
                    if (idx != lastS + 1) return false;
                    lastS = idx;
                }
                else if (k[0] == 'B')
                {
                    if (idx != lastB + 1) return false;
                    seenBus = true;
                    lastB = idx;
                }
                else return false;
            }
            return seenBus;
        }

        private static Dictionary<string, Dictionary<string, string>> ParseIni(string path)
        {
            Dictionary<string, Dictionary<string, string>> result =
                new Dictionary<string, Dictionary<string, string>>();
            string section = "";
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    if (!result.ContainsKey(section))
                        result[section] = new Dictionary<string, string>();
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                if (!result.ContainsKey(section))
                    result[section] = new Dictionary<string, string>();
                result[section][key] = val;
            }
            return result;
        }

        private static string Get(Dictionary<string, string> d, string key, string def)
        {
            string v;
            if (d.TryGetValue(key, out v)) return v;
            return def;
        }

        private static bool GetBool(Dictionary<string, string> d, string key, bool def)
        {
            string v;
            if (!d.TryGetValue(key, out v)) return def;
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetInt(Dictionary<string, string> d, string key, int def)
        {
            string v;
            int r;
            if (!d.TryGetValue(key, out v)) return def;
            if (!int.TryParse(v, out r)) return def;
            if (r < 0) return 0;
            if (r >= SoundFx.StyleCount) return def;
            return r;
        }
    }
}
