using System;

namespace FluxForge
{
    /// <summary>
    /// 芬达受热分解：原版 <c>ChemicalHelper.HandleFantaHeat</c>。
    ///
    /// 温度达到 130°C 后，每轮只剩原来的 98%；低于 130°C 时数值不变。
    /// 原版在通过「有芬达」判断后总是写回，这里保持同样的写入点，
    /// 因此低温时也会把同一数值写一次（语义上等价）。
    /// </summary>
    public sealed class FantaHeatRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 190; }
        }

        public override void React(FlowProperties properties)
        {
            // 温度是标量，按 double 读取：原版读的就是 GetDouble。
            double temperature = properties.Get(FlowProps.Temperature);
            int fantaPart = FlowReactionMath.GetInt(properties, FlowProps.Fanta);
            if (fantaPart <= 0)
            {
                return;
            }

            if (temperature >= 130.0d)
            {
                // 原版：(int)Math.Max(0.0, fanta * 0.98)，双重截断。
                fantaPart = (int)Math.Max(0.0d, (double)fantaPart * 0.98d);
            }

            FlowReactionMath.SetInt(properties, FlowProps.Fanta, fantaPart);
        }
    }
}
