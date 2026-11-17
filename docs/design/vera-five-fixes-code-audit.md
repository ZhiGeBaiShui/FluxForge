> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 薇拉五项修改：源码依据与验证记录

审查范围：本轮五项需求涉及的生产代码 diff；没有调整通用框架或目录架构。

此前仅运行的 27 项 HospitalContract 测试只包含 Score 和 Schedule，
未编译剧情图、执行边或 StoryPlayer，不能为五条交互流程提供验证结论。
本轮另建 tests/FluxForge.Story.Tests，直接编译实际框架与业务源文件。

## 框架能力的源码依据

| 机制 | 具体源码依据 | 本次用法 |
|---|---|---|
| 注册选项 | src/Story/Runtime/Graph/StoryEdge.cs 的 Connect：设置 From/To，并调用 from.AddOutgoing(this) | 装配新确认节点和返回路径；此前声称 tradeAfterHigh 未连接是错误的，它原本已连接 |
| 玩家选择 | Runtime/Dialogue/StoryNodeDialogue.cs 的 Attach：Label 进入 AddChoice，selected.SetStartAction 调用 RunOption | 所有“是”“我搞错了”“放在桌上了”均由已有机制执行 |
| NPC 台词 | Runtime/Execution/StorySequence.cs 的 SayNpc：传入 _npcName() | “还有高浓度试剂吗”是 NPC 话语，不再放在玩家 Label |
| 动态跳转 | Runtime/StoryPlayer.cs 的 RunEdge：先 await edge.Execute，再调用 edge.ResolveNext | 取货边根据本次检查返回重试或不足量确认节点 |
| 覆写跳转先例 | Npcs/AcidBuyer/Events/AcidBuyerRepeatVisit/Edges/AcidBuyerRepeatVisitEdges.cs 的 ResolveNext | 新取货边沿用现有取货商的执行结果分支方式 |
| 台词后的动作 | Runtime/Execution/StorySequence.cs 的 Do：动作挂在当前尾行 endAction | 原料说完才发放，发放时读取声望 |
| 退出/完成 | Runtime/StoryPlayer.cs 的 ExitStory：Complete 后调用 _onExit；Direction/NpcStoryHost.cs 的 OnStoryFinished/ApplyOutcome 记录事件并按 Leave 离场 | 暂缓进入卖货图后由原合同事件统一结束，不启动嵌套 StoryPlayer |
| 可重复事件 | Runtime/Selection/StoryEventSelector.cs：只在 MaxTimes > 0 时限制 SeenCount | 合同提议 MaxTimes 改为 0；是否再提仍由 OfferReady/Started/OfferDeclined 控制 |
| 七天间隔 | Runtime/Selection/VisitRuleSelector.cs：Day - LastVisitDay < CooldownDays 则跳过；AcidBuyerStory 的普通到访冷却为 7，NpcStoryHost.StartVisit 记录到访日 | 暂缓不清除 offer_ready、不写 offer_declined，无需另建计时器或读档补偿 |

本地游戏 API 依据（已反编译文件不纳入仓库）：
`.tmp-decomp/Dialogue.cs` 的 SetStartAction、SetNextDialogue、ExecuteEndAction；
`.tmp-decomp/DialogUIManager.cs` 的 StartDialogue、ContinueConversation。
它们确认 startAction 可以在进入选中对话时注册后续链，结束动作按 ExecuteEndAction 执行一次。
测试没有替换框架源码，仅模拟上述游戏边界。

## 修改语句的逐项用途

以下按同一职责的连续语句列出，字段、赋值和连接语句均归入对应组。

