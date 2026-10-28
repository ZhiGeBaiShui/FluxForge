using System;
using Il2CppInterop.Runtime;

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

        public override void Attach(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            // 发射装置不播报：tag 在发射后由 FlowRuntime.EmitFrom 对账写入，
            // 所以不订阅 FlowDeviceEvents（EmitterDevice 也没实现 IFlowNotifier）。
            EmitterDevice device = new EmitterDevice(0d, InitialTemperature);
            DeviceRegistry.Register(item.Pointer, device, item);

            WireActivation(item);
        }

        /// <summary>把"激活"菜单接到三个原生委托上。谓词只读，执行时才动状态。</summary>
        private static void WireActivation(Il2Cpp.GameItem item)
        {
            CallbackWiring.EnsureRegistered<EmitterActivationCallbacks>();
            EmitterActivationCallbacks callbacks = new EmitterActivationCallbacks();

            item.mayActivateSlotItemFunc =
                new Il2CppSystem.Func<Il2Cpp.GameItem, Il2Cpp.GameInventory, Il2Cpp.SlotMarker, bool>(
                    callbacks, CallbackWiring.Method<EmitterActivationCallbacks>(
                        nameof(EmitterActivationCallbacks.MayActivate), 3));
            item.canActivateSlotItemFunc =
                new Il2CppSystem.Func<Il2Cpp.GameItem, Il2Cpp.GameInventory, Il2Cpp.SlotMarker, bool>(
                    callbacks, CallbackWiring.Method<EmitterActivationCallbacks>(
                        nameof(EmitterActivationCallbacks.CanActivate), 3));
            item.onActivateSlotItemFunc =
                new Il2CppSystem.Action<Il2Cpp.GameItem, Il2Cpp.GameInventory, Il2Cpp.SlotMarker>(
                    callbacks, CallbackWiring.Method<EmitterActivationCallbacks>(
                        nameof(EmitterActivationCallbacks.Activate), 3));
        }

        /// <summary>仅支持已登记的发射装置及其当前直接父网格，不递归查找父容器。</summary>
        internal static Il2Cpp.GameGridInventory GetEmitterInventory(Il2Cpp.GameItem item)
        {
            if (item == null || item.Pointer == IntPtr.Zero
                || DeviceRegistry.Get<EmitterDevice>(item.Pointer) == null)
            {
                return null;
            }

            Il2Cpp.GameGridInventory inventory = item.parentInventory
                ?.TryCast<Il2Cpp.GameGridInventory>();
            return inventory != null && inventory.Pointer != IntPtr.Zero ? inventory : null;
        }

        internal static bool MayActivateEmitter(
            Il2Cpp.GameItem item, Il2Cpp.GameInventory inventory, Il2Cpp.SlotMarker marker)
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
            Il2Cpp.GameItem item, Il2Cpp.GameInventory inventory, Il2Cpp.SlotMarker marker)
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
            Il2Cpp.GameItem item, Il2Cpp.GameInventory inventory, Il2Cpp.SlotMarker marker)
        {
            // 原生执行回调重新读当前物品的父网格，不相信菜单阶段或回调参数里的旧容器。
            try
            {
                Il2Cpp.GameGridInventory currentInventory = GetEmitterInventory(item);
                if (currentInventory == null)
                {
                    MelonLoader.MelonLogger.Warning(
                        "[发射装置] 激活未执行：物品失效或当前直接父容器不是支持的网格");
                    return;
                }

                FlowDriver.GetOrCreate(currentInventory).Emit(item);
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("[发射装置] 激活失败：" + e);
            }
        }
    }
}
