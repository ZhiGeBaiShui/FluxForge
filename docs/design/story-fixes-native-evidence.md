> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 本轮修复的原版证据复核

本表重新核对本地 `.tmp-decomp` 原版反编译源码和当前 Mod 实现。
原版 API 约定与本框架自己的规则分别列出，不把本项目代码冒充原版代码。

## 1. 跳过对白后仍等待普通停顿

原版 `DialogUIManager.cs:345–360`：

```csharp
if (isSkipCurrentDialog)
{
    isSkipCurrentDialog = false;
    currentDialog.ExecuteEndAction();
    if (EmporiumOption.Instance.autoNextDialog)
        ScheduleAutoAdvance(0.01f);
}
```

同文件只有两处 ScheduleAutoAdvance 调用：351 的跳过路径和 360 的普通自动推进。
普通路径使用 ScaleByDialogSpeed(0.99f) + GetTypewriterDelay()。
ScaleByDialogSpeed 在 373 起按 Clamp01 后的速度计算，普通值不会等于 0.01f。

旧补丁读取 isSkipCurrentDialog，进入补丁时它已经是 false，因而会覆盖跳过延时。
当前 `DialoguePacing.cs` 改为保留实际传入的原版 0.01f。
只在原版自动推进开启且走该跳过路径时涉及此问题，不代表关闭自动推进后的手动模式也失效。
直接调用真实 Prefix 的测试先复现 0.01f 被覆盖为 2f，再验证修复。

## 2. 立绘防御及异常处理：纠正一项过度删除

### 已恢复的初始化状态检查

原版 `SpriteDict.cs:219–223` 在 GetPool 中明确处理：

```csharp
if (Instance == null || Instance.spriteDictionary == null)
{
    Debug.LogWarning("SpriteDict pool " + prefix + " requested before the dictionary exists");
    value.Add(prefix + 1);
    return value;
}
```

因此不能仅凭 Awake 的正常流程断言该对象在所有外部访问时都已初始化。
此前删除 Apply 初始化检查的依据不足，现已恢复。
Shutdown 注销键时同样先检查字典是否可用，自有资源仍正常释放。
这是参照原版承认的边界状态进行处理；没有声称已经在游戏中复现本 Mod 的初始化异常。

### 保留资源缺失处理的依据

原版 `ModLoader.cs:1295–1298` 仅在 Textures 目录存在时加载；
`:1315` 只枚举实际存在的 png/jpg，`:1330` 写入 Textures 字典。
因此字典不保证包含某个指定文件名。保留 TryGetValue 失败后的日志和跳过具有明确输入边界依据。
`:1317` 新建 Texture2D 后才写入字典，未找到向此字典写 null 的代码。

### 其他删除的依据与限度

缓存只由本类写入，Load 成功后才登记；本类唯一销毁路径是 Shutdown，随后清表。
没有找到缓存对象被外部销毁、成功创建的 Sprite 没有自有纹理的具体代码路径，故不保留这些推测性兜底。

删除 catch(Exception) 是取消本项目未设计恢复策略的统一吞错，不能描述为“原版证明创建操作不会抛异常”。
原版 Texture/Sprite 使用代码提供接口调用依据，但无法证明所有 Unity 原生异常都不存在。
错误现在正常暴露；本轮没有承诺贴图创建已经过真实 Unity 运行验证。

## 3. 重复声望限制属于本项目规则

这项不存在对应的原版医院剧情，不能提供伪造的原版业务依据。

完整的本项目证据链为：

- `AcidBuyerStory.cs:148–176`：20、40 声望事件均配置 MaxTimes=1。
- `StoryEventSelector.cs:45–46`：统一排除 SeenCount 已达到 MaxTimes 的事件。
- `StoryPlayer.cs:147–151`：注册事件正常返回时统一 MarkSeen。
- 20 声望事件对 40 声望事件的跨事件检查仍保留，避免先完成高阶段后补播低阶段。

因此删除的是自身播放次数的重复判断；没有改变次数限制。
两个使用真实注册表和选择器的回归用例验证了不重播及不补播低阶段。

## 4. 注销立绘键与释放自有资源

原版 `SpriteDict.cs:248–255` 的 GetSprite 对字典命中的值直接返回，只有缺键才走后备立绘：

```csharp
if (spriteDictionary.TryGetValue(key, out var value))
    return value;
```

本类 Apply 写入自定义键，Shutdown 销毁所生成的 Sprite；若保留键，原版仍按“键已命中”返回该值。
因此在销毁前注销自己登记的键，与原版查找语义一致。
不删除 ModLoader 持有的原始 source 贴图，只销毁本类创建的 canvas 和 Sprite。

原版 `ModLoader.cs:682`、`:1464` 分别在运行中停止与退出时调用 OnDisable，
本项目 `FFModEntry.OnDisable → StoryBootstrap.Shutdown → NpcPortraitRegistry.Shutdown` 接入释放。
字典尚不可用时跳过注销，不妨碍自有资源释放。

本项是登记和释放的对称清理，不把它称作已经复现的玩家可见故障。

## 结论

跳过和立绘访问/注销有直接的原版调用依据；声望限制依赖本框架自己的规则。
立绘初始化判空的过度删除已纠正。其余无路径防御的删除不等于证明底层永不抛异常。
