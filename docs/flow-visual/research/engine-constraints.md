# 流源视觉 · 引擎技术约束分析

> 读者：视觉设计代理 / 实现工程师。
> 目的：把「这套新视觉能怎么被画出来、代价多少」讲成不用猜的数字，让设计在动笔前就知道边界。
> 方法：只读源码 + 本机程序集元数据。**未改动任何源码。**
> 引用格式 `路径:行`；标 **未验证** 的是没有运行时实测的推断。所有数字可由同目录 `measure_budget.py` 复算。

---

## 0. 一页结论

| 问题 | 结论 | 依据 |
|---|---|---|
| 单 quad 成本 | 4 顶点 + 6 索引 = **444 B**（顶点 432 B + 索引 12 B） | `FlowParticleGraphic.cs:139-145`；UIVertex 布局见 §3.1 |
| 64 quads 有多大 | **27.8 KiB** 顶点数据，1x 下等于 64 px² = **1/4 格** | `measure_budget.py` |
| 64 quads 现实吗 | **现实**：18 个语义块就能覆盖 6 条信息，余量 3.6 倍；但 **16×16 逐像素画 = 256 quads，超 4 倍** | §3.4 |
| 一次重建多少 draw call | 1 个 Graphic = **1 次重建 = 1 个 draw call**（同材质同图集） | §3.5 |
| 当前实现多贵 | 满含量 1 源 @60fps = **840 quads**（64 的 13 倍）；1024 粒上限 = **2048 quads / 888 KiB** | `DefaultEmissionPolicy.cs:53,73` + `FlowParticleField.cs:51` |
| 最大技术风险 | 自定义 Graphic 未注册 `ClassInjector` → 大概率走回退；「一 quad 一 Image」的降级安全预算只有 **≤12 quads/源**，分层 Image（每源 2–4 张）才是可行降级 | §5.1、§5.6、§9 |
| 最大设计风险 | **成分 / 射程 / 状态枚举在逻辑层没有字段**（不是"读得到但没接"，是根本不存在） | §6.4 |
| 数据双轨 | 主轨按完整契约设计（6 成分/射程/状态）；Tier-0 降级只能表达温度/含量/方向/运动/出生消失 | §6.4 |
| 实现契约 | **主轨 = 自绘 Graphic 单层**（1 重建/网格）；**降级轨 = 每源 2–4 个 Image + 运行时生成图集**（tint 仅在档位变化时改） | §9 |

**一句话**：纯顶点最多做到「每流源 64 个轴对齐色块」（8×8 逐像素块，或 18–24 个语义块 + 动画余量），一次重建、一个 draw call、每帧 0 分配；**16×16 逐像素画、柔边、发光都超出顶点预算，必须改用 1 张图集贴图（1 quad + UV），而不是加顶点。**

---

## 1. 可用绘制手段表

### 1.1 顶点矩形：`MaskableGraphic.OnPopulateMesh` + `VertexHelper`

| 项 | 内容 |
|---|---|
| 能力 | 任意**轴对齐**实心矩形；逐 quad 顶点色（RGBA）；一次 `OnPopulateMesh` 画完整层；UV 全 0 时是纯色（默认 UI 材质的白纹理） |
| 每次重建成本 | N quads → 4N 次 `AddVert` + 2N 次 `AddTriangle`；顶点缓冲 4N×108 B、索引 6N×2 B；**每个 Graphic 每帧最多重建一次**（CanvasUpdateRegistry 去重） |
| 已知坑 | ① `vh.Clear()` 必须自己调（`FlowParticleGraphic.cs:57`）；② 数据必须**拷一份**，因为重建时机由 Unity 决定、可能延后到帧末（`FlowParticleGraphic.cs:18-23`）；③ 改完必须 `SetVerticesDirty()`（`:46`），漏了就是"数据变了但画面不动"；④ `raycastTarget=false`（`FlowParticleLayer.cs:84`），否则透明 quad 也会吃点击；⑤ 半透明 quad 叠加是 `SrcAlpha OneMinusSrcAlpha`，叠 3 层以上会发脏、发灰（见 §1.4）；⑥ 顶点色是 `Color32`，alpha 量化 1/255（`FlowParticleGraphic.cs:132-134`）；⑦ 不能旋转、不能抗锯齿——这是 C1 的要求，不是引擎缺陷 |
| IL2CPP 注意事项 | 本游戏程序集里 `UnityEngine.UI.Graphic.OnPopulateMesh(VertexHelper)` 是 **public virtual**（两个重载都是），`MaskableGraphic` 没有覆写它 → 继承时**必须写 `public override`**；写 `protected override` 会 CS0507 编译失败（改访问级别）。`FlowParticleGraphic.cs:49-55` 的注释与此一致。元数据核验方法见 §5.1。`VertexHelper` 是 Il2Cpp 侧类型，mod（Mono/net6）调用它是**跨运行时调用**，单次比普通托管调用贵（未实测），所以 quad 数直接等于 CPU 成本 |

**代价换算**：64 quads = 384 次跨运行时调用 ≈ 15 µs 量级（按 40 ns/次假设）；2048 quads = 12,288 次 ≈ 0.5 ms 量级，外加上传 888 KiB。60fps 的整帧预算只有 16.7 ms。

### 1.2 `Image` 池

| 项 | 内容 |
|---|---|
| 能力 | 每粒/每块一个 UGUI `Image`；可给 sprite（或留空 = 白块），可逐块改色/改位置/改尺寸；不需要类型注入，**IL2CPP 下最稳** |
| 每次重建成本 | **每个 Image 是一个独立 Graphic**：改 color/rect 就把它标脏，一次重建一次 CanvasRenderer 提交。N 个 Image = **N 次重建**（不是 1 次）；顶点仍是 4N/6N；同 sprite 同材质可合成 1 个 draw call，但重建开销不合并 |
| 已知坑 | ① `SetActive(true)` 每帧对已激活对象调用（`FlowParticleLayer.cs:168`）是纯浪费；② 池对象被销毁后 `_pool[i]` 是假 null，必须 `if (image == null)` 重建（`:162-166`）；③ 锚点/pivot 必须显式设成左上角，否则 §2 的公式对不上（`:219-221`）；④ Image 自带 `MaskableGraphic` 的 `OnPopulateMesh`，无 sprite 时画白块——想画"纯色方块"不需要贴图；⑤ 池容量 = 粒子上限（`:29`，1024），全开时是 1024 个 GameObject + CanvasRenderer |
| IL2CPP 注意事项 | `Image` 是游戏自带 UI 程序集里的类型，`AddComponent<Image>()` 一直可用（`FlowParticleLayer.cs:224`），不需要注册；这是当前代码的**兜底路径**（`:20-22`、`:93-99`）。反过来说：如果设计需要 64 quads/源，而实际走的是这条路径，64 个 Image/源 × 4 源 = 256 个 Graphic 每帧重建，**不可接受** |

**结论**：Image 池只适合「≤12 quads/源、≤2 源/网格」的降级形态。它不能承载 64 quads 的设计。

**补充（决定两条路线怎么选）**：Image 改 `anchoredPosition` **不会**触发网格重建（顶点在局部空间没变，只更新 transform 矩阵）；改 `Image.color` 会 `SetVerticesDirty` → **每帧 tint = 每帧每 Image 一次重建**。所以分层 Image 路线必须把 tint 改成「只在档位/状态变化时赋值」，否则成本比自绘层还高。纯色 Image 不需要 sprite；一旦要轮廓/形状差异，就必须有 sprite（§1.3）。

### 1.3 `Texture2D.LoadImage` + `Sprite.Create`

