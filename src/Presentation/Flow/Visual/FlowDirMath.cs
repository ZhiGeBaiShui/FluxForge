namespace ProbablyBagMod.Presentation.Flow.Visual
{
    /// <summary>
    /// 方向枚举 → 单位步长 / 运动反方向。**纯函数、零 Unity 依赖**。
    ///
    /// 坐标约定与网格一致：X 向右增大、Y 向下增大，所以 Up = (0, -1)。
    /// 越界枚举值按 Right 处理，与 <see cref="FlowTierMap.DirFrom"/> 的兜底一致。
    ///
    /// 逻辑层的 <c>FlowMotionData</c> 另有一份方向表（运动语义的一部分），
    /// 本次重构刻意不合并——那边是「物理位移」，这里是「视觉朝向」。
    /// </summary>
    public static class FlowDirMath
    {
        /// <summary>方向在 X 轴上的单位步长：Left = -1，Up/Down = 0，Right（及越界）= 1。</summary>
        public static int StepX(FlowDir dir)
        {
            switch (dir)
            {
                case FlowDir.Up:
                case FlowDir.Down:
                    return 0;
                case FlowDir.Left:
                    return -1;
                default:
                    return 1;
            }
        }

        /// <summary>方向在 Y 轴上的单位步长：Up = -1（Y 向下），Down = 1，Left/Right（及越界）= 0。</summary>
        public static int StepY(FlowDir dir)
        {
            switch (dir)
            {
                case FlowDir.Up:
                    return -1;
                case FlowDir.Down:
                    return 1;
                default:
                    return 0;
            }
        }

        /// <summary>运动反方向的 X 分量：<see cref="StepX"/> 取反。</summary>
        public static int BackwardX(FlowDir dir)
        {
            return -StepX(dir);
        }

        /// <summary>运动反方向的 Y 分量：<see cref="StepY"/> 取反。</summary>
        public static int BackwardY(FlowDir dir)
        {
            return -StepY(dir);
        }
    }
}
