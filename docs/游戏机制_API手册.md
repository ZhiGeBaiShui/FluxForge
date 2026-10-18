# Probably Stolen 游戏机制 API 手册

> 来源：2026-10-09 ~ 10-17 的 mod 开发沉淀（ProbablyBagMod）
> 取证手段：ISIL 转储（`_isil_out/IsilDump`）+ ilspycmd 反编译（mod DLL / Il2Cpp 程序集）+ UnityPy 资源提取 + 游戏内只读探针
> 适用对象：给 Probably Stolen 做自定义物品 / 贴图 / UI 的 mod 开发者

---

## 0. 项目历史与结论

| 日期 | 事件 | 沉淀结论 |
|---|---|---|
| 10-09 | 网格系统迁移到独立类库 `ProbablyBagCore` | 47 项测试全绿；网格语义规格书可用 |
| 10-13 | 落地「流源传导」框架（运动层 / 反应层解耦） | 装置接口 `IFlowDevice` 同时决定「往哪出」和「带什么载荷」 |
| 10-14 | 代码清零，改由用户主导设计 | 保留 29 项设计决策，代码从 git 历史恢复 |
| 10-16 | 自定义物品生成层 | 目录注册 + `items.json` / `sprites.json` 双配置驱动 |
| 10-17 | **贴图链路攻关**（本文重点） | 从「16px 假设」到「ppu 才是真参数」，渲染层 patch 全部撤销 |

**10-17 的最终结论（一句话）**：
> 游戏把贴图按 **ppu 换算成「世界尺寸」** 再排进 UI；只要 `ppu = 贴图宽 ÷ (列数 × 0.16)`，图片尺寸、平铺格数、命中判定会同时自动正确，**不需要任何渲染层补丁**。

---

## 1. 游戏机制总览

### 1.1 三层结构

```
数据层  GameItem
        ├─ identifier / name / value / tags
        ├─ shape（GridShape：占哪几格）
        └─ spriteAtlasPath / spritePath / spriteChanged（贴图三字段）
            │
渲染层  GameItemElement（继承 GameItem，是「会显示自己的物品」）
        ├─ rectTransform        元素 rect = shape 格数 × gridSize
        ├─ handler              根节点（Image 无 sprite，点击层）
        ├─ background           物品贴图节点
        ├─ fakeBackground       拖拽幽灵节点
        └─ rotator / middle / inner …（旋转、翻转、锚点容器）
            │
UI 层   UnityEngine.UI.Image + RectTransform
        ├─ sprite / type / preserveAspect
        ├─ alphaHitTestMinimumThreshold（点击判定）
        └─ rectTransform.sizeDelta / anchors / pivot
```

### 1.2 核心常量（全部实测）

| 名称 | 值 | 来源 |
|---|---|---|
| `UISettings.gridSize` | **16** | 实例 +0x28；整套 UI 的唯一长度单位 |
| 原版贴图密度 | 习惯 16px / 格 | 2077 个 sprite 实测（全为 16 的倍数）——**仅是原版美术习惯，不是硬约束** |
| 原版 `pixelsPerUnit` | **100** | UnityPy 读取 `m_PixelsToUnits` |
| 每格世界尺寸 | **0.16 单位** | 16 ÷ 100 |
| `alphaHitTestMinimumThreshold` | **1e-5** | `GameItemElement..ctor` 写入 Image+0x100 |
| 原版「透明背景」的 alpha | **1 ~ 3**（非 0） | 像素实测：四角 RGBA=(0,0,0,1~3) |

---

## 2. 机制原理（四个独立机制）

> 这四件事各自都能单独让贴图失效，且会互相掩盖——所以排障时只能逐层剥开。

### 2.1 贴图初始化：一次「默认参数构造」

游戏创建一个物品图片时，走的是**固定的参数构造**：

```
Sprite（贴图资源，自带 ppu）
   ↓  Sprite.Create 式构造：rect / pivot / pixelsPerUnit
Image.sprite = sprite
   ↓  默认参数：type = Tiled（平铺）、alphaHitTestMinimumThreshold = 1e-5
RectTransform.sizeDelta ← 由游戏按「贴图尺寸 ÷ ppu」推导
```

**关键点**：这个构造里没有「像素密度」这个参数，密度完全由 **`pixelsPerUnit`（ppu）** 表达。
只要 ppu 按「贴图宽 ÷ (列数 × 0.16)」设好，任意像素尺寸的贴图都能正确落进格子——
**不存在「必须 16px/格」的约束**，格数只由 shape 决定，贴图只需保证**长宽比 = 格数比**。

### 2.2 格子对齐：把贴图排进 x×y 布局

```
shape（0/1 字符串 + shapeWidth）
   ↓  × UISettings.gridSize(16)
元素 rect（UI 像素）  ← ValidateSelectLocked: sizeDelta = (宽×16, 高×16)
   ↓  贴图被「排」进这个 rect（拉伸，不裁切）
显示结果
```

