namespace FluxForge
{
    /// <summary>
    /// 反应装置接口：由「装置的独特类」实现。
    ///
    /// 流源进入装置时由运动层调用，传入完整流源；装置可以读取属性、位置、
    /// 速度等上下文，并自行决定是否发生反应。
    /// </summary>
    public interface IFlowDevice
    {
        /// <summary>
        /// 尝试对流源发生反应。是否真的反应由实现类自己判断。
        /// </summary>
        /// <param name="entity">完整流源；实现可原地修改其属性。</param>
        /// <returns>true = 本帧发生了反应（运动层据此截停流源）。</returns>
        bool TryReact(FlowEntity entity);
    }
}
