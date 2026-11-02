using System;
using System.Collections.Generic;

namespace FluxForge
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

            // 只遍历本帧真正经过的整数坐标，包含“起点所在格但还没经过的坐标”。
            // 向左/向上时起点格可能正是装置格：Floor(start) 会把该格排除掉，
            // 必须从 Ceiling(start)-1 开始，才能看到起点与终点之间的装置坐标。
            if (stepX != 0)
            {
                int baseY = (int)Math.Floor(startPos.Y);
                int cellX = stepX > 0
                    ? (int)Math.Floor(startPos.X) + 1
                    : (int)Math.Ceiling(startPos.X) - 1;

                while (stepX > 0 ? cellX <= endPos.X : cellX >= endPos.X)
                {
                    if (TryProbeCell(entity, flows, cellX, baseY))
                    {
                        return true;
                    }

                    cellX += stepX;
                }

                return false;
            }

            int baseX = (int)Math.Floor(startPos.X);
            int cellY = stepY > 0
                ? (int)Math.Floor(startPos.Y) + 1
                : (int)Math.Ceiling(startPos.Y) - 1;

            while (stepY > 0 ? cellY <= endPos.Y : cellY >= endPos.Y)
            {
                if (TryProbeCell(entity, flows, baseX, cellY))
                {
                    return true;
                }

                cellY += stepY;
            }

            return false;
        }

        private bool TryProbeCell(
            FlowEntity entity, FlowSpawner flows, int cellX, int cellY)
        {
            IFlowRouter router = _grid.GetRouterAt(cellX, cellY);
            IFlowDevice reactor = _grid.GetReactorAt(cellX, cellY);

            // 先反应、后路由：同一格两者都有时，反应先改属性，路由再按新属性分流
            bool blocked = false;
            if (reactor != null && reactor.TryReact(entity.Properties))
            {
                blocked = true;
            }
            if (router != null && router.Route(entity, flows, cellX, cellY))
            {
                blocked = true;
            }

            if (!blocked)
            {
                return false;
            }

            // 反应发生后，流源截停到装置所在的整数格。
            StopAt(entity, cellX, cellY);
            return true;
        }

        /// <summary>
        /// 把流源截停到装置所在的整数格中心。
        /// </summary>
        private static void StopAt(FlowEntity entity, int cellX, int cellY)
        {
            entity.Position.Set(cellX, cellY);
        }
    }
}
