> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 剧情装配一致性核对（2026-11-16）

本轮先部署已修复版本，游戏目录DLL与Release产物逐字节一致。随后检查现有剧情的连接方式，不增加无代码依据的异常处理。

## 统一方法

1. 提问、玩家选择、NPC回复、后续菜单使用同一张图的Connect/Branch/ResolveNext；菜单承接实际回复对白。
2. 插入点用于独立的通知、任务进度和完成片段；不能把尚未通过原版UI验证的“子图返回后父图直接显示菜单”当作可用交互。
3. 对白只存在于SayNpc等序列步骤，玩家选项只在StoryEdge.Label；不能只写Texts而不连接边。
4. 真实显示对白必须非空，测试还要实际选择后续选项直到完成，不只检查Choices集合非空。

## 已逐项检查的接法

| 当前剧情 | 代码依据与结果 |
|---|---|
| 奥雅入门邀请 | `FlowEmitterArrivalAssembly`：greeting→interest；接受→work，拒绝→结束；提交失败Branch回work，结束尝试→告别。使用普通图连接。 |
| 奥雅第二访/日常售卖 | 各Assembly以有台词的入口结束，Complete上货，Host应用OpenShop。没有插入子图后直接菜单的连接。 |
| 薇拉初次/日常散卖 | 两个Assembly显式连接root/success/highPurity，失败返回原节点，高浓度回答直接回success。 |
| 合同提议及再次询问 | `HospitalContractOfferAssembly.CreateOptions`：各回答边播放回复，直接连selling或afterAsk；没有跨帧返回菜单。 |
| 正常收货循环与二次确认 | `HospitalContractPickupAssembly`：提交/结束在普通与任务菜单均有连接，失败与撤回通过ResolveNext回本次菜单；结束才统一付款。 |
| 60声望请求 | `HospitalHighestDemandOfferAssembly.CreateOptions(returnTo)`：本次修复后接受与拒绝回复直接连普通交货菜单。不再注册开场插入事件。 |
| 100声望请求与拒绝 | `HospitalSynaprestOfferAssembly`：开场连接两种选择，回答有实际对白，结束本次邀请。 |
| 原液提交、放弃、完成 | `HospitalSynaprestPickupEdges`：反馈后动态返回普通/任务菜单；完成或放弃即时去掉两个特殊选项。 |
| 20/40声望额度通知 | 注册在MaterialDelivered，仅播放通知；不独立抢占开场，不再次发原料。 |
| 60声望进度/完成 | 注册在PickupSettled，父流程HospitalPickupSession.Finish在Insert之后明确SayNpc(Farewell)。 |
| 医院日常送料 | MaterialDelivered是送料的结尾，后续没有玩家菜单，不涉及空白菜单返回路径。 |
| 合同终止 | 明确的系统结果对白结束；没有等待玩家选择的空节点。 |
| 4位医院采购访客 | 原版Dialogue串联招呼，然后AddBasicDialog接原版采购交互；不混入自定义子图菜单。 |

## 代码证据与验证边界

- 本次缺陷依据：`StoryPlayer.BuildNext`子图末尾设置续接桥；恢复父图时tail为空且Next非空，调用`StoryDialogueAdapter.BuildOptions`创建空标题空文本菜单承载行。截图中的空白薇拉对白与此路径一致。
- 当前业务代码中仅剩两处`player.Insert`调用：`HospitalContractMaterialEdge.Append`、`HospitalPickupSession.Finish`；已按上表核对其后续。业务代码没有其他`player.Call`调用。
- 当前`StoryFlowTests.Conversation.Advance`在模拟startAction改写当前对白后，已经断言实际显示的Text非空。此次未重复添加同样断言。
- `HighestDemandDecisionReturnsToDeliveryOnVisibleNpcResponse`分别验证接受、拒绝后的实际回复文字、交货/结束选项，以及继续提交和结算；159项剧情测试通过。
- 奥雅剧情本轮是静态装配核对；测试替身不是原版Unity UI，因此不声称所有分支已游戏内实测。

未发现其他相同接法的明确遗漏；未修改播放器、引入通用兜底、为假想中断增加恢复机制。以后新增提问剧情沿用上述普通图连接。
