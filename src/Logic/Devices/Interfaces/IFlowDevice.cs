namespace FluxForge
{
    /// <summary>
    /// 反应装置接口：由「装置的独特类」实现。
    ///
    /// 流源进入装置时由运动层调用，把流源属性作为实参传进来；
    /// 装置自己判断条件是否满足、是否发生反应（可能不发生）。
    /// </summary>
    public interface IFlowDevice
    {
        /// <summary>
        /// 尝试对流源发生反应。是否真的反应由实现类自己判断。
        /// </summary>
        /// <param name="properties">流源的属性（实参），实现里可直接原地修改。</param>
        /// <returns>true = 本帧发生了反应（运动层据此截停流源）。</returns>
        bool TryReact(FlowProperties properties);
    }
}
