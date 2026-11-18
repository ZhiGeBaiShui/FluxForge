# FluxForge 双语适配交接上下文

更新时间：2026-11-17。可将本文全文提供给其他 AI，作为继续开发的上下文。

## 项目及当前状态

- 项目：`E:\code\FluxForge`；PowerShell；《Probably Stolen》Mono 0.46；C# / Unity。
- 原版程序集：`E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed`。
- 原版反编译取证：项目下 `.tmp-decomp/`（被 Git 忽略，仅当前工作区存在）。
- 远程：`https://github.com/ZhiGeBaiShui/FluxForge.git`，分支 `master`。
- 本轮实现已随提交 `64d5eda` 推送。该提交同时包含此前积累的医院主线、剧情框架、资源和文档，不仅是翻译。
- 中文、英文各 389 个对应键；171 条原中文对白逐字核对保留。剧情分支、物品 ID、存档键与任务数值不因翻译更改。
- Release 已部署到现有创意工坊安装位置，用户准备进行游戏内测试；尚未收到实际测试结果。

## 用户要求与不可擅自扩展的范围

1. 所有玩家可见文本使用外置 CSV，包括剧情对白、选项、姓名、物品、状态行、声望奖励、扫描仪、成型机、手册及便条。
2. 完全沿用原版语言选择与本地化接口；不自建语言检测、翻译缓存或回退机制。
3. 所有防御性代码必须有实际代码证据，不能推测异常再补兜底、重试、吞异常或兼容分支。
4. NPC 本次到访视为原子行为；不为异常中断设计待结算分数持久化或恢复。
5. 尽量采用普通类、方法与条件判断，少用高级语法；遵守已有逻辑层、适配层和剧情层边界。
6. 中文代码注释、开发日志、异常诊断不是玩家界面，不为翻译将游戏依赖引入纯逻辑/表现计算层。
7. 不因后续改译文擅自修改任务设计、重新发布工坊、重打旧正式包或迁移存档。

## 原版规范

源文件为 `mod/Localization/zh.csv`、`mod/Localization/en.csv`。发布后目录为：

```text
FluxForge.dll
manifest.xml
preview.png
Localization/zh.csv
Localization/en.csv
Books/starter_note.json
Textures/...
AssetBundles/fluxforge_device_manual
```

CSV 与 DLL 同属一个模组目录，但 CSV 放在 `Localization/` 子目录，不嵌入 DLL。中文 locale 是 **zh**，不是 zh-CN。

```csv
story.example.greeting,"你好，老板。"
story.example.progress,"目前进度：{0}/{1}。"
```

每条记录占一个物理行：键不加引号，值加双引号；值内双引号写两次；换行用字面量 `\n`，不能写真实跨行字段。中英文键、格式参数与富文本标签保持一致。原版按首个逗号分割，键只 Trim，不会剥除键上的引号，因此不要用 QUOTE_ALL 重写整表。

```csharp
ModHelper.GetLocalized("fluxforge", "story.example.greeting");
ModHelper.GetLocalized("fluxforge", "story.example.progress", current, total);
```

原版 `ModHelper.GetLocalized` 负责当前语言及英文/键回退。原版自有物品名继续用其 `LocHelper.GetLocalizedItem` 等接口，不复制原版词典。

## 加载时机与语言切换

原版 `ModLoader.LoadAllMods` 在 `LoadModAssets` 前调用 OnEnable；`LoadModAssets` 才加载本地化、纸张等资源，声音协程结束后广播 `ModHook.OnModAssetsLoaded`。不能在 OnEnable 直接解析并永久保存 CSV 文本。

- `src/ItemIntegration/Content/Localization/ModMetadataLocalization.cs`：OnEnable 只订阅资源就绪事件；筛选本模组后设置 manifest 描述与类型文本，并订阅 SelectedLocaleChanged；卸载退订。
- `src/LogicIntegration/Bootstrap/FFModEntry.cs`：接入上述初始化和卸载，保留既有读档类型注册。
- `src/Story/Runtime/Bootstrap/StoryBootstrap.cs`：切语言更新已存在医院阵营的名称及当前语言 perk 表。签约前医院阵营不存在，不因切语言提前创建。
- `HospitalReputation.EnsureFaction`：恢复已有阵营时同样更新 `factionDisplay`；原版 `StoreReputation.GetFactionNameByID` 直接读取该字段。
- 禁用的模组不能执行回调，根 manifest 描述以英文作后备；已启用模组的描述来自 CSV。

## 各模块实现位置

### 物品与状态

- `assets/items.json` 使用 `nameLoc`、`descriptionLoc`、`flavorLoc`，状态行使用 `labelLoc`，保留原 identifier/tag/配方。
- `src/ItemIntegration/Content/Items/FlowItemRegistry.cs`：读取配置。
- `src/ItemIntegration/ItemSpawn/Services/Factory/ItemFactory.cs`：创建物品时调用原版本地化。
- `src/ItemIntegration/Content/Items/ItemTypeLocalization.cs`：将翻译写入原版类型表。
- `src/ItemIntegration/Content/Tooltip/`：按 key 解析状态标签。
- 医院原料、散货库存及塞纳普斯原料的动态物品名称也通过 CSV。
- **旧物品不全局重翻译**：原版物品创建时翻译，SaveManager 保存并恢复最终 name/shortDescription/flavorText。不要增加扫描库存或强制改写旧存档文字。

### 剧情

