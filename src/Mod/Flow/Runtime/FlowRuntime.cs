using System;
using System.Collections.Generic;
using ProbablyBagMod.Mod.Visual;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 一块网格的运行环境：借用游戏网格，拥有流源、运动采样与视觉视图。
    /// 每次激活只发射当前装置；运行中不轮询布局或追踪装置迁移。
    /// </summary>
    public sealed class FlowRuntime
    {
        /// <summary>
        /// 已做过「从 tag 载入」的物品。
        ///
        /// 装置对象是 tag 的运行时镜像，而 tag 才是存档的真实来源。
        /// 物品第一次被接管时先反向读一次，之后才单向写。
        /// </summary>
        private readonly HashSet<GameItem> _adopted = new HashSet<GameItem>();

        public FlowRuntime(GameGridInventory inventory)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

            // 先读取网格尺寸，再装配自有对象；构造期间不刷新布局、不创建 UGUI 绘制层。
            Grid = new FlowGridAdapter(inventory);
            Flows = new FlowSpawner();
            Motion = new FlowManager(Grid, Flows);
            // 视觉最后装配，完成全部订阅后才交给 Driver 登记及发射。
            // 构造失败时未登记的名单和运动订阅没有外部持有者，可一起回收。
            Visual = new FlowVisualLayer(inventory, Flows);
        }

        /// <summary>本网格的运动层代理（流源靠它查装置、判越界）。</summary>
        public FlowGridAdapter Grid { get; }

        /// <summary>本网格的流源名单与生死广播。</summary>
        public FlowSpawner Flows { get; }

        /// <summary>本网格的流源推进器，订阅 <see cref="Flows"/> 生死事件维护采样记录。</summary>
        public FlowManager Motion { get; }

        /// <summary>借用的游戏网格，不随运行环境退役而释放。</summary>
        public GameGridInventory Inventory { get; }

        /// <summary>
        /// 网格专属的视觉视图，构造时装配，由 <see cref="FlowDriver"/> 驱动与释放。
        /// 为 null 表示已退役。
        /// </summary>
        public FlowVisualLayer Visual;

        /// <summary>只按网格引用存活判断，不依赖任何一件发射装置。</summary>
        public bool IsAlive
        {
            get { return Inventory != null; }
        }

        /// <summary>
        /// 激活：仅把当前发射装置的存量整份转成一个流源。
        /// 空库存或直接父网格不匹配时不刷新、不出生、不扣量。
        /// </summary>
        public bool Emit(GameItem emitterItem)
        {
            if (!IsAlive || emitterItem == null)
            {
                return false;
            }

            EmitterDevice emitter = DeviceRegistry.Get<EmitterDevice>(emitterItem);
            GameGridInventory parent = emitterItem.parentInventory as GameGridInventory;
            if (emitter == null || parent == null || !ReferenceEquals(parent, Inventory))
            {
                return false;
            }

            if (FlowStock.Total(emitterItem) <= 0d)
            {
                return false;
            }

            // 每次有库存的有效尝试仅刷新一次，之后运动只查询布局快照。
            Grid.Refresh();

            List<GameItem> items = Inventory.childItems;
            if (items == null)
            {
                return false;
            }

            // 完整接管先于 Accept，避免同步出生事件读到尚未载入的装置镜像。
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                CollectorDevice collector = DeviceRegistry.Get<CollectorDevice>(item);
                if (collector != null)
                {
                    Adopt(item, collector);
                    continue;
                }

                IceBlockDevice iceBlock = DeviceRegistry.Get<IceBlockDevice>(item);
                if (iceBlock != null)
                {
                    Adopt(item, iceBlock);
                }
            }

            return EmitFrom(emitterItem, emitter);
        }

        /// <summary>
        /// 把一件发射装置里的存量整份转成一个流源，并从容器里扣掉。
        /// 返回是否真的发出了流源。
        /// </summary>
        private bool EmitFrom(GameItem item, EmitterDevice emitter)
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

            // 容器 tag 是唯一真相：发射前按表读一份完整属性
            emitter.Stock = FlowStock.Read(item);

            if (!emitter.EmitAll(origin, direction, Flows))
            {
                return false;
            }

            // 取走存量：按映射表把各属性对应的 tag 写 0
            FlowStock.Take(item);
            return true;
        }

        /// <summary>
        /// 物品第一次被本网格接管时，用 tag 里的值覆盖装置存量。
        /// 读档后 tag 才是真相，装置对象只是它的运行时镜像。
        /// </summary>
        private void Adopt(GameItem item, object device)
        {
            if (!_adopted.Add(item))
            {
                return;
            }

            FlowStock.ReadInto(item, device);
        }

        /// <summary>
        /// 从装置物品解出「从哪发、往哪发」。
        ///
        /// ⚠ 必须读 <c>modifiedShape</c> 而不是 <c>shape</c>：
        ///   后者是物品的**定义形状**，minX/minY 是定义时的局部原点，拖到哪里都不会变。
        ///   拖放落点写的是 modifiedShape。
        ///
        /// 朝向同样在 modifiedShape 上：orientation 0..3 表示 0°/逆90°/180°/顺90°，
        ///   物品原图朝上，所以方向码 = (4 - orientation) &amp; 3 → 0上 1右 2下 3左。
        /// </summary>
        private static bool ResolveEmit(GameItem item, out FlowPos origin, out int direction)
        {
            origin = null;
            direction = 0;

            // 调用方已跳过 null 物品，这里直接读形状
            GridShape shape = item.modifiedShape;
            if (shape == null)
            {
                return false;
            }

            direction = (4 - (shape.orientation & 3)) & 3;

            // Floor 判格，中心位于格左上边界向右下各半格处。
            origin = new FlowPos(shape.minX + 0.5d, shape.minY + 0.5d);
            return true;
        }
    }
}
