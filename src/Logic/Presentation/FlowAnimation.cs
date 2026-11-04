using System;

namespace FluxForge
{
    /// <summary>
    /// 一次动画播报。AnimationId 是逻辑层定义的语义动画 ID，
    /// Mod 层决定实际贴图/缩放/销毁效果。
    /// </summary>
    public readonly struct FlowAnimationEvent
    {
        public readonly string AnimationId;
        public readonly object Source;
        public readonly double Amount;

        public FlowAnimationEvent(string animationId, object source, double amount)
        {
            AnimationId = animationId;
            Source = source;
            Amount = amount;
        }
    }

    /// <summary>逻辑层动画播报入口；当前 Mod 层只有空壳消费者。</summary>
    public static class FlowAnimation
    {
        public static event Action<FlowAnimationEvent> Played;

        public static void Play(string animationId, object source, double amount = 0d)
        {
            Played?.Invoke(new FlowAnimationEvent(animationId, source, amount));
        }
    }
}
