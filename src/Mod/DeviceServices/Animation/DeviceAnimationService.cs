namespace FluxForge.Mod
{
    /// <summary>
    /// 动画服务：订阅逻辑层 FlowAnimation.Played。
    ///
    /// 当前是空壳，后续按 AnimationId / item 实现贴图切换、缩放与销毁动画。
    /// </summary>
    public static class DeviceAnimationService
    {
        public static void Initialize()
        {
            FlowAnimation.Played += OnPlayed;
        }

        public static void Shutdown()
        {
            FlowAnimation.Played -= OnPlayed;
        }

        private static void OnPlayed(FlowAnimationEvent evt)
        {
            GameItem item = DeviceRegistry.GetItemForDevice(evt.Source);

            // 空壳：预留贴图切换 / 缩放 / 销毁动画。
        }
    }
}
