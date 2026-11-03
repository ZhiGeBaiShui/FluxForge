# Probably Stolen 游戏机制 API 手册

> **历史取证记录（2026-10-09 ~ 10-18，Il2Cpp / MelonLoader 时期）。**
> 本文的**游戏 API / 机制**仍可参考；其中出现的 **FluxForge 架构代码**（`FlowMachine`、旧 `FlowDriver`、
> 指针式 `DeviceRegistry`、`Il2Cpp.` 前缀、把 `OnGameLoadedInit` 当读档回灌点）是**旧实现，已被当前
> Mono 0.46 代码取代**。当前架构、生命周期与测试基线以 [`README.md`](../README.md) 和
> [`src/Mod/GridBuilding/Lifecycle/README.md`](../src/Mod/GridBuilding/Lifecycle/README.md) 为准；
> 0.46 Mono 下类型名不再需要 `Il2Cpp.` 前缀。

> 来源：2026-10-09 ~ 10-18 的 mod 开发沉淀（FluxForge）
> 取证手段：ISIL 转储（`_isil_out/IsilDump`）+ ilspycmd 反编译（mod DLL / Il2Cpp 程序集）+ UnityPy 资源提取 + 游戏内只读探针
> 适用对象：给 Probably Stolen 做自定义物品 / 贴图 / UI 的 mod 开发者

---

## 0. 项目历史与结论

| 日期 | 事件 | 沉淀结论 |
|---|---|---|
| 10-09 | 网格系统迁移到独立类库 `FluxForgeCore` | 47 项测试全绿；网格语义规格书可用 |
| 10-13 | 落地「流源传导」框架（运动层 / 反应层解耦） | 装置接口 `IFlowDevice` 同时决定「往哪出」和「带什么载荷」 |
| 10-14 | 代码清零，改由用户主导设计 | 保留 29 项设计决策，代码从 git 历史恢复 |
| 10-16 | 自定义物品生成层 | 目录注册 + `items.json` / `sprites.json` 双配置驱动 |
| 10-17 | **贴图链路攻关**（本文重点） | 从「16px 假设」到「ppu 才是真参数」，渲染层 patch 全部撤销 |
| 10-18 | 物品交互层攻关（面板 / 拖拽 / 液体 / 生命周期） | 双击走 `contentWindow`、拖拽走 `Target` 链、水瓶按物质分 tag、网格 Y 轴向下 |

**10-17 的最终结论（一句话）**：
> 游戏把贴图按 **ppu 换算成「世界尺寸」** 再排进 UI；只要 `ppu = 贴图宽 ÷ (列数 × 0.16)`，图片尺寸、平铺格数、命中判定会同时自动正确，**不需要任何渲染层补丁**。

**10-18 的最终结论（一句话）**：
> 物品交互都由游戏通用处理器驱动：双击看 `contentWindow`、拖拽看 `Target` 链、液体看容器类型；mod 只挂数据、打行为 Prefix，不改交互层。

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

**用途**：从零构建容器类物品（需要自己配 `SetContentWindow` / 标签；面板构造见 3.8）。

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

#### 网格坐标系（Y 轴向下）

`GridPixelElement.Validate` 把网格坐标换算成 UI 位置，ISIL 指令 470~482：

```
470 Move rax, r14          ; 行号 × 格高
471 Neg rax                 ; 取负
472 Move xmm0, rbp          ; 列号 × 格宽
474 Move stack:0x38, xmm0   ; x
476 Move xmm0, rax          ; -y
477 Move stack:0x3C, xmm0   ; y
482 Call RectTransform.set_anchoredPosition
```

⇒ **`anchoredPosition = (x, -y)`，网格 Y 轴向下。**

- `minY = 0` 是**最上面**一行；`minY` 越大越靠屏幕下方
- 想让物品在视觉上「向上」移动，网格 Y 要**减小**
- 不要按数学坐标（Y 向上）理解 `shape.minY`，否则上下会反

> 按 `shape.minY + height` 遍历占格时，行号递增在屏幕上就是往下走。

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

