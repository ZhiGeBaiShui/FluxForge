using HarmonyLib;
using UnityEngine;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 拦截游戏从 atlas 取 sprite 的调用（物品生成时设置的贴图字段最终走这里解析）：
    /// atlas 名命中贴图管理者配置时返回我们的嵌入贴图。
    ///
    /// 正路：物品工厂设置三字段（spriteAtlasPath=我们的atlas / spritePath=我们的key /
    /// spriteChanged=true）→ 元素刷新 → 解析时 atlas 命中 → 返回嵌入图。
    /// </summary>
    [HarmonyPatch(typeof(Il2Cpp.RenderHandler), "LoadFromAtlas")]
    public static class LoadFromAtlasPatch
    {
        public static bool Prefix(string atlasPath, string name, ref Sprite __result)
        {
            SpriteEntry entry;
            if (FlowSpriteRegistry.Instance.TryMatchAtlas(atlasPath, out entry))
            {
                Sprite sprite = FlowSpriteRegistry.Instance.GetSprite(entry.key);
                if (sprite != null)
                {
                    __result = sprite;
                    return false;   // 拦截：返回我们的贴图
                }
            }

            return true;            // 放行：游戏原生 atlas
        }
    }
}