| 项 | 内容 |
|---|---|
| 能力 | 真正的像素画/图集：一张 PNG 里放任意多 tile，每 tile 用 1 个 quad（UV 指向图集）或 1 个 Image 画出来。**16×16 像素画从 256 quads 降到 1 quad** |
| 每次重建成本 | 加载是**一次性**：PNG 解码 + 上传，之后只读缓存（`FlowSpriteRegistry.cs:48-70`）。显存 = 4 B/px（RGBA32）：64×64 = 16 KiB，128×128 = 64 KiB，256×256 = 256 KiB。重建时每个 tile 仍是 1 quad |
| 已知坑 | ① IL2CPP 下不能直接调 `Texture2D.LoadImage`，必须反射 `ImageConversion.LoadImage`，参数类型是 `Texture2D` + `Il2CppStructArray<byte>`（`FlowSpriteRegistry.cs:23-26`、`:158-172`）；② 必须 `FilterMode.Point`（`:169`），否则像素画被双线性糊掉；③ 必须 `hideFlags = DontSave`（`:171`、`:179`），否则切场景/资源回收会把运行期贴图卸掉；④ `Sprite.Create` 的 ppu 要自己算，否则 `Image` 的 native size 不对（`:174-178`）；⑤ 每张**不同贴图**会打断 UI 合批 → 多 1 个 draw call，所以**必须一张图集**；⑥ 运行期创建的 Sprite 不进图集打包，`Image` 用 `type=Sliced/Tiled` 时边界不可控，建议 `Simple` + 自己算 sizeDelta；⑦ 资源入口只有 `assets/*.png`（**顶层，不含子目录**，`ProbablyBagMod.csproj:33`）→ 图集放 `assets/flow_atlas.png` 即自动嵌入，不用改 csproj；⑧ 反射 `Invoke` 会分配 `object[]`，**只能初始化时调一次** |
| IL2CPP 注意事项 | 反射 `GetMethod` 必须匹配精确签名，取不到就返回 null 并降级（`:24-26`、`:158-161`），这条降级路径已经在生产代码里验证过；`(Il2CppStructArray<byte>)png` 是隐式转换，会做一次托管→原生的数组拷贝（一次性，可接受） |

### 1.4 默认 UI 材质（`Graphic.defaultGraphicMaterial`）

| 项 | 内容 |
|---|---|
| 能力 | `UI/Default`：顶点色 × `_MainTex`，`Blend SrcAlpha OneMinusSrcAlpha`，`ZWrite Off`、`Cull Off`，支持 Mask/RectMask2D。纯色 quad 与贴图 quad 共用同一 shader |
| 每次重建成本 | 材质本身 0 成本；**不要每帧换材质**。同材质 + 同贴图 = 同批次 |
| 已知坑 | ① **绝对不要 `Shader.Find`**：IL2CPP 打包后 shader 会被裁剪，按名字找常拿到 null，整层直接不显示（`FlowParticleLayer.cs:86-88` 明确记录了这个坑）；② `Graphic.defaultGraphicMaterial` 是**全局共享**材质，直接改它的 `mainTexture` 会污染所有默认 UI → 需要贴图时只能 `new Material(Graphic.defaultGraphicMaterial)` **克隆一次**（每层一个），并在 `Destroy()` 里销毁；③ UI/Default 没有加色混合、没有 Bloom、没有径向渐变——「发光」只能靠叠半透明 quad 或预烘到贴图 alpha；④ 叠层混合是直通 alpha：3 层 50% 白叠起来 ≈ 87% 白，会把成分色洗掉；⑤ 如果项目是 Linear 色彩空间，顶点色可能被转成线性，**屏幕取色会和设计稿不一致**（未验证本项目色彩空间） |
| IL2CPP 注意事项 | `Graphic.defaultGraphicMaterial` 属性存在（元数据已核验）；克隆材质走 `new Material(Material)`，不需要 `Shader.Find`。`UI/Default` 只用 uv0，所以 `Canvas` 的 `additionalShaderChannels` 默认值够用；要 uv2/uv3 才需要改它（本设计不需要） |

### 1.5 手段对比总表

| 手段 | 1 源 64 quads 可行 | draw call | 每次重建 | 主要风险 |
|---|---|---|---|---|
| 顶点矩形（自绘 Graphic） | ✅ | 1/层 | 1 次/层 | 类型注入失败 → 回退 |
| Image 池 | ❌（64 个 Graphic） | 1（同图同材质） | 64 次/源 | 重建开销爆炸 |
| 贴图 + UV quad | ✅（1 quad/tile） | 1/层（单图集） | 1 次/层 | 需要一张图集 + 克隆材质 |
| 默认 UI 材质 | ✅（前两者共用） | — | — | 不能改共享材质 |

---

## 2. 像素对齐公式

### 2.1 坐标系事实（先对齐认知）

- 格宽 16 px（`FlowVisual.cs:28`、`FlowParticleLayer.cs:26`；BRIEF §1）。
- 网格 `RectTransform.rect` 的局部坐标：`xMin` 是左、`xMax` 是右、`yMin` 是下、`yMax` 是上（UGUI 的局部 +Y 向上）。
- 格坐标 **Y 向下**：格 (0,0) 在网格**左上角**；所以 Y 用 `area.yMax` 往下减（`FlowParticleGraphic.cs:121-122`、`FlowVisual.cs:422-424`）。
- 整数格坐标 = **格边界**，不是格中心：运动层用 `Math.Floor` 切格、撞停点取 `cell + eps`（`FlowManager.cs:51`、`:89-92`、`:143-156`）。渲染层再 +0.5 格得到格中心（`FlowParticleGraphic.cs:111`、`FlowVisual.cs:407-409`）。
  ⚠️ 文档不一致：`FlowPos.cs:12` 写「整数点为格中心」，与 `FlowPos.cs:15-16` 自己承认的半格偏移、以及 `FlowManager` 的 Floor 语义矛盾。**以实现为准：整数 = 格边界**；`FlowPos.cs:12` 那句是过时注释（未验证是否会在后续被修）。
- `lossyScale.x` = 1 局部单位对应多少屏幕像素（`FlowParticleGraphic.cs:68`、`FlowParticleLayer.cs:146`）。Screen Space Overlay 下就是 Canvas 的 scaleFactor；Screen Space Camera 下还含相机缩放。

### 2.2 完整换算链

```
格内设计像素 (cx, cy, ox, oy, w, h)
   │  ① 格 → 网格局部：lx = area.xMin + cx*16 + ox
   │                     ly = area.yMax - cy*16 - oy        （Y 向下）
   ▼
网格局部坐标 (lx, ly)
   │  ② 局部 → 屏幕像素：px = (lx * ppu) + X0
   │                       py = (ly * ppu) + Y0             （X0,Y0 = 网格在屏幕上的原点）
   ▼
屏幕像素
   │  ③ 四条边各自 Mathf.Round()
   ▼
整数像素 (l, t, r, b)
   │  ④ 像素 → 局部：l/ppu, t/ppu, r/ppu, b/ppu
   ▼
写进 VertexHelper 的 Vector3
```

**为什么四条边各自取整**：若只取整中心再取整边长，ppu 不是整数时会出现 .5 像素；若只取整左上角，右边和下边会落在分数像素上（`FlowParticleGraphic.cs:124-130` 取整了 left/top 但右边由 `left + sizePx/ppu` 得到，sizePx 是整数像素，所以在像素空间里右边也是整数——两种写法在「尺寸先取整」的前提下等价）。四条边独立取整的代价是：**当 `16 * ppu` 不是整数时，相邻格的宽可能差 1 px**。像素风里这不可见，但会让「每格 1px 的描边」在个别格上变成 2px。

### 2.3 可直接复制到 C# 的公式