> `ModHook.OnGameLoadedInit` 在读档流程的 **ISIL 108** 就触发，早于真正的读档 IO
> （`SaveFiles.WithIORetry` 121/134、`SaveFiles.IndexLoad` 154），即**先于物品重建**。
> 要清空「旧物品 → 自定义状态」的缓存，放在这个钩子里是安全的。详见 4.5。

---

### 3.8 物品 UI 面板（PixelWindow）

> 结论先行：物品面板不需要预制体，也不需要 patch 双击——`PixelWindow` 可以纯代码构造，
> `item.SetContentWindow(window)` 之后游戏的双击处理器会自动开/关它。
>
> 取证：`ItemMouseDoubleClickHandler` / `PixelWindow` / `GridPixelElement` 反编译，`AnimalCage.CreateCageWindow` 原版范例。

#### 3.8.1 双击打开：`ItemMouseDoubleClickHandler.DoubleClickAction`

处理顺序（ISIL 反编译）：

```
DoubleClickAction(item, mousePosition)
  ├─ item.contentWindow != null → OpenContentAction（打开 / 关闭，开关式）
  └─ 否则                      → OpenExamineAction（原版检视窗）
```

所以 mod 侧只需要：

```csharp
item.SetContentWindow(window);
```

`GameItem.SetContentWindow(PixelWindow window)` 把窗口挂到物品的 `contentWindow` 字段；
窗口的开关由双击处理器负责。双击就自动生效，**不要**再 patch `DoubleClickAction` 去手动 Show/Hide。
只有要追加「双击时顺便做别的事」才 patch（见 7.4）。

#### 3.8.2 `PixelWindow` 构造

```csharp
new PixelWindow(
    int width,
    int height,
    bool canMove,
    int xPadding,
    int yPadding,
    string title = null)
```

- 纯代码构造：内部 `new GameObject`，边框由 `RenderHandler.LoadFromAtlas("border_*")` 自绘，不依赖任何预制体
- 构造末尾执行 `Validate(); Hide();` ⇒ **new 出来默认是隐藏的**；手动展示要调 `Show()`，走双击则由 `OpenContentAction` 打开
- 宽高是 UI 像素；本项目按「格数 × 16 + padding」算（见 3.8.5）

| 方法 | 作用 |
|---|---|
| `bool Attach(PixelElement element)` | 把一个子元素挂进窗口 |
| `PixelWindow SetTitle(string title)` | 设置标题 |
| `PixelWindow Show()` / `PixelWindow Hide()` | 显示 / 隐藏 |
| `PixelWindow Center()` | 居中 |
| `void ToFront(bool force = false)` | 提到最前 |
| `Canvas GetBoundsCanvas()` | 取画布边界（定位用） |

#### 3.8.3 `GridPixelElement`：窗口里的网格容器

| 成员 | 签名 |
|---|---|
| 构造 | `new GridPixelElement(int gridWidth, int gridHeight, bool isCentered = false)` |
| 间距 | `GridPixelElement SetSpacing(int x, int y)`（实例方法） |
| 挂接 | `bool Attach(PixelElement element, int x, int y, int minimumWidth = 0, int minimumHeight = 0)` |

`GridPixelElement` 本身不是「物品网格」，只是按格坐标摆放 `PixelElement` 的布局容器；
真正的背包数据由 `GameGridInventory` 提供，挂进去之后游戏自带的拖拽/放置链就能工作。

#### 3.8.4 Il2CppInterop 的继承关系坑

`GameGridInventory` / `GridPixelElement` 在运行时确实是 `PixelElement` 的子类，
但 Il2CppInterop **没有生成这条继承关系**，所以不能直接传给 `Attach(PixelElement)`：

```csharp
// 编译不过：GameGridInventory 不被视为 PixelElement
// grid.Attach(inventory, 0, 0);

// 正确：走原生类型转换
Il2Cpp.PixelElement inventoryElement = inventory.TryCast<Il2Cpp.PixelElement>();
grid.Attach(inventoryElement, 0, 1, 1, 4);

window.Attach(grid.TryCast<Il2Cpp.PixelElement>());
```

#### 3.8.5 原版组装范例（`AnimalCage.CreateCageWindow`）