**关键点**：
- 元素 rect 只由 **shape** 决定，与贴图尺寸无关
- 贴图**不会**撑大 rect，也不会被裁——它是被拉伸填满 rect 的
- 所以「2×3 的物品 + 640×960 的图」是合法组合，前提是 ppu 正确

### 2.3 点击命中：alpha 判定 + 1e-5 阈值

`Image.IsRaycastLocationValid` 的实际逻辑：

```
threshold <= 0            → 命中（整块 rect 可点）
threshold > 1             → 不命中
activeSprite == null      → 命中（无贴图 = 整块可点）
否则：sprite.texture.GetPixelBilinear(uv).a >= threshold
```

游戏把阈值设为 **1e-5**，含义精确：

| 像素 alpha | 判定 | 原版用途 |
|---|---|---|
| 0 | **不命中**（点击穿透） | 真正的空白区 |
| 1 ~ 3（不可见） | **命中** | 原版「透明背景」——看不见但可点 |
| 255 | 命中 | 实体像素 |

**所以原版的「透明背景」不是 alpha=0，而是 alpha=1**。这是本手册最重要的坑之一。

### 2.4 渲染类型：Tiled 与顶点上限

物品图片默认 `Image.type = Tiled`（平铺），Unity 的平铺网格按 `rect ÷ pixelsPerUnit` 切格重复。
一旦贴图密度错误（例如 640×960 的图被当成 640×960 的 rect），格子数会达到 **100×100 = 1 万格**，
顶点数超过 Unity 单 mesh 上限（65535）→ **mesh 生成失败 → 整张图不渲染**（且无任何报错）。

ppu 正确时，平铺格大小同步变成「1 格 = 1 块」，只铺一次，不会爆。

---

## 3. API 参考

> 约定：`→` 后为返回值；示例为 C#（Il2CppInterop 代理，`using Il2Cpp;`）

### 3.1 物品创建

#### `DirectoryMaster.Item(string identifier, bool isOwned = true)` → `GameItem`

从物品目录创建物品（走原版工厂，返回可用的 `GameItem`，通常是 `GameItemElement`）。

```csharp
GameItem item = DirectoryMaster.Item("system_module_performance", true);
```

**用途**：作为自定义物品的「模板」——继承它的功能、标签、属性，再覆盖贴图/形状/名称。

#### `ItemDirectory.CreateEmptyItem(string identifier = null)` → `GameItem`

创建一个空白物品（无模板继承）。

```csharp
GameItem item = ItemDirectory.CreateEmptyItem(null);
```

**用途**：从零构建容器类物品（需要自己配 `SetContentWindow` / 标签）。

---

### 3.2 贴图三件套

#### `GameItem.SetSprite(string spriteAtlasPath, string spritePath)` → `GameItem`

只写三个字段，**不动 shape**：

| 字段 | 偏移 | 含义 |
|---|---|---|
| `spriteAtlasPath` | +0x250 | atlas 名（自定义名，与原生区分） |
| `spritePath` | +0x248 | sprite 名（= 配置里的 key） |
| `spriteChanged` | +0x258 | 置 1，触发元素重新解析贴图 |

```csharp
item.SetSprite("flow_machine_atlas", "flow_machine");
```

> 等价于手写三个属性赋值；用 API 更稳（未来字段变化不受影响）。

#### `GameItem.SetShape(GridShape itemShape)` → `GameItem`

单独设置占位形状。

```csharp
item.SetShape(new GridShapeBuilder().SetDataFill(2, 3, 1).Build());
```

#### `GameItem.SetSpriteAndShape(string spriteAtlasPath, string spritePath)` → `GameItem`

设三字段 + **从贴图 alpha 推导 shape**：

```
width  = sprite.rect.width  / UISettings.gridSize(16)
height = sprite.rect.height / UISettings.gridSize(16)
逐格采样 alpha>0 → 该格占位
```

```csharp
// 32×32 的贴图 → 自动得到 2×2 的形状
item.SetSpriteAndShape("custom_atlas", "custom_storage_box_sprite");
```

**注意**：它按 `贴图像素 ÷ 16` 反推格数（把 16 当默认密度），所以只适合 16px/格的图。
自定义贴图（任意密度）应改用 `SetSprite` + `SetShape` 分开设置，格数由 shape 明确指定。

---

### 3.3 形状构建

#### `GridShapeBuilder`

| 方法 | 说明 |
|---|---|
| `new GridShapeBuilder()` | 默认 1×1 全占 |
| `SetData(string data, int width)` | 0/1 字符串按行拼接 + 每行宽度 |
| `SetDataFill(int w, int h, byte b = 1)` | 填充 w×h 矩形 |
| `SetDataOutside(byte b)` | 设置「外部」值 |
| `SetPosition(int minX, int minY)` | 设置最小坐标 |
| `SetOrientation(int orientation)` | 旋转（0~3，90° 步进） |
| `SetTransform(GridShape shape)` | 套用另一形状的变换 |
| `Build()` | 克隆成独立形状（推荐，避免共享 builder 状态） |
| `get_shape()` | 返回 `this`（builder 继承 GridShape） |

