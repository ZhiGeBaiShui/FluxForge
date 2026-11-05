using System.Collections.Generic;

/// <summary>
/// 测试用最小 <c>GameItem</c> 桩：只保留 Mod 层读写 tag 所需的两个状态表与查询，
/// 外加装置耗尽路径需要的父容器（<see cref="TestInventory"/>）。
///
/// 游戏类型在测试环境不可加载，因此这里按 <c>GameItemTags</c> 的真实用法复刻：
///   · 读走 <see cref="modifiedState"/>（游戏校验过的快照）；
///   · 写走 <see cref="state"/>（活的 tag 表）；
///   · <see cref="IsTag"/> 同时检查两份状态。
/// </summary>
public sealed class GameItem
{
    public TagSystem state = new TagSystem();

    public TagSystem modifiedState = new TagSystem();

    /// <summary>装置耗尽时 <c>DeviceStateSync.Exhausted</c> 会把物品从这里移除。</summary>
    public TestInventory parentInventory = new TestInventory();

    /// <summary>物品是否带有该 tag（state 或 modifiedState 任一存在即可）。</summary>
    public bool IsTag(string tag)
    {
        return state.dict.ContainsKey(tag) || modifiedState.dict.ContainsKey(tag);
    }

    /// <summary>测试辅助：启用一个身份 tag（如 ice_block），让目录按 tag 找到装置定义。</summary>
    public void EnableTag(string tag)
    {
        state.GetTag(tag).Enable();
    }

    /// <summary>
    /// 测试辅助：按游戏语义重建快照（<c>modifiedState = state</c>）。
    /// 由 <see cref="FluxForge.Mod.ItemTagCommit"/> 桩在提交时调用。
    /// </summary>
    public void SyncModifiedState()
    {
        modifiedState = state;
    }

    /// <summary>测试辅助：把 int tag 写进 modifiedState 快照（模拟游戏同步后的可读状态）。</summary>
    public void SetModifiedTag(string tag, int value)
    {
        TagState tagState = modifiedState.GetTag(tag);
        tagState.Enable();
        tagState.SetInt(value);
    }

    /// <summary>测试辅助：把 double tag 写进 modifiedState 快照。</summary>
    public void SetModifiedTag(string tag, double value)
    {
        TagState tagState = modifiedState.GetTag(tag);
        tagState.Enable();
        tagState.SetDouble(value);
    }
}

/// <summary>测试用最小 <c>TagSystem</c> 桩：tag 名 → 状态，缺失时按需创建。</summary>
public sealed class TagSystem
{
    public readonly Dictionary<string, TagState> dict = new Dictionary<string, TagState>();

    /// <summary>取 tag；不存在时创建，与游戏一致。</summary>
    public TagState GetTag(string tag)
    {
        TagState tagState;
        if (!dict.TryGetValue(tag, out tagState))
        {
            tagState = new TagState();
            dict.Add(tag, tagState);
        }

        return tagState;
    }
}

/// <summary>测试用最小 <c>TagState</c> 桩：int / double 两个槽位 + enabled 标记。</summary>
public sealed class TagState
{
    private int _intValue;
    private double _doubleValue;

    /// <summary>是否已被写入方启用；写非零 tag 前调用 <see cref="Enable"/>。</summary>
    public bool Enabled { get; private set; }

    public int GetInt()
    {
        return _intValue;
    }

    public void SetInt(int value)
    {
        _intValue = value;
    }

    public double GetDouble()
    {
        return _doubleValue;
    }

    public void SetDouble(double value)
    {
        _doubleValue = value;
    }

    public void Enable()
    {
        Enabled = true;
    }
}

/// <summary>测试用最小父容器桩：只记录装置耗尽时的 <c>Expel</c> 调用。</summary>
public sealed class TestInventory
{
    /// <summary>是否收到过 <see cref="Expel"/>。</summary>
    public bool WasExpelled { get; private set; }

    /// <summary>最后一次被移除的物品，便于断言淘汰的是正确实例。</summary>
    public GameItem ExpelledItem { get; private set; }

    public void Expel(GameItem item)
    {
        WasExpelled = true;
        ExpelledItem = item;
    }
}

namespace FluxForge.Mod
{
    /// <summary>
    /// 测试用 <c>ItemTagCommit</c> 桩：DeviceStateSync 提交时只做快照同步与计数。
    ///
    /// 生产实现还会刷新水量贴图 / tooltip；测试环境没有游戏 sprite / WaterHelper，
    /// 因此这里只保留「CommitCount + SyncModifiedState」，不编入生产 ItemTagCommit.cs。
    /// </summary>
    public static class ItemTagCommit
    {
        /// <summary>Commit 调用次数；每个用例前后由测试重置。</summary>
        public static int CommitCount;

        public static void Commit(GameItem item)
        {
            item.SyncModifiedState();
            CommitCount++;
        }
    }
}
