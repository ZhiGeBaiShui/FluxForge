> 当前状态索引：2026-11-16，见[项目状态](../../../../docs/current-project-state.md)。自动测试结果及待执行清理见维护记录。

# 医院特殊采购

80 声望解锁 `HOSPITAL_SPECIAL_PROCUREMENT`，名称、效果和风味文本使用用户指定版本。

`HospitalProcurement` 订阅原版 `OnGenerateCustomerLate`，解锁后用 `RNG.Roll(10)` 判定，成功时从采购名单等概率选择一个 ID，经原版 `StoreClientListDict.CreateStoreClient` 创建，再以 `AddClient` 追加到队列。

`Services/HospitalProcurementClients.cs` 登记四位普通买家，统一对外名称为“中心医院访客”。每次命中采购概率后，四人等概率选择。对应关系如下：

| 用户源文件 | 内部 NPC ID | 立绘键 / PNG 文件名 |
| --- | --- | --- |
| Sprite-0001.aseprite | fluxforge_hospital_visitor_01 | npc_hospital_visitor_01 |
| Sprite-0002.aseprite | fluxforge_hospital_visitor_02 | npc_hospital_visitor_02 |
| Sprite-0003.aseprite | fluxforge_hospital_visitor_03 | npc_hospital_visitor_03 |
| Sprite-0004.aseprite | fluxforge_hospital_visitor_04 | npc_hospital_visitor_04 |

源文件保存在 `assets/npcs/hospital_visitors/`；可见图层按原始 160×160 RGBA 像素无缩放导出到 `mod/Textures/`，沿用 NpcPortraitRegistry 的画布补齐与加载方式。

问候文本在 `Services/Texts/HospitalVisitorTexts.cs`：前三人各两段，第四人三段，段落对应独立的原版 Dialogue 节点。没有首次/再次来访标记。共同使用 BUY、ALL_TYPE、原版预算、AddBasicDialog 和 CompleteClientCreation；原版负责交易、成交回应和离场。预算沿用上层区普通访客的 `RNG.GetRandomInt(250, 300) + StoreStation.GetDayCounter() * 2`。

四人共同沿用原版上层医院采购员的 `IsPersonalConsumption = false` 和 `CanExposeChem = true`：代医院采购不进入个人用药反馈/回购筛选，具备识别虚标化学纯度的扫描能力。扫描能力通过原版字段与鉴别流程实现，不额外生成扫描仪物品。没有额外设置 CanReturn 或 CanReturnAsBuyer，避免重复屏蔽同一流程。

医院派系的鉴别路由统一由 `Hospital/Patches/HospitalClientCreationPatch.cs` 接入：在原版 `CompleteClientCreation` 完成后，为医院派系选择 `ClientCanExposeFunc.ULClientExposeCapacity`，即原版上层医院采购员的鉴别函数。具体 NPC 只配置是否携带扫描能力 `CanExposeChem`，工厂不重复指定鉴别委托。调用者显式使用 `IgnoreExposeFunc=true` 时保留自定义委托，遵守原版参数语义。枪击、再次出现等核查记录见 `docs/design/hospital-visitor-native-behavior-audit.md`。

`Patches/HospitalCrimeModifierPatch.cs` 只修补 `SecData.ApplyCrimeModifier(string, int)`。医院派系且奖励激活时返回 `value / 2`；其余派系和未激活情况由原版处理。不改价格、支付或犯罪分类，不在交易之后补减证据。

`StoryBootstrap` 成对登记和注销四个原版工厂、订阅和退订顾客生成钩子。禁用时先退订并移除排队中的采购客人、释放当前采购客人，再注销工厂和立绘。该机制没有额外天数标记、读档补抽、开门单独抽取或失败重试。

## 原版依据

反编译文件位于 `artifacts/npc-registration-native`：

- `PlayerStore.decompiled.cs:1289`：开门进入 `GenerateClient`；该函数并非每生成一名普通顾客调用一次。
- `StoreClientManager.decompiled.cs:181`：`OnGenerateCustomerLate` 紧邻 `HandlePerkClient`。
- 同文件 `:1308`：“家喻户晓”检查解锁、`RNG.Roll(20)`、挑选客户、`AddClient`。医院沿用这一阶段与流程，概率改为 10。
- `RNG.decompiled.cs:9`：整数概率为随机数 `[0, 100)` 小于 chance。
- 同文件 `:43`：`GetRandomInt` 两端均包含，名单索引上限使用长度减一。
- `SecData.decompiled.cs:585`：`CheckSoldContraband` 取 `GetNegociatedValue()`，调用派系修正，再记录对应犯罪价值。
- 同文件 `:644`：客户对象重载转发到派系字符串重载，因此只修补后者，不重复减半。
- 同文件 `:661`：“守口如瓶”返回整数 `value / 2`，奇数截断。医院使用相同算法。
- `StoreClientList.decompiled.cs:120`：上层区访客的 BUY、预算、分段开场、AddBasicDialog 和 CompleteClientCreation 流程。
- `StoreClient.decompiled.cs:457`：ALL_TYPE 是原版通用收购标签。
- 同文件 `:592`—`:610`：拒收违禁品由 isSecurity、SCAV、LOWER_LEVEL_CITIZEN 等条件开启。医院访客没有这些条件，因此不添加无实际作用的 AcceptingContrabandOverride。
- 同文件 `:619`：AddBasicDialog 提供原版成交与交易完成后离场对话。
- `StoreClientListTier.decompiled.cs:538`：CreateHospitalAdmin 设置 CanExposeChem=true、IsPersonalConsumption=false，是医院采购身份的直接依据。下层医生在 :487—:492 设置非个人消费，并以 50% 概率具备扫描能力；医院采购员则直接具备。
- `SoldItem.decompiled.cs:51`：记录客户 IsPersonalConsumption；同文件 :157—:162 的 SelectEligibleChemicalItem 只筛选个人消费且允许返回的销售记录。非个人消费会在此排除，无需额外关闭返回开关。
- `ClientCanExposeFunc.decompiled.cs:289`：化学纯度特征不匹配、客户 CanExposeChem=true 时，可以揭穿虚标。不是对所有 Mod 自定义液体字段无条件读取。

原版该派系修正入口也用于赃物销赃价值，因此医院与下层区一样会影响经过此入口的销赃记录；没有另外编写违禁品专属公式。

## 防御性代码依据

四人名单已固定填写，删除了之前空名单阶段使用的提前返回。没有添加工厂缺失兜底、随机重试、重复启动兼容、异常捕获或空值默认值。

卸载清理的 HasInstance 和当前客户判空沿用 NpcStoryHost.RemoveClients 已核实的原版状态依据：菜单中可能没有存档，PlayerStore.Instance 会懒建存档；没有当前顾客时 currentClientInstance 为 null。

证据补丁中的派系与 perk 判断是效果适用条件，其他情况返回 true 是 Harmony 的原版执行约定。

## 验证

主项目编译：0 警告、0 错误。剧情测试 121 项通过，覆盖 79/80 阈值、指定文本、10%随机边界、四个真实工厂的身份/立绘/分段对白/共同采购设置、四人均可抽中、卸载时队列与当前客人清理、派系隔离及奇数取整。测试中的 test.procurement.* ID 仅是测试输入，没有加入生产名单。尚未游戏内实测，也未部署。