**样例**：

```csharp
// 2×3 满矩形
GridShape rect = new GridShapeBuilder().SetDataFill(2, 3, 1).Build();

// L 型（2×2 缺右下）：行拼接 "11" + "10"
GridShape lShape = new GridShapeBuilder().SetData("1110", 2).Build();
```

#### `GridShape` 关键字段

| 成员 | 说明 |
|---|---|
| `width` / `height` | 列数 / 行数 |
| `globalWidth` / `globalHeight` | 按 orientation 交换后的宽高 |
| `minX` / `minY` | 最小坐标（锚点） |
| `data` | `byte[]`，0/1 占位表 |

---

### 3.4 贴图解析（游戏侧）

#### `RenderHandler.LoadFromAtlas(string atlasPath, string name)` → `Sprite`

游戏解析物品贴图的**唯一入口**：先查 `atlasCache`，miss 再走 `LoadFromAtlasRaw`。

```csharp
// 我们拦截它（Harmony Prefix）来返回自定义贴图
[HarmonyPatch(typeof(Il2Cpp.RenderHandler), "LoadFromAtlas")]
static bool Prefix(string atlasPath, string name, ref Sprite __result)
{
    if (atlasPath != "flow_machine_atlas") return true;
    __result = FlowSpriteRegistry.Instance.GetSprite("flow_machine");
    return false;
}
```

#### `RenderHandler.atlasCache` → `Dictionary<string, Dictionary<string, Sprite>>`

**public static** 的贴图缓存：`atlas 名 → (sprite 名 → Sprite)`。

```csharp
// 另一条路：直接注入缓存，再走 SetSprite（FutureTech 的做法）
RenderHandler.atlasCache["my_atlas"]["my_sprite"] = mySprite;
item.SetSprite("my_atlas", "my_sprite");
```

#### `RenderHandler.LoadFromFile(string spritePath)` → `Sprite`

官方 mod 贴图路径：`Resources.Load<Sprite>(spritePath)`，加载真实导入的 sprite 资产。
（`useSpriteFromMod = true` 时走这条；运行时创建的贴图不受它影响。）

---

### 3.5 元素渲染

#### `GameItemElement.UpdateRectTransform()`

元素每次重算布局（选中 / 旋转 / 换格 / 拖拽）都会执行：
设置 `localScale`（±1 镜像）、`anchorMin/Max`（左上角锚点）、`pivot`（角点）、
`localEulerAngles`（0/90/180/270）、`anchoredPosition`。
**不设置 sizeDelta**——尺寸由 `ValidateSelectLocked` 按 `shape × gridSize` 写入。

#### `GameItemElement` 关键成员

| 成员 | 说明 |
|---|---|
| `rectTransform` | 元素矩形（= 格数 × 16） |
| `widthPixels` / `heightPixels` | 格数 × gridSize（内部就是 shape.Width × 16） |
| `handler` | 根节点（Image 无 sprite） |
| `background` | 物品贴图节点（游戏在此设 sprite） |
| `fakeBackground` | 拖拽幽灵节点 |
| `rotator` / `middle` / `inner` | 旋转 / 中间 / 内层容器 |

#### 节点偏移对照表（调试用）

| 属性 | 偏移 | 十进制 |
|---|---|---|
| handler | 0x2D0 | 720 |
| inner | 0x2D8 | 728 |
| fakeInner | 0x2E0 | 736 |
| middle | 0x2E8 | 744 |
| rotator | 0x2F0 | 752 |
| **background** | **0x2F8** | **760** |
| fakeMiddle | 0x310 | 784 |
| fakeRotator | 0x318 | 792 |
| **fakeBackground** | **0x320** | **800** |

---

### 3.6 Sprite 创建（Unity 侧）

#### `Sprite.Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit)` → `Sprite`

| 参数 | 说明 |
|---|---|
| `texture` | 运行时纹理（`new Texture2D` + `ImageConversion.LoadImage`） |
| `rect` | 通常 `new Rect(0, 0, texture.width, texture.height)` |
| `pivot` | `(0.5, 0.5)` 居中即可 |
| **`pixelsPerUnit`** | **贴图密度——本手册的核心参数** |

**ppu 公式（与原版对齐）**：

```
基准：原版 16px/格 配 ppu=100 → 1 格 = 16/100 = 0.16 世界单位
自定义：ppu = 贴图宽 ÷ (列数 × 0.16)      ← 任意像素密度都适用

例：640 宽、2 列 → 2000     32 宽、2 列 → 100     32 宽、1 列 → 200
贴图只需满足「长宽比 = 格数比」，像素密度由 ppu 吸收
```

---

### 3.7 存档 / 读档

`SaveManager.DecodeNodes` 读档时：

