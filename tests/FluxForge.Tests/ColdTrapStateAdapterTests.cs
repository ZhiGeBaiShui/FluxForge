using FluxForge;
using FluxForge.Mod;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 冷阱适配器：四个配置 tag + 原版容量 tag 构造装置，
    /// 抽提存量按全部流源属性 tag 读写。
    /// </summary>
    public class ColdTrapStateAdapterTests
    {
        private static GameItem NewConfiguredItem(int capacity)
        {
            GameItem item = new GameItem();

            TagState temperatureDelta = item.state.GetTag(FlowTags.TemperatureDelta);
            temperatureDelta.Enable();
            temperatureDelta.SetDouble(30d);

            TagState threshold = item.state.GetTag(FlowTags.ColdTrapPhaseThreshold);
            threshold.Enable();
            threshold.SetDouble(20d);

            TagState baseExtraction = item.state.GetTag(FlowTags.ColdTrapBaseExtraction);
            baseExtraction.Enable();
            baseExtraction.SetInt(5);

            TagState efficiency = item.state.GetTag(FlowTags.ColdTrapEfficiency);
            efficiency.Enable();
            efficiency.SetDouble(3d);

            TagState capacityTag = item.state.GetTag(FlowTags.LiquidContainerCapacity);
            capacityTag.Enable();
            capacityTag.SetInt(capacity);

            item.SyncModifiedState();
            return item;
        }

        private static FlowEntity NewEntity(double temperature, double water, double sugar)
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Temperature, temperature);
            properties.Set(FlowProps.Water, water);
            properties.Set(FlowProps.Sugar, sugar);

            return new FlowEntity(
                new FlowPos(0d, 0d),
                new FlowMotionData(1, 2.5d),
                properties);
        }

        [Fact]
        public void Create_UsesConfigTagsAndVanillaCapacity()
        {
            GameItem item = NewConfiguredItem(2);
            ColdTrapStateAdapter adapter = new ColdTrapStateAdapter();

            ColdTrapDevice device = Assert.IsType<ColdTrapDevice>(adapter.Create(item));

            FlowEntity entity = NewEntity(20d, 1000d, 5d);
            device.TryReact(entity);

            // 基础量 5，但原版容量 tag 只有 2 µL：只能抽 2。
            Assert.Equal(2d, device.Extracted.Get(FlowProps.Sugar));
            Assert.Equal(2d, device.Extracted.SubstanceTotal);
        }

        [Fact]
        public void SaveAndLoad_RoundTripsExtractedStock()
        {
            GameItem item = NewConfiguredItem(1000);
            ColdTrapStateAdapter adapter = new ColdTrapStateAdapter();
            ColdTrapDevice first = Assert.IsType<ColdTrapDevice>(adapter.Create(item));

            FlowEntity entity = NewEntity(20d, 1000d, 10d);
            first.TryReact(entity);

            Assert.True(adapter.Save(item, first));
            Assert.Equal(5, item.state.dict[FlowTagNaming.ForProperty("sugar", FlowPropertyKind.Liquid)].GetInt());
            Assert.Equal(
                20d,
                item.state.dict[FlowTagNaming.ContainerTemperature].GetDouble());
            Assert.False(adapter.Save(item, first));

            // 模拟 ItemTagCommit：Save 写 state 后刷新 modifiedState 快照。
            item.SyncModifiedState();

            ColdTrapDevice second = Assert.IsType<ColdTrapDevice>(adapter.Create(item));
            adapter.Load(item, second);

            Assert.Equal(5d, second.Extracted.Get(FlowProps.Sugar));
            Assert.Equal(20d, second.Extracted.Get(FlowProps.Temperature));

            // 配置 tag 不被存量回写覆盖。
            Assert.Equal(
                30d,
                item.modifiedState.dict[FlowTags.TemperatureDelta].GetDouble());
        }
    }
}
