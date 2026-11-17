# 原版本地化规范核对

日期：2026-11-17。依据本机Probably Stolen 0.46反编译源码 `.tmp-decomp/`；当前游戏程序集SHA256：`725cb999d019dff1ff31bed353c7f9bb8d81a7605a15de1f80f5a3a6cf7b1d05`。本记录区分原版事实与Mod整改，不代表本轮已经实现英文版。

## 原版事实

1. 简体中文语言码为 `zh`，英文为 `en`。`MainMenuUIController.LOCALE_DROPDOWN` 明确列出。原版ModHelper精确匹配locale文件名；当前 `zh-CN.csv` 不能直接当作 `zh.csv` 使用。
2. 首次启动由 `LocHelper.ResolveStartupLocale` 决定：已有设置优先；没有设置才检测Steam/系统语言，最后英文。Mod只消费当前游戏语言，不再自行检测Steam或操作系统。
3. 主菜单设置语言后保存 `locale`，经 `LocHelper.SetLocale` 等待初始化后赋值 `LocalizationSettings.SelectedLocale`。
4. Mod文本使用 `ModHelper.GetLocalized(modId,key,args)`；查当前locale，不存在则查en；仍无结果记录警告并返回key，带参数时用string.Format。沿用它的全部行为，不另写中文分支、缓存、回退链或异常吞噬。
5. ModLoader按Localization下CSV文件名登记locale；解析按物理行与首个逗号进行，不能直接当作支持任意多行单元格的标准CSV库。翻译文件按现有ParseCsvValue约定编码换行与引号。
6. 原版物品工厂创建时解析名称与描述。SaveManager保存name/shortDescription/flavorText，读档重新创建物品后又恢复存档文本。RelocalizeIfMissing只修复缺失翻译标记，不把普通中文重新翻译。跨语言读档有原版提示；原版没有全库存自动重翻译机制。
7. 原版剧情在构造Dialogue时通过LocHelper读取名称、台词与选项。Mod可用官方ModHelper读取自有CSV；不要把第一次创建的译文作为永久静态常量或全局翻译缓存。
8. 静态Unity文本通过LocalizeStringEvent更新内容，TMPLocalizer按SelectedLocaleChanged选择FontLoader字体。其他动态UI在构建/打开时解析。不是所有类型都必须订阅全局语言变化。
9. 原版PaperUI支持text节点的loc键，Mod场景直接调用ModHelper.GetLocalized；PaperItemData有nameLoc/descriptionLoc；PaperBookRegistry.CreateItem按这些键生成便条物品。ModLoader先加载Localization，再加载Books。可复用原版图文节点，而不继续把中文固定到整张图片。
10. 原版ModUIManager直接显示manifest.Description，不支持按locale自动选择多份manifest描述。遵循原版时，此处应使用简短双语描述，不能宣称仅加en.csv就会翻译模组详情页。

## 对FluxForge的整改约定

- 统一使用 `Localization/zh.csv` 和 `Localization/en.csv`，中英文键集合一致。原中文先保留原句，不改剧情业务、物品ID或存档键。
- 配置名称/描述采用原版PaperItemData同类命名风格的 `nameLoc`、`descriptionLoc` 等键，ItemFactory按当前语言调用ModHelper。内部标识不翻译。
- 剧情文本与动态菜单在创建对话时取当前语言；需要调整现有StoryEdge.Label和NpcDefinition.DisplayName的构造期缓存位置，但不改变剧情图拓扑、任务计分或插入规则。
- 手册删除ChineseManualText和zh-CN直读；字体遵循原版FontLoader/TMPLocalizer；原版字符串表注入只处理确实由原版UI按表键读取的类型名、声望perk等内容。
- 奥雅第一张便条优先改为原版Books图文定义：不带字的示意图加loc文字节点，由原版PaperUI显示；保持现有入门内容和物品ID，核对注册顺序，不能与自有ItemSetup重复注册。
- Scanner、Tooltip、成型机提示、原料名、NPC/阵营显示名和声望文本全部纳入键清单，已有原版液体/物品文本继续用原版LocHelper，不复制一份翻译。
- 不新增全库存重命名、旧存档文本迁移、私有语言设置、每帧语言轮询、翻译缓存、异步重试或缺失翻译修复。遵循原版跨语言存档语义。
- 游戏内模组详情使用双语manifest简介；Steam工坊简介属于独立发布元数据，不与游戏内语言混为一谈。

## 最小验证

中文/英文分别从主菜单进入；新生成物品名称说明、NPC菜单对白、手册字体、便条、扫描器与声望面板正确。中英文CSV无缺键、占位参数相同。跨语言读旧档保留原版警告和存档文本恢复行为，不把旧物品残留原语言误判为本次遗漏，也不额外改写存档。

## 原版代码定位

| 文件与行号（相对.tmp-decomp） | 方法或语句 |
|---|---|
| `MainMenuUIController.cs:124` | `private static readonly (string code, string label)[] LOCALE_DROPDOWN` |
| `MainMenuUIController.cs:780` | `public void OnLocaleChanged` |
| `LocHelper.cs:276` | `public static string ResolveStartupLocale` |
| `LocHelper.cs:387` | `public static IEnumerator SetLocale` |
| `ModHelper.cs:91` | `public static string GetLocalized` |
| `ModLoader.cs:1359` | `private void LoadLocalization` |
| `FoodItemDirectory.cs:154` | `private GameItem EmptyBeerBottle` |
| `SaveManager.cs:59` | `saveItemNode.name =` |
| `SaveManager.cs:153` | `private static GameItem DecodeNodesInternal` |
| `LocHelper.cs:228` | `public static string RelocalizeIfMissing` |
| `LoadGameUI.cs:562` | `string currentLocale =` |
| `TMPLocalizer.cs:78` | `protected virtual void OnEnable` |
| `StoreClientDialogList.cs:134` | `public static Dialogue MentorAfterhoursDialog` |
| `PaperElementData.cs:28` | `public string loc;` |
| `PaperUI.cs:840` | `public string ResolveText` |
| `PaperBookRegistry.cs:685` | `public static GameItem CreateItem` |
| `ModUIManager.cs:627` | `SetText(manifest.Description)` |
