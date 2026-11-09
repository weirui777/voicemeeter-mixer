using System;
using System.Collections.Generic;
using System.Drawing;

namespace VMixer
{
    /// <summary>
    /// 通道图标：只提供一组内置字形（扬声器 / 麦克风 / 波形 / 音乐 / 耳机 / 总线 / 屏幕 / 唱片），
    /// 配置里存的是 "b:speaker" 这种短标记，不涉及任何外部图片文件。
    /// </summary>
    public static class IconStore
    {
        public const string PrefixBuiltin = "b:";

        private static readonly string[] _builtinNames = new string[]
        {
            "扬声器", "麦克风", "波形", "音乐", "耳机", "总线", "屏幕", "唱片"
        };

        private static readonly string[] _builtinKeys = new string[]
        {
            "speaker", "microphone", "wave", "music", "headphones", "bus", "monitor", "disc"
        };

        public static string[] BuiltinNames { get { return _builtinNames; } }
        public static string[] BuiltinKeys { get { return _builtinKeys; } }

        /// <summary>内置字形的显示名。</summary>
        public static string BuiltinLabel(string key)
        {
            for (int i = 0; i < _builtinKeys.Length; i++)
                if (_builtinKeys[i] == key) return _builtinNames[i];
            return key;
        }

        /// <summary>由内置 key 得到字形类型；不认识就返回 false。</summary>
        public static bool TryBuiltin(string key, out GlyphKind kind)
        {
            switch (key)
            {
                case "speaker": kind = GlyphKind.Speaker; return true;
                case "microphone": kind = GlyphKind.Microphone; return true;
                case "wave": kind = GlyphKind.Wave; return true;
                case "music": kind = GlyphKind.Music; return true;
                case "headphones": kind = GlyphKind.Headphones; return true;
                case "bus": kind = GlyphKind.Bus; return true;
                case "monitor": kind = GlyphKind.Monitor; return true;
                case "disc": kind = GlyphKind.Disc; return true;
            }
            kind = GlyphKind.Speaker;
            return false;
        }

        /// <summary>配置值是不是内置字形。</summary>
        public static bool IsBuiltin(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith(PrefixBuiltin);
        }

        /// <summary>取配置值对应的内置字形；不是内置就返回 false。</summary>
        public static bool TryGetBuiltinGlyph(string value, out GlyphKind kind)
        {
            kind = GlyphKind.Speaker;
            if (!IsBuiltin(value)) return false;
            return TryBuiltin(value.Substring(PrefixBuiltin.Length), out kind);
        }

        /// <summary>通道没有自定义图标时，按通道类型挑一个默认字形。</summary>
        public static GlyphKind DefaultKind(VmChannel c)
        {
            if (c == null) return GlyphKind.Speaker;
            if (c.IsInput)
            {
                if (c.Index <= 2) return GlyphKind.Microphone;
                if (c.Index == 3) return GlyphKind.Music;
                return GlyphKind.Wave;
            }
            return c.IsMaster ? GlyphKind.Speaker : GlyphKind.Headphones;
        }

        /// <summary>取通道最终要画的字形：用户指定的内置字形优先，没有才按类型自动挑。</summary>
        public static GlyphKind ResolveKind(VmConfig cfg, VmChannel c)
        {
            if (cfg != null && c != null)
            {
                string v;
                if (cfg.Icons.TryGetValue(c.Key, out v))
                {
                    GlyphKind k;
                    if (TryGetBuiltinGlyph(v, out k)) return k;
                }
            }
            return DefaultKind(c);
        }
    }
}
