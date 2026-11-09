using FluxForge.Mod;
using Xunit;

namespace FluxForge.Tests
{
    public class FlowStockForeignTests
    {
        [Fact]
        public void UnknownLiquid_IsAggregatedAndOriginalTagsRemain()
        {
            GameItem item = new GameItem();
            SetLiquid(item, FlowTagNaming.Water, 100);
            SetLiquid(item, "CURRENT_PART_CUSTOM_JUICE", 700);
            SetLiquid(item, "CURRENT_PART_CUSTOM_SYRUP", 300);

            FlowProperties properties = FlowStock.Read(item);

            Assert.Equal(100d, properties.Get(FlowProps.Water));
            Assert.Equal(1000d, properties.Get(FlowProps.Foreign));
            Assert.Equal(1100d, properties.SubstanceTotal);

            FlowStock.Write(item, properties);

            Assert.True(item.state.dict.ContainsKey("CURRENT_PART_CUSTOM_JUICE"));
            Assert.True(item.state.dict.ContainsKey("CURRENT_PART_CUSTOM_SYRUP"));
            Assert.Equal(
                1000d,
                FlowStock.Read(item).Get(FlowProps.Foreign));
        }

        [Fact]
        public void AfterEmit_UnknownTagsAreZeroedNotDeleted()
        {
            GameItem item = new GameItem();
            SetLiquid(item, "CURRENT_PART_CUSTOM_JUICE", 700);

            FlowProperties cleared = new FlowProperties();
            FlowStock.WriteAfterEmit(item, cleared);

            Assert.True(item.state.dict.ContainsKey("CURRENT_PART_CUSTOM_JUICE"));
            Assert.Equal(0, item.state.dict["CURRENT_PART_CUSTOM_JUICE"].GetInt());
            Assert.Equal(0d, FlowStock.Total(item));
        }

        [Fact]
        public void DeviceStockWithoutRawTags_UsesForeignAggregateTag()
        {
            GameItem item = new GameItem();
            FlowProperties stock = new FlowProperties();
            stock.Set(FlowProps.Foreign, 500d);

            FlowStock.Write(item, stock);

            Assert.True(item.state.dict.ContainsKey(FlowTagNaming.ForeignLiquid));
            Assert.Equal(500d, FlowStock.Total(item));
        }

        private static void SetLiquid(GameItem item, string tag, int amount)
        {
            TagState state = item.state.GetTag(tag);
            state.Enable();
            state.SetInt(amount);

            TagState modified = item.modifiedState.GetTag(tag);
            modified.Enable();
            modified.SetInt(amount);
        }
    }
}
