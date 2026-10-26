using System;
using System.Collections.Generic;
using System.Reflection;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品装配入口：先按 json 装配数据（<see cref="ItemFactory"/>），
    /// 再按 items.json 声明的 attachments 列表依次挂行为（<see cref="ItemAttachment"/>）。
    ///
    /// 放在物品目录工厂里而不是 ItemFactory 内部，是为了让 ItemFactory 保持
    /// 「纯数据装配」的职责——行为属于 mod 层的事。
    ///
    /// 分派不看 def.key：key 只负责存档标识与贴图约定；新增装配件 = 新写一个
    /// <see cref="ItemAttachment"/> 子类，不改本文件。
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

        /// <summary>已发现的装配件：名字 → 全局共享实例。名字比较不区分大小写。</summary>
        private static readonly Dictionary<string, ItemAttachment> _attachments
            = new Dictionary<string, ItemAttachment>(StringComparer.OrdinalIgnoreCase);

        /// <summary>启动期是否尝试过扫描；为 true 后即使表为空也不再重试（避免每次创建物品都跑反射 + 刷日志）。</summary>
        private static bool _initAttempted;

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
        /// 扫描本程序集里的全部 <see cref="ItemAttachment"/> 实现，建成名字 → 实例的表。
        ///
        /// 幂等：重复调用只是重建同一张表。失败不重试——<see cref="_initAttempted"/> 在方法第一行置位。
        /// 整个方法体包在 try/catch 里：任何未预期异常只记 Error，表保持为空，不向外抛。
        /// </summary>
        public static void Initialize()
        {
            _initAttempted = true;

            try
            {
                _attachments.Clear();

                Type[] types;
                try
                {
                    types = typeof(ItemAttachment).Assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    // 类型加载失败会被误判成「json 拼错了」，这里把真实原因单独记下来
                    types = e.Types;
                    LogLoaderExceptions(e);
                }

                List<Type> candidates = new List<Type>();
                for (int i = 0; i < types.Length; i++)
                {
                    Type type = types[i];
                    if (type == null || !type.IsClass || type.IsAbstract)
                    {
                        continue;
                    }

                    if (typeof(ItemAttachment).IsAssignableFrom(type))
                    {
                        candidates.Add(type);
                    }
                }

                // 按 FullName 排序：出现重复 Name 时「保留第一个」必须是确定行为
                candidates.Sort(CompareByFullName);

                for (int i = 0; i < candidates.Count; i++)
                {
                    Type type = candidates[i];

                    ItemAttachment instance;
                    try
                    {
                        // 没有 public 无参构造时 MissingMethodException 也走这里
                        instance = (ItemAttachment)Activator.CreateInstance(type);
                    }
                    catch (Exception e)
                    {
                        MelonLoader.MelonLogger.Error("[装配] " + type.FullName + " 实例化失败：" + e);
                        continue;
                    }

                    string name;
                    try
                    {
                        name = instance.Name;
                    }
                    catch (Exception e)
                    {
                        MelonLoader.MelonLogger.Error(
                            "[装配] " + type.FullName + " 的 Name 读取失败：" + e);
                        continue;
                    }

                    name = name?.Trim();
                    if (string.IsNullOrEmpty(name))
                    {
                        MelonLoader.MelonLogger.Error("[装配] " + type.FullName + " 的 Name 为空，已跳过");
                        continue;
                    }

                    ItemAttachment existing;
                    if (_attachments.TryGetValue(name, out existing))
                    {
                        MelonLoader.MelonLogger.Error(
                            "[装配]「" + name + "」重复：" + existing.GetType().FullName + " / " + type.FullName);
                        continue;
                    }

                    _attachments[name] = instance;

                    if (HasInstanceFields(type))
                    {
                        MelonLoader.MelonLogger.Warning(
                            "[装配] " + type.FullName + " 声明了实例字段，违反「无实例字段」规约（实例全局共享）");
                    }
                }

                if (_attachments.Count == 0)
                {
                    MelonLoader.MelonLogger.Error("[装配] 发现 0 个装配件：过滤条件或程序集有误");
                    return;
                }

                MelonLoader.MelonLogger.Msg(
                    "[装配] 已发现 " + _attachments.Count + " 个：" + DescribeNames());
            }
            catch (Exception e)
            {
                // 半建成的表也不能拿来分派：清空后物品退化成纯数据
                _attachments.Clear();
                MelonLoader.MelonLogger.Error("[装配] 扫描装配件失败：" + e);
            }
        }

        /// <summary>
        /// 按声明顺序执行一件物品的全部装配件。
        ///
        /// 边界约定：外层 try 是最后的兜底（异常不得冒到原生栈）；逐装配件的 try 只隔离单个装配件，
        /// 不能替代外层。未识别的名字用 Msg 级——配置笔误不该刷 Error，物品照常产出。
        /// </summary>
        private static void ApplyAttachments(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            try
            {
                if (!_initAttempted)
                {
                    MelonLoader.MelonLogger.Warning(
                        "[装配] 装配件表未在启动期初始化，已在物品创建时补建");
                    Initialize();
                }

                string[] names = def.attachments;
                if (names == null || names.Length == 0)
                {
                    return;
                }

                HashSet<string> usedSlots = null;

                for (int i = 0; i < names.Length; i++)
                {
                    string name = names[i]?.Trim();
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    ItemAttachment attachment = Get(name);
                    if (attachment == null)
                    {
                        MelonLoader.MelonLogger.Msg(
                            "[装配]「" + def.key + "」引用了未识别的装配件「" + name + "」");
                        continue;
                    }

                    string slot = attachment.ExclusiveSlot;
                    if (slot != null)
                    {
                        usedSlots = usedSlots ?? new HashSet<string>(StringComparer.Ordinal);
                        if (!usedSlots.Add(slot))
                        {
                            MelonLoader.MelonLogger.Error(
                                "[装配]「" + def.key + "」独占槽「" + slot + "」已被占用，跳过「" + name + "」");
                            continue;
                        }
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
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("[装配]「" + def.key + "」装配流程异常：" + e);
            }
        }

        private static ItemAttachment Get(string name)
        {
            ItemAttachment attachment;
            return _attachments.TryGetValue(name, out attachment) ? attachment : null;
        }

        /// <summary>日志用的清单，例如 machine(MachineAttachment), emitter(EmitterAttachment)。</summary>
        private static string DescribeNames()
        {
            List<string> names = new List<string>(_attachments.Keys);
            names.Sort(StringComparer.Ordinal);

            string report = string.Empty;
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0)
                {
                    report += ", ";
                }

                report += names[i] + "(" + _attachments[names[i]].GetType().Name + ")";
            }

            return report;
        }

        /// <summary>逐个记录类型加载失败的原因（去重），否则只会看到「发现 0 个」这种误导性结论。</summary>
        private static void LogLoaderExceptions(ReflectionTypeLoadException e)
        {
            HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
            Exception[] loaderExceptions = e.LoaderExceptions;
            if (loaderExceptions == null)
            {
                return;
            }

            for (int i = 0; i < loaderExceptions.Length; i++)
            {
                Exception loaderException = loaderExceptions[i];
                if (loaderException == null)
                {
                    continue;
                }

                string message = loaderException.Message;
                if (reported.Add(message))
                {
                    MelonLoader.MelonLogger.Error("[装配] 类型加载失败：" + message);
                }
            }
        }

        /// <summary>规约 1：装配件不得有实例字段（实例全局共享，带状态会串物品）。</summary>
        private static bool HasInstanceFields(Type type)
        {
            return type.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly).Length > 0;
        }

        private static int CompareByFullName(Type left, Type right)
        {
            return string.CompareOrdinal(left.FullName, right.FullName);
        }
    }
}