| 生产文件/语句组 | 必要性与依据 |
|---|---|
| HospitalContractOfferAssembly：selling 节点及两个 decline.Connect | 需求1：直接拒绝或先问详情后暂缓，都回到卖货选项；Connect 的节点注册语义见上表 |
| AcidBuyerRepeatVisitAssembly：CreateTradeOptions 与构造器 | 提取已有完整卖货图供合同复用；避免构造一个入口完全不用的完整 Assembly，再暴露内部 Options 属性 |
| HospitalContractOfferEdges：删除暂缓分支的 DeclineOffer，追加 ResumeTrade | 需求1：暂缓保留 offer_ready；永久拒绝仍调用 DeclineOffer；台词在 Texts 定义 |
| AcidBuyerStory：contract_offer 的 MaxTimes 从 1 改为 0 | 否则已播放一次会被 SeenCount 永久过滤；条件仍阻止已签约或明确拒绝后再提 |
| HospitalContractMaterialEdges：抽取 Append | 正常送料和里程碑共享同一个 SayNpc/Do 发货动作；不复制发货及 MarkMaterial 代码 |
| HospitalContractMilestoneEdges：到期时 Append | 里程碑优先级高于送料，选择器每次仅选一个 VisitStarted 事件；必须显式处理同次到期送料，否则会被本次里程碑占用 |
| HospitalContractStock.WeeklyCount（未改） | 已在方法内读取 HospitalReputation.Get，按20/40分别增加瓶数；Append 的 Do 内调用它，正好满足需求2，无需新建另一套声望判断 |
| HospitalContractInspection：Qualified/Score/HasUnderConcentration 和构造赋值 | 需求3：保存一次当前选择的检查结果，供分支与随后结算共同使用；不跨选项保存库存快照，不添加有效性回退 |
| HospitalContractTrade.Inspect | 原扫描逻辑拆为只读阶段；不销毁、不加钱、不改声望；sawRequestedChemical 区分“相关试剂浓度不够”和“桌上其他液体” |
| HospitalContractTrade.Settle | 仅结算本次检查的 Qualified 与 Score，不再次读取桌面；没有 commit 开关、二次校验或异常吞掉逻辑 |
| HospitalContractPickupAssembly：不足量节点、是、返回连接 | 需求3：两个选项；“我搞错了”返回最初放货选项；“是”复用结算流程并允许不足量 |
| HospitalContractPickupConfirmEdge：字段、构造器和 ResolveNext | 重试和确认目标由 Assembly 明确提供；_acceptShort 区分初次提交与明确确认；_next 保存本次结果，不创建新框架机制 |
| HospitalContractPickupConfirmEdge：每次 Execute 清空 _next | 同一个边对象在图中会被反复选择；若前次不足返回确认节点、下次达标，不清空就会保留前次目标。ShortDeliveryCanReturnToOptionsAndThenCompleteOnce 实际覆盖此路径 |
| HospitalContractPickupConfirmEdge：每次选择 Inspect，验证通过后 Settle/MarkPickup/Notify | 每次玩家选择均读取当下桌面；两次选择之间可补货或换货，这是需求3本身允许的流程；同一次选择不重复扫描 |
| HospitalContractPickupConfirmEdge：按 Target*2 / Target 输出结果 | 需求4：超额仅按得分；保留原超额阈值600分；纯度仍参与原计分公式，但不再独立触发感谢 |
| HospitalContractPickupRetryEdge：无动作的 Execute | “我搞错了”只需要沿 Connect 返回，不应读库存或产生交易副作用；仍按 StoryEdge 的 Task 接口返回完成 |
| HospitalContractPickupTexts / OfferTexts / FirstHighPurityTexts | 把所有新增台词、按钮文字收回已有 Texts 类；删除不再调用的旧台词；与已有事件目录写法一致 |
| HospitalContractSettlement / HospitalContractScore：移除 HasHighPurity 及其阈值 | 需求4已取消纯度分支；不保留无调用方的数据、计算或常量；计分权重仍原样保留 |
| AcidBuyerArrivalAssembly / RepeatVisitAssembly：回答后接 success | 需求5：两种回答都回到已有继续卖货/结束选项，不需要额外 highPuritySuccess 节点和专用交易边 |
| AcidBuyerFirstHighPurityEdges：追加 ContinueQuestion 的 SayNpc | 用户明确指定薇拉提问；使用现有说话人机制，并沿本轮对话链显示普通卖货选项 |
| AcidBuyerTradeSession：ShouldShowHighPurityDialogue 使用本次累计交付体积>0 | 需求5首次提供高浓度试剂就问，不再额外要求100mL；已问过标记仍保证只触发一次 |
| AcidBuyerTrade：更正注释（不改算法） | 原算法是记录本次高纯度体积，并非累计交付次数，修正文档与源码的对应关系 |

## 判断和防御性代码审计

本轮没有新增 null 回退、try/catch、重试循环、重复发货保护标记、额外冷却计时器或静默失败。
下面列出新增/涉及的所有分支及其依据；业务条件不当成“为了安全”添加的防御。

