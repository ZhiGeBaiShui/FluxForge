using System;
using System.Collections.Generic;
using FluxForge.Mod.Visual;

namespace FluxForge.Mod
{
    /// <summary>
    /// 逐环境驱动流源与视觉，隔离单网格异常。
    /// 登记与查找属于 <see cref="GridRegistry"/>，本类只负责推进与退役。
    /// </summary>
    public static class FlowDriver
    {
        private static readonly HashSet<FlowRuntime> _visualErrorReported
            = new HashSet<FlowRuntime>();

        /// <summary>由 <see cref="ModHost"/> 每帧调用。</summary>
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
        /// 读档 / 场景卸载时登记表可能已先被 <see cref="Reset"/> 清空，查不到直接返回。
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
        /// 先从登记表摘除，再独立尝试终止流源和释放视图；不注销物品。
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

        /// <summary>读档 / 换场景整体归零，沿用原语义，不逐流源广播死亡。</summary>
        public static void Reset()
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
        /// <see cref="Retire"/> 与 <see cref="Reset"/> 共用同一条路径；
        /// 释放在这里兜底：视图的 Dispose 会碰 Unity 对象，抛异常不能打断其它环境的清理。
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
