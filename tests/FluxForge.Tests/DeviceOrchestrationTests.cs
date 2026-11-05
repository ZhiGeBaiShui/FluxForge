using System;
using System.Collections.Generic;
using FluxForge;
using FluxForge.Mod;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 装置挂载编排：Bind → Host/Registry → Load/Save → Exhausted。
    ///
    /// Load 由 Bind / ValidateShapeState 立即触发；Save 由 Changed 立即触发。
    /// 全部使用 GameStubs.cs 的最小 <see cref="GameItem"/> 与
    /// <see cref="ItemTagCommit"/> 桩。Registry 是进程级静态状态，每个用例前后清空。
    /// </summary>
    public class DeviceOrchestrationTests : IDisposable
    {
        public DeviceOrchestrationTests()
        {
            ResetStaticState();
        }

        public void Dispose()
        {
            ResetStaticState();
        }

        private static void ResetStaticState()
        {
            DeviceRegistry.Clear();
            ItemTagCommit.CommitCount = 0;
        }

        /// <summary>带 ice_block 身份 tag 的物品：目录据此绑定 IceBlockDevice。</summary>
        private static GameItem NewIceItem()
        {
            GameItem item = new GameItem();
            item.EnableTag("ice_block");
            return item;
        }

        private static DeviceHost BindIceItem(GameItem item)
        {
            Assert.True(ItemDeviceBinding.Bind(item));

            DeviceHost host = DeviceRegistry.GetHost(item);
            Assert.NotNull(host);
            return host;
        }

        [Fact]
        public void Bind_CreatesHostWithIceDevice_AndRebindKeepsDeviceIdentity()
        {
            GameItem item = NewIceItem();

            DeviceHost host = BindIceItem(item);

            Assert.Single(host.Bindings);
            IceBlockDevice device = Assert.IsType<IceBlockDevice>(host.Bindings[0].Device);

            // 第二次 Bind 返回 true，且不重建 Host / 装置：音频、动画按装置引用反查。
            Assert.True(ItemDeviceBinding.Bind(item));

            DeviceHost rebound = DeviceRegistry.GetHost(item);
            Assert.Same(host, rebound);
            Assert.Same(device, rebound.Bindings[0].Device);
        }

        [Fact]
        public void Bind_OnExistingHost_AlwaysReloadsLatestTags()
        {
            GameItem item = NewIceItem();
            DeviceHost host = BindIceItem(item);
            IceBlockDevice device = Assert.IsType<IceBlockDevice>(host.Bindings[0].Device);

            device.Water = 5d;
            device.Temperature = -1d;
            item.SetModifiedTag(FlowTagNaming.Water, 99);
            item.SetModifiedTag(FlowTagNaming.ContainerTemperature, -9.5d);

            Assert.True(ItemDeviceBinding.Bind(item));
            Assert.Equal(99d, device.Water);
            Assert.Equal(-9.5d, device.Temperature);
        }

        [Fact]
        public void GetItemForDevice_ReturnsOwningItem()
        {
            GameItem item = NewIceItem();
            DeviceHost host = BindIceItem(item);

            Assert.Same(item, DeviceRegistry.GetItemForDevice(host.Bindings[0].Device));
        }

        [Fact]
        public void Changed_WritesStateTagsImmediately_AndCommitsOnce()
        {
            GameItem item = NewIceItem();
            DeviceHost host = BindIceItem(item);
            IceBlockDevice device = Assert.IsType<IceBlockDevice>(host.Bindings[0].Device);
            device.Water = 7.9d;
            device.Temperature = -3.5d;

            host.OnDeviceChanged(device);

            Assert.Equal(1, ItemTagCommit.CommitCount);
            Assert.Equal(7, item.state.dict[FlowTagNaming.Water].GetInt());
            Assert.True(item.state.dict[FlowTagNaming.Water].Enabled);
            Assert.Equal(-3.5d, item.state.dict[FlowTagNaming.ContainerTemperature].GetDouble());

            // ItemTagCommit 桩把 state 同步成 modifiedState 快照。
            Assert.Equal(7, item.modifiedState.dict[FlowTagNaming.Water].GetInt());
            Assert.Equal(
                -3.5d,
                item.modifiedState.dict[FlowTagNaming.ContainerTemperature].GetDouble());

            // 值未变化时 SaveAll 返回 false，不重复提交。
            host.OnDeviceChanged(device);
            Assert.Equal(1, ItemTagCommit.CommitCount);
        }

        [Fact]
        public void Exhausted_SavesTags_UnregistersAndExpelsFromParentInventory()
        {
            GameItem item = NewIceItem();
            DeviceHost host = BindIceItem(item);
            IceBlockDevice device = Assert.IsType<IceBlockDevice>(host.Bindings[0].Device);

            // 物品原本带着非零水量 tag：耗尽必须把已有的 3 覆盖成 0，而不是跳过零值写入。
            Assert.True(GameItemTags.WriteInt(item, FlowTagNaming.Water, 3));
            device.Water = 0d;
            device.Temperature = -4.5d;

            DeviceStateSync.Exhausted(host);

            // Save → Commit：state 落盘，快照同步后可被后续 Load 读到。
            Assert.Equal(0, item.state.dict[FlowTagNaming.Water].GetInt());
            Assert.True(item.state.dict[FlowTagNaming.Water].Enabled);
            Assert.Equal(
                -4.5d,
                item.state.dict[FlowTagNaming.ContainerTemperature].GetDouble());
            Assert.True(item.modifiedState.dict.ContainsKey(FlowTagNaming.Water));
            Assert.Equal(0, GameItemTags.ReadInt(item, FlowTagNaming.Water));
            Assert.Equal(
                -4.5d,
                GameItemTags.ReadDouble(item, FlowTagNaming.ContainerTemperature));
            Assert.Equal(1, ItemTagCommit.CommitCount);

            // Registry：Host 与装置反查都已摘除。
            Assert.Null(DeviceRegistry.GetHost(item));
            Assert.Throws<KeyNotFoundException>(() =>
            {
                DeviceRegistry.GetItemForDevice(device);
            });

            // 从载体移除。
            Assert.True(item.parentInventory.WasExpelled);
            Assert.Same(item, item.parentInventory.ExpelledItem);
        }
    }
}
