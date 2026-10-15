namespace ProbablyBagMod
{
    /// <summary>
    /// 流源的运动数据：方向 + 速度。
    ///
    /// 方向用竞赛编程（ICPC）经典的 dx/dy 表示法：0-3 的整数索引，
    /// 通过 <see cref="Dx"/> / <see cref="Dy"/> 两张表查位移。
    ///   索引 0 = 上   1 = 右   2 = 下   3 = 左
    /// 索引 +1 即顺时针（向右）旋转 90°，装置旋转时入口/出口方向跟着转。
    ///
    /// 声明为 class（而非 struct）：接口按引用把它交给装置原地修改，
    /// struct 会传值拷贝，装置的改动静默丢失。
    /// </summary>
    public class FlowMotionData
    {
        /// <summary>方向索引对应的 X 位移表（与 <see cref="Dy"/> 同序使用）。</summary>
        public static readonly int[] Dx = { 0, 1, 0, -1 };

        /// <summary>方向索引对应的 Y 位移表。</summary>
        public static readonly int[] Dy = { 1, 0, -1, 0 };

        /// <summary>方向索引（0=上 1=右 2=下 3=左）。</summary>
        public int Direction;

        /// <summary>速度（格/帧）。</summary>
        public float Speed;
    }
}
