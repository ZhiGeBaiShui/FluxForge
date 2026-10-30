namespace ProbablyBagMod.Presentation.Flow.Visual
{
    /// <summary>
    /// 数据 → 图集枚举的**唯一换算入口**（IMPL-PLAN-A §4「Tier-0」）。
    ///
    /// 刻意保持纯函数、零状态、零 Unity：
    ///   · 温度只影响「冷/常温/热」三档，不改尺寸（SPEC §2 的 bbox 不变约束）；
    ///   · 含量四档是**唯一**控制尺寸的通道（SPEC §1）；
    ///   · 状态目前来自「两帧未动」一个布尔，不读 Composition；
    ///   · 方向来自 <c>Motion.Direction</c> 的 0..3。
    /// 值域以外的输入都有明确的兜底，保证渲染层永远拿到一个合法枚举。
    /// </summary>
    public static class FlowTierMap
    {
        /// <summary>冷档上界（℃）：≤ 该值为 cold，与 SPEC §2 一致。</summary>
        public const float ColdMaxCelsius = 5f;

        /// <summary>热档下界（℃）：≥ 该值为 hot，与 SPEC §2 一致。</summary>
        public const float HotMinCelsius = 60f;

        /// <summary>
        /// 空档上界（µL）：只把真正没有液体的流源判成空。
        /// 低含量在 16px 下画成 6×1 干痕会小到看不见，所以 1 µL 以上一律至少走「少」档。
        /// </summary>
        public const double EmptyMaxMicroliters = 1d;

        /// <summary>少档上界（µL）：低于 5 000 000 µL（5 L）为 small。</summary>
        public const double SmallMaxMicroliters = 5000000d;

        /// <summary>中档上界（µL）：低于 20 000 000 µL（20 L）为 mid，否则 large。</summary>
        public const double MidMaxMicroliters = 20000000d;

        /// <summary>
        /// 温度（℃）→ 温度档。NaN 视为未知，按 normal 处理。
        /// 边界：≤5 cold、≥60 hot、其余 normal（5.1 与 59.9 都是 normal）。
        /// </summary>
        public static FlowTemp TempFrom(float celsius)
        {
            if (float.IsNaN(celsius))
            {
                return FlowTemp.Normal;
            }

            if (celsius <= ColdMaxCelsius)
            {
                return FlowTemp.Cold;
            }

            if (celsius >= HotMinCelsius)
            {
                return FlowTemp.Hot;
            }

            return FlowTemp.Normal;
        }

        /// <summary>
        /// 含量（µL）→ 含量档。
        /// 边界：&lt; 1 µL empty（含 0 与负数）、&lt; 5e6 small、&lt; 2e7 mid、否则 large。
        ///
        /// NaN 是脏数据，按 empty 处理：把它画成 large 会凭空出现一大滴液体，
        /// 比「看不见」更容易误导玩家。
        /// </summary>
        public static FlowContent ContentFromWater(double waterAmount)
        {
            if (double.IsNaN(waterAmount) || waterAmount < EmptyMaxMicroliters)
            {
                return FlowContent.Empty;
            }

            if (waterAmount < SmallMaxMicroliters)
            {
                return FlowContent.Small;
            }

            if (waterAmount < MidMaxMicroliters)
            {
                return FlowContent.Mid;
            }

            return FlowContent.Large;
        }

        /// <summary>
        /// 状态判定：被阻挡（blocked）优先，否则流动（flow）。
        ///
        /// collect 是死亡当帧的一次性贴图，已从生产路径移除（死亡改由粒子淡出表达），
        /// 所以这里不再接收 removed 参数，也不产出 <see cref="FlowState.Collect"/>。
        /// </summary>
        public static FlowState StateFrom(bool blocked)
        {
            if (blocked)
            {
                return FlowState.Blocked;
            }

            return FlowState.Flow;
        }

        /// <summary>
        /// 拖尾层数（1–6）：尾巴长度要跟体量走，否则"重"和"轻"的流源拖着一样长的尾巴，
        /// 会把含量通道读乱。按面积档给：空 1、少 2、中 4、多 6（每层 3px）。
        /// </summary>
        public static int TrailLayersFromContent(FlowContent content)
        {
            switch (content)
            {
                case FlowContent.Empty:
                    return 1;
                case FlowContent.Small:
                    return 2;
                case FlowContent.Mid:
                    return 4;
                case FlowContent.Large:
                    return 6;
                default:
                    return 6;
            }
        }

        /// <summary>
        /// 方向整数 → 方向档。0/1/2/3 → 上/右/下/左；其它值回退为 right。
        /// 回退选 right 而不是 up，是为了与 <c>Motion.Direction</c> 缺省值 1 一致。
        /// </summary>
        public static FlowDir DirFrom(int dir)
        {
            switch (dir)
            {
                case 0:
                    return FlowDir.Up;
                case 1:
                    return FlowDir.Right;
                case 2:
                    return FlowDir.Down;
                case 3:
                    return FlowDir.Left;
                default:
                    return FlowDir.Right;
            }
        }
    }
}
