# 剧情开发指南

这套框架只负责两件事：

```text
节点 = 暂停状态，等待玩家选择
边   = 自动剧情，执行完抵达目标节点
```

开发者新增一个剧情，只需要写：

```text
1. 文本
2. 边
3. 装配类
```

不需要写节点类。

## 目录

每个剧情一个独立目录：

```text
src/Story/Stories/<剧情名>/
├─ <剧情名>Texts.cs          本剧情全部文本
├─ <剧情名>Assembly.cs       起点、节点、边的连接关系
├─ <剧情名>Npc.cs           剧情 NPC（可选）
└─ Edges/
   ├─ XxxGreetingEdge.cs
   ├─ XxxDemoEdge.cs
   └─ XxxExitEdge.cs
```

参考剧情：

```text
src/Story/Stories/FlowEmitterDemo/
```

通用运行时在：

```text
src/Story/Runtime/
```

## 1. 写文本

文本文件只存纯文本：

```csharp
internal static class MyStoryTexts
{
    internal const string Greeting = "你好。";
    internal const string Prompt = "你想看哪个？";
    internal const string OptionA = "选项 A";
    internal const string OptionB = "选项 B";
}
```

文本不包含 NPC 名字或玩家名字。

- NPC 台词统一用 `player.SayNpc(...)`，显示时取外部传入的 NPC 名字。
- 玩家台词统一用 `player.SayPlayer(...)`，显示时取 `PlayerStore.Instance.playerName`。
- 选项标签固定使用玩家名字。

## 2. 写边

一条边就是一个类，继承 `StoryEdge`。

### 普通自动边

```csharp
internal sealed class MyGreetingEdge : StoryEdge
{
    internal MyGreetingEdge()
        : base(null, null)
    {
    }

    internal override async Task Execute(StoryPlayer player)
    {
        await player.SayNpc(MyStoryTexts.Greeting);
    }
}
```

### 玩家选择边

玩家选项需要文字和图标：

```csharp
internal sealed class MyDemoEdge : StoryEdge
{
    internal MyDemoEdge()
        : base(MyStoryTexts.OptionA, "$")
    {
    }

    internal override async Task Execute(StoryPlayer player)
    {
        await player.SayNpc(MyStoryTexts.Prompt);
        await player.RunFlow(FlowEventApi.RunBoundaryEmitter);
        await player.Wait(0.75f);
    }
}
```

`"question"` 是原版物品 key，用作选项图标；不是事件 ID。

### 结束边

```csharp
internal sealed class MyExitEdge : StoryEdge
{
    internal MyExitEdge()
        : base(MyStoryTexts.OptionB, "x")
    {
    }

    internal override Task Execute(StoryPlayer player)
    {
        return Task.CompletedTask;
    }
}
```

## 3. 写装配类

装配类只做：

```text
创建节点
创建边
设置每条边的起点和终点
指定入口边
```

```csharp
internal sealed class MyStoryAssembly
{
    internal StoryEdge Entry { get; }

    internal MyStoryAssembly()
    {
        StoryNode root = new StoryNode(MyStoryTexts.Prompt);

        MyGreetingEdge greeting = new MyGreetingEdge();
        MyDemoEdge demo = new MyDemoEdge();
        MyExitEdge exit = new MyExitEdge();

        // Connect(起点, 终点)
        greeting.Connect(null, root);
        demo.Connect(root, root);
        exit.Connect(root, null);

        Entry = greeting;
    }
}
```

规则：

```text
null → root   入口边
root → root   自循环，例如演示完回到选择
root → null   结束剧情
```

如果有多条出边，它们就是同一个节点的多个选项。

## 4. 启动剧情

```csharp
var assembly = new MyStoryAssembly();
var player = new StoryPlayer(
    grid,
    () => PlayerStore.Instance.currentClientInstance
        .GetClientBlueprint().displayName,
    onExit);

_ = player.Start(assembly.Entry);
```

`onExit` 在剧情到达 `null` 时调用。

## 5. 剧情 NPC（可选）

如果剧情需要一个到店 NPC 来触发，写一个剧情专属 NPC 类：

```csharp
StoreClient client = new StoreClient
{
    identifier = "my_story_npc",
    displayName = "剧情 NPC",
    clientIntent = StoreClient.ClientIntent.DIALOGUE,
    mainDialogue = null,
    OnArrival = StartStory,
};
```

要点：

```text
mainDialogue = null        避免原版再启动一套主对话
OnArrival = StartStory     到店时启动 StoryPlayer
displayName                剧情显示名，由游戏 StoreClient 提供
```

启动时把 NPC 加入队列：

```csharp
PlayerStore.Instance.storeClientManager.AddNextClient(client);
```

剧情结束时：

```csharp
PlayerStore.Instance.DismissCurrentClient(callNextWhenPossible: false);
```

## 当前示例图

```text
入口边（NPC 开场）
    ↓
root 节点
├─ 流源发射机边 → 整套演示 → root
└─ 都知道了边 → null
```

## 最小检查清单

新增剧情时确认：

```text
[ ] 文本全部在 <剧情名>Texts.cs
[ ] 每条边是一个剧情专属类
[ ] 每条边实现 async Task Execute(StoryPlayer)
[ ] 装配类只调用 Connect，不写台词/事件
[ ] 入口边 Connect(null, root)
[ ] 结束边 Connect(root, null)
[ ] 自循环边 Connect(root, root)
[ ] 节点统一用 new StoryNode(prompt)
[ ] 启动用 StoryPlayer.Start(assembly.Entry)
```

## 不要做

```text
不要为每个暂停节点写一个节点类
不要在装配类里写事件或台词
不要在边构造函数里写执行逻辑
不要用字符串 ID 查事件
```

节点只有一种：

```text
new StoryNode(prompt)
```

剧情专属的是：

```text
文本
边
边的执行
装配关系
```