```csharp
using UnityEngine;
using UnityEngine.UI;

/// 16px 格 → 屏幕像素 → RectTransform 局部坐标。直接复制可用（net6 / C# 10）。
public static class FlowPixel
{
    public const float Cell = 16f;                 // 1 格 = 16 局部单位

    public static float Ppu(RectTransform rt)
    {
        float s = rt.lossyScale.x;                 // 画布缩放
        return s > 0f ? s : 1f;                    // 防御：失活/未布局时可能是 0
    }

    /// 格内矩形 → 局部坐标（左下、右上）。ox/oy/w/h 都以「1x 像素」为单位。
    public static void RectLocal(
        Rect area, float ppu, int cx, int cy, float ox, float oy, float w, float h,
        out Vector2 bottomLeft, out Vector2 topRight)
    {
        float lx = area.xMin + cx * Cell + ox;     // ① 格 → 局部 X（向右）
        float ly = area.yMax - cy * Cell - oy;     // ① 格 → 局部 Y（向下，所以从 yMax 减）

        float l = Mathf.Round(lx * ppu);           // ②③ 局部 → 像素，四条边各自取整
        float t = Mathf.Round(ly * ppu);
        float r = Mathf.Round((lx + w) * ppu);
        float b = Mathf.Round((ly - h) * ppu);

        bottomLeft = new Vector2(l / ppu, b / ppu); // ④ 像素 → 局部
        topRight   = new Vector2(r / ppu, t / ppu);
    }

    /// 往 VertexHelper 里塞一个 quad。UV 传 0 = 纯色；贴图时传图集 UV。
    public static void AddQuad(VertexHelper vh, Vector2 bl, Vector2 tr, Color32 c,
                               Vector4 uv0 = default)
    {
        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(bl.x, bl.y), c, uv0);   // 左下
        vh.AddVert(new Vector3(bl.x, tr.y), c, uv0);   // 左上
        vh.AddVert(new Vector3(tr.x, tr.y), c, uv0);   // 右上
        vh.AddVert(new Vector3(tr.x, bl.y), c, uv0);   // 右下
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}
```

调用示例（画格 (3,5) 内、距左上 2px、8×8 的方块）：

```csharp
Rect area = rectTransform.rect;                 // 每帧取一次，不要每个 quad 取
float ppu  = FlowPixel.Ppu(rectTransform);
FlowPixel.RectLocal(area, ppu, 3, 5, 2f, 2f, 8f, 8f, out var bl, out var tr);
FlowPixel.AddQuad(vh, bl, tr, new Color32(0x2F, 0xE0, 0xC8, 0xFF));
```

### 2.4 Image 路径的等价写法（回退路径必须逐字一致）

```csharp
// 锚点与 pivot 都设成左上角（FlowParticleLayer.cs:219-221）
img.rectTransform.anchoredPosition = new Vector2(bl.x - area.xMin, tr.y - area.yMax);
img.rectTransform.sizeDelta        = new Vector2(tr.x - bl.x, tr.y - bl.y);
```

来源 `FlowParticleLayer.cs:184-188`。两条路径必须用同一个 `FlowPixel.RectLocal`，否则面板一缩放就会「自绘层和回退层错半像素」（历史 bug，`:170-172` 注释）。

### 2.5 三个陷阱

1. **ppu 不是整数**：`16 * 1.1 = 17.6`，相邻格宽会在 17/18 px 之间跳。像素风要求 `16 * ppu` 为整数（1.0/1.5/2.0/2.5/3.0 都可以）。**未验证**本游戏 Canvas 的 scaleFactor 是否满足；建议实现时打一行日志 `area.size / ppu`，确认后删掉。
2. **全局原点**：公式只保证「同一层内所有 quad 共享同一整数像素网格」，不保证和游戏自身像素网格对齐——那取决于网格 RectTransform 的屏幕原点 `X0/Y0` 是否为整数（面板动画、居中布局都可能给出 .5）。这属于「看起来差半像素」的观感问题，不是功能问题；不要试图用 `WorldToScreenPoint` 去修正（Overlay 画布没有相机，且会引入浮点误差）。
3. **网格 rect 是否真的等于 16×列数**：若原版网格带内边距，公式会整体漂移。实现时断言一次 `Mathf.Abs(area.width - 16f * columns) < 0.5f`，不成立就 log error（**未验证** `GameGridInventory.rectTransform.rect` 的实际尺寸；`FlowVisual.cs:451` 只取了 rect，没有校验）。

---

## 3. 预算：实测/推算

### 3.1 单位成本

| 项 | 值 | 来源 |
|---|---|---|
| 1 quad | 4 顶点、6 索引、2 三角形 | `FlowParticleGraphic.cs:139-145` |
| UIVertex 大小 | **108 B** = position 12 + normal 12 + tangent 16 + color32 4 + uv0..uv3 4×16 | 由 `VertexHelper.AddVert` 重载签名推断（uv 是 `Vector4`），见 §5.1；**未用 `Marshal.SizeOf` 实测** |
| 1 quad 顶点数据 | 432 B + 12 B 索引 = **444 B** | 计算 |
| 64 quads | 27,648 B 顶点 + 768 B 索引 = **27.8 KiB** | `measure_budget.py` |
| 1 格 | 16×16 = 256 px²；64 个 1×1 quad 只占 1/4 格 | 计算 |

### 3.2 场景预算

| 场景 | quads | verts | 顶点字节 | 合计 |
|---|---|---|---|---|
| 单流源 C7 上限 | 64 | 256 | 27,648 | 27.8 KiB |
| 建议的静态构成（§7 建议 1） | 18 | 72 | 7,776 | 7.8 KiB |
| 当前实现：低含量 1 源（7 粒） | 14 | 56 | 6,048 | 6.1 KiB |
| **当前实现：满含量 1 源 @60fps（420 粒）** | **840** | 3,360 | 362,880 | **364 KiB** |
| 当前实现：单网格 1024 粒上限 | 2,048 | 8,192 | 884,736 | 888 KiB |
| 新设计：20 源 × 64 quads（同一层） | 1,280 | 5,120 | 552,960 | 555 KiB |
| 新设计：4 块网格 × 512 quads | 2,048 | 8,192 | 884,736 | 888 KiB |

### 3.3 当前 1024 粒子上限的最坏情况

当前策略：每帧 10 粒、寿命 0.7 s（`DefaultEmissionPolicy.cs:53`、`:73`），每粒 2 个 quad（本体 + Softness 外圈，`FlowParticleGraphic.cs:95-103`，`ParticleStyle.cs:31`、`DefaultEmissionPolicy.cs:78`）。

| 帧率 | 单源存活 | 单源 quads | 单源顶点字节 | 几个源打满 1024 |
|---|---|---|---|---|
| 30 | 210 | 420 | 181,440 | 4.9 |
| 60 | 420 | 840 | 362,880 | 2.4 |
| 90 | 630 | 1,260 | 544,320 | 1.6 |
| 120 | 840 | 1,680 | 725,760 | 1.2 |
| 144 | 1,008 | 2,016 | 870,912 | 1.0 |
| 240 | 1,024（封顶） | 2,048 | 884,736 | 1.0 |

**结论**：1024 不是"给多条流源的余量"，**144 Hz 下单条满含量流源就能打满**。上限是全局淘汰（`FlowParticleField.cs:148-151`），表现为「密度封顶、老粒子被瞬间删除」，也就是 BRIEF 说的「读成掉帧」。新设计若沿用粒子思路，必须先把 quads/源 压到 64 以下，即 **每秒发射量 × 寿命 × 2 ≤ 64** → 60fps 下「每帧 ≤ 0.76 粒」或「寿命 ≤ 0.05 s」，都不可读。

### 3.4 「单流源 ≤ 64 quads」是否现实

**是现实的，但不是像素画。**

