using System.Collections.Generic;
using ProbablyBagMod.Mod.Flow.View;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 多机器调度器：只管「有哪些机器」和每帧的调用顺序。
    ///
    /// 顺序有讲究：
    ///   ① OnUpdate 剔除已失效的机器（物品被销毁后不再触碰）
    ///   ② OnUpdate 逐台推进逻辑：每台机器用自己的网格推进自己的流源名单
    ///   ③ OnLateUpdate 逐台推进视觉：每台机器用自己的视图画自己的流源
    ///
    /// 机器之间没有任何共享状态——网格、名单、推进器、视图都各归各的，
    /// 所以这里不需要分区、不需要按网格分发，也不需要在循环里比对归属。
    /// 装置的状态回写与移除由装置自己的事件驱动，调度器不参与。
    /// </summary>
    public static class FlowDriver
    {
        private static readonly List<FlowMachine> _machines = new List<FlowMachine>();

        /// <summary>
        /// 已经报过视觉错误的机器：报错只报一次，但按机器记账，
        /// 不因为 A 机器出过错就把 B 机器后续的错误一起吞掉。
        /// </summary>
        private static readonly HashSet<FlowMachine> _visualErrorReported
            = new HashSet<FlowMachine>();

        /// <summary>流量机创建时登记。机器物品被销毁后会在下一次 Update 里自动剔除。</summary>
        public static void Add(FlowMachine machine)
        {
            _machines.Add(machine);
        }

        /// <summary>由 MelonMod.OnUpdate 每帧调用。</summary>
        public static void Update(double deltaTime)
        {
            for (int i = _machines.Count - 1; i >= 0; i--)
            {
                FlowMachine machine = _machines[i];

                // 物品被销毁后原生指针失效，再访问就会碰到野指针。
                // 一台机器出问题不该连累其他机器：这里把它的流源和显示一并撤掉。
                if (!machine.IsAlive)
                {
                    // 先摘出表再退役：退役里的广播会调订阅方，万一它抛异常，
                    // 已经摘掉的机器不会永远留在表里每帧重试
                    _machines.RemoveAt(i);
                    Retire(machine);
                    continue;
                }

                try
                {
                    machine.Motion.Tick(deltaTime, machine.Flows);
                }
                catch (System.Exception e)
                {
                    // 装置在这条链上会碰原生对象，一旦抛异常而没人兜，
                    // 后面所有机器当帧都不再推进，且下一帧照样复现——等于永久饿死
                    MelonLoader.MelonLogger.Error("[流量机] 推进失败，已退役该机器：" + e);
                    _machines.RemoveAt(i);
                    Retire(machine);
                }
            }
        }

        /// <summary>
        /// 由 MelonMod.OnLateUpdate 每帧调用：逐台推进**纯视觉**（不碰逻辑）。
        ///
        /// 为什么放在 LateUpdate：逻辑帧结束时游戏 UI 还没排版完，
        /// 那一刻算出的坐标会被下一帧的布局覆盖掉。
        ///
        /// 单台 try/catch：一台机器的网格/原生对象出问题，不应该让其它机器当帧全部停画；
        /// 失败只报一次（每帧复现的报错会刷屏，反而掩盖第一条有用信息）。
        /// </summary>
        public static void LateUpdateVisuals(float deltaTime)
        {
            for (int i = 0; i < _machines.Count; i++)
            {
                FlowMachine machine = _machines[i];
                FlowAtlasView view = machine == null ? null : machine.AtlasView;
                if (view == null)
                {
                    continue;
                }

                try
                {
                    view.Tick(deltaTime);
                }
                catch (System.Exception e)
                {
                    // 每台机器只报一次：每帧复现的报错会刷屏，反而掩盖第一条有用信息
                    if (_visualErrorReported.Add(machine))
                    {
                        MelonLoader.MelonLogger.Error(
                            "[流量机] 视觉推进失败，已释放该机器的视图（其它机器继续绘制）：" + e);
                    }

                    // 出错的视图不能留到下一帧再 Tick：同一份坏数据大概率每帧复现，
                    // 等于每帧抛一次。释放并摘掉后，这台机器退回「无视觉」，逻辑照常推进。
                    view.Dispose();
                    machine.AtlasView = null;
                }
            }
        }

        /// <summary>
        /// 撤掉一台作废的机器。
        ///
        /// 走到这里说明机器物品已经失效，所以**不能再读它的网格内容**——
        /// 那是在解引用已经归零的原生指针。只做两件不依赖原生对象的事：
        /// 广播流源死亡（视图据此收残影）、释放本机视图（退订 + 销毁层）。
        /// </summary>
        private static void Retire(FlowMachine machine)
        {
            // 机器已摘出调度表：它的「已报错」记账也一起丢掉，避免表无限增长
            _visualErrorReported.Remove(machine);

            try
            {
                machine.Flows.KillAll();

                // KillAll 之后再 Dispose：订阅还在，死亡广播才会被本视图接住；
                // Dispose 里退订并销毁层，之后这台机器不可能再往旧网格上画
                if (machine.AtlasView != null)
                {
                    machine.AtlasView.Dispose();
                    machine.AtlasView = null;
                }
            }
            catch (System.Exception e)
            {
                // 退役本身也在处理原生订阅方，失败只能记下来——机器已摘出表，不会重复
                MelonLoader.MelonLogger.Error("[流量机] 退役过程出错：" + e);
            }

            MelonLoader.MelonLogger.Msg("[流量机] 已移除失效的机器");
        }

        /// <summary>读档 / 换场景时整体归零（旧机器、旧网格、旧流源都已失效）。</summary>
        public static void Reset()
        {
            // 逐台释放视图：每台都是「退订 + 销毁层 + 清表」，
            // 漏掉任何一台都会留下订阅，下次开局重复订阅同一批流源
            for (int i = 0; i < _machines.Count; i++)
            {
                FlowMachine machine = _machines[i];
                FlowAtlasView view = machine == null ? null : machine.AtlasView;
                if (view == null)
                {
                    continue;
                }

                try
                {
                    view.Dispose();
                }
                catch (System.Exception e)
                {
                    MelonLoader.MelonLogger.Error("[流量机] 重置时释放视图失败：" + e);
                }

                machine.AtlasView = null;
            }

            _machines.Clear();
            _visualErrorReported.Clear();
            DeviceRegistry.Clear();
        }
    }
}
