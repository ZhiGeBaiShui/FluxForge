using System;
using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 从原版液体容器取液，并入自己的存量（<see cref="FlowProperties"/>）。
    ///
    /// 泛化点：合并不是逐字段手写，而是交给 <see cref="FlowProperties.Merge"/> 按
    /// <c>PropertyRule</c> 处理（水分 = 相加、温度 = 按质量加权）。
    /// 所以本类**不认识具体字段**——扩展属性时：
    ///   · 加字段 + 加分合规则 + 加 tag 绑定 → 本类零改动；
    ///   · 只有当新属性**来自原版容器**时，才需要补 <see cref="ReadSource"/>。
    /// </summary>
    internal static class LiquidTransfer
    {
        /// <summary>来源没有温度信息时的假定温度。</summary>
        public const double DefaultTemperature = 25d;

        private const string LiquidContainerTag = "LIQUID_CONTAINER_TAG";

        /// <summary>原版液体容器判定（按 tag，不看具体物品 id）。</summary>
        public static bool IsLiquidContainer(Il2Cpp.GameItem item)
        {
            return item != null
                && item.Pointer != IntPtr.Zero
                && item.IsTag(LiquidContainerTag);
        }

        /// <summary>把来源容器整份并入 <paramref name="self"/>，并抽干来源。</summary>
        public static void Accept(Il2Cpp.GameItem self, Il2Cpp.GameItem source)
        {
            FlowProperties incoming = ReadSource(source);
            if (incoming.Moisture <= 0d)
            {
                return;
            }

            FlowProperties stock = ReadStock(self);
            FlowProperties merged = stock.Merge(new List<FlowProperties> { incoming });

            FlowTagMap.Write(self.state, merged);
            SyncDevice(self, merged);

            // 抽干来源。用原版 Remove：它按物质占比分摊、自动钳到 0，并刷新容器外观
            Il2Cpp.WaterHelper.Remove(source, (int)incoming.Moisture);
        }

        /// <summary>
        /// 原版容器 → 属性。**新增"来自容器的属性"时只改这里。**
        /// 目前容器只提供两个量：总量（1/1000 mL）与温度。
        /// </summary>
        private static FlowProperties ReadSource(Il2Cpp.GameItem container)
        {
            return new FlowProperties(
                Il2Cpp.WaterHelper.GetTotalVolume(container),
                LiquidStock.Temperature(container, DefaultTemperature));
        }

        /// <summary>tag → 属性。经 <see cref="FlowTagMap"/> 批量读，不认识具体字段。</summary>
        private static FlowProperties ReadStock(Il2Cpp.GameItem self)
        {
            FlowProperties stock = new FlowProperties(0d, DefaultTemperature);
            FlowTagMap.Read(self.state, stock);

            // tag 不存在时 FlowTagMap 会写 0；存量温度 0 没有实际意义，回落到常温
            if (stock.Temperature == 0d)
            {
                stock.Temperature = DefaultTemperature;
            }

            return stock;
        }

        /// <summary>让装置镜像跟上 tag，否则下一次接管会把旧值灌回去。</summary>
        private static void SyncDevice(Il2Cpp.GameItem self, FlowProperties merged)
        {
            EmitterDevice device = DeviceRegistry.Get<EmitterDevice>(self.Pointer);
            if (device == null)
            {
                return;
            }

            device.Stock.Moisture = merged.Moisture;
            device.Stock.Temperature = merged.Temperature;
        }
    }
}
