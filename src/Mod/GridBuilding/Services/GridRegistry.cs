using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 网格引用 ↔ 运行环境的注册表。
    ///
    /// 与 <see cref="DeviceRegistry"/> 一样是进程级静态根：GC 不会替我们清空静态表，
    /// 也没有游戏对象能替它做这件事，只能靠显式调用。
    ///   · 网格在公开构造函数返回时登记，生命周期与 GameGridInventory 对齐；
    ///     SetShape 只做幂等确认（扩容由适配器现读 inventoryShape 生效）；
    ///   · 网格真正销毁时由 GridLifetime 回调 FlowDriver.Retire 逐条摘除；
    ///   · 退出存档 / mod 卸载由 FlowDriver.Shutdown 整体清空。
    /// FlowDriver.Reset 不会整体清空它，只把已经失效的条目逐条退役——场景卸载
    /// 不代表网格对象已经销毁。
    /// </summary>
    public static class GridRegistry
    {
        private static readonly Dictionary<GameGridInventory, FlowRuntime> _grids
            = new Dictionary<GameGridInventory, FlowRuntime>();

        /// <summary>遍历用列表：字典不能边遍历边删，所以另存一份，由增删路径同步维护。</summary>
        private static readonly List<FlowRuntime> _runtimes = new List<FlowRuntime>();

        /// <summary>
        /// 网格出现时创建运行环境并登记。重复调用返回已有实例，不会重复挂 GridLifetime。
        /// </summary>
        public static FlowRuntime Register(GameGridInventory inventory)
        {
            FlowRuntime existing;
            if (_grids.TryGetValue(inventory, out existing))
            {
                return existing;
            }

            FlowRuntime runtime = new FlowRuntime(inventory);
            _grids.Add(inventory, runtime);
            _runtimes.Add(runtime);

            // 网格销毁时由这个节点回调注销；登记在前，销毁回调才查得到。
            GridLifetime.Attach(inventory);

            return runtime;
        }

        /// <summary>取该网格的运行环境；未登记返回 null。</summary>
        public static FlowRuntime Get(GameGridInventory inventory)
        {
            FlowRuntime runtime;
            return _grids.TryGetValue(inventory, out runtime) ? runtime : null;
        }

        /// <summary>
        /// 对外统一的发射入口：从物品当前网格取运行环境并发射。
        /// 返回 false 表示没有可发射的存量或形状无效。
        ///
        /// 构造期登记已完成；不变量强校验尚未落地——这里没有显式守卫，
        /// 查不到运行环境时会直接空引用。调用方仍必须保证该网格已登记。
        /// </summary>
        public static bool Emit(GameItem item)
        {
            GameGridInventory parent = (GameGridInventory)item.parentInventory;
            return Get(parent).Emit(item);
        }

        /// <summary>
        /// 从登记表摘除，但不终止流源、不释放视图——完整的退役流程在
        /// <c>FlowDriver.Retire</c>。本方法只负责让登记表与驱动列表保持一致。
        /// </summary>
        public static void Unregister(GameGridInventory inventory)
        {
            FlowRuntime runtime;
            if (!_grids.TryGetValue(inventory, out runtime))
            {
                return;
            }

            _grids.Remove(inventory);
            _runtimes.Remove(runtime);
        }

        /// <summary>
        /// 整体清空登记表。由 <see cref="FlowDriver.Shutdown"/> 在退出存档 /
        /// mod 卸载时调用；调用方必须先取 <see cref="Runtimes"/> 快照，清完就
        /// 再也拿不到环境去 Dispose 视图。
        /// </summary>
        public static void Clear()
        {
            _grids.Clear();
            _runtimes.Clear();
        }

        /// <summary>内部列表，给 <see cref="FlowDriver"/> 倒序遍历用；调用方不得增删。</summary>
        internal static List<FlowRuntime> Runtimes
        {
            get { return _runtimes; }
        }
    }
}
