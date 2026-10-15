namespace ProbablyBagMod
{
    /// <summary>
    /// 装置接口：由「装置的独特类」实现。
    ///
    /// 流源进入装置时，由运动层调用本方法，把流源属性作为实参传进来；
    /// 装置自己判断条件是否满足、是否发生反应（可能不发生），
    /// 接口不做任何行为约定、不返回结果。
    /// </summary>
    public interface IFlowDevice
    {
        /// <summary>
        /// 尝试对流源发生反应。是否真的反应由实现类自己判断。
        /// </summary>
        /// <param name="properties">流源的属性（实参）。</param>
        void TryReact(FlowProperties properties);
    }
}
