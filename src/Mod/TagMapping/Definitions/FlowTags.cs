namespace FluxForge.Mod
{
    /// <summary>
    /// 物品身份 / 锚点等非流源属性的 tag 名；常量名与 tag 英文名一一对应，方便按名字搜到落点。
    ///
    /// 流源属性 tag 不在这里列：它们由 <see cref="FlowTagNaming.ForProperty"/>
    /// 按注册表 id + 种类生成。液体 tag 由游戏 <c>InitLiquidContainerItem</c>
    /// 按 <c>CURRENT_PART_&lt;ID&gt;</c> 创建，标量按 <c>&lt;ID&gt;_TAG</c>。
    /// </summary>
    public static class FlowTags
    {
        /// <summary>
        /// 原版液体容器的身份 tag
        /// （flow_container / water_bottle 装配件经 <c>WaterHelper.InitLiquidContainerItem</c> 建立）。
        /// </summary>
        public const string LiquidContainerTag = "LIQUID_CONTAINER_TAG";

        /// <summary>
        /// 中心格的局部 X 坐标。items.json 的 <c>anchorX</c> 写入这里。
        ///
        /// 名字带 DEVICE 是历史原因；它描述的是「物品的中心格」，
        /// 装置和发射器共用同一套 anchor，不区分角色。
        /// </summary>
        public const string AnchorX = "FLUXFORGE_DEVICE_ANCHOR_X";

        /// <summary>中心格的局部 Y 坐标，与 <see cref="AnchorX"/> 成对使用。</summary>
        public const string AnchorY = "FLUXFORGE_DEVICE_ANCHOR_Y";

        /// <summary>原版液体容器的容量上限，单位 µL。</summary>
        public const string LiquidContainerCapacity = "LIQUID_CONTAINER_CAPACITY";

        /// <summary>Mod 装置容器身份；不启用原版 LIQUID_CONTAINER_TAG。</summary>
        public const string DeviceContainerTag = "FLUXFORGE_DEVICE_CONTAINER_TAG";

        /// <summary>
        /// 装置配置：每次经过时对流源施加的温度变化（°C）。
        /// 正值 = 升温，负值 = 降温；冷阱与温度装置共用。
        /// </summary>
        public const string TemperatureDelta = "FLUXFORGE_TEMPERATURE_DELTA";

        /// <summary>温度装置：流源经过后允许达到的最高温度（°C）。</summary>
        public const string TemperatureMax = "FLUXFORGE_TEMPERATURE_MAX";

        /// <summary>温度装置：流源经过后允许达到的最低温度（°C）。</summary>
        public const string TemperatureMin = "FLUXFORGE_TEMPERATURE_MIN";

        /// <summary>冷阱：相变温度阈值（°C）。</summary>
        public const string ColdTrapPhaseThreshold = "COLD_TRAP_PHASE_THRESHOLD";

        /// <summary>冷阱：未跨阈值时的基础抽提量（µL）。</summary>
        public const string ColdTrapBaseExtraction = "COLD_TRAP_BASE_EXTRACTION";

        /// <summary>冷阱：跨阈值时的效率倍率。</summary>
        public const string ColdTrapEfficiency = "COLD_TRAP_EFFICIENCY";

        /// <summary>直通变速装置：速度改变基础值。</summary>
        public const string StraightSpeedChange = "STRAIGHT_SPEED_CHANGE";

        /// <summary>直通变速装置：速度上限。</summary>
        public const string StraightSpeedMax = "STRAIGHT_SPEED_MAX";

        /// <summary>直通变速装置：速度下限。</summary>
        public const string StraightSpeedMin = "STRAIGHT_SPEED_MIN";

        /// <summary>发射装置：流源出射速度（格/秒）。</summary>
        public const string EmitterSpeed = "FLOW_EMITTER_SPEED";
    }
}
