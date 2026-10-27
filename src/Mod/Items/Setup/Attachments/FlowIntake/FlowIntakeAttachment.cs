using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// "能接液体"这一项能力：让物品接收玩家拖上来的液体容器。
    ///
    /// 与"能发射"（<see cref="EmitterAttachment"/>）完全独立——一件物品可以只接不发、
    /// 只发不接，或两样都要。组合方式就是在 items.json 里写两个名字：
    ///
    ///     "attachments": ["flow_intake", "emitter"]
    ///
    /// 两种接水方式，由 <see cref="UseOwnTarget"/> 选择：
    ///   · false（当前）：<c>InitLiquidContainerItem</c>，物品就是原版液体容器，
    ///     倒水与存储都交给游戏（水在 <c>CURRENT_PART_*</c> 里）。
    ///   · true：自己接管 <c>may/can/onThisTargetItemFunc</c>，把来源抽干并入
    ///     <c>FLOW_STOCK</c> / <c>FLOW_TEMPERATURE</c>（见 <see cref="FlowTransfer"/>）。
    ///
    /// ⚠ 翻成 true 之前必须验证：拖拽水瓶到本物品时 <c>onThisTargetItemFunc</c> 真的被调用。
    ///   未验证就翻开，表现是"水倒不进去"，而且没有任何报错。
    /// </summary>
    public sealed class FlowIntakeAttachment : ItemAttachment
    {
        /// <summary>是否为液体容器（原版用它判定"谁能接受倒液"）。</summary>
        public const string ContainerTag = "LIQUID_CONTAINER_TAG";

        /// <summary>是否用自己的落点逻辑接管接水（见类注释的验证前提）。</summary>
        private static readonly bool UseOwnTarget = false;

        /// <summary>容量（毫升）。原版硬上限 2,147,483，超了会 LogError 并钳制。</summary>
        private const int CapacityMl = 1000000;

        public override string Name => "flow_intake";

        public override void Attach(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            if (UseOwnTarget)
            {
                WireTarget(item);
            }
            else
            {
                Il2Cpp.WaterHelper.InitLiquidContainerItem(item, CapacityMl, 0);
            }
        }

        /// <summary>把"别人拖到我身上"接到三个落点委托上。谓词只读，执行时才动状态。</summary>
        private static void WireTarget(Il2Cpp.GameItem item)
        {
            CallbackWiring.EnsureRegistered<FlowTargetCallbacks>();
            FlowTargetCallbacks callbacks = new FlowTargetCallbacks();

            item.mayThisTargetItemFunc =
                new Il2CppSystem.Func<Il2Cpp.GameItem, Il2Cpp.GameItem, bool>(
                    callbacks, CallbackWiring.Method<FlowTargetCallbacks>(
                        nameof(FlowTargetCallbacks.MayTarget), 2));
            item.canThisTargetItemFunc =
                new Il2CppSystem.Func<Il2Cpp.GameItem, Il2Cpp.GameItem, bool>(
                    callbacks, CallbackWiring.Method<FlowTargetCallbacks>(
                        nameof(FlowTargetCallbacks.CanTarget), 2));
            item.onThisTargetItemFunc =
                new Il2CppSystem.Action<Il2Cpp.GameItem, Il2Cpp.GameItem>(
                    callbacks, CallbackWiring.Method<FlowTargetCallbacks>(
                        nameof(FlowTargetCallbacks.OnTarget), 2));
        }
    }
}