| 想画的东西 | 需要 quads | 判定 |
|---|---|---|
| 16×16 逐像素精灵 | 256 | ❌ 超 4 倍 |
| 8×8 逐像素块 | 64 | ⚠️ 刚好用满，没有动画余量 |
| 4×4 个 2×2 像素块（= 8×8 覆盖） | 16 | ✅ 余 48 |
| 12×12 图标外 1px 框 | 4 | ✅ |
| 12×12 全填充 | 1 | ✅ 但违反 C5（盖住图标） |
| 18 个语义块（§7 建议 1） | 18 | ✅ 余 46 |

**结论**：64 quads 能承载「几何/符号/分段/框架」语言，静态用 18–24 个、动画峰值 ≤48 个；**承载不了逐像素手绘**。逐像素必须走 §1.3 的贴图路线：一张图集里每个 16×16 tile 用 **1 quad + UV** 画出来，quads 反而降到 1。

### 3.5 一次 UI 重建的成本

- 触发：`Submit` → `SetVerticesDirty()`（`FlowParticleGraphic.cs:46`）→ CanvasUpdateRegistry 在渲染前统一重建 → `OnPopulateMesh` → `FillMesh` → `CanvasRenderer.SetMesh`。
- **同一个 Graphic 每帧最多重建一次**，无论你调多少次 `SetVerticesDirty`。
- 成本模型：`N × (4 次 AddVert + 2 次 AddTriangle)` 次跨运行时调用 + 顶点拷贝 + GPU 上传。

| 规模 | 调用次数 | 假设 40 ns/次 | 顶点内存 |
|---|---|---|---|
| 18 quads | 108 | ~4 µs | 7.8 KiB |
| 64 quads | 384 | ~15 µs | 27.8 KiB |
| 512 quads | 3,072 | ~0.12 ms | 221 KiB |
| 2,048 quads | 12,288 | ~0.5 ms（+888 KiB 上传） | 888 KiB |

`40 ns` 是「Mono→IL2CPP 跨运行时调用 + `List.Add`」的量级假设，**未实测**；真实值请用 Profiler 打点（`Profiler.BeginSample("FlowPopulateMesh")` 包住 `OnPopulateMesh`）。

**对照**：Image 池路径下 64 个 Image = 64 次 `OnPopulateMesh` + 64 次 CanvasRenderer 提交，即使每次只要 ~1 µs，也已经是自绘路径的 4 倍，且随源数线性增长。

---

### 3.6 每源预算表与两条实现路线

#### A. ≤48 quads 的可行构造（自绘 Graphic）

| 构造 | 构成 | quads | 说明 |
|---|---|---|---|
| 极简 | 主体 1 + 内框 4 + 含量 2 + 温度 2 + 方向 1 + 射程 2 | 12 | 1x 可读，4x 细节少 |
| **标准（推荐）** | 主体 1 + 1px 内框 4 + 含量 3 段 3 + 温度 3 pips 3 + 方向头 3 + 射程 4 缺口 4 | **18** | §7 建议 1 |
| 富裕 | 标准 18 + 成分 3 条带 + 高光 2 + 状态角标 4 + 动画头 8 | 35 | 仍在 48 内 |
| 叠层发光 | 标准 18 × 2 层（外圈淡色） | 36 | 叠 2 层 alpha 会洗色（§1.4） |
| 像素画 8×8 | 逐像素块 | 64 | 用满、无动画余量 |
| 像素画 16×16 | 逐像素块 | 256 | **不可行**，超 4 倍 → 走贴图 |

≤48 quads/源时：顶点 ≤192、顶点数据 ≤20.7 KiB；20 源同层 = 960 quads / 405 KiB / **1 次重建**。

#### B. 两条路线的成本对比

| | 自绘 Graphic 单层 | 分层 Image（每源 2–4 张） |
|---|---|---|
| quads/源 | 12–48 | 2–4 |
| 重建次数/帧 | **1 / 网格**（所有源合并） | 2–4 / 源，且**仅在 tint 变化时** |
| draw call | 1 / 网格 | 1 / 网格（同一图集 + 同材质才成立） |
| 每帧分配 | 0（warmup 后） | 0（`Color32` 值类型） |
| 运行时生成贴图 | 不需要（可选 1 张图集） | **需要**：`SetPixels32` + `Apply` + `Sprite.Create`（§5.6.2，已核验可行） |
| 像素对齐代码 | 一套 | 一套（但 Image 的 pivot/锚点必须同源，否则历史 bug 复现） |
| 源数上限 | 受顶点/重建影响小（20 源 960 quads 仍 1 次） | 约 30 源（60–120 个 Graphic）后 transform/重建开始明显（未实测） |
| 主要风险 | `ClassInjector` 注入失败（§5.6.1） | 源多时 Graphic 数线性增长；每源 GameObject 生命周期 |

**降级轨的贴图成本**：16×16 RGBA32 = 1 KiB/张；64×64 图集 = 16 KiB，可放 16 个 16×16 tile；一次性生成、缓存。每帧只改 `anchoredPosition`（不重建）与档位变化时的 `color`（重建 1 次）。

---

## 4. 时钟与生命周期

### 4.1 三个时钟入口

| 入口 | 调用链 | 用途 |
|---|---|---|
| `OnUpdate` | MelonMod → `FlowDriver.Update(Time.deltaTime)`（`PBModEntry.cs:51-54`） | 推进逻辑位置、碰撞（`FlowDriver.cs:27-57`、`FlowManager.cs:42-59`） |
| `OnLateUpdate` | MelonMod → `FlowVisual.LateUpdate()`（`PBModEntry.cs:56-59`） | 同步壳位置、发射粒子、推进粒子、提交顶点（`FlowVisual.cs:129-145`、`:178-257`） |
| `Time.deltaTime` | 帧间隔（受 `timeScale` 影响） | 速度、寿命、行波相位都以它为准（`FlowParticleField.cs:82-108`、`FlowMotionData.cs:59-63`） |

**为什么视觉同步必须在 `LateUpdate`**：逻辑帧结束时游戏 UI 还没排版完，那一刻算出的坐标会被本帧布局覆盖（`FlowVisual.cs:19-21`、`FlowManager.cs:15-17`）。设计不需要关心这个，但实现者不能把位置计算搬到 `Update`。

**暂停/时间缩放**：`deltaTime == 0` 时 `Advance` 直接返回（`FlowParticleField.cs:84-87`），粒子冻结、不老化；`Time.timeScale = 0` 时整个视觉静止。若设计里有「呼吸/脉动」动效，它也会一起停——这是正确的（暂停时不该动），不要改用 `unscaledDeltaTime`。

### 4.2 面板关闭时（`activeInHierarchy == false`）

| 行为 | 事实 | 依据 |
|---|---|---|
| 隐藏 | 壳/粒子层挂在网格 RectTransform 下，父节点失活 → 整层自动隐藏，不需要额外代码 | `FlowVisual.cs:11-15`、`FlowParticleLayer.cs:12-13` |
| 停止发射 | 显式检查 `shell.Rect.gameObject.activeInHierarchy`，关闭期间不发射，避免重开瞬间糊成一片 | `FlowVisual.cs:210-215` |
| 继续老化 | `Advance` 与 `Submit` 仍对每个已登记网格执行（遍历 `_grids`，不是 `_shells`） | `FlowVisual.cs:231-256` |
| 不重建 | `Graphic.SetVerticesDirty()` 在对象失活时直接返回（UGUI 行为，**未在本机逐行核对**），所以隐藏期间只有列表拷贝、没有顶点重建 | `FlowParticleGraphic.cs:46` |
| 重开 | `OnEnable` → `SetAllDirty()` → 用当前列表重建；若列表已空则画不出东西（正确） | UGUI 行为 |
| 浪费 | 隐藏期间每帧仍拷贝一次粒子列表（1024 个 struct ≈ 57 KiB memcpy）。建议：`if (!activeInHierarchy) { 记一个 dirty 标志; return; }`，重开时补一次 `Submit` | 优化项，非必须 |

