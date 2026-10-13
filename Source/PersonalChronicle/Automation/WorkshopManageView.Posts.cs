using System.Collections.Generic;
using PersonalChronicle.Archive.UI;
using RimWorld;
using PersonalChronicle.Domain;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间管理 ·「岗位」页（<c>CHG-20261003-POSTS</c> · Phase 1）——<b>只读展示</b>切片。
    ///
    /// <para><b>Phase 1 边界（务必保持）</b>：本页只<b>读</b>并展示
    /// 「成员 × 岗位」的<b>建议分配结果</b>。<b>不写任何原版工作状态</b>：不改
    /// <c>Pawn_WorkSettings</c>（不 SetPriority / Disable）、不改 <c>timetable</c>。
    /// 玩家在页面上必须能一眼看出"本阶段不会动我的原版工作"（UI-009 可观测性要求）。</para>
    ///
    /// <para><b>绘制纪律（PERF-002 / UI-003）</b>：分配计算在 <see cref="Refresh"/> 期做一次，
    /// 绘制路径<b>只读缓存字符串</b>，零翻译、零分配器、零业务计算。
    /// 分层纪律（MATRIX-003）：UI 只读 <c>Domain</c> 纯函数的输出，不自己判断资格。</para>
    ///
    /// <para>⚠ <b>CHG-20261005-POSTLAND 的边界例外</b>：页面新增的「按岗位落地」**按钮**
    /// 会在**点击事件**里写原版工作档位（走 <see cref="WorkshopPostLandingService"/>）。
    /// 这**不**违反上面的"只读"承诺 —— 那条承诺约束的是 Refresh / 绘制路径；
    /// 按钮是玩家显式触发的动作，且写入范围严格限于"该岗位对应的那一个工作类型"，
    /// **不清零、不接管**（详见该服务的类注释）。</para>
    /// </summary>
    internal sealed partial class WorkshopManageView
    {
        /// <summary>岗位行高（与人员页行高同档，保持左侧列表密度一致）。</summary>
        private const float PostRowH = 26f;

        /// <summary>岗位块之间的纵向间距。</summary>
        private const float PostGap = 6f;

        /// <summary>岗位表（Refresh 期构建；绘制路径零计算）。</summary>
        private readonly List<PostRowCache> postRows = new List<PostRowCache>();

        /// <summary>岗位页顶部的边界声明（Keyed，静态文本）。</summary>
        private string postsNoticeKey = "PostsNoticePhase1";

        private struct PostRowCache
        {
            internal string Title;
            internal string Covered;
            /// <summary>承担者显示名（逗号分隔）。⚠ 原先只算 HasHolder 却从不显示是谁担任。</summary>
            internal string Holders;
            internal bool Delivers;
            internal bool HasHolder;
        }

        /// <summary>
        /// 构建岗位页缓存（<b>Refresh 期调用</b>，幂等）。
        ///
        /// <para>分配走 <see cref="WorkshopPostAssigner"/>（Domain 纯函数）：候选人 = 在编班组成员，
        /// 岗位 = <c>WorkshopPostCatalog</c> 的全部岗位行。<b>不做任何写操作</b>。</para>
        /// </summary>
        private void BuildPostRows()
        {
            postRows.Clear();
            // CHG-20261005-REDLINE：分配**收口**到 WorkshopPostAssignmentService ——
            // 接管闸门（WorkTakeover）也要用同一份结果判断"某工作类型是否有在岗岗主"，
            // 两处各算一遍会让页面与接管给出不同岗位（GOV-005 / UI-009 自证）。
            List<Pawn> roster = AllRosterMembers();
            Dictionary<Pawn, string> assignment = WorkshopPostAssignmentService.AssignOf(roster);

            // pawn → 岗位 defName（供人员 tab 与「按岗位落地」）
            pawnPostDefNames.Clear();
            // 反向：岗位 defName → 承担者名字（本页显示）
            var holderNames = new Dictionary<string, List<string>>();
            foreach (KeyValuePair<Pawn, string> kv in assignment)
            {
                if (kv.Key == null || string.IsNullOrEmpty(kv.Value))
                {
                    continue;
                }
                pawnPostDefNames[kv.Key] = kv.Value;
                List<string> names;
                if (!holderNames.TryGetValue(kv.Value, out names))
                {
                    names = new List<string>();
                    holderNames[kv.Value] = names;
                }
                names.Add(kv.Key.LabelShort);
            }

            List<WorkshopPostDef> posts = WorkshopPostCatalog.AllPosts();
            for (int i = 0; i < posts.Count; i++)
            {
                WorkshopPostDef def = posts[i];
                List<string> names;
                holderNames.TryGetValue(def.defName, out names);
                string holders = names != null ? string.Join(" / ", names.ToArray()) : string.Empty;
                var row = new PostRowCache
                {
                    Title = def.LabelCap,
                    Covered = BuildCoveredText(def),
                    Holders = holders,
                    Delivers = def.deliversOwnService,
                    HasHolder = !string.IsNullOrEmpty(holders),
                };
                postRows.Add(row);
            }
        }

        // ⚠ CHG-20261005-REDLINE：原 `ResolvePostDefs` / `FindPawnByStableIdSafe` /
        //   `ResolveHolderNames` / `BuildPostCandidates` / `BuildPostSpecs` 五个方法
        //   （以及 `postSkillDefs` / `postCapacityDefs` 两个缓存字段）**已删除** ——
        //   它们的职责（技能 / 能力 Def 解析、候选构造、岗位规格、id 回查）已整体收口到
        //   `WorkshopPostAssignmentService`，供本页与接管闸门共用（GOV-005 单一实现）。

        /// <summary>
        /// 某 pawn 的岗位（<b>供人员 tab 显示</b>；未分配返回空串）。
        /// <para>2026-10-03 用户定责：<b>岗位自动分配的结果属于"人"</b>，应在人员 tab 逐人显示；
        /// 岗位 tab 只作为<b>岗位标准表</b>（岗位 / 承接工作 / 能否独立交付），不列承担者。</para>
        /// </summary>
        internal string PostLabelOfPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return string.Empty;
            }
            pawnPostDefNames.TryGetValue(pawn, out string assignedPost);
            WorkshopPostDef def = WorkshopPostCatalog.PostOf(assignedPost);
            // CHG-20261004-POSTOVERRIDE：玩家手动覆盖优先于自动分配结果。
            // 口径（2026-10-04 玩家决策）= **先自动分配，再允许玩家手动覆盖**。
            // 覆盖项失效（岗位 Def 被第三方移除）时静默回落自动，
            // 不让存档里的死 defName 把该成员卡成「未分配」。
            if (zone != null)
            {
                string manualDefName = zone.GetPostOverride(pawn);
                if (!manualDefName.NullOrEmpty())
                {
                    WorkshopPostDef manual = WorkshopPostCatalog.PostOf(manualDefName);
                    if (manual != null)
                    {
                        return manual.LabelCap;
                    }
                }
            }
            if (def == null)
            {
                return string.Empty;
            }
            return def.LabelCap;
        }

        /// <summary>CHG-20261004-POSTOVERRIDE：该成员的岗位是否为玩家手动指定（用于 UI 标记）。</summary>
        internal bool IsPostOverridden(Pawn pawn)
        {
            return zone != null && !zone.GetPostOverride(pawn).NullOrEmpty();
        }

        /// <summary>
        /// CHG-20261004-POSTOVERRIDE：岗位选择菜单（人员页岗位列点击）。
        ///
        /// <para>菜单首项恒为「跟随自动」= 清除覆盖、让该成员回到分配器结果。
        /// 这样玩家可以对同一批人做「整体自动 + 个别手动」，且随时能整批还原。</para>
        ///
        /// <para><b>注意</b>：本操作只改「岗位显示与统计」，<b>不</b>改工作类型 ——
        /// 工作类型由班次 <c>shift.TaskType</c> 决定（玩家口径：只分配岗位，工作类型自动）。</para>
        /// </summary>
        private void OpenPostOverrideMenu(Pawn pawn)
        {
            if (zone == null || pawn == null) { return; }
            var options = new List<FloatMenuOption>();
            List<WorkshopPostDef> posts = WorkshopPostCatalog.AllPosts();
            for (int i = 0; i < posts.Count; i++)
            {
                WorkshopPostDef def = posts[i];
                if (def == null) { continue; }
                string defName = def.defName;
                options.Add(new FloatMenuOption(def.LabelCap, () =>
                {
                    zone.SetPostOverride(pawn, defName);
                    OnRosterChanged();
                }));
            }
            options.Add(new FloatMenuOption(T("PostFollowAuto"), () =>
            {
                zone.SetPostOverride(pawn, null);
                OnRosterChanged();
            }));
            Find.WindowStack.Add(new FloatMenu(options, T("SetPost")));
        }

        /// <summary>pawn → 岗位 defName（Refresh 期构建）。</summary>
        private readonly Dictionary<Pawn, string> pawnPostDefNames = new Dictionary<Pawn, string>();

        // ⚠ CHG-20261005-REDLINE：原 `ResolveHolderNames` / `BuildPostCandidates` /
        //   `BuildPostSpecs` 三个方法**已删除**（连同 `postSkillDefs` / `postCapacityDefs`
        //   两个缓存字段）—— 其职责已收口到 `WorkshopPostAssignmentService.AssignOf`，
        //   由 <see cref="BuildPostRows"/> 直接调用，并与接管闸门共用同一份分配结果。

        /// <summary>岗位承接的原版工作类型（Keyed 逗号串；空 = 纯内部岗）。</summary>
        private string BuildCoveredText(WorkshopPostDef def)
        {
            List<string> names = def.CoveredWorkTypeNames();
            if (names.Count == 0)
            {
                return T("PostCoversNone");
            }
            var parts = new List<string>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string defName = names[i];
                WorkTypeDef wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
                parts.Add(wt != null ? wt.LabelCap : defName + T("PostMissingSuffix"));
            }
            return string.Join(" / ", parts.ToArray());
        }

        /// <summary>上次「岗位落地」的结果文本（点击后写入；<b>不在 Refresh 里清空</b> ——
        /// 否则提示会被紧随其后的刷新擦掉，玩家看不到反馈）。</summary>
        private string postLandResult = string.Empty;

        /// <summary>
        /// 执行一次「岗位落地」（<b>点击事件</b>，非绘制路径）。
        ///
        /// <para>口径与安全边界全部在 <see cref="WorkshopPostLandingService"/> 的类注释里；
        /// 本方法只负责取"本车间在编成员"、调用、并把结果转成一行反馈。</para>
        ///
        /// <para><b>为什么传 <see cref="pawnPostDefNames"/></b>：那是 Refresh 期算好的
        /// 「人 → 岗位」分配结果 ⇒ 落地与页面显示**必然一致**；若在这里重算一遍分配，
        /// 两处就可能给出不同岗位（同一批数据两处计算会分叉）。</para>
        /// </summary>
        private void DoLandPosts()
        {
            if (zone == null)
            {
                return;
            }
            WorkshopPostLandingService.Result r = WorkshopPostLandingService.Land(
                zone.Map, zone, AllRosterMembers(), pawnPostDefNames);
            postLandResult = string.Format(T("PostLandDone"),
                r.Landed, r.SkippedNoPost, r.SkippedNoWork);
            // 工作页 / 人员页按新档位刷新（档位变了，那边显示的工作高亮也应立刻跟上）。
            OnRosterChanged();
        }

        /// <summary>岗位页绘制（返回占用高度）。<b>只读缓存</b>，零业务计算。</summary>
        private float DrawPostsPage(Rect rect)
        {
            float y = rect.y;
            UIComponents.Label(new Rect(rect.x, y, rect.width, 18f),
                T("PostsTitle"), UITheme.FontLabel, UITheme.Muted);
            y += 20f;
            UIComponents.Label(new Rect(rect.x, y, rect.width, 18f),
                T(postsNoticeKey), UITheme.FontLabel, UITheme.Dim);
            y += 22f;

            // CHG-20261005-POSTLAND（2026-10-05 玩家要求「将这些岗位进行落地，除操作员工暂时不动」）：
            // 「按岗位落地」= 把岗位对应的工作写进成员的原版工作优先级（勾选 + 提档），
            // **不动其它工作、不清零、不接管**（后者会因 PC 无生产 Job 而让殖民地失去服务）。
            // 悬停说明讲清与右侧「未接管」标记的区别，避免"落地了怎么还写未接管"的误读。
            float landW = 172f;
            Rect landRect = new Rect(rect.x, y, landW, PostRowH);
            if (UIComponents.ThemedButton(landRect, T("PostLandButton")))
            {
                DoLandPosts();
            }
            TooltipHandler.TipRegion(landRect, new TipSignal(T("PostLandTip"), "pcPostLand".GetHashCode()));
            if (!string.IsNullOrEmpty(postLandResult))
            {
                UIComponents.Label(new Rect(landRect.xMax + 8f, y, rect.width - landW - 8f, PostRowH),
                    UIComponents.TruncateToWidth(postLandResult, rect.width - landW - 8f, UITheme.FontLabel),
                    UITheme.FontLabel, UITheme.Accent, TextAnchor.MiddleLeft);
            }
            y += PostRowH + PostGap;
            // CHG-20261004-POSTGUIDE（2026-10-04 玩家反复反馈「岗位尚未实现 ⇒ 员工无法工作」）：
            // 这一页的「尚未实现」被误读为"功能坏了"，而它其实**只**表示 PC 尚未提供岗位专属 Job
            // —— v1 让员工真正开工走的是**原版工作面板联动**（车间类型 → workTypeDefNames →
            // Enable 并提到 1 档），与本节内容无关。两个前置条件任一缺失，成员都只会站岗：
            //   ① 车间未指定类型 ⇒ CollectWorkSyncTypes 直接 return，除值勤外不勾任何工作；
            //   ② 原版「手动优先级」未开启 ⇒ 档位写了也不被读取（WorkEnable 的既有披露）。
            // 放在这里是因为玩家遇到问题**就是**打开这一页看（成本最低、命中率最高）。
            if (zone != null && zone.WorkshopType == null)
            {
                UIComponents.Label(new Rect(rect.x, y, rect.width, 18f),
                    T("PostsNeedWorkshopType"), UITheme.FontLabel, UITheme.Accent, TextAnchor.MiddleLeft);
                y += 20f;
                // 拆两行（2026-10-04 实机修复）：单行 Label 不换行，此前把解释与警告挤在一句里
                // 会被右侧裁断，玩家只看到"……右侧 |"这种半句（截图实证）。
                UIComponents.Label(new Rect(rect.x, y, rect.width, 18f),
                    T("PostsNeedWorkshopType2"), UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
                y += 22f;
            }

            for (int i = 0; i < postRows.Count; i++)
            {
                if (y + PostRowH > rect.yMax)
                {
                    break;   // 大列表降级：超出即停（UI-004）
                }
                PostRowCache row = postRows[i];
                Rect line = new Rect(rect.x, y, rect.width, PostRowH);
                Widgets.DrawBoxSolid(line, UITheme.WithAlpha(UITheme.Text, 0.03f));
                UIComponents.Label(new Rect(line.x + 6f, line.y, line.width * 0.28f, line.height),
                    row.Title, UITheme.FontLabel,
                    row.HasHolder ? UITheme.Accent : UITheme.Muted, TextAnchor.MiddleLeft);
                UIComponents.Label(new Rect(line.x + line.width * 0.28f, line.y,
                        line.width * 0.62f, line.height),
                    UIComponents.TruncateToWidth(row.Covered, line.width * 0.62f, UITheme.FontLabel),
                    UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
                // ⚠ 2026-10-03 用户定责：岗位 tab 回归**纯岗位标准表**（岗位 / 承接工作 / 能否独立交付），
                //   承担者列已移除 —— 「谁担任什么岗」属于**人**，改在人员 tab 逐人显示。
                string tag = row.Delivers ? T("PostDelivers") : T("PostNotDelivers");
                var tagRect = new Rect(line.xMax - 110f, line.y, 104f, line.height);
                UIComponents.Label(tagRect, tag, UITheme.FontLabel,
                    row.Delivers ? UITheme.Alive : UITheme.Warn, TextAnchor.MiddleRight);
                // 2026-10-05 玩家反馈（原话：「我先点击接管原版工作，提示无可接管的工作，
                // 但它显示规划中-未接管」）：两处提示其实**同根因** —— 都是"车间尚未声明
                // 可独立交付的工作类型"，但一处说"无可接管"、一处说"未接管"，看着像互相矛盾。
                // 补悬停说明把因果讲清（并与设置页的 Takeover_no_takeoverable_work 对齐措辞）。
                string tagTip = T(row.Delivers ? "PostDeliversTip" : "PostNotDeliversTip");
                TooltipHandler.TipRegion(tagRect, new TipSignal(tagTip, row.Title.GetHashCode() + 31));
                y += PostRowH + PostGap;
            }
            return y - rect.y;
        }
    }
}
