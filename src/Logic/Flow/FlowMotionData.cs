namespace ProbablyBagMod
{
    /// <summary>
    /// 流源的运动数据：方向 + 速度。
    ///
    /// 方向用 0-3 的整数索引，通过 <see cref="Dx"/> / <see cref="Dy"/> 两张表查位移。
    ///   索引 0 = 上   1 = 右   2 = 下   3 = 左
    /// 索引 +1 即顺时针（向右）旋转 90°，装置旋转时入口/出口方向跟着转。
    ///
    /// <para>
    /// ⚠ Y 轴方向：游戏网格的 Y 轴是**向下**的（<c>GridPixelElement.Validate</c> 里
    /// <c>anchoredPosition = (x, -y)</c>），<c>minY</c> 越大越靠屏幕下方。
    /// 所以「上」对应 Y 减小，<see cref="Dy"/> 的 0 位是 -1。
    /// 若按数学惯例把「上」写成 +1，流源在界面上会朝反方向跑。
    /// </para>
    ///
    /// 声明为 class（而非 struct）：接口按引用把它交给装置原地修改，
    /// struct 会传值拷贝，装置的改动静默丢失。
    /// </summary>
    public class FlowMotionData
    {
        public FlowMotionData(int direction, double speed)
        {
            Direction = direction;
            Speed = speed;
        }

        /// <summary>方向索引对应的 X 位移表（与 <see cref="Dy"/> 同序使用）。</summary>
        private static readonly int[] DxTable = { 0, 1, 0, -1 };

        /// <summary>方向索引对应的 Y 位移表。Y 轴向下，故「上」是 -1。</summary>
        private static readonly int[] DyTable = { -1, 0, 1, 0 };

        /// <summary>
        /// 某个方向在 X 轴上的位移（-1 / 0 / 1）。
        /// 用方法而不是公开数组：数组元素是可变，任何一处改一格都会影响所有机器的运动。
        /// </summary>
        public static int StepX(int direction)
        {
            return DxTable[direction];
        }

        /// <summary>某个方向在 Y 轴上的位移。-1 = 向上（Y 减小）。</summary>
        public static int StepY(int direction)
        {
            return DyTable[direction];
        }

        /// <summary>方向索引（0=上 1=右 2=下 3=左）。</summary>
        public int Direction;

        /// <summary>速度（格/秒）。</summary>
        public double Speed;

        /// <summary>
        /// 按当前方向和速度，算一帧（deltaTime 秒）的位移分量。
        /// 查表封装在此——调用方无需感知 Dx / Dy 表。
        /// </summary>
        public (double Dx, double Dy) FrameOffset(double deltaTime)
        {
            double distance = Speed * deltaTime;
            return (DxTable[Direction] * distance, DyTable[Direction] * distance);
        }
    }
}
