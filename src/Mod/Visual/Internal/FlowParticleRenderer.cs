using System.Collections.Generic;
using FluxForge.Presentation.Flow.Particles;
using UnityEngine;
using UnityEngine.UI;

namespace FluxForge.Mod.Visual.Internal
{
    /// <summary>将粒子快照绘制到借入的根下；独占粒子池与白图，不参与模拟或根生命周期。</summary>
    internal sealed class FlowParticleRenderer
    {
        private readonly RectTransform _root;
        private readonly Image[] _dots = new Image[512];
        private int _activeDots;
        private Sprite _dotSprite;

        public FlowParticleRenderer(RectTransform root)
        {
            _root = root;
        }

        /// <summary>消费当次快照与绘制参数；提交末尾按 Layer 的请求重排已有粒子。</summary>
        public void Submit(List<FlowAmbientParticle> dots, Rect area, float ppu, bool reorderDots)
        {
            int count = FlowAtlasLayer.ClampCount(dots.Count, _dots.Length);

            for (int i = 0; i < count; i++)
            {
                Image dot = _dots[i];
                if (dot == null)
                {
                    dot = FlowAtlasLayer.CreateImage(_root, "FlowAtlasDot");
                    dot.sprite = DotSprite;
                    dot.color = Color.white;
                    _dots[i] = dot;
                }

                FlowAtlasLayer.Activate(dot);

                FlowAmbientParticle particle = dots[i];

                float sizePx = ParticleSizePx(particle.SizePx, ppu);
                float sizeUi = sizePx / ppu;
                dot.rectTransform.sizeDelta = new Vector2(sizeUi, sizeUi);
                dot.rectTransform.anchoredPosition = FlowAtlasLayer.PositionFor(
                    area, ppu, particle.X, particle.Y, sizeUi);

                float lifeRatio = particle.MaxLife > 0f
                    ? particle.Life / particle.MaxLife
                    : 0f;
                float alpha = Mathf.Clamp01(lifeRatio * (particle.Alpha / 255f));
                dot.color = new Color32(
                    particle.R,
                    particle.G,
                    particle.B,
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }

            for (int i = count; i < _activeDots; i++)
            {
                FlowAtlasLayer.Deactivate(_dots[i]);
            }

            _activeDots = count;

            // 空列表也消费重排请求，失活槽同样保持在本体之后。
            if (reorderDots)
            {
                PromoteDots();
            }
        }

        /// <summary>只释放本实例懒创建的白图；根与子 Image 由 Layer 销毁。</summary>
        public void DestroyResources()
        {
            if (_dotSprite != null)
            {
                Texture2D texture = _dotSprite.texture;
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }

                UnityEngine.Object.Destroy(_dotSprite);
                _dotSprite = null;
            }
        }

        private static float ParticleSizePx(byte sizePx, float ppu)
        {
            float scaledPx = Mathf.Round(sizePx * ppu);
            if (scaledPx < 1f)
            {
                scaledPx = 1f;
            }

            return scaledPx;
        }

        /// <summary>全部粒子槽共用本实例的 1×1 白图，首次创建粒子 Image 时才初始化。</summary>
        private Sprite DotSprite
        {
            get
            {
                if (_dotSprite == null)
                {
                    Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    texture.SetPixel(0, 0, Color.white);
                    texture.Apply();
                    texture.hideFlags = HideFlags.DontSave;

                    _dotSprite = Sprite.Create(
                        texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                    _dotSprite.hideFlags = HideFlags.DontSave;
                }

                return _dotSprite;
            }
        }

        private void PromoteDots()
        {
            for (int i = 0; i < _dots.Length; i++)
            {
                Image dot = _dots[i];
                if (dot != null)
                {
                    dot.rectTransform.SetAsLastSibling();
                }
            }
        }
    }
}
