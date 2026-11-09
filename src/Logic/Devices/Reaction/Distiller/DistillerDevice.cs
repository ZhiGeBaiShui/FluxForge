using System;

namespace FluxForge
{
    /// <summary>蒸馏所需的纯逻辑资源成员；电池身份和存档由适配层处理。
    /// 不管理计时、入口或出口。调用方负责保存抽出的水。
    /// </summary>
    public sealed class DistillerDevice : IFlowNotifier
    {
        public int Energy { get; private set; }
        public double WaterPerEnergy { get; set; }
        public event Action<IFlowNotifier> Changed;
        public event Action<IFlowNotifier> Exhausted { add { } remove { } }

        public void LoadEnergy(int batteryEnergy)
        {
            Energy = batteryEnergy;
        }

        public double Distill(FlowProperties properties, double freeWaterCapacity)
        {
            if (Energy <= 0 || WaterPerEnergy <= 0 || freeWaterCapacity <= 0)
                return 0;
            double extracted = DistillationReaction.React(properties,
                Math.Min(freeWaterCapacity, Energy * WaterPerEnergy));
            if (extracted <= 0) return 0;
            int cost = Math.Min(Energy, (int)Math.Ceiling(extracted / WaterPerEnergy));
            Energy -= cost;
            Changed?.Invoke(this);
            return extracted;
        }
    }
}
