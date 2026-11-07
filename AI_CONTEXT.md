# FluxForge — AI Context

本文件面向接手本项目的 AI / 开发者，用于快速建立整体认知。  
先读本文件，再按需读各子目录 README。

## 1. 项目概况

```text
游戏：Probably Stolen 0.46
引擎：Unity 2021.3，Mono
加载器：官方 ModLoader + Harmony
语言：C#
```

Mod 目标：

```text
流源属性与运动
流量装置与反应
装置事件演示
NPC 剧情与商品
人物立绘、音效、表现层
```

当前状态：

```text
Logic / Presentation / 大部分 Mod 纯逻辑有单元测试
游戏生命周期、Harmony patch、视觉和剧情需要在游戏内验证
```

验证命令：

```powershell
dotnet build FluxForge.csproj -c Release --no-incremental `
  -p:GameManagedDir="E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed"

dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release
```

部署目录：

```text
<游戏目录>/Mods/FluxForge/
├─ FluxForge.dll
├─ manifest.xml
└─ Textures/
   ├─ *.png            人物立绘等根目录资源
   └─ Items/*.png      物品图标
```

## 2. AI 推荐阅读顺序

```text
1. AI_CONTEXT.md（本文件）
2. README.md
3. src/Logic/README.md
4. src/Mod/README.md
5. src/Story/README.md
6. src/Story/Npcs/FlowEmitterMerchant/PORTRAIT.md
7. 按任务读对应子系统
```

## 3. 分层与依赖规则

```text
src/Mod          →  src/Logic
src/Mod          →  src/Presentation
src/Story        →  src/Mod / Logic / Presentation
src/Events       →  src/Logic / Mod
src/Traits       →  官方 StartingPerk API
```

禁止反向依赖：

```text
Logic / Presentation 不引用 UnityEngine
Logic / Presentation 不引用 Assembly-CSharp
```

原因：

```text
纯 C# 才能脱离游戏测试
游戏类型进入 Logic 会破坏可移植性和单元测试
```

## 4. 目录总览

```text
src/
├─ Logic/           纯 C# 领域逻辑
│  ├─ Flow/          流源、属性、位置、运动数据
│  ├─ Motion/        流源生命周期、移动、碰撞
│  ├─ Devices/       装置接口、反应装置、路由装置
│  ├─ Reactions/     实时反应规则与公式
│  └─ Presentation/  纯逻辑的音效/动画语义 ID
├─ Presentation/    纯 C# 表现计算
│  ├─ Flow/Visual/   颜色、图集索引、像素坐标、视觉状态
│  └─ Flow/Particles/粒子场、粒子规则
├─ Mod/             游戏适配层
│  ├─ Bootstrap/     入口、读档、场景、每帧宿主
│  ├─ Common/        游戏适配工具、嵌入资源、序列化
│  ├─ Content/       items/sprites/tooltip 内容注册
│  ├─ DeviceServices/装置实例、绑定、音频、动画
│  ├─ DeviceState/   Tag ↔ 装置状态适配器
│  ├─ GridBuilding/  网格、生命周期、FlowRuntime
│  ├─ ItemSpawn/     物品创建、attachment、工厂
│  ├─ Portraits/     人物立绘注册
│  ├─ TagMapping/    tag 常量、库存读写
│  └─ Visual/        流源视觉层
├─ Events/           事件层
│  ├─ Public/        公共事件 API
│  ├─ Runtime/       事件会话、协程宿主
│  ├─ Demos/         演示事件
│  ├─ Placement/     演示摆位
│  └─ Locks/         网格锁
├─ Story/            剧情层
│  ├─ Runtime/       通用剧情运行时
│  └─ Npcs/          具体 NPC 剧情
├─ Traits/           官方 StartingPerk 特质层
└─ Presentation/     见上
```

## 5. 物品与装置机制

### 5.1 物品创建流程

```text
ModItemDirectory 初始化
→ ItemSetup.Create(def)
→ ItemDirectory.CreateEmptyItem
→ 按 items.json 顺序执行 attachments
→ ItemFactory.Apply（通用属性 / tag）
→ 返回 GameItem
```

关键文件：

```text
src/Mod/ItemSpawn/Setup/ItemSetup.cs
src/Mod/ItemSpawn/Services/Factory/ItemFactory.cs
src/Mod/ItemSpawn/Services/Attachments/Base/ItemAttachment.cs
assets/items.json
```

硬性规则：

```text
attachment 先执行，factory 后执行
attachment 不要依赖 factory 写入的通用字段
attachment 必须无实例字段、无静态可变状态
新增 attachment = 写一个 public sealed ItemAttachment 子类
```

### 5.2 当前 attachments

```text
device          写装置身份 tag + anchor
flow_container  液体容器初始化
water_bottle    水瓶初始化
emitter         激活菜单 + 发射；读取 activateSound
```

### 5.3 装置绑定

```text
DeviceAttachment 写 device tag / anchorX / anchorY
→ ItemDeviceBinding 在物品进入网格或读档时创建装置实例
→ DeviceRegistry 保存 GameItem ↔ 装置实例
→ DeviceHost 广播朝向 / 配置
→ DeviceStateAdapter 负责 tag ↔ 装置状态
```

关键文件：

```text
src/Mod/ItemSpawn/Services/Attachments/Device/DeviceAttachment.cs
src/Mod/DeviceServices/Core/ItemDeviceBinding.cs
src/Mod/DeviceServices/Core/DeviceRegistry.cs
src/Mod/DeviceState/Adapters/*.cs
src/Mod/DeviceState/Catalog/DeviceAdapterCatalog.cs
```

新增装置：

```text
1. Logic 写装置类
2. DeviceState 写 [DeviceAdapter("key")] 适配器
3. assets/items.json 声明 device / anchor / attachments
4. assets/sprites.json + mod/Textures/Items 配图标
5. 测试补适配器与行为
```

## 6. 网格与流源机制

### 6.1 网格生命周期

```text
GridCreatedPatch
→ GridRegistry 建 FlowRuntime
→ FlowRuntime = FlowSpawner + FlowManager + FlowVisualLayer + FlowGridAdapter

ItemAttached / ItemDetached / ItemDropped / ItemPickedUp
→ 维护网格登记

InventorySorted / 读档
→ 重建物品格表
```

关键文件：

```text
src/Mod/GridBuilding/Lifecycle/*.cs
src/Mod/GridBuilding/Services/GridRegistry.cs
src/Mod/GridBuilding/Services/FlowRuntime.cs
src/Mod/GridBuilding/Services/FlowGridAdapter.cs
src/Mod/GridBuilding/Core/FlowDriver.cs
```

### 6.2 流源生命周期

```text
FlowSpawner.Accept → OnEntityAdded
FlowManager.Tick   → 移动 / 装置碰撞 / 反应
FlowSpawner.Kill   → OnEntityRemoved
```

流源死亡条件：

```text
走出网格
SubstanceTotal <= 0（液体被反应消耗完）
Collector 吸收
Split 多出口时旧流源被替换
```

关键文件：

```text
src/Logic/Flow/FlowEntity.cs
src/Logic/Motion/FlowSpawner.cs
src/Logic/Motion/FlowManager.cs
src/Logic/Motion/IFlowGrid.cs
```

### 6.3 移动与装置碰撞

```text
FlowManager.MoveFlowEntity
→ 本帧路径逐格探测
→ reactor.TryReact 先执行
→ router.Route 后执行
→ blocked 时 StopAt 装置格中心
```

格中心坐标：

```text
整数 = 格中心
```

转向装置在单出口变更方向时写入 `StopOffsetX/Y`，范围 `0~0.1` 格。  
必须保持非负，否则 `Floor` 会落到相邻格。

### 6.4 反应

```text
FlowReactionSolver 静态扫描全部 FlowReactionRule
每次 NextReactionTime 到期执行一轮
规则按 Order 排序
```

关键文件：

```text
src/Logic/Reactions/Realtime/Core/
src/Logic/Reactions/Realtime/Rules/01..23_*.cs
src/Logic/Reactions/Formulas/
```

属性系统：

```text
36 种液体 + 温度
FlowPropertyRegistry 定义顺序 / 分合规则
FlowProperties 是纯数据
```

## 7. 表现层机制

```text
FlowVisualLayer
→ 每网格一个视图
→ 12fps 动效时钟
→ FlowAtlasLayer 绘制本体
→ FlowParticleSystem 绘制每源粒子
```

关键文件：

```text
src/Mod/Visual/Core/FlowVisualLayer.cs
src/Mod/Visual/Internal/FlowAtlasLayer.cs
src/Mod/Visual/Internal/FlowParticleRenderer.cs
src/Presentation/Flow/Visual/*
src/Presentation/Flow/Particles/*
```

已知上限：

```text
FlowAtlasLayer 本体池：64
FlowParticleRenderer 粒子池：512
```

流源超过 64 时，超出的流源逻辑仍存在，但不会被绘制。  
如果出现“部分流源消失”，先检查这里。

## 8. 事件层机制

```text
FlowEventApi.RunXxx(grid) 启动事件
→ EventSession 管理物品 / cleanup / 协程
→ session.Complete / Fail
→ EventSession.End 清理并触发 FlowEventApi.EventCompleted(grid)
```

关键文件：

```text
src/Events/Public/FlowEventApi.cs
src/Events/Runtime/EventSession.cs
src/Events/Runtime/EventCoroutineHost.cs
src/Events/Demos/*.cs
```

事件完成由事件自己负责：

```text
必须调用 session.Complete()
或 session.CompleteAfter(seconds)
或 session.Fail(reason)
```

Story / 外部等待只监听 `EventCompleted`。  
事件协程不结束时，等待方会一直挂起。

## 9. Story 层机制

完整设计见：

```text
src/Story/README.md
```

核心模型：

```text
StoryAssembly      装配类：Entry + Complete
NpcStoryAssembly   增加 Phase + CanPlay
StoryPhase         Arrival / SecondVisit / GenericVisit / SoldOut

StoryNode          只保存玩家选项，没有 NPC 文本
StoryEdge          一轮命令注册

StoryPlayer        统筹：注册 → 执行 → 等结束 → 下一个节点
StorySequence      装配对话链、挂事件、等待结束
StoryNodeDialogue  节点 → 原版 Dialogue 选项
```

边只注册：

```csharp
internal override Task Execute(StorySequence player)
{
    player.SayNpc("...");
    player.StartFlow(...);
    player.Do(delegate { ... });
    return Task.CompletedTask;
}
```

禁止在边里：

```text
await StartFlow
await WaitForSpeech
手动 ContinueConversation
```

事件边最后一句会在 Flow 完成后播放，然后挂节点选项。  
普通边最后一句直接挂选项，不重建对话。

## 10. 奥雅 / FlowEmitterMerchant

NPC：

```text
显示名：奥雅
标识：fluxforge_flow_emitter_demo
到访间隔：7 天
人物立绘：fluxforge_saleswoman
```

剧情阶段：

```text
Arrival       第一次到访：介绍、演示、首次库存、离场赠礼
SecondVisit   第二次到访：冷萃装置
GenericVisit  无专用剧情时的普通招呼
SoldOut       首次库存卖完后的赠礼
```

到访流程：

```text
StartStory
→ IncrementVisitCount
→ 写下次到访日
→ TryPlay(Arrival)
→ TryPlay(SecondVisit)
→ TryPlay(GenericVisit)
→ OpenShop
```

关键文件：

```text
src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantNpc.cs
src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantPlan.cs
src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantProgress.cs
src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantStock.cs
src/Story/Npcs/FlowEmitterMerchant/Events/*
```

### 剧情标识

```text
ArrivalSeenKey    第一次剧情已播放
FirstStockKey     首次破旧库存已上架
FirstStoryKey     第一次剧情已完成
SecondVisitKey    第二次剧情已完成
FarewellKey       赠礼已完成
VisitCountKey     到访次数
ThirdVisitKey     第三次到访
```

演示/提示：

```text
EmitterDemoKey
FilterDemoKey
ColdTrapDemoKey
SoundHintKey
```

库存规则：

```text
FirstStoryKey  → 发射器 ×2 + 过滤装置 ×1
SecondVisitKey → 冷萃装置 ×1
ThirdVisitKey  → 变速装置 ×2 + L 型转弯 ×2 + 直行装置 ×4
```

首次库存独立使用 `FirstStockKey`，且首次货物带“破旧”。

## 11. 人物立绘机制

设计文档：

```text
src/Story/Npcs/FlowEmitterMerchant/PORTRAIT.md
```

实现：

```text
src/Mod/Portraits/NpcPortraitRegistry.cs
```

链路：

```text
Textures/npc_saleswoman.png
→ LoadedMod.Textures
→ 补透明边到原版比例
→ Sprite.Create
→ SpriteDict.spriteDictionary["fluxforge_saleswoman"]
→ StoreClient.spriteName = "fluxforge_saleswoman"
```

当前参数：

```text
原图：640 × 912
PortraitScale = 0.76
画布：约 1638 × 1200
PPU ≈ 568.75
世界尺寸：2.88 × 2.11
人物高度：约 1.60
```

规则：

```text
不裁人物
不拉伸
不降像素
底部对齐
水平居中
不超出交易窗口
```

`SpriteDict.Awake` 后注册；`OnGameLoaded` 再兜底一次。

## 12. 音效机制

物品激活音效：

```text
items.json activateSound
→ EmitterAttachment 写入 item.soundActivate
→ 原版 ActivateSlotItem 播放
```

当前发射器：

```json
"activateSound": "fluxforge.emitter.fire"
```

自定义音效：

```text
assets/sfx_emitter_fire.wav
→ DeviceAudioClips 嵌入并 AudioDirectory.RegisterModClip
```

官方 ModLoader 也支持：

```text
Sounds/*.wav|ogg|mp3
→ 自动注册为 mod 音效标识
```

## 13. 文件索引

### 根目录

```text
FluxForge.csproj      项目、引用、资源复制规则
manifest.xml          Mod 清单
README.md             人类向安装/开发说明
AI_CONTEXT.md         本文件
assets/               源数据与嵌入资源
mod/                  构建时复制到运行时的资源
src/                  源码
tests/                单元测试
```

### Logic

```text
src/Logic/Flow/FlowEntity.cs
src/Logic/Flow/FlowProperties.cs
src/Logic/Flow/FlowPropertyRegistry.cs
src/Logic/Flow/FlowProps.cs
src/Logic/Flow/FlowMotionData.cs
src/Logic/Flow/FlowPos.cs

src/Logic/Motion/FlowSpawner.cs
src/Logic/Motion/FlowManager.cs
src/Logic/Motion/IFlowGrid.cs

src/Logic/Devices/Interfaces/
src/Logic/Devices/Reaction/Filter/
src/Logic/Devices/Reaction/IceBlock/
src/Logic/Devices/Reaction/ColdTrap/
src/Logic/Devices/Reaction/Temperature/
src/Logic/Devices/Routing/Collector/
src/Logic/Devices/Routing/Split/Core/
src/Logic/Devices/Routing/Split/Turn/
src/Logic/Devices/Routing/Split/LRightDown/
src/Logic/Devices/Routing/Split/TJunction/
src/Logic/Devices/Routing/Split/DoubleEmit/
src/Logic/Devices/Routing/StraightSpeed/

src/Logic/Reactions/Realtime/Core/
src/Logic/Reactions/Realtime/Rules/
src/Logic/Reactions/Formulas/
```

### Presentation

```text
src/Presentation/Flow/Visual/FlowFrameInput.cs
src/Presentation/Flow/Visual/FlowVisualState.cs
src/Presentation/Flow/Visual/FlowTint.cs
src/Presentation/Flow/Visual/FlowLiquidPalette.cs
src/Presentation/Flow/Visual/FlowPixelMath.cs
src/Presentation/Flow/Visual/FlowAtlasIndex.cs

src/Presentation/Flow/Particles/FlowParticleSystem.cs
src/Presentation/Flow/Particles/FlowAmbientField.cs
src/Presentation/Flow/Particles/FlowParticleRules.cs
src/Presentation/Flow/Particles/FlowParticleProfile.cs
```

### Mod

```text
src/Mod/Bootstrap/FFModEntry.cs
src/Mod/Bootstrap/ModHost.cs
src/Mod/Bootstrap/SaveLoaded.cs
src/Mod/Bootstrap/SaveExited.cs

src/Mod/Content/Items/FlowItemRegistry.cs
src/Mod/Content/Sprites/FlowSpriteRegistry.cs
src/Mod/Content/Sprites/LoadFromAtlasPatch.cs
src/Mod/Content/Tooltip/*

src/Mod/ItemSpawn/Setup/ItemSetup.cs
src/Mod/ItemSpawn/Services/Factory/ItemFactory.cs
src/Mod/ItemSpawn/Services/Attachments/*

src/Mod/DeviceServices/Core/DeviceRegistry.cs
src/Mod/DeviceServices/Core/ItemDeviceBinding.cs
src/Mod/DeviceServices/Core/DeviceHost.cs
src/Mod/DeviceServices/Audio/DeviceAudioClips.cs
src/Mod/DeviceServices/Audio/DeviceAudioService.cs
src/Mod/DeviceServices/Animation/DeviceAnimationService.cs

src/Mod/DeviceState/Catalog/DeviceAdapterCatalog.cs
src/Mod/DeviceState/Adapters/*.cs
src/Mod/DeviceState/Sync/*.cs

src/Mod/GridBuilding/Core/FlowDriver.cs
src/Mod/GridBuilding/Services/GridRegistry.cs
src/Mod/GridBuilding/Services/FlowRuntime.cs
src/Mod/GridBuilding/Services/FlowGridAdapter.cs
src/Mod/GridBuilding/Lifecycle/*.cs

src/Mod/Portraits/NpcPortraitRegistry.cs
src/Mod/TagMapping/**/*.cs
src/Mod/Visual/**/*.cs
```

### Events

```text
src/Events/Public/FlowEventApi.cs
src/Events/Public/FlowEventApi.Relay.cs
src/Events/Runtime/EventSession.cs
src/Events/Runtime/EventCoroutineHost.cs
src/Events/Demos/BoundaryEmitterEvent.cs
src/Events/Demos/TwoEmitterRelayEvent.cs
src/Events/Demos/FilterLoopEvent.cs
src/Events/Demos/ColdTrapLoopEvent.cs
src/Events/Placement/DemoPlacement.cs
src/Events/Locks/GridLock.cs
```

### Story

```text
src/Story/Runtime/StoryPlayer.cs
src/Story/Runtime/Contracts/*
src/Story/Runtime/Graph/*
src/Story/Runtime/Execution/StorySequence.cs
src/Story/Runtime/Dialogue/StoryNodeDialogue.cs

src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantNpc.cs
src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantPlan.cs
src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantProgress.cs
src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantStock.cs
src/Story/Npcs/FlowEmitterMerchant/Events/*
```

### Tests

```text
tests/FluxForge.Tests/
  Flow*.cs
  Device*.cs
  Reaction*.cs
  SplitVariantTests.cs
  TurnDeviceTests.cs
  ColdTrap*Tests.cs
  FlowAtlasTests.cs
  FlowParticle*Tests.cs
  GameStubs.cs
  TestFlow.cs
```

### Assets / runtime resources

```text
assets/items.json         物品定义
assets/sprites.json       物品贴图定义
assets/flow_atlas.json    流源图集索引
assets/flow_mask*.png     流源蒙版
assets/sfx_*.wav          设备音效
assets/推销员.png         人物立绘源文件

mod/Textures/Items/*.png  物品图标
mod/Textures/npc_saleswoman.png  人物立绘运行时资源
```

## 14. 常见坑

### 物品

```text
attachment 不能依赖 factory 写入的字段
tag 必须最后写，否则会被 attachment 覆盖
读档时 AddParent 早于最终 shape，批量后要 Rebuild
```

### 装置

```text
同一物品声明两个装置 → 后者覆盖
存档状态在 tag，不在装置实例
适配器负责 tag ↔ 装置状态，不要把状态藏到静态字段
```

### 流源

```text
格中心是整数
转向偏移必须非负，否则 Floor 落相邻格
Split 多出口会 Kill 旧流源并生成新流源
Collector 吸收会 Kill 流源
反应消耗完 SubstanceTotal 会 Kill
走出网格会 Kill
本体渲染最多 64，粒子最多 512
```

### 事件

```text
事件不 Complete，等待方永远不结束
EventCompleted 是按网格的全局静态事件
EventSession 负责清理，不要在事件外手动清理物品
```

### Story

```text
边只注册，不 await
节点不持有 NPC 文本
FirstStoryKey 不等于 FarewellKey
库存不要用演示 key，使用剧情 key
StorySequence 控制对话链，不要手动 ContinueConversation
```

### 立绘

```text
放 Textures/ 根目录，不是 Textures/Items
SpriteDict.Awake 后注册，场景重建会重建字典
原图比例不等于原版时用透明补边，不裁不拉伸
```

## 15. 当前已知状态

```text
单元测试：339 通过
测试 trait 已删除，TraitLayer 仍保留自动发现框架
Story runtime 已拆分为 Contracts / Graph / Execution / Dialogue
FlowEmitterMerchant 已有第一/第二/通用/赠礼四段内容
奥雅立绘和发射器激活音效已接入
```

需要游戏内验证的内容：

```text
Harmony 生命周期
人物立绘实际显示
读档后的装置与剧情标识
事件协程与视觉层联动
```
