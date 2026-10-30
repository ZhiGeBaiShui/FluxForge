using System;
using HarmonyLib;
using UnityEngine;
using ProbablyBagMod.Mod.Rendering.ItemSprites;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 拦截游戏从 atlas 取 sprite 的调用（物品生成时设置的贴图字段最终走这里解析）：
    /// atlas 名命中贴图管理者配置时返回我们的嵌入贴图。
    ///
    /// 正路：物品工厂设置三字段（spriteAtlasPath=我们的atlas / spritePath=我们的key /
    /// spriteChanged=true）→ 元素刷新 → 解析时 atlas 命中 → 返回自建图（原始像素密度）。
    ///
    /// ⚠ 这是游戏 UI 的原生调用入口，Prefix 里抛异常会顺着原生栈冒出去直接杀进程。
    ///   所以整段包 try/catch，任何失败都退回「放行」，让游戏用原生贴图继续跑。
    /// </summary>
    [HarmonyPatch(typeof(RenderHandler), "LoadFromAtlas")]
    public static class LoadFromAtlasPatch
    {
        /// <summary>命中日志只打一次：UI 每次刷新都会走这里，无条件打会刷爆日志。</summary>
        private static bool _hitLogged;

        public static bool Prefix(string atlasPath, string name, ref Sprite __result)
        {
            try
            {
                SpriteEntry entry;
                if (!FlowSpriteRegistry.Instance.TryMatchAtlas(atlasPath, out entry))
                {
                    return true;
                }

                Sprite sprite = FlowSpriteRegistry.Instance.GetSprite(entry.key);
                if (sprite == null)
                {
                    return true;
                }

                __result = sprite;

                if (!_hitLogged)
                {
                    _hitLogged = true;
                    PBModEntry.Log.Log("[贴图] 首次拦截命中：atlas=" + atlasPath
                        + " name=" + name + " sprite=" + sprite.rect.width + "x" + sprite.rect.height
                        + "（后续命中不再打印）");
                }

                return false;
            }
            catch (Exception e)
            {
                PBModEntry.Log.Error("[贴图] 拦截过程出错，退回原生贴图：" + e);
                return true;
            }
        }
    }
}