```
1. DirectoryMaster.Item(identifier)  ← 按 identifier 重建物品（走目录工厂）
2. TagSystem.Clone(...)              ← 回灌标签状态
3. GameItem.SetSprite(...)           ← 恢复贴图字段
```

**结论**：物品必须在目录里**稳定注册**（`directory.Add(key, factory)`），否则读档后物品丢失；
运行中临时改的字段若不在存档里，读档后会被目录工厂的默认值覆盖。

---

## 4. 我们的框架（ProbablyBagMod）

### 4.1 目录结构

```
src/Mod/
├─ PBModEntry.cs                  入口：目录注册
├─ Common/EmbeddedResources.cs    嵌入资源（前缀探测 + 读取）
├─ Items/
│   ├─ FlowItemRegistry.cs        物品定义（items.json）
│   └─ ItemFactory.cs             物品工厂（模板/空装配 + 标签 + 代码工厂注册）
├─ Sprites/
│   ├─ FlowSpriteRegistry.cs      贴图管理（sprites.json + PNG→Sprite）
│   └─ SpriteEntry.cs             贴图配置模型
└─ Patches/LoadFromAtlasPatch.cs  贴图拦截
```

### 4.2 数据流

```
items.json ──→ FlowItemRegistry ──→ ItemFactory.Create(def) ──→ item
                                     │
sprites.json ─→ FlowSpriteRegistry ──┘
   （含 PNG）    （贴图 + shape）

ItemFactory 内部：
  ① 代码工厂（若已 Register）→ 直接返回
  ② 否则数据化装配：
     CreateBase   template → DirectoryMaster.Item(模板)
                  empty    → ItemDirectory.CreateEmptyItem
                  ↓ 统一 item.identifier = key
     ApplySprite  三字段 + shape
     ApplyText    name / shortDescription / flavorText
     ApplyType    SetGameItemType + SetValue
     ApplyTags    逐条写 tag（按声明的字段名写 TagState 槽位）

游戏解析贴图 → RenderHandler.LoadFromAtlas
             → LoadFromAtlasPatch（atlas 命中）→ 返回我们的 Sprite
```

### 4.3 配置文件

**sprites.json**

```json
{
  "sprites": [
    {
      "key": "flow_machine",
      "atlas": "flow_machine_atlas",
      "file": "flow_machine.png",
      "shape": "111111",
      "shapeWidth": 2
    }
  ]
}
```

| 字段 | 说明 |
|---|---|
| `key` | 贴图逻辑名（= sprite 名） |
| `atlas` | 自定义 atlas 名（拦截匹配用） |
| `file` | `assets/` 下的 PNG 文件名 |
| `shape` | 0/1 按行拼接（`"111111"` + width 2 = 2×3 满矩形） |
| `shapeWidth` | 每行宽度（列数） |

**items.json**

```json
{
  "items": [
    {
      "key": "flow_machine",
      "base": "template",
      "template": "system_module_performance",
      "sprite": "flow_machine",
      "name": "流量机",
      "desc": "使背包内相邻的流源按方向逐格传导。",
      "flavor": "它嗡嗡作响，像在抱怨自己被降级了。",
      "type": "MODULE",
      "value": 100,
      "tags": [
        { "valueName": "BONUS_PERCENTAGE_PERFORMANCE_INT", "valueInt": 20 },
        { "valueName": "ADDITIONAL_EFFECT_STRING", "valueString": "传导时额外 +1 格" }
      ]
    }
  ]
}
```

| 字段 | 必填 | 说明 |
|---|---|---|
| `key` | ✅ | identifier（存档按它找回） |
| `base` | — | `template`（默认）/ `empty`（从零装配） |
| `template` | base=template 时必填 | 原版模板物品 id |
| `sprite` | — | 贴图 key（空则回退 `key`） |
| `name` / `desc` / `flavor` | — | 名称 / 正文 / 风味（空则沿用模板） |
| `type` | — | `MODULE` / `STORAGE` / `TOOL` / `MACHINE` / `FOOD` / `WEAPON` / `MEDICAL` / `ARMOR` |
| `value` | — | 基础价值（0 = 沿用模板） |
| `tags` | — | 数组，每项 `{ "valueName": tag名, "valueXxx": 值 }`；见下方说明 |

**`tags` 写法**：结构直接镜像游戏的 `TagState`，**写哪个槽位由字段名决定**，与数字字面形态无关。

```json
"tags": [
  { "valueName": "ICE_BLOCK_TAG" },                                  // 只启用，不设值
  { "valueName": "LIQUID_CONTAINER_CURRENT", "valueInt": 100000 },   // valueInt  (+0x30)
  { "valueName": "ICE_TEMPERATURE",          "valueFloat": -5.0 },   // valueFloat(+0x34)
  { "valueName": "MY_PRECISE_VALUE",         "valueDouble": 12.3123 },// valueDouble(+0x40)
  { "valueName": "MY_FLAG",                  "valueBool": true },    // valueBool (+0x48)
  { "valueName": "MY_TEXT",                  "valueString": "abc" }  // valueString(+0x28)
]
```

