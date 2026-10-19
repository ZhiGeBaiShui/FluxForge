using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 多机器调度器：只管「有哪些机器」和每帧的调用顺序。
    ///
    /// 顺序有讲究：
    ///   ① 剔除已失效的机器（物品被销毁后不再触碰）
    ///   ② 逐台推进：每台机器用自己的网格推进自己的流源名单
    ///
    /// 机器之间没有任何共享状态——网格、名单、推进器都各归各的，
    /// 所以这里不需要分区、不需要按网格分发，也不需要在循环里比对归属。
    /// 装置的状态回写与移除由装置自己的事件驱动，调度器不参与。
    /// </summary>
    public static class FlowDriver
    {
        private static readonly List<FlowMachine> _machines = new List<FlowMachine>();

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
        /// 撤掉一台作废的机器。
        ///
        /// 走到这里说明机器物品已经失效，所以**不能再读它的网格内容**——
        /// 那是在解引用已经归零的原生指针。只做两件不依赖原生对象的事：
        /// 广播流源死亡（订阅方销毁外壳）、撤销显示登记。
        /// </summary>
        private static void Retire(FlowMachine machine)
        {
            try
            {
                machine.Flows.KillAll();
                FlowVisual.UnregisterGrid(machine.Grid);
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
            _machines.Clear();
            DeviceRegistry.Clear();
            FlowVisual.Clear();
        }
    }
}