**设计含义**：不要在「面板关闭」时保留任何状态动画（例如"关面板时流源继续走"）；视觉层的生命周期完全跟随网格。

### 4.3 销毁与「假 null」

- `UnityEngine.Object` 派生类型（`RectTransform`、`Image`、`Material`、`GameObject`）在原生对象销毁后，`== null` 返回 **true**，但字段访问会抛 `MissingReferenceException`。代码里依赖这个语义：`shell.Rect == null`（`FlowVisual.cs:190`）、`_root == null`（`FlowParticleLayer.cs:109`）、`_graphic != null`（`:121`）。
- 纯 C# 类（`FlowParticleLayer`、`FlowParticleField`、`FlowVisual.Shell`、`GridVisual`）**不会**变假 null；它们内部的 Unity 引用才会。所以「层还在、根没了」是正常中间态，`Submit` 第一件事就是查 `_root`。
- `Object.Destroy` 是**延迟到帧末**执行：同一帧内刚销毁的壳仍可能被绘制一次。设计上不要依赖「销毁立即消失」；要做消失动画，就在视觉层自己延迟 N 帧再销毁（当前是收到 `OnEntityRemoved` 立刻销毁，`FlowVisual.cs:388-402`）。
- 异常兜底：`LateUpdate` 整帧被 try/catch 包住，只记一次错误（`FlowVisual.cs:133-144`）；单个壳抛异常不会让视觉层永久停摆，但会**整帧跳过同步**。实现新绘制代码时不要在里面做可能抛的操作（例如访问已销毁对象的 `.rect`）。

### 4.4 重建时机

`OnPopulateMesh` 可能在**一帧内被调用多次**（布局变化、Canvas 重建、动画组件标脏）。因此：

1. 它必须是**只读**的：只读快照、只写 `vh`，不能在里面改游戏状态/发事件/分配大对象。
2. 快照必须是**拷贝**：`FlowParticleGraphic.cs:18-23` 的 `_particles` 拷贝就是为此存在；直接持有 `FlowParticleField.Particles` 会被下一帧就地改写。
3. 不要在 `OnPopulateMesh` 里调 `SetVerticesDirty`（递归标脏）。

---

## 5. IL2CPP 约束

> 本节结论来自对本机 `MelonLoader/Il2CppAssemblies` 与 `MelonLoader/net6` 的**程序集元数据读取**（Mono.Cecil，2026-10-20），不是猜。

### 5.1 自定义 `Graphic` 能否注册

- 能。`Il2CppInterop.Runtime.Injection.ClassInjector` 在随附的 `Il2CppInterop.Runtime.dll` 里存在，公开方法包括：
  `RegisterTypeInIl2Cpp()`、`RegisterTypeInIl2Cpp(Type)`、`RegisterTypeInIl2Cpp(RegisterTypeOptions)`、`RegisterTypeInIl2Cpp(Type, RegisterTypeOptions)`，以及 `IsTypeRegisteredInIl2Cpp()` / `IsTypeRegisteredInIl2Cpp(Type)` 可用于先探测。MelonLoader 0.7.3 的 `MelonLoader.dll` 也引用了同一套符号。
- **当前代码没有调用它**：`FlowParticleLayer.cs:68-99` 直接 `AddComponent<FlowParticleGraphic>()` 然后靠异常/null 兜底。也就是说，除非 MelonLoader 自动注入了 mod 程序集里的组件类型（**未验证**，0.7.x 默认不做），否则运行时走的是 **Image 回退**。日志里出现「[粒子] 自定义 Graphic 不可用」即为实证。
- 建议路径：在 `OnInitializeMelon` 里 `ClassInjector.RegisterTypeInIl2Cpp<FlowParticleGraphic>()`（用 `IsTypeRegisteredInIl2Cpp` 先判断，避免重复注册），再创建层；注册失败则保留回退。**注册一个带 public override 的 `MaskableGraphic` 子类是否成功，未验证**——必须在游戏内跑一次看日志。失败到底是抛异常还是返回 null，见 §5.6.1。
- 为什么 `public override` 是硬要求：本机 `UnityEngine.UI.dll` 元数据显示 `Graphic.OnPopulateMesh(VertexHelper)` 的两个重载都是 **public virtual**，`MaskableGraphic` 未覆写；C# 不允许 override 改访问级别（CS0507），所以写 `protected override` 编译不过。`FlowParticleGraphic.cs:49-55` 的注释与此一致。

### 5.2 `AddComponent` 返回 null 的已知情况

| 情况 | 表现 | 处理 |
|---|---|---|
| 类型没注入到 IL2CPP 域 | 返回 null（`Il2CppType.Of<T>()` 拿不到）或抛异常 | 当前已兜底（`FlowParticleLayer.cs:68-99`） |
| 抽象类 / 接口 | null | 不要注册抽象类型 |
| `RequireComponent` 不满足 | `Graphic` 需要 `CanvasRenderer`，`RectTransform` 必须先有 | 先 `AddComponent<RectTransform>()`（`:48`），再挂 Graphic；`CanvasRenderer` 由引擎补（**未验证**注入类型是否也走 RequireComponent） |
| GameObject 已销毁 / 为空 | 抛 `MissingReferenceException` | 先判 `go != null` |
| 编辑器专用类型 | 运行时 null | 不要用 |

**设计含义**：任何「必须靠自绘 Graphic 才能成立」的方案，都要准备一个「Image 池也能看」的降级形态；否则一旦注入失败，玩家看到的是完全空白。

### 5.3 为什么 `Shader.Find` 不可用

- IL2CPP 打包时 shader 变体被裁剪，`Shader.Find("UI/Default")` 之类的按名查找经常返回 **null**；就算找得到，也可能拿到一个没有变体的 shader，材质渲染为粉红/透明。代码里明确记录了「按名字找会拿到 null，整层直接不显示」（`FlowParticleLayer.cs:86-88`）。
- 正确做法：**只从已有对象拿材质引用**——`Graphic.defaultGraphicMaterial`（`:88`）；要贴图就 `new Material(Graphic.defaultGraphicMaterial)` 克隆一次。
- 这也意味着：**不能引入任何自带 shader 的资产**（C6 已禁止），因为没有可靠的加载路径；`AssetBundle` 也不在现有工程里（csproj 只有 IL2CPP/MelonLoader 引用 + 嵌入资源，`ProbablyBagMod.csproj:20-35`）。

### 5.4 `DelegateSupport.ConvertDelegate` 的用法边界

| 能 | 不能 |
|---|---|
| 把托管方法转成 **Il2Cpp 侧声明的委托类型**，用于游戏回调：`Il2CppSystem.Action`、`Action<T>`、`Func<T>`（`PBModEntry.cs:28-32`、`:110`、`FlowItemSetup.cs:137`） | 不能用于纯托管事件（`FlowSpawner.OnEntityAdded` 是普通 C# event，`FlowSpawner.cs:23-26`，直接 `+=` 即可，`FlowVisual.cs:97-98` 就是这么做的） |
| 转换后**必须自己保住引用**（静态字段/长生命周期对象），否则 GC 回收托管委托 → 原生侧悬空 → 崩溃 | 不能每帧转换（每次都会新建原生委托对象，且是分配） |
| 目标方法应是实例/静态的**非泛型**方法 | 不要转换带泛型参数、`ref struct`、或返回 Il2Cpp 泛型类型的方法（**未验证**这些限制的确切边界，保守起见避开） |

对本设计的直接影响：视觉层不需要任何 `ConvertDelegate`——它只读数据、只画顶点；不要为了「拿到装置事件」去转换委托，那会引入生命周期风险。

### 5.5 其它 IL2CPP/UGUI 硬限制

