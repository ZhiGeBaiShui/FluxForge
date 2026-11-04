using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// Logic 装置目录的契约：key 集合、工厂每次返回新实例、无参构造、接口关系。
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
            string[] actual = FlowDeviceCatalog.Keys
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
            object first = FlowDeviceCatalog.Create(key);
            object second = FlowDeviceCatalog.Create(key);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Same(first.GetType(), second.GetType());
            Assert.NotSame(first, second);
        }

        [Theory]
        [MemberData(nameof(RegisteredKeys))]
        public void EveryRegisteredDevice_HasPublicParameterlessConstructor(string key)
        {
            object created = FlowDeviceCatalog.Create(key);
            Type deviceType = created.GetType();

            object activated = Activator.CreateInstance(deviceType);

            Assert.NotNull(activated);
            Assert.Same(deviceType, activated.GetType());
            Assert.NotSame(created, activated);
        }

        [Fact]
        public void Notifier_IsAFlowState()
        {
            Assert.True(typeof(IFlowState).IsAssignableFrom(typeof(IFlowNotifier)));
        }

        [Theory]
        [InlineData("turn")]
        [InlineData("t_junction")]
        [InlineData("double_emit")]
        public void SplitVariants_ExposeFacing(string key)
        {
            SplitDevice split = Assert.IsAssignableFrom<SplitDevice>(FlowDeviceCatalog.Create(key));

            Assert.Equal(0, split.Facing);

            split.Facing = 2;
            Assert.Equal(2, split.Facing);
        }
    }
}
