using System;
using HarmonyLib;
using UnityEngine;

namespace FluxForge.Mod
{
    /// <summary>
    /// 把 Mod 的人物立绘注册进原版 SpriteDict。
    ///
    /// 原图不裁、不缩，先补透明边到原版人物比例，再按原版世界尺寸创建 Sprite。
    /// SpriteDict 每次 Awake 都会重建字典，所以用 postfix 在每次重建后补登记。
    /// </summary>
    public static class NpcPortraitRegistry
    {
        public const string SpriteKey = "fluxforge_saleswoman";

        private const string TextureName = "npc_saleswoman";

        /// <summary>人物在画布中的占比；0.76 = 整体缩小 24%。</summary>
        private const float PortraitScale = 0.76f;

        // 原版人物 Sprite：288 × 211，PPU 100。
        private const float ReferenceWidth = 2.88f;
        private const float ReferenceHeight = 2.11f;
        private const int ReferencePixelWidth = 288;
        private const int ReferencePixelHeight = 211;

        private static Sprite _sprite;

        /// <summary>在 SpriteDict.Awake 之后调用；重复调用安全。</summary>
        public static void Apply()
        {
            if (SpriteDict.Instance == null
                || SpriteDict.Instance.spriteDictionary == null)
            {
                return;
            }

            if (_sprite == null)
            {
                _sprite = Load();
            }

            if (_sprite == null)
            {
                return;
            }

            SpriteDict.Instance.spriteDictionary[SpriteKey] = _sprite;
        }

        public static void Shutdown()
        {
            if (_sprite == null)
            {
                return;
            }

            Texture2D texture = _sprite.texture;
            UnityEngine.Object.Destroy(_sprite);
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }

            _sprite = null;
        }

        private static Sprite Load()
        {
            LoadedMod mod = ModLoader.Mods.Find(
                delegate(LoadedMod candidate)
                {
                    return candidate.Manifest.ID == "fluxforge";
                });

            if (mod == null)
            {
                FFModEntry.Log.Warning("[立绘] 未找到 FluxForge 的 LoadedMod");
                return null;
            }

            Texture2D source;
            if (!mod.Textures.TryGetValue(TextureName, out source)
                || source == null)
            {
                FFModEntry.Log.Warning(
                    "[立绘] Textures/" + TextureName + ".png 未加载");
                return null;
            }

            // 原图完整保留，只补透明边到原版人物比例：
            // 画布保持原版 2.88×2.11，人物按 PortraitScale 缩小并底部对齐。
            int canvasHeight = Mathf.RoundToInt(
                source.height / PortraitScale);
            int canvasWidth = Mathf.RoundToInt(
                canvasHeight * (float)ReferencePixelWidth / ReferencePixelHeight);

            Texture2D canvas = null;
            try
            {
                canvas = new Texture2D(
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
                sprite.name = SpriteKey;
                sprite.hideFlags = HideFlags.DontSave;

                FFModEntry.Log.Log(
                    "[立绘] 已注册 " + SpriteKey
                    + " → " + canvasWidth + "×" + canvasHeight
                    + " ppu=" + ppu.ToString("0.##"));
                return sprite;
            }
            catch (Exception e)
            {
                FFModEntry.Log.Error("[立绘] 创建失败：" + e);
                if (canvas != null)
                {
                    UnityEngine.Object.Destroy(canvas);
                }

                return null;
            }
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
