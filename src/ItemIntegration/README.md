# ItemIntegration

原版物品出生与物品 helper。

## 职责

```text
ItemSpawn/
  Services/Attachments/  items.json 声明的装配件
  Services/Factory/      通用属性装配
  Setup/                 物品出生编排
Content/                 items / sprites / tooltip 内容注册
Common/Item/             容量、tag 提交等物品侧 helper
Shared/                  嵌入资源、序列化
```

## 边界

- 负责“物品出生后长什么样、挂什么行为、tag 怎么提交”。
- 不实现 Logic 装置的状态机；装置行为在 `src/Logic/Devices/`。
- 不负责装置实例的创建、登记和每帧驱动；那属于 `src/LogicIntegration/`。

## 新增物品

1. `assets/items.json` 增加定义。
2. `assets/sprites.json` 登记贴图，PNG 放到 `mod/Textures/Items/`。
3. 需要新行为时新增 `ItemAttachment` 子类到 `ItemSpawn/Services/Attachments/`。
4. tag 写完统一走 `Common/Item/ItemTagCommit.Commit`，不要自己拼 `SyncModifiedState`。