- `src/Story/**/*Texts.cs` 的对白常量保存 CSV **键**，不是中文文字。技术 ID（如 PerkId）仍是 ID，不参与翻译。
- 台词在边的 Execute 中调用 `ModHelper.GetLocalized`，再传给 `player.SayNpc`。
- `src/Story/Runtime/Graph/StoryEdge.cs` 保存 `_labelKey`，Label 属性在菜单构建时解析。入口/阶段边没有选项，null 标签是正常模型。
- 所有选项构造函数及转发参数传 **键**，不能传提前翻译的字符串，避免重复翻译或启动时缓存语言。
- `StoryDialogueAdapter.AttachOptions` 每次建菜单仅解析一次标签，再用于原版的相关文字字段。
- `AcidBuyerHighPurityAnswerEdge` 保存 `_responseKey`，执行时解析回答，避免建图时缓存。
- `NpcDefinition` 保存姓名键，DisplayName 访问时解析；薇拉和奥雅分别用 `npc.vera.name`、`npc.oya.name`。
- `HospitalVisitorTexts.Visitor01..04` 改为属性，在生成医院路人对白时解析，不使用静态已翻译数组。
- 合同价目便条的标题、正文、作者、物品名和说明均来自 CSV。
- `HospitalReputationTexts` 保留原 perk ID，通过 CSV 填充原版 RepPerkTable。
- 新剧情写法见 `src/Story/README.md`、`src/Story/Runtime/Graph/README.md`。

### 界面与手册

- `src/ItemIntegration/Content/Scanner/FlowScannerText.cs` 及两个扫描仪补丁：标题、外来液体、温度、合计、成分分隔格式本地化。
- `src/ItemIntegration/ItemSpawn/Services/Attachments/ChemicalFormer/`：提示、选配方、材料条件本地化；原版产物名取原版 key。
- `src/Manual/Books/DeviceManual/DeviceManual.cs`：移除中文直读入口，统一原版 helper；已有七页均有双语。

### 奥雅第一张便条

- `mod/Books/starter_note.json`：原版图文纸张格式，一页 22 个节点，文字节点使用 `loc`；使用现有装置图和箭头，不再显示烘焙中文的整张 PNG。
- JSON **没有 item 字段**：物品仍由原 ItemSetup 注册原 ID，避免 PaperBookRegistry 重复注册。
- `src/Manual/Shared/PaperNoteContent.cs`：`PaperBookRegistry.Open/Close("fluxforge:starter_note")`；OnOpened 回调设置两台发射器图的中心和朝向，重建后也会执行。
- 旧纸张 PNG 留作历史源图，但不再被阅读逻辑和 csproj 复制规则引用。

## 验证结果与命令

以下结果在当前实现完成后已实际通过：

```powershell
dotnet build FluxForge.csproj -c Release --nologo --verbosity quiet
dotnet test tests/FluxForge.Story.Tests --nologo --verbosity quiet
dotnet test tests/FluxForge.Tests --nologo --verbosity quiet
python tools/build_story_review.py
```

- 生产构建：0 警告、0 错误。
- 剧情测试：192 通过；逻辑测试：517 通过。
- 用原版 `ModLoader.ParseCsvValue` 反射调用解析中英文合计 778 条，检查键格式及重复键，并核实示例对白结果。
- 用原版 `PaperJson.ParseBook` 实际解析便条成功。
- 验证双语键一致、占位参数和富文本标签一致、英文无残留中文、物品和状态配置有译文、便条无重复注册、原料文字改变但成分不变。
- 完整医院端到端流程按 zh/en 各跑一次；验证图在中文创建、英文显示时的选项和姓名。
- 原中文 171 条与迁移前提取结果逐字一致；输出取证在被忽略的 `output/`，不是远程仓库交付文件。
- **未做游戏内交互、字体、布局或实时切语言实测**。不要把单元测试当成这些项目已验收。

测试边界：剧情测试使用 GameBoundaryStubs 和 NativeLocalizationData 读取实际 CSV；纯规则测试新加 LocalizationStub，仅返回 key，不负责验证翻译。不要把测试桩作为生产实现。

## 当前部署

安装位置：`E:\SteamLibrary\steamapps\workshop\content\4348910\3840844438`。本地 Mods 下没有另加一份，避免重复 ID。

2026-11-17 已复制 Release DLL、manifest、封面及当前 mod 下的 Localization/Books/Textures/AssetBundles，31 个文件与源文件哈希一致。

部署前备份：`E:\code\FluxForge\output\deployment-backups\20261117-120224\FluxForge`。

删除旧 `Localization/zh-CN.csv` 的命令被工具自动审批以 `blocked by policy` 拒绝（未给更具体理由）；之后改为只复制更新，部署成功。**旧 zh-CN.csv 仍在安装目录**，新版使用 zh.csv；不要声称旧文件已清除，也不要绕过工具限制删除。

用户需要重启游戏加载新 DLL。既有 ZIP、dist 正式目录和 VDF **没有因这次双语部署重新打包或上传**；它们不能视为最新双语发版内容。

## 文档与后续工作

- 原版规范证据：`docs/design/native-localization-audit.md`。
- 实现说明：`docs/design/item-and-note-localization.md`。
- 项目索引：`AI_CONTEXT.md`、`docs/current-project-state.md`。
- 中文对白清单：`docs/design/story-v5-all-text.md`。
- `tools/build_story_review.py` 已从中文 CSV 解析文本键，生成 `dist/FluxForge-story-review.html`（98 节点 / 18 章节，独立文件，主要用于中文剧情审阅）。
- 首要下一步是接收用户游戏内双语测试反馈，按实际问题修正。重点检查手册/便条布局与字体、菜单选项、NPC姓名、声望面板、模组描述、读档及切语言行为。
- 用户要求正式双语发布时，必须重新 Release 构建并从当前输出生成正式包，确认 Localization 和 Books 完整；不要复用旧 ZIP 假装已更新。
- 本交接文档创建于 `64d5eda` 之后，尚未再次提交 Git；后续以实际 git status 为准。
