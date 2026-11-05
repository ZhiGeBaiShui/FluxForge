using System;
using FluxForge;
using FluxForge.Mod;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 装置状态适配器 + tag 命名契约：Create / Load / Save 与 GameItem 的映射。
    ///
    /// 测试用 GameStubs.cs 的最小 <see cref="GameItem"/> / <see cref="TagSystem"/> /
    /// <see cref="TagState"/> 复刻游戏语义：读 modifiedState、写 state、缺失读 0、
    /// 零值不建 tag、等值不写。全部是纯内存对象，无全局状态、无随机、无时序依赖。
    /// </summary>
    public class DeviceStateAdapterTests
    {
        [Fact]
        public void IceBlock_Create_ReadsWaterIntAndTemperatureDouble()
        {
            GameItem item = new GameItem();
            item.SetModifiedTag(FlowTagNaming.Water, 7);
            item.SetModifiedTag(FlowTagNaming.ContainerTemperature, -3.5d);

            IceBlockDevice device = Assert.IsType<IceBlockDevice>(
                new IceBlockStateAdapter().Create(item));

            Assert.Equal(7d, device.Water);
            Assert.Equal(-3.5d, device.Temperature);
        }

        [Fact]
        public void IceBlock_Load_UpdatesExistingInstance_WithoutRecreating()
        {
            IceBlockStateAdapter adapter = new IceBlockStateAdapter();

            GameItem oldItem = new GameItem();
            oldItem.SetModifiedTag(FlowTagNaming.Water, 7);
            oldItem.SetModifiedTag(FlowTagNaming.ContainerTemperature, -3.5d);
            IceBlockDevice device = Assert.IsType<IceBlockDevice>(adapter.Create(oldItem));

            GameItem newItem = new GameItem();
            newItem.SetModifiedTag(FlowTagNaming.Water, 2);
            newItem.SetModifiedTag(FlowTagNaming.ContainerTemperature, -4.5d);

            adapter.Load(newItem, device);

            Assert.Equal(2d, device.Water);
            Assert.Equal(-4.5d, device.Temperature);
        }

        [Fact]
        public void IceBlock_Save_WritesStateTags_AndSecondSaveIsUnchanged()
        {
            IceBlockStateAdapter adapter = new IceBlockStateAdapter();
            GameItem item = new GameItem();
            IceBlockDevice device = Assert.IsType<IceBlockDevice>(adapter.Create(item));

            // Water 是 double 成员、tag 是 int：写回按旧行为截断。
            device.Water = 5.9d;
            device.Temperature = -2.5d;

            Assert.True(adapter.Save(item, device));

            Assert.Equal(2, item.state.dict.Count);
            TagState water = item.state.dict[FlowTagNaming.Water];
            TagState temperature = item.state.dict[FlowTagNaming.ContainerTemperature];
            Assert.Equal(5, water.GetInt());
            Assert.True(water.Enabled);
            Assert.Equal(-2.5d, temperature.GetDouble());
            Assert.True(temperature.Enabled);

            // Save 只写 state；读快照 modifiedState 仍为空。
            Assert.False(item.modifiedState.dict.ContainsKey(FlowTagNaming.Water));
            Assert.False(item.modifiedState.dict.ContainsKey(FlowTagNaming.ContainerTemperature));

            // 第二次 Save 值没变：每个 WriteXxx 都返回 false，聚合结果也必须是 false。
            Assert.False(adapter.Save(item, device));
        }

        [Fact]
        public void IceBlock_Save_ZeroState_DoesNotCreateTags()
        {
            IceBlockStateAdapter adapter = new IceBlockStateAdapter();
            GameItem item = new GameItem();
            IceBlockDevice device = Assert.IsType<IceBlockDevice>(adapter.Create(item));

            Assert.False(adapter.Save(item, device));
            Assert.Empty(item.state.dict);
        }

        [Fact]
        public void GameItemTags_WriteInt_ExistingNonZeroTag_AcceptsZero()
        {
            // 冰块耗尽路径：CURRENT_PART_WATER 已存在且非零，必须能写回 0 并触发提交。
            GameItem item = new GameItem();
            Assert.True(GameItemTags.WriteInt(item, FlowTagNaming.Water, 3));

            Assert.True(GameItemTags.WriteInt(item, FlowTagNaming.Water, 0));

            TagState water = item.state.dict[FlowTagNaming.Water];
            Assert.Equal(0, water.GetInt());
            Assert.True(water.Enabled);

            // 已经是 0：值没变，不再返回 true。
            Assert.False(GameItemTags.WriteInt(item, FlowTagNaming.Water, 0));
        }

        [Fact]
        public void IceBlock_Create_MatchesDefaultThenLoad_ForSameTags()
        {
            GameItem item = new GameItem();
            item.SetModifiedTag(FlowTagNaming.Water, 7);
            item.SetModifiedTag(FlowTagNaming.ContainerTemperature, -3.5d);

            DeviceStateAdapter adapter = DeviceAdapterCatalog.Get("ice_block").Adapter;
            IceBlockDevice created = Assert.IsType<IceBlockDevice>(adapter.Create(item));

            // 有状态装置：Create 等价于「默认构造 + Load」同一份 tag。
            IceBlockDevice loaded = new IceBlockDevice(0d, 0d);
            adapter.Load(item, loaded);

            Assert.Equal(created.Water, loaded.Water);
            Assert.Equal(created.Temperature, loaded.Temperature);
            Assert.Equal(7d, created.Water);
            Assert.Equal(-3.5d, created.Temperature);
        }

        [Fact]
        public void Collector_Create_ReadsStockFromTags()
        {
            GameItem item = new GameItem();
            item.SetModifiedTag(FlowTagNaming.Water, 30);
            item.SetModifiedTag(FlowTagNaming.ContainerTemperature, 12.5d);

            CollectorDevice device = Assert.IsType<CollectorDevice>(
                new CollectorStateAdapter().Create(item));

            Assert.Equal(30d, device.Stock.Get(FlowProps.Water));
            Assert.Equal(12.5d, device.Stock.Get(FlowProps.Temperature));
            Assert.Equal(0d, device.Stock.Get(FlowProps.Fat));
        }

        [Fact]
        public void Collector_Create_MatchesDefaultThenLoad_ForSameTags()
        {
            GameItem item = new GameItem();
            item.SetModifiedTag(FlowTagNaming.Water, 30);
            item.SetModifiedTag(FlowTagNaming.ContainerTemperature, 12.5d);

            DeviceStateAdapter adapter = DeviceAdapterCatalog.Get("collector").Adapter;
            CollectorDevice created = Assert.IsType<CollectorDevice>(adapter.Create(item));

            // 默认空存量 + Load 必须得到与 Create 完全相同的整份存量。
            CollectorDevice loaded = new CollectorDevice(new FlowProperties());
            adapter.Load(item, loaded);

            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                Assert.Equal(
                    created.Stock.Get(property.Key),
                    loaded.Stock.Get(property.Key));
            }

            Assert.Equal(30d, created.Stock.Get(FlowProps.Water));
            Assert.Equal(12.5d, created.Stock.Get(FlowProps.Temperature));
        }

        [Fact]
        public void Collector_Load_ReplacesExistingStock()
        {
            CollectorStateAdapter adapter = new CollectorStateAdapter();

            GameItem oldItem = new GameItem();
            oldItem.SetModifiedTag(FlowTagNaming.Water, 5);
            CollectorDevice device = Assert.IsType<CollectorDevice>(adapter.Create(oldItem));
            device.Stock.Set(FlowProps.Fat, 99d);
            FlowProperties stockBefore = device.Stock;

            GameItem item = new GameItem();
            item.SetModifiedTag(FlowTagNaming.Water, 30);
            item.SetModifiedTag(FlowTagNaming.ContainerTemperature, 12.5d);

            adapter.Load(item, device);

            // 回灌整体替换存量引用，不改写旧对象。
            Assert.NotSame(stockBefore, device.Stock);
            Assert.Equal(30d, device.Stock.Get(FlowProps.Water));
            Assert.Equal(12.5d, device.Stock.Get(FlowProps.Temperature));
            Assert.Equal(0d, device.Stock.Get(FlowProps.Fat));
        }

        [Fact]
        public void Collector_Save_SparseStock_WritesExactlyOneTag()
        {
            CollectorStateAdapter adapter = new CollectorStateAdapter();
            GameItem item = new GameItem();
            CollectorDevice device = Assert.IsType<CollectorDevice>(adapter.Create(item));
            device.Stock.Set(FlowProps.Water, 42d);

            Assert.True(adapter.Save(item, device));

            // 其余 36 个槽位都是 0：WriteXxx 的「不存在 + 零值」规则应拒绝建 tag。
            Assert.Single(item.state.dict);
            Assert.Equal(42, item.state.dict[FlowTagNaming.Water].GetInt());
            Assert.True(item.state.dict[FlowTagNaming.Water].Enabled);
            Assert.False(item.state.dict.ContainsKey(FlowTagNaming.ContainerTemperature));
            Assert.False(item.state.dict.ContainsKey(
                FlowTagNaming.ForProperty("fat", FlowPropertyKind.Liquid)));
        }

        [Fact]
        public void Collector_TemperatureScalar_RoundTripsThroughDoubleTag()
        {
            CollectorStateAdapter adapter = new CollectorStateAdapter();
            GameItem item = new GameItem();
            CollectorDevice source = Assert.IsType<CollectorDevice>(adapter.Create(item));
            source.Stock.Set(FlowProps.Temperature, 12.5d);

            Assert.True(adapter.Save(item, source));
            Assert.Single(item.state.dict);
            Assert.Equal(12.5d, item.state.dict[FlowTagNaming.ContainerTemperature].GetDouble());

            // modifiedState 是读快照：模拟游戏把 state 同步过去后重新创建装置。
            item.modifiedState = item.state;
            CollectorDevice restored = Assert.IsType<CollectorDevice>(adapter.Create(item));

            Assert.Equal(12.5d, restored.Stock.Get(FlowProps.Temperature));
            Assert.Equal(0d, restored.Stock.Get(FlowProps.Water));
        }

        [Fact]
        public void Temperature_Create_UsesTag_AndLoadOverwrites()
        {
            TemperatureConfigAdapter adapter = new TemperatureConfigAdapter();

            GameItem initial = new GameItem();
            initial.SetModifiedTag(FlowTagNaming.ContainerTemperature, 8.5d);
            TemperatureDevice device = Assert.IsType<TemperatureDevice>(adapter.Create(initial));
            Assert.Equal(8.5d, device.Temperature);

            GameItem overwrite = new GameItem();
            overwrite.SetModifiedTag(FlowTagNaming.ContainerTemperature, -2.25d);
            adapter.Load(overwrite, device);
            Assert.Equal(-2.25d, device.Temperature);

            // 缺失 tag 读到 0，覆盖当前配置值。
            adapter.Load(new GameItem(), device);
            Assert.Equal(0d, device.Temperature);
        }

        [Fact]
        public void Temperature_Create_MatchesDefaultThenLoad_ForSameTag()
        {
            GameItem item = new GameItem();
            item.SetModifiedTag(FlowTagNaming.ContainerTemperature, 8.5d);

            DeviceStateAdapter adapter = DeviceAdapterCatalog.Get("temperature").Adapter;
            TemperatureDevice created = Assert.IsType<TemperatureDevice>(adapter.Create(item));

            TemperatureDevice loaded = new TemperatureDevice(0d);
            adapter.Load(item, loaded);

            Assert.Equal(created.Temperature, loaded.Temperature);
            Assert.Equal(8.5d, created.Temperature);
        }

        [Fact]
        public void Temperature_Save_IsFalse_AndDoesNotWriteTags()
        {
            TemperatureConfigAdapter adapter = new TemperatureConfigAdapter();
            GameItem item = new GameItem();
            TemperatureDevice device = Assert.IsType<TemperatureDevice>(adapter.Create(item));
            device.Temperature = 12.5d;

            Assert.False(adapter.Save(item, device));
            Assert.Empty(item.state.dict);
        }

        [Fact]
        public void Filter_CreateReturnsFilter_AndLoadSaveAreNoOps()
        {
            FilterStateAdapter adapter = new FilterStateAdapter();
            GameItem item = new GameItem();
            FilterDevice device = Assert.IsType<FilterDevice>(adapter.Create(item));

            adapter.Load(item, device);

            Assert.False(adapter.Save(item, device));
            Assert.Empty(item.state.dict);
        }

        [Fact]
        public void TJunction_CreateReturnsTJunction_AndLoadSaveAreNoOps()
        {
            TJunctionStateAdapter adapter = new TJunctionStateAdapter();
            GameItem item = new GameItem();
            TJunctionDevice device = Assert.IsType<TJunctionDevice>(adapter.Create(item));

            adapter.Load(item, device);

            Assert.False(adapter.Save(item, device));
            Assert.Empty(item.state.dict);
        }

        /// <summary>TagFields 映射的测试目标：字段与可写属性各一个。</summary>
        private sealed class TagFieldTarget
        {
            public int Count;
            public double Ratio { get; set; }
        }

        [Fact]
        public void TagFields_SimpleMembers_MapLoadAndSave()
        {
            TagField<TagFieldTarget> count
                = TagFields.Int<TagFieldTarget>(
                    "TEST_COUNT",
                    target => target.Count,
                    (target, value) => target.Count = value);

            TagField<TagFieldTarget> ratio
                = TagFields.Double<TagFieldTarget>(
                    "TEST_RATIO",
                    target => target.Ratio,
                    (target, value) => target.Ratio = value);

            GameItem item = new GameItem();
            item.SetModifiedTag("TEST_COUNT", 11);
            item.SetModifiedTag("TEST_RATIO", 0.25d);

            TagFieldTarget target = new TagFieldTarget();
            count.Load(item, target);
            ratio.Load(item, target);
            Assert.Equal(11, target.Count);
            Assert.Equal(0.25d, target.Ratio);

            target.Count = 12;
            target.Ratio = 0.5d;
            Assert.True(count.Save(item, target));
            Assert.True(ratio.Save(item, target));
            Assert.Equal(12, item.state.dict["TEST_COUNT"].GetInt());
            Assert.Equal(0.5d, item.state.dict["TEST_RATIO"].GetDouble());

            Assert.False(count.Save(item, target));
            Assert.False(ratio.Save(item, target));
        }

        [Fact]
        public void FlowPropertyTags_CachesRegistryOrderedTagNames()
        {
            Assert.Equal(FlowPropertyRegistry.Count, FlowPropertyTags.All.Count);

            for (int i = 0; i < FlowPropertyRegistry.Count; i++)
            {
                FlowPropertyDef def = FlowPropertyRegistry.All[i];
                FlowPropertyTag property = FlowPropertyTags.All[i];

                Assert.Equal(def.Key, property.Key);
                Assert.Equal(def.Kind, property.Kind);
                Assert.Equal(FlowTagNaming.ForProperty(def.Id, def.Kind), property.Tag);
            }
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
