namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 装置状态 ↔ 物品 tag 的唯一映射。
    ///
    /// 按**属性**分派（<see cref="IMoistureState"/> / <see cref="ITemperatureState"/>），
    /// 不按装置类型：加一种新装置只要实现对应的接口，本文件一行都不用改。
    ///
    /// 不在这里做显示换算（µL → g 之类）——那是 items.json 的 statusLines 的事。
    /// </summary>
    internal static class FlowTagMap
    {
        /// <summary>装置状态 → tag。存量写 int 槽、温度写 float 槽。</summary>
        public static void Write(Il2Cpp.TagSystem state, object device)
        {
            if (state == null || device == null)
            {
                return;
            }

            IMoistureState moisture = device as IMoistureState;
            if (moisture != null)
            {
                FlowTags.WriteInt(state, FlowTags.Stock, ToStockUnits(moisture.Moisture));
            }

            ITemperatureState temperature = device as ITemperatureState;
            if (temperature != null)
            {
                FlowTags.WriteFloat(
                    state, FlowTags.FlowTemperature, (float)temperature.Temperature);
            }
        }

        /// <summary>
        /// 存量 double → tag 的 int 槽位。
        ///
        /// 这里**不是**容量上限（我们不设上限，原版倒多少收多少），而是 int 的物理边界：
        /// 单位是 1/1000 mL，int 上限约 2,147,483 mL。越界后强转在 unchecked 下结果未定义
        /// （通常是负数），所以钳住并留一条日志——真到这一步说明已经灌了两千多升。
        /// </summary>
        private static int ToStockUnits(double moisture)
        {
            if (moisture <= 0d)
            {
                return 0;
            }

            if (moisture < int.MaxValue)
            {
                return (int)moisture;
            }

            MelonLoader.MelonLogger.Warning(
                "[流源] 存量超过 int 槽位上限（约 2147 L），已钳到上限");
            return int.MaxValue;
        }

        /// <summary>tag → 装置状态。只在读档接管时调用一次。</summary>
        public static void Read(Il2Cpp.TagSystem state, object device)
        {
            if (state == null || device == null)
            {
                return;
            }

            IMoistureState moisture = device as IMoistureState;
            if (moisture != null)
            {
                moisture.Moisture = FlowTags.ReadInt(state, FlowTags.Stock, 0);
            }

            ITemperatureState temperature = device as ITemperatureState;
            if (temperature != null)
            {
                temperature.Temperature = FlowTags.ReadFloat(state, FlowTags.FlowTemperature, 0f);
            }
        }
    }
}
