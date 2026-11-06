using System.Collections;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>
    /// 事件层唯一的协程宿主：隐藏的 DontDestroyOnLoad 对象，不实现 Update。
    /// 事件只通过 FlowSpawner 事件和协程等待推进，不做每帧扫描。
    /// </summary>
    internal sealed class EventCoroutineHost : MonoBehaviour
    {
        private static EventCoroutineHost _instance;

        internal static Coroutine Run(IEnumerator routine)
        {
            Ensure();
            return _instance.StartCoroutine(routine);
        }

        internal static void Stop(Coroutine routine)
        {
            if (_instance != null)
            {
                _instance.StopCoroutine(routine);
            }
        }

        private static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject host = new GameObject("[FluxForge] EventCoroutineHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(host);
            _instance = host.AddComponent<EventCoroutineHost>();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