- 一条可以只写一个槽位，也可以同时写多个（原版各 setter 互不干扰）
- **没写的槽位保持默认值**（`0` / `0f` / `0d` / `false` / `null`），不会被清空或覆盖
- **没有 `valueLong`**：原版只有 `valueLong` 的 setter，没有 `GetLong()`，写进去读不出来
- 读取端（`statusLines`）必须用**对应的字段**去读，否则会读到默认值

### 4.4 代码工厂

需要模板/数据化装配表达不了的行为时，注册一个代码工厂（**优先于**数据化装配）：

```csharp
ItemFactory.Register("my_device", def =>
{
    Il2Cpp.GameItem item = ItemFactory.CreateFromData(def);   // 先走数据化（贴图/文本/tags）
    if (item == null) return null;

    // 追加自定义装配
    item.EnableTag("MY_DEVICE_TAG", true);
    item.state.GetTag("CHARGE_INT").valueInt = 5;
    return item;
});
```

`CreateFromData` 可复用，所以代码工厂只需写「额外那部分」。

---

## 5. 踩坑清单

| 现象 | 根因 | 结论 |
|---|---|---|
| 物品空白、无法选中 | 图片 rect 被设成贴图原生尺寸（640×960），点击区被顶到元素外 | **ppu 错误**——ppu 正确时 rect 自动 = 元素尺寸 |
| sprite 在、enabled、alpha 1，却完全不渲染 | `Image.type = Tiled` + 大贴图 → 平铺 1 万格 → 顶点超 65535 → mesh 失败 | ppu 正确后平铺只有 1 块，自动消失 |
| 点击物品本体可以，点四周留白不行 | 留白 alpha=0 < 阈值 1e-5 | 贴图透明区 **alpha 至少给 1**（原版约定） |
| 高清图被算成几十格 | `SetSpriteAndShape` 用「像素 ÷ 16」反推 shape | 自定义贴图用 `SetSprite` + `SetShape` 分开设，格数由 shape 决定 |
| 读档后自定义物品消失 | 未在目录稳定注册 | 用 `directory.Add(key, factory)` 注册 |
| 改物品字段后重读档失效 | 读档按目录工厂重建 | 稳定值写进工厂/配置，不要只改运行时对象 |

---

## 6. 新增一个物品（操作手册）

### 6.1 准备贴图

```bash
python tools/pixel_pipeline.py 原图.png assets/新物品.png <列数> <行数>
```

管线：去背景 → 取主体 → 补边至格数比例 → 缩放到 `16×列 × 16×行` → 量化 → 平滑。

> ⚠️ **管线输出 alpha 只有 0 / 255**（硬切），而游戏要求「不可见的透明区 alpha ≥ 1」才能点中。
> 出图后必须补一步：
>
> ```python
> a = np.array(Image.open(path).convert('RGBA'))
> a[:, :, 3] = np.maximum(a[:, :, 3], 1)   # 0 → 1，视觉无变化
> Image.fromarray(a, 'RGBA').save(path)
> ```

尺寸不必是 16 的倍数（密度由 ppu 自动换算），但**长宽比要等于格数比**（2×3 用 2:3 的图）。

### 6.2 配 sprites.json

```json
{ "key": "my_thing", "atlas": "my_thing_atlas", "file": "my_thing.png",
  "shape": "111111", "shapeWidth": 2 }
```

字段：`key`（贴图名/sprite 名）、`atlas`（**每物品唯一**）、`file`、`shape`（0/1 按行拼接）、`shapeWidth`（列数）。

### 6.3 配 items.json

```json
{ "key": "my_thing", "base": "template", "template": "system_module_performance",
  "sprite": "my_thing", "name": "我的装置",
  "desc": "第一行。\n第二行。", "flavor": "一句风味小字。",
  "type": "MODULE", "value": 100,
  "tags": [ { "valueName": "BONUS_PERCENTAGE_PERFORMANCE_INT", "valueInt": 20 } ] }
```

| 字段 | 必填 | 说明 |
|---|---|---|
| `key` | ✅ | identifier，**存档按它找回，改名等于换物品** |
| `base` | — | `template`（默认）/ `empty` |
| `template` | base=template 时 ✅ | 原版模板 id |
| `sprite` | — | 对应 sprites.json 的 key（空则用 `key`） |
| `name` / `desc` / `flavor` | — | 空则沿用模板 |
| `type` / `value` / `tags` | — | 空则沿用模板 |

### 6.4 重编译并验证

`dotnet build -c Release` → `E:\code\build\ProbablyBagMod.dll`

日志应为：

```
[贴图] 已加载 my_thing → 32x48 ppu=100
[物品] 定义加载完成，共 2 项
[注册] 2 个物品已注册
```

`ppu=100` = 标准密度（32 ÷ (2 × 0.16)）；640 宽的图应显示 `ppu=2000`。

### 6.5 需要专属装配时

