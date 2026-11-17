# FluxForge

《Probably Stolen》0.46 的 Unity Mono 模组，提供流源与化学装置、装置教学手册、奥雅与薇拉剧情、中心医院合同和声望系统。

当前医院主线已实现闭环；奥雅黑针配方与塞纳普斯合成路线属于后续扩展。以[当前项目状态](docs/current-project-state.md)为准，历史方案保留其设计上下文。

## 构建和验证

```powershell
dotnet build FluxForge.csproj -c Release
dotnet test tests/FluxForge.Tests/FluxForge.Tests.csproj
dotnet test tests/FluxForge.Story.Tests/FluxForge.Story.Tests.csproj
python tools/build_story_review.py
```

主工程引用游戏Managed目录，路径在 `FluxForge.csproj` 的 `GameManagedDir` 中。测试使用边界桩，不代表游戏内实测。

退出游戏后，把 `bin/Release` 中的 `FluxForge.dll`、`manifest.xml`、`preview.png`、`AssetBundles/`、`Localization/`、`Textures/` 以及存在的 `Books/` 内容复制到 `<游戏目录>/Mods/FluxForge/`。不要把工程目录、测试或设计素材作为正式Mod发布。

## 阅读入口

| 内容 | 文档 |
|---|---|
| 当前功能与已知限制 | [当前项目状态](docs/current-project-state.md) |
| 项目约束与维护入口 | [AI_CONTEXT.md](AI_CONTEXT.md) |
| 新剧情与插入机制 | [剧情框架](src/Story/README.md) |
| 全部源码台词 | [台词目录](docs/design/story-v5-all-text.md) |
| 条件与剧情交互审阅 | [独立HTML](dist/FluxForge-story-review.html) |
| 逻辑层 | [Logic](src/Logic/README.md) |
| 装置适配层 | [LogicIntegration](src/LogicIntegration/README.md) |
| 物品、资源与注册 | [ItemIntegration](src/ItemIntegration/README.md) |
| 手册与演示 | [Manual](src/Manual/README.md) / [事件层](docs/event-layer-design.md) |
| 当前资源与清理清单 | [维护记录](docs/maintenance/2026-11-16-cleanup.md) |
| 音效来源及授权 | [音效声明](docs/sfx-credits.md) |

## 分层

`Logic`、`Presentation` 保持纯C#；`LogicIntegration` 处理游戏对象与装置适配；`ItemIntegration` 负责物品装配；`Story` 负责NPC与对话；`Manual` 负责手册UI；`Events` 负责装置演示。剧情框架使用自己的Patch钩子，不复用其他层的剧情生命周期。

新增装置：逻辑类 → `[DeviceAdapter]`适配器 → `assets/items.json` → `assets/sprites.json`与`mod/Textures/Items/` → 测试和游戏内验证。

新增剧情请从 `src/Story/README.md` 的现有装配流程开始。普通语法优先；防御分支必须给出项目或原版源码中的实际调用依据。
