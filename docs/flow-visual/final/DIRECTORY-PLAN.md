# 工程目录规划（贴图层 / 管理层分离）

> 状态：已执行（2026-10-21）

> 目标：职责单一、依赖单向、调用链一眼可见。
> 只做**编译期目录与命名空间**调整；不涉及 Unity 序列化资源（现有类全是普通 C# 类，无 MonoBehaviour/ScriptableObject），迁移安全。

## 1. 现状问题

| 位置 | 问题 |
|---|---|
| `Mod/Flow/Visual/` | 三种职责混在一起：`FlowAtlasTexture`（贴图加载/切片/缓存）、`FlowAtlasLayer`（UGUI 节点/池/提交）、`FlowAtlasView`（每机器编排） |
| `Mod/Sprites/` | 物品图标贴图，和 `FlowAtlasTexture` 同属"贴图层"，却分在两处 |
| `Mod/Flow/` | 管理层里混了 `FlowGridAdapter`（适配器）、`LiquidStock`（数据）、`FlowDeviceEvents`（事件） |
| `Presentation/Flow/` | 纯视觉状态（state/tint/index）和纯粒子仿真（profile/field/modifiers）混在同一个目录 |
| `Mod/Items/` | 物品工厂、初始化、本地化、Tooltip 混在一起（本轮不动，后续再拆） |

## 2. 目标结构

```
src/
├─ Logic/                                  # 规则与状态（不动）
│
├─ Presentation/                           # 纯 C#，零 Unity，可单测
│  └─ Flow/
│     ├─ Visual/                           # 视觉状态与颜色
│     │   ├─ FlowVisualState.cs
│     │   ├─ FlowComposition.cs
│     │   ├─ FlowColor.cs
│     │   ├─ FlowTint.cs
│     │   ├─ FlowTierMap.cs
│     │   ├─ FlowPixelMath.cs
│     │   └─ FlowAtlasIndex.cs
│     └─ Particles/                        # 纯粒子仿真
│         ├─ FlowParticleProfile.cs
│         ├─ FlowParticleTuning.cs
│         ├─ IFlowParticleModifier.cs
│         ├─ TemperatureSpeedModifier.cs
│         ├─ FlowParticleRules.cs
│         └─ FlowAmbientField.cs
│
└─ Mod/                                    # Unity / 游戏适配层
   ├─ FFModEntry.cs                        # Bootstrap（唯一入口）
   ├─ Common/
   │   └─ EmbeddedResources.cs
   │
   ├─ Flow/                                # ── 管理层 ──
   │   ├─ FlowMachine.cs                   # 一台机器（持有 Motion/Flows/AtlasView）
   │   ├─ FlowDriver.cs                    # 多机器调度（Update / LateUpdateVisuals）
   │   ├─ FlowDeviceEvents.cs
   │   ├─ DeviceRegistry.cs
   │   ├─ LiquidStock.cs
   │   ├─ Adapters/                        # 游戏类型 → IFlowGrid
   │   │   └─ FlowGridAdapter.cs
   │   └─ View/                            # 每机器视觉编排
   │       └─ FlowAtlasView.cs             # 实体表 / blocked / 发射器 / 粒子场
   │
   ├─ Rendering/                           # ── 贴图层 / 渲染层 ──
   │   ├─ FlowAtlas/
   │   │   ├─ FlowAtlasTexture.cs          # 贴图：加载、索引、Sprite 切片、缓存
   │   │   └─ FlowAtlasLayer.cs            # 绘制：Image 池、像素对齐、Submit/SubmitDots
   │   └─ ItemSprites/
   │       ├─ FlowSpriteRegistry.cs        # 物品图标贴图（原 Mod/Sprites）
   │       └─ SpriteEntry.cs
   │
   ├─ Items/                               # 物品与 Tooltip（本轮不动）
   ├─ Patches/
   └─ ...
```

### 命名空间对照