```csharp
// 在 OnInitializeMelon 里注册（必须在物品注册前）
ItemFactory.Register("my_thing", def =>
{
    Il2Cpp.GameItem item = ItemFactory.CreateFromData(def);
    if (item == null) return null;

    item.EnableTag("MY_TAG", true);
    item.state.GetTag("CHARGE_INT").valueInt = 5;
    return item;
});
```

---

## 7. 从零构建「专属物品」（能力层）

> 前置结论：**IL2CPP 下无法继承 `GameItem`**，游戏里也不存在「物品类」这个概念。
> 一个物品的「身份 + 能力」由三样东西决定：**identifier + 标签（Tag）+ 类型（GameItemType）**。

### 7.1 三种自定义程度

| 方案 | 做法 | 适用 |
|---|---|---|
| **A. 模板继承** | `DirectoryMaster.Item(模板)` + 改 identifier/贴图/形状/名称 | 换皮变体（当前框架） |
| **B. 空物品装配** | `ItemDirectory.CreateEmptyItem(id)` + 手工配标签/类型/价值/描述 | 真正「自己的物品」 |
| **C. 自定义行为** | Harmony patch 行为入口 + 按 identifier 分发 | 专属逻辑 |

### 7.2 方案 B：空物品装配

```csharp
GameItem item = ItemDirectory.CreateEmptyItem("my_device");
item.SetName("我的装置");
item.SetSprite("my_atlas", "my_device");
item.SetShape(new GridShapeBuilder().SetDataFill(2, 2, 1).Build());
item.SetValue(100);
item.unitBaseValue = 100;
item.SetGameItemType("MODULE");     // MODULE / STORAGE / TOOL / MACHINE / FOOD / WEAPON / MEDICAL / ARMOR / LUXURY_ITEM
item.EnableTag("SYSTEM_TAG");
item.EnableTag("MODULE_TAG");
item.EnableTag("SYSTEM_TAG_UTILITY");
item.EnableManufacturer("my_mod");  // 可选：厂商标记
```

**注意**：空物品没有模板默认值（价值、描述、标签都要自己给），标签组合决定游戏系统是否「认」它。

### 7.3 标签目录（实测高频）

| 标签 | 含义 | 约使用次数 |
|---|---|---|
| `LIQUID_CONTAINER_TAG` | 液体容器（倒/灌/计量） | 77 |
| `MODULE_TAG` | 模块（可安装到设备） | 41 |
| `SYSTEM_TAG` | 系统物品（不计入普通商品逻辑） | 38 |
| `CONTAINER_TAG` | 容器（需要配 `SetContentWindow`） | 35 |
| `ITEM_MEDICAL_TAG` | 医疗物品 | 33 |
| `DURABILITY_TAG` | 有耐久度 | 18 |
| `BROKEN_MACHINE_TAG` | 损坏的机器 | 18 |
| `WINE_CONTAINER_TAG` | 酒容器 | 17 |
| `CAGE_TAG` | 笼子（装动物） | 14 |
| `IMPORTANT_TAG` / `STOLEN_TAG` / `IS_OWNED_TAG` | 重要性 / 赃物 / 已拥有 | 90 / 30 / 39 |

### 7.4 方案 C：自定义行为入口

| 入口 | 时机 | 签名 |
|---|---|---|
| `ItemSelectHandler.TrySelectItem` | 选中物品 | `(GameItem newItem)` |
| `ItemSelectHandler.TryEquipItem` | 装备物品 | `(GameItem newItem)` |
| `ItemMouseDoubleClickHandler.DoubleClickAction` | 双击 | `(GameItem newItem, Vector2 mousePosition)` |
| `ItemContextHandler` | 右键菜单 | — |
| `ItemDefaultActivateHandler` | 默认激活（按键） | — |

```csharp
[HarmonyPatch(typeof(Il2Cpp.ItemMouseDoubleClickHandler), "DoubleClickAction")]
static class MyDeviceDoubleClick
{
    static void Postfix(Il2Cpp.GameItem newItem, Vector2 mousePosition)
    {
        if (newItem == null || newItem.identifier != "my_device") return;
        // 你的专属逻辑
    }
}
```

### 7.5 自定义状态持久化

物品上的自定义状态**一律挂在 TagSystem 上**（tag 是存档一等公民）：

```csharp
// 写入：GetTag 内部 InitTagString 会自动创建并返回 dict 引用，直接改字段
item.EnableTag("my_device_charge", true);
item.state.GetTag("my_device_charge").valueString = "5";

// 读取
TagState state = item.GetTagReadonly("my_device_charge");
string charge = state == null ? null : state.valueString;
```

> 不要用 `ModifyTag(Action<TagState>)` —— IL2CPP 下自定义委托有类型不匹配风险。

`TagState` 字段：`identifier` / `identifierName` / `valueString` / `valueInt` / `valueLong` / `valueFloat` / `valueDouble` / `valueBool`；
方法：`IsEnabled` / `Enable` / `Disable` / `ToggleEnabled` / `Clone` / `Reset`。

