using UnityEngine;
using UnityEngine.SceneManagement;

namespace FluxForge.Mod
{
    /// <summary>
    /// 每帧宿主：内部 MonoBehaviour，由 <see cref="FFModEntry.OnEnable"/> 创建，
    /// 挂在隐藏且跨场景保留的 GameObject 上。
    ///
    /// 职责只有三件：Update 推进逻辑、LateUpdate 推进视觉、场景卸载时重置。
    /// </summary>
    internal sealed class ModHost : MonoBehaviour
    {
        private bool _sceneUnloadedSubscribed;

        /// <summary>创建隐藏宿主。重复调用会得到多个实例，调用方负责只创建一次。</summary>
        internal static ModHost Attach()
        {
            GameObject hostObject = new GameObject("FluxForge.ModHost");
            hostObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(hostObject);
            return hostObject.AddComponent<ModHost>();
        }

        private void OnEnable()
        {
            if (!_sceneUnloadedSubscribed)
            {
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                _sceneUnloadedSubscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_sceneUnloadedSubscribed)
            {
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
                _sceneUnloadedSubscribed = false;
            }
        }

        private void Update()
        {
            FlowDriver.Update(Time.deltaTime);
        }

        private void LateUpdate()
        {
            // 视觉归每台机器自己：调度器逐台 Tick，单台失败不连累其它机器。
            FlowDriver.LateUpdateVisuals(Time.deltaTime);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            FlowDriver.Reset();
        }

        /// <summary>由 FFModEntry 在 OnDisable 调用；销毁 GameObject 后宿主不再收到帧回调。</summary>
        internal void Stop()
        {
            UnityEngine.Object.Destroy(gameObject);
        }
    }
}
