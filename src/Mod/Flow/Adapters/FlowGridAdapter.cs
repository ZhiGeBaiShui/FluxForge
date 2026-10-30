using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 把游戏背包网格接到框架的运动层：回答「某格上有什么装置」。
    ///
    /// GameGridInventory 内部是 List&lt;GameItem&gt;，没有「按坐标查物品」的接口，
    /// 物品位置存在各自的 shape.minX/minY 里，占格要逐格用 shape.Get 现算。
    /// 所以发射时 Refresh 一次展开成格子表，之后探测就只查字典。
    ///
    /// ⚠ childItems 每次访问都会 new 一个 List，所以只在 Refresh 里取一次。
    ///   而 Refresh 只在有效发射尝试（FlowRuntime.Emit）时调，之后使用布局快照，
    ///   运行中拖动装置不会反映到这张表里。
    ///
    /// TODO(网格增量维护) 下面三条是当前"发射时全量刷新"造成的已知缺陷，改造后一并消除：
    ///   1. 玩家拿起 / 放下物品后 _cells 仍指向旧格位 → 流源会撞上"已经不在那儿"的装置：
    ///      装置被凭空融化、tag 被写、流源停在空格上；
    ///   2. 物品被卖掉（已从网格移除）时条目仍在 → TryReact 会继续改那件已离场物品的 tag；
    ///   3. 运行中新拖入的装置不被识别，要重新激活一次才生效。
    /// 计划：改为在「拿起」「放下」「移出网格」三个时机增量维护 _cells；
    ///      届时不再需要发射时的全量 Refresh，FlowDeviceEvents 里的"物品先死"窗口也随之关闭。
    /// </summary>
    public sealed class FlowGridAdapter : IFlowGrid
    {
        private readonly GameGridInventory _inventory;
        private readonly Dictionary<long, GameItem> _cells
            = new Dictionary<long, GameItem>();

        /// <summary>
        /// 最近一次 <see cref="Refresh"/> 见到的物品。
        ///
        /// 只保存布局快照中的对象引用；网格退役不据此注销装置，
        /// 因为装置可能已被转移到其它容器，登记生命周期属于物品。
        /// </summary>
        private readonly HashSet<GameItem> _items = new HashSet<GameItem>();

        private int _columns;
        private int _rows;

        public FlowGridAdapter(GameGridInventory inventory)
        {
            _inventory = inventory;

            // 网格尺寸只由构造时决定，玩家不会中途改，取一次就够
            GridShape shape = inventory == null ? null : inventory.inventoryShape;
            if (shape != null)
            {
                _columns = shape.width;
                _rows = shape.height;
            }
        }

        /// <summary>背包还在不在（读档后旧引用会失效）。</summary>
        public bool IsAlive
        {
            get { return _inventory != null; }
        }

        /// <summary>最近一次 Refresh 见到的网格物品。</summary>
        public IReadOnlyCollection<GameItem> Items
        {
            get { return _items; }
        }

        public bool Contains(int cellX, int cellY)
        {
            return cellX >= 0 && cellY >= 0 && cellX < _columns && cellY < _rows;
        }

        /// <summary>有效发射尝试时调一次，把物品按占格展开成坐标到物品的表。</summary>
        public void Refresh()
        {
            _cells.Clear();
            _items.Clear();
            if (!IsAlive)
            {
                return;
            }

            System.Collections.Generic.List<GameItem> items = _inventory.childItems;
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                // 先记物品再读形状，没有形状的物品也记录在布局快照中。
                _items.Add(item);

                // ⚠ 用 modifiedShape：shape 是物品的**定义形状**，minX/minY 不随摆放更新。
                //   玩家把装置拖到哪，只有 modifiedShape 反映真实格位。
                GridShape shape = item.modifiedShape;
                if (shape == null)
                {
                    continue;
                }

                int maxX = shape.minX + shape.width;
                int maxY = shape.minY + shape.height;
                for (int y = shape.minY; y < maxY; y++)
                {
                    for (int x = shape.minX; x < maxX; x++)
                    {
                        if (shape.Get(x, y) != 0)
                        {
                            _cells[Pack(x, y)] = item;
                        }
                    }
                }
            }
        }

        public IFlowRouter GetRouterAt(int cellX, int cellY)
        {
            GameItem item;
            if (!_cells.TryGetValue(Pack(cellX, cellY), out item))
            {
                return null;
            }

            return DeviceRegistry.GetRouter(item);
        }

        public IFlowDevice GetReactorAt(int cellX, int cellY)
        {
            GameItem item;
            if (!_cells.TryGetValue(Pack(cellX, cellY), out item))
            {
                return null;
            }

            return DeviceRegistry.GetReactor(item);
        }

        /// <summary>把两个 int 打包成 long 当字典 key（避免 ValueTuple 的额外分配）。</summary>
        private static long Pack(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }
    }
}
