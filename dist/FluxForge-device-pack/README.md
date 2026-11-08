# FluxForge 装置贴图包

当前实装的正式装置贴图与用途。用途文本直接取自 `items.json` 的 `desc`。

## 冰块

- 物品 key：`ice_block`
- 装置类型：`ice_block`
- 贴图：`Textures/ice_block.png`

吸收流源的热量，自身逐渐融化。
融化后水分会被流源带走。

## 冷萃装置

- 物品 key：`flow_cold_trap`
- 装置类型：`cold_trap`
- 贴图：`Textures/chem_reactor_top.png`

会进行冷萃，抽取流源中含量最低的液体成分，存入自身。

## 发射装置

- 物品 key：`flow_emitter`
- 装置类型：`collector`
- 贴图：`Textures/flow_emitter_2x3.png`

从容器接收液体，启动后把存量液体一次性发射出去

## 过滤装置

- 物品 key：`flow_filter`
- 装置类型：`filter`
- 贴图：`Textures/chem_reactor_alkali.png`

让流源多次穿过时，可以让化学污染和重金属污染降低到一定浓度。
流速越慢，温度越高，净化效率越高。

## 升温装置

- 物品 key：`flow_heater`
- 装置类型：`temperature`
- 贴图：`Textures/chem_reactor_heat.png`

会加热经过的流源。

## 降温装置

- 物品 key：`flow_cooler`
- 装置类型：`temperature`
- 贴图：`Textures/chem_reactor_cool.png`

会冷却经过的流源。

## L 型转向装置

- 物品 key：`l_turn_device`
- 装置类型：`l_right_down`
- 贴图：`Textures/l_turn_device.png`

可以改变流源的方向。
