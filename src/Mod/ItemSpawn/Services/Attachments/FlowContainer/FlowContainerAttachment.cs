using System;

namespace FluxForge.Mod
{
    /// <summary>
    /// 装置专用液体容器。
    ///
    /// 与原版容器的区别：
    ///   · 不启用 LIQUID_CONTAINER_TAG；
    ///   · 外部水源因此不会把装置当成可倒入目标；
    ///   · 装置自己注册“倒向外部容器”的交互委托；
    ///   · 内部液体仍使用原版 CURRENT_PART_* tag，FlowStock 可直接读写。
    ///
    /// 这个装配件只解决容器规则，不负责 tooltip / 倒出菜单 / 液体外观。
    /// </summary>
    public sealed class FlowContainerAttachment : ItemAttachment
    {
        /// <summary>
        /// 单次倒水的最小有效容量，单位 µL。
        /// 低于这个量不执行，避免用极小剩余容量做无限细分。
        /// </summary>
        private const int MinimumTransferMicrolitres = 100;

        public override string Name => "flow_container";

        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            item.EnableTag(FlowTags.DeviceContainerTag, true);
            item.EnableTag(FlowTags.LiquidContainerCapacity, true);

            TagState capacity = item.state.GetTag(
                FlowTags.LiquidContainerCapacity);
            capacity.SetInt(def.capacityMicrolitres);

            item.mayThisTargetItemFunc = CanPourInto;
            item.canThisTargetItemFunc = CanPourInto;
            item.onThisTargetItemFunc = PourInto;

            item.SyncModifiedState();
        }

        private static bool CanPourInto(GameItem source, GameItem target)
        {
            return target.IsTag(FlowTags.LiquidContainerTag)
                && GeneralHelper.IsItemOwned(source)
                && GeneralHelper.IsItemOwned(target)
                && WaterHelper.GetFreeCapacity(target)
                    >= MinimumTransferMicrolitres
                && FlowStock.Total(source) > 0d;
        }

        /// <summary>
        /// 对应原版 WaterHelper.TransferLiquid 的主体。
        ///
        /// 原版开头会检查双方 LIQUID_CONTAINER_TAG；装置不能带这个 tag，
        /// 所以这里去掉身份检查，只保留它的取量、按比例分配、过滤和温度处理。
        ///
        /// 原版结尾还会调用 OnLiquidContainerPourOutOf(source)，刷新源物品的
        /// 水容器 feature / sprite / unitValue；装置不是原版容器，故意不调用，
        /// 只刷新目标容器并提交装置自己的 tag。
        /// </summary>
        private static void PourInto(GameItem source, GameItem target)
        {
            if (!CanPourInto(source, target))
            {
                return;
            }

            int total = (int)FlowStock.Total(source);
            int free = WaterHelper.GetFreeCapacity(target);
            int amount = Math.Min(total, free);
            if (amount <= 0)
            {
                return;
            }

            bool wasEmpty = WaterHelper.IsEmpty(target);
            GameItem filter = WaterHelper.GetFilterItem(target);
            int[] sourceParts = WaterHelper.GetLiquidParts(source);
            int[] transferParts = WaterHelper.SplitProportionally(
                sourceParts,
                amount);

            for (int i = 0; i < Liquid.Liquids.Count; i++)
            {
                Liquid liquid = Liquid.Liquids[i];
                int sourceAmount = sourceParts[i];
                int remove = transferParts[i];
                int transfer = WaterHelper.HandleFilter(
                    liquid.identifier,
                    remove,
                    source,
                    filter);

                ChemicalHelper.HandleReceiveLiquidTemp(
                    target,
                    transfer,
                    source);

                int targetAmount = GameItemTags.ReadInt(
                    target,
                    liquid.currentPart);

                GameItemTags.WriteInt(
                    target,
                    liquid.currentPart,
                    targetAmount + transfer);

                GameItemTags.WriteInt(
                    source,
                    liquid.currentPart,
                    sourceAmount - remove);
            }

            target.SyncModifiedState();
            SpriteHelper.UpdateWaterContainerSprite(target);
            WaterFeatureHelper.OnLiquidContainerPourInto(
                target,
                source,
                wasEmpty);

            ItemTagCommit.Commit(source);
        }
    }
}
