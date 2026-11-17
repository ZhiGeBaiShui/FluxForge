# FluxForge — 开发上下文

更新：2026-11-16。先读[当前状态](docs/current-project-state.md)，再读具体层的README。

## 环境

- Probably Stolen 0.46，Unity 2021.3，Mono，官方ModLoader与Harmony。
- 工作目录 `E:/code/FluxForge`；Unity手册工程 `E:/code/ProbablyMod`。
- 游戏Managed目录由 `FluxForge.csproj` 的 `GameManagedDir` 指定。
- 生产与两套测试命令见 [README](README.md)。不把桩测试称为游戏内验证。

## 维护约束

- 不覆盖工作区已有的无关改动。
- 防御判断必须有实际代码证据；不为假想损坏存档、重复启动、中断恢复新增分支。
- NPC到访是原子业务。本次交易分数只在内存；跨次任务进度才使用现有modData。异常中断直接丢弃本次数据。
- 优先普通类、具名方法、if/for/foreach；避免不必要高级语法、包装与抽象。
- 不擅自增加台词、奖励或剧情。奥雅已有入门剧情，但医院之后的黑针配方与原液合成路线未实现。
- 不自动杀游戏；部署遇DLL占用就报告。发布只复制构建产物，不打包工作目录。
- 小键盘9声望调试已经删除，不要重新加入。

## 目录与入口

| 层 | 入口 |
|---|---|
| 纯逻辑 | `src/Logic/README.md` |
| 纯表现计算 | `src/Presentation/` |
| 装置适配、生命周期与tag | `src/LogicIntegration/README.md` |
| 物品装配与资源 | `src/ItemIntegration/README.md` |
| NPC与剧情 | `src/Story/README.md`、`src/Story/Runtime/README.md` |
| 手册UI | `src/Manual/README.md` |
| 独立装置演示 | `src/Events/`、`docs/event-layer-design.md` |

Logic/Presentation不依赖Unity或Assembly-CSharp。剧情层只用自己的生命周期Patch。管理类的被调用服务放在各自子目录，遵守现有分层。

## 已实现业务

医院：固定送料/收货星期；20/40额度提示只插入送料；60任务有接拒选项；80采购访客；100原液提纯任务和外聘专家奖励。原液完成后周供和普通交易闭环，当前没有接取后放弃选项。

手册七页：概览、发射器、过滤、基础冷萃、进阶冷萃、自动化冷萃、离心。进阶仅右侧发射一次；自动化补入口转角后10秒结束。新页直接绑定用户构建的预制体，不再克隆基础页。

升温+50至160°C，降温−50最低0°C；冷萃接受入口/出口恰好等于阈值，当前入口0至5°C含端点可增效。环境温度规则依然生效。

演示图像改Simple以排除Tiled窄边，尚待游戏内确认；不要宣称视觉修复已实测。

## 构建与资源

唯一运行资源依据是csproj的显式EmbeddedResource、复制规则及配置引用。蒙版为 `flow_mask.json` 和四张mask PNG；12个正式WAV均被嵌入。人物PNG在 `mod/Textures/`，物品PNG在 `mod/Textures/Items/`。

旧候选、试听与压缩包已列入维护清单，批量删除被工具策略拒绝，尚未删除，历史脚本保留不代表其旧输入仍保留。源代码取证 `.tmp-decomp/*.cs` 保留；资源来源/授权文档保留。

全文台词见 `docs/design/story-v5-all-text.md`；`python tools/build_story_review.py` 可重新生成独立HTML。

## 2026-11-17 本地化接入

所有玩家可见文本（含NPC对白、选项、姓名、医院声望奖励、扫描仪、成型机、手册及便条）均使用外置Localization/zh.csv和en.csv，共389个对应键。中文locale为zh。原版ModHelper负责选语言及回退；选项保存键、展示时解析，不缓存启动时语言。原版物品存档保留最终文字，不额外重翻译旧物品。模组描述和类型文字在OnModAssetsLoaded后注册；未启用模组的manifest描述为英文。验证为517项逻辑、192项剧情测试通过，原版JSON/CSV解析通过；未游戏内实测，未部署或更新旧发布包。详见docs/design/item-and-note-localization.md。
