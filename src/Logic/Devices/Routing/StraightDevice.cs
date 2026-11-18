namespace FluxForge
{
    /// <summary>显式直通装置：保留通道身份，不改变流源、不截停。</summary>
    public sealed class StraightDevice : IFlowRouter, IFacing, IFlowInlet
    {
#if FLOW_DIAGNOSTICS
        private int _facing;
        internal System.Diagnostics.StackTrace LastAxisChange { get; private set; }

        public int Facing
        {
            get { return _facing; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            set
            {
                if ((_facing & 1) != (value & 1))
                    LastAxisChange = new System.Diagnostics.StackTrace(1, false);
                _facing = value;
            }
        }
#else
        public int Facing { get; set; }
#endif
        public bool Flipped { get; set; }

        public bool CanEnter(int direction) => direction % 2 == Facing % 2;

        public bool Route(FlowEntity incoming, FlowSpawner flows, int cellX, int cellY)
        {
            return false;
        }
    }
}
