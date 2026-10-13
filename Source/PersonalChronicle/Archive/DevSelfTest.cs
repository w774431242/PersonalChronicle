// [DEV] 独立自检单元（2026-08-30 建立，PM 要求「进游戏后弹窗报告」）。
//
// 与既有自检的关系（两者独立、互不依赖，勿合并）：
//   · DevCoverageProbe      —— 数据覆盖率探针（ITab footer 手动触发，统计 15 个维度填充率）
//   · 本单元 DevSelfTest     —— 完整性 + 逻辑冒烟（进游戏自动弹窗，判 PASS/FAIL）
//
// 设计铁律：
//   1. 只读：不修改任何存档/累加器，不触发 MarkChanged（除 C 组亦只读）。
//   2. 逻辑冒烟用内存构造对象，绝不污染真实 PawnObject。
//   3. 每条断言必须写清「测什么 + 失败意味着什么」——后续开发看报告即可定位，
//      不必回读实现（呼应 PM 对注释可读性的要求）。
//   4. 触发受三重闸门：Prefs.DevMode && 设置开关 && 每会话仅一次。
using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using Verse;

namespace PersonalChronicle.Archive
{
    public static class DevSelfTest
    {
        // ── 结果模型 ──────────────────────────────────────────────

        /// <summary>单条断言结果。</summary>
        public sealed class CheckResult
        {
            /// <summary>检查项名（报告左列）。</summary>
            public string Name;

            /// <summary>是否通过。</summary>
            public bool Passed;

            /// <summary>详情/实测值（失败时给出定位线索）。</summary>
            public string Detail;

            /// <summary>是否关键项（关键项失败 = 整份报告 Failed，UI 标红）。</summary>
            public bool Critical = true;
        }

        /// <summary>整份自检报告。</summary>
        public sealed class SelfTestReport
        {
            public List<CheckResult> Checks = new List<CheckResult>();
            public int PassedCount;
            public int FailedCount;
            public bool AllPassed => FailedCount == 0;
        }

        // ── 触发闸门 ──────────────────────────────────────────────

        /// <summary>每进程只弹一次（静态标记；重启游戏后再次生效）。</summary>
        private static bool shownThisSession;

        /// <summary>
        /// 进游戏（存档加载完成）后调用：三重闸门通过才跑自检并弹窗。
        /// 任一步失败都静默返回，绝不打断正常游玩。
        /// </summary>
        public static void TryRunOnStartup()
        {
            try
            {
                // 闸门 1：仅开发者模式（正常玩家永不看到）
                if (!Prefs.DevMode) return;
                // 闸门 2：设置开关（默认 true，玩家/PM 可在设置里关）
                if (PersonalChronicleMod.Settings != null && !PersonalChronicleMod.Settings.EnableStartupSelfTest) return;
                // 闸门 3：每会话一次（避免反复读档反复弹）
                if (shownThisSession) return;
                if (Find.WindowStack == null) return;

                shownThisSession = true;
                SelfTestReport report = Run();
                DumpReportToLog(report);
                Find.WindowStack.Add(new Dialog_DevSelfTestReport(report));
            }
            catch (Exception ex)
            {
                // 自检本身出问题绝不能影响游戏
                ChronicleLog.Warning(ChronicleLog.Category.Ui, "startup self-test failed: " + ex.Message);
            }
        }

        // ── 主入口 ────────────────────────────────────────────────

        /// <summary>跑全部检查项，返回报告。只读。</summary>
        public static SelfTestReport Run()
        {
            SelfTestReport report = new SelfTestReport();
            RunDefIntegrity(report);
            RunLogicSmoke(report);
            RunSaveConsistency(report);
            RunCoverageSnapshot(report);

            for (int i = 0; i < report.Checks.Count; i++)
            {
                CheckResult c = report.Checks[i];
                if (c.Passed) report.PassedCount++;
                else report.FailedCount++;
            }
            return report;
        }

        // ================= A 组：Def 完整性（静态、只读）=================
        // 目的：抓出「Def 里写了某个 defName，但那个 Def 根本不存在」的悬空引用。
        // 这类问题在游戏里往往静默失效（加成不生效、方向显示为空），极难排查，
        // 故在启动自检里一次性暴露。

