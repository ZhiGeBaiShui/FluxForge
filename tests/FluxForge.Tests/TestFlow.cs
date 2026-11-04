using FluxForge;

namespace FluxForge.Tests
{
    /// <summary>
    /// 测试用的属性构造入口：按属性键写水与温度，不再依赖已删除的
    /// <c>FlowProperties(double, double)</c> 兼容构造。
    /// </summary>
    public static class TestFlow
    {
        public static FlowProperties Make(double water, double temperature = 0d)
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Water, water);
            properties.Set(FlowProps.Temperature, temperature);
            return properties;
        }
    }
}
