using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 发射装置：装配成**原版液体容器**，倒水完全交给游戏。
    ///
    /// <c>WaterHelper.InitLiquidContainerItem</c> 会给物品加 LIQUID_CONTAINER_TAG/CAPACITY，
    /// 并挂一个 target 委托——玩家把水瓶拖上来时由原版调用 TransferLiquid，
    /// 每种物质、温度、纯度全部自动处理。我们不再自己写转交代码。
    ///
    /// 容量取 100 万 mL（游戏硬上限 2,147,483，超了会 LogError 并钳制）：
    /// 对玩法而言等同于无上限，又不会触发那条错误。
    ///
    /// 存量初值给 0，激活时从容器 tag 读取，装置只是它的运行时镜像。
    /// </summary>
    public sealed class EmitterAttachment : ItemAttachment
    {
        public override string Name => "emitter";

        public override string ExclusiveSlot => "device";

        public override void Attach(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            const int CapacityMl = 1000000;

            Il2Cpp.WaterHelper.InitLiquidContainerItem(item, CapacityMl, 0);

            EmitterDevice device = new EmitterDevice(0d, 25d);
            DeviceRegistry.Register(item.Pointer, device, item);

            // 游戏通过 DynamicInvoke 查询激活；ConvertDelegate 的反射方法与桥接目标不匹配。
            // 使用注入类型的原生实例方法，保证反射和直接 Invoke 都具有正确的目标类型。
            if (!ClassInjector.IsTypeRegisteredInIl2Cpp<EmitterActivationCallbacks>())
            {
                ClassInjector.RegisterTypeInIl2Cpp<EmitterActivationCallbacks>();
            }
            EmitterActivationCallbacks callbacks = new EmitterActivationCallbacks();
            IntPtr callbackClass = Il2CppClassPointerStore<EmitterActivationCallbacks>.NativeClassPtr;
            item.mayActivateSlotItemFunc =
                new Il2CppSystem.Func<Il2Cpp.GameItem, Il2Cpp.GameInventory, Il2Cpp.SlotMarker, bool>(
                    callbacks, IL2CPP.il2cpp_class_get_method_from_name(
                        callbackClass, nameof(EmitterActivationCallbacks.MayActivate), 3));
            item.canActivateSlotItemFunc =
                new Il2CppSystem.Func<Il2Cpp.GameItem, Il2Cpp.GameInventory, Il2Cpp.SlotMarker, bool>(
                    callbacks, IL2CPP.il2cpp_class_get_method_from_name(
                        callbackClass, nameof(EmitterActivationCallbacks.CanActivate), 3));
            item.onActivateSlotItemFunc =
                new Il2CppSystem.Action<Il2Cpp.GameItem, Il2Cpp.GameInventory, Il2Cpp.SlotMarker>(
                    callbacks, IL2CPP.il2cpp_class_get_method_from_name(
                        callbackClass, nameof(EmitterActivationCallbacks.Activate), 3));
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
