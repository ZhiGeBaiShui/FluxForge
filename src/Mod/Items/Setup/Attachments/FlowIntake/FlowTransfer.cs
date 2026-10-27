using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 把来源液体容器的内容整份并入目标物品自己的存量。
    ///
    /// 泛化点：合并不是逐字段手写，而是交给 <see cref="FlowProperties.Merge"/> 按
    /// <c>PropertyRule</c> 处理（水分 = 相加、温度 = 按质量加权）。
    /// 所以本类**不认识具体字段**——扩展属性时：
    ///   · 加字段 + 加分合规则 + 加 tag 绑定 → 本类零改动；
    ///   · 只有当新属性**来自原版容器**时，才需要补 <see cref="ReadSource"/>。
    /// </summary>
    internal static class FlowTransfer
    {
        /// <summary>把来源容器整份并入 <paramref name="self"/>，并抽干来源。</summary>
        public static void Accept(Il2Cpp.GameItem self, Il2Cpp.GameItem source)
        {
            FlowProperties incoming = ReadSource(source);
            if (incoming.SubstanceTotal <= 0d)
            {
                return;
            }

            FlowProperties stock = ReadStock(self);
            FlowProperties merged = stock.Merge(new List<FlowProperties> { incoming });

            // 只写 tag：装置侧在下一次发射时会用 FlowStock 重新读一遍，
            // 不需要在这里反向同步任何装置镜像。
            FlowStock.Write(self, merged);

            // 抽干来源。容器走原版 Remove：按物质占比分摊、自动钳到 0，并刷新容器外观
            FlowStock.Take(source);
        }

        /// <summary>
        /// 原版容器 → 属性。容器侧每一行怎么取由 <see cref="FlowTagMap"/> 的表决定。
        /// </summary>
        private static FlowProperties ReadSource(Il2Cpp.GameItem container)
        {
            return FlowStock.Read(container);
        }

        /// <summary>tag → 属性。经 <see cref="FlowTagMap"/> 批量读，不认识具体字段。</summary>
        private static FlowProperties ReadStock(Il2Cpp.GameItem self)
        {
            FlowProperties stock = new FlowProperties(0d, 0d);
            FlowStock.ReadInto(self, stock);
            return stock;
        }
    }
}
