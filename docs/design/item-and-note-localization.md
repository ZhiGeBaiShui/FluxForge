# 物品与奥雅便条中英文接入

日期：2026-11-17。规范依据见[native-localization-audit.md](native-localization-audit.md)。

## 当前改动

- items.json的name/desc/flavor改为nameLoc/descriptionLoc/flavorLoc；不更改identifier、tag、配方和剧情数值。
- ItemFactory创建时调用原版ModHelper.GetLocalized，CSV外置于Localization/zh.csv和en.csv，包含原有中文与对应英文。没有自造语言检测、翻译缓存或回退分支。
- 状态行label改为labelLoc；类型名和库存/收集量显示使用相同CSV。没有更改状态行实际读取的tag和数值格式。
- 剧情生成的受污染原料、医院酸碱、塞纳普斯原料名称使用同一接口。
- DeviceManual删除ChineseManualText硬编码，已有七页文本均有中英文键。
- 便条仍由ItemSetup注册原ID fluxforge_starter_note。Books/starter_note.json没有item字段，原版BookRegistry只加载版式，不重复注册物品。
- 中文整张PNG保留为历史源图，但已取消CopyToOutput规则并不再被阅读逻辑引用。原生图文版式直接使用现有装置图像、箭头和本地化文字。

## 每一处新增运行行为的原版依据

| 行为 | 原版依据 |
|---|---|
| 按当前语言创建物品名称与说明 | FoodItemDirectory.EmptyBeerBottle；PaperBookRegistry.CreateItem中的nameLoc/descriptionLoc |
| 按原语言保留旧存档文字 | SaveManager.DecodeNodesInternal恢复name/shortDescription/flavorText；不加旧文本强制迁移 |
| 字体与文本取当前语言 | ModHelper.GetLocalized；DeviceManual现有FontLoader入口 |
| JSON便条自动加载 | ModLoader先LoadLocalization，再PaperBookRegistry.LoadMod |
| 使用loc文字、现有图片和箭头 | PaperUI.ResolveText、BuildImage、BuildLine及PaperElementData |
| 发射器朝向回调 | PaperBookRegistry.OnOpened/FireOpened，含原生Rebuilt后的回调；PaperUI.Get返回对应Image |
| 关闭便条 | 原有退出/卸载入口改为调用PaperBookRegistry.Close，仅关闭本便条 |

没有新增防御性判断、重试、异常吞噬、存档修复或中断恢复。运行时代码保留既有可选说明/风味字段判断；配置中确实存在没有flavorLoc的物品。

## 验证与范围

原版PaperJson实际解析JSON成功，原版ModLoader.ParseCsvValue实际解析336行成功。新增测试检查中英文键和参数一致、英文无中文、所有物品及状态行有译文、便条无重复ID和重复item注册、医院原料名称随语言变化而成分不变。

现已覆盖剧情对白、选项、NPC姓名、声望perk、扫描仪、成型机及模组介绍：中英文各389个键，171条原中文对白逐字保留。192项剧情及517项逻辑测试通过。原版CSV解析器接受778条记录，原版便条JSON解析通过。游戏界面尚未实测；没有部署或重打此前的正式发布包。

## 加载与语言切换依据

原版ModLoader.LoadAllMods先调用OnEnable，再执行LoadModAssets；后者先加载Localization，声音加载协程结束后FireOnModAssetsLoaded。因此模组介绍和类型名的首次翻译放在OnModAssetsLoaded，不在OnEnable直接读取。SelectedLocaleChanged更新当前语言的类型表、医院perk表及已注册医院阵营名。医院签约前不存在阵营（HospitalReputation.EnsureFaction），因此切语言时不创建阵营。StoreReputation.GetFactionNameByID读取factionDisplay，读档恢复医院时同样重设显示名。未启用模组无法执行回调，manifest保留英文后备描述。

剧情常量只保存键。StoryEdge在菜单显示时解析标签，台词在执行时解析；医院路人对白在创建顾客时解析。无需新增翻译缓存、失败重试或存档修复。中文日志、异常信息和代码注释属于开发诊断，不接入游戏文本表。
