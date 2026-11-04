using System;

namespace FluxForge
{
    /// <summary>
    /// 一次音效播报。SoundId 是逻辑层定义的语义音效 ID，
    /// 不是 AudioClip 名字；由 Mod 层映射成实际音效。
    /// </summary>
    public readonly struct FlowAudioEvent
    {
        public readonly string SoundId;
        public readonly object Source;
        public readonly double Amount;

        public FlowAudioEvent(string soundId, object source, double amount)
        {
            SoundId = soundId;
            Source = source;
            Amount = amount;
        }
    }

    /// <summary>
    /// 逻辑层音效播报入口。装置在动作发生时调用 Play，
    /// Mod 层订阅 Played 后负责 Unity 播放。
    /// </summary>
    public static class FlowAudio
    {
        public static event Action<FlowAudioEvent> Played;

        public static void Play(string soundId, object source, double amount = 0d)
        {
            Played?.Invoke(new FlowAudioEvent(soundId, source, amount));
        }
    }
}
