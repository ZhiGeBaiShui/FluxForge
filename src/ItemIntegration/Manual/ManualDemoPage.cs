using System;
using FluxForge.Events;
using UnityEngine;

namespace FluxForge.Mod
{
    /// <summary>把事件层演示装配到手册页面预留的虚拟网格区域。</summary>
    public sealed class ManualDemoPage : MonoBehaviour
    {
        // 手动调整演示网格尺寸；净化与冷萃路线至少需要 7 格宽。
        private const int DemoGridWidth = 7;
        private const int DemoGridHeight = 3;
        private const float DemoGridTransparency = 0.65f;
        // 每条边的留白，给非方形装置贴图留出视觉余量。
        private const float DemoGridEdgePadding = 16f;

        private RectTransform gridArea;
        private GameObject placeholder;
        private Func<GameGridInventory, bool> runEvent;
        private GameGridInventory grid;

        internal void Configure(
            RectTransform area,
            GameObject placeholderObject,
            Func<GameGridInventory, bool> eventRunner)
        {
            gridArea = area;
            placeholder = placeholderObject;
            runEvent = eventRunner;
        }

        public void Play()
        {
            Play(runEvent);
        }

        public void PlayBoundaryEmitter()
        {
            Play(FlowEventApi.RunBoundaryEmitter);
        }

        public void PlayTwoEmitterRelay()
        {
            Play(FlowEventApi.RunTwoEmitterRelay);
        }

        private void Play(Func<GameGridInventory, bool> eventRunner)
        {
            Stop();

            grid = new GameGridInventory(DemoGridWidth, DemoGridHeight);
            grid.rectTransform.SetParent(gridArea, false);
            grid.rectTransform.anchorMin = new Vector2(0f, 1f);
            grid.rectTransform.anchorMax = new Vector2(0f, 1f);
            grid.rectTransform.pivot = new Vector2(0f, 1f);

            Vector2 available = gridArea.rect.size;
            Vector2 native = new Vector2(grid.widthPixels, grid.heightPixels);
            float scale = Mathf.Min(
                (available.x - DemoGridEdgePadding * 2f) / native.x,
                (available.y - DemoGridEdgePadding * 2f) / native.y);
            grid.rectTransform.sizeDelta = native;
            grid.rectTransform.anchoredPosition = new Vector2(
                (available.x - native.x * scale) * 0.5f,
                -(available.y - native.y * scale) * 0.5f);
            grid.rectTransform.localScale = new Vector3(scale, scale, 1f);
            grid.SetTransparency(DemoGridTransparency);

            CanvasGroup nonInteractiveGrid = grid.handler.gameObject.AddComponent<CanvasGroup>();
            nonInteractiveGrid.interactable = false;
            nonInteractiveGrid.blocksRaycasts = false;
            placeholder.SetActive(false);
            FlowEventApi.EventCompleted += OnEventCompleted;

            if (!eventRunner(grid))
            {
                Stop();
                throw new InvalidOperationException("手册演示事件启动失败");
            }
        }

        internal void Stop()
        {
            if (grid == null)
            {
                return;
            }

            FlowEventApi.EventCompleted -= OnEventCompleted;
            FlowEventApi.Cancel(grid);
            FlowDriver.RetireByGrid(grid);
            Destroy(grid.handler.gameObject);
            grid = null;
            placeholder.SetActive(true);
        }

        private void OnEventCompleted(GameGridInventory completedGrid)
        {
            if (completedGrid != grid)
            {
                return;
            }

            Stop();
        }

        private void OnDisable()
        {
            Stop();
        }

    }
}