- **顶点上限**：UGUI 的 Mesh 默认 16 位索引 → 单 Graphic ≤ 65,535 顶点 = **16,383 quads**。当前最坏 2,048 quads，安全；但不要设计「每格 1 quad × 大网格」的方案（32×32 格 = 1,024 quads，已接近建议上限）。
- **UV 通道**：`VertexHelper.AddVert` 只有 `(Vector3, Color32, Vector4)` 这个最小重载（元数据核验），uv0 是 `Vector4`；`UI/Default` 只用 uv0。要 uv2/uv3 必须改 `Canvas.additionalShaderChannels`——本设计不需要。
- **顶点色**：`Color32` 8 位/通道，alpha 最小可见 1/255；全透明 quad 仍然占顶点，不如不提交。BRIEF 的「alpha ≥ 1」下限（BRIEF.md:50）只在需要参与点击命中时才有意义，而本层 `raycastTarget=false`（`FlowParticleLayer.cs:84`、`:225`），命中阈值 1e-5 与视觉层无关。
- **色彩空间**：若项目是 Linear，顶点色/贴图会被转线性，屏幕取色与设计稿不一致（**未验证**本项目色彩空间）。定色值时以游戏内截图像素为准，不要以设计稿十六进制为准。
- **遮罩**：父级 `RectMask2D`/`Mask` 对嵌套 Canvas 内 Graphic 的裁切行为**未验证**；本层与网格同尺寸，正常不会越界，设计不要依赖「画出格子外再被裁掉」。

---

### 5.6 两条实现路线的可行性核验（本轮新增）

#### 5.6.1 不调用 `ClassInjector` 时，`AddComponent<自定义 Graphic>` 会怎样

- 元数据事实：`UnityEngine.GameObject` 只暴露 `AddComponent(Il2CppSystem.Type)` 与泛型 `AddComponent<T>()`；泛型版本内部要拿 `Il2CppType.Of<T>()`（`Il2CppInterop.Runtime.Il2CppType` 提供 `Of()` / `Of(bool)` / `From(Type)`）。
- `ClassInjector` 是**显式注册**用的（`RegisterTypeInIl2Cpp()` 四个重载 + `IsTypeRegisteredInIl2Cpp()`）。在 MelonLoader 0.7.3 的托管程序集里**没有找到"自动注册 mod 程序集类型"的调用**（字符串 + 元数据检索；未逐 IL 反编译 MelonLoader 的启动路径）。
- 因此：**不能假设自动注入**。未注册时 `Il2CppType.Of<T>()` 拿不到原生类指针，`AddComponent` 必然失败——**但"失败"是抛异常还是返回 null，元数据无法判定**（Il2CppInterop 里没有 `"is not an Il2Cpp type"` 之类的固定错误文案；两种代码路径都可能）。当前实现对两种都做了兜底（`FlowParticleLayer.cs:68-99`），所以结论只能写：**不可依赖，必须显式注册**。
- 可执行的判定顺序：
  ```csharp
  if (!ClassInjector.IsTypeRegisteredInIl2Cpp(typeof(FlowParticleGraphic)))
      ClassInjector.RegisterTypeInIl2Cpp<FlowParticleGraphic>();   // 失败则 try/catch 后永久走 Image
  var g = go.AddComponent<FlowParticleGraphic>();
  ```
  注册失败只记一次日志，不要每帧重试。**注册一个带 `public override` 的 `MaskableGraphic` 子类是否成功，仍未验证**（见 §8 第 1 项）。

#### 5.6.2 分层 Image 能否在运行时生成贴图

**能。** 元数据核验（`Il2CppAssemblies/UnityEngine.CoreModule.dll`，2026-10-20）：

| API | 存在的签名 |
|---|---|
| `Texture2D.SetPixels32` | `(Il2CppStructArray<Color32>)`、`(Il2CppStructArray<Color32>, int)`、`(int,int,int,int,Il2CppStructArray<Color32>)`、`(int,int,int,int,Il2CppStructArray<Color32>, int)` |
| `Texture2D.Apply` | `()`、`(bool)`、`(bool,bool)` |
| `Sprite.Create` | `(Texture2D, Rect, Vector2, float)`、`+uint`、`+SpriteMeshType`、`+Vector4`、`+bool` |
| `Color32[]` → 原生数组 | `Il2CppStructArray<T>.op_Implicit(T[])`（`Il2CppInterop.Runtime.dll`）→ **直接隐式转换，不需要手写拷贝** |

模板（一次性，缓存结果）：

```csharp
var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
tex.filterMode = FilterMode.Point;          // 像素风必须
tex.wrapMode   = TextureWrapMode.Clamp;
tex.hideFlags  = HideFlags.DontSave;        // 否则切场景被回收
tex.SetPixels32(pixels);                    // Color32[] 隐式转 Il2CppStructArray<Color32>
tex.Apply(false, false);                    // 不生成 mipmap；第二参 false 才能继续改像素
var sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
sprite.hideFlags = HideFlags.DontSave;
```

参考同类实现：`FlowSpriteRegistry.cs:168-179`（PNG 版），差别只是把 `LoadImage` 换成 `SetPixels32`。

**坑**：① `Apply(true, …)` 会生成 mipmap，像素画会糊；② `Apply(…, true)` 之后贴图不可再读/写；③ 贴图与 Sprite 都必须 `DontSave`（`FlowSpriteRegistry.cs:171`、`:179` 同样的坑）；④ `Sprite.Create` 必须在初始化时调用并缓存，**不能每帧**；⑤ ppu 只影响 `Image` 的 native size，实际尺寸由 `sizeDelta` 控制，取 16 或 100 均可；⑥ 所有 Image 必须共用同一张贴图 + 同一材质才能合批。

---

## 6. 新设计的实现边界

### 6.1 纯顶点就能做（0 贴图、0 shader、1 次重建）

- 任意**轴对齐**矩形、条、框、L 形、阶梯箭头、缺口、棋盘格。
- 逐 quad 顶点色（RGBA），允许同层内不同颜色；透明/半透明叠加（≤2 层）。
- 1px 边线、分段计量条（3/4/8 段）、pips、角标、双色分层、内嵌小方块。
- 逐帧改位置/尺寸/颜色（位置像素对齐、无旋转、无缩放插值）。
- 方向语言：同一套格内坐标 + 方向表（`FlowMotionData.cs:29-32`）旋转 90°，0 额外 quad。
- **带 UV 的图集 quad**（仍算"纯顶点"路径）：1 quad 画一个 16×16 tile，材质是克隆的默认 UI 材质——这是像素画唯一的低成本出路（§1.3）。
- 父级提供的 Mask/RectMask2D 裁切（不保证跨嵌套 Canvas，见 §5.5）。

### 6.2 需要贴图（顶点做不到或代价过高）

- **16×16 逐像素像素画**：256 quads vs 1 quad + UV → 必须贴图。
- **柔边/光晕/渐变**：顶点只能叠硬边半透明块；3 层以上会洗色。真正的柔边要预烘 alpha 到贴图。
- **非 45°/90° 的斜线、圆、圆弧**：像素风下要用阶梯近似，逐像素阶梯 = 大量 quad；贴图更省。
- **6 种成分的形状差异**（锯齿=酸、气泡=毒、波纹=水…）：每个形状 1 个 tile 比几十个顶点便宜得多。
- **图标化符号**（雪花/火焰/骷髅）：贴图 1 quad。
- 贴图成本上限：一张 **128×128 RGBA32 = 64 KiB** 图集足够放 8×8 个 16×16 tile；1 个额外 draw call（与默认材质的其他 UI 不同批）。

### 6.3 需要 shader，放弃（C6 明令禁止，且 §5.3 无可靠加载路径）

- 加色发光（Additive/HDR）、Bloom、外发光。
- 径向/圆形渐变、软粒子、噪声场、溶解/阈值。
- 描边后处理、颜色分级、屏幕空间效果。
- 任意角度旋转/斜切（C1 也已禁止；shader 救不了像素纪律）。
- 自定义混合模式（UI/Default 固定 `SrcAlpha OneMinusSrcAlpha`）。
- 逐帧改纹理像素来做动画（CPU 上传纹理，代价远高于顶点）。