| 判断 | 性质与具体依据 |
|---|---|
| inspection.HasUnderConcentration | 需求3明确要求任何相关不达标试剂存在时返回；检查发生于销毁和付款之前 |
| !_acceptShort && Score < Target | 需求3明确要求先询问；只有明确“是”才能接受不足量 |
| Score >= Target*2 / >=Target | 需求4及已有 HospitalContractScore.Target / 原超额阈值；不是纯度分支 |
| ShouldRunMaterial(day) | 合同状态的实际到期判断；Start 设置首批+3天，MarkMaterial 推进下次日期。没有这个判断会在未到期或已发过的里程碑到访重复发货 |
| 本次到访累计高纯度体积>0 && 尚未问过 | 需求5：首次高浓度交付；体积由 AcidBuyerTrade 对>=99%的实际收货记录，问答标记由回答边写入 |
| sawRequestedChemical / !accepted | 需求3要识别不达标“试剂”；合同品类在 HospitalContractDefinition 明确列出，不能把任意其他液体都当成浓度不够 |
| total<=0（原有，保留） | FlowStock.Read 对不存在的液体tag返回0；空容器是实际数据状态，不能除以0或把它当成不达标试剂 |
| active<=0（原有，保留） | 一个容器不必同时含酸、碱和酒精，零含量意味着该合同品类不存在；不是参数损坏兜底 |
| LiquidContainerTag / FlowDevice（原有，保留） | assets/items.json 的 flow_emitter 同时声明 flow_container 和 device；FlowContainerAttachment.Attach 调用 WaterHelper.InitLiquidContainerItem；.tmp-decomp/WaterHelper.cs:53 确实启用 LIQUID_CONTAINER_TAG。因此发射器能通过第一个判断，必须再排除 FLOW_DEVICE，否则其中的酸液会导致整台设备被卖掉 |
| concentration+Epsilon<MinimumConcentration（原有，保留） | 原浓度比较容差，与 HospitalContractScore.Weight 的同类比较一致；非本轮新添的容错策略 |
| scorePerMillilitre>bestScorePerMillilitre（原有，保留） | 原算法对一个容器只按一个最高匹配品类计分，不能把同一体积重复累计 |

_next 是本次执行的业务分支状态，清空不是空值兜底。Inspect/Settle 之间无 await，也没有用户回调，故不增加快照失效检测或锁。

参数不存在的推测性检查均未添加：Inspect 的 requests 来自静态 HospitalContractDefinition.Chemicals；
Settle 的 inspection 来自同一个 Execute 内刚刚创建的 Inspect 结果；节点由装配层直接构造。

## 可复现验证与边界

```powershell
dotnet build FluxForge.csproj -c Release --no-incremental
dotnet test tests/FluxForge.Story.Tests/FluxForge.Story.Tests.csproj -c Release
dotnet test tests/FluxForge.Tests/FluxForge.Tests.csproj -c Release --filter 'FullyQualifiedName~HospitalContract|FullyQualifiedName~StoryEventSelector|FullyQualifiedName~VisitRuleSelector'
git diff --check
```

新测试逐项验证：混合好坏货整单不结算、返回后重新提交、确认不足量按当前桌面结算、
确认前新增不合格货时返回、零交付需明确确认、599/600分超额边界、纯度满但只达标不感谢、
直接/问详情后暂缓都能卖货、6天不来7天可重提、永久拒绝不重提、首次/重复到访的两种高纯度回答、
低于旧100mL阈值仍只问一次、19/20/39/40声望的实际发货数量、里程碑送料不重复。

实际运行生产代码中的检查、交易、合同存档字典读写、剧情播放器和选择器；
游戏声望适配、库存对象和 UI 使用边界替身。未进行游戏内 UI、真实存读档或协程延时实测。

本轮执行结果：主工程编译通过（0警告、0错误）；新增剧情交互测试23项全部通过；
已有医院计分/日程与剧情/到访选择器测试36项全部通过；git diff --check通过。

上述结果不扩大为全游戏兼容保证；真实游戏的UI交互仍须实测。

## 后续修正：告别依据本次到访所有批次

旧的 AcidBuyerProgress.SetLastHighPurityDeliveryVolume 使用赋值，后交的低浓度批次会把之前的高纯度记录覆写为0。
现已移除该持久化字段的读写，将临时收购记录收回 AcidBuyerTradeSession：

- 首次收购、日常收购和合同拒绝后的收购，各自的装配构造一个 session；同一次到访的交易边与告别边共享它。
- 收货成功后 RecordHighPurityDelivery 以 += 累加本批高纯度体积。失败分支在此之前返回，不记入未购买货物。
- 告别依据整次到访累计结果。原有“>=99%的试剂累计100mL才算足够”的标准没有改变。
- 首次惊讶问答检查本次累计高纯度体积>0，以及永久的已问过标记，继续保持只问一次。
- BeginVisit 由首次/日常/合同提议的实际入口调用。原因是 AcidBuyerStory.Create 构造一次图，NpcStoryHost 后续到访复用该图；因此每次入口必须清零临时累计，不能把前次来访算入本次。
- session 不保存到 modData；不需要增加存档迁移、跨NPC全局变量或兜底清理。

新增14项验证：四种入口先高后低/先低后高、四种入口分两批达到高纯度总量、失败交易不覆盖成功记录、复用剧情图下次到访清零。
本次剧情测试共38项全部通过；主工程编译通过。

## 后续修正：零交付单独确认

按用户最新指定：零交付时先播放“桌上什么都没有......”“你的意思是这周没有试剂提供吗。”，
提供“是。”“搞错了。”；不足量原有确认独立保留。

