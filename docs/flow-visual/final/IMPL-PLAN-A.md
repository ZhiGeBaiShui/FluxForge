> ⚠ **已作废**：运行时实现以 `REFACTOR-A.md` 与 `IMPL-STATUS.md` 为准。
> 本文描述的是旧「彩色 4896 图集 + 静态 FlowAtlasVisual」方案，代码已删除。

# A 方案实现契约（图集路线 · 运行时）

> 目标：把 `final/render_final.py` 的视觉**不改一个像素**地搬到游戏里。
> 原则：Python 只产图；C# 只做「属性 → key → UV → 1 个 Image」。
> 边界：只动 `src/Presentation/Flow/**`、`src/Mod/Flow/Visual/**`、`assets/flow_atlas.*`、`tests/**`；
> `src/Logic/**` 与物品/补丁层**一行不改**。

## 1. 运行时图集

| 项 | 值 |
|---|---|
| 文件 | `assets/flow_atlas.png`（必须放 assets 顶层，csproj 自动嵌入） |
| 索引 | `assets/flow_atlas.json` |
| tile | 16×16 RGBA，透明背景，NEAREST，无任何文字/标签 |
| 布局 | 每行 16 个；`x = (i%16)*16`，`y = (i//16)*16` |
| 内容 | ① 静态：`comp|temp|content|state|dir` = 6×3×4×5×4 = **1440** 张 ② 动效：`comp|temp|content|flow|dir|f0..f11` = 6×3×4×4×12 = **3456** 张。合计 4896 张（256×4896 px，约 5 MB 显存） |
| 射程点 | **不进图集**，运行时用 3 个 1×1 白点 Image 着色叠加（无数据时隐藏） |
| 索引格式 | `{"tiles": {"key": [x,y], ...}}`，key 全小写、`|` 分隔 |

**枚举字符串**：comp = water/poison/oil/blood/acid/energy；temp = cold/normal/hot；
content = empty/small/mid/large；state = flow/blocked/split/merge/collect；dir = up/right/down/left。

## 2. C# 文件

### 纯逻辑（`src/Presentation/Flow/`，零 Unity，可单测）

| 文件 | 职责 |
|---|---|
| `FlowVisualState.cs` | struct：Comp/Temp/Content/State/Dir/Frame/Range；`ToKey()` |
| `FlowAtlasIndex.cs` | 解析索引 JSON；`TryGet(key, out x, out y)`；带 fallback（state→flow、content→large） |
| `FlowTierMap.cs` | 数据 → 枚举：温度阈值、Moisture → content 四档、状态判定（见 §4） |
| `FlowPixelMath.cs` | 格内像素 → 局部坐标；四边取整；`Quad(x,y,w,h)` 结构 |

### Unity 层（`src/Mod/Flow/Visual/`）

| 文件 | 职责 |
|---|---|
| `FlowAtlasTexture.cs` | 反射 `ImageConversion.LoadImage` 载入 `flow_atlas.png`；`FilterMode.Point`、`DontSave`；解析 json；懒创建 Sprite 缓存 |
| `FlowAtlasLayer.cs` | 每块网格一个 GameObject（挂网格下、SetAsLastSibling）；每流源 1 个 body Image + 3 个 pip Image；池化；`Submit(IReadOnlyList<FlowDrawItem>)` |
| `FlowAtlasVisual.cs` | 静态桥：RegisterGrid/UnregisterGrid/LateUpdate；订阅 FlowSpawner；读 FlowEntity；调 Layer |

### 接线（只改 `FlowVisual.cs` 三处，最小 diff）

1. `RegisterGrid` 末尾：`FlowAtlasVisual.RegisterGrid(grid, inventory, flows);`
2. `UnregisterGrid` 开头：`FlowAtlasVisual.UnregisterGrid(grid);`
3. `LateUpdate` 末尾：`FlowAtlasVisual.LateUpdate();`

> 旧的粒子路径先保留但不再发射（`Field` 不再 Emit），作为回退；确认新层稳定后再删。

## 3. 绘制规则

- 每流源 1 个 body Image：`sprite = cache.Get(key)`；`rectTransform.sizeDelta = 16×16 × ppu/16`；
  位置 = 格中心（格坐标 × 16 → 局部），四边取整到屏幕像素后再除回（见 `FlowPixelMath`）。
- 3 个 pip Image：1×1 白 sprite，`color = mix(compColor, white, 0.75)` 或暗色；
  位置 `x=4/6/8, y=14`（格内单位）；range 未知时 `SetActive(false)`。
- 动效：flow 状态用帧 key（12fps，时钟由 Layer 维护）；其它状态用静态 key。
- 层序：Layer 根节点 `SetAsLastSibling()`；pip 在 body 之后创建（更上层）。
- 面板关闭：父节点失活自动隐藏；`activeInHierarchy == false` 时不更新 sprite。

## 4. Tier-0（今天就能跑）

| 视觉输入 | Tier-0 来源 |
|---|---|
| 成分 | 固定 water |
| 温度 | `Properties.Temperature`：≤5 cold；≥60 hot；否则 normal |
| 含量 | `Properties.Moisture`（µL）：< 1 000 empty；< 5 000 000 small；< 20 000 000 mid；否则 large |
| 方向 | `Motion.Direction`（0上/1右/2下/3左） |
| 状态 | 位置 2 帧未变且 Speed>0 → blocked；OnEntityRemoved → collect（画一帧后销毁）；否则 flow |
| 射程 | 无数据 → 隐藏 pips |
| 帧 | 12fps 时钟，flow 用 |

主轨（字段获批后）：Composition 取最大分量（<0.5 用中性水色）、RangeRemaining → 3/2/1、State 直接映射。

## 5. 验收

1. `dotnet build -c Release` 0 error。
2. `dotnet test`：新增测试全绿（key/索引/阈值/像素对齐）。
3. 图集自检：`python tools/flow-visual/verify_runtime_atlas.py` 抽样 20 个 key，与 `render_final.py` 的 `render()` 逐像素比对一致。
4. 视觉：把图集里 20 个 tile 拼一张对照图，与 `final/png/` 对拍（像素差 0）。
5. 游戏内：**不由我部署**；构建产物路径 + SHA256 交给用户自己测。

## 6. 预算

- 每流源 4 个 Image（1 body + 3 pips）；10 条流源 = 40 个 Graphic，只在换帧/换档时重建。
- 稳态 0 GC：key 用预分配 char 缓冲或 string.Intern？先用 `string.Concat` 但缓存；
  更稳的做法是 `FlowVisualState` 用整数枚举 + 预生成 4896 个 key 字符串（一次性）。
- 纹理显存约 5 MB；Sprite 缓存按需创建。
