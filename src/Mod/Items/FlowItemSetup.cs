using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品创建后的「装置挂接」：按 key 决定这件物品带什么行为。
    ///
    /// 放在物品目录工厂里而不是 ItemFactory 内部，是为了让 ItemFactory 保持
    /// 「纯数据装配」的职责——装置是行为，属于 mod 层的事。
    /// </summary>
    public static class FlowItemSetup
    {
        public const string FlowMachineKey = "flow_machine";
        public const string EmitterKey = "flow_emitter";
        public const string CollectorKey = "flow_collector";
        public const string IceBlockKey = "ice_block";

        /// <summary>
        /// 我们的物品类型。在 <c>GameItem.SetGameItemType</c> 内部它就是 tag（EnableTag），
        /// 所以同一个标识既用于 tooltip 显示，也用于容器白名单。
        /// </summary>
        public const string FlowDeviceType = "FLOW_DEVICE";

        /// <summary>该类型的显示名。</summary>
        public const string FlowDeviceDisplayName = "流量装置";

        /// <summary>
        /// 物品目录工厂的入口：先按 json 装配，再按 key 挂装置。
        ///
        /// 挂装置单独包一层 try/catch：这段代码是**在游戏的物品创建回调内部**执行的，
        /// 异常会顺着 IL2CPP 的原生调用栈往上抛，轻则这个物品没行为、重则整次读档失败。
        /// 出问题时留下 Error 日志、物品照常返回，比让存档加载中断要好。
        /// </summary>
        public static Il2Cpp.GameItem Create(FlowItemDefinition def)
        {
            Il2Cpp.GameItem item;

            try
            {
                item = ItemFactory.Create(def);
            }
            catch (Exception e)
            {
                // 这一段是游戏物品创建链直接调用的，异常会顺着原生栈冒出去
                MelonLoader.MelonLogger.Error("[物品] 「" + def.key + "」装配失败：" + e);
                return null;
            }

            if (item == null)
            {
                return null;
            }

            try
            {
                Attach(item, def);
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error(
                    "[物品] 「" + def.key + "」装置挂接失败，物品将以无行为状态存在：" + e);
            }

            return item;
        }

        private static void Attach(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            switch (def.key)
            {
                case FlowMachineKey:
                    AttachMachine(item, def);
                    break;
                case EmitterKey:
                    AttachEmitter(item);
                    break;
                case CollectorKey:
                    AttachCollector(item);
                    break;
                case IceBlockKey:
                    AttachIceBlock(item);
                    break;
                default:
                    // 走到这里说明 items.json 里写了这个物品，但代码不认识它——
                    // 物品本身能用（纯数据），只是没有任何行为，属于配置笔误
                    MelonLoader.MelonLogger.Warning(
                        "[物品] 「" + def.key + "」没有对应的装置挂接逻辑，将作为纯数据物品存在");
                    break;
            }
        }

        /// <summary>
        /// 流量机面板：仅提供装置网格与窗口，运行环境由装置激活时按需创建。
        ///
        /// ⚠ <see cref="Il2Cpp.PixelWindow"/> 只有**一个**子元素槽——`Attach` 会先 Detach 旧 child。
        ///   如果先 Attach 网格、再 Attach 按钮，网格就被顶掉了，表现为「窗口能开、内容全空」。
        ///   原版所有多元素窗口都是：建一个 <see cref="Il2Cpp.GridPixelElement"/> 当根，
        ///   网格/标签/按钮全部 Attach 进根里，最后只 window.Attach(根) 一次。
        ///
        /// 另外窗口尺寸不用自己算：<c>Attach</c> 会按根元素的实际尺寸重算并覆盖构造时传的值。
        /// </summary>
        private static void AttachMachine(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            int columns = 4;
            int rows = 3;
            ParseGrid(def.grid, ref columns, ref rows);

            // 根只保留网格，不再放启动按钮或行间距。
            Il2Cpp.GridPixelElement root = new Il2Cpp.GridPixelElement(1, 1, true);

            Il2Cpp.GameGridInventory inventory = new Il2Cpp.GameGridInventory(columns, rows);

            // Il2CppInterop 没生成 GameGridInventory → PixelElement 的继承关系，
            // 运行时确实是子类，所以用 TryCast 走原生类型转换。
            // 第 4/5 个参数是**最小像素宽高**（不是行列数），给 0 表示用元素自身尺寸。
            if (!root.Attach(inventory.TryCast<Il2Cpp.PixelElement>(), 0, 0, 0, 0))
            {
                MelonLoader.MelonLogger.Error(
                    "[流量机] 网格挂载失败（" + columns + "x" + rows + "），面板里不会有格子");
            }

            // 用游戏自己的容器白名单，按**物品类型**过滤——
            // 类型在 SetGameItemType 里就是 tag（EnableTag），所以这里传类型名即可。
            // 原版也是这样用的：nutrifruit_seed_packet 先 SetGameItemType("SEED")，
            // 再 AllowOnlyTaggedItem(inv, "SEED")。
            Il2Cpp.ContainerHelper.AllowOnlyTaggedItem(inventory, FlowDeviceType);

            Il2Cpp.PixelWindow window = new Il2Cpp.PixelWindow(1, 1, true, 8, 8, "流量机");
            window.Attach(root.TryCast<Il2Cpp.PixelElement>());
            item.SetContentWindow(window);
        }

        /// <summary>解析 "4x3" 这类尺寸串，格式不对就保留默认值。</summary>
        private static void ParseGrid(string text, ref int columns, ref int rows)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string[] parts = text.Split('x', 'X');
            int parsedColumns;
            int parsedRows;

            if (parts.Length == 2
                && int.TryParse(parts[0], out parsedColumns)
                && int.TryParse(parts[1], out parsedRows)
                && parsedColumns > 0
                && parsedRows > 0)
            {
                columns = parsedColumns;
                rows = parsedRows;
            }
            else
            {
                MelonLoader.MelonLogger.Warning("[流量机] grid 配置无效：" + text + "（用默认 4x3）");
            }
        }

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
        private static void AttachEmitter(Il2Cpp.GameItem item)
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
        private static Il2Cpp.GameGridInventory GetEmitterInventory(Il2Cpp.GameItem item)
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

        /// <summary>收集装置：吸收流源并把属性累积起来。</summary>
        private static void AttachCollector(Il2Cpp.GameItem item)
        {
            CollectorDevice device = new CollectorDevice();
            DeviceRegistry.Register(item.Pointer, device, item);
            Subscribe(device);
        }

        /// <summary>冰块：存量从物品的 tag 里读出来，之后由装置自己维护。</summary>
        private static void AttachIceBlock(Il2Cpp.GameItem item)
        {
            double moisture = FlowTags.ReadInt(item.state, FlowTags.Stock, 0);
            double temperature = FlowTags.ReadFloat(item.state, FlowTags.IceTemperature, 0f);

            IceBlockDevice device = new IceBlockDevice(moisture, temperature);
            DeviceRegistry.Register(item.Pointer, device, item);
            Subscribe(device);
        }

        /// <summary>
        /// 挂上装置的播报事件。
        ///
        /// 不需要显式退订：读档时 <see cref="DeviceRegistry.Clear"/> 丢弃全部引用，
        /// 装置对象再也没有持有者，随后被回收，挂在它身上的订阅列表也一起消失。
        /// </summary>
        private static void Subscribe(IFlowNotifier device)
        {
            device.Changed += FlowDeviceEvents.OnChanged;
            device.Exhausted += FlowDeviceEvents.OnExhausted;
        }
    }

    /// <summary>供原生委托反射调用的实例目标；不保存物品或网格归属。</summary>
    public sealed class EmitterActivationCallbacks : Il2CppSystem.Object
    {
        public EmitterActivationCallbacks(IntPtr pointer) : base(pointer)
        {
        }

        public EmitterActivationCallbacks()
            : base(ClassInjector.DerivedConstructorPointer<EmitterActivationCallbacks>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }

        public bool MayActivate(
            Il2Cpp.GameItem item, Il2Cpp.GameInventory inventory, Il2Cpp.SlotMarker marker)
        {
            return FlowItemSetup.MayActivateEmitter(item, inventory, marker);
        }

        public bool CanActivate(
            Il2Cpp.GameItem item, Il2Cpp.GameInventory inventory, Il2Cpp.SlotMarker marker)
        {
            return FlowItemSetup.CanActivateEmitter(item, inventory, marker);
        }

        public void Activate(
            Il2Cpp.GameItem item, Il2Cpp.GameInventory inventory, Il2Cpp.SlotMarker marker)
        {
            FlowItemSetup.ActivateEmitter(item, inventory, marker);
        }
    }
}
