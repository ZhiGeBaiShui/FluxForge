using System;
using System.Collections.Generic;
using System.Linq;
using FluxForge;
using FluxForge.Mod;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// Mod 装置目录的契约：key 集合、单例适配器、按物品查定义，以及适配器创建的装置类型与接口。
    /// </summary>
    public class DeviceCatalogTests
    {
        private static readonly string[] ExpectedKeys =
        {
            "collector",
            "filter",
            "temperature",
            "ice_block",
            "turn",
            "l_right_down",
            "t_junction",
            "double_emit",
        };

        private static readonly Dictionary<string, Type> ExpectedDeviceTypes =
            new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                { "collector", typeof(CollectorDevice) },
                { "filter", typeof(FilterDevice) },
                { "temperature", typeof(TemperatureDevice) },
                { "ice_block", typeof(IceBlockDevice) },
                { "turn", typeof(TurnDevice) },
                { "l_right_down", typeof(LRightDownDevice) },
                { "t_junction", typeof(TJunctionDevice) },
                { "double_emit", typeof(DoubleEmitDevice) },
            };

        public static IEnumerable<object[]> RegisteredKeys
        {
            get
            {
                foreach (string key in ExpectedKeys)
                {
                    yield return new object[] { key };
                }
            }
        }

        /// <summary>目录是唯一装置出生入口：按 key 找定义，由单例适配器创建。</summary>
        private static object Create(string key)
        {
            return DeviceAdapterCatalog.Get(key).Adapter.Create(new GameItem());
        }

        [Fact]
        public void Keys_ExposeExactlyTheKnownDeviceSet_InOrdinalOrder()
        {
            string[] expected = ExpectedKeys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, DeviceAdapterCatalog.Keys.ToArray());
        }

        [Fact]
        public void TryGet_ReportsKnownAndUnknownKeys()
        {
            DeviceDefinition definition;

            Assert.True(DeviceAdapterCatalog.TryGet("ice_block", out definition));
            Assert.Equal("ice_block", definition.Key);

            Assert.False(DeviceAdapterCatalog.TryGet("missing", out definition));
            Assert.Null(definition);
        }

        [Theory]
        [MemberData(nameof(RegisteredKeys))]
        public void Get_ReturnsSameDefinitionAndAdapterSingleton(string key)
        {
            DeviceDefinition first = DeviceAdapterCatalog.Get(key);
            DeviceDefinition second = DeviceAdapterCatalog.Get(key);

            Assert.Same(first, second);
            Assert.Same(first.Adapter, second.Adapter);
            Assert.Equal(key, first.Key);
        }

        [Fact]
        public void FindFor_ReturnsTaggedDefinitions_SortedByKey()
        {
            GameItem item = new GameItem();
            item.state.GetTag("temperature");
            item.modifiedState.GetTag("collector");
            item.state.GetTag("ice_block").Enable();

            List<DeviceDefinition> first = DeviceAdapterCatalog.FindFor(item);
            List<DeviceDefinition> second = DeviceAdapterCatalog.FindFor(item);

            string[] expected = { "collector", "ice_block", "temperature" };
            Assert.Equal(expected, first.Select(definition => definition.Key).ToArray());
            Assert.Equal(expected, second.Select(definition => definition.Key).ToArray());
            Assert.All(first, definition =>
                Assert.Same(DeviceAdapterCatalog.Get(definition.Key).Adapter, definition.Adapter));

            Assert.Empty(DeviceAdapterCatalog.FindFor(new GameItem()));
        }

        [Theory]
        [MemberData(nameof(RegisteredKeys))]
        public void Adapter_Create_ReturnsExpectedFreshDevice(string key)
        {
            DeviceDefinition definition = DeviceAdapterCatalog.Get(key);
            GameItem item = new GameItem();

            object first = definition.Adapter.Create(item);
            object second = definition.Adapter.Create(item);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(ExpectedDeviceTypes[key], first.GetType());
            Assert.Equal(ExpectedDeviceTypes[key], second.GetType());
            Assert.NotSame(first, second);
        }

        [Fact]
        public void IceBlock_ImplementsReactionAndBothNotifications()
        {
            Assert.True(typeof(IFlowDevice).IsAssignableFrom(typeof(IceBlockDevice)));
            Assert.True(typeof(IFlowNotifier).IsAssignableFrom(typeof(IceBlockDevice)));
            Assert.False(typeof(IFlowRouter).IsAssignableFrom(typeof(IceBlockDevice)));
        }

        [Fact]
        public void Collector_ImplementsRoutingAndChangedOnly()
        {
            Assert.True(typeof(IFlowRouter).IsAssignableFrom(typeof(CollectorDevice)));
            Assert.True(typeof(IFlowNotifier).IsAssignableFrom(typeof(CollectorDevice)));
            Assert.False(typeof(IFlowDevice).IsAssignableFrom(typeof(CollectorDevice)));
        }

        [Theory]
        [InlineData(typeof(FilterDevice))]
        [InlineData(typeof(TemperatureDevice))]
        public void ReactionOnlyDevices_ExposeNoRoutingOrNotifications(Type deviceType)
        {
            Assert.True(typeof(IFlowDevice).IsAssignableFrom(deviceType));
            Assert.False(typeof(IFlowRouter).IsAssignableFrom(deviceType));
            Assert.False(typeof(IFlowNotifier).IsAssignableFrom(deviceType));
        }

        [Theory]
        [InlineData("turn")]
        [InlineData("l_right_down")]
        [InlineData("t_junction")]
        [InlineData("double_emit")]
        public void SplitDevices_AreRoutingOnly(string key)
        {
            object device = Create(key);

            Assert.True(device is IFlowRouter);
            Assert.False(device is IFlowDevice);
            Assert.True(device is IFlowNotifier);
        }

        [Fact]
        public void NotificationInterface_UsesSenderfulActions()
        {
            Assert.Equal(
                typeof(Action<IFlowNotifier>),
                typeof(IFlowNotifier).GetEvent("Changed").EventHandlerType);
            Assert.Equal(
                typeof(Action<IFlowNotifier>),
                typeof(IFlowNotifier).GetEvent("Exhausted").EventHandlerType);
        }

        [Theory]
        [InlineData("turn")]
        [InlineData("t_junction")]
        [InlineData("double_emit")]
        public void SplitVariants_ExposeFacing(string key)
        {
            SplitDevice split = Assert.IsAssignableFrom<SplitDevice>(Create(key));

            Assert.Equal(0, split.Facing);

            split.Facing = 2;
            Assert.Equal(2, split.Facing);
        }
    }
}