```
new PixelWindow(...)
  → new GridPixelElement(...)
  → new TagElement(...).SetText(...)
  → new GameGridInventory(w, h)
  → grid.Attach(inventory, ...)
  → window.Attach(grid)
```

顺序不能反：`GameGridInventory` 必须先包进 `GridPixelElement`，再挂到 `PixelWindow`；
原版与项目都按这个顺序组装。

本项目中的完整版本（`FlowItemSetup.AttachPanel`）：

```csharp
PixelWindow window = new PixelWindow(columns * 16 + 48, rows * 16 + 72, true, 8, 8, "流量机");
GridPixelElement grid = new GridPixelElement(columns, rows, true);
GameGridInventory inventory = new GameGridInventory(columns, rows);

grid.Attach(inventory.TryCast<PixelElement>(), 0, 1, 1, 4);
window.Attach(grid.TryCast<PixelElement>());

item.SetContentWindow(window);
```

> 挂按钮同样要 `TryCast`：`window.Attach(button.TryCast<PixelElement>())`。

### 3.9 物品拖拽的 Target 链

拖拽松手 `ItemMouseDragHandler.EndDrag` 的判定顺序（ISIL 反编译）：

```
MayPlayerInsertInto
  → SlotMarker.IsValid
  → AcceptUnchecked / TryAcceptOnce
  → 失败则 MayStack / GetStackSlot
  → 再失败 MayTarget / CanTarget / Target
```

前两段是「放进格子 / 堆叠」，最后一段是「A 落到 B 上」的物品间交互。

#### `GameItem` 的三个 Target 方法

实例是**被拖的物品**，参数是**落点物品**：

| 方法 | 语义 |
|---|---|
| `bool MayTarget(GameItem targetItem)` | 权限：允不允许尝试 |
| `bool CanTarget(GameItem targetItem)` | 可行性：当前条件下能不能成 |
| `void Target(GameItem targetItem)` | 执行：真正发生交互 |

底层由 `ItemBehaviourManager.current.targetBehaviourList`（`List<ItemTargetBehaviour>`）驱动，
每个行为实现 `CheckFilter` / `CheckMay` / `CheckCan` / `OnTarget` / `GetTooltip`。

#### mod 侧接入

不要改拖拽代码，直接 patch `GameItem.CanTarget` / `Target` 的 Prefix，按 identifier 或物品指针识别自己的物品：

```csharp
[HarmonyPatch(typeof(Il2Cpp.GameItem), "CanTarget")]
static class MyCanTargetPatch
{
    static bool Prefix(Il2Cpp.GameItem __instance, Il2Cpp.GameItem targetItem, ref bool __result)
    {
        if (__instance == null || __instance.identifier != "water_jug") return true;
        if (targetItem == null || targetItem.identifier != "my_device") return true;

        __result = true;
        return false;   // 已给出结论，跳过原逻辑
    }
}
```

`Target` 的 Prefix 返回 `false` 表示「已处理」，游戏不会再走原行为。
`GetTooltip` 也挂在同一个 behaviour 上，用于生成悬停提示。


#### ⚠ 两个必须记住的坑（实测踩过）

**1. 物品的格位在 `modifiedShape`，不是 `shape`**

| 字段 | 偏移 | 语义 |
|---|---|---|
| `GameItem.shape` | +0x198 | **定义形状**：宽高、像素掩码、局部原点。minX/minY 不随摆放更新 |
| `GameItem.modifiedShape` | +0x1A0 | **摆放后的形状**：拖放落点写这里（`SlotMarker.AcceptUnchecked` → `GridShapeBuilder.SetTransform`） |

想要「物品现在在哪一格」必须读 `modifiedShape.minX/minY`。
读 `shape` 会永远拿到定义时的原点，表现为「位置固定不动」。

**2. `GameItem.GetTagReadonly` 返回的是克隆体**

`GameItem.txt:6311-6322` 末尾 `Call TagState.Clone` —— 拿到的是副本，
改它**不会影响活 tag**。

写活 tag 必须走：
```csharp
item.ModifyTag(tag, state => state.Enable().SetInt(newValue));
```
原版 API 内部都走 `ModifyTag`。

**3. 物品朝向在 `GridShape.orientation`**