        private static void RunDefIntegrity(SelfTestReport report)
        {
            List<ProfessionalSkillDef> skillDefs = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
            List<ProfessionalDirectionDef> dirDefs = DefDatabase<ProfessionalDirectionDef>.AllDefsListForReading;

            // A1：专业技能 Def 数量（去年只有 1 个，现已补齐 12 个；为 0 说明 Def 未加载）
            Add(report, "A1 专业技能 Def 已加载", skillDefs != null && skillDefs.Count > 0,
                "count=" + (skillDefs != null ? skillDefs.Count.ToString() : "null"),
                "为 0 表示 ProfessionalSkills.xml 未被引擎加载（Def 语法错误或文件未被扫描）");

            if (skillDefs == null || skillDefs.Count == 0) return;

            List<string> badDir = new List<string>();
            List<string> badEffect = new List<string>();
            List<string> badWorkType = new List<string>();
            List<string> badSource = new List<string>();

            foreach (ProfessionalSkillDef sd in skillDefs)
            {
                if (sd == null) continue;

                // A2：direction 必须指向真实存在的 ProfessionalDirectionDef
                if (!string.IsNullOrEmpty(sd.direction)
                    && DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(sd.direction) == null)
                {
                    badDir.Add(sd.defName + "→" + sd.direction);
                }

                // A3：effectDefNames 不得悬空（悬空 = 该技能声明的效果永远不生效）
                if (sd.effectDefNames != null)
                {
                    foreach (string eff in sd.effectDefNames)
                    {
                        if (string.IsNullOrEmpty(eff)) continue;
                        if (DefDatabase<ProfessionalEffectDef>.GetNamedSilentFail(eff) == null)
                        {
                            badEffect.Add(sd.defName + "→" + eff);
                        }
                    }
                }

                // A4：practiceWorkTypeDefNames 必须是真实 WorkTypeDef（拼错 = 实践永不命中）
                if (sd.practiceWorkTypeDefNames != null)
                {
                    foreach (string wt in sd.practiceWorkTypeDefNames)
                    {
                        if (string.IsNullOrEmpty(wt)) continue;
                        if (DefDatabase<WorkTypeDef>.GetNamedSilentFail(wt) == null)
                        {
                            badWorkType.Add(sd.defName + "→" + wt);
                        }
                    }
                }

                // A5：sourceSkills 必须是真实原版 SkillDef（决定 XP 从哪些原版技能分流）
                if (sd.sourceSkills != null)
                {
                    foreach (string sk in sd.sourceSkills)
                    {
                        if (string.IsNullOrEmpty(sk)) continue;
                        if (DefDatabase<SkillDef>.GetNamedSilentFail(sk) == null)
                        {
                            badSource.Add(sd.defName + "→" + sk);
                        }
                    }
                }
            }

            Add(report, "A2 方向引用有效", badDir.Count == 0, Join(badDir), "悬空 direction → 该技能无方向归属、能力映射失效");
            Add(report, "A3 效果引用有效", badEffect.Count == 0, Join(badEffect), "悬空 effectDefName → 加成永远为 0");
            Add(report, "A4 实践工作类型有效", badWorkType.Count == 0, Join(badWorkType), "拼错的 WorkTypeDef → 实践永不命中、加成不生效");
            Add(report, "A5 原版技能来源有效", badSource.Count == 0, Join(badSource), "无效 SkillDef → 该专业技能永远拿不到 XP");

            // A6：XP 曲线策略（缺省回退口径的兜底）
            bool hasPolicy = DefDatabase<ProfessionalXpPolicyDef>.AllDefsListForReading != null
                             && DefDatabase<ProfessionalXpPolicyDef>.AllDefsListForReading.Count > 0;
            Add(report, "A6 XP 曲线策略 Def", hasPolicy, hasPolicy ? "ok" : "none（将走 DefaultCurvePower 回退）",
                "缺失不致命：代码有回退值 1.6，但升级曲线将不可配置", critical: false);

            // A7：评级 Def（决定 ratingWeight，缺失则加成退化为纯 value）
            bool hasRating = ProfessionalEffectIndex.RatingDefs != null && ProfessionalEffectIndex.RatingDefs.Count > 0;
            Add(report, "A7 评级 Def 已注册", hasRating, hasRating ? "ok" : "none",
                "缺失 → ratingWeight=0，加成退化为 value×(1+0)", critical: false);

            int emptyDir = 0;
            if (dirDefs != null)
            {
                for (int i = 0; i < dirDefs.Count; i++)
                {
                    ProfessionalDirectionDef d = dirDefs[i];
                    if (d == null) continue;
                    if (d.skillDefNames == null || d.skillDefNames.Count == 0) emptyDir++;
                }
            }
            Add(report, "A8 二级方向已挂技能", emptyDir == 0,
                "dirs=" + (dirDefs != null ? dirDefs.Count.ToString() : "0") + " emptySkill=" + emptyDir,
                "skillDefNames 空 → 方向悬停显示待落地、专业等级字段取不到技能");

            // A9：方向→技能「正向」引用必须落地（skillDefNames 指向的技能 Def 必须真实存在）。
            // 这是 UI 蓝图与运行时的唯一真源（Dialog_SetPrimaryDirection.FirstSkillOfDirection /
            // OverviewFieldRegistry.ProLevelValue 都按 direction.skillDefNames 解析）。
            // 悬空 → 玩家选该方向后无任何技能、XP 无处落地、卡牌降级显示「暂未配置」。
            // 注意：A2 查的是 skill.direction（反向），本条查 direction.skillDefNames（正向），二者独立。
            List<string> badDirSkill = new List<string>();
            if (dirDefs != null)
            {
                foreach (ProfessionalDirectionDef d in dirDefs)
                {
                    if (d == null || d.skillDefNames == null) continue;
                    foreach (string sn in d.skillDefNames)
                    {
                        if (string.IsNullOrEmpty(sn)) continue;
                        if (DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(sn) == null)
                        {
                            badDirSkill.Add(d.defName + "→" + sn);
                        }
                    }
                }
            }
            Add(report, "A9 方向→技能引用有效", badDirSkill.Count == 0, Join(badDirSkill),
                "悬空 skillDefName → 玩家选该方向后无技能、XP 无处落地、卡牌降级为「暂未配置」");
        }

