using System;
using System.Collections.Generic;
using ProbablyBagMod.Mod.Flow.Adapters;
using ProbablyBagMod.Mod.Flow.View;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 一台流量机的运行时状态：物品本体 + 它那块网格 + 网格适配器。
    ///
    /// 按机器分对象（而不是用静态网格引用）：第二台机器落地时不会覆盖第一台，
    /// 流源出生就带着「自己属于哪块网格」，多台机器自然互不干扰。
    ///
    /// 装置归属不做注册：物品创建时玩家还没决定把它拖进哪个面板，
    /// 所以每台机器只在启动时遍历一次本机网格、按指针反查装置。
    /// 装置运行中的状态变化由装置自己播报事件，本类不轮询。
    /// </summary>
    public sealed class FlowMachine
    {
        /// <summary>
        /// 已做过「从 tag 载入」的物品。
        ///
        /// 装置对象是 tag 的运行时镜像，而 tag 才是存档的真实来源。
        /// 物品第一次被接管时先反向读一次，之后才单向写。
        /// </summary>
        private readonly HashSet<IntPtr> _adopted = new HashSet<IntPtr>();

        public FlowMachine(Il2Cpp.GameItem item, Il2Cpp.GameGridInventory inventory)
        {
            Item = item;
            Inventory = inventory;
            Grid = new FlowGridAdapter(inventory);
            Flows = new FlowSpawner();
            Motion = new FlowManager(Grid);
        }

        /// <summary>本机网格的运动层代理（流源靠它查装置、判越界）。</summary>
        public FlowGridAdapter Grid { get; }

        /// <summary>本机的流源名单与生死广播。</summary>
        public FlowSpawner Flows { get; }

        /// <summary>本机的流源推进器。与 <see cref="Flows"/> 互不隶属，由本类接线。</summary>
        public FlowManager Motion { get; }

        /// <summary>流量机物品本身。物品被销毁（读档 / 删除）即本机作废。</summary>
        public Il2Cpp.GameItem Item { get; }

        /// <summary>面板里的那块网格。</summary>
        public Il2Cpp.GameGridInventory Inventory { get; }

        /// <summary>
        /// 本机专属的视觉视图（REFACTOR-A §5）：持有蒙版层、订阅本机流源的生死广播。
        /// 由 <see cref="Mod.FlowItemSetup"/> 在创建机器时装配，<see cref="FlowDriver"/> 驱动与释放。
        /// 为 null 表示视图还没装配（或已退役）——渲染层必须容忍这一点。
        /// </summary>
        public FlowAtlasView AtlasView;

        /// <summary>物品与网格都还指向有效原生对象。</summary>
        public bool IsAlive
        {
            get
            {
                return Item != null && Item.Pointer != IntPtr.Zero
                    && Inventory != null && Inventory.Pointer != IntPtr.Zero;
            }
        }

        /// <summary>
        /// 启动：把每个发射装置里的存量**整份**转成一个流源。
        ///
        /// 一次性而不是按间隔连发：存量本来就是一个整体，
        /// 拆成几十个小流源会让下游装置处理一堆碎片，语义也不对。
        /// </summary>
        public void Emit()
        {
            // 机器物品或网格已失效时不能再碰原生对象
            if (!IsAlive)
            {
                return;
            }

            // 布局在按下启动的那一刻就定了：格子表这里建一次，
            // 之后的碰撞探测只查字典，不再每帧遍历 childItems
            Grid.Refresh();

            Il2CppSystem.Collections.Generic.List<Il2Cpp.GameItem> items = Inventory.childItems;
            if (items == null)
            {
                return;
            }

            int emitted = 0;
            for (int i = 0; i < items.Count; i++)
            {
                Il2Cpp.GameItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                EmitterDevice emitter = DeviceRegistry.Get<EmitterDevice>(item.Pointer);
                if (emitter != null)
                {
                    if (EmitFrom(item, emitter))
                    {
                        emitted++;
                    }

                    continue;
                }

                CollectorDevice collector = DeviceRegistry.Get<CollectorDevice>(item.Pointer);
                if (collector != null)
                {
                    Adopt(item, collector.Stock);
                    continue;
                }

                IceBlockDevice iceBlock = DeviceRegistry.Get<IceBlockDevice>(item.Pointer);
                if (iceBlock != null)
                {
                    // 冰块的存量不是 FlowProperties，单独读一次；
                    // 读档后 tag 才是真相，装置对象只是它的运行时镜像
                    if (_adopted.Add(item.Pointer))
                    {
                        iceBlock.Moisture = FlowTags.ReadInt(item.state, FlowTags.Stock, 0);
                        iceBlock.Temperature = FlowTags.ReadFloat(
                            item.state, FlowTags.IceTemperature, 0f);
                    }
                }
            }

            MelonLoader.MelonLogger.Msg("[流量机] 启动，发出 " + emitted + " 个流源");
        }

        /// <summary>
        /// 把一件发射装置里的存量整份转成一个流源，并从容器里扣掉。
        /// 返回是否真的发出了流源。
        /// </summary>
        private bool EmitFrom(Il2Cpp.GameItem item, EmitterDevice emitter)
        {
            FlowPos origin;
            int direction;
            if (!ResolveEmit(item, out origin, out direction))
            {
                return false;
            }

            if (!Grid.Contains((int)Math.Floor(origin.X), (int)Math.Floor(origin.Y)))
            {
                return false;
            }

            // 容器 tag 是唯一真相：发射前读一次，发射后按实扣量对账
            double available = LiquidStock.Total(item);
            if (available <= 0d)
            {
                return false;
            }

            emitter.Stock.Moisture = available;
            emitter.Stock.Temperature = LiquidStock.Temperature(item, emitter.Stock.Temperature);

            if (!emitter.EmitAll(origin, direction, Flows))
            {
                return false;
            }

            double consumed = LiquidStock.Consume(item, available);
            emitter.Stock.Moisture = available - consumed;
            FlowTags.WriteInt(item.state, FlowTags.Stock, (int)emitter.Stock.Moisture);
            FlowTags.WriteFloat(
                item.state, FlowTags.FlowTemperature, (float)emitter.Stock.Temperature);
            return true;
        }

        /// <summary>
        /// 物品第一次被本机接管时，用 tag 里的值覆盖装置存量。
        /// </summary>
        private void Adopt(Il2Cpp.GameItem item, FlowProperties stock)
        {
            if (!_adopted.Add(item.Pointer))
            {
                return;
            }

            stock.Moisture = FlowTags.ReadInt(item.state, FlowTags.Stock, 0);
            stock.Temperature = FlowTags.ReadFloat(
                item.state, FlowTags.FlowTemperature, 0f);
        }

        /// <summary>
        /// 从装置物品解出「从哪发、往哪发」。
        ///
        /// ⚠ 必须读 <c>modifiedShape</c>（+0x1A0）而不是 <c>shape</c>（+0x198）：
        ///   后者是物品的**定义形状**，minX/minY 是定义时的局部原点，拖到哪里都不会变。
        ///   拖放落点写的是 modifiedShape（SlotMarker.AcceptUnchecked → SetTransform）。
        ///
        /// 朝向同样在 modifiedShape 上：orientation 0..3 表示 0°/逆90°/180°/顺90°，
        ///   物品原图朝上，所以方向码 = (4 - orientation) &amp; 3 → 0上 1右 2下 3左。
        /// </summary>
        private static bool ResolveEmit(Il2Cpp.GameItem item, out FlowPos origin, out int direction)
        {
            origin = null;
            direction = 0;

            // 调用方已跳过 null 物品，这里直接读形状
            Il2Cpp.GridShape shape = item.modifiedShape;
            if (shape == null)
            {
                return false;
            }

            direction = (4 - (shape.orientation & 3)) & 3;

            // 发射点就是装置所在的整数格位。逻辑层不补半格——
            // 「落在格子中间」是渲染层的事，由视觉映射统一处理。
            origin = new FlowPos(shape.minX, shape.minY);
            return true;
        }
    }
}
