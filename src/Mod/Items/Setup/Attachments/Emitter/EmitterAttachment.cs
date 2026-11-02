using System;

namespace FluxForge.Mod
{
    /// <summary>
    /// "能发射"这一项能力：把物品里的存量整份转成一个流源。
    ///
    /// 它**不管液体从哪来**——那是 <see cref="FlowContainerAttachment"/> 的职责；
    /// 也**不创建发射装置**——那是 <see cref="DeviceAttachment"/> 按 items.json 的
    /// <c>device</c> key 去 Logic 目录取的职责。物品想能发射，两个附件都要声明：
    ///
    ///     "device": "emitter",
    ///     "attachments": ["flow_container", "device", "emitter"]
    ///
    /// 本附件只把游戏原生的"激活"菜单（三个 <c>*ActivateSlotItemFunc</c> 委托）接到
    /// 发射流程上：谓词只读，真正的发射发生在 <see cref="ActivateEmitter"/>。
    ///
    /// Attach 期**不校验**装置是否存在：此时可能还没轮到 device 附件（声明顺序不保证），
    /// 校验放在启动期的定义校验里（ItemSetup），激活谓词再按实际登记结果判。
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
        /// 仅支持已登记发射装置（由 device 附件按 "emitter" key 创建）且当前直接父容器是网格的物品，
        /// 不递归查找父容器。
        /// </summary>
        internal static GameGridInventory GetEmitterInventory(GameItem item)
        {
            if (item == null || DeviceRegistry.Get<EmitterDevice>(item) == null)
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
            // 原生执行回调重新读当前物品的父网格，不相信菜单阶段或回调参数里的旧容器。
            try
            {
                GameGridInventory currentInventory = GetEmitterInventory(item);
                if (currentInventory == null)
                {
                    FFModEntry.Log.Warning(
                        "[发射装置] 激活未执行：物品失效或当前直接父容器不是支持的网格");
                    return;
                }

                FlowDriver.GetOrCreate(currentInventory).Emit(item);
            }
            catch (Exception e)
            {
                FFModEntry.Log.Error("[发射装置] 激活失败：" + e);
            }
        }
    }
}
