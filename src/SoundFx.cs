using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace VMixer
{
    /// <summary>
    /// 调节音量时的听觉反馈（类似 Win10 调整音量时的「叮」声）。
    ///
    /// 不依赖任何外部音频库或音频文件：用 winmm 的 waveOut 自己合成提示音，
    /// 音量按所调节通道的增益缩放，这样「听到的就是你调到的那个音量」。
    ///
    /// 音色用「铃铛分音」模型：基频 + 若干非整数倍泛音（2 / 2.76 / 4.07 / 5.43），
    /// 高次泛音衰减更快，听起来是干净的金属「叮」，而不是刺耳的蜂鸣。
    /// </summary>
    internal static class SoundFx
    {
        private const int SampleRate = 44100;
        private const uint WAVE_MAPPER = 0xFFFFFFFF;
        private const uint WHDR_DONE = 0x00000001;

        /// <summary>四种音色，顺序与配置文件里的 ToneStyle 一致。</summary>
        public const int StyleClear = 0;   // 清脆叮（默认，最接近系统提示音）
        public const int StyleBright = 1;  // 明亮叮
        public const int StyleSoft = 2;    // 柔和钟
        public const int StyleBlip = 3;    // 短促滴

        private static readonly string[] StyleNames = new string[] { "清脆叮", "明亮叮", "柔和钟", "短促滴" };

        public static string StyleName(int style)
        {
            if (style < 0 || style >= StyleNames.Length) return StyleNames[0];
            return StyleNames[style];
        }

        public static int StyleCount { get { return StyleNames.Length; } }

        private static double[] _tone;           // 预生成的提示音波形
        private static int _toneStyle = -1;
        private static readonly object _sync = new object();

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr hwo, uint deviceId, ref WAVEFORMATEX format,
            IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr hwo);
        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr header, uint size);
        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr header, uint size);
        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr hwo, IntPtr header, uint size);
        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr hwo);

        /// <summary>把 dB 换算成线性幅度（-60 dB 以下视为静音）。</summary>
        private static double Amplitude(double db)
        {
            if (db <= -60.0) return 0.0;
            if (db >= 0.0) return 1.0;
            return Math.Pow(10.0, db / 20.0);
        }

        /// <summary>铃铛分音的比例与强度（经典管钟比例）。</summary>
        private static readonly double[] BellRatios = new double[] { 1.0, 2.0, 2.76, 4.07, 5.43 };
        private static readonly double[] BellAmps = new double[] { 1.0, 0.34, 0.20, 0.11, 0.05 };

        /// <summary>按音色生成一段归一化的波形（-1 ~ 1）。</summary>
        private static double[] BuildTone(int style)
        {
            double f0;
            double dur;
            double dec0;
            int partials;

            switch (style)
            {
                case StyleBright:
                    f0 = 1318.51;   // E6
                    dur = 0.40;
                    dec0 = 0.17;
                    partials = 5;
                    break;
                case StyleSoft:
                    f0 = 880.0;     // A5
                    dur = 0.55;
                    dec0 = 0.30;
                    partials = 4;
                    break;
                case StyleBlip:
                    f0 = 2093.0;    // C7
                    dur = 0.16;
                    dec0 = 0.055;
                    partials = 2;
                    break;
                default:
                    f0 = 1046.50;   // C6
                    dur = 0.45;
                    dec0 = 0.21;
                    partials = 5;
                    break;
            }

            int n = (int)(SampleRate * dur);
            double[] buf = new double[n];
            double dt = 1.0 / SampleRate;

            for (int i = 0; i < n; i++)
            {
                double t = i * dt;
                double s = 0.0;
                for (int k = 0; k < partials; k++)
                {
                    double ratio;
                    double amp;
                    if (style == StyleBlip)
                    {
                        ratio = (k == 0) ? 1.0 : 2.0;
                        amp = (k == 0) ? 1.0 : 0.18;
                    }
                    else
                    {
                        ratio = BellRatios[k];
                        amp = BellAmps[k];
                    }
                    // 高次分音衰减更快：金属味更干净，不会糊成嗡嗡声
                    double dec = dec0 * Math.Pow(0.42, k);
                    s += amp * Math.Exp(-t / dec) * Math.Sin(2.0 * Math.PI * f0 * ratio * t);
                }

                // 起音 3 ms，避免爆音
                double atk = t / 0.003;
                if (atk > 1.0) atk = 1.0;
                s *= atk;

                // 收尾 30 ms 淡出
                double rem = (n - i) * dt;
                if (rem < 0.03) s *= rem / 0.03;

                buf[i] = s;
            }

            // 归一化（峰值为 0.72，留足余量）
            double peak = 0.0;
            for (int i = 0; i < n; i++)
            {
                double a = buf[i] < 0 ? -buf[i] : buf[i];
                if (a > peak) peak = a;
            }
            if (peak > 0.000001)
            {
                double k = 0.72 / peak;
                for (int i = 0; i < n; i++) buf[i] *= k;
            }
            return buf;
        }

        /// <summary>按目标增益缩放一段波形并转成 16bit PCM。</summary>
        private static byte[] ToPcm(double[] buf, double amp)
        {
            byte[] data = new byte[buf.Length * 2];
            for (int i = 0; i < buf.Length; i++)
            {
                double v = buf[i] * amp * 32767.0;
                int iv = (int)v;
                if (iv > 32767) iv = 32767;
                if (iv < -32768) iv = -32768;
                data[i * 2] = (byte)(iv & 0xFF);
                data[i * 2 + 1] = (byte)((iv >> 8) & 0xFF);
            }
            return data;
        }

        /// <summary>播放一次提示音，音量按 db 缩放。调用不阻塞 UI。</summary>
        public static void Play(double db, int style)
        {
            double amp = Amplitude(db);
            if (amp <= 0.002) return;

            try
            {
                byte[] pcm;
                lock (_sync)
                {
                    if (_tone == null || _toneStyle != style)
                    {
                        _tone = BuildTone(style);
                        _toneStyle = style;
                    }
                    pcm = ToPcm(_tone, amp);
                }
                ThreadPool.QueueUserWorkItem(delegate { PlayRaw(pcm); });
            }
            catch { }
        }

        /// <summary>把指定音色的提示音导出成 WAV（调试 / 波形比对用）。</summary>
        public static byte[] RenderWav(int style)
        {
            double[] buf;
            lock (_sync)
            {
                if (_tone == null || _toneStyle != style)
                {
                    _tone = BuildTone(style);
                    _toneStyle = style;
                }
                buf = _tone;
            }
            byte[] pcm = ToPcm(buf, 1.0);
            int dataLen = pcm.Length;
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            using (System.IO.BinaryWriter w = new System.IO.BinaryWriter(ms))
            {
                w.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write((int)(36 + dataLen));
                w.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
                w.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write((int)16);
                w.Write((short)1);                 // PCM
                w.Write((short)1);                 // 单声道
                w.Write((int)SampleRate);
                w.Write((int)(SampleRate * 2));    // 字节率
                w.Write((short)2);                 // 块对齐
                w.Write((short)16);                // 位深
                w.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write((int)dataLen);
                w.Write(pcm);
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>自检：能否用当前格式打开波形输出设备（0 = 正常）。</summary>
        public static int Probe()        {
            IntPtr hwo = IntPtr.Zero;
            WAVEFORMATEX fmt = new WAVEFORMATEX();
            fmt.wFormatTag = 1;
            fmt.nChannels = 1;
            fmt.nSamplesPerSec = SampleRate;
            fmt.wBitsPerSample = 16;
            fmt.nBlockAlign = 2;
            fmt.nAvgBytesPerSec = (uint)(SampleRate * 2);

            int r;
            try
            {
                r = waveOutOpen(out hwo, WAVE_MAPPER, ref fmt, IntPtr.Zero, IntPtr.Zero, 0);
            }
            catch { return -1; }
            if (r == 0)
            {
                try { waveOutClose(hwo); } catch { }
            }
            return r;
        }

        private static void PlayRaw(byte[] pcm)
        {
            IntPtr hwo = IntPtr.Zero;
            IntPtr buffer = IntPtr.Zero;
            IntPtr header = IntPtr.Zero;
            WAVEFORMATEX fmt = new WAVEFORMATEX();
            fmt.wFormatTag = 1;              // PCM
            fmt.nChannels = 1;
            fmt.nSamplesPerSec = SampleRate;
            fmt.wBitsPerSample = 16;
            fmt.nBlockAlign = 2;
            fmt.nAvgBytesPerSec = (uint)(SampleRate * 2);

            try
            {
                if (waveOutOpen(out hwo, WAVE_MAPPER, ref fmt, IntPtr.Zero, IntPtr.Zero, 0) != 0) return;

                buffer = Marshal.AllocHGlobal(pcm.Length);
                Marshal.Copy(pcm, 0, buffer, pcm.Length);

                int hdrSize = Marshal.SizeOf(typeof(WAVEHDR));
                header = Marshal.AllocHGlobal(hdrSize);
                WAVEHDR hdr = new WAVEHDR();
                hdr.lpData = buffer;
                hdr.dwBufferLength = (uint)pcm.Length;
                Marshal.StructureToPtr(hdr, header, false);

                if (waveOutPrepareHeader(hwo, header, (uint)hdrSize) != 0) return;
                if (waveOutWrite(hwo, header, (uint)hdrSize) != 0) return;

                // 等播放结束（轮询 WHDR_DONE，最多 2 秒）
                int waited = 0;
                while (waited < 2000)
                {
                    WAVEHDR cur = (WAVEHDR)Marshal.PtrToStructure(header, typeof(WAVEHDR));
                    if ((cur.dwFlags & WHDR_DONE) != 0) break;
                    Thread.Sleep(15);
                    waited += 15;
                }

                waveOutReset(hwo);
                waveOutUnprepareHeader(hwo, header, (uint)hdrSize);
            }
            catch { }
            finally
            {
                try { if (hwo != IntPtr.Zero) waveOutClose(hwo); } catch { }
                try { if (header != IntPtr.Zero) Marshal.FreeHGlobal(header); } catch { }
                try { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); } catch { }
            }
        }
    }
}
