using UnityEngine;

namespace FluxForge.Mod
{
    /// <summary>
    /// 音效服务：订阅逻辑层 FlowAudio.Played，把语义音效 ID 映射成游戏实际音频。
    ///
    /// 订阅 / 退订由 <see cref="FFModEntry"/> 在启用与卸载时调用。
    /// </summary>
    public static class DeviceAudioService
    {
        private static AudioSource _source;

        public static void Initialize()
        {
            GameObject host = new GameObject("FluxForge.DeviceAudio");
            UnityEngine.Object.DontDestroyOnLoad(host);

            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.bypassEffects = true;
            _source.bypassListenerEffects = true;
            _source.bypassReverbZones = true;
            _source.volume = 1f;

            FlowAudio.Played += OnPlayed;
        }

        public static void Shutdown()
        {
            FlowAudio.Played -= OnPlayed;

            if (_source != null)
            {
                UnityEngine.Object.Destroy(_source.gameObject);
                _source = null;
            }
        }

        private static void OnPlayed(FlowAudioEvent evt)
        {
            GameItem item = DeviceRegistry.GetItemForDevice(evt.Source);
            Resolve(item, evt.SoundId);
        }

        /// <summary>
        /// 按语义音效 ID 选实际音频并播放。
        /// item 参与反查，逐物品音效差异以后从这里扩展。
        /// </summary>
        private static void Resolve(GameItem item, string soundId)
        {
            string audioId;

            switch (soundId)
            {
                case FlowSoundIds.FilterCleaned:
                    // 自定义音效：一次过滤动作只播一次
                    audioId = DeviceAudioClips.FilterChemical;
                    break;

                case FlowSoundIds.Heated:
                    // 自定义音效：加热元件通电
                    audioId = DeviceAudioClips.Heater;
                    break;

                case FlowSoundIds.Cooled:
                    // 自定义音效：制冷元件启动
                    audioId = DeviceAudioClips.Cooler;
                    break;

                case FlowSoundIds.Absorbed:
                    audioId = AudioHelper.POUR_WATER2;
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

            AudioClip clip = AudioManager.Instance.audioDirectory.GetAudioClip(audioId);
            _source.PlayOneShot(clip);
        }
    }
}

