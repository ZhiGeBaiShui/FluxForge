using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 流源的视觉壳：每个流源一个小方块，**挂在游戏网格自己的 RectTransform 下**。
    ///
    /// 为什么不做成独立的 overlay 层：
    ///   面板关闭时格子组件本身还在（只是所在的 GameObject 失活），
    ///   独立层无法感知“窗口关掉了”，壳会停在原位置继续更新，看起来就是流源飘在空界面上。
    ///   挂成网格的子节点后，父节点失活 → 子节点自动隐藏，裁切、缩放、面板拖动也全部跟随原版，
    ///   不需要任何世界坐标换算。
    ///
    /// 订阅是**按机器**的：每家机器有自己的流源名单，出生/死亡各自广播。
    /// 所以壳天生知道自己属于哪块网格，不需要再从流源身上反查。
    ///
    /// 位置同步放在 LateUpdate：逻辑帧结束时游戏 UI 还没排版完，
    /// 那一刻算出的坐标会被下一帧的布局覆盖掉。
    /// </summary>
    public static class FlowVisual
    {
        private const float CellSize = 16f;

        /// <summary>一块网格的显示信息：物品引用留着，面板重建后靠它重新拿到 rectTransform。</summary>
        private sealed class GridVisual
        {
            public Il2Cpp.GameGridInventory Inventory;
            public RectTransform Rect;
            public FlowSpawner Flows;

            // 订阅用的委托要留着：撤销登记时要 -=
            // （lambda 每次是不同实例，不留引用就再也退订不掉）
            public System.Action<FlowEntity> AddedHandler;
            public System.Action<FlowEntity> RemovedHandler;
        }

        private static readonly Dictionary<FlowEntity, RectTransform> _shells
            = new Dictionary<FlowEntity, RectTransform>();

        private static readonly Dictionary<IFlowGrid, GridVisual> _grids
            = new Dictionary<IFlowGrid, GridVisual>();

        /// <summary>
        /// 出生时网格还取不到 rectTransform 的流源。
        /// 逻辑照跑，只推迟到能取到父节点的那一帧再补壳——否则这些流源永远看不见。
        /// </summary>
        private static readonly Dictionary<FlowEntity, IFlowGrid> _pending
            = new Dictionary<FlowEntity, IFlowGrid>();

        /// <summary>
        /// 流量机创建时登记：记住网格与它对应的流源名单，并订阅该机器的生死广播。
        /// 每家机器一次，互不影响。
        /// </summary>
        public static void RegisterGrid(IFlowGrid grid, Il2Cpp.GameGridInventory inventory, FlowSpawner flows)
        {
            if (grid == null || flows == null)
            {
                return;
            }

            GridVisual visual = new GridVisual();
            visual.Inventory = inventory;
            visual.Flows = flows;
            visual.AddedHandler = entity => OnEntityAdded(entity, grid);
            visual.RemovedHandler = OnEntityRemoved;
            _grids[grid] = visual;

            flows.OnEntityAdded += visual.AddedHandler;
            flows.OnEntityRemoved += visual.RemovedHandler;
        }

        /// <summary>网格作废（流量机被销毁）时撤销登记，避免映射表一直挂着死网格。</summary>
        public static void UnregisterGrid(IFlowGrid grid)
        {
            GridVisual visual;
            if (grid == null || !_grids.TryGetValue(grid, out visual))
            {
                return;
            }

            // 必须退订：机器退役后如果它还能广播，会往 _pending 里塞永远解析不到的实体
            // （网格已不在表里），_pending 就会无界增长
            if (visual.Flows != null)
            {
                visual.Flows.OnEntityAdded -= visual.AddedHandler;
                visual.Flows.OnEntityRemoved -= visual.RemovedHandler;
            }

            _grids.Remove(grid);
        }

        /// <summary>由 MelonMod.OnLateUpdate 每帧调用。</summary>
        public static void LateUpdate()
        {
            // 这里会读原生组件（rectTransform），对象被销毁后可能抛。
            // 不兜住的话视觉层整帧停摆且每帧复现；只记一次，避免刷屏。
            try
            {
                SyncShells();
            }
            catch (Exception e)
            {
                if (!_syncFailed)
                {
                    _syncFailed = true;
                    MelonLoader.MelonLogger.Error("[流源] 视觉同步失败，后续不再重复报告：" + e);
                }
            }
        }

        private static bool _syncFailed;

        private static void SyncShells()
        {
            BuildPendingShells();

            if (_shells.Count == 0)
            {
                return;
            }

            List<FlowEntity> dead = null;

            foreach (KeyValuePair<FlowEntity, RectTransform> pair in _shells)
            {
                // 壳跟着父节点一起被销毁时，这里的引用会变成 Unity 的「假 null」
                if (pair.Value == null)
                {
                    if (dead == null)
                    {
                        dead = new List<FlowEntity>();
                    }

                    dead.Add(pair.Key);
                    continue;
                }

                pair.Value.anchoredPosition = ToGridLocal(pair.Value.parent as RectTransform, pair.Key.Position);
            }

            if (dead != null)
            {
                for (int i = 0; i < dead.Count; i++)
                {
                    _shells.Remove(dead[i]);
                }
            }
        }

        /// <summary>读档时清空所有壳与网格登记。</summary>
        public static void Clear()
        {
            foreach (KeyValuePair<FlowEntity, RectTransform> pair in _shells)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.Destroy(pair.Value.gameObject);
                }
            }

            foreach (KeyValuePair<IFlowGrid, GridVisual> pair in _grids)
            {
                if (pair.Value.Flows != null)
                {
                    pair.Value.Flows.OnEntityAdded -= pair.Value.AddedHandler;
                    pair.Value.Flows.OnEntityRemoved -= pair.Value.RemovedHandler;
                }
            }

            _shells.Clear();
            _pending.Clear();
            _grids.Clear();
        }

        private static void OnEntityAdded(FlowEntity entity, IFlowGrid grid)
        {
            RectTransform gridRect = ResolveGridRect(grid);
            if (gridRect == null)
            {
                _pending[entity] = grid;
                return;
            }

            CreateShell(entity, gridRect);
        }

        /// <summary>为上一帧没能建壳的流源补壳（面板打开、排版完成后就能建）。</summary>
        private static void BuildPendingShells()
        {
            if (_pending.Count == 0)
            {
                return;
            }

            List<FlowEntity> ready = null;

            foreach (KeyValuePair<FlowEntity, IFlowGrid> pair in _pending)
            {
                RectTransform gridRect = ResolveGridRect(pair.Value);
                if (gridRect == null)
                {
                    continue;
                }

                if (ready == null)
                {
                    ready = new List<FlowEntity>();
                }

                CreateShell(pair.Key, gridRect);
                ready.Add(pair.Key);
            }

            if (ready != null)
            {
                for (int i = 0; i < ready.Count; i++)
                {
                    _pending.Remove(ready[i]);
                }
            }
        }

        private static void CreateShell(FlowEntity entity, RectTransform gridRect)
        {
            GameObject shell = new GameObject("FlowEntity");

            RectTransform rect = shell.AddComponent<RectTransform>();

            // 锚点与 pivot 显式写成中心：代码创建的 RectTransform 默认锚点在左下角，
            // 不显式设的话 anchoredPosition 的参照点会和 ToGridLocal 的算法对不上
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            rect.SetParent(gridRect, false);

            // 用网格所在层，避免被只作用于 UI 层的遮罩裁掉
            shell.layer = gridRect.gameObject.layer;

            rect.sizeDelta = new Vector2(CellSize, CellSize);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = ToGridLocal(gridRect, entity.Position);

            // 排在网格里所有物品之后，保证流源画在装置上层
            rect.SetAsLastSibling();

            Image image = shell.AddComponent<Image>();
            image.color = new Color(0.55f, 0.85f, 1f, 0.95f);
            image.raycastTarget = false;

            _shells[entity] = rect;
        }

        private static void OnEntityRemoved(FlowEntity entity)
        {
            _pending.Remove(entity);

            RectTransform rect;
            if (_shells.TryGetValue(entity, out rect))
            {
                if (rect != null)
                {
                    UnityEngine.Object.Destroy(rect.gameObject);
                }

                _shells.Remove(entity);
            }
        }

        /// <summary>
        /// 格坐标 → 网格 RectTransform 的本地坐标（anchoredPosition）。
        ///
        /// 逻辑层的整数点代表「这一格」，不是格子边界，所以每个轴要再加半格，
        /// 壳才会落在格子正中。这个半格偏移只属于渲染，逻辑层不参与。
        ///
        /// 壳的锚点与 pivot 都是 (0.5,0.5)，anchoredPosition 就是壳中心相对网格中心的偏移；
        /// 网格的 <c>rect</c> 同样相对 pivot，因此直接从 rect 的左上角往里算：
        /// X 从左边加，Y 从上边减——格坐标的 (0,0) 是网格左上角，Y 越大越靠下。
        /// </summary>
        private static Vector2 ToGridLocal(RectTransform gridRect, FlowPos cell)
        {
            if (gridRect == null)
            {
                return Vector2.zero;
            }

            Rect area = gridRect.rect;
            return new Vector2(
                area.xMin + (float)((cell.X + 0.5d) * CellSize),
                area.yMax - (float)((cell.Y + 0.5d) * CellSize));
        }

        /// <summary>拿某块网格的 RectTransform（缓存；面板重建后 rectTransform 会变，失效就重取）。</summary>
        private static RectTransform ResolveGridRect(IFlowGrid grid)
        {
            GridVisual visual;
            if (grid == null || !_grids.TryGetValue(grid, out visual))
            {
                return null;
            }

            if (visual.Rect != null)
            {
                return visual.Rect;
            }

            if (visual.Inventory == null || visual.Inventory.Pointer == IntPtr.Zero)
            {
                return null;
            }

            visual.Rect = visual.Inventory.rectTransform;
            return visual.Rect;
        }
    }
}
