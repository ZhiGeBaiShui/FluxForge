namespace FluxForge.Mod
{
    /// <summary>
    /// 音效服务：订阅逻辑层 FlowAudio.Played，把语义音效 ID 映射成游戏实际音频。
    ///
    /// 订阅 / 退订由 <see cref="FFModEntry"/> 在启用与卸载时调用。
    /// </summary>
    public static class DeviceAudioService
    {
        public static void Initialize()
        {
            FlowAudio.Played += OnPlayed;
        }

        public static void Shutdown()
        {
            FlowAudio.Played -= OnPlayed;
        }

        private static void OnPlayed(FlowAudioEvent evt)
        {
            Resolve(evt.SoundId);
        }

        /// <summary>
        /// 按语义音效 ID 选实际音频并播放。
        /// </summary>
        private static void Resolve(string soundId)
        {
            string audioId;

            switch (soundId)
            {
                case FlowSoundIds.FilterCleaned:
                    // 过滤装置使用独立的裁短净化音
                    audioId = DeviceAudioClips.FilterCrystal;
                    break;

                case FlowSoundIds.Heated:
                    // 自定义音效：加热元件通电
                    audioId = DeviceAudioClips.Heater;
                    break;

                case FlowSoundIds.Cooled:
                    // 自定义音效：制冷元件启动
                    audioId = DeviceAudioClips.Cooler;
                    break;

                case FlowSoundIds.SpeedUp:
                    audioId = DeviceAudioClips.SpeedUp;
                    break;

                case FlowSoundIds.SpeedDown:
                    audioId = DeviceAudioClips.SpeedDown;
                    break;

                case FlowSoundIds.Absorbed:
                    audioId = DeviceAudioClips.ReceiverReceive;
                    break;

                case FlowSoundIds.Extracted:
                    audioId = DeviceAudioClips.ColdTrapBase;
                    break;

                case FlowSoundIds.ExtractedCross:
                    audioId = DeviceAudioClips.ColdTrapCross;
                    break;

                case FlowSoundIds.CentrifugeSpin:
                    audioId = DeviceAudioClips.CentrifugeSpin;
                    break;

                case FlowSoundIds.ItemPlaced:
                    audioId = DeviceAudioClips.ItemPlacement;
                    break;

                case FlowSoundIds.Melted:
                    audioId = AudioHelper.DEBRIS_IMPACT;
                    break;

                case FlowSoundIds.Exhausted:
                    audioId = AudioHelper.GLASS_DROP;
                    break;

                default:
                    return;
            }

            AudioManager.Instance.Play2(audioId);
        }
    }
}

