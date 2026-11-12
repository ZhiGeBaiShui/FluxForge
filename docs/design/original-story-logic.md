# 原版剧情/访客逻辑核对

## 目的

设计前先确认原版到底怎么安排“剧情客户”，避免把原版没有的“每日新剧情”节奏塞进 Mod。

证据来自游戏反编译源码：

```text
StoreClientManager.cs
StoreClientListStory.cs
StoreClientListAppraisal.cs
StoreClient.cs
StoreClientListSpec.cs
```

---

## 1. 原版不是每日剧情

原版没有一个“每天生成一条新剧情”的调度器。

剧情客户和特殊客户由 `StoreClientManager` 在开门/换日流程里按条件追加。

典型代码：

```csharp
// StoreClientManager.HandleAlwayClient()
if (12 == StoreStation.Instance.dayCounter
    && !PlayerStore.Instance.isAugIntroduced)
{
    AddClient(StoreClientListStory.CreateMentorShotgun());
}

if (24 == StoreStation.Instance.dayCounter
    && !PlayerStore.Instance.hasBusinessPermit)
{
    AddClient(StoreClientListStory.CreateMentorPermit());
}

if (38 <= dayCounter
    && dayCounter % 7 == 3
    && !hasBusinessPermit)
{
    AddClient(StoreClientList.CreateBusinessPermit());
}
```

特征：

```text
按具体天数
按未完成 flag
按周期（% 7）
按玩家行为/选择/开局身份
```

不是每天固定一条。

---

## 2. 原版剧情客户的密度

同一处 `HandleAlwayClient` 里同时检查很多条件，但真正命中的通常只有一两个。

```text
第 2 天：wanted 客户
第 5 天：鉴定板剧情
第 10 天：市场/剧情检查
第 12 天：导师霰弹枪剧情
第 18 天：房东批发
第 24 天：营业执照剧情
第 25 天：地下交易所剧情
第 38 天后：每 7 天一次营业执照催办
```

玩家通常不会每天遇到一个新的“主线人物”。

日常客户仍然由：

```text
随机普通客户
短缺/过剩客户
声望解锁客户
事件客户
```

填充。

---

## 3. 原版剧情客户经常不可遣散

`StoreClientListStory` 的多个剧情客户：

```csharp
new StoreClient
{
    identifier = "mentor",
    clientIntent = StoreClient.ClientIntent.DIALOGUE,
    dismissable = false,
    mainDialogue = StoreClientDialogList.MentorIntroDialog()
};
```

```csharp
storeClient.dismissable = false;
storeClient.mainDialogue
    .SetText(...)
    .NextDialogue()
    ...
    .SetEndAction(() => { ... DismissCurrentClient(...); });
```

含义：

```text
原版为了确保教学/主线信息送达，会强制玩家听完；
剧情客户在对话结束前不能正常遣散。
```

这与用户要求的“玩家有绝对自由、可以拒绝后续剧情链”冲突。

因此：

```text
保留原版的低频、条件门控
不照搬原版的不可拒绝
```

---

## 4. 原版有“临时访客”模式

`StoreClientListAppraisal.CreateAppraisalRandom()`：

```csharp
storeClient.identifier = "scavAppraisalRandom";
storeClient.clientIntent = StoreClient.ClientIntent.APPRAISAL_SERVICE;
storeClient.mainDialogue
    .SetText(...)
    .NextDialogue()
    .SetText(...)
    .SetEndAction(() =>
    {
        // 随机生成 2–5 件物品放到前台
    });
```

特征是：

```text
临时生成
随机到访
一次交易/服务
不进入固定主线
不要求玩家记住人物
```

这正适合“偶尔来一个收购高质量化学品的人”，而不是每天固定剧情。

---

## 5. 原版客户收购逻辑

`StoreClient` 的收购判断：

```csharp
public bool IsClientRefusingItem(GameItem gameItem)
{
    ...
    if (ChemicalProductHelper.HasChemicalPurity(gameItem)
        && !ChemicalProductHelper.WillAcceptNarcotic(
            gameItem, acceptedChemicalProductGrade))
        return true;
    ...
}
```

`StoreClient` 还维护：

```text
clientBuyingTagList
clientBuyingIdList
acceptedChemicalProductGrade
CanExposeChem
budget
```

说明原版客户可以：

```text
按 tag / id 选择要买的物品
按化学品质等级决定是否接受
有预算限制
```

这适合做“站务采购员”：

```text
偶发到访
按品质/化学残留收购
有预算
不进入固定剧情链
```

---

## 6. 原版剧情选择的真实情况

原版剧情客户内部有对话分支和 choices，但：

```text
很多剧情客户本身不可拒绝
选择通常发生在对话内部
不是“玩家可以拒绝整条剧情链”
```

所以本 Mod 要做的不是复制原版，而是：

```text
保留“条件驱动、低频出现”
增加“玩家可以拒绝整条链”
```

---

## 7. 对本 Mod 的结论

```text
1. 不做每日新剧情
2. 剧情链按条件/天数/前置 flag 解锁
3. 同一时间最多一条主动剧情链
4. 每条链之间至少间隔 2–4 个游戏日
5. 每个链节点都可以明确拒绝
6. 拒绝后不再自动追加后续节点
7. 拒绝不扣钱、不扣声望、不惩罚玩家
8. 临时采购/鉴定客户可以偶发，不占剧情链槽位
9. 原版不可遣散的强制剧情，不作为本 Mod 的设计模板
```

---

## 8. 设计含义

原版是：

```text
事件调度器 + 条件客户 + 少量强制剧情
```

本 Mod 应是：

```text
低频剧情链 + 玩家主动接受 + 可随时退出
临时采购客户作为经济补充
日常普通客户保持原版随机性
```