| 目录 | 命名空间 |
|---|---|
| `Presentation/Flow/Visual` | `FluxForge.Presentation.Flow.Visual` |
| `Presentation/Flow/Particles` | `FluxForge.Presentation.Flow.Particles` |
| `Mod/Flow`（根文件） | `FluxForge.Mod`（保持原 namespace，避免无谓改动） |
| `Mod/Flow/View` | `FluxForge.Mod.Flow.View` |
| `Mod/Rendering/FlowAtlas` | `FluxForge.Mod.Rendering.FlowAtlas` |
| `Mod/Rendering/ItemSprites` | `FluxForge.Mod.Rendering.ItemSprites` |
| `Mod/Flow/Adapters` | `FluxForge.Mod.Flow.Adapters` |
| 其余 | `FluxForge.Mod` / `FluxForge.Mod.Items` / `FluxForge.Mod.Patches` |

## 3. 依赖规则（单向）

```
FFModEntry (Bootstrap)
   │
   ├─► Mod.Flow (管理) ──────────► Mod.Flow.View ──► Mod.Rendering.FlowAtlas
   │        │                          │                    │
   │        └──────────────┬───────────┴────────────────────┘
   │                       ▼
   │              Presentation.Flow.Visual / .Particles
   │                       ▼
   │                    Logic
   │
   ├─► Mod.Patches ──► Mod.Items ──► Mod.Rendering.ItemSprites
   └─► Mod.Common
```

- **Rendering 不得引用 Mod.Flow**（不认识流源，只认 key / draw item / particle）。
- **Mod.Flow.View 只依赖**：Management 的 `FlowMachine` 数据、Rendering 的 Submit API、Presentation。
- **Presentation 不引用任何 Mod/Unity**。
- **Logic 谁都不引用**（既有约束）。

## 4. 调用链（粒子 + 贴图）

```
FFModEntry.OnUpdate            → FlowDriver.Update(dt)            # 逻辑推进
FFModEntry.OnLateUpdate        → FlowDriver.LateUpdateVisuals(dt)
                                   └─ FlowMachine.AtlasView.Tick(dt)      # View（管理）
                                        ├─ TryBuildItem → Layer.Submit    # 贴图
                                        └─ UpdateParticles
                                             ├─ Rules.Build / ColorFor    # Presentation
                                             ├─ Field.Spawn / Advance
                                             └─ Layer.SubmitDots          # 粒子绘制
                                                  └─ Atlas.Get(key)       # 贴图切片
```

## 5. 迁移步骤（机械操作，1.5h）

1. **建目录 + `git mv`**（未跟踪文件用 `Move-Item`；已跟踪文件用 `git mv` 保留历史）。
2. 改 **namespace**（6 个）+ 全局 `using` 修正（Mod 内约 10 处、测试 3 处）。
3. `dotnet build -c Release` 修到 0/0；`dotnet test` 107/107。
4. 全仓检索旧命名空间：`FluxForge.Mod` 下不应再有 `Visual` 子目录引用。
5. 更新文档：`REFACTOR-A.md` 文件树、`IMPL-STATUS.md`、本文件状态。

## 6. 风险与对策

| 风险 | 对策 |
|---|---|
| 命名空间改动遗漏 `using` | 编译器会全部报出来；一次 build 即可收敛 |
| 反射/字符串引用旧类型名 | 全仓检索 `"FluxForge.Mod.` 字符串；当前无 |
| Unity 序列化丢失 | 现有类均非 MonoBehaviour/ScriptableObject，无资产引用；DLL 内部类型名变化不影响 MelonLoader |
| IL2CPP 注入类型 | 无 `[RegisterTypeInIl2Cpp]`，安全 |
| 与并行 agent 冲突 | 迁移期间暂停其它写手；一次搬完再验证 |

## 7. 本轮不做（留待后续）

- `Mod/Items` 内部拆分（Factory / Setup / Localization / Tooltip）。
- `Mod/Patches` 的 patch plan 收敛。
- `Presentation` 顶层再分 `Visual` / `Particles` 之外的子域（目前只有一个域）。
