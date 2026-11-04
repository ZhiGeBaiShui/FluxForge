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
    /// 登记粒度是 anchor 格（物品定义里的中心格）：一个装置只占表里的一格，
    /// 流源沿形状的其它格走到中心格才触发路由/反应。
    /// </summary>
    public sealed class FlowGridAdapter : IFlowGrid
    {
        private readonly GameGridInventory _inventory;

        /// <summary>anchor 格 → 物品。</summary>
        private readonly Dictionary<long, GameItem> _cells
            = new Dictionary<long, GameItem>();

        /// <summary>物品 → 它登记在哪个格；注销时不用遍历 _cells。</summary>
        private readonly Dictionary<GameItem, long> _itemCells
            = new Dictionary<GameItem, long>();

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
        /// 只有参与路由/反应的装置才进表；纯数据物品没有 anchor，也没有
        /// router/reactor，走到这里会直接返回。朝向也在这里同步，因为分流器的
        /// 通道方向依赖物品当前朝向。
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
            _cells.Clear();
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

            IFlowRouter router = DeviceRegistry.GetRouter(item);
            IFlowDevice reactor = DeviceRegistry.GetReactor(item);
            if (router == null && reactor == null)
            {
                return;
            }

            // 路由装置才有方向：朝向跟随物品旋转，装置内部据此把「相对通道」
            // 换算成世界方向。纯反应装置（只改属性）没有 Facing。
            if (router != null)
            {
                router.Facing = FlowFacing.FromOrientation(shape.orientation);
            }

            // DeviceAttachment 保证每个 device 都有 anchor tag；
            // 缺 anchor 属于非法物品，直接让字典索引失败，不做 fallback。
            TagState anchorXState = item.state.dict[FlowTags.AnchorX];
            TagState anchorYState = item.state.dict[FlowTags.AnchorY];

            (int anchorX, int anchorY) =
                shape.ForwardTransform(anchorXState.GetInt(), anchorYState.GetInt());

            long key = Pack(anchorX, anchorY);
            _cells[key] = item;
            _itemCells[item] = key;
        }

        /// <summary>装置离开网格时注销。</summary>
        public void Unregister(GameItem item)
        {
            if (item == null)
            {
                return;
            }

            long key;
            if (!_itemCells.TryGetValue(item, out key))
            {
                return;
            }

            _itemCells.Remove(item);
            _cells.Remove(key);
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