| 值 | 含义 |
|---|---|
| 0 | 0° |
| 1 | 逆时针 90° |
| 2 | 180° |
| 3 | 顺时针 90° |

物品原图朝上时，方向码 = `(4 - orientation) & 3` → 0上 1右 2下 3左。
`GameItem` 本身没有 rotation 字段，朝向完全存在 shape 里。

**4. 扣液体用 `WaterHelper.Remove(item, volumeInPart)`**

- 内部单位 = **0.001 mL**（`InitLiquidContainerItem` 把 capacityInMl × 1000 存进 tag）
- `Remove` 按各物质占比分摊、自动钳到 0、并刷新水质与容器贴图
- `AddLiquid` 的 addAmount ≤ 0 会直接 return，**不能用负数扣水**

### 3.10 背包数据结构（GameGridInventory）

> 以下为 `GameGridInventory` / `GridShape` 反编译 + 项目侧实测结论。

| 成员 | 事实 |
|---|---|
| `childItems` | `List<GameItem>`；**每次访问都 `new List<GameItem>(...)`** |
| `item.shape.minX` / `minY` | 物品在网格中的最小坐标 |
| `shape.Get(x, y)` | 该格是否占用（`!= 0`）；内部处理旋转 / 翻转 |
| `inventoryShape` | 网格自身的尺寸（`width` / `height`） |

#### 读取纪律

```csharp
// 每次刷新只取一次 childItems，不要在「每格 / 每帧」循环里反复访问
Il2CppSystem.Collections.Generic.List<GameItem> items = inventory.childItems;
for (int i = 0; i < items.Count; i++)
{
    GameItem item = items[i];
    GridShape shape = item.shape;
    for (int y = shape.minY; y < shape.minY + shape.height; y++)
    for (int x = shape.minX; x < shape.minX + shape.width; x++)
        if (shape.Get(x, y) != 0) { /* 该格属于 item */ }
}
```

`shape.Get(x, y)` 已经处理 orientation（旋转）与镜像，**不要**自己按 `data[]` 下标算。

#### 物品身份：只能用 Pointer

`GameItem` 不是 `UnityEngine.Object`，没有 `GetInstanceID()`，也不能挂组件/字段。
做「物品 → 自定义状态」字典时只能用 `item.Pointer`（`IntPtr`）：

```csharp
Dictionary<IntPtr, MyDevice> _devices = new Dictionary<IntPtr, MyDevice>();
_devices[item.Pointer] = device;
```

⚠️ 读档后游戏按目录重建物品，旧 wrapper 失效、原生对象可能被复用（指针值重复），
所以缓存必须在读档时**整体 `Clear()`**，不能按指针「复用」旧条目。见 4.5。

> 这里说的是**可重建的运行时缓存**；需要跨读档保存的状态仍按 7.5 写进 tag。

---

## 4. 我们的框架（FluxForge）

### 4.1 目录结构