        // ================= B 组：逻辑冒烟（内存构造，零副作用）=================
        // 目的：验证「实际注入值」与「浮层明细」同源，以及工作类型门控符合预期。

        private static void RunLogicSmoke(SelfTestReport report)
        {
            List<ProfessionalSkillDef> defs = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
            Dictionary<string, ProfessionalEffectDef> effects = ProfessionalEffectIndex.EffectDefs;
            if (defs == null || defs.Count == 0 || effects == null)
            {
                Add(report, "B1 明细与聚合同源", false, "Defs 未就绪", "依赖 A 组");
                return;
            }

            // 取第一个声明了 WorkSpeed 效果的技能做样本
            ProfessionalSkillDef sample = null;
            foreach (ProfessionalSkillDef sd in defs)
            {
                if (sd == null || sd.effectDefNames == null || sd.effectDefNames.Count == 0) continue;
                foreach (string e in sd.effectDefNames)
                {
                    ProfessionalEffectDef ed = DefDatabase<ProfessionalEffectDef>.GetNamedSilentFail(e);
                    if (ed != null && ed.kind == ProfessionalEffectKind.WorkSpeed) { sample = sd; break; }
                }
                if (sample != null) break;
            }
            if (sample == null)
            {
                Add(report, "B1 明细与聚合同源", true, "无 WorkSpeed 样本，跳过", "当前没有声明 WorkSpeed 的技能", critical: false);
                return;
            }

            // 构造内存样本：等级 10（确保 level>=1 才进入计算）
            List<ProfessionalSkillData> skills = new List<ProfessionalSkillData>
            {
                new ProfessionalSkillData { skillDefName = sample.defName, level = 10, xp = 1000f }
            };

            string recipeName = (sample.practiceRecipeDefNames != null && sample.practiceRecipeDefNames.Count > 0)
                ? sample.practiceRecipeDefNames[0] : null;
            string workTypeName = (sample.practiceWorkTypeDefNames != null && sample.practiceWorkTypeDefNames.Count > 0)
                ? sample.practiceWorkTypeDefNames[0] : null;

            // B1：明细求和 == 聚合结果（防止浮层与实际注入两套公式漂移）
            List<ProfessionalEffectResolver.SkillBonusContribution> contribs =
                ProfessionalEffectResolver.ResolveContributions(
                    skills, defs, effects, recipeName, workTypeName, ProfessionalEffectIndex.RatingDefs);
            float aggregate = ProfessionalEffectResolver.ResolveSpeedFactor(
                skills, defs, effects, recipeName, ProfessionalEffectIndex.RatingDefs, workTypeName);

            float sum = 1f;
            foreach (ProfessionalEffectResolver.SkillBonusContribution c in contribs)
            {
                if (c != null && c.ActiveNow) sum += c.SpeedBonus;
            }
            bool same = Math.Abs(sum - aggregate) < 1e-5f && contribs.Count > 0;
            Add(report, "B1 明细与聚合同源", same,
                "sum=" + sum.ToString("F5") + " aggregate=" + aggregate.ToString("F5") + " lines=" + contribs.Count,
                "不一致 = ResolveContributions 与 ResolveSpeedFactor 公式漂移，浮层数字会与实际加成对不上");

            // B2：门控——命中白名单应生效，两个上下文都为空则不应生效
            bool activeOk = contribs.Count > 0 && contribs[0].ActiveNow;
            Add(report, "B2 命中白名单时生效", activeOk, "ActiveNow=" + (contribs.Count > 0 ? contribs[0].ActiveNow.ToString() : "n/a"),
                "命中却未生效 = RecipeMatches 判定有误，玩家练了等级却拿不到加成");

            List<ProfessionalEffectResolver.SkillBonusContribution> noCtx =
                ProfessionalEffectResolver.ResolveContributions(
                    skills, defs, effects, null, null, ProfessionalEffectIndex.RatingDefs);
            bool gated = true;
            foreach (ProfessionalEffectResolver.SkillBonusContribution c in noCtx)
            {
                // 只有声明了白名单的技能才必须被门控；无白名单的技能恒命中
                if (c != null && c.ActiveNow)
                {
                    ProfessionalSkillDef sd = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(c.SkillDefName);
                    bool hasWhitelist = sd != null
                        && ((sd.practiceRecipeDefNames != null && sd.practiceRecipeDefNames.Count > 0)
                            || (sd.practiceWorkTypeDefNames != null && sd.practiceWorkTypeDefNames.Count > 0));
                    if (hasWhitelist) gated = false;
                }
            }
            Add(report, "B3 无上下文时不误算", gated, "gated=" + gated,
                "无 recipe/工作类型却被判生效 = 加成会被当成永久被动套用（数值失衡）");

            // B4：WorkCompleted 量子口径（2500 tick = 游戏 1 小时，与 UI「每事件 1h」对齐）
            const long quantum = ArchiveService.WorkCompletedQuantumTicks;
            bool quantumOk = quantum == 2500L;
            Add(report, "B4 工时量子=1游戏小时", quantumOk, "quantum=" + quantum,
                "改了量子而 UI 仍按 1h/事件 折算 → 职业时长统计会整体偏大或偏小");
        }

