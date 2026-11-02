using System;

namespace FluxForge.Presentation.Flow.Visual
{
    /// <summary>
    /// 流源网格与像素之间的纯换算。
    ///
    /// 这里不持有 Logic 坐标语义：调用方必须先把 Logic 中心换算成
    /// 游戏格中心，再交给本类换成图块左上角。
    /// </summary>
    public static class FlowPixelMath
    {
        /// <summary>一个格在流源图集里的像素尺寸。</summary>
        public const int CellPixels = 16;

        /// <summary>游戏格中心 → 16×16 图块左上角（中心回退半格）。</summary>
        public static double TileLeftFromGameCenter(double gameCenter)
        {
            return gameCenter - 0.5d;
        }

        /// <summary>
        /// 格坐标 + 格内像素 → 局部像素坐标：<c>cell * 16 + px</c>。
        /// 不做边界钳制：负格坐标（面板外）是合法输入，由调用方裁剪。
        /// </summary>
        public static int CellToLocal(int cell, int px)
        {
            return cell * CellPixels + px;
        }

        /// <summary>
        /// 把 value 吸到 scale 的整数倍网格上：<c>round(value * scale) / scale</c>。
        /// scale ≤ 0 或 NaN 时原样返回。
        /// </summary>
        public static float Snap(float value, float scale)
        {
            if (scale <= 0f || float.IsNaN(scale))
            {
                return value;
            }

            return MathF.Round(value * scale) / scale;
        }
    }
}