```
src/Mod/
├─ FFModEntry.cs                  入口：目录注册 + 每帧驱动 + 读档重置
├─ Common/EmbeddedResources.cs    嵌入资源（前缀探测 + 读取）
├─ Items/
│   ├─ FlowItemRegistry.cs        物品定义（items.json）
│   ├─ ItemFactory.cs             物品工厂（模板/空装配 + 标签 + 代码工厂注册）
│   ├─ FlowItemSetup.cs           按 key 挂装置 / 面板 / Target 行为
│   └─ StatusLine.cs              statusLines 配置模型
├─ Flow/
│   ├─ FlowDriver.cs              每帧中枢（逐台刷新 → 发射 → 全局推进）
│   ├─ FlowGridAdapter.cs         背包网格 → 格子表
│   ├─ FlowMachine.cs             机器（网格 + 发射 / 收集 / 冰块）
│   ├─ FlowVisual.cs              流源可视化（overlay Canvas）
│   ├─ DeviceRegistry.cs          物品指针 → 装置实例
│   ├─ LiquidStock.cs             容器存量读写（唯一适配点）
│   └─ FlowMachine.cs             一台流量机的运行时状态
├─ Sprites/
│   ├─ FlowSpriteRegistry.cs      贴图管理（sprites.json + PNG→Sprite）
│   └─ SpriteEntry.cs             贴图配置模型
└─ Patches/
    ├─ LoadFromAtlasPatch.cs      贴图拦截
    ├─ PatchRunner.cs             逐组安装补丁（单个失败不杀进程）
    └─ FlowTooltipPatch.cs        tooltip 分派（GetTooltipBasic）
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

### 4.5 每帧驱动与生命周期

| 需求 | 入口 | 说明 |
|---|---|---|
| 逻辑推进 | `MelonMod.OnUpdate` | 每帧一次；`Time.deltaTime` 在暂停时为 0 |
| 位置同步 | `MelonMod.OnLateUpdate` | 游戏 UI 排完版之后，避免用到上一帧的 rect |
| 读档初始化 | `ModHook.OnGameLoadedInit` | 读档流程早期触发，早于物品重建 |
| 场景加载 | `MelonMod.OnSceneWasLoaded` | 游戏没有专门的「场景切换」钩子，用这个 |
| 常驻 overlay | `CustomUIManager.Instance.OverlayRoot()` | `DontDestroyOnLoad` 的 overlay Canvas |
| 窗口清理 | `OnSceneUnloaded → PruneDeadWindows` | `CustomUIManager` 自己清理 |

#### 暂停语义

`UnityEngine.Time.deltaTime` 在游戏暂停时自动为 0，所以自维护时间轴不需要额外判断暂停：

```csharp
public override void OnUpdate()
{
    float dt = UnityEngine.Time.deltaTime;
    if (dt <= 0f) return;      // 暂停帧
    FlowDriver.Update(dt);
}

public override void OnLateUpdate()
{
    FlowVisual.LateUpdate();   // UI 已排完版
}
```

#### `OnGameLoadedInit` 的真实时机

`PlayerStore` 读档流程的 ISIL：

```
指令 108   Call ModHook.OnGameLoadedInit
...
指令 121   SaveFiles.WithIORetry
指令 134   SaveFiles.WithIORetry
...
指令 154   SaveFiles.IndexLoad
```

⇒ **Init 钩子先于真正的读档 IO，也先于物品重建**。
在这里「清空旧缓存」是安全的：新的物品数据还没被读出来，不存在「清完又被旧数据覆盖」的窗口。

注册方式（静态委托字段，不是 event）：

```csharp
Il2Cpp.ModHook.OnGameLoadedInit = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OnGameLoaded);

