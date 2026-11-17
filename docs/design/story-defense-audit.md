> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 剧情框架过度防御专项审计

## 范围与方法

三位子代理重新阅读当前源码，分别检查播放、模型与选择、游戏适配与注册。
范围为 Runtime 全部 36 个 C# 文件，以及立绘、两个 NPC 注册入口和医院插入点共 4 个接入文件。
根代理复核每项发现的原版调用和项目状态来源。沿用逐行审查清单，文件哈希与连续覆盖可执行
`python docs/design/story-line-audit/verify.py` 验证。

没有把“当前没有复现”当作“异常不可能”的证明，也没有把正常业务选择和初始状态统称为异常防御。
本轮不包含所有剧情业务计算和文本的逐行审查，尤其不擅自修改浓度容差、奖励或交易门槛。

## 删除一项

`Playback/Dialogue/StoryDialogueBridge.cs` 原先只在 next 非空时调用 SetCurrentDialog。
原版 `.tmp-decomp/DialogUIManager.cs:754–756` 只有：

```csharp
public void SetCurrentDialog(Dialogue dialogue)
{
    currentDialog = dialogue;
}
```

方法接受 null，且没有额外副作用。桥此前已将当前对白清空，所以再判断 next 不为空没有必要。
现改为直接设置返回值；保留续接前清空，避免扩大回调执行期间的状态变化。
原版 ContinueConversation 在 `:421` 进入 next、`:424` 执行 startAction、`:426` 检查 currentDialog，
因此桥与正常结束协议本身必须保留。

## 保留的边界保护

| 位置 | 保留理由和代码依据 |
|---|---|
| StoryPlayer 的取消检查，包括 Do 回调内部 | PlayerStore.cs:1669 强制离场，:1611 调用 OnDismiss；DialogUIManager.cs:723 在中断时执行 endAction。必须阻止未执行的赠礼及后续动作 |
| NpcStoryHost.RemoveClients 的 HasInstance | PlayerStore.cs:671–682 的 Instance getter 会懒创建，:3609 的 ResetData 会清空。卸载不能创建存档 |
| 当前客户非空且属于注册 NPC 才清理 | PlayerStore.cs:1613 正常离场会清空客户；Host 注册只包含本 Mod 的两个 NPC，不能解散普通访客 |
| StoryNpcRuntime.CancelForClient 的身份比较 | Host 为每个自定义客户设置 OnDismiss，而未命中剧情时不 BeginStory；正常结束也会 EndStory 清空身份 |
| Shutdown 有活动播放器才 Cancel | BeginStory/EndStory 显式写入和清空；无活动剧情时 InterruptDialog 会中断游戏自己的对白 |
| 立绘 Apply/Shutdown 的字典可用性 | SpriteDict.GetPool:219–223 明确允许实例或字典未初始化。本检查承认原版边界状态，不宣称已在本 Mod 中实际触发 |
| 立绘贴图缺键及 Load 返回空后跳过 | ModLoader.cs:1295–1298 只加载存在的目录，:1315 枚举外部文件，:1330 写入对应键；不保证指定文件存在。Load 有明确的 return null 来源 |
| DialoguePacing 只覆盖已登记行 | CreateLine 只登记本 Mod 的对白，而 Harmony 补丁拦截所有原版 ScheduleAutoAdvance；原版行没有登记是正常输入 |
| Pacing 保留 0.01f 跳过延时 | DialogUIManager.cs:347 清除标记，:351 传入 0.01f；普通 :360 使用 ScaleByDialogSpeed(0.99f)+GetTypewriterDelay。两条调用路径已直接核对 |

## 正常控制流和配置，不按异常兜底删除

