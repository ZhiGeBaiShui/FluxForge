#if FLOW_DIAGNOSTICS
using System;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>临时排查构建专用：只读检查，首次偏差写入原版 Player.log。</summary>
    internal static class FlowDiagnostics
    {
        private static bool _patchesReported;

        internal static void CheckFrame()
        {
            // 拿起时本来就会注销格子；只在拖拽结束后的稳定状态作完整性检查。
            if (ItemMouseDragHandler.current != null && ItemMouseDragHandler.current.IsDraggingItem) return;
            if (ItemMultiSelectHandler.current != null && ItemMultiSelectHandler.current.IsGroupDragging) return;
            foreach (FlowRuntime runtime in GridRegistry.Runtimes)
            {
                // 一块网格只自动报告一次；F8 仍可随时导出后续现场。
                if (runtime.DiagnosticMismatchReported || !runtime.IsAlive) continue;
                try
                {
                    string registration = runtime.Grid.FindRegistrationMismatch();
                    if (registration == null)
                    {
                        foreach (GameItem child in runtime.Inventory.childItems)
                        {
                            if (DeviceRegistry.GetHost(child) != null && !runtime.Grid.IsRegistered(child))
                            {
                                registration = "missing device registration item=" + child.GetHashCode();
                                break;
                            }
                        }
                    }
                    string visual = runtime.Visual == null ? null : runtime.Visual.FindCenterMismatch();
                    if (registration == null && visual == null) continue;

                    runtime.DiagnosticMismatchReported = true;
                    FFModEntry.Log.Error("[流源诊断][自动偏差] grid=" + runtime.Inventory.GetHashCode()
                        + " registration=" + (registration ?? "一致")
                        + " visual=" + (visual ?? "未发现可检测偏差"));
                    FFModEntry.Log.Log(runtime.Grid.DescribeRegistration());
                    if (runtime.Visual != null)
                        FFModEntry.Log.Log(runtime.Visual.DescribePositions());
                    ReportPatchOwners();
                }
                catch (Exception e)
                {
                    // 诊断本身失败不退役正常运行的网格，也不每帧重复报错。
                    runtime.DiagnosticMismatchReported = true;
                    FFModEntry.Log.Error("[流源诊断][检查失败] grid="
                        + runtime.Inventory.GetHashCode() + "：" + e);
                }
            }
        }

        internal static void LogEmission(FlowRuntime runtime, GameItem item, FlowPos origin, int direction)
        {
            FFModEntry.Log.Log("[流源诊断][发射尝试] frame=" + UnityEngine.Time.frameCount
                + " grid=" + runtime.Inventory.GetHashCode() + " item=" + item.GetHashCode()
                + " origin=(" + origin.X + "," + origin.Y + ") direction=" + direction);
            FFModEntry.Log.Log(runtime.Grid.DescribeRegistration());
            foreach (GameItem child in runtime.Inventory.childItems)
            {
                GameItemElement element = child as GameItemElement;
                if (element == null || DeviceRegistry.GetHost(child) == null) continue;
                FFModEntry.Log.Log("[流源诊断][装置UI] item=" + child.GetHashCode()
                    + " min=(" + child.modifiedShape.minX + "," + child.modifiedShape.minY + ")"
                    + " anchor=" + element.rectTransform.anchoredPosition
                    + " world=" + element.rectTransform.position
                    + " scale=" + element.rectTransform.lossyScale);
            }
        }

        internal static void LogDrop(FlowRuntime runtime, GameItem item)
        {
            FFModEntry.Log.Log("[流源诊断][拖拽收尾] frame=" + UnityEngine.Time.frameCount
                + " grid=" + runtime.Inventory.GetHashCode() + " item=" + item.GetHashCode());
            FFModEntry.Log.Log(runtime.Grid.DescribeRegistration());
            ReportPatchOwners();
        }

        internal static void LogStraightEntry(FlowRuntime runtime, FlowEntity entity, int x, int y,
            IFlowRouter router, string result)
        {
            // 诊断读取 Unity UI；UI 读取失败不能改变已经作出的入口判定。
            try
            {
                StraightDevice registered = router as StraightDevice;
                foreach (GameItem item in runtime.Inventory.childItems)
                {
                    if (DeviceRegistry.GetRouter(item) is not StraightDevice device) continue;
                    GridShape shape = item.modifiedShape;
                    var cell = shape.ForwardTransform(item.state.dict[FlowTags.AnchorX].GetInt(),
                        item.state.dict[FlowTags.AnchorY].GetInt());
                    if ((cell.Item1 != x || cell.Item2 != y) && device != registered) continue;
                    int direction = entity.Motion.Direction;
                    string info = "[流源诊断][直通入口] frame=" + UnityEngine.Time.frameCount
                        + " grid=" + runtime.Inventory.GetHashCode() + " flow=" + entity.GetHashCode()
                        + " cell=(" + x + "," + y + ") itemCell=" + cell + " item=" + item.GetHashCode()
                        + " direction=" + direction + " facing=" + device.Facing
                        + " orientation=" + shape.orientation + " registeredRouter=" + router?.GetType().Name
                        + " sameRouter=" + (registered == device) + " result=" + result;
                    if (item is GameItemElement element)
                    {
                        var graphic = element.background;
                        var axis = runtime.Inventory.inner.rectTransform.InverseTransformVector(
                            graphic.rectTransform.TransformVector(UnityEngine.Vector3.up));
                        bool horizontal = System.Math.Abs(axis.x) > System.Math.Abs(axis.y);
                        info += " graphicAxis=" + (horizontal ? "horizontal" : "vertical")
                            + " sprite=" + graphic.image.sprite?.name
                            + " matchesIncomingAxis=" + (horizontal == ((direction & 1) == 1));
                    }
                    FFModEntry.Log.Log(info);
                    if (result != "allowed" && device.LastAxisChange != null)
                    {
                        var trace = device.LastAxisChange;
                        var caller = trace.GetFrame(0)?.GetMethod();
                        FFModEntry.Log.Log("[流源诊断][直通最近轴向写入] callerMod="
                            + ModId(caller?.Module.Assembly) + " stack=" + trace);
                    }
                    ReportPatchOwners();
                }
            }
            catch (Exception e)
            {
                FFModEntry.Log.Error("[流源诊断][直通入口读取失败] " + e);
            }
        }

        private static string ModId(System.Reflection.Assembly assembly)
        {
            foreach (LoadedMod mod in ModLoader.Mods)
                if (assembly != null && mod.Assembly == assembly) return mod.Manifest.ID;
            return assembly?.GetName().Name ?? "unknown";
        }

        private static void ReportPatchOwners()
        {
            if (_patchesReported) return;
            _patchesReported = true;
            FFModEntry.Log.Log("[流源诊断] 下列补丁是排查线索，不代表已确认冲突来源。");
            foreach (var method in Harmony.GetAllPatchedMethods())
            {
                string type = method.DeclaringType.FullName;
                if (!type.Contains("Grid") && !type.Contains("GameItem")
                    && !type.Contains("ItemMouseDrag") && !type.Contains("ItemMultiSelect")
                    && !type.Contains("UISettings") && !type.Contains("RectTransform")
                    && !type.Contains("StraightDevice") && !type.Contains("FlowManager")
                    && !type.Contains("DeviceRegistry") && !type.Contains("FlowFacingAdapter")
                    && !type.Contains("FlowSpawner")) continue;
                Patches patches = Harmony.GetPatchInfo(method);
                foreach (Patch patch in patches.Prefixes) ReportPatch(type, method.Name, "prefix", patch);
                foreach (Patch patch in patches.Postfixes) ReportPatch(type, method.Name, "postfix", patch);
                foreach (Patch patch in patches.Transpilers) ReportPatch(type, method.Name, "transpiler", patch);
                foreach (Patch patch in patches.Finalizers) ReportPatch(type, method.Name, "finalizer", patch);
            }
        }

        private static void ReportPatch(string type, string method, string kind, Patch patch)
        {
            FFModEntry.Log.Log("[流源诊断][补丁] target=" + type + "." + method
                + " kind=" + kind + " owner=" + patch.owner
                + " mod=" + ModId(patch.PatchMethod.Module.Assembly)
                + " assembly=" + patch.PatchMethod.DeclaringType.Assembly.GetName().Name
                + " patch=" + patch.PatchMethod.DeclaringType.FullName + "." + patch.PatchMethod.Name);
        }
    }
}
#endif
