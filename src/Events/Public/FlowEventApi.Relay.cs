namespace FluxForge.Events
{
    public static partial class FlowEventApi
    {
        /// <summary>
        /// 演示二：左右各放一个相对朝向的发射器；左侧先发射，右侧接收后等待 2 秒再发射。
        /// 返回 true 只表示事件已启动；完成由 FlowSpawner 事件驱动。
        /// </summary>
        public static bool RunTwoEmitterRelay(GameGridInventory grid)
        {
            return TwoEmitterRelayEvent.Start(grid);
        }
    }
}
