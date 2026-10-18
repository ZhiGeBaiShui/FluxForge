namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// tooltip 状态行声明（对应 items.json 的一条 statusLines）。
    ///
    /// 设计目标：新增装置不写代码，只在配置里声明「读哪个 tag、怎么显示」。
    /// 游戏 tooltip 每帧全量重建（ItemTooltipHandler.OnEventUpdate → ShowTooltip），
    /// 所以这些值天然实时刷新，无需监听。
    /// </summary>
    public sealed class StatusLine
    {
        /// <summary>要读的 tag 名。</summary>
        public string tag { get; set; }

        /// <summary>显示标签（如「含冰量」）。</summary>
        public string label { get; set; }

        /// <summary>
        /// tag 值 → 显示值的乘数。例：tag 存 µL、显示 g 则填 0.001（1000 µL = 1 g）。
        /// 缺省或 ≤0 时按 1 处理。
        /// </summary>
        public double scale { get; set; }

        /// <summary>数字格式（.NET 格式串），缺省 "0.#"。</summary>
        public string format { get; set; }

        /// <summary>单位后缀（如 "g"、"℃"）。可空。</summary>
        public string unit { get; set; }

        /// <summary>颜色名（RenderHandler.ColorPalette 成员名），缺省 LightBlue。</summary>
        public string color { get; set; }

        /// <summary>读取哪个字段：int（默认）/ float / string。</summary>
        public string valueType { get; set; }
    }
}
