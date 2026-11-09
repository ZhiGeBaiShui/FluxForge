namespace FluxForge
{
    /// <summary>显式直通装置：保留通道身份，不改变流源、不截停。</summary>
    public sealed class StraightDevice : IFlowRouter, IFacing, IFlowInlet
    {
        public int Facing { get; set; }
        public bool Flipped { get; set; }

        public bool CanEnter(int direction) => direction % 2 == Facing % 2;

        public bool Route(FlowEntity incoming, FlowSpawner flows, int cellX, int cellY)
        {
            return false;
        }
    }
}
