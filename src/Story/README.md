> 当前状态索引：2026-11-16，见[项目状态](../../docs/current-project-state.md)。自动测试结果及待执行清理见维护记录。

# Story Framework

## 新增剧情

1. 写 StoryAssembly，Entry指向入口边，用StoryNode/StoryEdge连接玩家选项。
2. 边的 `void Execute(StorySequence player)` 同步注册对白、动作、子调用或插入点。
3. 在NPC的NpcStoryContent.EventDefinitions注册StoryEventDefinition，条件、次数和优先级沿用StoryEvent。
4. 在 `mod/Localization/zh.csv` 和 `en.csv` 添加同名键。Texts常量保存键；台词在执行时调用原版ModHelper，选项构造函数向StoryEdge传键，由菜单显示时解析。不要把翻译结果存入静态字段或长期存在的剧情图。

日常写剧情只需要普通类、方法、条件判断和下面的序列接口，不需要修改播放器或原版UI适配。

```csharp
internal override void Execute(StorySequence player)
{
    player.SayNpc(ModHelper.GetLocalized("fluxforge", "story.example.received"));
    player.Do(CommitDelivery);
    player.Insert(HospitalStoryPoints.PickupSettled);
    player.SayNpc(ModHelper.GetLocalized("fluxforge", "story.example.farewell"));
}
```

上例为示意，example键须在新剧情落地时添加到两份CSV；医院提交时累计本次分数，确认结束时统一付款。插入点应放在业务动作之后、告别之前。CSV每条占一行，键不加引号，值加双引号，换行写成字面量 `\n`，值内双引号写两次。参数用 `{0}` 等，中英文须一致。

## 独立注册中途片段

```csharp
new StoryEventDefinition(
    new StoryEvent(
        "my.progress", npcId, HospitalStoryPoints.PickupSettled,
        20, 0, null, context => MyQuest.Active),
    new MyProgressAssembly(),
    StoryOutcome.Default)
```

主流程只声明稳定命名的插入点，不引用具体任务。新增片段只添加自己的注册与内容。
当前实例：HospitalHighestDemand.CreateEvents独立注册进度、完成片段；请求与接受/拒绝后返回交货菜单采用下述普通图连接。

## 提问、玩家选择与返回菜单

沿用 HospitalContractOfferAssembly 的设计：提问边连接选项节点，各回答边播放对应 NPC 回复，再 Connect 到后续菜单。需要复用时使用 CreateOptions(returnTo) 组装同一张图，不跨播放帧转交菜单。

HospitalHighestDemandOfferAssembly.CreateOptions(returnTo) 也使用这一方式：接受/拒绝边直接连接当前交货菜单，菜单附在最后一句实际回复上。不能只断言 Choices 存在；测试也要检查最后一条显示对白非空，并能选择交货直至结束。

现有播放器对子图末尾使用续接桥恢复父图。若插入点后父图直接是菜单、没有后续对白，会走 StoryDialogueAdapter.BuildOptions 的空白承载行；该路径尚未完成原版 UI 适配。不要把这样的分支当作已验证的普通交互。插入进度/完成片段后接实际告别对白的原有用法保持不变。

直接调用已知子图：`player.Call(new ChildAssembly())`。
子图可以有选项、循环、嵌套Call或Insert；子图终点null只返回父调用后面，不结束NPC来访。
只有最外层结束才调用Host的离场/开店回调。各子图正常返回仍会调用自身Complete。

## 执行语义

- SayNpc/SayPlayer/SaySystem注册对白；Texts只存文字，Label是玩家选项。
- Do按注册顺序在上一句结束时执行；此前没有对白时立即执行。
- Call和Insert是真正的执行边界：前面的对白与Do完成后才进入，不提前求条件。
- 每条边Execute后立即快照ResolveNext，保留既有Branch语义，子图不会覆盖父返回节点。
- 父帧游标在调用前推进，返回不重跑付款等副作用；没有额外“动作执行过”集合。
- 插入候选按优先级降序、同优先级按注册顺序，一次点调用中每个候选考察一次。
- 每个候选开始前按最新状态运行原选择器规则。独立片段可连续播放；同互斥组只能选一段。
- 已注册事件（生命周期或插入片段）正常返回后，由播放器统一记录NpcState.SeenCount。取消不会记录完成，也不会执行后续父步骤。
- MaxTimes=0不限次数，互斥组范围仍是整次来访；没有新建第二套once/visited状态。
- Condition只读取状态，可能被多次求值；付款、赠礼、写标记等动作放在剧情执行中，不放进条件。