### 6.4 数据侧边界（双轨：主轨契约 + Tier-0 降级）

> 裁决（director）：设计以**完整数据契约**为目标，不因数据缺失砍掉设计目标；同时给出只用现有可读量时的降级形态。
> 本节是设计唯一可依赖的数据清单；清单之外不要假设任何字段存在。

#### 6.4.1 展示层代理量（今天就能读）

**今天的可读量**（`FlowEntity` 三个字段，`FlowEntity.cs:19-36`）：

| 信息 | 字段 | 说明 |
|---|---|---|
| 格位置 | `Position`（`FlowPos.cs:23-24`） | 连续 double；整数=格边界（§2.1） |
| 方向 | `Motion.Direction` 0..3（`FlowMotionData.cs:50`） | 0=上 1=右 2=下 3=左（`:29-32`） |
| 速度 | `Motion.Speed`（`:53`） | 格/秒 |
| 含量 | `Properties.Moisture`（`FlowProperties.cs:21`） | 单位 µL（内部 0.001 mL；`DefaultEmissionPolicy.cs:56-63`） |
| 温度 | `Properties.Temperature`（`:24`） | double，**无单位、无范围约束**（-20..100 要自己 clamp） |
| 状态（部分可推） | 位置逐帧差 + `Speed` | 流动=位置在变；受阻=`Speed>0` 但位置不变（`FlowManager.cs:116`）；被收集=`OnEntityRemoved`（`FlowSpawner.cs:50-58`）；分流/汇聚**无事件**，只能靠"同帧新增/消失"猜（未验证） |

#### 6.4.2 主轨缺口（完整契约里今天**不存在**的字段）

设计若依赖这些，必须先补逻辑层 → 与 BRIEF「逻辑层冻结」冲突：

| 信息 | 缺什么 | 证据 |
|---|---|---|
| I1 六种成分占比 | `FlowProperties` 只有 Moisture + Temperature 两个字段；`Validate()` 强制每个 public 字段必须在规则表声明，加字段必须改逻辑层 | `FlowProperties.cs:21-30`、`:177-196` |
| 成分（表现层） | `CompositionShare` 只有 `Moisture` 一个字段；`FlowVisual` 硬编码 `PureWater` | `EmissionContext.cs:62-77`、`FlowVisual.cs:271` |
| I4 剩余射程 | 全 `src` 无 Range/Remaining 字段；`Particle.Remaining` 是粒子寿命比例，不是射程 | `Particle.cs:55-58`，grep 结果 |
| I5 状态枚举（分流/汇聚） | `FlowEntity` 无 status 字段；Split/Merge 只发生在逻辑层内部（`SplitDevice.cs:133`、`CollectorDevice.cs:65`），不广播 | `FlowEntity.cs:19-36` |

#### 6.4.3 完整契约需要的最小只读新增

> ⚠️ 以下三项是**后续需用户批准的独立前置任务**，不属于本轮设计改动，也不由设计代理实施。设计可以在主轨里按「它们存在」画，但必须在交付物里标注依赖。

| 新增 | 类型 / 语义 | 写入点 | 代价 |
|---|---|---|---|
| `FlowProperties.Composition` | 6 个 0..1 的占比（水/毒/油/血/酸/能量），或一个 `CompositionShare` 结构 | 装置反应 / 生成时 | **必须同时进 `_ruleTable`**（Split=Divide、Merge=Add），否则静态构造直接抛 `InvalidOperationException`（`FlowProperties.cs:165-197`） |
| `FlowEntity.RangeRemaining` | double，剩余可走格数；可选 `RangeMax` 用于归一化 | `FlowManager.Tick`（每帧减）、生成时 | 1 个字段 + 2 处写入；不改 `IFlowGrid` |
| `FlowEntity.State` | enum：`Flowing / Blocked / Split / Merged / Collected / Idle` | `FlowManager`（受阻/流动）、`SplitDevice`、`CollectorDevice`、`FlowSpawner`（收集） | 1 个字段 + 3–4 处写入；视觉层直接读，**不需要新事件** |

接口影响：`IFlowGrid`、`FlowSpawner` 的事件签名都不用改；`FlowEntity` 构造函数（`FlowEntity.cs:30-35`）加可选参数即可。逻辑层新增约 40 行、0 处语义修改。**是否批准由用户决定**，设计代理只需把依赖写清。

#### 6.4.4 Tier-0：只用现有可读量能做什么

**能表达**：

| 信息 | 代理量 | 读法 |
|---|---|---|
| I2 温度 | `Properties.Temperature` | 自行 clamp 到 -20..100 后分 3 档 |
| I3 含量 | `Properties.Moisture` | µL；按 BRIEF 0..50,000,000 归一化 |
| I6 方向 | `Motion.Direction` | 0..3 查表（`FlowMotionData.cs:29-32`） |
| 运动 / 静止 | `Position` 逐帧差 + `Speed` | 差≈0 且 `Speed>0` → 受阻；差≈0 且 `Speed==0` → 静止（需 1 帧历史） |
| 出生 / 被收集 | `OnEntityAdded` / `OnEntityRemoved` | `FlowSpawner.cs:23-26`、`:35-58`；消失动效需视觉层延迟 0.1–0.2 s 再销毁壳 |

**不能表达（硬缺口）**：

- **I1 成分**：只能是"水"（单色），BRIEF 的 6 色 / 色盲 4 组无法成立。
- **I4 射程**：完全不可得。没有任何代理量——`Speed × 剩余时间` 不等于射程，`Moisture` 也不是射程。
- **I5 分流 / 汇聚**：无事件。只能从"同帧新增/消失"猜，会把"装置正常产出"误判为"分流"（未验证，不建议）。
- **I5 受阻 vs 静止**：同形，无法静态区分（都表现为位置不动）。

**Tier-0 视觉退化策略（建议）**：

1. **把"成分"通道让给"温度"**：温度用色相（冷蓝 → 暖橙），成分固定为水青。6 色目标降级成 3 色，但 I2 仍静态可读、通道仍正交。
2. **射程通道不新增**：原计划的 4 缺口改为"含量 4 段"（第 4 段 = 接近耗尽）。注意**不要**让玩家把它读成射程——Tier-0 下根本没有射程这个概念。
3. **状态只做二态**：流动 = 边框常态、受阻 = 边框亮 2 帧后停住、静止 = 无闪烁。分流/汇聚/被收集不做静态表达，被收集只做 0.15 s 消失动效。
4. **同一套绘制器换调色板**：主轨的 6 色设计稿保留为交付物，Tier-0 是"数据未接通时的 fallback 皮肤"，只换通道开关与颜色表（前提是绘制器参数化，见 §9.2-1）。

**结论**：I2、I3、I6 今天就能画；**I1、I4、I5 的主轨表达依赖 §6.4.3 的三个字段**。设计交付物必须同时给出「主轨（完整契约）」与「Tier-0（降级皮肤）」两套通道分配，否则数据未接通时整套语言会失效。这条已单独回传给父代理。

---

## 7. 给设计的 5 条硬建议

1. **静态 ≤ 24 quads，动画峰值 ≤ 48，硬顶 64。**
   一个能覆盖 6 条信息的构成：主体 1 + 1px 内框 4 + 含量 3 段 3 + 温度 3 pips 3 + 方向头 3 + 射程 4 缺口 4 = **18 quads**，余量 46。1x 下每个通道至少 1px 宽；一次重建 ≤ 15 µs、顶点 ≤ 27.8 KiB。

