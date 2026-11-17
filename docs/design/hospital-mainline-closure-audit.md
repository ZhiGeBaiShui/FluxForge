> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 医院主线闭环检查（2026-11-16）

结论：接受并完成的业务路线已贯通。根据后续确认，已删除接取后的放弃选项，任务期间菜单固定三个选项；原第四选项显示缺口不再存在。完整游戏界面仍未逐条实测。奥雅黑针配方/合成路线属于明确暂缓的扩展。

| 环节 | 当前代码证据 | 核对结果 |
|---|---|---|
| 签约入口与拒绝 | [HospitalContractOfferAssembly.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalContractOffer/HospitalContractOfferAssembly.cs:16) | 怎么供后才出现同意；暂缓和永久拒绝都回散卖。永久拒绝不进入医院主线。 |
| 固定周业务 | [HospitalContractState.cs](E:/code/FluxForge/src/Story/Hospital/HospitalContractState.cs:114) | 收货按签约星期，送料按签约后三天的星期；跳天不补发其他星期。 |
| 60声望任务接取 | [HospitalContractPickupEdges.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalContractPickup/HospitalContractPickupEdges.cs:23) | 收货开场提议，接受/拒绝直接连接交货菜单；没有空白子图返回。 |
| 60声望进度与完成 | [HospitalHighestDemandState.cs](E:/code/FluxForge/src/Story/Hospital/HospitalHighestDemandState.cs:44) | 已接取后累计已提交纯酸、纯碱、纯酒精各1000mL；结尾感谢后Completed，完成结尾覆盖普通结束段。 |
| 100声望邀请前置 | [HospitalSynaprestState.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalSynaprest/Services/HospitalSynaprestState.cs:28) | 要求有效合同、60任务完成、声望100、未开始且未拒绝、非送料/收货业务日。 |
| 初始原料与周供 | [HospitalSynaprestStock.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalSynaprest/Services/HospitalSynaprestStock.cs:7) | 初始600mL有效成分约99%；完成后周供400mL有效成分>95%，淀粉加酸或碱。 |
| 现有装置可提纯 | [ColdTrapDevice.cs](E:/code/FluxForge/src/Logic/Devices/Reaction/ColdTrap/ColdTrapDevice.cs:108) | 冷萃去除最低含量成分，当前淀粉与酸/碱均少于赛纳普斯；测试用当前1µL/次、1000µL存量配置，并在满后倒空，可无损保留600mL有效成分。测试没有模拟发射、完整管路和Unity UI。 |
| 100任务提交与关闭 | [HospitalSynaprestPickupEdges.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalSynaprest/Edges/HospitalSynaprestPickupEdges.cs:25) | 只收100%纯液，每mL5分；到550mL立即完成，移除原液提交选项；接取后可无限期搁置，不再提供放弃选项。 |
| 完成后的普通收购 | [HospitalContractDefinition.cs](E:/code/FluxForge/src/Story/Hospital/HospitalContractDefinition.cs:15) | 新增100%赛纳普斯，普通权重计算最终每mL5分，形成周供→提纯→收购循环。 |
| 完成后额外周供的实际调用 | [HospitalContractMaterialEdges.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalContractMaterial/HospitalContractMaterialEdges.cs:25) | 正常送料时额外调用GiveWeekly，不替换酸碱原料。 |
| 100声望外聘专家 | [HospitalContractScore.cs](E:/code/FluxForge/src/Story/Hospital/HospitalContractScore.cs:46) | 声望≥100不足交货不扣分，零交货不扣分；低于100沿用旧规则，达标仍增加声望。 |
| 原第四选项问题 | [HospitalContractPickupAssembly.cs](E:/code/FluxForge/src/Story/Hospital/Events/HospitalContractPickup/HospitalContractPickupAssembly.cs:17) | 已删除接取后的放弃边及文案；菜单只有普通提交、结束、提交原液三个选项。 |
| 暂缓扩展 | [ChemicalFormerRecipeCatalog.cs](E:/code/FluxForge/src/ItemIntegration/ItemSpawn/Services/Attachments/ChemicalFormer/ChemicalFormerRecipeCatalog.cs:10) | 当前仅重金属助溶剂两配方，没有黑针。奥雅配方与合成路线由用户明确留待后续，不属于本次医院任务实现遗漏。 |
| 贯通测试 | [HospitalSynaprestTests.cs](E:/code/FluxForge/tests/FluxForge.Story.Tests/HospitalSynaprestTests.cs:65) | 签约→三周正常交货升60→接60任务并完成→再升100→空闲日接原液→实际冷萃逻辑提纯发放原料→完成任务→领取周供→普通选项卖纯原液。 |

初次拒绝是明确的终止分支：拒绝60任务后不能满足100任务前置；初次拒绝100任务后不解锁周供，但普通合同仍可进行。100任务接取后不再有放弃选项，可一直搁置并保留进度。

初始有效成分600mL，完成要求550mL；已验证现有冷萃逻辑可无损完成，但不会补偿玩家实际操作损耗、丢弃或异常中断，遵循用户要求。

验证：179项剧情测试通过。新增贯通测试使用真实剧情图、交易规则、原料生成和冷萃类，替换原版存档/UI边界；不冒称原版游戏完整界面与液体管路实测。此次仅补测试和检查记录，未擅自修改任务或补偿逻辑。
