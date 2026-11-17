> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 当前剧情选项遗漏检查（2026-11-16）

范围：当前已实现的奥雅、薇拉、医院合同、20/40声望通知、60声望最高需求、100声望原液任务、4位采购访客。

结论：60声望自动开始的已知遗漏已修复；此次未发现其他有明确设计依据的同类遗漏。没有把任务进度通知或奖励通知一律当作必须新增选项的任务。

| 剧情 | 当前代码证据 | 检查结果 |
|---|---|---|
| 奥雅入门邀请、拒绝后再次邀请 | [FlowEmitterArrivalAssembly.cs](E:/code/FluxForge/src/Story/Npcs/FlowEmitterMerchant/Events/FlowEmitterArrival/FlowEmitterArrivalAssembly.cs:17) | interest连接领取体验包与拒绝两条边；不是开场自动领取。 |
| 奥雅入门验收、失败重试与结束尝试 | [FlowEmitterArrivalAssembly.cs](E:/code/FluxForge/src/Story/Npcs/FlowEmitterMerchant/Events/FlowEmitterArrival/FlowEmitterArrivalAssembly.cs:22) | work包含提交与鼓励告别两条选择，未通过验收回到work；选择结束尝试后才赠礼和完成入门。 |
| 奥雅第二阶段到访 | [FlowEmitterSecondVisitAssembly.cs](E:/code/FluxForge/src/Story/Npcs/FlowEmitterMerchant/Events/FlowEmitterSecondVisit/FlowEmitterSecondVisitAssembly.cs:10) | 自动赠送手册并进入商店，未发现已经要求额外接取/拒绝的设计依据；不是任务自动接受。 |
| 奥雅日常售卖 | [FlowEmitterGenericVisitAssembly.cs](E:/code/FluxForge/src/Story/Npcs/FlowEmitterMerchant/Events/FlowEmitterGenericVisit/FlowEmitterGenericVisitAssembly.cs:10) | 对白后补货进入原版商店；原版交易交互承接，不是丢失玩家选择。 |
| 薇拉首次散卖及高浓度问答 | [AcidBuyerArrivalAssembly.cs](E:/code/FluxForge/src/Story/Npcs/AcidBuyer/Events/AcidBuyerArrival/AcidBuyerArrivalAssembly.cs:36) | 初始卖货/退出、成交后继续卖货/没有库存、自己做的/运气好均挂入对应节点，问答返回继续卖货。 |
| 薇拉日常散卖及高浓度问答 | [AcidBuyerRepeatVisitAssembly.cs](E:/code/FluxForge/src/Story/Npcs/AcidBuyer/Events/AcidBuyerRepeatVisit/AcidBuyerRepeatVisitAssembly.cs:36) | 与初访同样连接初始、继续和高纯度问答节点；不是只写了Texts而未挂边。 |
| 医院初次与再次提议合同 | [HospitalContractOfferAssembly.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalContractOffer/HospitalContractOfferAssembly.cs:16) | root接怎么供/暂缓/没有意愿；afterAsk接同意/暂缓/没有意愿；两种拒绝均接selling，未擅自离场。 |
| 合同正式接受与永久拒绝 | [HospitalContractOfferEdges.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalContractOffer/HospitalContractOfferEdges.cs:121) | Start仅在AcceptEdge；DeclineOffer仅在NoInterestEdge。说明详情只记DetailsExplained，未自动签约。 |
| 每周交货循环、零交付/不足二次确认 | [HospitalContractPickupAssembly.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalContractPickup/HospitalContractPickupAssembly.cs:14) | 普通与任务菜单均有提交和结束；不足和零交付确认各有是/撤回两条边，结束不自动收取桌面未提交物品。 |
| 60声望最高需求提议 | [HospitalHighestDemandAssemblies.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalHighestDemand/Assemblies/HospitalHighestDemandAssemblies.cs:15) | 已修复的遗漏：现在明确连接接受和拒绝边，OfferAssembly.Complete不再自动Start；本次检查未新发现同类问题。 |
| 60声望任务的实际开始位置 | [HospitalHighestDemandEdges.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalHighestDemand/Edges/HospitalHighestDemandEdges.cs:31) | 只有接受决策调用Start；拒绝调用Decline。任务提议注册到PickupStarted插入点，两种选择返回收货父流程。 |
| 60声望进度与完成 | [HospitalHighestDemandAssemblies.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalHighestDemand/Assemblies/HospitalHighestDemandAssemblies.cs:42) | 进度是通知，完成状态对应达成条件后的结束对白；不需要再次接受任务，不属于缺失选择。 |
| 100声望原液委托 | [HospitalSynaprestOfferAssembly.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalSynaprest/HospitalSynaprestOfferAssembly.cs:11) | 两个选项均挂入options；接受边发原料并Start，拒绝边Close并播放指定告别，没有自动接受。 |
| 100声望原液提交、放弃与完成 | [HospitalSynaprestPickupEdges.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalSynaprest/Edges/HospitalSynaprestPickupEdges.cs:11) | 仅任务菜单含两个特殊选项；Submit成功累计后完成即切普通菜单，Abandon关闭任务后返回普通菜单，不撤销本次已累计货分。 |
| 20/40声望原料额度通知 | [AcidBuyerStory.cs](E:/code/FluxForge/src/Story/Npcs/AcidBuyer/AcidBuyerStory.cs:159) | 已修复为MaterialDelivered插入事件，不再接管VisitStarted；属于额度通知，无新增任务，无需接取选择。 |
| 合同终止 | [HospitalContractTerminationEdges.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalContractTermination/HospitalContractTerminationEdges.cs:13) | 是低声望触发的合同结果，按现有设计自动终止；没有玩家可否拒绝终止的设计依据。 |
| 4位医院采购访客 | [HospitalProcurementClients.cs](E:/code/FluxForge/src/Story/Hospital/Procurement/Services/HospitalProcurementClients.cs:77) | 招呼后明确调用原版AddBasicDialog接入普通交易，不能将没有自定义StoryEdge误认为没有交易选择。 |

检查方式：读取装配中的Connect/Branch/SetTargets、动态ResolveNext，以及Start/Close/Decline/Complete状态写入调用位置；核对事件注册与父子剧情返回关系。

验证边界：现有157项剧情测试覆盖医院/薇拉与框架的可执行路径；奥雅入门/商店装配本次为静态源码核对，未冒称全部游戏内实测。部署版本的29个文件已与Release产物逐一校验。
