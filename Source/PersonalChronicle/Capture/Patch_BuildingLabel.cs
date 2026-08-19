using HarmonyLib;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// v1.1.4 原版建筑 Label 运行时覆盖：Postfix on <c>ThingWithComps.get_Label</c>。
    ///
    /// 当玩家通过 ITab 改名对话框设置工坊别名时，别名被写入
    /// <see cref="Data.ChronicleGameComponent.BuildingLabelOverrides"/>（key=thingIDNumber）。
    /// 本 Postfix 在每次任何 Thing 的 Label 被读取时检查该表，命中则返回自定义名，
    /// 实现原版 UI（选中建筑信息面板、房间信息面板等）也显示改名后的名字。
    ///
    /// 性能注意：get_Label 是高频调用（每帧数百次），但 Dictionary&lt;int,string&gt; 查找是 O(1)，
    /// 且大部分 Thing 不在 overrides 表中，开销极低（一次 int 字典查找 + string 比较）。
    /// </summary>
    [HarmonyPatch(typeof(Thing), "get_Label")]
    internal static class Patch_BuildingLabel
    {
        static void Postfix(Thing __instance, ref string __result)
        {
            try
            {
                // 仅当有覆盖表且当前结果非空时才尝试替换。
                var component = Current.Game?.GetComponent<Data.ChronicleGameComponent>();
                if (component == null || component.BuildingLabelOverrides == null
                    || component.BuildingLabelOverrides.Count == 0)
                {
                    return;
                }

                // thingIDNumber 是 public int 字段（反射核验确认）。
                int id = __instance.thingIDNumber;
                if (id <= 0) return;

                string customLabel;
                if (component.BuildingLabelOverrides.TryGetValue(id, out customLabel)
                    && !string.IsNullOrWhiteSpace(customLabel))
                {
                    __result = customLabel;
                }
            }
            catch (System.Exception)
            {
                // get_Label is hot path — never let a transient failure break the call site.
            }
        }
    }
}
