using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace FluxForge.Mod
{
    /// <summary>
    /// 自定义设备音效：把编译期嵌入的 WAV 解码成 AudioClip，登记到游戏的 AudioDirectory。
    ///
    /// 资源随 DLL 一起编译，缺失或格式不对都属于构建/资源错误，直接抛异常暴露，不做降级。
    /// </summary>
    public static class DeviceAudioClips
    {
        /// <summary>过滤介质被化学冲洗掉。</summary>
        public const string FilterChemical = "fluxforge.filter.chemical";

        /// <summary>加热元件通电工作。</summary>
        public const string Heater = "fluxforge.heater";

        /// <summary>制冷元件启动。</summary>
        public const string Cooler = "fluxforge.cooler";

        /// <summary>冷萃：未跨阈值的常态短点击。</summary>
        public const string ColdTrapBase = "fluxforge.coldtrap.base";

        /// <summary>冷萃：跨阈值的清脆玻璃/结晶音。</summary>
        public const string ColdTrapCross = "fluxforge.coldtrap.cross";

        /// <summary>过滤装置：独立裁短的清脆净化音。</summary>
        public const string FilterCrystal = "fluxforge.filter.crystal";

        /// <summary>变速装置：截断后的涡轮加速音。</summary>
        public const string SpeedUp = "fluxforge.speed.up";

        /// <summary>变速装置：降调、低通的涡轮减速音。</summary>
        public const string SpeedDown = "fluxforge.speed.down";

        /// <summary>发射器激活时的一次性发射音。</summary>
        public const string EmitterFire = "fluxforge.emitter.fire";

        /// <summary>接收装置吸收流源时的水滴音。</summary>
        public const string ReceiverReceive = "fluxforge.receiver.receive";

        /// <summary>离心机分离时的旋转音。</summary>
        public const string CentrifugeSpin = "fluxforge.centrifuge.spin";

        /// <summary>事件演示物品放置到网格时的瞬发音效。</summary>
        public const string ItemPlacement = "fluxforge.item.placement";

        /// <summary>加载并登记全部自定义音效；每次 OnEnable 调用一次。</summary>
        public static void Initialize()
        {
            string prefix = EmbeddedResources.FindPrefix(
                Assembly.GetExecutingAssembly(), "sfx_filter_chemical.wav");

            Register(prefix, "sfx_filter_chemical.wav", FilterChemical);
            Register(prefix, "sfx_heater.wav", Heater);
            Register(prefix, "sfx_cooler.wav", Cooler);
            Register(prefix, "sfx_coldtrap_base.wav", ColdTrapBase);
            Register(prefix, "sfx_coldtrap_cross.wav", ColdTrapCross);
            Register(prefix, "sfx_filter_crystal.wav", FilterCrystal);
            Register(prefix, "sfx_speed_up.wav", SpeedUp);
            Register(prefix, "sfx_speed_down.wav", SpeedDown);
            Register(prefix, "sfx_emitter_fire.wav", EmitterFire);
            Register(prefix, "sfx_receiver_receive.wav", ReceiverReceive);
            Register(prefix, "sfx_centrifuge_spin.wav", CentrifugeSpin);
            Register(prefix, "sfx_item_placement.wav", ItemPlacement);
        }

        private static void Register(string prefix, string fileName, string identifier)
        {
            AudioDirectory.RegisterModClip(
                identifier,
                Parse(EmbeddedResources.Read(prefix, fileName), identifier));
        }

        /// <summary>解析 16-bit PCM RIFF WAV；支持单/双声道。</summary>
        private static AudioClip Parse(byte[] wav, string identifier)
        {
            int fmt = FindChunk(wav, "fmt ");
            int data = FindChunk(wav, "data");
            int dataLength = ReadUInt32(wav, data - 4);

            int format = ReadUInt16(wav, fmt);
            int channels = ReadUInt16(wav, fmt + 2);
            int frequency = ReadUInt32(wav, fmt + 4);
            int bitsPerSample = ReadUInt16(wav, fmt + 14);

            if (format != 1 || bitsPerSample != 16 || (channels != 1 && channels != 2))
            {
                throw new InvalidDataException(
                    "[音效] " + identifier + " 不是 16-bit PCM 单/双声道 WAV：format="
                    + format + "，channels=" + channels + "，bits=" + bitsPerSample);
            }

            int sampleFrames = dataLength / (channels * 2);
            float[] samples = new float[sampleFrames * channels];

            for (int i = 0; i < samples.Length; i++)
            {
                short sample = (short)(wav[data + i * 2] | (wav[data + i * 2 + 1] << 8));
                samples[i] = sample / 32768f;
            }

            AudioClip clip = AudioClip.Create(identifier, sampleFrames, channels, frequency, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>沿 RIFF 块链查找目标块，返回块负载起始偏移；找不到直接抛异常。</summary>
        private static int FindChunk(byte[] wav, string id)
        {
            for (int pos = 12; pos + 8 <= wav.Length; )
            {
                int size = ReadUInt32(wav, pos + 4);

                if (Encoding.ASCII.GetString(wav, pos, 4) == id)
                {
                    return pos + 8;
                }

                // 块按偶数字节对齐，奇数长度块后有一个填充字节。
                pos += 8 + size + (size & 1);
            }

            throw new InvalidDataException("[音效] WAV 缺少 " + id.Trim() + " 块");
        }

        private static int ReadUInt16(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8);
        }

        private static int ReadUInt32(byte[] data, int offset)
        {
            return ReadUInt16(data, offset) | (ReadUInt16(data, offset + 2) << 16);
        }
    }
}
