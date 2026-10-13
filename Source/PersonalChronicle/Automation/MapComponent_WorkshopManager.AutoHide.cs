using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// CHG-20261004-AUTOHIDE · 「加入车间即自动隐藏」执行层。
    ///
    /// 玩家口径（2026-10-04）：殖民者加入车间后，自动从【工作】优先级面板隐藏
    /// （不再需要逐个点 gizmo 隐藏），且其原版工作类型只保留车间需要的那些
    /// （后者由 WorkTakeover.TakeOverVanillaWork 负责，本类不重复实现）。
    ///
    /// 为什么与接管共用同一份「车间成员」口径：隐藏与接管是同一件事的两面
    /// （这个人的活在车间里管）。若两处各判一次「是否在车间」，迟早分叉成
    /// 「被隐藏但仍在原版工作」或「被接管却仍显示在面板」两种自相矛盾的状态（GOV-005）。
    ///
    /// <para><b>2026-10-05 更新（CHG-20261005-NODEMOTE）</b>：本节原写「刻意不做『离队自动取消隐藏』」，
    /// 理由是"隐藏名单同时服务手动隐藏，自动取消会把玩家手动隐藏的人一起放出来"。
    /// 该理由已由**分账**解决：PC 自动隐藏写入独立的
    /// <c>ChronicleGameComponent.autoHiddenWorkTabColonists</c> ⇒ 现在**会**在成员移出车间体系时
    /// 自动取消隐藏（只动自动集合，玩家手动隐藏的人**依旧不受影响**），
    /// 实现见 <c>ReconcileAutoHideOnLeave</c> 的成员差集对账。</para>
    /// <para>需要一次性恢复全部（含玩家手动隐藏的）时，仍可用 Mod 设置里的
    /// 「恢复显示全部殖民者」—— 现为两集合一起清。</para>
    /// </summary>
    public partial class MapComponent_WorkshopManager
    {
        /// <summary>
        /// 本车间「属于车间」的全部殖民者 = 人员名单 ∪ 班组编制（去重、排除已销毁）。
        /// 供接管与自动隐藏共用，保证两者口径一致。
        /// </summary>
        public List<Pawn> AllWorkshopMemberPawns(Zone_Workshop zone)
        {
            var result = new List<Pawn>();
            if (zone == null)
            {
                return result;
            }
            var seen = new HashSet<Pawn>();

            List<Pawn> roster = zone.Members;
            for (int i = 0; roster != null && i < roster.Count; i++)
            {
                Pawn p = roster[i];
                if (p != null && !p.Destroyed && seen.Add(p))
                {
                    result.Add(p);
                }
            }

            List<WorkshopCrew> crews = zone.Crews;
            for (int c = 0; crews != null && c < crews.Count; c++)
            {
                List<Pawn> members = crews[c] != null ? crews[c].Members : null;
                for (int m = 0; members != null && m < members.Count; m++)
                {
                    Pawn p = members[m];
                    if (p != null && !p.Destroyed && seen.Add(p))
                    {
                        result.Add(p);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 把本车间全体成员加入【工作】面板隐藏名单（幂等）。
        /// </summary>
        /// <returns>本次新隐藏的人数（0 = 全部已在名单里）。</returns>
        public int SyncAutoHideWorkTabColonists(Zone_Workshop zone)
        {
            Data.ChronicleGameComponent component = Current.Game != null
                ? Current.Game.GetComponent<Data.ChronicleGameComponent>()
                : null;
            if (component == null)
            {
                return 0;
            }
            List<Pawn> members = AllWorkshopMemberPawns(zone);
            int added = 0;
            for (int i = 0; i < members.Count; i++)
            {
                Pawn p = members[i];
                // ⚠ CHG-20261005-NODEMOTE：改走**自动集合**（原先直接写 HiddenWorkTabColonists）。
                //   分账理由见 ChronicleGameComponent.autoHiddenWorkTabColonists 注释：
                //   唯有分开，才能在"人员移出车间"时只放出自动的、而不动玩家手动隐藏的人。
                if (component.SetAutoHiddenFromWorkTab(p, true))
                {
                    added++;
                }
            }
            return added;
        }

        // ---- CHG-20261005-NODEMOTE · 移出车间即恢复（A 方案的核心机制）------------------

        /// <summary>
        /// 上一轮对账时"在册"的车间成员快照（**运行态，不入存档**）。
        /// <para>与 <c>idleSplitCapacityByZone</c> 同款先例：放 Manager 而非 View
        ///（View 每次开窗都 new，实例字段会被重置）；不入存档 ⇒ 读档后首次对账快照为空
        /// ⇒ 差集为空 ⇒ **不会误把任何人判成"已离开"**。</para>
        /// </summary>
        private readonly List<Pawn> lastWorkshopMembers = new List<Pawn>();

        /// <summary>本轮成员收集缓冲（复用，避免每次对账分配）。</summary>
        private readonly List<Pawn> reconcileScratch = new List<Pawn>();

        /// <summary>
        /// **成员差集对账**（CHG-20261005-NODEMOTE）：把「上一轮在册、本轮已不在册」的成员
        /// 视为已移出车间体系 ⇒ ① 从**自动隐藏集合**移除（原版工作面板恢复显示该殖民者）；
        /// ② **按人解除接管**（把当初被取消勾选的车间类工作还原）。
        ///
        /// <para><b>为什么用差集而不是给每个移除入口挂钩</b>：取证显示"移出车间"至少四条路径
        /// （人员页移除 / 班组编辑器移出 / 删除班组 / 删除班次），且成员口径是
        /// <c>zone.Members ∪ crew.Members</c> —— 逐入口挂钩必然漏、且易与成员口径分叉。
        /// 差集对账只看"结果状态"，天然覆盖全部路径（含死亡 / 换图 / 读档）。</para>
        ///
        /// <para><b>口径纪律</b>：本轮成员集合用 <see cref="AllWorkshopMemberPawns"/>
        /// （与接管 / 隐藏 / 作息表同一份成员视图）⇒ 不会出现"某条链认为他还在车间"的分叉。</para>
        ///
        /// <para><b>调用时机</b>（事件驱动，非 tick）：<c>WorkshopManageView.Refresh</c>（开面板）
        /// 与 <c>OnRosterChanged</c>（排班/名册变更），均在 <see cref="SyncAutoHideWorkTabColonists"/>
        /// <b>之后</b>调用（先收新人、再放旧人）。</para>
        /// </summary>
        /// <returns>本轮判定为"已移出"的人数（诊断用）。</returns>
        public int ReconcileAutoHideOnLeave()
        {
            Data.ChronicleGameComponent component = Current.Game != null
                ? Current.Game.GetComponent<Data.ChronicleGameComponent>()
                : null;
            if (component == null)
            {
                return 0;
            }
            // ① 收集本轮在册成员（所有车间的并集 —— 成员可能同时属于多个车间的名单）。
            reconcileScratch.Clear();
            List<Zone_Workshop> zones = AllWorkshopZones();
            for (int z = 0; z < zones.Count; z++)
            {
                Zone_Workshop zone = zones[z];
                if (zone == null)
                {
                    continue;
                }
                List<Pawn> members = AllWorkshopMemberPawns(zone);
                for (int i = 0; i < members.Count; i++)
                {
                    Pawn p = members[i];
                    if (p == null || p.Destroyed || reconcileScratch.Contains(p))
                    {
                        continue;
                    }
                    reconcileScratch.Add(p);
                }
            }
            // ② 差集 = 上一轮在册 − 本轮在册 ⇒ 这些人已移出车间体系。
            int left = 0;
            for (int i = 0; i < lastWorkshopMembers.Count; i++)
            {
                Pawn p = lastWorkshopMembers[i];
                if (p == null || reconcileScratch.Contains(p))
                {
                    continue;   // 仍在册（或引用已空）
                }
                if (p.Destroyed || p.Dead)
                {
                    // ⚠ NODEMOTE-AUDIT（P2 修复）：已消亡者**不能只跳过** ——
                    //   原先直接 continue，造成两处残留：① 自动隐藏键永留（旧注释误称
                    //   "由 Sanitize 清理"，而 Sanitize 并不校验 pawn 存在性）；
                    //   ② 接管记录只能等玩家手点「解除接管」才被全量版顺带清掉。
                    //   现在顺手两者都清（SetAutoHiddenFromWorkTab 幂等、DiscardTakeoverPawn 幂等）。
                    component.SetAutoHiddenFromWorkTab(p, false);
                    component.DiscardTakeoverPawn(p.thingIDNumber);
                    continue;
                }
                // 恢复显示（只动自动集合，玩家手动隐藏的不受影响）
                component.SetAutoHiddenFromWorkTab(p, false);
                // 按人解除接管（把当初取消勾选的车间类工作还原；玩家手动改过的项会被跳过）
                ReleaseVanillaWorkFor(p);
                left++;
            }
            // ③ 重建快照（下轮比较用）。
            lastWorkshopMembers.Clear();
            lastWorkshopMembers.AddRange(reconcileScratch);
            return left;
        }
    }
}