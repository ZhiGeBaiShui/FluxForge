> 当前状态索引：2026-11-16，见[项目状态](../../../../docs/current-project-state.md)。自动测试结果及待执行清理见维护记录。

# FlowEmitterMerchant

「装置推销员」奥雅的专属目录。

```text
FlowEmitterMerchantStory.cs      身份、到访规则、剧情事件注册
FlowEmitterMerchantProgress.cs   旧存档兼容的进度计数
FlowEmitterMerchantStock.cs      奥雅库存规则
Events/                          纯剧情内容
```

## 到访

```text
7 天冷却
VisitKind.Sell
OnVisitStarted 增加旧进度 VisitCount
QueueOnGameLoad = true
```

## 剧情事件

```text
arrival        Priority 100  条件：还没完成首次剧情   Outcome Leave
second_visit   Priority 90   条件：首次完成、二次未完成 Outcome OpenShop
generic_visit  Priority 10   条件：总是成立           Outcome OpenShop
```

首次剧情里的拒绝路径不写入 `arrivalSeen`，所以下次仍会进入首次剧情。

## 库存

```text
第二次剧情 Complete：
  MarkSecondVisit + PlaceSecondVisit

通用剧情 Complete：
  Refresh

旧库存由原版客户离店时的 not owned 清理回收。
```

新增剧情时只改 `FlowEmitterMerchantStory.cs` 和 `Events/`，不改通用控制器。