| 判断或默认值 | 实际状态来源 |
|---|---|
| Start 的 first 非空才启动 UI | BuildNext 可以执行无对白步骤并正常返回 null；不应让原版显示空桥 |
| 帧栈非空、步骤游标未耗尽 | Start/Call 创建帧，返回时 Pop；Say/Do/Call/Insert 注册步骤，Position 每次推进 |
| Sequence 为空才 Register | StoryCallFrame 构造尚未注册；Select 显式清空；Register 显式赋值 |
| Insertion 存在、候选为空时结束该点 | 到达 Insert 创建游标，Next 单次遍历后返回 null，清空后继续父序列 |
| 游标 position>0、position<count | 对应列表插入及读取的真实下标范围，不是猜测性无限循环保护 |
| tail 是否存在 | Start/Resume 没有前置行，RunOption 带选中行；决定连接对白、立即 Do 或延后 endAction，以及何时通过桥调用子图 |
| frame.Next 是否存在 | Connect/Branch 决定下一个选择节点；null 是子图正常终点 |
| Event 是否存在 | 普通子图和根帧没有注册事件身份；注册事件帧才需要完成次数 |
| Call 未指定 Context 时继承父上下文 | StoryCallStep 的普通子图构造不指定上下文；注册事件构造可明确指定，来访编排实际使用两种构造 |
| Speaker 类型和步骤类型分发 | StorySequence 只写入声明的三种说话人和四种步骤，没有未知类型修复或回退执行器 |
| 候选的 NPC、Trigger、Point、条件 | 是已设计的路由规则，分别对应多个 NPC、生命周期和医院两个插入点 |
| MaxTimes 和已消费互斥组 | 事件元数据声明次数与互斥；已完成后 MarkSeen，组命中后消费，不增加第二套 visited |
| 可选互斥组为空 | 无组代表独立事件；具名组代表本次来访的互斥协议，不要求每个事件创建组 |
| 首个候选为空或优先级更高 | 首次选中和更高优先级是两种正常选择状态，严格大于保留同优先级注册顺序 |
| 无到访规则或事件时返回 null | 条件、冷却或概率可以排除所有候选；Host 不排队或按默认来访结果处理 |
| 到访冷却大于零、上次日期非负 | 日常规则 7 天，医院日期规则 0 天；新 NPC 日期 -1 表示没有来过 |
| 非必然来访时抽随机，失败不回退 | VisitRuleSelector 声明的概率协议，已有 ChanceFailureDoesNotFallBack 测试 |
| trade 或阶段事件不存在 | 待处理交易初始为空且被消费后清空；交易后、离场前阶段允许不注册内容 |
| Outcome 的 Default 判断 | Default 明确表示沿用主结果或按 VisitKind 决定，不是错误修复 |
| 完成回调默认空实现 | 只有部分剧情需要 Complete 副作用，其他装配可不覆写 |
| Branch 条件为空使用 Connect 目标 | 奥雅初遇装配同时存在普通 Connect 和条件 Branch |
| Connect 的 from 可以为空 | FlowEmitterArrivalAssembly.cs:17 真实使用 greeting.Connect(null, interest)，入口不属于玩家选项节点 |
| 缺 visits/lastVisitDay/seen 键返回初始值 | PlayerStore.cs:48 创建空 modData；首次到访及首次完成才分别写键。已有数值仍直接 Parse，不静默重置坏数据 |
| VisitCountKey 未指定时用标准键 | 奥雅明确提供旧键，薇拉传 null，两种实际注册都存在 |
| 可选 OnVisitStarted、FactionId、QueueOnGameLoad | 奥雅和薇拉配置不同，分别控制旧计数、派系及读档排队 |
| ContainsKey 和 SetTag(enabled) | 是作者状态接口的查询和开关含义，不是异常恢复。部分接口尚无当前剧情消费，如实保留已设计契约 |
| 立绘缓存未命中则创建 | 首次 Apply 缓存必然为空；Awake 补丁和读档回调会重复 Apply，缓存避免重复生成资源 |
| TryGet 失败时 out seconds=0 | C# out 参数赋值契约，调用方仅在返回 true 时使用，不表示未登记行改为零秒 |

没有新增参数空值修复、广义 catch、TryParse 静默回退、自动重试、递归上限或未知事件替代内容。
本轮结论是上述范围未再发现其他应删除的防御；不是凭此承诺所有调用状态永远不会出错。

## 验证范围

使用现有真实播放器回归测试覆盖嵌套返回、空插入、根结束、取消、事件计数及选项续接。
没有为这项等价简化添加只复刻实现的测试。编译、测试与哈希检查结果见本次执行记录。
真实 Unity UI 与贴图创建仍未实测，没有部署。
