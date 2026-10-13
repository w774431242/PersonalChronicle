using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 班组（制造业「轮班作业组」编制单位）：一组固定成员，按<b>倒班表</b>轮换承担不同班次。
    ///
    /// <para><b>与既有概念的分工</b>（对齐现实制造企业）：</para>
    /// <list type="bullet">
    /// <item><b>班组（本类）</b>= 编制单位（甲班/乙班/丙班/丁班），有固定成员与人数；</item>
    /// <item><b>班次</b>（<see cref="WorkshopShift"/> 的时段 + 名额）= 一天里的作业窗口（早/中/夜）；</item>
    /// <item><b>倒班表</b>（<see cref="PersonalChronicle.Domain.WorkshopCrewCycle"/>）=
    /// 班组 × 日期 × 班次 的轮转关系 —— 这是"三班倒 / 四班三运转"的本质：**班组跟着表轮班**，
    /// 而不是每条排班各自指定固定时段。</item>
    /// </list>
    ///
    /// <para>存档：随 <c>Zone_Workshop</c> 以 <c>LookMode.Deep</c> 持久化，
    /// 成员用 <c>LookMode.Reference</c>（与 <c>Zone_Workshop.members</c> 同规格）。</para>
    /// </summary>
    public sealed class WorkshopCrew : IExposable
    {
        /// <summary>班组名（默认按甲乙丙丁/ABCD 自动命名，玩家可改）。</summary>
        public string Name = "A";

        /// <summary>班组成员（编制）；为空 = 该班组无人（排班表会提示编制缺口）。</summary>
        public List<Pawn> Members = new List<Pawn>();

        /// <summary>
        /// 本班组被指派负责（覆盖）的班次清单（<b>运行时解析结果</b>）。玩家可在班组设置里勾选该车间已有的班次，
        /// 使该班组成员并入这些班次的候选池（与全局倒班表 <see cref="PersonalChronicle.Domain.WorkshopCrewCycle"/>
        /// 叠加，二者都满足者优先）。为空 = 仅按倒班表轮转，不做显式指派。
        ///
        /// <para>⚠ <b>不持久化</b>（2026-10-02）：<c>WorkshopShift</c> 不是 <c>ILoadReferenceable</c>，
        /// 以 <c>LookMode.Reference</c> 保存本列表会让整次存档失败。持久化形式改为
        /// <see cref="AssignedShiftIds"/>（按 ID 关联），读档后由
        /// <c>MapComponent_WorkshopManager.EnsureShiftIdsAndAssignments</c> 还原本列表。</para>
        ///
        /// <para>写入请一律走 <see cref="AssignShift"/> / <see cref="UnassignShift"/>，保证两份数据同步。</para>
        /// </summary>
        [Unsaved]
        public List<WorkshopShift> AssignedShifts = new List<WorkshopShift>();

        /// <summary>
        /// 指派班次的<b>持久化形式</b>（<see cref="WorkshopShift.LoadId"/> 列表）。
        ///
        /// <para>2026-10-02：取代原先的 <c>LookMode.Reference</c> 写法 —— 后者要求元素实现
        /// <c>ILoadReferenceable</c>（原版 <c>Scribe_References.Look&lt;T&gt; where T : ILoadReferenceable</c>），
        /// 而 <c>WorkshopShift</c> 只实现 <c>IExposable</c> ⇒ 列表非空时保存必抛
        /// <c>InvalidOperationException</c>、<b>整次存档报废</b>（用户 2026-10-02 实机报错）。</para>
        /// </summary>
        public List<int> AssignedShiftIds = new List<int>();

        /// <summary>
        /// 岗位→班次的<b>固定绑定</b>（<see cref="WorkshopShift.LoadId"/>；CHG-20261003-WORKSHOP-SPLIT · 修成因 A）。
        ///
        /// <para><b>为什么需要它</b>：作息策略 FixedPerShift（默认）要求一个人的作息时段
        /// <b>永久绑定同一个班次</b>。若一个班组被指派多个班次且不记绑定，
        /// 每次都要重算"绑定哪个"—— 一旦某次重算结果变了（新增/删除班次导致 ID 升序变化），
        /// 玩家的作息表会<b>毫无征兆地跳到另一个班次</b>。持久化该绑定即可消除这种漂移。</para>
        ///
        /// <para><b>加性可选字段</b>：<c>-1</c> = 未绑定；旧档没有该节点。
        /// 常见情形（一个班组只被指派 1 个班次）天然固定 ⇒ <b>无需存储</b>，
        /// 解析时直接取那唯一的班次（见 <see cref="Domain.WorkshopRosterRules.ResolveFixedShiftId"/>）。</para>
        /// </summary>
        public int FixedShiftId = -1;

        /// <summary>该班次是否已被本班组指派（引用口径）。</summary>
        public bool IsAssigned(WorkshopShift shift)
        {
            return shift != null && AssignedShifts != null && AssignedShifts.Contains(shift);
        }

        /// <summary>指派一个班次（引用与 ID 两份数据同步写入；重复指派幂等）。</summary>
        public void AssignShift(WorkshopShift shift)
        {
            if (shift == null)
            {
                return;
            }
            if (AssignedShifts == null)
            {
                AssignedShifts = new List<WorkshopShift>();
            }
            if (!AssignedShifts.Contains(shift))
            {
                AssignedShifts.Add(shift);
            }
            if (shift.LoadId >= 0)
            {
                if (AssignedShiftIds == null)
                {
                    AssignedShiftIds = new List<int>();
                }
                if (!AssignedShiftIds.Contains(shift.LoadId))
                {
                    AssignedShiftIds.Add(shift.LoadId);
                }
            }
        }

        /// <summary>解除指派（同 <see cref="AssignShift"/> 的双份同步；不存在该指派时无副作用）。</summary>
        public void UnassignShift(WorkshopShift shift)
        {
            if (shift == null)
            {
                return;
            }
            if (AssignedShifts != null)
            {
                AssignedShifts.Remove(shift);
            }
            if (shift.LoadId >= 0 && AssignedShiftIds != null)
            {
                AssignedShiftIds.Remove(shift.LoadId);
            }
        }

        public WorkshopCrew()
        {
        }

        public WorkshopCrew(string name)
        {
            Name = name;
        }

        /// <summary>班组在编人数（不含 null/已销毁引用）。</summary>
        public int Headcount
        {
            get
            {
                if (Members == null)
                {
                    return 0;
                }
                int count = 0;
                for (int i = 0; i < Members.Count; i++)
                {
                    Pawn pawn = Members[i];
                    if (pawn != null && !pawn.Destroyed)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// 直加入（不查其它班组）。<b>仅供 <see cref="Zone_Workshop.AssignPawnToCrew"/> 内部使用</b>：
        /// 「一人一组」不变式由 zone 层保证，其它调用方必须走
        /// <see cref="Zone_Workshop.AssignPawnToCrew"/>，否则会复现一人多组（AUDIT-2026-WSCREW-INVARIANT）。
        /// </summary>
        internal void Add(Pawn pawn)
        {
            if (Members == null)
            {
                Members = new List<Pawn>();
            }
            if (pawn != null && !Members.Contains(pawn))
            {
                Members.Add(pawn);
            }
        }

        public void Remove(Pawn pawn)
        {
            if (pawn != null && Members != null)
            {
                Members.Remove(pawn);
            }
        }

        /// <summary>清理已死亡/已删除引用（purge 与刷新路径调用）。</summary>
        public void PurgeDead()
        {
            if (Members != null)
            {
                Members.RemoveAll(p => p == null || p.Destroyed);
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Name, "name", "A");
            Scribe_Collections.Look(ref Members, "members", LookMode.Reference);
            // 2026-10-02：改为「按 ID 持久化」。旧写法（AssignedShifts + LookMode.Reference）在列表
            // 非空时会抛 "Cannot save reference to ... if it is not ILoadReferenceable" ⇒ 整次存档失败。
            Scribe_Collections.Look(ref AssignedShiftIds, "assignedShiftIds", LookMode.Value);
            // CHG-20261003-WORKSHOP-SPLIT：岗位→班次固定绑定（加性；-1 = 未绑定）。
            Scribe_Values.Look(ref FixedShiftId, "fixedShiftId", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (Members == null)
                {
                    Members = new List<Pawn>();
                }
                if (AssignedShiftIds == null)
                {
                    AssignedShiftIds = new List<int>();
                }
                // AssignedShifts（引用列表）由 MapComponent_WorkshopManager.RebuildAssignedShifts 按 ID 还原；
                // 这里只保证非 null（LoadingVars 阶段还取不到班次列表）。
                if (AssignedShifts == null)
                {
                    AssignedShifts = new List<WorkshopShift>();
                }
            }
        }
    }
}
