namespace FluxForge.Mod.Rendering.ItemSprites
{
    /// <summary>
    /// 单张贴图的配置项（sprites.json 一条）。
    /// 形状表示法依照游戏原版（GridShapeBuilder.SetData）：0/1 字符串按行拼接 + 每行宽度。
    /// </summary>
    public sealed class SpriteEntry
    {
        /// <summary>逻辑名（物品按它取图）。</summary>
        public string key { get; set; }

        /// <summary>自定义 atlas 名（与游戏原生 atlas 区分）。</summary>
        public string atlas { get; set; }

        /// <summary>嵌入资源文件名（assets/ 下）。</summary>
        public string file { get; set; }

        /// <summary>
        /// 可选的游戏 spritePath 覆盖。留空时用 key。
        /// 原版液体容器需要 spritePath="small" 才能加载 small_*_100 / small_hl_100 修饰层。
        /// </summary>
        public string spritePath { get; set; }

        /// <summary>形状数据：0/1 按行拼接（1=占格）。例：L 型（2×2 缺右下）="1110"，shapeWidth=2。</summary>
        public string shape { get; set; }

        /// <summary>每行宽度（列数）。</summary>
        public int shapeWidth { get; set; }
    }
}