⚠️ **每个 `SetXxx` 只写自己那一个槽位，其余槽位保持默认值（`0` / `null`），既不置 null 也不清零**；
`GetXxx` 就是裸读对应偏移，**没有任何类型判断**。所以「写哪个槽」和「读哪个槽」必须自己配对。
`valueLong` 有 setter 但**没有 `GetLong()`**，写进去读不出来。

读档时 `DirectoryMaster.Item(identifier)` 重建 + `TagSystem.Clone` 回灌 → 状态自动恢复。

### 7.6 推荐路径

```
1. 方案 B 造出来（能放、能看、能拖）
2. 方案 C 加行为（patch 入口 + identifier 分发）
3. 状态全放 tag（读档自动恢复）
```

---

### 7.7 物品说明（text 字段与 tooltip 拼装）

物品说明**不是一整块文本**，而是三个独立字段，显示时由游戏运行时拼成富文本。

#### 三个字段

| 字段 | 偏移 | 显示效果 | 原版来源 key |
|---|---|---|---|
| `name` | +0x1D8 | 标题（`GetDisplayName()` 优先取自定义名） | `item_xxx_name` |
| `shortDescription` | +0x1E0 | 正文段落，颜色 `#C4C4B0`（暖灰） | `item_xxx_desc` |
| `longDescription` | +0x1E8 | 详细说明（商店/检视面板） | — |
| `flavorText` | +0x1F0 | 风味小字，颜色 `#88948A`（灰绿），斜体 | `item_xxx_flavor` |

#### 原版写法（`AmenitiesItemDirectory` 实测）

```csharp
item.SetName(LocHelper.GetLocalizedItem("item_skincare_cream_name"));
item.flavorText       = LocHelper.GetLocalizedItem("item_skincare_cream_flavor");  // → +0x1F0
item.shortDescription = LocHelper.GetLocalizedItem("item_skincare_cream_desc");    // → +0x1E0
```

#### Tooltip 拼装顺序（`GameItemElement.GetTooltipBasic()`）

```
1. GetDisplayName()                          ← 标题（自定义名优先）
2. " (厂商)"                                  ← DisplayManufacturer
3. CommonTooltipHelper.CreateItemTypeTooltip ← 按 tag/type 生成「类型段」
                                                （mech_tooltip_disabled / _locked_machine / _hidden_dev）
4. GeneralHelper.CreateGeneralItemTooltip    ← 价值 / 锁定 / 重量
5. shortDescription                          ← AddLine(色 #C4C4B0)
6. flavorText                                ← AddLine(色 #88948A)
7. Contraband / Stolen / ToggleAppearance 段 ← 按 tag 追加
   ↓
OverlayHandler.ShowTooltip(position, RichText, maxCharWidth: 60)
```

#### 自定义文本

```csharp
item.SetName("流量机");
item.shortDescription = "使背包内相邻的流源按方向逐格传导。\n每帧推进一格。";
item.flavorText = "它嗡嗡作响，像在抱怨自己被降级了。";
```

- **支持 `\n` 换行**（`RichTextBuilder` 按换行拆行）
- **支持富文本标签**：`<color=#xxxxxx>` / `</color>` / `<b>` / `<i>` / `<s>`
- **不支持本地化切换**：直接写字面量，切语言不会变（原版走 `LocHelper` 查表）
- **类型段是自动生成的**：加了 `LIQUID_CONTAINER_TAG` 等标签，游戏会自己插入对应说明

### 7.8 模块效果文本（tag 驱动，非手写）

「-性能 +16%」这类文本**不是写在物品上的字符串**，而是显示时从 tag 读数值现场拼出来的。

#### 存储（`ModuleHelper.InitModuleItem`）

```
InitModuleItem(item, moduleType, perf%, effi%, qual%, isShipModule, additionalEffectDisplay, tier)
    ↓ 只写 tag，不写任何显示文本
BONUS_PERCENTAGE_PERFORMANCE_INT.valueInt = 16
BONUS_PERCENTAGE_EFFICIENCY_INT.valueInt  = 0
BONUS_PERCENTAGE_QUALITY_INT.valueInt     = 0
ADDITIONAL_EFFECT_STRING.valueString      = <additionalEffectDisplay>
MODULE_TYPE.valueString / MODULE_TIER_INT / MODULE_TAG / SHIP_MODULE_TAG …
```

#### 显示（`ModuleHelper.CreateModuleTooltip`）

```
GetTagReadonly("BONUS_PERCENTAGE_PERFORMANCE_INT").valueInt   → 16
GetTagReadonly("TEMP_PERCENTAGE_PERFORMANCE_INT").valueInt    → 临时加成
  ↓ AddModuleStatLine(builder, 本地化("mech_module_stat_performance"), 16, temp)
String.Format(" -{0} {1}{2}%", "性能", "+", 16)      ← 无临时加成
String.Format(" ({0}: {1}{2}%)", "性能", "+", 16)    ← 有临时加成，接在“基础”后
ADDITIONAL_EFFECT_STRING → 单独一行（mech_module_effect_header 之后）
```