private void OnGameLoaded()
{
    DeviceRegistry.Clear();    // 旧指针可能复用，必须整体失效
}
```

#### 场景与常驻 UI

- 没有场景切换专用钩子；场景相关逻辑放 `MelonMod.OnSceneWasLoaded(int buildIndex, string sceneName)`
- `CustomUIManager.Instance.OverlayRoot()` 返回常驻 overlay Canvas（`DontDestroyOnLoad`），适合挂跨场景的流源可视化
- `CustomUIManager` 的窗口在 `OnSceneUnloaded → PruneDeadWindows` 中清理，这是游戏侧的窗口生命周期

```csharp
public override void OnSceneWasLoaded(int buildIndex, string sceneName)
{
    // 场景相关初始化
}
```

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
| 双击容器/机器没反应，只开检视窗 | 物品的 `contentWindow` 为空 | `item.SetContentWindow(window)` 后双击自动开关，不要 patch 双击 |
| `grid.Attach(inventory, ...)` 编译不过 | Il2CppInterop 没生成 `GameGridInventory → PixelElement` 继承 | 传参前 `TryCast<Il2Cpp.PixelElement>()` |
| 面板里的物品上下位置反了 | 网格 `anchoredPosition = (x, -y)`，Y 轴向下 | 要视觉上移就减小网格 Y，不要加 |
| 水瓶的 `LIQUID_CONTAINER_CURRENT` 永远是 0 | 水瓶用 `CURRENT_PART_<ID>` 分物质存储 | 读 `LIQUID_CONTAINER_CAPACITY` + 物质 tag；机器才读 `MAX/CURRENT` |
| 倒液后源和目标反了 | `LiquidContainerHelper.TransferLiquid(target, source)` 与 `WaterHelper.TransferLiquid(source, target)` 顺序相反 | 按具体 helper 核对参数顺序 |
| `dict.Keys[i]` 编译不过 | IL2CPP 的 `KeyCollection` 不支持索引器 | `foreach` `Il2CppSystem.Collections.Generic.KeyValuePair` |
| 背包每帧卡顿 / GC 飙升 | `GameGridInventory.childItems` 每次访问都新建 List | 每次刷新只取一次，循环内复用 |
| 读档后装置状态错乱 / 挂到旧物品 | `GameItem` 没有 `GetInstanceID()`；指针读档后可能复用 | 用 `item.Pointer` 做 key，读档时整体 `Clear()` |

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

`dotnet build -c Release` → `E:\code\build\FluxForge.dll`

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
| `ItemMouseDoubleClickHandler.DoubleClickAction` | 双击 | `(GameItem newItem, Vector2 mousePosition)`；有 `contentWindow` 时自动开关窗口 |
| `ItemContextHandler` | 右键菜单 | — |
| `ItemDefaultActivateHandler` | 默认激活（按键） | — |

> `DoubleClickAction` 自带两个分支：`contentWindow != null` → 开关窗口；否则 → `OpenExamineAction`（检视窗）。
> 所以容器/机器类物品只要 `item.SetContentWindow(window)`，双击就自动开关，**不需要 patch**（详见 3.8）。
> 下面这个 patch 只用于追加「双击时做别的事」。

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

`TagState` 字段：`identifier` / `identifierName` / `valueString` / `valueInt` / `valueLong`（仅可写，无 getter）/ `valueFloat` / `valueDouble` / `valueBool`；
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

### 7.9 液体容器（两套存储）

> 这是**游戏原生**的液体机制（可倒、可灌、可喝、有水质等级）。
> 我们自己的装置状态（冰块的水分 / 温度）用自定义 tag（`FLOW_STOCK` / `ICE_TEMPERATURE`），
> 不占用任何原版槽位；发射装置的存量则以真实容器 tag 为唯一真相。
> 不调用液体容器的倒 / 灌 API。

#### 两种装配入口

```csharp
// 水瓶 / 酒瓶：按物质分 tag
WaterHelper.InitLiquidContainerItem(
    GameItem item,
    int capacityInMl,                  // 容量（毫升）——water_jug 是 6000
    int basePrice,
    bool isFilterContainer = false,
    bool isGraduated = false,          // 是否有刻度
    bool isDropper = false,
    bool isTransparentContainer = false,
    int sheetPage = 3);

// 机器 / 简化容器：只记总量
LiquidContainerHelper.InitLiquidContainerItem(
    GameItem item, int capacity = 500, bool isStartFull = true);
```

#### 两套存储（互不相通）

| 容器类型 | 容量 tag | 内容 tag | 备注 |
|---|---|---|---|
| 机器 / 简化容器 | `LIQUID_CONTAINER_MAX` | `LIQUID_CONTAINER_CURRENT` | 总量，不分物质 |
| 水瓶 / 酒瓶 | `LIQUID_CONTAINER_CAPACITY` | `CURRENT_PART_<ID>`，每种物质一个 | `WaterHelper.InitLiquidContainerItem` 写入 |

- 物质 tag 名由 `Liquid.GetCurrentPartFromId(id)` 生成，实际就是 `"CURRENT_PART_" + id.ToUpperInvariant()`
- **两套互不相通**：水瓶上 `LIQUID_CONTAINER_CURRENT` 恒为 0；机器上也不会出现 `CURRENT_PART_*`
- 所以判断容器类型不能只看 `LIQUID_CONTAINER_TAG`，要看它带的是 `LIQUID_CONTAINER_CAPACITY` 还是 `LIQUID_CONTAINER_MAX`

> **修正（10-18）**：旧版本手册把水瓶也写成 `LIQUID_CONTAINER_MAX/CURRENT`。
> 实测 `WaterHelper.InitLiquidContainerItem` 写的是 `LIQUID_CONTAINER_CAPACITY` + 每种物质一个
> `CURRENT_PART_<ID>`；`MAX/CURRENT` 是机器/简化容器那一套。两套字段不通用。

装配时还会写入/设置（与容量存储无关）：

| 载体 | 内容 |
|---|---|
| `LIQUID_CONTAINER_TAG` | 标志（`IsTag` 判定） |
| `LIQUID_CONTAINER_SIPPING` | 吸饮状态（简化版） |
| `GameItemType` | `WATER` |
| 水质数据 / 音效 | 等级与特征 / `AudioHelper.InitDrinkableAudio` |

#### 读取

```csharp
bool isLiquid = item.IsTag("LIQUID_CONTAINER_TAG");

