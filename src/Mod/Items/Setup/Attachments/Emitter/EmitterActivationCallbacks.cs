using System;
using Il2CppInterop.Runtime.Injection;

namespace ProbablyBagMod.Mod
{
    /// <summary>供原生委托反射调用的实例目标；不保存物品或网格归属。</summary>
    public sealed class EmitterActivationCallbacks : Il2CppSystem.Object
    {
        public EmitterActivationCallbacks(IntPtr pointer) : base(pointer)
        {
        }

        public EmitterActivationCallbacks()
            : base(ClassInjector.DerivedConstructorPointer<EmitterActivationCallbacks>())
        {
            ClassInjector.DerivedConstructorBody(this);
        }

        public bool MayActivate(
            Il2Cpp.GameItem item, Il2Cpp.GameInventory inventory, Il2Cpp.SlotMarker marker)
        {
            return EmitterAttachment.MayActivateEmitter(item, inventory, marker);
        }

        public bool CanActivate(
            Il2Cpp.GameItem item, Il2Cpp.GameInventory inventory, Il2Cpp.SlotMarker marker)
        {
            return EmitterAttachment.CanActivateEmitter(item, inventory, marker);
        }

        public void Activate(
            Il2Cpp.GameItem item, Il2Cpp.GameInventory inventory, Il2Cpp.SlotMarker marker)
        {
            EmitterAttachment.ActivateEmitter(item, inventory, marker);
        }
    }
}
