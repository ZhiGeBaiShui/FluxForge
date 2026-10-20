using System;
using Il2CppInterop.Runtime;
using ProbablyBagMod.Mod.Flow.View;

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
        /// 流量机面板：一个网格 + 一个启动按钮。
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

            // 根：1 列 × 2 行（第 0 行放网格，第 1 行放按钮）
            Il2Cpp.GridPixelElement root = new Il2Cpp.GridPixelElement(1, 2, true);
            root.SetSpacing(0, 8);

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

            FlowMachine machine = new FlowMachine(item, inventory);

            Il2Cpp.ButtonElement button = new Il2Cpp.ButtonElement("启动", 64, 20, null, 0, "interface_click");

            // 按钮回调闭包捕获本机：多台机器的面板同时存在时，点哪台就只管哪台。
            // 这是「一次性发射」，按下就把装置里的存量整份转成一个流源，不是开关。
            Action start = () => Start(machine);
            button.SetCallback(DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(start));
            root.Attach(button.TryCast<Il2Cpp.PixelElement>(), 0, 1, 64, 20);

            Il2Cpp.PixelWindow window = new Il2Cpp.PixelWindow(1, 1, true, 8, 8, "流量机");
            window.Attach(root.TryCast<Il2Cpp.PixelElement>());
            item.SetContentWindow(window);

            FlowDriver.Add(machine);

            // 每台机器一个视图：自己订阅本机流源的生死广播，自己持有一块蒙版层。
            // 不再有按网格登记的静态表，所以第二台机器落地不会覆盖第一台。
            // 覆盖字段前先释放旧视图：否则旧视图的订阅与层会永远留在已销毁的网格下。
            machine.AtlasView?.Dispose();
            machine.AtlasView = new FlowAtlasView(inventory, machine.Flows);
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
        /// <summary>
        /// 按钮回调：把本机所有发射装置的存量整份转成流源。
        /// 一次性动作，不是开关——发完装置就空了，要再发得重新灌水。
        /// </summary>
        private static void Start(FlowMachine machine)
        {
            // 这是原生按钮的委托回调，异常会顺着原生栈冒出去，必须兜住
            try
            {
                machine.Emit();
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("[流量机] 启动失败：" + e);
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
        /// 存量初值给 0——真实数值每帧从容器 tag 读取，装置只是它的运行时镜像。
        /// </summary>
        private static void AttachEmitter(Il2Cpp.GameItem item)
        {
            const int CapacityMl = 1000000;

            Il2Cpp.WaterHelper.InitLiquidContainerItem(item, CapacityMl, 0);

            EmitterDevice device = new EmitterDevice(0d, 25d);
            DeviceRegistry.Register(item.Pointer, device, item);
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
}