2. **每块网格只允许一个 Graphic、一次重建、一个 draw call。**
   禁止「每粒/每源一个 Image」：1024 粒 = 1024 次重建；64 quads/源 × 4 源 = 256 个 Graphic。作为对照，自绘路径 1,280 quads 仍只有 **1 次重建**。若最终只能走 Image 回退，把预算降到 **≤12 quads/源、≤2 源/网格**。

3. **稳态 0 GC alloc / 帧，初始化一次性。**
   允许：1 张 ≤128×128 图集（64 KiB）、1 个克隆材质（每层一个，`Destroy` 时销毁）、每层 1 个 `List` 缓冲（warmup 后不再扩容）。禁止：每帧 `new Material`/`Sprite.Create`/`new Texture2D`/字符串拼接/LINQ/闭包；每帧 `SetVerticesDirty` 最多 1 次。

4. **像素对齐：四条边先乘 ppu 再 `Mathf.Round`，然后除回 ppu；特征尺寸取 1/2/4/8/16 的整数倍。**
   1x 下最小可见特征 **1px**，4x 下 4px；半透明叠加 **≤2 层**（UI/Default 是直通 alpha，第 3 层开始洗色）；alpha 字节 ≥1；不要设计旋转、斜边和渐变。若 `16 × ppu` 不是整数，接受相邻格 ±1px 的宽度差，不要试图用小数坐标"抹平"（那会产生半像素灰边）。

5. **4 向复用同一套格内坐标，只旋转 3 个头部 quad。**
   方向表 0/1/2/3 = 上/右/下/左（`FlowMotionData.cs:29-32`）；主体、含量、温度、射程四组 quad 在四个方向下**完全不动**，只有头部 3 个 quad 换偏移 → 方向通道 **0 额外 quad**，L 拐角 **0 错位**。若用贴图，把 4 向预烘进同一张图集，每向仍 1 quad、draw call 不变。

---

## 8. 未验证清单（按优先级）

| # | 待验证 | 验证方法 | 影响 |
|---|---|---|---|
| 1 | `ClassInjector.RegisterTypeInIl2Cpp<FlowParticleGraphic>()` 是否成功 | 游戏内跑一次，看是否打印「使用自定义 Graphic 渲染」而不是回退日志 | 决定走主轨（64/48 quads）还是 §9 降级轨（分层 Image 2–4/源） |
| 2 | 网格 `rect.width` 是否恰为 `16 * 列数` | `FlowVisual.ResolveGridRect` 里临时 log 一次 `rect.size` | 决定 §2 公式是否整体漂移 |
| 3 | Canvas scaleFactor（`16 * ppu` 是否整数） | log `lossyScale.x` 与 `rect.size` | 决定 1px 描边是否会有 2px 格 |
| 4 | 稳态是否真 0 分配 | Unity Profiler / `ProfilerRecorder(GC.Alloc)` 跑 60 s | 决定是否需要改 `Submit` 的每帧拷贝 |
| 5 | 项目色彩空间 + 顶点色取色偏差 | 截图取色 vs 设计稿十六进制 | 决定 6 成分色值是否需要预补偿 |
| 6 | 嵌套 Canvas 是否受父级 Mask 裁切 | 在 ScrollRect 里滚动观察 | 决定能否画到格子外 |
| 7 | `FlowPos.cs:12`「整数点=格中心」是否为遗留错误 | 对照 `FlowManager` 的 Floor 与 `FlowVisual` 的 +0.5 | 决定格内偏移基准（当前实现按格边界） |

---

## 9. 实现契约推荐

### 9.1 推荐：双轨交付，主轨「自绘 Graphic 单层」，降级轨「分层 Image + 运行时图集」

| | 主轨（自绘 Graphic） | 降级轨（分层 Image） |
|---|---|---|
| 前提 | `ClassInjector.RegisterTypeInIl2Cpp<>()` 成功 | 无前提（`Image` 是游戏自带类型，`AddComponent` 一直可用） |
| quads/源 | 12–48（标准 18） | 2–4 |
| 重建/帧 | **1 / 网格** | 2–4 / 源，且仅在 tint 变化时 |
| draw call | 1 / 网格 | 1 / 网格（同一图集） |
| 每帧分配 | 0（warmup 后） | 0 |
| 代码量 | 1 个 Graphic 子类 + 1 个参数化绘制器 | 1 个 Image 池 + 1 个图集生成器 |
| 风险 | 注入可能失败（§5.6.1，未验证） | 源多时 Graphic 数线性增长（约 30 源后明显，未实测） |

**为什么主轨选自绘 Graphic**：

1. 64 quads 预算只有在"整层一次重建"下才成立。Image 路线把重建次数变成 `2–4 × 源数`，10 源就是 20–40 次/帧；自绘路线 20 源仍是 1 次（§3.6）。
2. 所有通道由同一份顶点数据生成，像素对齐只有一套代码；两条路径各写一套会在面板缩放时差半像素（`FlowParticleLayer.cs:170-172` 记录过这个 bug）。
3. 顶点数据可一次提交，`OnPopulateMesh` 里不需要访问任何 GameObject/Transform，异常面小（`FlowVisual.cs:133-144` 的整帧 try/catch 只应该兜住原生访问）。

**为什么保留降级轨**：注入结果未验证。若失败，Image 路线用 2–4 层仍能表达主体/含量/温度/方向头四个通道，且运行时贴图已核验可行（§5.6.2）。降级轨**从第一天就按 2–4 层设计**，不是把 18 quads 硬拆成 18 个 Image（那是 18 次重建/源，见 §3.6-B）。

### 9.2 需要预留的东西

1. **参数化绘制器**：通道 → quad 列表（数量/颜色/偏移）做成数据；Tier-0 只换调色板与通道开关（§6.4.4），不重写绘制。
2. **一张预留图集**：即使主轨不用贴图，也先放 `assets/flow_atlas.png` + `sprites.json` 条目（csproj 已自动嵌入，`ProbablyBagMod.csproj:33-34`），降级轨直接用。
3. **一处注入探测**：`IsTypeRegisteredInIl2Cpp` + `RegisterTypeInIl2Cpp` + 一次性日志；失败后永久降级，不重试。
4. **一个像素对齐工具**（§2.3 的 `FlowPixel`）：两条路线共用，禁止各写一份。
5. **一张 4 向偏移表**：方向只影响头部 3 个 quad 的偏移（§7 建议 5），预留 `Vector2[4]`。
6. **一条数据契约注释**：绘制器顶部写清「依赖 `FlowProperties.Composition` / `FlowEntity.RangeRemaining` / `FlowEntity.State`；当前缺失，见 §6.4.3」，防止后来者以为字段已存在。

### 9.3 一句话交付契约

> 主轨：每流源 ≤48 quads、整网格 1 次重建 1 个 draw call、每帧 0 分配、四条边像素对齐；贴图只允许 1 张图集（1 quad/tile）。降级轨：每源 2–4 个 Image、只在档位变化时改 tint、同一图集 1 个 draw call。

---

## 附录 A 测量脚本

`docs/flow-visual/research/measure_budget.py`（同目录）。

```
C:\Users\Administrator\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe measure_budget.py
```

输出 §3.1–§3.5 的全部表格：quad 字节数、各场景 quads/verts/字节、各帧率下的稳态存活数与打满 1024 所需源数、64 quads 的容量表、64 quads 的可行构成、重建成本粗估。改常量即可复算（`MAX_PARTICLES`、`QUADS_PER_PARTICLE`、`MAX_PER_FRAME`、`MAX_LIFE` 都在脚本顶部）。

**数字来源汇总**：`444 B/quad`、`108 B/顶点` 由元数据推断；`840 quads/源 @60fps`、`2048 quads/888 KiB` 由 `measure_budget.py` 算出；`64 quads = 27.8 KiB` 是算术。除元数据外**没有运行时实测**，所有「µs / ms」都是量级估算，落地前请用 Profiler 复核。
