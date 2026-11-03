using System;

namespace FluxForge.Mod
{
    /// <summary>
    /// "能发射"这一项能力：把物品里的存量整份转成一个流源。
    ///
    /// 它**不管液体从哪来**——那是 <see cref="FlowContainerAttachment"/> 的职责；
    /// 也**不创建流源**——那是 <see cref="FlowRuntime.Emit"/> 的职责。
    /// 物品想能发射，两个附件都要声明：
    ///
    ///     "attachments": ["flow_container", "emitter"]
    ///
    /// 本附件只把游戏原生的"激活"菜单（三个 <c>*ActivateSlotItemFunc</c> 委托）接到
    /// 发射流程上：谓词只读，真正的发射发生在 <see cref="ActivateEmitter"/>。
    /// </summary>
    public sealed class EmitterAttachment : ItemAttachment
    {
        public override string Name => "emitter";

        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            WireActivation(item);
        }

        /// <summary>把"激活"菜单接到三个原生委托上。谓词只读，执行时才动状态。</summary>
        private static void WireActivation(GameItem item)
        {
            // Mono 下这些字段就是普通的 Func / Action，直接给方法组即可，
            // 不再需要 ClassInjector + 原生方法指针。
            item.mayActivateSlotItemFunc = MayActivateEmitter;
            item.canActivateSlotItemFunc = CanActivateEmitter;
            item.onActivateSlotItemFunc = ActivateEmitter;
        }

        /// <summary>
        /// 仅支持当前直接父容器是网格的物品，不递归查找父容器。
        /// 发射能力由本附件是否存在决定，不再依赖装置实例。
        /// </summary>
        internal static GameGridInventory GetEmitterInventory(GameItem item)
        {
            if (item == null)
            {
                return null;
            }

            return item.parentInventory as GameGridInventory;
        }

        internal static bool MayActivateEmitter(
            GameItem item, GameInventory inventory, SlotMarker marker)
        {
            try
            {
                return GetEmitterInventory(item) != null;
            }
            catch
            {
                // 菜单查询可反复执行，失效时静默禁用，避免刷日志。
                return false;
            }
        }

        internal static bool CanActivateEmitter(
            GameItem item, GameInventory inventory, SlotMarker marker)
        {
            try
            {
                return GetEmitterInventory(item) != null && FlowStock.Total(item) > 0d;
            }
            catch
            {
                return false;
            }
        }

        internal static void ActivateEmitter(
            GameItem item, GameInventory inventory, SlotMarker marker)
        {
            GridRegistry.Emit(item);
        }
    }
}
