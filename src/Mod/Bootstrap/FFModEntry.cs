using System;
using FluxForge.Mod.Visual;
using UnityEngine;

namespace FluxForge.Mod
{
    /// <summary>
    /// mod 入口：只做接线，不承载业务。
    ///
    /// 职责边界：
    ///   · <see cref="Init"/>      —— 绑定日志、初始化装配件 / tooltip helper / 本地化
    ///   · <see cref="OnEnable"/>  —— 订阅「物品目录初始化」与读档完成、启动每帧宿主
    ///   · <see cref="OnDisable"/> —— 退订、停宿主、清空两张静态登记表与图集
    ///
    /// 物品注册走官方钩子 <see cref="ModHook.OnModItemDirectoryInit"/>：
    /// <c>ModItemDirectory.InitDirectory</c> 里 Fire，mod 在游戏初始化前加载，
    /// 订阅一定早于触发。
    ///
    /// 读档重建由 <see cref="SaveLoaded"/> 负责（生命周期 #6），订阅在
    /// <see cref="OnEnable"/> 完成。
    ///
    /// 生命周期 #9（mod 卸载），完整表见 `Lifecycle/README.md`。
    /// </summary>
    public sealed class FFModEntry : IMod
    {
        /// <summary>每帧宿主；OnDisable 时销毁。</summary>
        private ModHost _host;

        /// <summary>
        /// 全局日志入口。Init 之前也可安全调用（退回 Debug.Log）；
        /// 其它类统一通过 <c>FFModEntry.Log.Log/Warning/Error</c> 输出。
        /// </summary>
        public static FFLogger Log { get; } = new FFLogger();

        public void Init(ModManifest manifest)
        {
            Log.Bind(new ModLog(manifest));

            // 装配件靠反射自动发现：启动时装一次，之后创建物品只查表
            ItemSetup.Initialize();

            // tooltip helper 登记（AppendAll 里也会懒初始化，这里提前装好）
            FlowTooltipHelpers.EnsureInitialized();

            // 本地化表启动时补一次；读档会重载，SaveLoaded 再补
            ItemTypeLocalization.RegisterAll();

            Log.Log("FluxForge 已加载");
        }

        public void OnEnable()
        {
            // 直接委托：Mono 下 ModHook 的事件就是普通 C# 事件，不需要 DelegateSupport。
            ModHook.OnModItemDirectoryInit += OnModItemDirectoryInit;
            SaveLoaded.Subscribe();

            // 先把自定义 WAV 登记进 AudioDirectory，再启动音效服务。
            DeviceAudioClips.Initialize();
            DeviceAudioService.Initialize();
            DeviceAnimationService.Initialize();

            _host = ModHost.Attach();

            Log.Log("FluxForge 已启用");
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= OnModItemDirectoryInit;
            SaveLoaded.Unsubscribe();

            DeviceAudioService.Shutdown();
            DeviceAnimationService.Shutdown();

            // 先停宿主：停止每帧驱动，再拆登记表与视图。
            if (_host != null)
            {
                _host.Stop();
                _host = null;
            }

            // 两张登记表都是静态根：宿主销毁不会带走它们，必须在这里显式清。
            FlowDriver.Shutdown();

            // 最后释放进程级图集；放在视图 Dispose 之后，避免仍挂着的 Image
            // 引用已经销毁的 Sprite。
            FlowVisualLayer.Shutdown();

            Log.Log("FluxForge 已卸载");
        }

        /// <summary>官方钩子：mod 目录初始化时把配置里的全部物品注册进去。</summary>
        private void OnModItemDirectoryInit(ModItemDirectory directory)
        {
            if (directory == null)
            {
                return;
            }

            Log.Log("[注册] " + RegisterInto(directory));
        }

        /// <summary>把配置里的每个物品塞进目录。返回简要报告。</summary>
        private static string RegisterInto(ItemDirectory directory)
        {
            FlowItemRegistry registry = FlowItemRegistry.Instance;
            int success = 0;

            foreach (string key in registry.Keys)
            {
                FlowItemDefinition def = registry.Get(key);
                Func<GameItem> factory = () => ItemSetup.Create(def);
                bool added = directory.Add(def.key, factory);

                if (added)
                {
                    success++;
                }
                else
                {
                    Log.Warning("[注册] " + def.key + " 添加失败（可能已存在）");
                }
            }

            return success + "/" + registry.Count + " 个物品";
        }
    }

    /// <summary>
    /// 静态日志门面：Init 之前退回 Unity 的 Debug 通道，Init 之后转发给游戏 ModLog。
    /// 保留 <c>Log.Log / Log.Warning / Log.Error</c> 三个方法与原有调用点一致。
    /// </summary>
    public sealed class FFLogger
    {
        private ModLog _modLog;

        public void Bind(ModLog modLog)
        {
            _modLog = modLog;
        }

        public void Log(object message)
        {
            if (_modLog != null)
            {
                _modLog.Log(message);
                return;
            }

            Debug.Log("[FluxForge] " + message);
        }

        public void Warning(object message)
        {
            if (_modLog != null)
            {
                _modLog.Warning(message);
                return;
            }

            Debug.LogWarning("[FluxForge] " + message);
        }

        public void Error(object message)
        {
            if (_modLog != null)
            {
                _modLog.Error(message);
                return;
            }

            Debug.LogError("[FluxForge] " + message);
        }
    }
}
