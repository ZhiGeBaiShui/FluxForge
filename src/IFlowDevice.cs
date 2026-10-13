using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 装置：流源经过时改变其方向 / 性质的行为载体。
    ///
    /// 装置是「运动 + 反应」的复合体，一个接口搞定：
    ///   返回值决定流源从哪些方向出去（运动）
    ///   载荷由装置自行 <see cref="IFlowPayload.Clone"/> / <see cref="IFlowPayload.Merge"/> 得到
    ///   （反应——装置调用这两个能力，却不必知道载荷是什么）
    /// </summary>
    public interface IFlowDevice
    {
        /// <summary>
        /// 流源进入本装置。
        /// </summary>
        /// <param name="incoming">进入的流源（含其载荷）。</param>
        /// <param name="entrySide">流源是从装置的哪一侧进来的。</param>
        /// <returns>
        /// 输出集合。返回 0 份 = 流源被吸收；1 份 = 直通 / 转向；
        /// 多份 = 分流。返回 null 视为空集合。
        /// </returns>
        IReadOnlyList<FlowOutput> OnFlowEnter(FlowToken incoming, Direction entrySide);
    }
}