- 在不达标试剂检查通过后，以 inspection.Qualified.Count == 0 识别零交付，转入单独的 zeroConfirmation 节点。
- “搞错了。”通过已有 Connect 返回放货节点，无业务副作用。
- “是。”使用独立 HospitalContractPickupNoDeliveryEdge，调用 SettleNoDelivery，按0分记录本周不交货。
  即使询问后玩家又放了东西，也不会因选择“本周不交货”而买走它们；需要交货应选“搞错了。”重新提交。
- 正常交货与不交货统一经过 CompleteSettlement 和 HospitalContractPickupSettlement.Append：
  复用付款/声望公式、日期推进与通知，避免两套结算规则。
- 零交付完成语句与部分交付区分，不再说“按这些结算”。
- 保留“这些我收下了”→“......”→惊讶问话的顺序，四个问答测试参数已验证该顺序。

源码检查和测试均使用实际装配、执行边、播放器。新增返回后补货和明确不交货不收取后来货物两项，
补强原空桌测试；首次与重复到访的告别断言分别匹配各自Texts，避免强行要求相同措辞。
剧情交互测试40项通过，主工程编译通过。

## 后续修正：回访问候和供货说明只问一次

旧代码 GreetingEdge.Execute 无条件播放四句首次问候，AskEdge 没有记录已说明状态；
所以此前暂缓后每次都能重新问“怎么供”，并重复得到价目单。

现由 HospitalContractOfferAssembly.EventId 保持原事件ID不变，AcidBuyerStory注册时传入同一NPC的NpcState。
GreetingEdge读取 NpcState.SeenCount(EventId)，复用 NpcStoryHost.OnStoryFinished 已写入的事件完成次数，
不另加已问候标记或新计数器。已完成过合同提议时，改说“你好，老板。”“关于合同，你改变主意了吗？”。

只新增 hospital.contract.details_explained 标记：随首次发放价目单动作写入，跨来访/读档保留。
未说明时入口第一项为“怎么供。”，已说明则转到接受节点，第一项为“行，我答应了。”。
回访第二项是“还是只散卖。”，第三项是“没有意愿。”；初次问候仍保持原分支结构。

GreetingEdge 中 SeenCount>0 或 DetailsExplained 的条件并非损坏存档兜底：
实际存在问过详情、说明标记已写入，但主剧情尚未结束便触发 OnDismiss/Cancel 的路径。
StoryPlayer取消后不会Complete，NpcStoryHost也不会MarkSeen；因此即使完成次数为0，
已问过详情仍必须跳过重复询问。InterruptedOfferDoesNotAllowAlreadyExplainedDetailsAgain验证了此路径。

CreateOptions只连接现有StoryNode/StoryEdge；SetTargets/ResolveNext沿用收购分支方式，未改通用框架。
新增四个测试案例：首次问/未问两种跨读档路径、多轮暂缓不重复价目单、问详情后中断再访不重复。
剧情测试共44项通过；主工程编译通过，0警告0错误。

旧版没有保存供货说明是否已经问过，故无法从现有存档可靠还原这个信息；新标记从更新后开始记录。
旧版已完成过提议的次数则已存在，因此可直接使用回访问候。

## 回访改动的防御性代码专项复核

范围：回访问候、一次性供货说明及其装配/存档改动。没有发现需要删除的无依据防御分支，生产代码本次未改动。

1. SeenCount>0：正常完成过合同提议后需要改用回访问候；NpcStoryHost.OnStoryFinished明确记录MarkSeen。
2. DetailsExplained选择接受节点：明确的业务需求，不是异常兜底。SeenCount不能替代它：正常暂缓也会增加SeenCount，但玩家未必问过供货方式。
3. `SeenCount>0 || DetailsExplained`中的第二项具有实际中断路径依据，不依赖测试随意构造的状态：
   - AskEdge在发价目单的Do回调里写入DetailsExplained，此时主合同剧情仍等待玩家后续选择。
   - 本地游戏反编译源码 `.tmp-decomp/PlayerStore.cs` 的 EndNight 会直接 DismissCurrentClient，
     DismissCurrentClient 调用蓝图的OnDismiss；这条路径不检查dismissable。
   - NpcStoryHost.Create的OnDismiss调用StoryNpcRuntime.CancelForClient，后者调用StoryPlayer.Cancel。
   - StoryPlayer.RunEdge在取消后返回，不调用ExitStory/_onExit，因此不会到达OnStoryFinished的MarkSeen。
   - 所以确实存在DetailsExplained=true而SeenCount=0；不保留该项会再次提供已经询问过的内容。

未新增参数null判断、异常吞掉、节点缺失回退、存档猜测迁移、重试、重复冷却或“已问候”冗余标记。
新存档键缺失表示尚未询问供货方式，是正常初始状态；ContainsKey按既有modData约定读取，不是损坏存档修复。
旧版未记录的供货说明历史仍不推测恢复。
