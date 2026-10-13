using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// Partial of <see cref="ChronicleGameComponent"/> —— **设备卡编排位置持久化**（2026-09-26）。
    ///
    /// <para><b>与车间块布局（<c>mfgLayout</c>）的分工</b>：车间块按 <c>Zone.ID</c> 存；
    /// 设备卡按<b>稳定 ID（stableId）</b>存（GOV-009：逻辑只认稳定 ID，不认 label/翻译）。
    /// 二者同源：都挂在随存档序列化的 <c>GameComponent</c> 上，换档串档问题同样规避。</para>
    ///
    /// <para><b>SchemaVersion 影响：无（加性可选字段）</b> —— 与 <c>mfgLayout</c> 同款策略：
    /// <c>Scribe_Collections.Look(..., LookMode.Deep)</c>，旧档读回 <c>null</c> ⇒ 回落空列表
    /// ⇒ 设备回到默认网格位（SAVE-003 向后兼容），**不需要 Migration、不需要 bump 版本**。</para>
    /// </summary>
    public sealed partial class ChronicleGameComponent
    {
        /// <summary>一条设备布局项：设备稳定 ID + 画布坐标（原点居中、+X→右 / +Y↓下）。</summary>
        public sealed class DeviceLayoutEntry : IExposable
        {
            /// <summary>设备稳定 ID；空串视为无效项（读取时剔除）。</summary>
            public string StableId = string.Empty;

            public float X;
            public float Y;

            public void ExposeData()
            {
                Scribe_Values.Look(ref StableId, "stableId", string.Empty);
                Scribe_Values.Look(ref X, "x", 0f);
                Scribe_Values.Look(ref Y, "y", 0f);
            }
        }

        private List<DeviceLayoutEntry> manufacturingDeviceLayout;

        /// <summary>只读视图；未初始化时回落为空列表（调用方不必判空）。</summary>
        internal List<DeviceLayoutEntry> ManufacturingDeviceLayout
        {
            get
            {
                return manufacturingDeviceLayout ?? (manufacturingDeviceLayout = new List<DeviceLayoutEntry>());
            }
        }

        /// <summary>由 <see cref="ChronicleGameComponent.ExposeData"/> 调用（紧跟 <c>ExposeLayout()</c>）。</summary>
        private void ExposeDeviceLayout()
        {
            Scribe_Collections.Look(ref manufacturingDeviceLayout, "mfgDeviceLayout", LookMode.Deep);
            if (manufacturingDeviceLayout == null)
            {
                return;
            }
            // 单条损坏/空 ID 就地剔除（SAVE-003：降级到"该项不存在"，不阻止整档加载）。
            for (int i = manufacturingDeviceLayout.Count - 1; i >= 0; i--)
            {
                DeviceLayoutEntry entry = manufacturingDeviceLayout[i];
                if (entry == null || string.IsNullOrEmpty(entry.StableId))
                {
                    manufacturingDeviceLayout.RemoveAt(i);
                }
            }
        }

        /// <summary>写入/更新一条（同 stableId 覆盖；坐标无变化时不动，避免无意义脏写）。</summary>
        internal void SetDeviceLayoutEntry(string stableId, float x, float y)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }
            List<DeviceLayoutEntry> list = ManufacturingDeviceLayout;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].StableId == stableId)
                {
                    if (System.Math.Abs(list[i].X - x) < 0.01 && System.Math.Abs(list[i].Y - y) < 0.01)
                    {
                        return;
                    }
                    list[i].X = x;
                    list[i].Y = y;
                    return;
                }
            }
            list.Add(new DeviceLayoutEntry { StableId = stableId, X = x, Y = y });
        }

        /// <summary>移除一条（设备被"收回车间"/画布清空时调用）。</summary>
        internal bool RemoveDeviceLayoutEntry(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return false;
            }
            List<DeviceLayoutEntry> list = ManufacturingDeviceLayout;
            bool removed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] != null && list[i].StableId == stableId)
                {
                    list.RemoveAt(i);
                    removed = true;
                }
            }
            return removed;
        }
    }
}
