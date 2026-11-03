using System;

namespace FluxForge.Presentation.Flow.Visual
{
    /// <summary>
    /// 表现层的 8 位 RGB 颜色（0–255）。
    ///
    /// 刻意不引用 <c>UnityEngine.Color32</c>：<c>src/Presentation/Flow</c> 必须零 Unity 依赖，
    /// 才能在测试项目里直接编译、脱离游戏跑单测。Mod/Visual 渲染层需要时自行把 R/G/B 转成
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

        /// <summary>
        /// 线性插值：<c>a * (1 - t) + b * t</c>，每通道按 double 运算后四舍五入。
        /// 公式与 <c>render_final.mix</c> 完全一致，**不要**改写成 <c>a + (b - a) * t</c>：
        /// 浮点误差会在个别通道上差 1。
        /// </summary>
        internal static FlowColor Mix(in FlowColor a, in FlowColor b, double t)
        {
            return new FlowColor(
                RoundToByte(a.R * (1d - t) + b.R * t),
                RoundToByte(a.G * (1d - t) + b.G * t),
                RoundToByte(a.B * (1d - t) + b.B * t));
        }

        /// <summary>
        /// double → byte：先银行家舍入（恰为 .5 时取偶），再钳到 0–255。
        /// 负权重等脏数据可能把结果推出色域，钳制保证渲染层永远拿到合法 byte。
        /// 全仓库只有这一份落点规则，颜色相关实现一律调用这里。
        /// </summary>
        internal static byte RoundToByte(double value)
        {
            double rounded = Math.Round(value);
            if (rounded <= 0d)
            {
                return 0;
            }

            if (rounded >= 255d)
            {
                return 255;
            }

            return (byte)rounded;
        }

        /// <summary>
        /// double → byte：**截断**取整，对齐游戏原版。
        ///
        /// 原版 <c>MetaHelper.ColorToHex</c> 把 <c>Color</c>（0–1 float）隐式转
        /// <c>Color32</c>，Unity 那个转换是 <c>(byte)(Clamp01(c) * 255f)</c>，
        /// 而 C# 的 <c>(byte)</c> 对浮点是向零截断，不是四舍五入。
        /// 液体混合色必须走这里，否则每个通道可能比原版大 1。
        /// </summary>
        internal static byte TruncateToByte(double value)
        {
            if (value <= 0d)
            {
                return 0;
            }

            if (value >= 255d)
            {
                return 255;
            }

            return (byte)value;
        }
    }
}
