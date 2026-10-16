using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;

[assembly: MelonInfo(typeof(ProbablyBagMod.Mod.PBModEntry), "ProbablyBagMod", "0.1.0", "ProbablyBagMod")]
[assembly: MelonGame(null, null)]

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// mod 入口：场景加载时把 items.json 的每个物品注册进原版物品目录；
    /// 生成走通用工厂（原版模板 + 贴图三字段 + shape 占位 + 名称）。
    /// </summary>
    public sealed class PBModEntry : MelonMod
    {
        private bool _registered;

        public override void OnInitializeMelon()
        {
            HarmonyInstance.PatchAll();
            LoggerInstance.Msg("ProbablyBagMod 已加载");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (_registered)
            {
                return;
            }

            Il2CppReferenceArray<UnityEngine.Object> dirs
                = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Il2Cpp.ItemDirectory>());
            if (dirs.Length == 0)
            {
                return;
            }

            Il2Cpp.ItemDirectory directory = dirs[0].TryCast<Il2Cpp.ItemDirectory>();
            FlowItemRegistry registry = FlowItemRegistry.Instance;

            foreach (string key in registry.Keys)
            {
                FlowItemDefinition def = registry.Get(key);
                Func<Il2Cpp.GameItem> factory = () => CreateItem(def);
                directory.Add(def.key,
                    DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Il2Cpp.GameItem>>(factory));
            }

            _registered = true;
            LoggerInstance.Msg("[注册] " + registry.Count + " 个物品已注册");
        }

        /// <summary>通用工厂：原版模板 + 贴图（三字段 + shape）+ 名称。</summary>
        private static Il2Cpp.GameItem CreateItem(FlowItemDefinition def)
        {
            Il2Cpp.GameItem item = Il2Cpp.DirectoryMaster.Item(def.template, true);
            SpriteEntry sprite = FlowSpriteRegistry.Instance.GetEntry(def.SpriteKey);

            if (sprite != null)
            {
                item.spriteAtlasPath = sprite.atlas;
                item.spritePath = sprite.key;
                item.spriteChanged = true;

                Il2Cpp.GridShapeBuilder shapeBuilder = new Il2Cpp.GridShapeBuilder();
                shapeBuilder.SetData(sprite.shape, sprite.shapeWidth);
                item.shape = shapeBuilder.shape;
            }

            if (!string.IsNullOrEmpty(def.name))
            {
                item.SetName(def.name);
            }

            return item;
        }
    }
}
