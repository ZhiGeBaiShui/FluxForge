using System;
using System.Collections.Generic;
using System.Linq;
using FluxForge.Mod;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// Mod 装置目录的契约：key 集合、工厂每次返回新实例、无参构造、接口关系。
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

        [Fact]
        public void Keys_ExposeExactlyTheKnownDeviceSet()
        {
            string[] actual = DeviceAdapterCatalog.Keys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            string[] expected = ExpectedKeys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(RegisteredKeys))]
        public void Create_KnownKey_ReturnsFreshDevice(string key)
        {
            object first = DeviceAdapterCatalog.CreateDevice(key);
            object second = DeviceAdapterCatalog.CreateDevice(key);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Same(first.GetType(), second.GetType());
            Assert.NotSame(first, second);
        }

        [Theory]
        [MemberData(nameof(RegisteredKeys))]
        public void EveryRegisteredDevice_HasPublicParameterlessConstructor(string key)
        {
            object created = DeviceAdapterCatalog.CreateDevice(key);
            Type deviceType = created.GetType();

            object activated = Activator.CreateInstance(deviceType);

            Assert.NotNull(activated);
            Assert.Same(deviceType, activated.GetType());
            Assert.NotSame(created, activated);
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
            object device = DeviceAdapterCatalog.CreateDevice(key);

            Assert.True(device is IFlowRouter);
            Assert.False(device is IFlowDevice);
            Assert.True(device is IFlowNotifier);
        }

        [Fact]
        public void NotificationInterface_UsesSenderfulActions()
        {
            Assert.Equal(typeof(Action<IFlowNotifier>), typeof(IFlowNotifier).GetEvent("Changed").EventHandlerType);
            Assert.Equal(typeof(Action<IFlowNotifier>), typeof(IFlowNotifier).GetEvent("Exhausted").EventHandlerType);
        }

        [Fact]
        public void EveryCatalogDevice_HasAnAutoDiscoveredAdapter()
        {
            foreach (string key in DeviceAdapterCatalog.Keys)
            {
                object device = DeviceAdapterCatalog.CreateDevice(key);
                DeviceStateAdapter adapter = DeviceAdapterCatalog.CreateAdapter(device);

                Assert.Same(device, adapter.Device);
            }
        }

        [Theory]
        [InlineData("turn")]
        [InlineData("t_junction")]
        [InlineData("double_emit")]
        public void SplitVariants_ExposeFacing(string key)
        {
            SplitDevice split = Assert.IsAssignableFrom<SplitDevice>(DeviceAdapterCatalog.CreateDevice(key));

            Assert.Equal(0, split.Facing);

            split.Facing = 2;
            Assert.Equal(2, split.Facing);
        }
    }
}


