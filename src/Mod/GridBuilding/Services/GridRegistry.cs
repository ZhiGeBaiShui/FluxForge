using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 网格引用 ↔ 运行环境的注册表。
    ///
    /// 与 <see cref="DeviceRegistry"/> 同构：登记时创建管理者，之后所有查询都走这里。
    /// 区别是装置登记挂在物品上，网格登记挂在网格尺寸确定的那一刻
    /// （<see cref="GameGridInventory.SetShape"/> 返回时 <c>inventoryShape</c> 才可用，
    /// 私有无参构造的 postfix 里还是 null，所以不能挂构造）。
    /// </summary>
    public static class GridRegistry
    {
        private static readonly Dictionary<GameGridInventory, FlowRuntime> _grids
            = new Dictionary<GameGridInventory, FlowRuntime>();

        /// <summary>遍历用：<see cref="FlowDriver"/> 每帧按顺序驱动，字典本身不能边遍历边删。</summary>
        private static readonly List<FlowRuntime> _runtimes = new List<FlowRuntime>();

        /// <summary>
        /// 网格尺寸确定时创建运行环境并登记。重复调用返回已有实例。
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
        /// 注销登记，不负责终止流源或释放视图——那是 <see cref="FlowDriver"/> 的退役路径。
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

        /// <summary>读档 / 换场景时整体失效。</summary>
        public static void Clear()
        {
            _grids.Clear();
            _runtimes.Clear();
        }

        /// <summary>给 <see cref="FlowDriver"/> 倒序遍历用。</summary>
        internal static List<FlowRuntime> Runtimes
        {
            get { return _runtimes; }
        }
    }
}
