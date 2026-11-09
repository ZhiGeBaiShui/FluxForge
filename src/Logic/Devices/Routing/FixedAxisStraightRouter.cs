namespace FluxForge
{
    /// <summary>具有固定世界轴线的直通通道，不随物品朝向变化。</summary>
    public sealed class FixedAxisStraightRouter : IFlowRouter, IFlowInlet
    {
        public FixedAxisStraightRouter(FlowAxis axis)
        {
            Axis = axis;
        }

        public FlowAxis Axis { get; }

        public bool CanEnter(int direction) => direction % 2 == (int)Axis;

        public bool Route(FlowEntity incoming, FlowSpawner flows, int cellX, int cellY)
        {
            return false;
        }
    }
}
