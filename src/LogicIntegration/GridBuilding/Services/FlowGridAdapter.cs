using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 把游戏背包网格接到框架的运动层：回答「某格上有什么装置」。
    ///
    /// 表在挂载 / 离开时增量维护：落到格子时登记（<c>GameItem.AddParent</c> postfix），
    /// 离开容器时注销（<c>GameItem.RemoveParent</c> prefix），拿起时由
    /// <see cref="ItemPickedUp"/> 提前注销。发射路径只做查询，所以运行中拖动、
    /// 新增、卖掉装置都会立即反映到这张表。
    ///
    /// 装置可按格子布局登记路由 / 反应；普通物品按实际形状登记占位，空洞不登记。
    /// </summary>
    public sealed class FlowGridAdapter : IFlowGrid
    {
        private readonly GameGridInventory _inventory;

        /// <summary>装置格 → 该格自己的路由与反应行为。</summary>
        private readonly Dictionary<long, FlowCellBehavior> _deviceCells
            = new Dictionary<long, FlowCellBehavior>();

        /// <summary>物品 → 登记格列表，用于移动、拿起与注销。</summary>
        private readonly Dictionary<GameItem, List<long>> _itemCells
            = new Dictionary<GameItem, List<long>>();

        private readonly HashSet<long> _occupied = new HashSet<long>();

        public FlowGridAdapter(GameGridInventory inventory)
        {
            _inventory = inventory;
        }

        /// <summary>网格 UI 还在不在（销毁后 rectTransform 变成 Unity 的 fake null）。</summary>
        public bool IsAlive
        {
            get { return _inventory.rectTransform != null; }
        }

        public bool Contains(int cellX, int cellY)
        {
            if (cellX < 0 || cellY < 0)
            {
                return false;
            }

            // 尺寸现读：mod 可以在运行时用 SetShape 扩容，缓存会过期。
            // 每次 SetShape 都 new 一个 GridShape，这里读到的永远是最新引用。
            GridShape shape = _inventory.inventoryShape;
            return cellX < shape.width && cellY < shape.height;
        }

        /// <summary>
        /// 装置落到格子时登记。
        ///
        /// 装置按格子布局登记并同步朝向；普通物品登记真实形状占位。
        /// </summary>
        public void Register(GameItem item)
        {
            if (item == null || !IsAlive)
            {
                return;
            }

            // 同网格移动可能只触发 AddParent（旧条目先清掉，避免旧格残留）。
            Unregister(item);
            RegisterCore(item);
        }

        /// <summary>仅刷新已落地登记的物品；拿起后已注销的物品不能被校验回调重新登记。</summary>
        public void RefreshRegistered(GameItem item)
        {
            if (_itemCells.ContainsKey(item))
                Register(item);
        }

        /// <summary>
        /// 全量重建格表。
        ///
        /// 排序和读档都会成批改写 <c>modifiedShape</c>：排序先逐件 Expel 再
        /// UncheckedAccept，读档则在 AddParent 之后才 ValidateShapeState。
        /// 增量的挂载信号拿到的可能不是最终形状，所以在这些批量路径结束后
        /// 统一重建一次。调用方负责在批量变化后主动调这里。
        /// </summary>
        public void Rebuild()
        {
            _deviceCells.Clear();
            _occupied.Clear();
            _itemCells.Clear();

            if (!IsAlive)
            {
                return;
            }

            List<GameItem> items = _inventory.childItems;
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                RegisterCore(items[i]);
            }
        }

        /// <summary>只写表，不先清旧条目；<see cref="Register"/> 与 <see cref="Rebuild"/> 共用。</summary>
        private void RegisterCore(GameItem item)
        {
            if (item == null)
            {
                return;
            }

            GridShape shape = item.modifiedShape;
            if (shape == null)
            {
                return;
            }

            DeviceHost host = DeviceRegistry.GetHost(item);
            if (host == null)
            {
                var keys = new List<long>();
                for (int y = 0; y < shape.height; y++)
                {
                    for (int x = 0; x < shape.width; x++)
                    {
                        if (shape.GetLocal(x, y) == 0) continue;
                        (int worldX, int worldY) = shape.ForwardTransform(x, y);
                        long occupiedKey = Pack(worldX, worldY);
                        _occupied.Add(occupiedKey);
                        keys.Add(occupiedKey);
                    }
                }
                _itemCells[item] = keys;
                return;
            }

            // 朝向和镜像位跟物品变换走；同一个物品上的所有 IFacing 装置都收到同一状态。
            DeviceRegistry.SetFacing(
                item,
                FlowFacingAdapter.FromOrientation(shape.orientation),
                shape.flipped);

            // DeviceAttachment 保证每个 device 都有 anchor tag；
            // 缺 anchor 属于非法物品，直接让字典索引失败，不做 fallback。
            TagState anchorXState = item.state.dict[FlowTags.AnchorX];
            TagState anchorYState = item.state.dict[FlowTags.AnchorY];

            (int anchorX, int anchorY) =
                shape.ForwardTransform(anchorXState.GetInt(), anchorYState.GetInt());

            var deviceCells = new List<long>();
            if (host.CellLayout == null)
            {
                long key = Pack(anchorX, anchorY);
                _deviceCells[key] = new FlowCellBehavior(host.Router, host.Reactor);
                deviceCells.Add(key);
            }
            else
            {
                for (int y = 0; y < shape.height; y++)
                {
                    for (int x = 0; x < shape.width; x++)
                    {
                        if (shape.GetLocal(x, y) == 0) continue;
                        (int worldX, int worldY) = shape.ForwardTransform(x, y);
                        long cellKey = Pack(worldX, worldY);
                        var context = new FlowCellContext(
                            x, y, worldX, worldY, anchorX, anchorY);
                        _deviceCells.Add(cellKey, host.CellLayout.GetCellBehavior(context));
                        deviceCells.Add(cellKey);
                    }
                }
            }
            _itemCells[item] = deviceCells;
        }

        /// <summary>装置离开网格时注销。</summary>
        public void Unregister(GameItem item)
        {
            if (item == null)
            {
                return;
            }

            List<long> keys;
            if (!_itemCells.TryGetValue(item, out keys))
            {
                return;
            }

            _itemCells.Remove(item);
            foreach (long key in keys)
            {
                _deviceCells.Remove(key);
                _occupied.Remove(key);
            }
        }

        public FlowCellKind GetCellKind(int cellX, int cellY)
        {
            long key = Pack(cellX, cellY);
            if (_occupied.Contains(key)) return FlowCellKind.Occupied;
            return _deviceCells.ContainsKey(key) ? FlowCellKind.Device : FlowCellKind.Empty;
        }

        public IFlowRouter GetRouterAt(int cellX, int cellY)
        {
            FlowCellBehavior behavior;
            return _deviceCells.TryGetValue(Pack(cellX, cellY), out behavior)
                ? behavior.Router
                : null;
        }

        public IFlowDevice GetReactorAt(int cellX, int cellY)
        {
            FlowCellBehavior behavior;
            return _deviceCells.TryGetValue(Pack(cellX, cellY), out behavior)
                ? behavior.Reactor
                : null;
        }

        /// <summary>把两个 int 打包成 long 当字典 key（避免 ValueTuple 的额外分配）。</summary>
        private static long Pack(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }
    }
}