// 机器 / 简化容器
int max     = item.GetTagReadonly("LIQUID_CONTAINER_MAX").valueInt;
int current = item.GetTagReadonly("LIQUID_CONTAINER_CURRENT").valueInt;

// 水瓶 / 酒瓶
int capacity    = item.GetTagReadonly("LIQUID_CONTAINER_CAPACITY").valueInt;
int waterAmount = item.GetTagReadonly(Liquid.GetCurrentPartFromId("WATER")).valueInt;

int value = LiquidContainerHelper.GetLiquidContainerValue(item);
```

> `GetLiquidContainerValue` 内部读的是 `LIQUID_CONTAINER_CURRENT`，只对机器/简化容器有效；
> 水瓶的 `CURRENT` 恒为 0，不能拿它算水瓶价值。

#### 枚举全部物质

**方案 A：遍历物品的 tag 字典，按前缀过滤**（推荐，只列实际存在的物质）：

```csharp
Il2CppSystem.Collections.Generic.Dictionary<string, Il2Cpp.TagState> dict = item.state.dict;

foreach (Il2CppSystem.Collections.Generic.KeyValuePair<string, Il2Cpp.TagState> pair in dict)
{
    if (pair.Key == null || !pair.Key.StartsWith("CURRENT_PART_", StringComparison.Ordinal))
        continue;

    string liquidId = pair.Key.Substring("CURRENT_PART_".Length);
    int amount = pair.Value.valueInt;
}
```

IL2CPP 限制：

- `Dictionary.KeyCollection` **不支持索引器**（`dict.Keys[i]` 编译不过），必须 `foreach` `KeyValuePair`
- `KeyValuePair` 必须用 `Il2CppSystem.Collections.Generic.KeyValuePair<K,V>`，**不是** `System.Collections.Generic.KeyValuePair`
- 遍历时不要增删 tag；要清零就先收集 key，再统一写

**方案 B：遍历 `Liquid.InitLiquid()` 的完整物质表**（需要「所有物质都列出来，包括 0」时用）：
对每一项的物质 id 调 `Liquid.GetCurrentPartFromId(id)` 拼出 tag 名，再逐个读 `valueInt`。

#### 倒空

没有专门的「倒空」API。原版做法是：

```csharp
item.state.GetTag(Liquid.GetCurrentPartFromId(liquidId)).SetInt(0);
WaterFeatureHelper.OnLiquidContainerPourOutOf(item);
SpriteHelper.UpdateWaterContainerSprite(item);
```

顺序：先把物质 tag 清零，再通知液体特性「倒出」，最后刷新容器贴图。
`SetInt` 只写 `valueInt` 槽位，不碰该 tag 的其他槽位。

#### 两个同名方法，参数顺序相反（易错）

```csharp
LiquidContainerHelper.TransferLiquid(GameItem targetItem, GameItem sourceItem)   // (target, source)
WaterHelper.TransferLiquid(GameItem sourceContainer, GameItem targetContainer)   // (source, target)
```

调用前先确认是哪个 helper；两者顺序相反，不能按同一个习惯传参。

---

## 附录：取证速查

| 需求 | 命令 / 位置 |
|---|---|
| 反编译 mod DLL | `ilspycmd xxx.dll -o out/` |
| 反编译 Il2Cpp 程序集 | `ilspycmd MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll -o out/` |
| 查看游戏函数汇编 | `_isil_out/IsilDump/Assembly-CSharp/<类名>.txt` |
| 提取原版贴图 / 尺寸 | UnityPy 读 `Probably Stolen_Data/resources.assets` |
| 游戏路径 | `E:\SteamLibrary\steamapps\common\Probably Stolen Demo` |
