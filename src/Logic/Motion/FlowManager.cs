using System;
using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 运动层（单例，全局唯一）：管「活着的流源」的推进，并向上通报位置变化。
    ///
    /// 三时机之一「动」的广播者：每帧推进后对流源逐个发 <see cref="OnEntityMoved"/>，
    /// mod 层订阅它同步壳位置（DOTween 补间）。
    ///
    /// 收人 / 送人（<see cref="Accept"/> / <see cref="Remove"/>）由
    /// <see cref="FlowSpawner"/> 驱动——本类不持有生命周期事件，不认识 mod 层。
    /// </summary>
    public sealed class FlowManager
    {
        public static FlowManager Instance { get; } = new FlowManager();

        private IFlowGrid _grid;
        private readonly List<FlowEntity> _active = new List<FlowEntity>();

        /// <summary>
        /// 流源位置更新时触发（每帧每流源一次）。
        /// mod 层订阅 → 查壳字典 → DOTween 把壳滑向新坐标。
        /// </summary>
        public event Action<FlowEntity> OnEntityMoved;

        private FlowManager()
        {
        }

        /// <summary>注入网格查询（外部实现）。启动时调一次。</summary>
        public void Bind(IFlowGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
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
        /// 推进一帧。deltaTime = 本帧现实间隔（秒）——与 FlowSpawner 同一个时间轴。
        /// 倒序遍历：装置反应中可能 Kill 当前流源（移除 _active），倒序不影响未处理项的索引。
        /// </summary>
        public void Tick(double deltaTime)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                FlowEntity entity = _active[i];
                if (MoveFlowEntity(entity, deltaTime))
                {
                    OnEntityMoved?.Invoke(entity);
                }
            }
        }

        /// <summary>
        /// 一帧推进单个流源：算候选终点 → 逐格探测装置 → 撞上则截停并触发装置，无阻则直达。
        /// 返回 false = 流源已被装置终结（Kill），无需广播。
        /// </summary>
        private bool MoveFlowEntity(FlowEntity entity, double deltaTime)
        {
            (double dx, double dy) = entity.Motion.FrameOffset(deltaTime);
            FlowPos endPos = new FlowPos(entity.Position.X + dx, entity.Position.Y + dy);

            if(!TryFindBlocker(entity.Position, endPos, entity.Motion.Direction, entity))
            {
                entity.Position.Set(endPos);
                return true;
            }
            return _active.Contains(entity);           // 装置可能已 Kill 它
        }

        /// <summary>
        /// 逐格碰撞探测：从起点格沿方向走到终点格（含终点格，不查起点格），
        /// 返回第一个「有路由或反应装置」的格子。
        /// </summary>
        private bool TryFindBlocker(FlowPos startPos, FlowPos endPos, int direction, FlowEntity entity)
        {
            int stepX = FlowMotionData.Dx[direction];
            int stepY = FlowMotionData.Dy[direction];

            int probeX = (int)Math.Floor(startPos.X);
            int probeY = (int)Math.Floor(startPos.Y);
            int endCellX = (int)Math.Floor(endPos.X);
            int endCellY = (int)Math.Floor(endPos.Y);

            // 位移不足一格（起点格 == 终点格）时循环不执行，直接判无阻挡
            while (probeX != endCellX || probeY != endCellY)
            {
                probeX += stepX;
                probeY += stepY;
                IFlowRouter deviceFlowRouter = _grid.GetRouterAt(probeX, probeY);
                IFlowDevice deviceFlowDevice = _grid.GetReactorAt(probeX, probeY);
                entity.Position.Set((double)probeX, (double)probeY); // 可以忽略double，这里是提醒开发者注意
                // 先检查反应装置，再检查路由装置
                bool flag = false;
                if(deviceFlowDevice != null && deviceFlowDevice.TryReact(entity.Properties))
                {
                    flag = true;
                }
                if(deviceFlowRouter != null && deviceFlowRouter.Route(entity))
                {
                    flag = true;
                }
                if(flag)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