        // ================= C 组：存档只读一致性 =================
        // 目的：检测已写进存档的职业账本是否有结构性异常（null 元素/负 tick/计数倒挂）。

        private static void RunSaveConsistency(SelfTestReport report)
        {
            if (Current.Game == null)
            {
                Add(report, "C1 职业账本结构", true, "no game (skip)", "无存档上下文", critical: false);
                return;
            }
            ChronicleGameComponent comp = Current.Game.GetComponent<ChronicleGameComponent>();
            if (comp == null || comp.Objects == null)
            {
                Add(report, "C1 职业账本结构", true, "no component (skip)", "组件未就绪", critical: false);
                return;
            }

            int nullEvent = 0, negTick = 0, countMismatch = 0, withCareer = 0;
            foreach (ArchiveObject o in comp.Objects)
            {
                PawnObject po = o as PawnObject;
                if (po == null || po.CareerData == null) continue;
                withCareer++;

                List<Domain.Career.CareerEvent> evs = po.CareerData.Events;
                if (evs == null) continue;
                foreach (Domain.Career.CareerEvent ev in evs)
                {
                    if (ev == null) { nullEvent++; continue; }
                    if (ev.Tick < 0L) negTick++;
                }
                // RecordCountByType 合计不应超过实际事件数（超发 = 计数器被重复递增）
                int total = 0;
                if (po.CareerData.RecordCountByType != null)
                {
                    foreach (KeyValuePair<string, int> kv in po.CareerData.RecordCountByType) total += kv.Value;
                }
                if (total > evs.Count) countMismatch++;
            }

            bool ok = nullEvent == 0 && negTick == 0 && countMismatch == 0;
            Add(report, "C1 职业账本结构", ok,
                "career=" + withCareer + " null=" + nullEvent + " negTick=" + negTick + " countMismatch=" + countMismatch,
                "账本结构异常会导致 Scribe 读档失败或职业时长统计错乱");
        }

