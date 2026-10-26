using System;
using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品装配入口：先按 json 装配数据（<see cref="ItemFactory"/>），
    /// 再按 items.json 声明的 attachments 列表依次挂行为（<see cref="ItemAttachment"/>）。
    ///
    /// 放在物品目录工厂里而不是 ItemFactory 内部，是为了让 ItemFactory 保持
    /// 「纯数据装配」的职责——行为属于 mod 层的事。
    ///
    /// 分派不看 def.key：key 只负责存档标识与贴图约定。
    /// 新增装配件 = 新建一个 <see cref="ItemAttachment"/> 子类 + 在 <see cref="All"/> 里加一行。
    /// </summary>
    public static class ItemSetup
    {
        /// <summary>
        /// 我们的物品类型。在 <c>GameItem.SetGameItemType</c> 内部它就是 tag（EnableTag），
        /// 所以同一个标识既用于 tooltip 显示，也用于容器白名单。
        /// </summary>
        public const string FlowDeviceType = "FLOW_DEVICE";

        /// <summary>该类型的显示名。</summary>
        public const string FlowDeviceDisplayName = "流量装置";

        /// <summary>
        /// 全部装配件。这是唯一的注册点：新增装配件只加一行，不改任何逻辑。
        ///
        /// 用显式数组而不是反射扫描：装配件是我们自己写的类，类名写错、漏写公开无参构造
        /// 这类错误由编译器和评审兜住；把它们变成运行期问题、再写一套扫表与诊断去观测，
        /// 是净损失。运行期只防御真正的外部输入——items.json 里的字符串。
        /// </summary>
        private static readonly ItemAttachment[] All =
        {
            new MachineAttachment(),
            new EmitterAttachment(),
            new CollectorAttachment(),
            new IceBlockAttachment(),
        };

        /// <summary>名字 → 共享实例。名字比较不区分大小写；同名时后者覆盖前者（数组里一眼可见）。</summary>
        private static readonly Dictionary<string, ItemAttachment> _attachments = BuildTable();

        private static Dictionary<string, ItemAttachment> BuildTable()
        {
            Dictionary<string, ItemAttachment> table
                = new Dictionary<string, ItemAttachment>(All.Length, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < All.Length; i++)
            {
                table[All[i].Name] = All[i];
            }

            return table;
        }

        /// <summary>
        /// 物品目录工厂的入口：先按 json 装配，再按 attachments 挂装置。
        ///
        /// 挂装置单独包一层 try/catch：这段代码是**在游戏的物品创建回调内部**执行的，
        /// 异常会顺着 IL2CPP 的原生调用栈往上抛，轻则这个物品没行为、重则整次读档失败。
        /// 出问题时留下 Error 日志、物品照常返回，比让存档加载中断要好。
        /// </summary>
        public static Il2Cpp.GameItem Create(FlowItemDefinition def)
        {
            Il2Cpp.GameItem item;

            try
            {
                item = ItemFactory.Create(def);
            }
            catch (Exception e)
            {
                // 这一段是游戏物品创建链直接调用的，异常会顺着原生栈冒出去
                MelonLoader.MelonLogger.Error("[物品] 「" + def.key + "」装配失败：" + e);
                return null;
            }

            if (item == null)
            {
                return null;
            }

            try
            {
                ApplyAttachments(item, def);
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error(
                    "[物品] 「" + def.key + "」装置挂接失败，物品将以无行为状态存在：" + e);
            }

            return item;
        }

        /// <summary>
        /// 按声明顺序执行一件物品的全部装配件。
        ///
        /// 异常边界只在 <see cref="Create"/>（原生回调的唯一出口），这里不重复包一层。
        /// 逐装配件的 try 只负责隔离：一个装配件失败不该让其余装配件也被跳过。
        /// 未识别的名字用 Msg 级——配置笔误不该刷 Error，物品照常产出。
        /// </summary>
        private static void ApplyAttachments(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            string[] names = def.attachments;
            if (names == null || names.Length == 0)
            {
                return;
            }

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i]?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    // TryGetValue(null) 会抛；空名字也不该被当成一次「未识别」刷日志
                    continue;
                }

                ItemAttachment attachment = Get(name);
                if (attachment == null)
                {
                    MelonLoader.MelonLogger.Msg(
                        "[装配]「" + def.key + "」引用了未识别的装配件「" + name
                        + "」，已注册：" + KnownNames());
                    continue;
                }

                try
                {
                    attachment.Attach(item, def);
                }
                catch (Exception e)
                {
                    MelonLoader.MelonLogger.Error(
                        "[装配]「" + def.key + "」" + name + " 失败：" + e);
                }
            }
        }

        private static ItemAttachment Get(string name)
        {
            ItemAttachment attachment;
            return _attachments.TryGetValue(name, out attachment) ? attachment : null;
        }

        /// <summary>日志用：已注册的装配件名，逗号分隔（只在配置写错时调用）。</summary>
        private static string KnownNames()
        {
            List<string> names = new List<string>(_attachments.Keys);
            names.Sort(StringComparer.Ordinal);
            return string.Join(", ", names);
        }
    }
}
