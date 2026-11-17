> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 中心医院访客：原版特殊判定检查

检查依据为当前安装游戏 Assembly-CSharp.dll；反编译文件位于 artifacts/npc-registration-native。此记录区分原版行为与医院访客配置，不新增死亡剧情。

## 枪击与永久离场

- StoreUIManager.decompiled.cs:1287 的 Shoot：非 survivesShot 客户进入普通枪击，按 identifier 调用 ChemicalFeedbackHelper.CancelFeedbackForShotClient。
- ChemicalFeedbackHelper.decompiled.cs:471：仅遍历当前 soldItemsTracker，把匹配客户的既有销售记录 CanReturn / CanReturnAsBuyer 设为 false；不写永久死亡记录，不封禁 sprite。
- AugHelper.decompiled.cs:29 的 GetShootResult：非 isAug 且不满足通缉击杀条件者，返回普通人误杀结果。
- 同文件:96 的 CleanupKill：处理通缉海报、离场、MANSLAUGHTER 和原版派系声望处罚，再调用客户配置的 onShotDead。没有通用贴图黑名单。
- StoreClientUniqueList.decompiled.cs:1312：Eleanor 的 onShotDead 明确设置 isEleanorDead；StoreClientManager.decompiled.cs:1873 根据该标记阻止她继续出现。这是角色专属实现。

医院访客没有 nonShootable、survivesShot、isAug、isWanted 或 onShotDead 的特殊配置，均使用普通客户默认值。因此可被枪杀并执行原版误杀后果；之后同一 ID / 贴图仍可能被特殊采购抽到。没有擅自新增永久死亡规则，也没有医院专属的击杀声望惩罚。原版 CleanupKill 并不会自动处罚任意自定义派系。

## 抓捕、通缉、伪装客户

- StoreClient.decompiled.cs:1270 的 CanReport：举报受 reported、isShot、胁迫、arrestable、dismissable 等条件限制；医院访客沿用普通客户流程。
- PlayerStore.decompiled.cs:1934 的 OnCriminalVisit 只对 isWanted 客户加入 arrestedClients。StoreClientManager.decompiled.cs:1630 的名单过滤用于 possibleRandomWantedClient；不是一般贴图屏蔽机制。
- PlayerStore.decompiled.cs:3427 的 CloseWantedPoster 也要求 isWanted 等条件。医院访客没有通缉海报，不会自动获得通缉角色的永久退出行为。
- StoreClientManager.decompiled.cs:2230 的 FindEventCustomerAugCandidate 要求 eventSourceId 和 MEDICAL / MATERIAL 等明确配置。当前医院访客没有 eventSourceId，不属于此伪装候选。未添加额外“不会成为伪装者”防御标记。

## 贴图选择和冷却

- ClientSpriteVariety.decompiled.cs 保存近期贴图使用记录，优先选择近期未出现的脸；不是死亡记录。医院四人一人一张固定图，不经过原版随机外貌池，因此没有套用该池的避重规则。
- StoreClientManager.decompiled.cs:337 的 IsClientValid 会检查冷却；原版 PickRandomValidClient 会调用它。医院特殊采购采用固定四人名单等概率选择，当前没有冷却筛选，可能连续两次选到同一人。AddClient 自身不等于 PickRandomValidClient。

## 个人用药、采购拒收与扫描

- 医院四人已配置 IsPersonalConsumption=false。SoldItem.decompiled.cs:157 的个人药物反馈筛选不接纳这类记录，所以不进入个人用药反馈 / 化学品回购链。不额外覆盖 CanReturn / CanReturnAsBuyer。
- ALL_TYPE 仍受 StoreClient.IsClientRefusingItem 限制：NOT_FOR_RESALE、危险废物、化学品级别和容器内容等通用规则仍生效。acceptedChemicalProductGrade 保持原版医院采购员同样使用的默认值 1。ChemicalProductHelper.decompiled.cs:216 会拒收 starch_grade / junk_grade；没有为“通用收购”绕过这些限制。
- 发现并修复一项配置遗漏：仅设置 CanExposeChem=true，无法让自定义派系自动获得化学鉴别入口。StoreClient.CompleteClientCreation 在 :565—:590 按派系配置 canClientExposeFeature，自定义派系得到 BasicClientExposeCapacity；ClientCanExposeFunc.decompiled.cs:164 的基础函数没有化学纯度检查。
- 现在由 HospitalClientCreationPatch 在 CompleteClientCreation 完成后统一为医院派系选择原版 ULClientExposeCapacity；具体 NPC 工厂不再指定委托，也不再传 IgnoreExposeFunc=true。适配保留原版 IgnoreExposeFunc 的显式自定义语义，不改变其他派系或 NPC 的 CanExposeChem。ClientCanExposeFunc.decompiled.cs:147 的上层区函数会调用 :289 的 ClientCanExposeChemPurity，后者检查 CanExposeChem 与虚标的 CATEGORY_CHEMICAL_PURITY。没有自定义扫描公式，不声称能读取任意 Mod 流体内部字段。

## 验证与部署状态

编译 0 警告、0 错误；剧情测试 126 项通过，四位客户工厂都断言非个人消费、具备扫描标记且使用原版上层区鉴别入口。另验证医院派系路由与具体 NPC 扫描标记分开、其他派系不受影响、原版 IgnoreExposeFunc 语义保留。测试替身模拟初始化后的 Harmony 回调，未进行游戏内 Harmony / 射击实测。本轮未重新部署。
