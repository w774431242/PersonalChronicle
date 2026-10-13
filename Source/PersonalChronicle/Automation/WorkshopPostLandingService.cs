using System.Collections.Generic;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// CHG-20261005-POSTLAND · 「岗位落地」：把岗位分配结果落到**原版工作优先级**上。
    ///
    /// <para><b>玩家口径（2026-10-05 确认）</b>：「将这些岗位进行落地，**除操作员工暂时不动**，
    /// 其他都对应原版工作类型进行落地」，并在确认时选择 ——
    /// ① **按岗位勾选优先**（不是接管清零）；② 存在多个原版工作类型时，
    /// **按车间类型自动分配单个**（原话：「是精准分工」）。</para>
    ///
    /// <para><b>做什么</b>（逐成员，每人至多一格）：</para>
    /// <list type="number">
    /// <item>取该成员的岗位（<b>玩家手动覆盖优先</b>，否则分配器结果）；</item>
    /// <item>候选 = 岗位承接的原版工作类型 <b>∩</b> 该**车间类型**声明的工作类型池；</item>
    /// <item>多个候选用 <see cref="AutoAssignPriority"/> 的同一公式**按技能挑出最优那一个**
    /// （精准分工：一人一岗一项工作）；</item>
    /// <item>调 <see cref="AutoAssignService.WriteCell"/> 写入（**勾选 + 按技能定档**）。</item>
    /// </list>
    ///
    /// <para><b>不做什么（关键安全边界）</b>：<b>不碰其它任何工作类型</b> —— 既不清零、也不接管。
    /// 理由：PC 目前只实现「值勤」（<c>PC_WorkshopDuty</c>）与「用餐」（<c>PC_MealGiver</c>）
    /// 两个 Job，**没有任何生产 Job**（建造 / 研究 / 种植 / 采矿 / 驯兽）。一旦接管
    /// （= 取消成员的原版该工作勾选），殖民地会立刻失去这些服务且**无人替代** ——
    /// 这正是 <c>WorkshopPostDef</c> 与 EXC-2026-016 反复警告的场景。
    /// 因此本服务**不走** <c>WorkshopTakeover</c> 那条清零路径（<c>SetPriority(x, 0)</c>
    /// 的唯一授权点仍只在接管通道）。</para>
    ///
    /// <para><b>硬红线</b>：搬运 / 清洁 / 医疗 / 灭火在 <see cref="AutoAssignPriority"/> 里属
    /// 保护名单（<c>ProtectedFloor = 2</c>）⇒ 即便被岗位承接，也只做"勾选 + 至少 2 档"，
    /// **永不取消勾选**。物料 / 仓储 / 医务 / 安保四个岗位因此是安全的
    /// （它们只让对应工作更靠前，不会夺走服务）。</para>
    ///
    /// <para><b>分层</b>：Automation 层（与 <see cref="AutoAssignService"/> 同级，在其授权的
    /// 写格范围内）。本类**不自己决定**"谁能当什么岗" —— 那是 <c>Domain.WorkshopPostAssigner</c>
    /// 的产物，本类只消费。</para>
    /// </summary>
    public static class WorkshopPostLandingService
    {
        /// <summary>落地结果（供 UI 反馈，避免"点了没反应"）。</summary>
        public class Result
        {
            /// <summary>成功写入的成员数（每人至多一格）。</summary>
            public int Landed;

            /// <summary>无岗位 / 岗位被豁免 / 岗位 Def 已失效 ⇒ 跳过的成员数。</summary>
            public int SkippedNoPost;

            /// <summary>挑不出可落地工作类型（岗位无承接项 / 全被车间类型池排除 / 类型不可见）⇒ 跳过。</summary>
            public int SkippedNoWork;

            /// <summary>失去车间还原权的格位数（M1 退让，见 <c>WriteCell</c>）。</summary>
            public int Relinquished;
        }

        /// <summary>
        /// 执行一次落地。作用域 = 传入的成员列表（调用方给"本车间在编成员"）。
        /// </summary>
        /// <param name="map">地图（取 GameComponent 台账用）。</param>
        /// <param name="zone">车间（提供车间类型池与玩家岗位覆盖）。</param>
        /// <param name="members">要落地的成员（通常 = 该车间在编成员）。</param>
        /// <param name="postOfPawn">分配器结果（人 → 岗位 defName）；由调用方传入它已算好的那份，
        /// 避免本服务重算一遍分配（同一批数据两处计算会分叉，GOV-005）。</param>
        public static Result Land(Map map, Zone_Workshop zone, List<Pawn> members,
            Dictionary<Pawn, string> postOfPawn)
        {
            Result res = new Result();
            if (map == null || zone == null || members == null || members.Count == 0)
            {
                return res;
            }
            ChronicleGameComponent comp = Current.Game != null
                ? Current.Game.GetComponent<ChronicleGameComponent>()
                : null;
            if (comp == null)
            {
                return res;   // 极早期 / 测试环境：无台账 ⇒ 不写（避免无记录的写入）
            }

            HashSet<string> shopPool = BuildShopPool(zone);

            for (int i = 0; i < members.Count; i++)
            {
                Pawn pawn = members[i];
                if (pawn == null || pawn.Destroyed || !pawn.IsFreeColonist
                    || pawn.workSettings == null || pawn.skills == null)
                {
                    continue;
                }

                WorkshopPostDef post = ResolvePostOf(zone, pawn, postOfPawn);
                if (post == null || post.landingExempt)
                {
                    res.SkippedNoPost++;
                    continue;
                }

                WorkTypeDef pick = PickWorkType(post, shopPool, pawn);
                if (pick == null)
                {
                    res.SkippedNoWork++;
                    continue;
                }
                // SetPriority 对被禁用的工作类型写非 0 档会 Log.Error 并 return（IL 实证）
                // ⇒ 先做同一道守卫（与 AutoAssignService / MapComponent_MealSchedule 同规）。
                if (pawn.WorkTypeIsDisabled(pick))
                {
                    res.SkippedNoWork++;
                    continue;
                }

                float avg = pawn.skills.AverageOfRelevantSkillsFor(pick);
                Passion passion = pawn.skills.MaxPassionOfRelevantSkillsFor(pick);
                int target;
                if (!AutoAssignPriority.TryCompute(pick, avg, passion, out target))
                {
                    res.SkippedNoWork++;
                    continue;
                }

                int current = pawn.workSettings.GetPriority(pick);
                if (current == target)
                {
                    continue;   // 幂等：值已正确 ⇒ 不写、不记账（重复点击无副作用）
                }

                if (AutoAssignService.WriteCell(comp, pawn, pick, current, target))
                {
                    res.Relinquished++;
                }
                pawn.workSettings.Notify_UseWorkPrioritiesChanged();
                res.Landed++;
            }
            return res;
        }

        /// <summary>
        /// 该车间类型"认得"的全部原版工作类型（四个任务类型组 + 设备通道的并集）。
        ///
        /// <para><b>为什么取并集，而不是按岗位的 <c>dutyTaskTypeKey</c> 取单组</b>：那个键是
        /// "该岗位的**值勤任务类型**"（Production / Maintenance / …），与"这个车间关心哪些工作类型"
        /// 不是同一维度 —— 例如农艺车间可能在 Production 组声明了 Growing，而农艺员工的
        /// dutyTaskTypeKey 未必是 Production ⇒ 按单组取会漏。取并集 =
        /// "本车间声明过的所有工作类型"，这才是"按车间类型筛选"的完整口径。</para>
        ///
        /// <para>车间类型未指定 ⇒ 返回空池，调用方**不筛**（改用岗位自己的承接列表），
        /// 保证"没设类型也能落地"（不会因为一个前置条件缺失就完全不生效）。</para>
        /// </summary>
        private static HashSet<string> BuildShopPool(Zone_Workshop zone)
        {
            var pool = new HashSet<string>();
            WorkshopTypeDef type = zone != null ? zone.WorkshopType : null;
            if (type == null)
            {
                return pool;
            }
            AddAll(pool, type.WorkTypeDefNamesOf(WorkshopTaskType.Production));
            AddAll(pool, type.WorkTypeDefNamesOf(WorkshopTaskType.Maintenance));
            AddAll(pool, type.WorkTypeDefNamesOf(WorkshopTaskType.QualityCheck));
            AddAll(pool, type.WorkTypeDefNamesOf(WorkshopTaskType.Standby));
            AddAll(pool, type.DeviceWorkTypeDefNames());
            return pool;
        }

        private static void AddAll(HashSet<string> set, List<string> names)
        {
            if (names == null)
            {
                return;
            }
            for (int i = 0; i < names.Count; i++)
            {
                if (!string.IsNullOrEmpty(names[i]))
                {
                    set.Add(names[i]);
                }
            }
        }

        /// <summary>
        /// 该成员的岗位：<b>玩家手动覆盖优先</b>（<c>zone.GetPostOverride</c>），
        /// 否则用分配器结果。<b>与岗位页显示同一口径</b>（否则会出现"页面写 A 岗、
        /// 落地按 B 岗执行"的自相矛盾）。
        /// </summary>
        private static WorkshopPostDef ResolvePostOf(Zone_Workshop zone, Pawn pawn,
            Dictionary<Pawn, string> postOfPawn)
        {
            string manual = zone.GetPostOverride(pawn);
            if (!manual.NullOrEmpty())
            {
                WorkshopPostDef def = WorkshopPostCatalog.PostOf(manual);
                if (def != null)
                {
                    return def;   // 覆盖项失效（第三方移除该 Def）⇒ 静默回落自动
                }
            }
            string auto = null;
            if (postOfPawn != null)
            {
                postOfPawn.TryGetValue(pawn, out auto);
            }
            if (string.IsNullOrEmpty(auto))
            {
                return null;
            }
            return WorkshopPostCatalog.PostOf(auto);
        }

        /// <summary>
        /// 从岗位承接列表里挑出**单个**工作类型（精准分工）：
        /// <list type="number">
        /// <item>先与车间类型池取交集（池为空 ⇒ 不筛，用岗位自己的列表）；</item>
        /// <item>多个候选 ⇒ 用 <see cref="AutoAssignPriority"/> 的同一公式算档位，取**档位最小者**
        /// （数字越小越优先 ⇒ 即"他最擅长/最该做的那一项"）；
        /// 档位并列时按 defName 序数排序，保证**结果可复现**（不依赖 Def 加载顺序）。</item>
        /// </list>
        /// </summary>
        private static WorkTypeDef PickWorkType(WorkshopPostDef post, HashSet<string> shopPool,
            Pawn pawn)
        {
            List<string> covered = post.CoveredWorkTypeNames();
            if (covered.Count == 0)
            {
                return null;
            }
            WorkTypeDef best = null;
            int bestPriority = int.MaxValue;
            for (int i = 0; i < covered.Count; i++)
            {
                string defName = covered[i];
                if (string.IsNullOrEmpty(defName))
                {
                    continue;
                }
                if (shopPool.Count > 0 && !shopPool.Contains(defName))
                {
                    continue;   // 车间类型没声明它 ⇒ 本车间不做这项工作
                }
                WorkTypeDef wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
                if (wt == null || !wt.visible)
                {
                    continue;
                }
                float avg = pawn.skills.AverageOfRelevantSkillsFor(wt);
                Passion passion = pawn.skills.MaxPassionOfRelevantSkillsFor(wt);
                int p;
                if (!AutoAssignPriority.TryCompute(wt, avg, passion, out p))
                {
                    continue;
                }
                bool better = p < bestPriority;
                if (!better && p == bestPriority && best != null)
                {
                    better = string.CompareOrdinal(defName, best.defName) < 0;
                }
                if (better)
                {
                    bestPriority = p;
                    best = wt;
                }
            }
            return best;
        }
    }
}
