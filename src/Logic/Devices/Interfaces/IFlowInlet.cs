namespace FluxForge
{
    /// <summary>装置入口限制。direction 为流源的世界移动方向（0上1右2下3左），不是入口朝向。</summary>
    public interface IFlowInlet
    {
        bool CanEnter(int direction);
    }
}
