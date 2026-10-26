namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 流量机面板：仅提供装置网格与窗口，运行环境由装置激活时按需创建。
    ///
    /// ⚠ <see cref="Il2Cpp.PixelWindow"/> 只有**一个**子元素槽——`Attach` 会先 Detach 旧 child。
    ///   如果先 Attach 网格、再 Attach 按钮，网格就被顶掉了，表现为「窗口能开、内容全空」。
    ///   原版所有多元素窗口都是：建一个 <see cref="Il2Cpp.GridPixelElement"/> 当根，
    ///   网格/标签/按钮全部 Attach 进根里，最后只 window.Attach(根) 一次。
    ///
    /// 另外窗口尺寸不用自己算：<c>Attach</c> 会按根元素的实际尺寸重算并覆盖构造时传的值。
    /// </summary>
    public sealed class MachineAttachment : ItemAttachment
    {
        public override string Name => "machine";

        public override void Attach(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            int columns = 4;
            int rows = 3;
            ParseGrid(def.grid, ref columns, ref rows);

            // 根只保留网格，不再放启动按钮或行间距。
            Il2Cpp.GridPixelElement root = new Il2Cpp.GridPixelElement(1, 1, true);

            Il2Cpp.GameGridInventory inventory = new Il2Cpp.GameGridInventory(columns, rows);

            // Il2CppInterop 没生成 GameGridInventory → PixelElement 的继承关系，
            // 运行时确实是子类，所以用 TryCast 走原生类型转换。
            // 第 4/5 个参数是**最小像素宽高**（不是行列数），给 0 表示用元素自身尺寸。
            if (!root.Attach(inventory.TryCast<Il2Cpp.PixelElement>(), 0, 0, 0, 0))
            {
                MelonLoader.MelonLogger.Error(
                    "[流量机] 网格挂载失败（" + columns + "x" + rows + "），面板里不会有格子");
            }

            // 用游戏自己的容器白名单，按**物品类型**过滤——
            // 类型在 SetGameItemType 里就是 tag（EnableTag），所以这里传类型名即可。
            // 原版也是这样用的：nutrifruit_seed_packet 先 SetGameItemType("SEED")，
            // 再 AllowOnlyTaggedItem(inv, "SEED")。
            Il2Cpp.ContainerHelper.AllowOnlyTaggedItem(inventory, ItemSetup.FlowDeviceType);

            Il2Cpp.PixelWindow window = new Il2Cpp.PixelWindow(1, 1, true, 8, 8, "流量机");
            window.Attach(root.TryCast<Il2Cpp.PixelElement>());
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
                MelonLoader.MelonLogger.Warning("[流量机] grid 配置无效：" + text + "（用默认 4x3）");
            }
        }
    }
}