取消来自原版OnDismiss→CancelForClient→StoryPlayer.Cancel，或Mod卸载时的StoryNpcRuntime.Shutdown。已执行交易不回滚；
尚未执行的Do、子图Complete和父后续都停止。任务进度按既有modData保存，不保存播放栈位置。

## 来访生命周期

开店/允许的读档检查→到访规则→客户实际到场→主事件选择。
命中主事件后，同一StoryPlayer依次调用主剧情、可选TradeCompleted剧情、可选VisitEnding剧情。
只有整个来访内容结束才应用Outcome。VisitEnding的明确Outcome可覆盖主结果，TradeCompleted不覆盖。
没有主事件则按VisitKind默认开店或离场。
卸载先取消活动播放，再由Host移除已注册NPC的排队客户，并通过原版DismissCurrentClient释放匹配的当前客户。
主菜单没有PlayerStore实例时，不读取会懒创建存档的Instance。

VisitRule：条件/冷却通过→最高优先级→只判一次Chance；失败不回退其他规则。
StoryEvent：NPC、Trigger、Point、条件、次数、互斥组通过后选择；生命周期阶段各选一个。
InsertionPoint是新增阶段，Point由业务域定义字符串常量，按上述顺序连续处理独立片段。

## 目录

```text
Runtime/
  Bootstrap/                    原项目接入
  Model/ Graph/ Selection/       共享模型、图、纯选择规则
  State/ Contracts/             存档与内容装配契约
  Playback/StoryPlayer.cs       播放管理入口
    Execution/                  调用帧、步骤序列、插入游标、来访上下文
      Steps/                    四种实际步骤
    Dialogue/                   原版UI、不可见续接桥与停顿适配
  Direction/NpcStoryHost.cs      到访管理入口
    Services/                   会话、整次来访装配、当前播放器接入
```

管理入口与辅助类分层；命名空间仍为FluxForge.Story。Runtime不引用Hospital业务类型。

## 原版UI接入

原版OnCurrentTextDisplayed会在行末执行动作前检查next是否为空，直接尾回调追加会过早结束。
StoryDialogueBridge在真实句后放一个不可见续接节点；ContinueConversation先执行其startAction，
桥同步构造下一真实段并SetCurrentDialog，原版显示的是替换后的真实对白，不显示空桥。
整次来访只StartDialogue一次；子图和三个生命周期阶段都沿这条链继续。
玩家选项保留菜单载体→玩家对白两层：原版SelectChoice先把载体设为currentDialog，
随后ContinueConversation进入载体的next，执行真正玩家对白的startAction。载体不作为对白显示。
玩家对白使用原版PLAYER说话人标识，交给原版识别玩家气泡。
Execute改为void保证续接同步，不保留无实际用途的CompletedTask/异步业务等待接口。

## 编写和维护约定

优先使用显式类型、普通方法、if、for和foreach。命名条件或回调变长时拆成普通方法；
简单的Action/Func回调继续保留，因为事件订阅、剧情条件和原版Dialogue就是以回调接入的。
不为替换一个回调新增接口、基类或服务。不引入反射注册、动态代码、异步播放、复杂模式匹配或多层LINQ链。

| 要修改什么 | 从哪里开始 |
|---|---|
| 台词和玩家选项 | 对应事件的Texts、StoryEdge和StoryAssembly |
| 来访时间、事件条件、优先级 | NPC的Story注册及Runtime/Selection |
| 中途插入剧情 | 业务域的命名插入点及片段注册 |
| 子图返回、动作执行时机 | Playback/StoryPlayer及Execution/StoryCallFrame |
| 原版菜单、气泡、续接 | Playback/Dialogue，先核对原版DialogUIManager |

维护时保留三个执行约定：

- 菜单载体和玩家对白是原版跳转所需的两个节点，不能当成重复对白合并。
- Execute返回时就会计算ResolveNext；分支所读的状态不能等后续Do播放时才写入。
- Complete和事件次数只在正常返回时处理；取消不能执行正常结束的奖励和后续动作。

插入候选使用普通循环按优先级排列，同优先级保留注册顺序；每个候选真正开始前再判断条件。
播放器保留调用栈以返回父剧情，不用额外业务标记模拟返回位置。

## 参考与验证

调用返回借鉴Yarn Spinner detour/return和Ink tunnels：
- https://docs.yarnspinner.dev/write-yarn-scripts/scripting-fundamentals/detour
- https://github.com/inkle/ink/blob/master/Documentation/WritingWithInk.md#tunnels-run-sub-stories

未导入它们的第三方运行库，继续使用游戏原版Dialogue界面。
测试：dotnet test tests/FluxForge.Story.Tests/FluxForge.Story.Tests.csproj -c Release
游戏API仍由边界替身模拟，不能替代真实UI实测。
