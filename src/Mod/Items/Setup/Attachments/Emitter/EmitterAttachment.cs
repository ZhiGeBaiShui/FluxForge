using System;
using Il2CppInterop.Runtime;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 发射装置。
    ///
    /// 它有两条独立的接线：
    ///   ① 接水 —— 谁来接收玩家倒进来的液体；
    ///   ② 激活 —— 右键菜单里的"激活"把存量整份转成一个流源。
    ///
    /// 接水目前有两种实现，由 <see cref="UseOwnLiquidTarget"/> 选择：
    ///   · false（当前）：<c>WaterHelper.InitLiquidContainerItem</c>，物品就是原版液体容器，
    ///     倒水完全交给游戏；存储也在原版容器 tag 上。
    ///   · true：自己接管 <c>may/can/onThisTargetItemFunc</c>，把来源抽干并入自己的
    ///     <c>FLOW_STOCK</c>（见 <see cref="LiquidTransfer"/>）。
    ///
    /// ⚠ 翻成 true 之前必须验证：拖拽水瓶到本物品时 <c>onThisTargetItemFunc</c> 真的被调用。
    ///   未验证就翻开，表现是"水倒不进去"，且没有报错。
    /// </summary>
    public sealed class EmitterAttachment : ItemAttachment
    {
        /// <summary>是否用自己的落点逻辑接管接水（见类注释的验证前提）。</summary>
        private static readonly bool UseOwnLiquidTarget = false;

        /// <summary>容量（毫升）。原版硬上限 2,147,483，超了会 LogError 并钳制。</summary>
        private const int CapacityMl = 1000000;

        public override string Name => "emitter";

        public override void Attach(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            if (UseOwnLiquidTarget)
            {
                WireLiquidTarget(item);
            }
            else
            {
                Il2Cpp.WaterHelper.InitLiquidContainerItem(item, CapacityMl, 0);
            }

            WireDevice(item);
            WireActivation(item);
        }

        /// <summary>登记装置：存量初值 0 / 常温，真实值在接管时从 tag 读。</summary>
        private static void WireDevice(Il2Cpp.GameItem item)
        {
            // 发射装置不播报：它的 tag 在发射后由 FlowRuntime.EmitFrom 对账写入，
            // 所以不订阅 FlowDeviceEvents（EmitterDevice 也没实现 IFlowNotifier）。
            EmitterDevice device = new EmitterDevice(0d, LiquidTransfer.DefaultTemperature);
            DeviceRegistry.Register(item.Pointer, device, item);
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

        /// <summary>把"别人拖到我身上"接到三个落点委托上。</summary>
        private static void WireLiquidTarget(Il2Cpp.GameItem item)
        {
            CallbackWiring.EnsureRegistered<EmitterTargetCallbacks>();
            EmitterTargetCallbacks callbacks = new EmitterTargetCallbacks();

            item.mayThisTargetItemFunc =
                new Il2CppSystem.Func<Il2Cpp.GameItem, Il2Cpp.GameItem, bool>(
                    callbacks, CallbackWiring.Method<EmitterTargetCallbacks>(
                        nameof(EmitterTargetCallbacks.MayTarget), 2));
            item.canThisTargetItemFunc =
                new Il2CppSystem.Func<Il2Cpp.GameItem, Il2Cpp.GameItem, bool>(
                    callbacks, CallbackWiring.Method<EmitterTargetCallbacks>(
                        nameof(EmitterTargetCallbacks.CanTarget), 2));
            item.onThisTargetItemFunc =
                new Il2CppSystem.Action<Il2Cpp.GameItem, Il2Cpp.GameItem>(
                    callbacks, CallbackWiring.Method<EmitterTargetCallbacks>(
                        nameof(EmitterTargetCallbacks.OnTarget), 2));
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
                return GetEmitterInventory(item) != null && LiquidStock.Total(item) > 0d;
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
