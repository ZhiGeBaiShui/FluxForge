> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 剧情框架整体清理

## 规范与范围

本次检查 `src/Story/Runtime` 全部生产代码，以及 NPC 注册、原版 UI、Mod 启停的接线。
对照 `src/Logic/README.md`、`src/LogicIntegration/README.md` 和实际管理入口的写法：

- 选择规则保持纯 C#，通过 `IStoryState` 读取状态，不调用游戏 UI。
- `NpcStoryHost` 管理来访，`StoryPlayer` 管理播放；专用辅助类放在各自子目录。
- 游戏状态与 UI 转换集中在 `State`、`Playback/Dialogue`，业务恢复逻辑归具体 NPC。
- `StoryBootstrap` 只创建对象和订阅、退订生命周期，不实现医院业务判断。
- 不改存档键、对白文案、合同要求、奖励或触发门槛。

## 已清理的坏味道

| 原实现及证据 | 处理 |
|---|---|
| `StoryPlayer.Start` 返回 Task，但生产调用方不等待；实际播放由 Dialogue 回调推进 | 删除 TaskCompletionSource 与无用完成任务，保留根结束回调 |
| 生命周期事件在 NpcVisitAssembly 手动记次数，插入事件在播放器另记次数 | 注册事件调用携带事件身份，正常返回时统一 MarkSeen；普通子图不记事件次数 |
| NpcVisitSession 仅包装已持有的上下文、互斥组，还有未被使用的字段 | 删除包装类，整次来访由 NpcVisitAssembly 编排，播放上下文持有共享数据 |
| NpcVisitAssembly 用 ending 布尔参数切换两种阶段行为 | 拆成 AppendTrade / AppendEnding，保留共同的选择操作 |
| NpcStoryHost 直接处理具体 NPC 立绘 | 组合根订阅立绘适配，Host 只管理通用来访 |
| StoryBootstrap 包含医院状态恢复判断 | 原逻辑移至 AcidBuyerStory.RestoreAfterLoad，组合根只订阅 |
| StoryPlayer 自己创建原版对白，同时另一类装配选项 | 合并到 StoryDialogueAdapter，播放器只调度步骤 |
| 上一轮将菜单载体和执行对白误判为重复显示 | 多身份复核原版 SelectChoice 后纠正：必须保留载体 → 执行对白，菜单载体本身不显示为对白；选择测试改为真实跳转顺序 |
| StorySequence 的自定义上下文普通子图 Call 重载没有调用方 | 删除该重载；普通子图继承父上下文，注册事件可携带生命周期上下文 |
| DialoguePacing.Set 先 Remove 再 Add，但唯一生产调用 CreateLine 每次都 new Dialogue | 删除 Remove，不保留没有调用证据的覆盖行为 |
| StoryEdge.From 只赋值从未读取，To 只在类内读取 | 删除 From，将目标节点改为私有字段 |
| EndStory 的客户编号比较只会接到同一个根播放器的结束回调 | 删除无效参数和判断；OnDismiss 入口仍按客户编号识别是否有活动剧情 |
| StoryBootstrap.Shutdown 空 Host 提前返回没有正常调用路径依据 | 删除；生产接线为 FFModEntry.OnEnable 初始化、OnDisable 释放 |
| 卸载只退订 Host，未停止已注册的 Dialogue 回调，且遗留不可解散访客 | StoryNpcRuntime.Shutdown 先取消活动剧情、清除活动身份与待处理交易；Host.Dispose 移除本 Mod 排队访客并标准解散当前匹配访客 |

最后一项有明确游戏路径：`.tmp-decomp/ModLoader.cs:653`、`:661` 调用
`StopRunningMod(..., callOnDisable: true)`；`:682` 调用 Mod 的 OnDisable，
所以卸载可以发生在正常播放期间。原版 PlayerStore.CanEndDay 也检查排队及当前访客的 dismissable，必须一并清理。测试覆盖活动剧情、无活动剧情、普通访客与菜单无存档状态。

## 保留的判断及代码依据

以下区分正常业务状态和异常防御，不能把所有 null 分支等同于防御代码。

