using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 把托管回调类型注入 IL2CPP，并按名字取它的原生方法指针。
    ///
    /// 为什么必须这么绕：游戏通过 <c>DynamicInvoke</c> 调用这些委托，
    /// <c>DelegateSupport.ConvertDelegate</c> 生成的委托目标是托管桥接对象，
    /// 反射时类型对不上；只有"注入的真实类型 + 原生方法指针"两边都正确。
    /// IL2CPP 下也不能直接赋 lambda（<c>Il2CppSystem.Func</c> 不是委托类型）。
    ///
    /// 每个回调类都需要同样一段样板，所以收敛在这里。
    /// </summary>
    internal static class CallbackWiring
    {
        /// <summary>注册注入类型；重复调用安全。</summary>
        public static void EnsureRegistered<T>() where T : Il2CppSystem.Object
        {
            if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
            {
                ClassInjector.RegisterTypeInIl2Cpp<T>();
            }
        }

        /// <summary>
        /// 取注入类型上的原生方法指针。
        /// 方法名或参数个数写错会返回 <see cref="IntPtr.Zero"/>，委托在调用时才失效。
        /// </summary>
        public static IntPtr Method<T>(string name, int argc) where T : Il2CppSystem.Object
        {
            return IL2CPP.il2cpp_class_get_method_from_name(
                Il2CppClassPointerStore<T>.NativeClassPtr, name, argc);
        }
    }
}
