using System;

namespace ProbablyBagMod.Presentation.Flow.Visual
{
    /// <summary>
    /// 格内像素 → 局部坐标的换算与四边取整。**零 Unity 依赖**，可脱离游戏单测。
    ///
    /// UGUI 层拿到的是「格坐标 + 格内像素」，最终要落到与图集 tile 对齐的整数像素上：
    ///   · <see cref="CellToLocal"/>：拼出格内坐标（单位 = tile 像素，1 格 = 16）；
    ///   · <see cref="Snap"/>：把 float 位置吸到屏幕像素网格，避免 NEAREST 采样时
    ///     半像素偏移导致整张贴图糊掉或抖动（SPEC §0「全部轴对齐整数像素」）。
    /// </summary>
    public static class FlowPixelMath
    {
        /// <summary>一格 = 16 像素，与 SPEC §0 的 tile 尺寸一致。</summary>
        public const int CellPixels = 16;

        /// <summary>
        /// 格坐标 + 格内像素 → 局部像素坐标：<c>cell * 16 + px</c>。
        /// 不做边界钳制：负格坐标（面板外）是合法输入，由调用方裁剪。
        /// </summary>
        public static int CellToLocal(int cell, int px)
        {
            return cell * CellPixels + px;
        }

        /// <summary>
        /// 把 <paramref name="value"/> 吸到 <paramref name="scale"/> 的整数倍网格上：
        /// <c>round(value * scale) / scale</c>。例如 scale = 7.0625（= 113/16）时，
        /// 1/7.0625 会先乘回 1 个像素、取整、再除回去，位置精确落在像素中心线上。
        ///
        /// 用 <see cref="MathF.Round(float)"/>（银行家舍入）而不是 <c>floor(x + 0.5)</c>：
        /// 对 .5 的中立处理可以避免同一格在两帧之间来回跳 1px（抖动）。
        ///
        /// <paramref name="scale"/> ≤ 0 或为 NaN 时原样返回，调用方不必先判断屏幕缩放是否就绪。
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
