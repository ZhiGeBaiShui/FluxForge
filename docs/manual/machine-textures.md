# 机器贴图更新（2026-11-16）

六张图由用户提供，直接复制，不重采样、不修改像素。来源：桌面 `管道机器贴图_16x_小16大48/textures/`。

| 新文件 | 正式资源名 | 尺寸 |
|---|---|---|
| centrifuge.png | centrifuge.png | 16×16 |
| cold_extractor.png | chem_reactor_top.png | 48×48 |
| cooler.png | chem_reactor_cool.png | 16×16 |
| filter.png | chem_reactor_alkali.png | 16×16 |
| former.png | fluxforge_chemical_former.png | 48×48 |
| heater.png | chem_reactor_heat.png | 16×16 |

正式PNG在 `mod/Textures/Items/`，`assets/sprites.json` 的已有键与占格不变。相同文件同步到 Unity 工程 `E:/code/ProbablyMod/Assets/FluxForgeAssets/`。已有Unity图片保留meta/GUID，原预制体图标引用自动跟随；成型机首次导入同目录，目前没有手册图标节点，不擅自新增页面。

已执行原菜单对应的 `BuildFluxForgeDeviceManualBundle.Build`，生成 `mod/AssetBundles/fluxforge_device_manual` 和 Unity 的 RuntimeBundle预制体。构建日志：`output/manual-six-textures-build.log`。六张源图、Mod图与Unity图逐字节一致。未自动部署至游戏。
