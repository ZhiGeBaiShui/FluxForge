using System.Collections.Generic;
using FluxForge.Mod;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 装置状态适配器 + tag 命名契约。
    ///
    /// 测试用 <see cref="FakeTagStore"/> 复刻 <c>GameItemTagStore</c> 的「不存在不读、
    /// 零值不建 tag、等值不写」语义，因此这些用例同时钉住了适配器的读写范围和跳过规则。
    /// 全部是纯内存对象，无全局状态、无随机、无时序依赖。
    /// </summary>
    public class DeviceStateAdapterTests
    {
        /// <summary>
        /// 内存版 <see cref="ITagStore"/>，语义与 <c>GameItemTagStore</c> 一致：
        ///   · 读不存在的 tag 返回 0；
        ///   · 写不存在的零值直接返回 false，不创建 tag；
        ///   · 写已有 tag 且值相等返回 false，否则写值并 Enable。
        /// 一个 tag 同时持有 int / double 两个槽位，对应原版 <c>TagState</c>。
        /// </summary>
        private sealed class FakeTagStore : ITagStore
        {
            private sealed class Entry
            {
                public int IntValue;
                public double DoubleValue;
                public bool Enabled;
            }

            private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

            /// <summary>当前存在的 tag 数。用来验证「不把零值写成新 tag」。</summary>
            public int Count
            {
                get { return _entries.Count; }
            }

            public bool Exists(string tag)
            {
                return _entries.ContainsKey(tag);
            }

            public int ReadInt(string tag)
            {
                Entry entry;
                return _entries.TryGetValue(tag, out entry) ? entry.IntValue : 0;
            }

            public double ReadDouble(string tag)
            {
                Entry entry;
                return _entries.TryGetValue(tag, out entry) ? entry.DoubleValue : 0d;
            }

            public bool WriteInt(string tag, int value)
            {
                Entry entry;
                if (_entries.TryGetValue(tag, out entry))
                {
                    if (entry.IntValue == value)
                    {
                        return false;
                    }
                }
                else
                {
                    if (value == 0)
                    {
                        return false;
                    }

                    entry = new Entry();
                    _entries.Add(tag, entry);
                }

                entry.Enabled = true;
                entry.IntValue = value;
                return true;
            }

            public bool WriteDouble(string tag, double value)
            {
                Entry entry;
                if (_entries.TryGetValue(tag, out entry))
                {
                    if (entry.DoubleValue == value)
                    {
                        return false;
                    }
                }
                else
                {
                    if (value == 0d)
                    {
                        return false;
                    }

                    entry = new Entry();
                    _entries.Add(tag, entry);
                }

                entry.Enabled = true;
                entry.DoubleValue = value;
                return true;
            }

            /// <summary>tag 是否处于 enabled 状态（写入方 Enable 后的预期）。</summary>
            public bool IsEnabled(string tag)
            {
                return _entries[tag].Enabled;
            }
        }

        [Fact]
        public void IceBlock_Load_ReadsWaterIntAndTemperatureDouble()
        {
            FakeTagStore tags = new FakeTagStore();
            tags.WriteInt(FlowTagNaming.Water, 7);
            tags.WriteDouble(FlowTagNaming.ContainerTemperature, -3.5d);

            IceBlockDevice device = new IceBlockDevice();
            IceBlockStateAdapter adapter = new IceBlockStateAdapter(device);

            adapter.Load(tags);

            Assert.Same(device, adapter.Device);
            Assert.Equal(7d, device.Water);
            Assert.Equal(-3.5d, device.Temperature);
        }

        [Fact]
        public void IceBlock_Save_WritesBothTags_AndSecondSaveIsUnchanged()
        {
            IceBlockDevice device = new IceBlockDevice(5d, -2d);
            IceBlockStateAdapter adapter = new IceBlockStateAdapter(device);
            FakeTagStore tags = new FakeTagStore();

            Assert.True(adapter.Save(tags));

            Assert.Equal(2, tags.Count);
            Assert.True(tags.Exists(FlowTagNaming.Water));
            Assert.True(tags.Exists(FlowTagNaming.ContainerTemperature));
            Assert.Equal(5, tags.ReadInt(FlowTagNaming.Water));
            Assert.Equal(-2d, tags.ReadDouble(FlowTagNaming.ContainerTemperature));
            Assert.True(tags.IsEnabled(FlowTagNaming.Water));
            Assert.True(tags.IsEnabled(FlowTagNaming.ContainerTemperature));

            // 第二次 Save 值没变：每个 WriteXxx 都返回 false，聚合结果也必须是 false。
            Assert.False(adapter.Save(tags));
        }

        [Fact]
        public void IceBlock_Save_ZeroState_DoesNotCreateTags()
        {
            IceBlockDevice device = new IceBlockDevice();
            IceBlockStateAdapter adapter = new IceBlockStateAdapter(device);
            FakeTagStore tags = new FakeTagStore();

            Assert.False(adapter.Save(tags));
            Assert.Equal(0, tags.Count);
        }

        [Fact]
        public void Collector_Load_AppliesAllTagValues_IncludingAbsentAsZero()
        {
            FakeTagStore tags = new FakeTagStore();
            tags.WriteInt(FlowTagNaming.Water, 30);
            tags.WriteDouble(FlowTagNaming.ContainerTemperature, 12.5d);

            CollectorDevice device = new CollectorDevice();

            // 存量里已有一份值，但对应 tag 不存在——适配器只执行同步，缺失读作 0。
            device.Set(FlowProps.Fat, 99d);

            CollectorStateAdapter adapter = new CollectorStateAdapter(device);
            adapter.Load(tags);

            Assert.Equal(30d, device.Get(FlowProps.Water));
            Assert.Equal(12.5d, device.Get(FlowProps.Temperature));
            Assert.Equal(0d, device.Get(FlowProps.Fat));
        }

        [Fact]
        public void Collector_Save_OnlyWaterValue_WritesExactlyOneTag()
        {
            CollectorDevice device = new CollectorDevice();
            device.Set(FlowProps.Water, 42d);

            CollectorStateAdapter adapter = new CollectorStateAdapter(device);
            FakeTagStore tags = new FakeTagStore();

            Assert.True(adapter.Save(tags));

            // 其余 36 个槽位都是 0：WriteXxx 的「不存在 + 零值」规则应拒绝建 tag。
            Assert.Equal(1, tags.Count);
            Assert.Equal(42, tags.ReadInt(FlowTagNaming.Water));
            Assert.False(tags.Exists(FlowTagNaming.ContainerTemperature));
            Assert.False(tags.Exists(FlowTagNaming.ForProperty("fat", FlowPropertyKind.Liquid)));
        }

        [Fact]
        public void Collector_TemperatureScalar_RoundTripsThroughExistingDoubleTag()
        {
            CollectorDevice source = new CollectorDevice();
            source.Set(FlowProps.Temperature, 12.5d);

            CollectorStateAdapter saveAdapter = new CollectorStateAdapter(source);
            FakeTagStore tags = new FakeTagStore();
            tags.WriteDouble(FlowTagNaming.ContainerTemperature, 0.1d);

            Assert.True(saveAdapter.Save(tags));
            Assert.Equal(1, tags.Count);
            Assert.True(tags.Exists(FlowTagNaming.ContainerTemperature));
            Assert.Equal(12.5d, tags.ReadDouble(FlowTagNaming.ContainerTemperature));

            CollectorDevice restored = new CollectorDevice();
            CollectorStateAdapter loadAdapter = new CollectorStateAdapter(restored);
            loadAdapter.Load(tags);

            Assert.Equal(12.5d, restored.Get(FlowProps.Temperature));
            Assert.Equal(0d, restored.Get(FlowProps.Water));
        }

        [Fact]
        public void TemperatureConfig_Load_AppliesTag_AndSaveIsFalse()
        {
            FakeTagStore tags = new FakeTagStore();
            tags.WriteDouble(FlowTagNaming.ContainerTemperature, 8.5d);

            TemperatureDevice device = new TemperatureDevice();
            device.Temperature = 3d;

            TemperatureConfigAdapter adapter = new TemperatureConfigAdapter(device);

            adapter.Load(tags);
            Assert.Same(device, adapter.Device);
            Assert.Equal(8.5d, device.Temperature);

            // 适配器不做存在性判断：缺失 tag 读到 0，覆盖当前配置值。
            FakeTagStore emptyTags = new FakeTagStore();
            adapter.Load(emptyTags);
            Assert.Equal(0d, device.Temperature);

            // 温度是 items.json 配置：Save 永远 false，也不写 tag。
            Assert.False(adapter.Save(emptyTags));
            Assert.Equal(0, emptyTags.Count);
        }

        [Fact]
        public void Filter_LoadIsNoOp_AndSaveIsFalse()
        {
            FilterDevice device = new FilterDevice();
            FilterStateAdapter adapter = new FilterStateAdapter(device);
            FakeTagStore tags = new FakeTagStore();

            adapter.Load(tags);

            Assert.Same(device, adapter.Device);
            Assert.False(adapter.Save(tags));
            Assert.Equal(0, tags.Count);
        }

        [Fact]
        public void Split_LoadIsNoOp_AndSaveIsFalse()
        {
            SplitDevice device = new TurnDevice();
            SplitStateAdapter adapter = new SplitStateAdapter(device);
            FakeTagStore tags = new FakeTagStore();

            adapter.Load(tags);

            Assert.Same(device, adapter.Device);
            Assert.False(adapter.Save(tags));
            Assert.Equal(0, tags.Count);
        }

        [Fact]
        public void FlowTagNaming_PinsOriginalTagNames()
        {
            Assert.Equal("CURRENT_PART_WATER", FlowTagNaming.Water);
            Assert.Equal("CONTAINER_TEMP_TAG", FlowTagNaming.ContainerTemperature);

            // 命名规则与两个常量必须指向同一个字符串。
            Assert.Equal(FlowTagNaming.Water, FlowTagNaming.ForProperty("water", FlowPropertyKind.Liquid));
            Assert.Equal(
                FlowTagNaming.ContainerTemperature,
                FlowTagNaming.ForProperty("container_temp", FlowPropertyKind.Scalar));
        }
    }
}