#### 改数值 / 加自定义效果

```csharp
// 只改数值：直接写 TagState 字段（GetTag 内部 InitTagString 自动创建，返回 dict 引用）
item.state.GetTag("BONUS_PERCENTAGE_PERFORMANCE_INT").valueInt = 20;

// 加一行自定义效果
item.state.GetTag("ADDITIONAL_EFFECT_STRING").valueString = "传导时额外 +1 格";
```

> 用 `state.GetTag(...)` 直接改字段，**不要用 `ModifyTag(Action)`** —— IL2CPP 下自定义委托有类型不匹配风险。

#### `TagState` 值字段

| 字段 | 偏移 | 类型 | getter |
|---|---|---|---|
| `valueString` | +0x28 | 文本（null 会被强制存成 `""`） | `valueString` |
| `valueInt` | +0x30 | int | `GetInt()` = `mov eax,[rcx+30h]` |
| `valueFloat` | +0x34 | float | `GetFloat()` = `movss xmm0,[rcx+34h]` |
| `valueLong` | +0x38 | long | **无 getter** |
| `valueDouble` | +0x40 | double | `GetDouble()` = `movsd xmm0,[rcx+40h]` |
| `valueBool` | +0x48 | bool | `GetBool()` = `movzx eax,byte ptr [rcx+48h]` |
| `internalValueString` | — | 内部文本 | — |
| `valueEnabled` | — | 是否启用（`IsTag` 会检查） | `IsEnabled()` |

**关键事实**（ISIL 反汇编实锤）：

- 六个槽位**并排存在，没有「当前值类型」标记字段**——类型信息只存在于代码里的读写约定
- `.ctor` 只显式写 `identifier` / `identifierName` / `valueString = ""`，
  `+0x30` 之后全靠 CLR 零初始化 ⇒ 默认 `0 / 0f / 0d / false`
- `SetInt` 的机器码就是 `mov [rcx+30h],edx; mov rax,rcx; ret` —— **不碰其他槽位**
- `Reset()` 是唯一会一次清空全部槽位的方法
- `ModifyTag` **不调用 `Reset`**，所以「先 SetFloat 再 SetInt」会留下旧 float 值

⇒ 这就是我们把 `items.json` 的 `tags` 设计成「字段名直接对应槽位」的原因：
让写端和读端共用同一套词汇，消灭「按字面形态猜类型」这层歧义。

### 7.9 液体容器（水分含量）

> 这是**游戏原生**的水瓶机制（可倒、可灌、可喝、有水质等级）。
> 我们自己的装置状态（冰块的水分 / 温度）用普通 tag 数值，不走这套系统。

#### 装配入口（原版水瓶走的就是它）

```csharp
WaterHelper.InitLiquidContainerItem(
    GameItem item,
    int capacityInMl,                  // 容量（毫升）——water_jug 是 6000
    int basePrice,
    bool isFilterContainer = false,
    bool isGraduated = false,          // 是否有刻度
    bool isDropper = false,
    bool isTransparentContainer = false,
    int sheetPage = 3)
```

简化版：`LiquidContainerHelper.InitLiquidContainerItem(item, capacity=500, isStartFull=true)`
（只设 tag + `WATER` 类型）。

#### 它写入的数据

| 载体 | 内容 |
|---|---|
| `LIQUID_CONTAINER_TAG` | 标志（`IsTag` 判定） |
| `LIQUID_CONTAINER_MAX.valueInt` | 容量（毫升） |
| `LIQUID_CONTAINER_CURRENT.valueInt` | 当前水量（毫升） |
| `LIQUID_CONTAINER_SIPPING` | 吸饮状态（简化版才有） |
| `GameItemType` | `WATER` |
| 水质数据 / 音效 | 等级与特征 / `AudioHelper.InitDrinkableAudio` |

#### 读取

```csharp
int currentMl = item.GetTagReadonly("LIQUID_CONTAINER_CURRENT").valueInt;
int maxMl     = item.GetTagReadonly("LIQUID_CONTAINER_MAX").valueInt;
bool isLiquid = item.IsTag("LIQUID_CONTAINER_TAG");
int value     = LiquidContainerHelper.GetLiquidContainerValue(item);
```

---

## 附录：取证速查

| 需求 | 命令 / 位置 |
|---|---|
| 反编译 mod DLL | `ilspycmd xxx.dll -o out/` |
| 反编译 Il2Cpp 程序集 | `ilspycmd MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll -o out/` |
| 查看游戏函数汇编 | `_isil_out/IsilDump/Assembly-CSharp/<类名>.txt` |
| 提取原版贴图 / 尺寸 | UnityPy 读 `Probably Stolen_Data/resources.assets` |
| 游戏路径 | `E:\SteamLibrary\steamapps\common\Probably Stolen Demo` |
