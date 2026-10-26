using System;
using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 流源的运动：一帧推进多少、撞上谁、被谁拦住。
    ///
    /// 与生命周期是**并行的两件事**：名单和生死广播在 <see cref="FlowSpawner"/>，
    /// 本类负责运动和受阻采样，通过生死事件维护本机的采样记录。
    /// 不持有名单，也不依赖视觉层。
    ///
    /// 网格由构造注入：一台机器一块网格，所以不需要每个流源再自带一份。
    ///
    /// 不向上通报每帧位置——视觉层在 <c>LateUpdate</c> 里直接读
    /// <see cref="FlowEntity.Position"/>，因为逻辑帧结束时游戏 UI 还没排完版，
    /// 那一刻算出的像素坐标到 UI 真正落地前还会变。
    /// </summary>
    public sealed class FlowManager
    {
        /// <summary>连续静止两次有效采样才判定受阻。</summary>
        private const int BlockedStillFrames = 2;

        /// <summary>速度高于该值却连续不动，才算受阻。</summary>
        private const double BlockedMinSpeed = 0.01d;

        /// <summary>
        /// 撞上装置时停在装置格内的偏移量（沿运动方向前进一点点）。
        /// 让位置明确落在装置格内部、不压在格边界上：
        /// 下一帧起点格仍是装置格，探测从下一格开始，本装置只触发一次。
        /// 1e-3 格远小于一格，视觉上不可见。
        /// </summary>
        private const double StopEpsilon = 1e-3;

        private sealed class BlockedState
        {
            public double LastX;
            public double LastY;
            public int StillFrames;
            public bool IsBlocked;
        }

        private readonly IFlowGrid _grid;
        private readonly Dictionary<FlowEntity, BlockedState> _blockedStates
            = new Dictionary<FlowEntity, BlockedState>();
        private FlowSpawner _flows;

        public FlowManager(IFlowGrid grid, FlowSpawner flows = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (flows != null)
            {
                BindFlows(flows);
            }
        }

        private void BindFlows(FlowSpawner flows)
        {
            if (_flows == flows)
            {
                return;
            }

            if (_flows != null)
            {
                _flows.OnEntityAdded -= OnEntityAdded;
                _flows.OnEntityRemoved -= OnEntityRemoved;
            }

            _blockedStates.Clear();
            _flows = flows;
            _flows.OnEntityAdded += OnEntityAdded;
            _flows.OnEntityRemoved += OnEntityRemoved;

            foreach (FlowEntity entity in _flows.Active)
            {
                OnEntityAdded(entity);
            }
        }

        private void OnEntityAdded(FlowEntity entity)
        {
            _blockedStates[entity] = new BlockedState
            {
                LastX = entity.Position.X,
                LastY = entity.Position.Y
            };
        }

        private void OnEntityRemoved(FlowEntity entity)
        {
            _blockedStates.Remove(entity);
        }

        /// <summary>
        /// 推进一帧。deltaTime = 本帧现实间隔（秒）。
        ///
        /// 倒序遍历：装置反应中可能终结当前流源（从名单里移除），
        /// 倒序不受影响；分流装置在同一帧新增的流源追加在末尾，本轮也不会被遍历到。
        /// </summary>
        public void Tick(double deltaTime, FlowSpawner flows)
        {
            // 保留原 Tick 接口；旧调用方首次推进时绑定，换名单时退订旧事件。
            BindFlows(flows);
            IReadOnlyList<FlowEntity> active = flows.Active;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                FlowEntity entity = active[i];

                // 走出网格的流源直接回收：否则它会一路飘向无穷远，名单只增不减
                if (!_grid.Contains((int)Math.Floor(entity.Position.X), (int)Math.Floor(entity.Position.Y)))
                {
                    flows.Kill(entity);
                    continue;
                }

                // 位置直接写进 entity.Position，视觉层在 LateUpdate 里自己读
                MoveFlowEntity(entity, deltaTime, flows);
            }

            UpdateBlockedStates(deltaTime, flows);
        }

        private void UpdateBlockedStates(double deltaTime, FlowSpawner flows)
        {
            if (!(deltaTime > 0d))
            {
                return;
            }

            IReadOnlyList<FlowEntity> active = flows.Active;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                FlowEntity entity = active[i];
                BlockedState state = _blockedStates[entity];
                double positionX = entity.Position.X;
                double positionY = entity.Position.Y;
                double speed = entity.Motion == null ? 0d : entity.Motion.Speed;

                // 比较原始 double 坐标，不按格号或容差判断。
                if (positionX == state.LastX && positionY == state.LastY
                    && speed > BlockedMinSpeed)
                {
                    state.StillFrames++;
                }
                else
                {
                    state.StillFrames = 0;
                }

                state.LastX = positionX;
                state.LastY = positionY;

                // 先写历史再通知，回调终结当前源后不再写回。
                bool blocked = state.StillFrames >= BlockedStillFrames;
                if (state.IsBlocked != blocked)
                {
                    state.IsBlocked = blocked;
                    flows.ReportBlockedState(entity, blocked);
                }
            }
        }

        /// <summary>
        /// 一帧推进单个流源：算候选终点 → 逐格探测装置 → 撞上则截停并触发装置，无阻则直达。
        /// </summary>
        private void MoveFlowEntity(FlowEntity entity, double deltaTime, FlowSpawner flows)
        {
            (double dx, double dy) = entity.Motion.FrameOffset(deltaTime);
            FlowPos endPos = new FlowPos(entity.Position.X + dx, entity.Position.Y + dy);

            if (!TryFindBlocker(entity.Position, endPos, entity.Motion.Direction, entity, flows))
            {
                entity.Position.Set(endPos);
            }
        }

        /// <summary>
        /// 逐格碰撞探测：从起点格沿方向走到终点格（含终点格，不查起点格），
        /// 返回是否撞上了「路由或反应装置」。
        ///
        /// 装置需要造新流源或终结旧流源时，用的是传进来的 <paramref name="flows"/>——
        /// 运动层不持有它，只是这一步借用一下。
        /// </summary>
        private bool TryFindBlocker(
            FlowPos startPos, FlowPos endPos, int direction, FlowEntity entity, FlowSpawner flows)
        {
            int stepX = FlowMotionData.StepX(direction);
            int stepY = FlowMotionData.StepY(direction);

            int probeX = (int)Math.Floor(startPos.X);
            int probeY = (int)Math.Floor(startPos.Y);
            int endCellX = (int)Math.Floor(endPos.X);
            int endCellY = (int)Math.Floor(endPos.Y);

            // 位移不足一格（起点格 == 终点格）时循环不执行，直接判无阻挡
            while (probeX != endCellX || probeY != endCellY)
            {
                probeX += stepX;
                probeY += stepY;

                IFlowRouter router = _grid.GetRouterAt(probeX, probeY);
                IFlowDevice reactor = _grid.GetReactorAt(probeX, probeY);

                // 先反应、后路由：同一格两者都有时，反应先改属性，路由再按新属性分流
                bool blocked = false;
                if (reactor != null && reactor.TryReact(entity.Properties))
                {
                    blocked = true;
                }
                if (router != null && router.Route(entity, flows, probeX, probeY))
                {
                    blocked = true;
                }

                if (blocked)
                {
                    StopAt(entity, probeX, probeY, stepX, stepY);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 把流源落到装置格的内缘上。
        /// 单独抽出来是因为「落位」必须发生在把控制权交给装置**之前**，
        /// 见 <see cref="TryFindBlocker"/> 里的说明。
        /// </summary>
        private static void StopAt(FlowEntity entity, int cellX, int cellY, int stepX, int stepY)
        {
            entity.Position.Set(
                StopInsideCell(entity.Position.X, cellX, stepX),
                StopInsideCell(entity.Position.Y, cellY, stepY));
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
