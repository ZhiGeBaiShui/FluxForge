using System;
using System.Collections.Generic;
using ProbablyBagMod.Mod.Visual;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 按网格对象引用管理唯一运行环境，逐环境隔离逻辑与视觉异常。
    /// 装置登记属于物品，运行环境只拥有自己的流源与视图。
    /// </summary>
    public static class FlowDriver
    {
        private static readonly List<FlowRuntime> _runtimes = new List<FlowRuntime>();
        private static readonly Dictionary<GameGridInventory, FlowRuntime> _runtimesByGrid
            = new Dictionary<GameGridInventory, FlowRuntime>();
        private static readonly HashSet<FlowRuntime> _visualErrorReported
            = new HashSet<FlowRuntime>();

        /// <summary>首次激活时按网格创建，同一网格的所有发射装置共用环境。</summary>
        public static FlowRuntime GetOrCreate(GameGridInventory inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (_runtimesByGrid.TryGetValue(inventory, out FlowRuntime existing))
            {
                try
                {
                    if (existing.IsAlive)
                    {
                        return existing;
                    }
                }
                catch (Exception e)
                {
                    PBModEntry.Log.Error("[流源] 网格存活检查失败，已退役旧环境：" + e);
                }

                // 同键失效项必须先从双表摘除并释放，才能创建替代环境。
                Retire(existing);
            }

            // 构造完成后才登记；登记失败也要释放已构造的自有视图与订阅。
            FlowRuntime runtime = new FlowRuntime(inventory);
            try
            {
                _runtimesByGrid.Add(inventory, runtime);
                _runtimes.Add(runtime);
            }
            catch
            {
                Retire(runtime);
                throw;
            }

            return runtime;
        }

        /// <summary>由 <see cref="ModHost"/> 每帧调用。</summary>
        public static void Update(double deltaTime)
        {
            for (int i = _runtimes.Count - 1; i >= 0; i--)
            {
                FlowRuntime runtime = _runtimes[i];

                // 存活检查也在异常边界内，坏网格不能拖住其它环境。
                try
                {
                    if (!runtime.IsAlive)
                    {
                        Retire(runtime);
                        continue;
                    }

                    runtime.Motion.Tick(deltaTime, runtime.Flows);
                }
                catch (Exception e)
                {
                    PBModEntry.Log.Error("[流源] 推进或存活检查失败，已退役该环境：" + e);
                    Retire(runtime);
                }
            }
        }

        /// <summary>
        /// 由 <see cref="ModHost"/> 每帧在游戏 UI 排版后调用，逐环境推进纯视觉。
        /// </summary>
        public static void LateUpdateVisuals(float deltaTime)
        {
            for (int i = 0; i < _runtimes.Count; i++)
            {
                FlowRuntime runtime = _runtimes[i];
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
                        PBModEntry.Log.Error(
                            "[流源] 视觉推进失败，已退役该环境（其它网格继续绘制）：" + e);
                    }

                    Retire(runtime);
                    i--;
                }
            }
        }

        /// <summary>
        /// 先从双表摘除，再独立尝试终止流源和释放视图；不读取失效网格，不注销物品。
        /// </summary>
        private static void Retire(FlowRuntime runtime)
        {
            _runtimes.Remove(runtime);
            if (_runtimesByGrid.TryGetValue(runtime.Inventory, out FlowRuntime registered)
                && ReferenceEquals(registered, runtime))
            {
                _runtimesByGrid.Remove(runtime.Inventory);
            }

            _visualErrorReported.Remove(runtime);

            // KillAll 与视图释放必须各自兜底：前者会调订阅方（可能碰 Unity 对象），
            // 一旦抛异常，后者若共用同一个 try 就再也不会执行，层和订阅都会泄漏。
            try
            {
                runtime.Flows.KillAll();
            }
            catch (Exception e)
            {
                PBModEntry.Log.Error("[流源] 退役时终止流源失败：" + e);
            }

            // 死亡广播先由仍订阅的视图接收，再退订并销毁绘制层。
            DisposeView(runtime);

            PBModEntry.Log.Log("[流源] 已移除失效的网格环境");
        }

        /// <summary>读档 / 换场景整体归零，沿用原语义，不逐流源广播死亡。</summary>
        public static void Reset()
        {
            FlowRuntime[] runtimes = _runtimes.ToArray();
            _runtimes.Clear();
            _runtimesByGrid.Clear();
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
                PBModEntry.Log.Error("[流源] 释放视图失败：" + e);
            }

            // 即使 Dispose 抛异常也断开引用：半销毁的视图不能再被下一帧 Tick。
            runtime.Visual = null;
        }
    }
}
