namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 流量机面板：仅提供装置网格与窗口，运行环境由装置激活时按需创建。
    ///
    /// ⚠ <see cref="PixelWindow"/> 只有**一个**子元素槽——`Attach` 会先 Detach 旧 child。
    ///   如果先 Attach 网格、再 Attach 按钮，网格就被顶掉了，表现为「窗口能开、内容全空」。
    ///   原版所有多元素窗口都是：建一个 <see cref="GridPixelElement"/> 当根，
    ///   网格/标签/按钮全部 Attach 进根里，最后只 window.Attach(根) 一次。
    ///
    /// 另外窗口尺寸不用自己算：<c>Attach</c> 会按根元素的实际尺寸重算并覆盖构造时传的值。
    /// </summary>
    public sealed class MachineAttachment : ItemAttachment
    {
        public override string Name => "machine";

        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            int columns = 4;
            int rows = 3;
            ParseGrid(def.grid, ref columns, ref rows);

            // 根只保留网格，不再放启动按钮或行间距。
            GridPixelElement root = new GridPixelElement(1, 1, true);

            GameGridInventory inventory = new GameGridInventory(columns, rows);

            // Mono 下 GameGridInventory 直接实现 PixelElement，不再需要 TryCast。
            // 第 4/5 个参数是**最小像素宽高**（不是行列数），给 0 表示用元素自身尺寸。
            if (!root.Attach(inventory, 0, 0, 0, 0))
            {
                PBModEntry.Log.Error(
                    "[流量机] 网格挂载失败（" + columns + "x" + rows + "），面板里不会有格子");
            }

            // 用游戏自己的容器白名单，按**物品类型**过滤——
            // 类型在 SetGameItemType 里就是 tag（EnableTag），所以这里传类型名即可。
            // 原版也是这样用的：nutrifruit_seed_packet 先 SetGameItemType("SEED")，
            // 再 AllowOnlyTaggedItem(inv, "SEED")。
            ContainerHelper.AllowOnlyTaggedItem(inventory, def.acceptsType);

            PixelWindow window = new PixelWindow(1, 1, true, 8, 8, def.name);
            window.Attach(root);
            item.SetContentWindow(window);
        }

        /// <summary>解析 "4x3" 这类尺寸串，格式不对就保留默认值。</summary>
        private static void ParseGrid(string text, ref int columns, ref int rows)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string[] parts = text.Split('x', 'X');
            int parsedColumns;
            int parsedRows;

            if (parts.Length == 2
                && int.TryParse(parts[0], out parsedColumns)
                && int.TryParse(parts[1], out parsedRows)
                && parsedColumns > 0
                && parsedRows > 0)
            {
                columns = parsedColumns;
                rows = parsedRows;
            }
            else
            {
                PBModEntry.Log.Warning("[流量机] grid 配置无效：" + text + "（用默认 4x3）");
            }
        }
    }
}
