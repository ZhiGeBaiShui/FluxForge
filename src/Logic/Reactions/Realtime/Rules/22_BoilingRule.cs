using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 沸腾蒸发：原版 <c>ChemicalHelper.HandleBoilingReaction</c>。
    ///
    /// 只有 <see cref="FlowBoilingTable"/> 里登记的液体（水 100、酒精 78.37、
    /// lipobond_agent 55）会蒸发；温度严格高于沸点才处理，每轮移除 2%，
    /// 至少 100 µL，且不超过当前存量。
    ///
    /// 原版遍历的是全部液体并靠 liquid.isEvaporative 过滤；表里只保留
    /// 可蒸发条目，两者等价。原版无论是否真的蒸发都返回 1（成功）；
    /// <see cref="React"/> 无返回值，这里按原版位置执行写入即可。
    /// </summary>
    public sealed class BoilingRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 220; }
        }

        public override void React(FlowProperties properties)
        {
            // 原版读 GetDouble；沸点 78.37 这类小数不能被 int 截断。
            double temperature = properties.Get(FlowProps.Temperature);
            IReadOnlyList<KeyValuePair<FlowPropertyKey, double>> entries = FlowBoilingTable.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                KeyValuePair<FlowPropertyKey, double> entry = entries[i];
                if (!(temperature > entry.Value))
                {
                    continue;
                }

                int originalAmount = FlowReactionMath.GetInt(properties, entry.Key);
                if (originalAmount <= 0)
                {
                    continue;
                }

                int amountToRemove = (int)((double)originalAmount * 0.02d);
                amountToRemove = FlowReactionMath.Max(amountToRemove, 100);
                if (amountToRemove > originalAmount)
                {
                    amountToRemove = originalAmount;
                }

                FlowReactionMath.SetInt(
                    properties,
                    entry.Key,
                    originalAmount - amountToRemove);
            }
        }
    }
}
