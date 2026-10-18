namespace ProbablyBagMod
{
    /// <summary>
    /// 温度改变：最基础的单一反应。
    ///
    /// 反应是「纯属性变换」——只认 FlowProperties，不认识 FlowEntity / 装置 / 运动层。
    /// 调用方（装置）负责把要改的属性取出来传入，反应只做一件事：施加一次温度增减。
    ///
    /// 这一类同时是所有「单一/简单反应」的样板：
    ///   - 静态类，不实例化（反应无自己的状态，状态全在流源属性和装置存量上）
    ///   - 签名自定义，只收自己需要的参数（这里：属性 + 增减量）
    ///   - 不做校验之外的任何事，边界情况（delta=0 等）天然合法
    /// </summary>
    public static class TemperatureChangeReaction
    {
        /// <summary>
        /// 施加一次温度改变：Temperature += delta。
        /// </summary>
        /// <param name="properties">要修改的流源属性。会被直接原地修改（调用方即装置，本就要改它）。</param>
        /// <param name="delta">温度增减量。正数升温、负数降温、0 无变化。</param>
        public static void React(FlowProperties properties, double delta)
        {
            properties.Temperature += delta;
        }
    }
}
