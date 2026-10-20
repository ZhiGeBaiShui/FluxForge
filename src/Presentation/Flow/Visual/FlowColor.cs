namespace ProbablyBagMod.Presentation.Flow.Visual
{
    /// <summary>
    /// 纯逻辑层的 8 位 RGB 颜色（0–255）。
    ///
    /// 刻意不引用 <c>UnityEngine.Color32</c>：<c>src/Presentation/Flow</c> 必须零 Unity 依赖，
    /// 才能在测试项目里直接编译、脱离游戏跑单测。UGUI 层需要时自行把 R/G/B 转成
    /// <c>Color32</c>（两者字段布局一致，转换是一次三个 byte 的赋值）。
    /// </summary>
    public readonly struct FlowColor
    {
        /// <summary>红通道，0–255。</summary>
        public readonly byte R;

        /// <summary>绿通道，0–255。</summary>
        public readonly byte G;

        /// <summary>蓝通道，0–255。</summary>
        public readonly byte B;

        /// <summary>用三个 0–255 通道构造颜色。</summary>
        public FlowColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }
    }
}
