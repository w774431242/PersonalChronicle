using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// Partial of <see cref="ChronicleGameComponent"/> —— **编排布局持久化**（PM 裁决「存入」）。
    ///
    /// <para><b>为什么必须随存档</b>：布局项是「<b>车间区 ID</b> → 画布绝对坐标」，而 <c>Zone.ID</c>
    /// 是**存档范围内的对象 ID**（换存档即无意义）。放进全局的 ModSettings 会**串档**
    /// （A 档的摆放出现在 B 档）。故只能挂在随存档序列化的 GameComponent 上。</para>
    ///
    /// <para><b>SchemaVersion 影响：无（加性可选字段）</b> —— SAVE 规范允许新增"旧档不存在即回落默认"
    /// 的字段：这里用 <c>Scribe_Collections.Look(..., LookMode.Deep)</c>，旧档读回 <c>null</c>，
    /// 取用时统一走 <see cref="ManufacturingLayout"/> 回落为空列表；**不改动既有字段语义**，
    /// 因此不需要新增迁移步骤（不需要 bump SchemaVersion）。</para>
    /// </summary>
    public sealed partial class ChronicleGameComponent
    {
        /// <summary>一条布局项：车间区 ID + 画布绝对坐标（原点居中、+X→右 / +Y↓下）。</summary>
        public sealed class LayoutEntry : IExposable
        {
            /// <summary>目标车间区 <c>Zone.ID</c>（-1 = 无效）。</summary>
            public int ZoneId = -1;

            public float X;
            public float Y;

            public void ExposeData()
            {
                Scribe_Values.Look(ref ZoneId, "zoneId", -1);
                Scribe_Values.Look(ref X, "x", 0f);
                Scribe_Values.Look(ref Y, "y", 0f);
            }
        }

        // 会话内缓存（读档后为 null，首次取用时回落空列表）
        private List<LayoutEntry> manufacturingLayout;

        /// <summary>只读视图；未初始化时回落为空列表（调用方不必判空）。</summary>
        internal List<LayoutEntry> ManufacturingLayout
        {
            get { return manufacturingLayout ?? (manufacturingLayout = new List<LayoutEntry>()); }
        }

        /// <summary>由 <see cref="ChronicleGameComponent.ExposeData"/> 调用（在既有字段之后）。</summary>
        private void ExposeLayout()
        {
            Scribe_Collections.Look(ref manufacturingLayout, "mfgLayout", LookMode.Deep);
            if (manufacturingLayout == null)
            {
                manufacturingLayout = new List<LayoutEntry>();
            }
        }

        /// <summary>
        /// 写入/更新一条（同 ZoneId 覆盖）。
        ///
        /// <para>⚠ 2026-09-15（审计 UI-001 / PERF-002 修复）：**值未变即跳过**。
        /// 调用方 `MasterControlTabWindow.AppShell.MirrorLayoutToSave()` 位于**绘制路径**（每帧调用），
        /// 此前无比较 ⇒ 对同一坐标反复赋值（无意义写盘语义 + 每帧全量遍历）。
        /// 容差 0.01：亚像素位移无视觉意义，不值得写档。</para>
        /// </summary>
        internal void SetLayoutEntry(int zoneId, float x, float y)
        {
            if (zoneId < 0)
            {
                return;
            }
            List<LayoutEntry> list = ManufacturingLayout;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].ZoneId == zoneId)
                {
                    if (System.Math.Abs(list[i].X - x) < 0.01
                        && System.Math.Abs(list[i].Y - y) < 0.01)
                    {
                        return;   // 值未变 ⇒ 零写入（绘制路径每帧调用也安全）
                    }
                    list[i].X = x;
                    list[i].Y = y;
                    return;
                }
            }
            list.Add(new LayoutEntry { ZoneId = zoneId, X = x, Y = y });
        }

        /// <summary>移除一条（车间被删/回收时调用）。</summary>
        internal void RemoveLayoutEntry(int zoneId)
        {
            List<LayoutEntry> list = ManufacturingLayout;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] != null && list[i].ZoneId == zoneId)
                {
                    list.RemoveAt(i);
                }
            }
        }

        /// <summary>清空（调试/重置用）。</summary>
        internal void ClearLayout()
        {
            ManufacturingLayout.Clear();
        }
    }
}
