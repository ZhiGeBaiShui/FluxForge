using System;
using System.Collections.Generic;
using ProbablyBagMod.Mod.Visual;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// mod 入口：把 items.json 的每个物品注册进游戏物品目录。
    ///
    /// 注册走两条路（钩子优先，扫描兜底）：
    ///   ① 官方钩子 <see cref="ModHook.OnModItemDirectoryInit"/>：游戏初始化 mod 目录时回调。
    ///      ModItemDirectory.InitDirectory 内部会 Fire，实测会触发。
    ///   ② 兜底：场景加载时 FindObjectsOfType&lt;ItemDirectory&gt; 取现成目录注册。
    /// 两条路只走一条（_registered 保证幂等）。
    ///
    /// tooltip 追加走 Harmony patch <see cref="FlowTooltipPatch"/>（挂在
    /// GetTooltipBasic 末尾，描述/风味之后）；物品贴图走自定义图集：
    /// sprites.json 声明 atlas / key / shape，<see cref="LoadFromAtlasPatch"/> 在
    /// <c>RenderHandler.LoadFromAtlas</c> 里返回按旧 ppu 公式自建的 Sprite
    /// （PNG 由官方 ModLoader 从 <c>Textures/Items/</c> 读进内存，保原始像素密度）。
    ///
    /// 每帧推进由 <see cref="ModHost"/> 负责。
    /// </summary>
    public sealed class PBModEntry : IMod
    {
        /// <summary>物品目录只在启动时创建一次，注册成功后不再重复。</summary>
        private bool _registered;

        /// <summary>每帧宿主；OnDisable 时销毁。</summary>
        private ModHost _host;

        /// <summary>
        /// 全局日志入口。Init 之前也可安全调用（退回 Debug.Log）；
        /// 其它类统一通过 <c>PBModEntry.Log.Log/Warning/Error</c> 输出。
        /// </summary>
        public static PBLogger Log { get; } = new PBLogger();

        public void Init(ModManifest manifest)
        {
            Log.Bind(new ModLog(manifest));

            // tooltip 分派器就绪（helper 只注册一次）
            FlowTooltipPatch.LogPatchState();

            RegisterItemTypeNames();

            // 装配件靠反射自动发现：启动时装一次，之后创建物品只查表
            ItemSetup.Initialize();

            Log.Log("ProbablyBagMod 已加载");
        }

        public void OnEnable()
        {
            // 直接委托：Mono 下 ModHook 的事件就是普通 C# 事件，不需要 DelegateSupport。
            ModHook.OnModItemDirectoryInit += OnModItemDirectoryInit;
            ModHook.OnGameLoadedInit += OnGameLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            _host = ModHost.Attach();

            Log.Log("ProbablyBagMod 已启用");
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= OnModItemDirectoryInit;
            ModHook.OnGameLoadedInit -= OnGameLoaded;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_host != null)
            {
                _host.Stop();
                _host = null;
            }

            FlowDriver.Reset();
            FlowVisualLayer.Shutdown();

            Log.Log("ProbablyBagMod 已卸载");
        }

        /// <summary>读档完成：旧物品、旧网格、旧流源全部作废，整体重置。</summary>
        private void OnGameLoaded()
        {
            FlowDriver.Reset();

            // 读档会重载本地化表，启动时补的类型名会丢，这里再补一次（AddEntry 是覆盖，幂等）
            RegisterItemTypeNames();
            Log.Log("[流源] 读档完成，已重置");
        }

        /// <summary>把 items.json 里声明的物品类型显示名补进本地化表（读档后本地化会重载，需再补）。</summary>
        private static void RegisterItemTypeNames()
        {
            foreach (KeyValuePair<string, string> pair in FlowItemRegistry.Instance.ItemTypes)
            {
                ItemTypeLocalization.Register(pair.Key, pair.Value);
            }
        }

        /// <summary>官方钩子：mod 目录初始化时回调。</summary>
        private void OnModItemDirectoryInit(ModItemDirectory directory)
        {
            if (_registered || directory == null)
            {
                return;
            }

            string report = RegisterInto(directory);
            _registered = true;
            Log.Log("[注册] 经 ModHook.OnModItemDirectoryInit：" + report);
        }

        /// <summary>钩子未触发时的兜底：场景加载后找现成的物品目录注册。</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_registered)
            {
                return;
            }

            ItemDirectory[] dirs = UnityEngine.Object.FindObjectsOfType<ItemDirectory>();
            if (dirs.Length == 0)
            {
                return;
            }

            ItemDirectory directory = dirs[0];
            if (directory == null)
            {
                return;
            }

            string report = RegisterInto(directory);
            _registered = true;
            Log.Log("[注册] 经场景扫描（钩子未触发）：" + report);
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
    public sealed class PBLogger
    {
        private ModLog _modLog;

        internal void Bind(ModLog modLog)
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

            Debug.Log("[ProbablyBagMod] " + message);
        }

        public void Warning(object message)
        {
            if (_modLog != null)
            {
                _modLog.Warning(message);
                return;
            }

            Debug.LogWarning("[ProbablyBagMod] " + message);
        }

        public void Error(object message)
        {
            if (_modLog != null)
            {
                _modLog.Error(message);
                return;
            }

            Debug.LogError("[ProbablyBagMod] " + message);
        }
    }
}
