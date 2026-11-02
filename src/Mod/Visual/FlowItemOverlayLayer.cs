using System.Collections.Generic;
using FluxForge.Mod.Rendering.ItemSprites;
using FluxForge.Presentation.Flow.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace FluxForge.Mod.Visual
{
    /// <summary>
    /// 每块网格一个的物品顶层覆盖层：把带 <see cref="FlowRenderTags.AboveFlow"/> 标记的
    /// 物品贴图画在流源之上（玻璃反应罐、转向器这类贴图不能被流源盖住）。
    ///
    /// 生命周期与流源解耦（本类不依赖 FlowSpawner / FlowRuntime）：
    ///   · 网格 UI 创建时由 <see cref="FlowItemOverlayLayer"/> 的构造钩子挂到网格节点下，
    ///     第一次发射之前就已经存在，发射瞬间外观不再变化；
    ///   · 每帧由自己刷新（物品拖动/旋转/网格重建都会在下一帧跟到）；
    ///   · 网格节点销毁时本组件随节点一起销毁，字典在 OnDestroy 清空。
    ///
    /// 叠放：本层根是网格 RectTransform 的直接子节点。流源层（FlowAtlas）每次提交时
    /// 会把自己排到网格子节点最后，所以 <see cref="FlowVisualLayer"/> 在提交流源之后
    /// 调 <see cref="BringToFront"/> 把本层压回最后一位——覆盖层永远在流源之上，
    /// 与流源层是第几次发射才懒创建的无关。
    /// </summary>
    public sealed class FlowItemOverlayLayer : MonoBehaviour
    {
        private readonly Dictionary<GameItem, Image> _images = new Dictionary<GameItem, Image>();
        private readonly HashSet<GameItem> _seen = new HashSet<GameItem>();

        /// <summary>首个覆盖 Image 只打一条日志，确认网格构造钩子与 tag 查询都真的生效。</summary>
        private static bool _firstImageLogged;

        private GameGridInventory _inventory;
        private RectTransform _gridRect;

        /// <summary>
        /// 在网格节点下创建覆盖层。由 <see cref="GameGridInventory"/> 的构造 postfix 调用：
        /// 网格 UI（handler / RectTransform）在私有构造里已经建好，此时挂上去即可。
        /// </summary>
        internal static FlowItemOverlayLayer Attach(GameGridInventory inventory)
        {
            RectTransform gridRect = inventory.rectTransform;

            GameObject go = new GameObject("FlowItemOverlay");
            go.layer = gridRect.gameObject.layer;

            RectTransform root = go.AddComponent<RectTransform>();
            root.SetParent(gridRect, false);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = gridRect.rect.size;
            root.localScale = Vector3.one;
            root.SetAsLastSibling();

            // 子 Canvas = 重建边界：覆盖层换贴图/改尺寸只重建自己，不把整块窗口标脏。
            // 与流源层一致，不挂 GraphicRaycaster——本层不接收点击。
            go.AddComponent<Canvas>();

            FlowItemOverlayLayer layer = go.AddComponent<FlowItemOverlayLayer>();

            // AddComponent 会同步触发 OnEnable（只碰 transform 做叠放，不读下表里的字段）；
            // 网格引用在这里补齐，下一帧 LateUpdate 才会第一次刷新内容。
            layer._inventory = inventory;
            layer._gridRect = gridRect;
            return layer;
        }

        /// <summary>
        /// 网格 UI 里已挂好的覆盖层；由 <see cref="FlowVisualLayer"/> 构造时解析一次。
        /// 只找直接子节点：覆盖层由网格构造时挂上，不会跑到下一层网格里去。
        /// </summary>
        internal static FlowItemOverlayLayer Find(RectTransform gridRect)
        {
            int count = gridRect.childCount;
            for (int i = 0; i < count; i++)
            {
                FlowItemOverlayLayer layer = gridRect.GetChild(i).GetComponent<FlowItemOverlayLayer>();
                if (layer != null)
                {
                    return layer;
                }
            }

            return null;
        }

        private void OnEnable()
        {
            // 网格重新激活时先压回最后：流源层可能在网格隐藏期间（面板重建）排到了上面。
            BringToFront();
        }

        private void LateUpdate()
        {
            SyncRootSize();
            UpdateImages();
        }

        /// <summary>
        /// 排到网格子节点的最后。流源层在构造与每 30 次提交时会把自己 SetAsLastSibling，
        /// 本方法在同一帧的流源提交之后被调用，覆盖层因此始终压在流源之上。
        /// </summary>
        internal void BringToFront()
        {
            Transform parent = transform.parent;
            if (parent != null && transform.GetSiblingIndex() != parent.childCount - 1)
            {
                transform.SetAsLastSibling();
            }
        }

        /// <summary>网格销毁：节点由 Unity 一并销毁，这里只清空托管表，避免留着旧物品引用。</summary>
        private void OnDestroy()
        {
            _images.Clear();
            _seen.Clear();
        }

        /// <summary>网格尺寸可能在 SetShape / 重建后变化，跟着同步一次；没变时不写。</summary>
        private void SyncRootSize()
        {
            Vector2 gridSize = _gridRect.rect.size;
            RectTransform root = (RectTransform)transform;
            if (root.sizeDelta != gridSize)
            {
                root.sizeDelta = gridSize;
            }
        }

        /// <summary>
        /// 按当前物品布局刷新覆盖层：只处理带 tag 的物品，物品移出网格 / 失去 tag 时隐藏。
        /// 每帧取一次 childItems，与旧 SubmitItemOverlays 的刷新频率一致。
        /// </summary>
        private void UpdateImages()
        {
            _seen.Clear();

            List<GameItem> items = _inventory.childItems;
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    GameItem item = items[i];
                    if (item == null || !FlowRenderTags.IsAboveFlow(item))
                    {
                        continue;
                    }

                    GridShape shape = item.modifiedShape;
                    if (shape == null)
                    {
                        continue;
                    }

                    Sprite sprite = FlowSpriteRegistry.Instance.GetSprite(item.spritePath);
                    if (sprite == null)
                    {
                        continue;
                    }

                    Image image;
                    if (!_images.TryGetValue(item, out image))
                    {
                        image = CreateImage((RectTransform)transform, "FlowItemOverlayImage");
                        _images[item] = image;

                        if (!_firstImageLogged)
                        {
                            _firstImageLogged = true;
                            FFModEntry.Log.Log("[覆盖层] 首个物品覆盖层已创建：" + item.spritePath);
                        }
                    }

                    _seen.Add(item);
                    image.gameObject.SetActive(true);

                    // 贴图只在变化时写：Image.sprite 赋值会触发本层 Canvas 重建，
                    // 物品没换贴图时（绝大多数帧）不该重建。
                    if (!ReferenceEquals(image.sprite, sprite))
                    {
                        image.sprite = sprite;
                    }

                    float width = shape.width * FlowPixelMath.CellPixels;
                    float height = shape.height * FlowPixelMath.CellPixels;
                    image.rectTransform.sizeDelta = new Vector2(width, height);
                    image.rectTransform.anchoredPosition = new Vector2(
                        shape.minX * FlowPixelMath.CellPixels + width * 0.5f,
                        -(shape.minY * FlowPixelMath.CellPixels + height * 0.5f));
                    image.rectTransform.localEulerAngles =
                        new Vector3(0f, 0f, shape.orientation * 90f);
                }
            }

            foreach (KeyValuePair<GameItem, Image> pair in _images)
            {
                if (!_seen.Contains(pair.Key))
                {
                    pair.Value.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>创建一个绕自身中心旋转的覆盖 Image（锚点左上角、pivot 中心）。</summary>
        private static Image CreateImage(RectTransform root, string name)
        {
            GameObject go = new GameObject(name);
            go.layer = root.gameObject.layer;

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;

            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = false;
            image.type = Image.Type.Simple;
            return image;
        }
    }
}
