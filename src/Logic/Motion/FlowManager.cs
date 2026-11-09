using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 流源的运动：一帧推进多少、撞上谁、被谁拦住。
    ///
    /// 与生命周期是**并行的两件事**：名单和生死广播在 <see cref="FlowSpawner"/>，
    /// 本类负责运动和受阻采样，通过生死事件维护本网格的采样记录。
    /// 不持有名单，也不依赖视觉层。
    ///
    /// 网格由构造注入：一块网格一份，所以不需要每个流源再自带一份。
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

        private sealed class BlockedState
        {
            public double LastX;
            public double LastY;
            public int StillFrames;
            public bool IsBlocked;
            public (int X, int Y)? DepartureCell;
        }

        private readonly IFlowGrid _grid;
        private readonly Dictionary<FlowEntity, BlockedState> _blockedStates
            = new Dictionary<FlowEntity, BlockedState>();
        private FlowSpawner _flows;

        public FlowManager(IFlowGrid grid, FlowSpawner flows = null)
        {
            _grid = grid;
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
                int currentX = CellAt(entity.Position.X, FlowMotionData.StepX(entity.Motion.Direction));
                int currentY = CellAt(entity.Position.Y, FlowMotionData.StepY(entity.Motion.Direction));
                if (!_grid.Contains(currentX, currentY)
                    || _grid.GetCellKind(currentX, currentY) == FlowCellKind.Occupied)
                {
                    flows.Kill(entity);
                    continue;
                }

                // 装置主动设置的停顿：到时间前完全不移动，也不推进反应计时。
                if (entity.HoldSeconds > 0d)
                {
                    entity.HoldSeconds = Math.Max(0d, entity.HoldSeconds - deltaTime);
                    if (entity.HoldSeconds > 1e-9d)
                    {
                        continue;
                    }

                    entity.HoldSeconds = 0d;
                }

                // 位置直接写进 entity.Position，视觉层在 LateUpdate 里自己读
                MoveFlowEntity(entity, deltaTime, flows);

                // 碰撞或路由已销毁的流源，不再执行本帧化学反应。
                if (!_blockedStates.ContainsKey(entity))
                    continue;

                // 反应等待时间按现实帧扣减；到期后执行一次，并保留越过 0 的小数残差。
                entity.NextReactionTime -= deltaTime;
                if (entity.NextReactionTime < 0d)
                {
                    FlowReactionSolver.React(entity.Properties);

                    // 保留负小数，再 +1 进入下一轮等待。
                    entity.NextReactionTime %= 1d;
                    entity.NextReactionTime += 1d;
                }

                // 反应或其它路径清空液体后，流源本身也要退出；FlowSpawner 的
                // OnEntityRemoved 会通知视觉层等既有订阅者，不需要 Mod 层额外代码。
                if (entity.Properties.SubstanceTotal <= 0d)
                {
                    flows.Kill(entity);
                }
            }

            UpdateBlockedStates(deltaTime, flows);
        }

        private void UpdateBlockedStates(double deltaTime, FlowSpawner flows)
        {
            if (deltaTime == 0d)
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
                double speed = entity.Motion.Speed;

                // 主动停顿不是“异常受阻”：期间不采样，也不产生 Blocked 事件。
                if (entity.HoldSeconds > 0d)
                {
                    state.StillFrames = 0;
                    state.LastX = positionX;
                    state.LastY = positionY;

                    if (state.IsBlocked)
                    {
                        state.IsBlocked = false;
                        flows.ReportBlockedState(entity, false);
                    }

                    continue;
                }

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
        /// 一帧推进：按路径顺序检查格边界入口与格内三分之一处交互，无截停则直达终点。
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

        /// <summary>整数是装置中心，半整数是格边界。按移动顺序检查入口与格内交互点，
        /// 即使一帧跨越多格，也不会跳过碰撞或提前触发后方装置。
        /// </summary>
        private bool TryFindBlocker(
            FlowPos startPos, FlowPos endPos, int direction, FlowEntity entity, FlowSpawner flows)
        {
            int stepX = FlowMotionData.StepX(direction);
            int stepY = FlowMotionData.StepY(direction);
            bool horizontal = stepX != 0;
            int step = horizontal ? stepX : stepY;
            double start = horizontal ? startPos.X : startPos.Y;
            double end = horizontal ? endPos.X : endPos.Y;
            int lane = CellAt(horizontal ? startPos.Y : startPos.X, 0);
            // 入口位于中心前半格，交互点位于入口后 1/3 格（即中心前 1/6 格）。
            // 分别遍历边界与交互点，再按运动方向合并，起点事件不重放。
            double boundary = step > 0 ? Math.Floor(start + .5) + .5
                : Math.Ceiling(start - .5) - .5;
            int interactionCell = step > 0 ? (int)Math.Floor(start + step / 6d) + 1
                : (int)Math.Ceiling(start + step / 6d) - 1;
            while (true)
            {
                double interaction = interactionCell - step / 6d;
                bool isBoundary = step > 0 ? boundary < interaction : boundary > interaction;
                double point = isBoundary ? boundary : interaction;
                if (step > 0 ? point > end : point < end) break;
                int cell = isBoundary ? (int)(boundary + step * .5) : interactionCell;
                int x = horizontal ? cell : lane;
                int y = horizontal ? lane : cell;
                if (isBoundary ? TryEnterCell(entity, flows, x, y)
                    : TryProbeCell(entity, flows, x, y))
                    return true;
                if (isBoundary) boundary += step;
                else interactionCell += step;
            }
            return false;
        }

        // 正好落在边界时，沿运动方向归入新格；横向车道边界归入较小格号。
        private static int CellAt(double position, int step)
        {
            return step <= 0 ? (int)Math.Ceiling(position - .5)
                : (int)Math.Floor(position + .5);
        }

        /// <summary>入口只检查占位和方向，不执行化学反应或转向。</summary>
        private bool TryEnterCell(FlowEntity entity, FlowSpawner flows, int x, int y)
        {
            if (!_grid.Contains(x, y) || _grid.GetCellKind(x, y) == FlowCellKind.Occupied)
            {
                flows.Kill(entity);
                return true;
            }
            var router = _grid.GetRouterAt(x, y);
            var reactor = _grid.GetReactorAt(x, y);
            int direction = entity.Motion.Direction;
            if ((router is IFlowInlet routerInlet && !routerInlet.CanEnter(direction))
                || (reactor is IFlowInlet reactorInlet && !reactorInlet.CanEnter(direction)))
            {
                flows.Kill(entity);
                return true;
            }
            return false;
        }

        private bool TryProbeCell(
            FlowEntity entity, FlowSpawner flows, int cellX, int cellY)
        {
            BlockedState state = _blockedStates[entity];
            var departure = state.DepartureCell;
            state.DepartureCell = null;
            // 转向后的微小偏移可能让下一帧再次探测当前装置格。
            // 这是沿出口离开，不能重复执行该装置的反应/路由。
            if (departure.HasValue && departure.Value.X == cellX && departure.Value.Y == cellY)
                return false;

            IFlowRouter router = _grid.GetRouterAt(cellX, cellY);
            IFlowDevice reactor = _grid.GetReactorAt(cellX, cellY);

            // 先反应、后路由：同一格两者都有时，反应先改属性，路由再按新属性分流
            bool blocked = false;
            if (reactor != null && reactor.TryReact(entity))
            {
                blocked = true;
            }
            if (router != null && router.Route(entity, flows, cellX, cellY))
            {
                blocked = true;
            }

            // 装置可以请求主动停顿；它和截停一样必须停到装置格中心。
            if (entity.HoldSeconds > 0d)
            {
                blocked = true;
            }

            if (!blocked)
            {
                return false;
            }

            // 反应发生后，流源截停到装置所在的整数格。
            StopAt(entity, cellX, cellY);
            if (_blockedStates.TryGetValue(entity, out state))
                state.DepartureCell = (cellX, cellY);
            return true;
        }

        /// <summary>
        /// 把流源截停到装置所在的整数格中心。
        /// </summary>
        private static void StopAt(FlowEntity entity, int cellX, int cellY)
        {
            double offsetX = entity.StopOffsetX;
            double offsetY = entity.StopOffsetY;
            entity.StopOffsetX = 0d;
            entity.StopOffsetY = 0d;

            entity.Position.Set(cellX + offsetX, cellY + offsetY);
        }
    }
}
