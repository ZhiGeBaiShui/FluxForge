using System;
using System.Collections.Generic;
using FluxForge.Mod.Visual;

namespace FluxForge.Mod
{
    /// <summary>
    /// 逐环境驱动流源与视觉，隔离单网格异常。
    /// 登记与查找属于 <see cref="GridRegistry"/>；本类只负责推进、单网格退役
    /// 和两个边界清理（<see cref="Reset"/> / <see cref="Shutdown"/>）。
    /// 两张登记表都是静态根，GC 与场景销毁都不会替我们清空，边界清理必须显式调用。
    /// </summary>
    public static class FlowDriver
    {
        /// <summary>已经报过视觉异常的环境：同一块网格只报一次，避免每帧刷日志。</summary>
        private static readonly HashSet<FlowRuntime> _visualErrorReported
            = new HashSet<FlowRuntime>();

        /// <summary>
        /// 由 <see cref="ModHost"/> 每帧调用。
        /// 倒序遍历：退役会立即从 <see cref="GridRegistry.Runtimes"/> 摘除当前项，
        /// 正序会跳过下一个环境。
        /// </summary>
        public static void Update(double deltaTime)
        {
            List<FlowRuntime> runtimes = GridRegistry.Runtimes;

            for (int i = runtimes.Count - 1; i >= 0; i--)
            {
                FlowRuntime runtime = runtimes[i];

                try
                {
                    runtime.Motion.Tick(deltaTime, runtime.Flows);
                }
                catch (Exception e)
                {
                    FFModEntry.Log.Error("[流源] 推进或存活检查失败，已退役该环境：" + e);
                    Retire(runtime);
                }
            }
        }

        /// <summary>
        /// 由 <see cref="ModHost"/> 每帧在游戏 UI 排版后调用，逐环境推进纯视觉。
        /// 正序保持绘制顺序；失败退役后 i-- 抵消列表前移，避免跳过下一块网格。
        /// </summary>
        public static void LateUpdateVisuals(float deltaTime)
        {
            List<FlowRuntime> runtimes = GridRegistry.Runtimes;

            for (int i = 0; i < runtimes.Count; i++)
            {
                FlowRuntime runtime = runtimes[i];
                FlowVisualLayer view = runtime.Visual;
                if (view == null)
                {
                    continue;
                }

                try
                {
                    view.Tick(deltaTime);
                }
                catch (Exception e)
                {
                    if (_visualErrorReported.Add(runtime))
                    {
                        FFModEntry.Log.Error(
                            "[流源] 视觉推进失败，已退役该环境（其它网格继续绘制）：" + e);
                    }

                    Retire(runtime);
                    i--;
                }
            }
        }

        /// <summary>
        /// 网格销毁回调：按网格引用退役对应环境。
        /// 退出存档 / 卸载时 <see cref="Shutdown"/> 可能已经先清空登记表，这里
        /// 查不到就直接返回；退役本身是幂等的。
        /// </summary>
        internal static void RetireByGrid(GameGridInventory inventory)
        {
            FlowRuntime runtime = GridRegistry.Get(inventory);
            if (runtime == null)
            {
                return;
            }

            Retire(runtime);
        }

        /// <summary>
        /// 单网格退役：先从登记表摘除，再分别兜底终止流源和释放视图。
        /// 不注销装置——<see cref="DeviceRegistry"/> 是全局静态表，不能因为一块
        /// 网格退役就整体清空。
        /// </summary>
        private static void Retire(FlowRuntime runtime)
        {
            GridRegistry.Unregister(runtime.Inventory);
            _visualErrorReported.Remove(runtime);

            // KillAll 与视图释放必须各自兜底：前者会调订阅方（可能碰 Unity 对象），
            // 一旦抛异常，后者若共用同一个 try 就再也不会执行，层和订阅都会泄漏。
            try
            {
                runtime.Flows.KillAll();
            }
            catch (Exception e)
            {
                FFModEntry.Log.Error("[流源] 退役时终止流源失败：" + e);
            }

            // 死亡广播先由仍订阅的视图接收，再退订并销毁绘制层。
            DisposeView(runtime);

            FFModEntry.Log.Log("[流源] 已移除失效的网格环境");
        }

        /// <summary>
        /// 场景边界：终止所有流源、清空装置登记，但保留网格登记。
        ///
        /// 由 <see cref="ModHost"/> 的 sceneUnloaded 调用。场景卸载不等于网格销毁：
        /// 网格对象可能还活着，清掉登记会让它变成没有运行环境的死网格；真正消失
        /// 时由 <see cref="GridLifetime"/> 的 OnDestroy 走 <see cref="RetireByGrid"/>。
        /// 已经失效的环境在这里顺手退役。
        ///
        /// <see cref="DeviceRegistry"/> 是独立的静态根，持有普通 C# 的 GameItem，
        /// Unity 不会替它清；场景切换必须在这里整表清空，否则旧场景的物品会一直
        /// 留在内存里。
        /// </summary>
        public static void Reset()
        {
            FlowRuntime[] runtimes = GridRegistry.Runtimes.ToArray();
            _visualErrorReported.Clear();

            for (int i = 0; i < runtimes.Length; i++)
            {
                if (!runtimes[i].IsAlive)
                {
                    Retire(runtimes[i]);
                    continue;
                }

                runtimes[i].Flows.KillAll();
            }

            DeviceRegistry.Clear();
        }

        /// <summary>
        /// 退出存档 / mod 卸载：连网格登记一起清。
        ///
        /// 先取运行时快照再清表，之后逐个 Dispose 视图（退订生死广播、销毁层）。
        /// 这里不逐个 KillAll：环境已经不再被登记表引用，视图 Dispose 之后整块
        /// 对象交给 GC 即可。
        ///
        /// 可重复调用：<see cref="SaveExited"/>、<see cref="EmporiumDestroyed"/>、
        /// <see cref="FFModEntry.OnDisable"/> 可能先后命中同一次退出，第二次起
        /// 两张表都是空的。
        /// </summary>
        public static void Shutdown()
        {
            FlowRuntime[] runtimes = GridRegistry.Runtimes.ToArray();
            GridRegistry.Clear();
            _visualErrorReported.Clear();

            for (int i = 0; i < runtimes.Length; i++)
            {
                DisposeView(runtimes[i]);
            }

            DeviceRegistry.Clear();
        }

        /// <summary>
        /// 释放一个环境的视觉视图（退订生死广播 + 销毁层 + 清空表），并把引用置空。
        /// <c>Retire</c>、<see cref="Reset"/>（失效分支）与 <see cref="Shutdown"/>
        /// 共用同一条路径；释放在这里兜底：视图的 Dispose 会碰 Unity 对象，
        /// 抛异常不能打断其它环境的清理。
        /// </summary>
        private static void DisposeView(FlowRuntime runtime)
        {
            try
            {
                if (runtime.Visual != null)
                {
                    runtime.Visual.Dispose();
                }
            }
            catch (Exception e)
            {
                FFModEntry.Log.Error("[流源] 释放视图失败：" + e);
            }

            // 即使 Dispose 抛异常也断开引用：半销毁的视图不能再被下一帧 Tick。
            runtime.Visual = null;
        }
    }
}
