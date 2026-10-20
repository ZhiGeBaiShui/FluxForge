# 流源视觉语言 v1 — 技术美术与资产计划

> 状态：草稿（必须由设计/架构 agent 填实后再通过 design gate）
> 游戏：Probably Stolen · Unity：2021.3.45f2 · 后端：il2cpp · Loader：MelonLoader 0.7.3
> 最后更新：待填 · 负责人：待填

## 1. 资产清单

| ID | 类型 | 用途 | 来源/授权 | 导入设置 | Shader/SRP | 包体预算 | 负责人 |
|---|---|---|---|---|---:|---|
| A-001 | texture | 待填 | 自绘/授权待填 | sRGB/压缩待填 | 待填 | 待填 | 待填 |
| A-002 | material | 待填 | 待填 | 待填 | 待填 | 待填 | 待填 |
| A-003 | prefab | 待填 | 待填 | 待填 | 待填 | 待填 | 待填 |

> 禁止把游戏原始资产打进发布包；只使用自绘、明确授权或程序生成的资产。

## 2. 渲染管线与版本

- 游戏渲染管线：Built-in / URP / HDRP / 待确认
- 游戏 Unity 版本：2021.3.45f2
- AssetBundle 构建版本必须与游戏一致；跨版本 bundle 不做兼容承诺。
- 目标平台与图形 API：Windows D3D11 / D3D12 / Vulkan（待确认）

## 3. Shader 兼容

- 使用的 shader 类型：
- 需要的关键字/变体：
- 内置管线 fallback：
- URP/HDRP 差异处理：
- Shader stripping 检查：
- 材质属性默认值与序列化：

## 4. 纹理与材质规范

| 项 | 规范 |
|---|---|
| 颜色空间 | Linear（与项目一致） |
| sRGB | 颜色贴图开；法线/数据贴图关 |
| Mipmap | 3D 世界物体开；UI 视情况 |
| 压缩 | 按平台覆盖；UI/法线单独设置 |
| Atlas | UI/2D 优先；避免过大图集 |
| 法线 | 标记为 Normal map，正确切线空间 |

## 5. 动画 / UI / VFX / 音频

- 动画：Humanoid/Generic、Avatar Mask、Root Motion、Timeline/Playables
- UI：UGUI/UI Toolkit、Canvas Scaler、安全区、CJK 字体 fallback、9-slice
- VFX：Particle System/Shader Graph、Overdraw、GPU Instancing
- 音频：wav/ogg、加载类型、混音组、空间化

## 6. 构建与加载

- Bundle 命名与依赖拆分：
- 构建脚本/命令：
- 加载失败 fallback：
- 平台/版本不匹配时的错误提示：
- 资源卸载与引用计数：

## 7. 性能预算

| 指标 | 预算 | 实测 | 证据 |
|---|---:|---:|---|
| 主线程帧时间增量 | 待填 | 待填 | Profiler 截图 |
| GC Alloc/帧 | 待填 | 待填 | Profiler |
| 纹理显存 | 待填 | 待填 | Memory Profiler |
| DrawCall 增量 | 待填 | 待填 | Frame Debugger |

## 8. 视觉 QA

- 分辨率：1280×720 / 1920×1080 / 2560×1440
- 场景：主菜单、游戏内白天/夜晚、UI 打开/关闭
- 对照：无 mod / 有 mod；截图命名与基线目录
- 检查：色差、透明排序、描边、字体缺字、UI 拉伸、过曝/过暗

## 9. 交付清单

- [ ] 资产来源与许可证记录
- [ ] Bundle 构建可复现
- [ ] Shader 在目标管线通过
- [ ] 视觉基线截图
- [ ] 性能证据
- [ ] 失败 fallback 已验证
