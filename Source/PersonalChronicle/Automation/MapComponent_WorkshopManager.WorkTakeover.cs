using System;
using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 「接管原版工作」执行层（CHG-20261003-TAKEOVER · Phase 2 / EXC-2026-016）。
    ///
    /// <para><b>EXC-2026-016 授权面</b>：本类是全工程<b>唯一</b>允许调用
    /// <c>SetPriority(type, 0)</c>（取消勾选原版工作）的地方，授权严格限定为
    /// 「玩家在面板<b>主动点击</b>后，对该车间在编成员、按下列三道闸门筛出的工作类型」。</para>
    ///
    /// <para><b>三道闸门</b>：① fail-closed（空 defName 拒）；② <b>硬红线</b>
    /// 搬运/清洁/医疗/灭火<b>永不接管</b>（否则无人送料⇒产线停摆 / filth 堆积 / 伤病无人治 / 火灾失控）；
    /// ③ <b>能力对等</b>：只有 deliversOwnService = true 的岗位所承接的工作才允许。</para>
    ///
    /// <para><b>强制兜底（闸门 ③ 之后）</b>：接管后本图仍须留有
    /// <see cref="MinFreeWorkers"/> 名自由工人，否则<b>整次拒绝</b>（不做部分接管）——
    /// 全员进车间 + 原版全禁 ⇒ 殖民地连"取消勾选"这个自救动作都做不出来。</para>
    ///
    /// <para><b>默认关闭</b>：本类不做任何自动调用；只有面板按钮点击才触发（MATRIX-003）。</para>
    /// </summary>
    public partial class MapComponent_WorkshopManager
    {
        /// <summary>接管后必须保留的自由工人下限（EXC-2026-016 硬约束）。</summary>
        private const int MinFreeWorkers = 1;

        /// <summary>最近一次接管/解除的项数（UI 读数；不持久化）。</summary>
        public int LastTakeoverCount;

        /// <summary>
        /// 最近一次接管时是否存在"全员进车间、无自由工人"的情况（**仅提示，不阻塞**）。
        /// <para>2026-10-03：由"拒绝接管"降级为提示 —— 硬红线已使死锁不可能发生，
        /// 继续用它拦截会造成 100% 误拒（实机：3 人全进车间 ⇒ 接管从未生效）。</para>
        /// </summary>
        public bool LastTakeoverShortfall;

        /// <summary>
        /// 该工作类型是否已被某个员工<b>真的实现</b>（能力对等闸门的唯一输入）。
        ///
        /// <para><b>2026-10-04 语义修正（CHG-20261004-STAFF）</b>：旧实现只问
        /// 「有没有 deliversOwnService=true 的岗位<b>声明</b>承接它」，于是
        /// <b>能力声明被误当成已实现</b> ⇒ 接管清零了玩家的原版工作，而车间对应 Job 并不存在
        /// ⇒ 工人停产（实测隐患：操作员的"全集上界"覆盖 Research ⇒ 科技停滞）。</para>
        ///
        /// <para>现在只认 <see cref="WorkshopPostDef.implementedWorkTypeDefNames"/>（"真的做出来了"）。
        /// 车间当前只实现值勤（站岗），该字段全空 ⇒ 本闸门恒 false ⇒ 接管自动空转，
        /// 逐个实现生产 Job 时再逐个点亮。</para>
        /// </summary>
        private static bool DeliveredByWorkshopPost(string workTypeDefName)
        {
            if (string.IsNullOrEmpty(workTypeDefName))
            {
                return false;
            }
            List<WorkshopPostDef> posts = WorkshopPostCatalog.PostsCovering(workTypeDefName);
            for (int i = 0; i < posts.Count; i++)
            {
                WorkshopPostDef post = posts[i];
                if (post == null)
                {
                    continue;
                }
                // ⚠ 刻意**不**回退到 deliversOwnService：那是"能力声明"，不是"已实现"。
                // 回退会让 4 个 true 岗位继续被判定可交付 ⇒ ① 的"全集上界"再次误伤 Research。
                List<string> implemented = post.ImplementedWorkTypeNames();
                for (int k = 0; k < implemented.Count; k++)
                {
                    if (implemented[k] == workTypeDefName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>本图可工作（未倒地 / 未崩溃）的玩家殖民者数。</summary>
        private int CountEligibleColonists()
        {
            int n = 0;
            List<Pawn> all = PawnsFinder.AllMapsWorldAndTemporary_Alive;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p != null && p.Faction == Faction.OfPlayer && !p.Dead && !p.Destroyed
                    && !p.Downed && !p.InMentalState)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>在编班组成员数（去重；跨班组只算一次）。</summary>
        private int CountCrewMembers()
        {
            var seen = new HashSet<Pawn>();
            List<Zone_Workshop> zones = AllWorkshopZones();
            for (int z = 0; z < zones.Count; z++)
            {
                List<WorkshopCrew> crews = zones[z] != null ? zones[z].Crews : null;
                if (crews == null)
                {
                    continue;
                }
                for (int c = 0; c < crews.Count; c++)
                {
                    List<Pawn> members = crews[c] != null ? crews[c].Members : null;
                    if (members == null)
                    {
                        continue;
                    }
                    for (int m = 0; m < members.Count; m++)
                    {
                        if (members[m] != null && !members[m].Destroyed)
                        {
                            seen.Add(members[m]);
                        }
                    }
                }
            }
            return seen.Count;
        }
        /// <summary>
        /// 随排班/名册变更<b>自动</b>接管（"加入车间即隐藏原版工作"，CHG-20261003-TAKEOVER-R2）。
        ///
        /// <para><b>幂等</b>：已是 0 的工作类型会被跳过；台账<b>首记优先</b>，
        /// 重复触发不会把原值越写越低。拒绝原因（硬红线 / 自由人兜底不足）在此<b>静默</b>返回
        /// —— 自动触发不应刷提示，玩家要原因可手动点「接管」按钮看。</para>
        /// </summary>
        public int SyncVanillaWorkTakeover(Zone_Workshop zone)
        {
            string reason;
            return TakeOverVanillaWork(zone, out reason);
        }

        /// <summary>
        /// 接管本车间在编成员的<b>车间类</b>原版工作（取消勾选）。
        /// </summary>
        /// <param name="zone">目标车间。</param>
        /// <param name="outReason">拒绝原因键（成功时空串），供 UI 如实告知玩家。</param>
        /// <returns>实际取消勾选的项数（0 = 未接管或被闸门全部拦下）。</returns>
        public int TakeOverVanillaWork(Zone_Workshop zone, out string outReason)
        {
            outReason = string.Empty;
            LastTakeoverCount = 0;
            LastTakeoverShortfall = false;
            if (zone == null)
            {
                outReason = "zone_missing";
                return 0;
            }
            // 自由人兜底（2026-10-03 修正 · 实机 100% 误拒）：
            //   原实现在此**整次拒绝**。当时的理由是"硬红线（MandatoryKeep）已保证任何殖民者
            //   永远还剩 4 项工作 ⇒ 死锁不可能发生"，而"3 人全部进车间"恰是该功能的目标用法
            //   ⇒ 小殖民地 100% 误拒。故降级为**提示**：不阻塞，只如实报给 UI。
            //
            // ⚠ CHG-20261005-REDLINE（2026-10-05 玩家口径「硬红线移除。有专属岗位。」）：
            //   上面那句"还剩 4 项"的保证**来源变了** —— 红线不再是"永不接管"，而是
            //   "**需有在岗的专属岗位持有者**才接管"，且清零时**豁免岗位持有者本人**。
            //   新保证更强也更精确：**每一个被接管的敏感工作类型，都必然有一个在岗岗主在做它**
            //   ⇒ 服务不会消失、死锁仍不可能发生（见下方写入循环的豁免判定）。
            //   因此本兜底继续只作提示（不阻塞），结论未变、依据已更新。
            bool freeWorkerShortfall = !WorkshopTakeover.PassesFreeWorkerFloor(
                CountEligibleColonists(), CountCrewMembers(), MinFreeWorkers);
            if (freeWorkerShortfall)
            {
                LastTakeoverShortfall = true;   // UI 显示用；不阻塞
            }
            // ⚠ 2026-10-04（玩家口径「加入车间即隐藏、只保留车间类型」）：作用域 =
            // 「车间人员名单 ∪ 班组编制」—— 旧实现只收 crews 成员 ⇒ 玩家仅把殖民者放进
            // "车间人员名单"而未编入任何班组时，接管与隐藏**都不会发生**（"加了车间却没反应"）。
            //
            // 2026-10-04 深度审计：此处原是 AllWorkshopMemberPawns 的**第二份逐字复制**
            // （AutoHide.cs 的注释自称"供接管与自动隐藏共用，保证口径一致"，实际并未共用），
            // 现改为直接调用，口径由单一实现保证（GOV-005）。
            // 行为等价：同序并集（先名单后班组）、同去重、同 Destroyed 过滤。
            List<Pawn> scope = AllWorkshopMemberPawns(zone);
            if (scope.Count == 0)
            {
                outReason = "no_crew_member";
                return 0;
            }
            Data.ChronicleGameComponent ledger = Current.Game != null
                ? Current.Game.GetComponent<Data.ChronicleGameComponent>()
                : null;

            // 候选 = 车间类型映射之外、且过三道闸门的工作类型（车间自己要用的不接管）。
            CollectKeptWorkTypes(zone);
            // CHG-20261005-REDLINE：敏感项（搬运 / 清洁 / 医疗 / 灭火）的放行条件是
            // **该工作类型在本车间有在岗的专属岗位持有者** ⇒ 需要"人 → 岗位"分配结果。
            // 用 WorkshopPostAssignmentService（与岗位页**同一份实现**）——
            // 两处各算一遍会出现"页面显示他是物料员工、接管却按别的岗位判定"的自相矛盾。
            Dictionary<Pawn, string> postAssigned = WorkshopPostAssignmentService.AssignOf(scope);
            var candidates = new List<WorkTypeDef>();
            List<WorkTypeDef> allTypes = DemotableWorkTypes();
            for (int i = 0; i < allTypes.Count; i++)
            {
                WorkTypeDef type = allTypes[i];
                if (type == null || workSyncTypes.Contains(type))
                {
                    continue;
                }
                if (WorkshopTakeover.IsTakeoverAllowed(type.defName,
                    DeliveredByWorkshopPost(type.defName),
                    WorkshopPostAssignmentService.HasStaffedPostCovering(
                        type.defName, scope, zone, postAssigned)))
                {
                    candidates.Add(type);
                }
            }
            if (candidates.Count == 0)
            {
                outReason = "no_takeoverable_work";
                return 0;
            }

            int written = 0;
            for (int p = 0; p < scope.Count; p++)
            {
                Pawn pawn = scope[p];
                Pawn_WorkSettings settings = pawn.workSettings;
                if (settings == null)
                {
                    continue;
                }
                settings.EnableAndInitializeIfNotAlreadyInitialized();
                int pawnId = pawn.thingIDNumber;
                for (int i = 0; i < candidates.Count; i++)
                {
                    WorkTypeDef type = candidates[i];
                    if (pawn.WorkTypeIsDisabled(type))
                    {
                        continue;   // 背景/技能禁用：原版对这类类型写档位会 Log.Error
                    }
                    // ⚠ CHG-20261005-REDLINE **第二层保护（必须有）**：豁免**岗位持有者本人**。
                    //   接管是"按人清零"；若把负责搬运的物料员工也一起清零，就退化成
                    //   "所有人都不能搬运" ⇒ 断料停摆 —— 敏感项闸门放行之后，靠这一条兜住。
                    //   两层合起来才是闭环：**有人担任岗位**才允许接管，且**那位岗主不被清零**。
                    if (WorkshopPostAssignmentService.IsHolderOfWorkType(
                            pawn, type.defName, zone, postAssigned))
                    {
                        continue;
                    }
                    int current = settings.GetPriority(type);
                    if (current <= 0)
                    {
                        continue;   // 玩家本来就没勾选 => 不认领
                    }
                    ledger?.RecordTakeover(pawnId, type.defName, current);
                    // ↓ EXC-2026-016 授权的**唯一** SetPriority(type, 0) 调用点
                    settings.SetPriority(type, 0);
                    written++;
                }
            }
            LastTakeoverCount = written;
            if (written == 0)
            {
                outReason = "nothing_to_take_over";
            }
            else if (freeWorkerShortfall)
            {
                // 不阻塞，但如实告知：全员进车间且原版工作已被接管过半。
                // （硬红线保证搬运/清洁/医疗/灭火仍在，所以不会真的没人可用。）
                outReason = "no_free_worker_warn";
            }
            return written;
        }
        /// <summary>
        /// 解除接管：把**台账里**的项还原（只还原 PC 动过的，绝不碰玩家自己改的档位）。
        /// </summary>
        /// <returns>实际还原项数。</returns>
        public int ReleaseVanillaWork()
        {
            LastTakeoverCount = 0;
            Data.ChronicleGameComponent ledger = Current.Game != null
                ? Current.Game.GetComponent<Data.ChronicleGameComponent>()
                : null;
            if (ledger == null || ledger.WorkTakeoverLedger == null
                || ledger.WorkTakeoverLedger.Count == 0)
            {
                return 0;
            }
            var snapshot = new List<Data.ChronicleGameComponent.TakeoverPawnRecord>(
                ledger.WorkTakeoverLedger);
            int restored = 0;
            for (int i = 0; i < snapshot.Count; i++)
            {
                Data.ChronicleGameComponent.TakeoverPawnRecord rec = snapshot[i];
                if (rec == null || rec.entries == null)
                {
                    continue;
                }
                Pawn pawn = FindPawnByStableId(rec.PawnId);
                if (pawn == null || pawn.workSettings == null)
                {
                    ledger.DiscardTakeoverPawn(rec.PawnId);
                    continue;
                }
                Pawn_WorkSettings settings = pawn.workSettings;
                var entries =
                    new List<Data.ChronicleGameComponent.TakeoverEntry>(rec.entries);
                for (int n = 0; n < entries.Count; n++)
                {
                    Data.ChronicleGameComponent.TakeoverEntry e = entries[n];
                    if (e == null || string.IsNullOrEmpty(e.WorkTypeDefName))
                    {
                        continue;
                    }
                    WorkTypeDef type = ResolveWorkType(e.WorkTypeDefName);
                    if (type == null || pawn.WorkTypeIsDisabled(type))
                    {
                        ledger.DiscardTakeover(rec.PawnId, e.WorkTypeDefName);
                        continue;
                    }
                    if (!WorkshopTakeover.ShouldRestore(settings.GetPriority(type),
                        e.OriginalPriority))
                    {
                        ledger.DiscardTakeover(rec.PawnId, e.WorkTypeDefName);
                        continue;
                    }
                    settings.SetPriority(type, e.OriginalPriority);
                    restored++;
                    ledger.DiscardTakeover(rec.PawnId, e.WorkTypeDefName);
                }
            }
            LastTakeoverCount = restored;
            return restored;
        }

        /// <summary>
        /// **按单个成员**解除接管（CHG-20261005-NODEMOTE；2026-10-05 玩家批准 A 方案的自动化出口）：
        /// 由「人员被移出车间体系」的对账路径调用（见
        /// <c>MapComponent_WorkshopManager.ReconcileAutoHideOnLeave</c>）。
        ///
        /// <para><b>闸门与全量版 <see cref="ReleaseVanillaWork"/> 逐条一致</b>（GOV-005 单一实现，
        /// 不另立一套判定）：<c>workSettings</c> 不可用 ⇒ 丢弃该人全部记录；
        /// 类型已禁用 / 解析不到 ⇒ 只丢弃不还原；当前档位已被玩家改过
        /// （<c>WorkshopTakeover.ShouldRestore</c> 判否）⇒ 只丢弃不还原；只有能还原时才写回原档位。
        /// 差别仅在于"只处理这一个 pawn"。</para>
        /// </summary>
        /// <returns>实际还原项数。</returns>
        public int ReleaseVanillaWorkFor(Pawn pawn)
        {
            // ⚠ NODEMOTE-AUDIT（P2 修复）：pawn 级校验必须与全量版**同款** ——
            //   全量版经 FindPawnByStableId 过滤（Destroyed / Dead / 非玩家阵营），
            //   按人版原先直接信任调用方传入的引用，与上方注释承诺的"逐条一致"不符。
            //   对账路径只保证 not-Destroyed，其余两项由这里补齐（防御式，成本可忽略）。
            if (pawn == null || pawn.Destroyed || pawn.Dead
                || (pawn.Faction != null && !pawn.Faction.IsPlayer))
            {
                return 0;
            }
            Data.ChronicleGameComponent ledger = Current.Game != null
                ? Current.Game.GetComponent<Data.ChronicleGameComponent>()
                : null;
            if (ledger == null)
            {
                return 0;
            }
            int pawnId = pawn.thingIDNumber;
            List<Data.ChronicleGameComponent.TakeoverPawnRecord> all = ledger.WorkTakeoverLedger;
            Data.ChronicleGameComponent.TakeoverPawnRecord rec = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].PawnId == pawnId)
                {
                    rec = all[i];
                    break;
                }
            }
            if (rec == null || rec.entries == null)
            {
                return 0;
            }
            Pawn_WorkSettings settings = pawn.workSettings;
            if (settings == null)
            {
                ledger.DiscardTakeoverPawn(pawnId);   // 没有可还原的对象 ⇒ 记录失效
                return 0;
            }
            int restored = 0;
            int pawnIdKey = pawnId;
            int pawnIdDiscard = pawnId;
            var entries = new List<Data.ChronicleGameComponent.TakeoverEntry>(rec.entries);
            for (int n = 0; n < entries.Count; n++)
            {
                Data.ChronicleGameComponent.TakeoverEntry e = entries[n];
                if (e == null || string.IsNullOrEmpty(e.WorkTypeDefName))
                {
                    continue;
                }
                WorkTypeDef type = ResolveWorkType(e.WorkTypeDefName);
                if (type == null || pawn.WorkTypeIsDisabled(type))
                {
                    ledger.DiscardTakeover(pawnIdDiscard, e.WorkTypeDefName);
                    continue;
                }
                if (!WorkshopTakeover.ShouldRestore(settings.GetPriority(type), e.OriginalPriority))
                {
                    // 玩家在接管之后自己改过这一项 ⇒ 他的新意图优先，只丢弃记录不还原。
                    ledger.DiscardTakeover(pawnIdDiscard, e.WorkTypeDefName);
                    continue;
                }
                settings.SetPriority(type, e.OriginalPriority);
                restored++;
                ledger.DiscardTakeover(pawnIdKey, e.WorkTypeDefName);
            }
            return restored;
        }
    }
}