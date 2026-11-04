using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 快照刷新后，把 tag 的新值同步回装置上的副本。
    ///
    /// 为什么需要：装置的库存（如 <c>CollectorDevice.Stock</c>）是 tag 的一份内存副本。
    /// 原版倒水（<c>WaterHelper.AddWater</c>）、药物溶解（<c>ChemicalHelper.DissolveInto</c>）、
    /// 环境降温（<c>HandleAmbientTempDrop</c>）都直接改 tag，装置不会收到任何通知，
    /// 副本就停留在旧值——下次装置写 tag 时会把原版改的内容覆盖掉。
    ///
    /// 为什么挂在 <see cref="GameItem.ValidateShapeState"/> 而不是 <c>ModifyTag</c>：
    /// <c>ModifyTag</c> 改的是 <c>state</c>，<c>modifiedState</c> 是它的克隆，只有
    /// <c>ValidateShapeState</c> 里那句 <c>ComputeModifiedShapeState</c> 才会重算。
    /// 而 <c>FlowStock.ReadInto</c> 读的是 <c>modifiedState</c>。
    /// 挂在 <c>ModifyTag</c> 上时（尤其 <c>skipValidate: true</c> 的调用）读到的是上一轮快照，
    /// 拿旧值覆盖装置比不同步更糟；挂在 <c>ValidateShapeState</c> 上则天然保证快照已刷新。
    ///
    /// 覆盖范围：所有让快照重算的路径——倒水、药物溶解、环境降温、进出容器、
    /// 玩家手动改 tag，不需要逐个 patch 业务函数。
    ///
    /// 性能：先查装置注册表（一次字典查找），没挂装置的物品直接返回；
    /// 只有真正挂着装置的物品才走 ReadInto。
    ///
    /// TODO（性能优化点，暂不做）：<c>FlowStock.ReadInto</c> 会遍历装置声明的全部
    /// 37 个槽位（36 液体 + 温度），而实际存在的 tag 通常只有 2 个。
    /// 如果后续出现卡顿、且 profile 显示本 patch 占比偏高，可以改成
    /// 「先查 <c>modifiedState.dict</c>，tag 不存在就跳过」——从 37 次降到 2 次。
    /// 代价是 tag 被删除时装置不会自动清零（当前液体 tag 由游戏创建后不会删，风险低）。
    /// 判断依据：<c>ValidateShapeState</c> 本身要遍历物品树 + 克隆 TagSystem + 刷 UI，
    /// 相比之下 ReadInto 只占千分之几，现在优化收益不明显。
    /// </summary>
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.ValidateShapeState))]
    public static class DeviceTagSyncPatch
    {
        public static void Postfix(GameItem __instance)
        {
            IFlowState device = DeviceRegistry.Get<IFlowState>(__instance);
            if (device == null)
            {
                return;
            }

            FlowStock.ReadInto(__instance, device);
        }
    }
}
