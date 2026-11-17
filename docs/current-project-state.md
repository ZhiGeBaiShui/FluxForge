# 当前项目状态

更新：2026-11-16。本文说明当前源码；历史设计和审查记录不能替代它。验证结果见[清理记录](maintenance/2026-11-16-cleanup.md)。

## 医院主线

医院主线的接受、交付、完成及周供循环已实现。奥雅已有前两次到访和普通来访；尚未设计的是她承接的黑针成型机配方、塞纳普斯合成路线，不是整条奥雅剧情。

| 阶段 | 当前行为 | 代码依据 |
|---|---|---|
| 合同 | 暂缓或无意愿都回到散货收购；暂缓后7天再询问；合同详情全存档只问一次 | `src/Story/Hospital/Events/HospitalContractOffer/` |
| 固定星期 | 签约日+3首次送料、+7首次收货；之后固定两个星期日程，跳天不在其他星期补发 | `src/Story/Hospital/HospitalContractSchedule.cs` |
| 20、40声望 | 当前声望决定3、4瓶额度；只在送料插入提示，不抢占收货 | `src/Story/Npcs/AcidBuyer/AcidBuyerStory.cs`、`HospitalContractStock.cs` |
| 60声望 | 周货款1.2倍；收货日邀请最高需求，玩家接受/拒绝；纯酸、纯碱、纯酒精各累计1000mL后完成，没有额外任务奖励 | `HospitalContractScore.cs`、`Events/HospitalHighestDemand/` |
| 80声望 | 四位医院采购访客；每日10%机会；接受违禁品，证据价值减半 | `src/Story/Hospital/Procurement/` |
| 100声望任务 | 必须完成60任务且当天无正常送料/收货；可接受/首次永久拒绝；接受后累计550mL的100%塞纳普斯有效成分 | `Events/HospitalSynaprest/` |
| 原液与周供 | 初次750mL瓶：600mL有效成分+6061µL杂质，约99%；完成后正常送料额外赠450mL瓶，400mL有效成分，浓度>95%；杂质为淀粉+酸或淀粉+碱，酸碱不同时出现 | `HospitalSynaprestStock.cs` |
| 100声望奖励 | 外聘专家：不足或零交付不掉声望，不二次确认，使用忙碌告别 | `HospitalContractScore.cs`、`HospitalPickupSession.cs` |

普通提交立即收货、累积本次分数，只提示分数；“没有库存了”后统一付款。任务原液提交使用独立选项；任务中普通提交排除塞纳普斯，任务完成后普通选项接收100%原液，每mL为5分。

当前原液任务菜单只有普通提交、结束、原液提交三个选项。接取后没有中途放弃选项；达到550mL立即完成并移除专用提交项。本次到访分数只放内存；不持久化、不补偿异常中断。

60任务完成片段覆盖当次普通总结，结束语只执行一次。100任务完成后的第一次续问为“你还有其他想要交的试剂吗？”，随后普通交货恢复“还有吗。”。

完整台词见[当前源码文本目录](design/story-v5-all-text.md)，交互审阅见[独立HTML](../dist/FluxForge-story-review.html)。HTML是供人工审阅的条件图，不模拟游戏或存档。

## 冷萃与装置手册

- 升温：+50°C，上限160°C；降温：−50°C，下限0°C。
- 冷萃：温差−5°C，相变阈值0°C；入口≥阈值且出口≤阈值时效率20倍，含恰好0°C/5°C。途中仍会向26°C环境温度变化，不能声称所有任意输入均能跨阈值。
- 基础冷萃演示保留；进阶演示仅右侧发射一次，左侧接收后保留2秒结束。
- 自动化：9×5网格，六个L转角、两个升温、两个降温、中央冷萃和左侧发射器。左下转角等流源进入冷萃后放置，闭环形成后10秒清理。
- 游戏 orientation 经 `FlowFacingAdapter.FromOrientation` 反向换成逻辑 Facing，不能直接当顺时针方向码。
- 演示图片使用 Simple，避免继承原版 RenderNodePrefab 的 Tiled 产生窄边重复；该修复尚待游戏内确认。

手册来自 Unity 源预制体 `E:/code/ProbablyMod/Assets/FluxForgeAssets/FlowDeviceBasicManual.prefab`，构建后写入 `mod/AssetBundles/fluxforge_device_manual`。两页直接绑定 `Page_coldtrap_manual` / `Demo_coldtrap_manual_advanced` 和 `Page_coldtrap_auto` / `Demo_coldtrap_auto_advanced`；不克隆基础页，不运行时重排按钮。

## 发布、调试与资源

小键盘9医院声望调试已移除，包括组件、创建和销毁入口。正式资源以 `FluxForge.csproj` 的 EmbeddedResource、CopyToOutputDirectory 和运行时配置引用为准；未使用候选、旧压缩包和试听文件已列入待清理清单，批量删除未获工具策略允许。授权说明保留，不作为无用资源删除。

发布目录包括DLL、manifest.xml、AssetBundles、Localization、Textures；Books仅在存在构建内容时复制。部署需要游戏释放DLL；本轮维护不自动部署或生成发布压缩包。

源码与测试通过不等于Unity游戏内验证；按钮、贴图、完整管线和交互仍需运行游戏确认。

## 游戏内模组详情

描述来自根目录manifest.xml的Description，封面来自根目录preview.png，与工坊VDF元数据分开。正式源图为mod/preview.png，经csproj复制到发布根目录；已采用选定封面并补充新简介及QQ群568324459。原版依据：ModUIManager.RefreshDetailDescription、GetLocalPreview；不新增UI补丁。

## 物品与便条本地化（2026-11-17）

已按原版ModHelper接入外置Localization/zh.csv和en.csv，389个相同文本键。物品名称、说明、风味文本、状态行及剧情原料名称随创建时语言解析。旧存档文字恢复沿用原版SaveManager，不新增文本迁移或库存扫描。

奥雅第一张便条改为Books/starter_note.json，原版图像/箭头/loc文字节点，不再显示写死中文的整张PNG。手册去掉zh-CN直读，打开时读取当前语言，并补齐已有七页英文。

后续已补齐NPC对白、选项、姓名、声望perk、扫描仪、成型机及模组详情，中英文各389个键。剧情选项保留键、显示时翻译；资源就绪后注册介绍与类型文字。未启用模组的manifest介绍为英文后备。未部署、未更新此前的正式ZIP/VDF上传内容。

验证：原版PaperJson成功解析1页22节点；原版ModLoader.ParseCsvValue成功解析中英文共778条；剧情测试192项、逻辑测试517项通过，生产构建通过。原版界面的实际字号、图像朝向及读档后便条交互仍需游戏内实测。
