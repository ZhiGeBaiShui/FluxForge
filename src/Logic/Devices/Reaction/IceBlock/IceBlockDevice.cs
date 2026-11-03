using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 冰块装置：控制反应的装置类（反应 = 改变流源属性，不改变流源去向）。
    ///
    /// 自身持有两个存量属性（与流源属性同名同义，但彼此独立）：
    ///   · <see cref="Water"/>    冰含量，同时用作物质量（1 mL 水 ↔ 1 g 冰）
    ///   · <see cref="Temperature"/> 温度（单位：摄氏度；沿用原版 CONTAINER_TEMP_TAG 约定，
    ///     本 mod 让流源也携带它）
    ///
    /// 对外以 <see cref="IFlowState"/> 暴露：映射层按属性键读写，
    /// 其它槽位读 0、写忽略。
    ///
    /// 与运动层的关系：
    ///   流源撞上本装置时，运动层调用 <see cref="TryReact"/>。
    ///   返回 true  = 发生了反应 → 流源停在装置格上（每次经过只触发一次）；
    ///   返回 false = 没有发生反应（冰已化完 / 流源不含水）→ 流源正常穿过。
    ///
    /// 融化模型：流源能带走多少冰，取决于**流源自己携带的水量**——
    ///   最多带走自身质量的一半（<see cref="MeltRatio"/>），且不超过冰块剩余量。
    ///   拿走的冰计入流源水量（1:1），温度按两者混合前的**总液体质量**配比加权。
    ///   冰被拿空则播报 <see cref="Exhausted"/>，由订阅方把物品从载体上移除。
    ///
    /// 为什么按流源的质量而不是按冰块剩余量的比例：冰是有限的，水是流动的。
    ///   一杯水浇不化整块冰，一桶水可以。若按冰块的一半来算，
    ///   任意小的流源都能反复切掉一半，冰块永远化不完，也就永远触发不了销毁。
    ///
    /// 本类不认识 GameItem / TagSystem / 存档——那是 mod / 适配层的事。
    /// </summary>
    public sealed class IceBlockDevice
        : IFlowDevice, IFlowNotifier
    {
        /// <summary>流源最多带走自身质量的比例（0.5 = 一半）。</summary>
        private const double MeltRatio = 0.5d;

        /// <inheritdoc/>
        public event Action<IFlowNotifier> Changed;

        /// <inheritdoc/>
        public event Action<IFlowNotifier> Exhausted;

        /// <summary>冰含量。1 mL 水对应 1 g 冰，所以它同时也是质量。</summary>
        public double Water { get; set; }

        /// <summary>温度（摄氏度）。</summary>
        public double Temperature { get; set; }

        /// <summary>目录工厂用：空冰块（0 冰、0 ℃），实际状态随后由 tag 回灌覆盖。</summary>
        public IceBlockDevice()
            : this(0d, 0d)
        {
        }

        public IceBlockDevice(double water, double temperature)
        {
            Water = water;
            Temperature = temperature;
        }

        /// <summary>冰块只拥有水量与温度两个槽位。</summary>
        private static readonly FlowPropertyKey[] OwnedKeys =
        {
            FlowProps.Water,
            FlowProps.Temperature,
        };

        /// <summary>只声明自己拥有的属性；tag 映射只遍历这两个 key。</summary>
        public IReadOnlyList<FlowPropertyKey> Keys
        {
            get { return OwnedKeys; }
        }

        /// <summary>读属性槽：只认水量与温度，其它槽位恒为 0。</summary>
        public double Get(FlowPropertyKey key)
        {
            if (key.Index == FlowProps.Water.Index)
            {
                return Water;
            }
            if (key.Index == FlowProps.Temperature.Index)
            {
                return Temperature;
            }
            return 0d;
        }

        /// <summary>写属性槽：只认水量与温度，其它槽位忽略。</summary>
        public void Set(FlowPropertyKey key, double value)
        {
            if (key.Index == FlowProps.Water.Index)
            {
                Water = value;
            }
            if (key.Index == FlowProps.Temperature.Index)
            {
                Temperature = value;
            }
        }

        /// <summary>反应入口：流源撞上冰块时由运动层调用。</summary>
        /// <param name="properties">流源属性，实现里可直接原地修改。</param>
        /// <returns>是否真的发生了反应（true → 该帧阻挡）。</returns>
        public bool TryReact(FlowProperties properties)
        {
            if (Water <= 0d)
            {
                return false;
            }

            // 冰块自己的规则：按流源水量决定最多带走多少冰。
            double capacity = properties.Get(FlowProps.Water) * MeltRatio;
            double melted = Math.Min(Water, capacity);
            if (melted <= 0d)
            {
                return false;
            }

            // 反应产生的属性作为一份独立 FlowProperties；按注册表合并进流源。
            FlowProperties reactionProperties = new FlowProperties();
            reactionProperties.Set(FlowProps.Water, melted);
            reactionProperties.Set(FlowProps.Temperature, Temperature);

            Water -= melted;
            properties.MergeFrom(reactionProperties);

            // 顺序不可调换：订阅方收到 Exhausted 就会把物品从网格里摘掉，
            // 摘掉之后就再也写不进 tag 了。Changed 必须在它之前把最终状态同步出去。
            Changed?.Invoke(this);

            // 只有真的化完了才请求销毁；没化完就留在原地等下一个流源
            if (Water <= 0d)
            {
                Exhausted?.Invoke(this);
            }

            return true;
        }
    }
}