| 判断 | 产生该状态的代码证据 |
|---|---|
| `_cancelled` 和待执行 Do 的取消检查 | `.tmp-decomp/PlayerStore.cs:1669` 强制 Dismiss；`:1611` 调用 OnDismiss。`.tmp-decomp/DialogUIManager.cs:723` 在 InterruptDialog 中执行当前 endAction，因此取消后必须拦住待执行的业务动作 |
| Host.RemoveClients 的 HasInstance 检查 | 原版 PlayerStore.cs:671 的 HasInstance 不创建实例，:679–682 的 Instance getter 懒建实例，:3609 的 ResetData 清空实例；菜单卸载不能创建新存档对象 |
| Host 的当前访客非空及注册 ID 匹配检查 | Queue 入队时未必有当前访客，原版也可正在接待普通访客；只清 _contents 已注册的客户 |
| Shutdown 的活动播放器不为空才 Cancel | BeginStory 只在 Host 命中主事件时调用；没有访客或正常 EndStory 后为空。不能在这种状态 InterruptDialog，否则会中断游戏自己的对白 |
| CancelForClient 的客户编号检查 | 每个自定义访客都注册 OnDismiss，而未命中主事件时不调用 BeginStory；正常结束后也已 EndStory。只有仍在播放的客户可以取消播放器 |
| 帧栈、Sequence、Next、Insertion、候选是否为空 | StoryCallFrame.Select 清空 Sequence，Register 赋值；Connect/ResolveNext 的 null 是子图终点；InsertionCursor.Next 遍历完返回 null；这是执行状态机的正常控制流 |
| 有无对白尾行 | Say 创建对白；Do 无前置对白立即执行、有前置对白附在 endAction；Call/Insert 必须在已有对白结束后进入。MultipleActions / NestedCalls 测试覆盖 |
| Call 的上下文继承和事件身份判断 | Call(StoryAssembly) 不指定上下文或事件；NpcVisitAssembly 调用注册事件时明确携带上下文及身份，两种调用同时存在 |
| 不可见桥的清空与替换 | `.tmp-decomp/DialogUIManager.cs:314` 的 OnCurrentTextDisplayed 在行尾回调前判断终点；`:408` 的 ContinueConversation 先运行 startAction 再显示 currentDialog。SetCurrentDialog允许null，已在后续专项审计删除next非空判断 |
| 无交易、无阶段事件、无到访规则、概率未命中 | TakeTradeCompleted 初始为空且读取后清除；选择器按条件、冷却、概率筛选后可以不产生事件；NpcVisitAssembly 只在命中时追加相应阶段 |
| 排队或当前访客存在 | Host 同时订阅读档与开门检查，两次 Queue 可能指向同一 NPC；检查 clientStack 和 currentClientInstance 避免重复入队 |
| OnVisitStarted 可选、VisitCountKey 可选 | FlowEmitterMerchantStory 显式提供旧进度计数与键，AcidBuyerStory 使用默认计数和键；保留两种注册方式及原有存档 |
| FactionId、ExclusionGroup 可选 | NpcDefinition / StoryEvent 注册同时存在 null 和具名值；分别决定是否设置派系、是否消费互斥组 |
| NpcState 的键不存在 | 新存档没有 visits/lastVisitDay/seen 键，第一次写入分别发生在来访和事件完成；这是初始化状态。已有非法值仍直接 Parse，不静默重置 |
| Branch 条件可选、Connect 起点可空 | FlowEmitterArrivalAssembly 同时使用 Connect 和 Branch；入口边 Connect(null, node) 无玩家选项所属节点 |
| 自定义停顿 TryGet 与跳过判断 | CreateLine 只登记自定义行，而 Harmony 补丁运行于所有原版 ScheduleAutoAdvance；原版在调用前已清除跳过标记，因此按实际传入的 0.01f 跳过延时识别，不覆盖该延时。此项依据已在后续逐行审查中纠正 |

没有新增参数空值修复、catch、重试、递归上限、动作去重集合或猜测性的存档恢复。

## 验证边界

测试编译真实 Host、播放器、图、选择器、来访阶段编排与医院业务代码；游戏 UI、客户对象和库存边界由替身提供。
新增检查：按原版菜单载体跳转进入选项、选中对白只显示一次、PLAYER 说话人约定、注册事件完成计数及次数限制、卸载取消待执行动作并释放本 Mod 访客、无剧情卸载不影响原版访客及对白、无存档卸载不创建实例。
编译和测试结果以本次执行记录为准；没有进行真实 Unity UI 实测，也没有部署到游戏目录。

最终多身份复核：Release 编译 0 警告、0 错误；剧情测试 86 项、逻辑测试 518 项全部通过；`git diff --check` 通过。

前次 80 项测试中的选择驱动遗漏了原版跳过菜单载体的语义，不能作为当时选项适配正确的证据。纠正驱动后先复现 54 项失败、26 项通过；恢复双节点适配后 80 项通过，再新增 6 项达到 86 项。详见 `story-framework-multi-agent-review.md`。
