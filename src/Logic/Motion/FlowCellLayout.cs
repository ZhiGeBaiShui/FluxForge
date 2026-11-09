namespace FluxForge
{
    /// <summary>一个网格格子上的通道轴线。</summary>
    public enum FlowAxis
    {
        Vertical = 0,
        Horizontal = 1
    }

    /// <summary>物品形状变换后的单格坐标，以及该物品的反应 anchor 坐标。</summary>
    public readonly struct FlowCellContext
    {
        public FlowCellContext(
            int localX,
            int localY,
            int worldX,
            int worldY,
            int anchorX,
            int anchorY)
        {
            LocalX = localX;
            LocalY = localY;
            WorldX = worldX;
            WorldY = worldY;
            AnchorX = anchorX;
            AnchorY = anchorY;
        }

        public int LocalX { get; }
        public int LocalY { get; }
        public int WorldX { get; }
        public int WorldY { get; }
        public int AnchorX { get; }
        public int AnchorY { get; }
    }

    /// <summary>单个实体格提供给运动层的路由与反应行为。</summary>
    public readonly struct FlowCellBehavior
    {
        public FlowCellBehavior(IFlowRouter router, IFlowDevice reactor)
        {
            Router = router;
            Reactor = reactor;
        }

        public IFlowRouter Router { get; }
        public IFlowDevice Reactor { get; }
    }

    /// <summary>由组合装置按实际网格格子声明流动行为。</summary>
    public interface IFlowCellLayout
    {
        FlowCellBehavior GetCellBehavior(FlowCellContext cell);
    }
}
