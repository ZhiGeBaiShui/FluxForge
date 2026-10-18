using Xunit;

// FlowSpawner / FlowManager 是单例，多个测试类并行跑会互相 Reset 掉对方的状态。
// 这些测试都是毫秒级的纯内存运算，串行跑的代价可以忽略。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
