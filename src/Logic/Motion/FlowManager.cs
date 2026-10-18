using System;
using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 运动层（单例，全局唯一）：管「活着的流源」的推进。
    ///
    /// 只管推进，不向上通报每帧位置——视觉层在 <c>LateUpdate</c> 里直接读
    /// <see cref="FlowEntity.Position"/>，因为逻辑帧结束时游戏 UI 还没排完版，
    /// 那一刻算出的像素坐标到 UI 真正落地前还会变。
    ///
    /// 收人 / 送人（<see cref="Accept"/> / <see cref="Remove"/>）由
    /// <see cref="FlowSpawner"/> 驱动——本类不持有生命周期事件，不认识 mod 层。
    ///
    /// 网格不在这里持有：每个流源自带 <see cref="FlowEntity.Grid"/>，
    /// 所以多台装置（多个网格）的流源可以同时在同一个列表里推进而互不干扰。
    /// </summary>
    public sealed class FlowManager
    {
        public static FlowManager Instance { get; } = new FlowManager();

        /// <summary>
        /// 撞上装置时停在装置格内的偏移量（沿运动方向前进一点点）。
        /// 让位置明确落在装置格内部、不压在格边界上：
        /// 下一帧起点格仍是装置格，探测从下一格开始，本装置只触发一次。
        /// 1e-3 格远小于一格，视觉上不可见。
        /// </summary>
        private const double StopEpsilon = 1e-3;

        private readonly List<FlowEntity> _active = new List<FlowEntity>();

        private FlowManager()
        {
        }

        /// <summary>当前管理的流源。</summary>
        public IReadOnlyList<FlowEntity> ActiveEntities
        {
            get { return _active; }
        }

        /// <summary>由 <see cref="FlowSpawner"/> 调用：把出队的流源纳入管理。</summary>
        public void Accept(FlowEntity entity)
        {
            _active.Add(entity);
        }

        /// <summary>由 <see cref="FlowSpawner"/> 调用：把流源移出管理。</summary>
        public void Remove(FlowEntity entity)
        {
            _active.Remove(entity);
        }

        /// <summary>清空活跃列表（读档 / 重开 / 测试隔离用）。</summary>
        public void Reset()
        {
            _active.Clear();
        }

        /// <summary>
        /// 回收落在某块网格上的所有流源。
        /// 网格被销毁（流量机被删 / 面板失效）时，它上面的流源失去了推进依据，
        /// 留着会让 _active 只增不减，且下一帧仍会去查已经是废墟的装置表。
        /// </summary>
        public void KillAllOfGrid(IFlowGrid grid)
        {
            if (grid == null)
            {
                return;
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_active[i].Grid, grid))
                {
                    FlowSpawner.Instance.Kill(_active[i]);
                }
            }
        }

        /// <summary>
        /// 推进一帧。deltaTime = 本帧现实间隔（秒）——与 FlowSpawner 同一个时间轴。
        /// 倒序遍历：装置反应中可能 Kill 当前流源（移除 _active），倒序不影响未处理项的索引。
        /// </summary>
        public void Tick(double deltaTime)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                FlowEntity entity = _active[i];

                // 没有网格的流源无处可去（外部构造漏传），按越界处理直接回收
                IFlowGrid grid = entity.Grid;
                if (grid == null)
                {
                    FlowSpawner.Instance.Kill(entity);
                    continue;
                }

                // 走出网格的流源直接回收：否则它会一路飘向无穷远，_active 只增不减
                if (!grid.Contains((int)Math.Floor(entity.Position.X), (int)Math.Floor(entity.Position.Y)))
                {
                    FlowSpawner.Instance.Kill(entity);
                    continue;
                }

                // 位置直接写进 entity.Position，视觉层在 LateUpdate 里自己读
                MoveFlowEntity(entity, deltaTime);
            }
        }

        /// <summary>
        /// 一帧推进单个流源：算候选终点 → 逐格探测装置 → 撞上则截停并触发装置，无阻则直达。
        /// 被装置终结（Kill）时由 <see cref="FlowSpawner"/> 从 _active 里摘掉，这里不再关心。
        /// </summary>
        private void MoveFlowEntity(FlowEntity entity, double deltaTime)
        {
            (double dx, double dy) = entity.Motion.FrameOffset(deltaTime);
            FlowPos endPos = new FlowPos(entity.Position.X + dx, entity.Position.Y + dy);

            if (!TryFindBlocker(entity.Position, endPos, entity.Motion.Direction, entity))
            {
                entity.Position.Set(endPos);
            }
        }

        /// <summary>
        /// 逐格碰撞探测：从起点格沿方向走到终点格（含终点格，不查起点格），
        /// 返回是否撞上了「路由或反应装置」。
        /// </summary>
        private bool TryFindBlocker(FlowPos startPos, FlowPos endPos, int direction, FlowEntity entity)
        {
            int stepX = FlowMotionData.Dx[direction];
            int stepY = FlowMotionData.Dy[direction];

            // 探测的是流源自己的网格：同一帧里可能混着多个网格的流源
            IFlowGrid grid = entity.Grid;

            int probeX = (int)Math.Floor(startPos.X);
            int probeY = (int)Math.Floor(startPos.Y);
            int endCellX = (int)Math.Floor(endPos.X);
            int endCellY = (int)Math.Floor(endPos.Y);

            // 位移不足一格（起点格 == 终点格）时循环不执行，直接判无阻挡
            while (probeX != endCellX || probeY != endCellY)
            {
                probeX += stepX;
                probeY += stepY;

                IFlowRouter router = grid.GetRouterAt(probeX, probeY);
                IFlowDevice reactor = grid.GetReactorAt(probeX, probeY);

                // 先反应、后路由：同一格两者都有时，反应先改属性，路由再按新属性分流
                bool blocked = false;
                if (reactor != null && reactor.TryReact(entity.Properties))
                {
                    blocked = true;
                }
                if (router != null && router.Route(entity))
                {
                    blocked = true;
                }

                if (blocked)
                {
                    entity.Position.Set(
                        StopInsideCell(entity.Position.X, probeX, stepX),
                        StopInsideCell(entity.Position.Y, probeY, stepY));
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 撞停位置必须落在格子内部：停在格边界上会让下一帧的 Floor 判到隔壁格，
        /// 向左/向下撞停时甚至会算出负数坐标，被越界判定当场回收。
        ///
        /// 沿运动的那个轴停在入口侧内缘（从右往左撞就停在右边缘内 eps 处）；
        /// 不沿运动轴移动的那个轴原样保留，否则流源会被瞬间吸附到格中线，看起来像跳了一下。
        /// </summary>
        private static double StopInsideCell(double current, int cell, int step)
        {
            if (step > 0)
            {
                return cell + StopEpsilon;
            }

            if (step < 0)
            {
                return cell + 1d - StopEpsilon;
            }

            return current;
        }
    }
}
