using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using ProbablyBagMod.Mod.Visual;

[assembly: MelonInfo(typeof(ProbablyBagMod.Mod.PBModEntry), "ProbablyBagMod", "0.1.0", "ProbablyBagMod")]
[assembly: MelonGame(null, null)]

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// mod 入口：把 items.json 的每个物品注册进游戏物品目录。
    ///
    /// 注册走两条路（钩子优先，扫描兜底）：
    ///   ① 官方钩子 <see cref="Il2Cpp.ModHook.OnModItemDirectoryInit"/>：游戏初始化 mod 目录时回调。
    ///      ModItemDirectory.InitDirectory 内部会 Fire，实测会触发。
    ///   ② 兜底：场景加载时 FindObjectsOfType&lt;ItemDirectory&gt; 取现成目录注册。
    /// 两条路只走一条（_registered 保证幂等）。
    /// </summary>
    public sealed class PBModEntry : MelonMod
    {
        private bool _registered;

        public override void OnInitializeMelon()
        {
            // 逐个装补丁：某一个目标不存在/是抽象方法时只记错误，不让整个进程陪葬
            PatchRunner.ApplyAll(HarmonyInstance);

            Il2Cpp.ModHook.OnModItemDirectoryInit = DelegateSupport.ConvertDelegate<
                Il2CppSystem.Action<Il2Cpp.ModItemDirectory>>(OnModItemDirectoryInit);

            Il2Cpp.ModHook.OnGameLoadedInit = DelegateSupport.ConvertDelegate<
                Il2CppSystem.Action>(OnGameLoaded);

            FlowTooltipPatch.LogPatchState();
            RegisterItemTypeNames();

            // 装配件靠反射自动发现：启动时装一次，之后创建物品只查表
            ItemSetup.Initialize();

            LoggerInstance.Msg("ProbablyBagMod 已加载");
        }

        /// <summary>
        /// 卸载 mod / 进程退出：撤掉所有全局状态，避免旧委托在原生侧继续被调用。
        ///
        /// 顺序：先退役机器（退订流源广播、销毁层），再断开游戏钩子、拆 Harmony 补丁，
        /// 最后释放图集贴图。反过来做的话，Hook 回调可能落到已经拆掉的补丁/已释放的贴图上。
        /// </summary>
        public override void OnDeinitializeMelon()
        {
            FlowDriver.Reset();

            // 这两个钩子把我们的托管方法交给了游戏原生侧：不断开的话，
            // 卸载后原生侧仍持有委托，可能在 mod 程序集已不可用时回调进来
            Il2Cpp.ModHook.OnModItemDirectoryInit = null;
            Il2Cpp.ModHook.OnGameLoadedInit = null;

            HarmonyInstance.UnpatchSelf();
            FlowVisualLayer.Shutdown();
            LoggerInstance.Msg("ProbablyBagMod 已卸载");
        }

        /// <summary>读档完成：旧物品、旧网格、旧流源全部作废，整体重置。</summary>
        private void OnGameLoaded()
        {
            FlowDriver.Reset();

            // 读档会重载本地化表，启动时补的类型名会丢，这里再补一次（AddEntry 是覆盖，幂等）
            RegisterItemTypeNames();

            LoggerInstance.Msg("[流源] 读档完成，已重置");
        }

        /// <summary>把 items.json 里声明的物品类型显示名补进本地化表（读档后本地化会重载，需再补）。</summary>
        private static void RegisterItemTypeNames()
        {
            foreach (KeyValuePair<string, string> pair in FlowItemRegistry.Instance.ItemTypes)
            {
                ItemTypeLocalization.Register(pair.Key, pair.Value);
            }
        }

        public override void OnUpdate()
        {
            FlowDriver.Update(UnityEngine.Time.deltaTime);
        }

        public override void OnLateUpdate()
        {
            // 视觉归每台机器自己：调度器逐台 Tick，单台失败不连累其它机器
            FlowDriver.LateUpdateVisuals(UnityEngine.Time.deltaTime);
        }

        /// <summary>官方钩子：mod 目录初始化时回调。</summary>
        private void OnModItemDirectoryInit(Il2Cpp.ModItemDirectory directory)
        {
            if (_registered || directory == null)
            {
                return;
            }

            string report = RegisterInto(directory);
            _registered = true;
            LoggerInstance.Msg("[注册] 经 ModHook.OnModItemDirectoryInit：" + report);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (_registered)
            {
                return;
            }

            Il2CppReferenceArray<UnityEngine.Object> dirs
                = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Il2Cpp.ItemDirectory>());
            if (dirs.Length == 0)
            {
                return;
            }

            Il2Cpp.ItemDirectory directory = dirs[0].TryCast<Il2Cpp.ItemDirectory>();
            if (directory == null)
            {
                return;
            }

            string report = RegisterInto(directory);
            _registered = true;
            LoggerInstance.Msg("[注册] 经场景扫描（钩子未触发）：" + report);
        }

        /// <summary>场景卸载：旧网格、旧物品、旧流源全部作废，和读档走同一条重置路径。</summary>
        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            FlowDriver.Reset();
        }

        /// <summary>把配置里的每个物品塞进目录。返回简要报告。</summary>
        private static string RegisterInto(Il2Cpp.ItemDirectory directory)
        {
            FlowItemRegistry registry = FlowItemRegistry.Instance;
            int success = 0;

            foreach (string key in registry.Keys)
            {
                FlowItemDefinition def = registry.Get(key);
                Func<Il2Cpp.GameItem> factory = () => ItemSetup.Create(def);
                bool added = directory.Add(def.key,
                    DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Il2Cpp.GameItem>>(factory));

                if (added)
                {
                    success++;
                }
                else
                {
                    MelonLogger.Warning("[注册] " + def.key + " 添加失败（可能已存在）");
                }
            }

            return success + "/" + registry.Count + " 个物品";
        }
    }
}
