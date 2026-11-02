using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 可按属性键读写的一份流源状态。
    ///
    /// <see cref="FlowProperties"/> 与各装置都实现它：
    /// 映射层只需要「读一个槽 / 写一个槽」，不必按装置类型分支。
    ///
    /// <see cref="Keys"/> 声明该状态真正拥有哪些属性；<see cref="FlowStock"/> 只遍历
    /// 这些 key 做 tag 映射，因此装置可以只实现自己需要的水量/温度等槽位。
    /// </summary>
    public interface IFlowState
    {
        /// <summary>该状态拥有的属性集合；不在集合里的 key 不参与 tag 读写。</summary>
        IReadOnlyList<FlowPropertyKey> Keys { get; }

        /// <summary>读一个属性槽。</summary>
        double Get(FlowPropertyKey key);

        /// <summary>写一个属性槽。</summary>
        void Set(FlowPropertyKey key, double value);
    }
}
