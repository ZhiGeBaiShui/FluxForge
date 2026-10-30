using System;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// "能发射"这一项能力：把物品里的存量整份转成一个流源。
    ///
    /// 它**不管液体从哪来**——那是 <see cref="FlowContainerAttachment"/> 的职责。
    /// 两样都想要就在 items.json 里声明两个装配件：
    ///
    ///     "attachments": ["flow_container", "emitter"]
    ///
    /// 存量统一走 <see cref="FlowStock"/>，所以无论物品是原版液体容器
    /// 还是自己存量的罐子，发射流程都不需要知道。
    ///
    /// 触发点是游戏原生的"激活"菜单（三个 <c>*ActivateSlotItemFunc</c> 委托）：
    /// 谓词只读，真正的发射发生在 <see cref="ActivateEmitter"/>。
    /// </summary>
    public sealed class EmitterAttachment : ItemAttachment
    {
        /// <summary>首次读到 tag 之前的初始温度（常温）。</summary>
        private const double InitialTemperature = 25d;

        public override string Name => "emitter";

        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            // 发射装置不播报：tag 在发射后由 FlowRuntime.EmitFrom 对账写入，
            // 所以不订阅 FlowDeviceEvents（EmitterDevice 也没实现 IFlowNotifier）。
            EmitterDevice device = new EmitterDevice(0d, InitialTemperature);
            DeviceRegistry.Register(item, device);

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

        /// <summary>仅支持已登记的发射装置及其当前直接父网格，不递归查找父容器。</summary>
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
                    PBModEntry.Log.Warning(
                        "[发射装置] 激活未执行：物品失效或当前直接父容器不是支持的网格");
                    return;
                }

                FlowDriver.GetOrCreate(currentInventory).Emit(item);
            }
            catch (Exception e)
            {
                PBModEntry.Log.Error("[发射装置] 激活失败：" + e);
            }
        }
    }
}
