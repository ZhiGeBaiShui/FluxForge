using System;
using System.Collections.Generic;

namespace ProbablyBagMod.Mod.Flow.Adapters
{
    /// <summary>
    /// 把游戏背包网格接到框架的运动层：回答「某格上有什么装置」。
    ///
    /// GameGridInventory 内部是 List&lt;GameItem&gt;，没有「按坐标查物品」的接口，
    /// 物品位置存在各自的 shape.minX/minY 里，占格要逐格用 shape.Get 现算。
    /// 所以每帧 Refresh 一次展开成格子表，之后探测就只查字典。
    ///
    /// ⚠ childItems 每次访问都会 new 一个 List，所以只在 Refresh 里取一次。
    ///   而 Refresh 只在机器启动（FlowMachine.Emit）时调——面板布局在启动后视为锁定，
    ///   运行中拖动装置不会反映到这张表里。
    /// </summary>
    public sealed class FlowGridAdapter : IFlowGrid
    {
        private readonly Il2Cpp.GameGridInventory _inventory;
        private readonly Dictionary<long, Il2Cpp.GameItem> _cells
            = new Dictionary<long, Il2Cpp.GameItem>();

        /// <summary>
        /// 最近一次 <see cref="Refresh"/> 见到的物品指针。
        ///
        /// 只存 IntPtr、不存对象：机器退役时网格里的原生对象可能已经失效，
        /// 那时只能用这些纯值去注销装置登记，不能再逐件访问 GameItem。
        /// 布局在启动时锁定（见 <see cref="Refresh"/>），所以这份集合就是本机的装置集合。
        /// </summary>
        private readonly HashSet<IntPtr> _itemPointers = new HashSet<IntPtr>();

        private int _columns;
        private int _rows;

        public FlowGridAdapter(Il2Cpp.GameGridInventory inventory)
        {
            _inventory = inventory;

            // 网格尺寸只由构造时决定，玩家不会中途改，取一次就够
            Il2Cpp.GridShape shape = inventory == null ? null : inventory.inventoryShape;
            if (shape != null)
            {
                _columns = shape.width;
                _rows = shape.height;
            }
        }

        /// <summary>背包还在不在（读档后旧引用会失效）。</summary>
        public bool IsAlive
        {
            get { return _inventory != null && _inventory.Pointer != IntPtr.Zero; }
        }

        /// <summary>最近一次 Refresh 见到的本机物品指针（退役时注销装置登记用）。</summary>
        public IReadOnlyCollection<IntPtr> ItemPointers
        {
            get { return _itemPointers; }
        }

        public bool Contains(int cellX, int cellY)
        {
            return cellX >= 0 && cellY >= 0 && cellX < _columns && cellY < _rows;
        }

        /// <summary>每帧调一次：把背包里所有物品按占格展开成 坐标 → 物品 的表。</summary>
        public void Refresh()
        {
            _cells.Clear();
            _itemPointers.Clear();
            if (!IsAlive)
            {
                return;
            }

            Il2CppSystem.Collections.Generic.List<Il2Cpp.GameItem> items = _inventory.childItems;
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                Il2Cpp.GameItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                // 先记指针再读形状：没有形状的物品也属于本机，退役时同样要注销登记
                _itemPointers.Add(item.Pointer);

                // ⚠ 用 modifiedShape：shape 是物品的**定义形状**，minX/minY 不随摆放更新。
                //   玩家把装置拖到哪，只有 modifiedShape 反映真实格位。
                Il2Cpp.GridShape shape = item.modifiedShape;
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
            Il2Cpp.GameItem item;
            if (!_cells.TryGetValue(Pack(cellX, cellY), out item))
            {
                return null;
            }

            return DeviceRegistry.GetRouter(item.Pointer);
        }

        public IFlowDevice GetReactorAt(int cellX, int cellY)
        {
            Il2Cpp.GameItem item;
            if (!_cells.TryGetValue(Pack(cellX, cellY), out item))
            {
                return null;
            }

            return DeviceRegistry.GetReactor(item.Pointer);
        }

        /// <summary>把两个 int 打包成 long 当字典 key（避免 ValueTuple 在 IL2CPP 下的开销）。</summary>
        private static long Pack(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }
    }
}
