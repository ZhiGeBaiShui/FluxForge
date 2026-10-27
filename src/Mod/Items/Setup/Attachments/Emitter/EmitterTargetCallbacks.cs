using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 落点回调：别人把物品拖到"我们"身上时，由游戏调用。
    ///
    /// ⚠ 这三个委托是否真的被当前游戏版本调用**尚未验证**。
    ///   在 <see cref="EmitterAttachment.UseOwnLiquidTarget"/> 翻成 true 之前，
    ///   必须先确认拖拽水瓶时 <c>OnTarget</c> 会被触发。
    ///
    /// 无状态：实例只作为原生委托的 target，逻辑全部转发给 <see cref="LiquidTransfer"/>。
    /// </summary>
    public sealed class EmitterTargetCallbacks : Il2CppSystem.Object
    {
        public EmitterTargetCallbacks(IntPtr pointer) : base(pointer)
        {
        }

        public EmitterTargetCallbacks()
            : base(ClassInjector.DerivedConstructorPointer<EmitterTargetCallbacks>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }

        /// <summary>权限。拖拽/悬停时可能反复查询，不能有副作用。</summary>
        public bool MayTarget(Il2Cpp.GameItem self, Il2Cpp.GameItem other)
        {
            try
            {
                return LiquidTransfer.IsLiquidContainer(other);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>可行性。只读：来源是液体容器且确实有液体。</summary>
        public bool CanTarget(Il2Cpp.GameItem self, Il2Cpp.GameItem other)
        {
            try
            {
                return LiquidTransfer.IsLiquidContainer(other)
                    && Il2Cpp.WaterHelper.GetTotalVolume(other) > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>执行：抽干来源，并入自己的存量。</summary>
        public void OnTarget(Il2Cpp.GameItem self, Il2Cpp.GameItem other)
        {
            try
            {
                LiquidTransfer.Accept(self, other);
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("[发射装置] 接水失败：" + e);
            }
        }
    }
}
