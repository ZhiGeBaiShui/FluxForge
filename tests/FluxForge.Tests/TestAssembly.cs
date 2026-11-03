using Xunit;

// FlowParticleRules 的 modifier 注册表是进程级全局状态（Register_Modifier_IsAppliedByBuild 会永久追加），
// 多个测试类并行跑会让彼此的 Build 看到未预期的全局状态。
// 这些测试都是毫秒级的纯内存运算，串行跑的代价可以忽略。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
