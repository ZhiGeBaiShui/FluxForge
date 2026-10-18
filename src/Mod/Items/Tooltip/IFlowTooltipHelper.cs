using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// tooltip 追加段（对应原版各 *Helper.CreateXxxTooltip 的角色）。
    ///
    /// 原版约定：helper 被无条件调用，**自己判断物品是否属于我这类**，不属于就立刻返回。
    /// 我们也照这个约定——新增装置只加一个 helper（或一条 json 声明），
    /// 不用再动 patch。
    /// </summary>
    public interface IFlowTooltipHelper
    {
        /// <summary>
        /// 尝试为物品追加 tooltip 段。不属于自己负责的范围就直接返回。
        /// </summary>
        void Append(Il2Cpp.RichTextBuilder builder, Il2Cpp.GameItemElement item);
    }

    /// <summary>
    /// helper 登记表：分派器按注册顺序逐个调用，各 helper 自判。
    /// 顺序有意义（先注册的先追加），所以用 List 而非 Dictionary。
    /// </summary>
    public static class FlowTooltipHelpers
    {
        private static readonly List<IFlowTooltipHelper> _helpers = new List<IFlowTooltipHelper>();
        private static bool _initialized;

        /// <summary>注册一个 helper（后注册的排在后面）。</summary>
        public static void Register(IFlowTooltipHelper helper)
        {
            if (helper != null)
            {
                _helpers.Add(helper);
            }
        }

        /// <summary>一次性注册内置 helper（重复调用安全）。</summary>
        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            Register(new StatusLineTooltipHelper());
        }

        /// <summary>分派：逐个 helper 追加（各自判断是否处理）。</summary>
        public static void AppendAll(Il2Cpp.RichTextBuilder builder, Il2Cpp.GameItemElement item)
        {
            EnsureInitialized();

            for (int i = 0; i < _helpers.Count; i++)
            {
                _helpers[i].Append(builder, item);
            }
        }

        public static int Count
        {
            get { return _helpers.Count; }
        }
    }
}
