namespace ProbablyBagMod
{
    /// <summary>
    /// 冰块装置：控制反应的装置类（反应 = 改变流源属性，不改变流源去向）。
    ///
    /// 自身持有两个存量属性（与流源属性同名同义，但彼此独立）：
    ///   · <see cref="Moisture"/>    水分含量（单位：µL 微升，与原版液体容器一致）
    ///   · <see cref="Temperature"/> 温度（单位：摄氏度；原版无温度系统，本 mod 自定）
    ///
    /// 与运动层的关系：
    ///   流源撞上本装置时，运动层调用 <see cref="TryReact"/>。
    ///   返回 true  = 发生了反应 → 流源停在装置格上（每次经过只触发一次）；
    ///   返回 false = 无条件反应（或存量已耗尽）→ 流源正常穿过。
    ///
    /// 本类不认识 GameItem / TagSystem / 存档——那是 mod / 适配层的事。
    /// </summary>
    public sealed class IceBlockDevice : IFlowDevice
    {
        /// <summary>水分含量（µL）。1 mL = 1000 µL。</summary>
        public double Moisture { get; set; }

        /// <summary>温度（摄氏度）。</summary>
        public double Temperature { get; set; }

        public IceBlockDevice(double moisture, double temperature)
        {
            Moisture = moisture;
            Temperature = temperature;
        }

        /// <summary>反应入口：流源撞上冰块时由运动层调用。</summary>
        /// <param name="properties">流源属性，实现里可直接原地修改。</param>
        /// <returns>是否真的发生了反应（true → 该帧阻挡）。</returns>
        public bool TryReact(FlowProperties properties)
        {
            // TODO: 反应逻辑待定（吸热 / 自融 / 平衡判定）
            return false;
        }
    }
}
