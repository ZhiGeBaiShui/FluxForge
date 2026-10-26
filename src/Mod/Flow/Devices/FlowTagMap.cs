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
                FlowTags.WriteInt(state, FlowTags.Stock, (int)moisture.Moisture);
            }

            ITemperatureState temperature = device as ITemperatureState;
            if (temperature != null)
            {
                FlowTags.WriteFloat(
                    state, FlowTags.FlowTemperature, (float)temperature.Temperature);
            }
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
