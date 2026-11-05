using System;
using System.Collections.Generic;
using FluxForge;
using FluxForge.Mod;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 装置挂载编排：Bind → Host/Registry → Load/Save → Dirty/Flush → Exhausted。
    ///
    /// 覆盖 Mod 层的静态流程（<see cref="ItemDeviceBinding"/> / <see cref="DeviceRegistry"/> /
    /// <see cref="DeviceStateSync"/>），全部使用 GameStubs.cs 的最小 <see cref="GameItem"/>
    /// 与 <see cref="ItemTagCommit"/> 桩。Registry / Sync 是进程级静态状态，因此每个用例
    /// 前后都清空，避免用例之间互相污染。
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

        /// <summary>清空 Registry / 脏队列并重置提交计数；用例开始与结束各调用一次。</summary>
        private static void ResetStaticState()
        {
            DeviceStateSync.Clear();
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
        public void Bind_OnDirtyHost_SkipsStaleLoad_AndCleanHostLoadsAgain()
        {
            GameItem item = NewIceItem();
            DeviceHost host = BindIceItem(item);
            IceBlockDevice device = Assert.IsType<IceBlockDevice>(host.Bindings[0].Device);
            device.Water = 5d;
            device.Temperature = -1d;

            // 装置有尚未回写的改动：旧的 modifiedState 快照不能覆盖它。
            host.IsDirty = true;
            item.SetModifiedTag(FlowTagNaming.Water, 99);
            item.SetModifiedTag(FlowTagNaming.ContainerTemperature, -9.5d);

            Assert.True(ItemDeviceBinding.Bind(item));
            Assert.Equal(5d, device.Water);
            Assert.Equal(-1d, device.Temperature);

            // 回写完成后 IsDirty=false，Bind 恢复 Load。
            host.IsDirty = false;

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
        public void MarkDirtyThenFlush_WritesStateTags_AndCommitsOnce()
        {
            GameItem item = NewIceItem();
            DeviceHost host = BindIceItem(item);
            IceBlockDevice device = Assert.IsType<IceBlockDevice>(host.Bindings[0].Device);
            device.Water = 7.9d;
            device.Temperature = -3.5d;

            DeviceStateSync.MarkDirty(host);

            Assert.True(host.IsDirty);
            Assert.Equal(0, ItemTagCommit.CommitCount);

            DeviceStateSync.Flush();

            Assert.False(host.IsDirty);
            Assert.Equal(1, ItemTagCommit.CommitCount);
            Assert.Equal(7, item.state.dict[FlowTagNaming.Water].GetInt());
            Assert.True(item.state.dict[FlowTagNaming.Water].Enabled);
            Assert.Equal(-3.5d, item.state.dict[FlowTagNaming.ContainerTemperature].GetDouble());

            // ItemTagCommit 桩把 state 同步成 modifiedState 快照。
            Assert.Equal(7, item.modifiedState.dict[FlowTagNaming.Water].GetInt());
            Assert.Equal(
                -3.5d,
                item.modifiedState.dict[FlowTagNaming.ContainerTemperature].GetDouble());

            // 脏队列已清空：再 Flush 不会重复提交。
            DeviceStateSync.Flush();
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

            DeviceStateSync.MarkDirty(host);
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
            Assert.False(host.IsDirty);
            Assert.Null(DeviceRegistry.GetHost(item));
            Assert.Throws<KeyNotFoundException>(() =>
            {
                DeviceRegistry.GetItemForDevice(device);
            });

            // 从载体移除。
            Assert.True(item.parentInventory.WasExpelled);
            Assert.Same(item, item.parentInventory.ExpelledItem);

            // Unregister → Forget 已把 Host 移出脏队列，帧末 Flush 不再重复落盘。
            DeviceStateSync.Flush();
            Assert.Equal(1, ItemTagCommit.CommitCount);
        }
    }
}
