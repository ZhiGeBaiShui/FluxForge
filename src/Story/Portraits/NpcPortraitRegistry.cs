using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FluxForge.Story.Portraits
{
    /// <summary>
    /// 把 Mod 的人物立绘注册进原版 SpriteDict。
    ///
    /// 保留源图像素，补透明画布后按本类参考尺寸创建 Sprite；不重采样源图。
    /// SpriteDict 每次 Awake 都会重建字典，所以用 postfix 在每次重建后补登记。
    /// </summary>
    public static class NpcPortraitRegistry
    {
        public const string SpriteKey = "fluxforge_saleswoman";
        public const string AcidBuyerSpriteKey = "fluxforge_acid_buyer";

        /// <summary>源图高度相对画布高度的目标比例；画布尺寸取整会产生少量误差。</summary>
        private const float PortraitScale = 0.76f;

        // 本类采用 288:211 的参考画布比例、Sprite 世界宽度 2.88，不断言全部原版立绘尺寸相同。
        private const float ReferenceWidth = 2.88f;
        private const int ReferencePixelWidth = 288;
        private const int ReferencePixelHeight = 211;

        private sealed class PortraitDefinition
        {
            internal string SpriteKey;
            internal string TextureName;
        }

        private static readonly PortraitDefinition[] Definitions =
        {
            new PortraitDefinition
            {
                SpriteKey = NpcPortraitRegistry.SpriteKey,
                TextureName = "npc_saleswoman",
            },
            new PortraitDefinition
            {
                SpriteKey = NpcPortraitRegistry.AcidBuyerSpriteKey,
                TextureName = "npc_acid_buyer",
            },
            new PortraitDefinition
            {
                SpriteKey = "npc_hospital_visitor_01",
                TextureName = "npc_hospital_visitor_01",
            },
            new PortraitDefinition
            {
                SpriteKey = "npc_hospital_visitor_02",
                TextureName = "npc_hospital_visitor_02",
            },
            new PortraitDefinition
            {
                SpriteKey = "npc_hospital_visitor_03",
                TextureName = "npc_hospital_visitor_03",
            },
            new PortraitDefinition
            {
                SpriteKey = "npc_hospital_visitor_04",
                TextureName = "npc_hospital_visitor_04",
            },
        };

        private static readonly Dictionary<string, Sprite> Sprites =
            new Dictionary<string, Sprite>();

        /// <summary>字典就绪时登记缓存立绘；Awake 后缀和读档完成回调均会调用。</summary>
        public static void Apply()
        {
            // 原版 SpriteDict.GetPool 也明确处理实例或字典尚未初始化的状态。
            SpriteDict dictionary = SpriteDict.Instance;
            if (dictionary == null || dictionary.spriteDictionary == null)
            {
                return;
            }

            for (int i = 0; i < Definitions.Length; i++)
            {
                PortraitDefinition definition = Definitions[i];
                Sprite sprite;
                if (!Sprites.TryGetValue(definition.SpriteKey, out sprite))
                {
                    sprite = Load(definition);
                    if (sprite == null)
                    {
                        continue;
                    }

                    Sprites[definition.SpriteKey] = sprite;
                }

                dictionary.spriteDictionary[definition.SpriteKey] = sprite;
            }
        }

        public static void Shutdown()
        {
            SpriteDict dictionary = SpriteDict.Instance;
            foreach (KeyValuePair<string, Sprite> entry in Sprites)
            {
                if (dictionary != null && dictionary.spriteDictionary != null)
                {
                    dictionary.spriteDictionary.Remove(entry.Key);
                }
                Sprite sprite = entry.Value;
                Texture2D texture = sprite.texture;
                UnityEngine.Object.Destroy(sprite);
                UnityEngine.Object.Destroy(texture);
            }

            Sprites.Clear();
        }

        private static Sprite Load(PortraitDefinition definition)
        {
            LoadedMod mod = ModLoader.Mods.Find(
                delegate(LoadedMod candidate)
                {
                    return candidate.Manifest.ID == "fluxforge";
                });

            Texture2D source;
            if (!mod.Textures.TryGetValue(definition.TextureName, out source))
            {
                Debug.LogWarning(
                    "[立绘] Textures/" + definition.TextureName + ".png 未加载");
                return null;
            }

            // 源图不重采样，透明画布采用本类参考比例。
            // 画布高度由源图高度和 PortraitScale 决定；源图在画布中水平居中、底部对齐。
            int canvasHeight = Mathf.RoundToInt(
                source.height / PortraitScale);
            int canvasWidth = Mathf.RoundToInt(
                canvasHeight * (float)ReferencePixelWidth / ReferencePixelHeight);

            Texture2D canvas = new Texture2D(
                canvasWidth,
                canvasHeight,
                TextureFormat.RGBA32,
                false);
            canvas.filterMode = FilterMode.Point;
            canvas.wrapMode = TextureWrapMode.Clamp;
            canvas.hideFlags = HideFlags.DontSave;

            Color32[] transparent = new Color32[canvasWidth * canvasHeight];
            canvas.SetPixels32(transparent);

            int offsetX = (canvasWidth - source.width) / 2;
            canvas.SetPixels(
                offsetX,
                0,
                source.width,
                source.height,
                source.GetPixels());
            canvas.Apply(false, false);

            float ppu = canvasWidth / ReferenceWidth;
            Sprite sprite = Sprite.Create(
                canvas,
                new Rect(0f, 0f, canvasWidth, canvasHeight),
                new Vector2(0.5f, 0.5f),
                ppu,
                0,
                SpriteMeshType.FullRect);
            sprite.name = definition.SpriteKey;
            sprite.hideFlags = HideFlags.DontSave;

            Debug.Log(
                "[立绘] 已注册 " + definition.SpriteKey
                + " → " + canvasWidth + "×" + canvasHeight
                + " ppu=" + ppu.ToString("0.##"));
            return sprite;
        }
    }

    /// <summary>SpriteDict 每次重建字典后补回自定义立绘。</summary>
    [HarmonyPatch(typeof(SpriteDict), "Awake")]
    internal static class NpcPortraitPatch
    {
        private static void Postfix()
        {
            NpcPortraitRegistry.Apply();
        }
    }
}