        // ================= D 组：数据覆盖快照（只读，不灌数据）=================
        // 目的：把覆盖自检并进启动报告。空缺在开局是正常的；点 ITab「全量灌满」后再点覆盖自检应全绿。

        private static void RunCoverageSnapshot(SelfTestReport report)
        {
            try
            {
                DevCoverageProbe.CoverageReport cov = DevCoverageProbe.ProbeColony();
                Add(report, "D0 已归档样本", cov.ArchivedTotal > 0,
                    cov.ArchivedTotal + "/" + cov.HumanlikeTotal
                    + " battles=" + cov.BattleObjectCount + " events=" + cov.ChronicleEventCount,
                    "无归档则后续维度全为 0；点档案 Tab「全量灌满」可一键写入",
                    critical: false);
                if (cov.Dimensions == null)
                {
                    return;
                }
                for (int i = 0; i < cov.Dimensions.Count; i++)
                {
                    DevCoverageProbe.DimensionResult d = cov.Dimensions[i];
                    if (d == null)
                    {
                        continue;
                    }
                    Add(report, "D " + d.Label, true,
                        d.Filled + "/" + d.Total + " " + d.Status,
                        "覆盖快照（空缺请用全量灌满+报告）",
                        critical: false);
                }
            }
            catch (Exception ex)
            {
                Add(report, "D0 覆盖快照", false, ex.Message, "ProbeColony 抛异常", critical: false);
            }
        }

        // ── 小工具 ────────────────────────────────────────────────

        private static void Add(SelfTestReport report, string name, bool passed, string detail, string hint, bool critical = true)
        {
            report.Checks.Add(new CheckResult
            {
                Name = name,
                Passed = passed,
                Detail = string.IsNullOrEmpty(detail) ? (passed ? "ok" : "—") : detail,
                Critical = critical
            });
            if (!passed && critical)
            {
                // 关键失败同时写日志，便于从 Player.log 回溯（弹窗可能被玩家秒关）
                ChronicleLog.Warning(ChronicleLog.Category.Ui, "[SelfTest] FAIL " + name + " :: " + detail + " — " + hint);
            }
        }

        private static string Join(List<string> items)
        {
            if (items == null || items.Count == 0) return "ok";
            int take = Math.Min(5, items.Count);
            string s = string.Join(", ", items.GetRange(0, take).ToArray());
            return items.Count > take ? s + " (+" + (items.Count - take) + ")" : s;
        }

        /// <summary>
        /// 把整份报告写入 Player.log（弹窗之外留一份可检索记录）。
        /// 公开供弹窗「输出日志」按钮调用——玩家秒关弹窗后仍可回溯。
        /// </summary>
        public static void DumpReportToLog(SelfTestReport report)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("== PersonalChronicle Self-Test ==");
            sb.AppendLine("pass=" + report.PassedCount + " fail=" + report.FailedCount);
            for (int i = 0; i < report.Checks.Count; i++)
            {
                CheckResult c = report.Checks[i];
                sb.AppendLine((c.Passed ? "[PASS] " : "[FAIL] ") + c.Name + " :: " + c.Detail);
            }
            ChronicleLog.Info(ChronicleLog.Category.Ui, sb.ToString());
        }
    }
}
