using System;
using System.Collections.Generic;
using UnityEngine;

namespace FluxForge.Traits
{
    /// <summary>
    /// 特质层：把程序集里的 <see cref="IStartingPerkTrait"/> 自动接到官方 StartingPerk 框架。
    ///
    /// 新增特质不需要修改本文件；只写一个特质类即可。
    /// 不引用 FluxForge.Mod、Logic 或贴图层。
    /// </summary>
    public static class TraitLayer
    {
        public const string ModId = "fluxforge";

        private static readonly List<IStartingPerkTrait> Traits = new List<IStartingPerkTrait>();

        public static void Initialize()
        {
            ModHook.OnNewGameStarted -= OnNewGameStarted;
            ModHelper.RemoveStartingPerks(ModId);
            Traits.Clear();

            Type[] types = typeof(TraitLayer).Assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type.IsAbstract
                    || type.IsInterface
                    || !typeof(IStartingPerkTrait).IsAssignableFrom(type))
                {
                    continue;
                }

                IStartingPerkTrait trait = (IStartingPerkTrait)Activator.CreateInstance(type);
                Register(trait);
            }

            ModHook.OnNewGameStarted += OnNewGameStarted;
        }

        public static void Shutdown()
        {
            ModHook.OnNewGameStarted -= OnNewGameStarted;
            ModHelper.RemoveStartingPerks(ModId);
            Traits.Clear();
        }

        private static void Register(IStartingPerkTrait trait)
        {
            ModHelper.AddStartingPerk(
                ModId,
                trait.Id,
                trait.Cost,
                trait.Type,
                () => trait.Name,
                () => trait.Description,
                iconKey: trait.IconKey,
                incompatiblePerks: null,
                extraPerkSlots: 0,
                mindEditable: false);
            Traits.Add(trait);
        }

        private static void OnNewGameStarted(string scenarioId)
        {
            Debug.Log("[FluxForge.Traits] OnNewGameStarted " + scenarioId);

            for (int i = 0; i < Traits.Count; i++)
            {
                IStartingPerkTrait trait = Traits[i];
                bool active = ModHelper.IsStartingPerkActive(trait.Id);
                Debug.Log("[FluxForge.Traits] " + trait.Id + " active=" + active);
                if (active)
                {
                    trait.Apply();
                }
            }
        }
    }
}
