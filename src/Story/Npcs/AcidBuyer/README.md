# AcidBuyer

随机到访的酸/碱收货商，不依赖展示柜或库存。

## 到访

```text
OnShutterOpenedLate
→ 每天最多检查一次
→ 当前测试概率 100%
→ 当前没有同名客户才添加
```

首次到访和后续到访使用两套开场文本，进度保存在：

```text
fluxforge.acidBuyer.met
```

## 交易

对话选项（首次为“放在桌上了。”，后续为“把酸和碱交给你”）会扫描后柜台：

```text
EmporiumEntry.Instance.backInvinvElement
```

只接受带 `LIQUID_CONTAINER_TAG` 的液体容器，并且：

```text
max(acid, base) / SubstanceTotal >= 0.99
```

带 `FLOW_DEVICE` 的装置即使内部有酸/碱，也不会被收走，避免把贵重容器一起结算。

满足条件的物品全部销毁，并即时 `PlayerStore.ModCash(reward)`。

没有酸/碱时：

```text
没看到我想要的酸和碱。
```

有酸/碱但浓度都不够时：

```text
浓度看起来不够高。
```

## 原料

首次到访直接赠送到前台：

```text
受污染酸液 ×2
受污染碱液 ×2
```

使用原版 250 mL 化学试剂瓶（`mini_bottle`），每瓶成分随机：

```text
先随机生成污染物
剩余容量全部补酸/碱
```

污染物包括：

```text
化学污染
重金属
微塑料
物理污染
矿物
碎屑
```

这些污染物不和酸/碱发生消耗体积的反应。酸/碱浓度低于 99%，不能直接卖回，需要先处理。

## 价格

```text
1 块 / 10 mL
99%        ×1
99.1%      ×1.1
99.9%      ×1.9
100%       ×3
```
