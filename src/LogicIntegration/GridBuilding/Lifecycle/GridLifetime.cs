using UnityEngine;

namespace FluxForge.Mod
{
    /// <summary>
    /// 网格销毁信号。
    ///
    /// 不碰游戏自己的组件：在网格 RectTransform 下建一个空节点，网格销毁时
    /// Unity 会一并销毁它并回调 <see cref="OnDestroy"/>，据此注销运行环境。
    /// 用节点而不是轮询 <c>rectTransform</c>，是为了让退役发生在销毁当帧，
    /// 不依赖下一帧的存活检查。
    ///
    /// 只在 <see cref="GridRegistry.Register"/> 首次登记时挂一次；构造与 SetShape
    /// 的重复登记不会再建节点。
    ///
    /// 生命周期 #7（网格节点销毁），完整表见 `Lifecycle/README.md`。
    /// </summary>
    public sealed class GridLifetime : MonoBehaviour
    {
        private GameGridInventory _inventory;

        /// <summary>由 <see cref="GridRegistry.Register"/> 在首次登记时调用。</summary>
        public static void Attach(GameGridInventory inventory)
        {
            RectTransform gridRect = inventory.rectTransform;
            if (gridRect == null)
            {
                return;
            }

            GameObject go = new GameObject("FluxForgeGridLifetime");
            go.layer = gridRect.gameObject.layer;
            go.transform.SetParent(gridRect, false);

            GridLifetime lifetime = go.AddComponent<GridLifetime>();

            // AddComponent 会同步触发 OnEnable，这里只补引用；
            // 销毁回调要等网格真正销毁才会走到。
            lifetime._inventory = inventory;
        }

        private void OnDestroy()
        {
            FlowDriver.RetireByGrid(_inventory);
        }
    }
}
