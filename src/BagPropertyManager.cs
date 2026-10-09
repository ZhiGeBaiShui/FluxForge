using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes;

namespace ProbablyBagMod
{
    /// <summary>
    /// 物品背包属性的统一管理类。
    ///
    /// 设计要点（已定案）：
    /// 1. 存储：静态字典，**原生指针 <c>IntPtr</c> 作键**。
    ///    依据：Il2CppInterop 自身即用 <c>ConcurrentDictionary&lt;IntPtr, ...&gt;</c> 缓存包装对象
    ///    （Il2CppObjectPool），且 <c>Il2CppObjectBase.Pointer</c> 在同一原生对象生命期内恒定。
    /// 2. 取值：惰性构建。先查字典；未命中则按「物品映射表 → 默认值公式」构建并写回。
    /// 3. 回收：由 <see cref="Remove"/> 提供入口，**待后续用 Harmony Prefix 挂到游戏物品的销毁函数**。
    ///
    /// 注意：<c>GameItem.Pointer</c> 在原生物体被回收后会**抛 ObjectCollectedException**，
    /// 因此取值前必须先用 <see cref="Il2CppObjectBase.WasCollected"/> 探测。
    /// </summary>
    public static class BagPropertyManager
    {
        /// <summary>已构建的物品属性缓存。键为原生物体指针。</summary>
        private static readonly Dictionary<IntPtr, ItemBagProperty> _cache
            = new Dictionary<IntPtr, ItemBagProperty>();

        /// <summary>
        /// 取出物品的背包属性。返回 null 表示该物品没有背包属性，
        /// 调用方应「静默处理，当作空格子」。
        /// </summary>
        /// <param name="item">游戏物品。允许为 null。</param>
        public static ItemBagProperty Get(GameItem item)
        {
            if (item == null || item.WasCollected)
            {
                return null;
            }

            IntPtr ptr = item.Pointer;
            if (_cache.TryGetValue(ptr, out ItemBagProperty cached))
            {
                return cached;
            }

            ItemBagProperty property = BuildProperty(item);
            if (property == null)
            {
                return null;
            }

            _cache[ptr] = property;
            return property;
        }

        /// <summary>
        /// 回收物品的背包属性。
        ///
        /// **入口已就绪，Pending：由 Harmony Prefix 挂到游戏的 GameItem 销毁函数上调用。**
        /// Hook 点（反编译待定，候选：<c>GameItem.OnDestroy</c> 或游戏自定的移除方法）。
        /// </summary>
        /// <param name="item">即将被销毁的游戏物品。允许为 null。</param>
        public static void Remove(GameItem item)
        {
            if (item == null || item.WasCollected)
            {
                return;
            }

            _cache.Remove(item.Pointer);
        }

        /// <summary>
        /// 构建物品的背包属性。**待实现**。
        ///
        /// 预期流程：先查「物品映射表」（按 <c>item.identifier</c>）；
        /// 查不到则走「默认值计算公式」。
        /// </summary>
        /// <param name="item">游戏物品。</param>
        /// <returns>构建出的属性；若无则为 null。</returns>
        private static ItemBagProperty BuildProperty(GameItem item)
        {
            // TODO: ① 查物品映射表（identifier → ItemBagProperty）
            // TODO: ② 映射表未命中时，走默认值计算公式
            return null;
        }
    }
}
