using System;

namespace FluxForge
{
    /// <summary>
    /// 连续位置（浮点）。
    ///
    /// 坐标轴与游戏网格一致：X 向右递增，<b>Y 向下递增</b>
    /// （游戏里格子的 <c>anchoredPosition = (x, -y)</c>，minY 越大越靠屏幕下方）。
    /// 所以「向上走」是 Y 减小，别按数学惯例理解。
    ///
    /// 位置表示流源中心，整数处是格边界；格子 (1,1) 的中心是 (1.5,1.5)。
    /// 出生时位于格中心，撞停时位于装置格内缘，行进途中保留连续坐标。
    /// <see cref="IsSameCell"/> 用 Math.Floor 判定中心当前所在格。
    ///
    /// 声明为 class：被流源（<see cref="FlowEntity"/>）按引用持有，
    /// 各层改的是同一份位置数据。
    /// </summary>
    public class FlowPos
    {
        public double X;
        public double Y;

        public FlowPos(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>把位置设为给定坐标（格子坐标截停等场景用）。</summary>
        public void Set(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>把位置设为另一位置的值（直达终点等场景用）。</summary>
        public void Set(FlowPos other)
        {
            X = other.X;
            Y = other.Y;
        }

        /// <summary>
        /// 判断给定坐标是否与本位置处于同一格：各自向下取整、舍弃小数位后比较。
        /// </summary>
        public bool IsSameCell(double x, double y)
        {
            return Math.Floor(x) == Math.Floor(X)
                && Math.Floor(y) == Math.Floor(Y);
        }

        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }
    }
}
