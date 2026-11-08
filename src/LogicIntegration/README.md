# LogicIntegration

Logic 层与原版游戏之间的适配层。

## 职责

```text
Bootstrap/       进程入口、读档、场景、每帧宿主
Common/Game/     坐标 / 方向适配
DeviceServices/  DeviceHost、DeviceRegistry、音频、动画
DeviceState/     DeviceStateAdapter、tag ↔ 装置状态
GridBuilding/    网格映射、生命周期 patch、FlowRuntime
TagMapping/      tag 常量、GameItemTags、FlowStock
Visual/          流源视觉层
```

## 边界

- 负责“Logic 对象 ↔ 原版 GameItem / 网格 / tag / Unity 表现”的转换。
- 不实现装置业务规则；业务规则在 `src/Logic/Devices/`。
- 不负责 items.json / sprites.json 的内容定义；那属于 `src/ItemIntegration/`。

## 新增装置

1. 在 `src/Logic/Devices/` 写装置类。
2. 在 `Devices/DeviceState/Adapters/` 写 `[DeviceAdapter("key")]` 适配器。
3. `assets/items.json` 声明 `device`、`anchorX/Y`、`attachments`。
4. 装置状态回写由 `DeviceStateSync` 负责，不在适配器里操作 `DeviceRegistry`。

## 自动回刷

```text
ItemTagCommit.Commit
→ ValidateShapeState
→ DeviceStateReconcilePatch
→ DeviceHost.LoadAll
```

所有直接改 tag 后的提交都要经过 `ItemTagCommit.Commit`，不要只调用 `SyncModifiedState`。

