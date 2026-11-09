using System;

namespace FluxForge
{
    /// <summary>
    /// 反应装置的主动停顿时间。
    ///
    /// 只用于“反应确实发生”的分支；没有效果时不设置，避免流源无故停住。
    /// 上限 0.3 秒，保留 0.05 秒的最小值，保证低帧率下也能看出来。
    /// </summary>
    public static class FlowReactionDelay
    {
        public const double MaxSeconds = 0.3d;
        private const double MinSeconds = 0.05d;

        private static readonly Random Random = new Random();

        public static double NextSeconds()
        {
            return MinSeconds + Random.NextDouble() * (MaxSeconds - MinSeconds);
        }
    }
}
