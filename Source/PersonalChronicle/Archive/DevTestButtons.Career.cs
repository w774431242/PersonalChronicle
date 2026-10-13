using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Api;
using PersonalChronicle.Application;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// [ARC-013] <see cref="DevTestButtons"/> 的「职业事实层调试设施」切片
    /// （P1 CAREER-001 / BE-007 / FE-001）。
    ///
    /// 原 DevTestButtons.cs 单文件 1051 行，且混装「通用测试数据生成」与「职业事实层调试」
    /// 两套职责，违反 [ARC-013] 单一职责与「单文件 ≤ 800 行」。按 08 §3「排序→识别→剥离」，
    /// 本文件剥离其中内聚度最高、源码已用 `// ---- P1 CAREER-001 ----` 注释明确划界的一组。
    ///
    /// 剥离原则（[ARC-013] 零契约改动）：仅物理搬移，类名、方法签名、访问级别、
    /// Save schema、翻译键一律未改；同 partial 类成员天然互相可见，跨文件调用无需改访问级别。
    /// </summary>
    public static partial class DevTestButtons
    {
        // ---- P1 CAREER-001 职业事实层调试设施（BE-007 / FE-001）-----------------

        /// <summary>
        /// 取 pawn 的 <see cref="PawnObject"/>（经 ChronicleGameComponent）；无则 null。
        /// 仅 Dev 工具使用。
        /// </summary>
        private static PawnObject GetCareerPawnObject(Pawn pawn)
        {
            if (pawn == null || Current.Game == null)
            {
                return null;
            }
            ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
            if (component == null)
            {
                return null;
            }
            return component.GetObject(pawn.GetUniqueLoadID()) as PawnObject;
        }

        /// <summary>BE-007 Print：输出职业事实 ledger 摘要到日志 + 屏幕消息。</summary>
        public static void CareerPrint(Pawn pawn)
        {
            PawnObject record = GetCareerPawnObject(pawn);
            if (record == null || record.CareerData == null)
            {
                Messages.Message("PersonalChronicle.UI.Dev.NoCareer".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            List<CareerEvent> events = record.CareerData.Events;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("== Career Events: " + (pawn?.LabelShort ?? "?") + " (Total " + events.Count + ") ==");
            for (int i = 0; i < events.Count; i++)
            {
                CareerEvent e = events[i];
                sb.AppendLine(e.Tick + " | " + e.EventType + " | " + e.DefName
                    + " | " + (e.Quality ?? "-") + " | " + (e.SkillDefName ?? "-"));
            }
            ChronicleLog.Info(ChronicleLog.Category.Ui, sb.ToString());
            Messages.Message("PersonalChronicle.UI.Dev.Printed".Translate(events.Count.ToString()).ToString(), MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>BE-007 Add Test：为 pawn 追加一条 ItemProduced 测试事实（模拟制造）。</summary>
        public static void CareerAddTest(Pawn pawn)
        {
            PawnObject record = GetCareerPawnObject(pawn);
            if (record == null)
            {
                Messages.Message("PersonalChronicle.UI.Dev.PawnNotArchived".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (record.CareerData == null)
            {
                record.CareerData = new CareerData();
            }
            long tick = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            string pawnId = pawn.GetUniqueLoadID();
            CareerEvent ev = new CareerEvent(
                pawnId + ":" + tick + ":devtest",
                pawnId,
                tick,
                CareerEventType.ItemProduced,
                "DevTest_Product",
                "Crafting",
                "Excellent",
                null);
            record.CareerData.Events.Add(ev);
            IncrementCareerCount(record.CareerData, CareerEventType.ItemProduced);
            ChronicleGameComponent component = Current.Game?.GetComponent<ChronicleGameComponent>();
            component?.MarkChanged();
            Messages.Message("PersonalChronicle.UI.Dev.EventAdded".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>BE-007 Clear：清空 pawn 的职业事实 ledger。</summary>
        public static void CareerClear(Pawn pawn)
        {
            PawnObject record = GetCareerPawnObject(pawn);
            if (record?.CareerData != null)
            {
                record.CareerData.Events.Clear();
                if (record.CareerData.RecordCountByType != null) record.CareerData.RecordCountByType.Clear();
                ChronicleGameComponent component = Current.Game?.GetComponent<ChronicleGameComponent>();
                component?.MarkChanged();
            }
            Messages.Message("PersonalChronicle.UI.Dev.Cleared".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>BE-007 Validate：校验 ledger 完整性（去重/类型白名单/空字段），输出到日志。</summary>
        public static void CareerValidate(Pawn pawn)
        {
            PawnObject record = GetCareerPawnObject(pawn);
            if (record == null || record.CareerData == null)
            {
                Messages.Message("PersonalChronicle.UI.Dev.NoCareer".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            List<CareerEvent> events = record.CareerData.Events;
            int dup = 0;
            int badType = 0;
            int nullField = 0;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < events.Count; i++)
            {
                CareerEvent e = events[i];
                if (e == null || string.IsNullOrEmpty(e.EventId) || string.IsNullOrEmpty(e.EventType)
                    || string.IsNullOrEmpty(e.PawnId) || string.IsNullOrEmpty(e.DefName))
                {
                    nullField++;
                    continue;
                }
                if (!CareerEventType.IsAllowed(e.EventType))
                {
                    badType++;
                }
                if (!seen.Add(e.EventId))
                {
                    dup++;
                }
            }
            string report = "Career Validate: total=" + events.Count + " dup=" + dup
                + " badType=" + badType + " nullField=" + nullField;
            ChronicleLog.Info(ChronicleLog.Category.Ui, report);
            Messages.Message("PC Career: " + report, dup == 0 && badType == 0 && nullField == 0
                ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, false);
        }

        private static void IncrementCareerCount(CareerData data, string eventType)
        {
            if (data.RecordCountByType == null) data.RecordCountByType = new Dictionary<string, int>();
            if (data.RecordCountByType.ContainsKey(eventType)) data.RecordCountByType[eventType]++;
            else data.RecordCountByType[eventType] = 1;
        }

        /// <summary>
        /// 随机化职业全链路测试数据（P1~P8 + 专业适配分析输入），仅 DevMode 工具用。
        /// 覆盖式：先清空再随机生成，便于反复点击看到不同数字；生成后触发 MarkChanged 使 ReadModel 重建。
        /// 覆盖范围（对齐 docs/UI预览/人物档案视窗/职业档案Tab预览.html randomizeCareerData）：
        ///   1) 原版 12 技能等级 + 兴趣（驱动职业规划 12 专业适配分析 chips）
        ///   2) 专业技能等级 + 能力 XP + 实践计数（按原版技能键）
        ///   3) CareerEvent 事实（制造/建造/研究，品质分布含 Masterwork，传奇触发成就）
        ///   4) 职称 5 档随机推导（等级区间 → 档位 → 职称/资格联动，非固定档）
        ///   5) 资格状态/预检/下一职称（考试/论文/答辩按下一档资格生成通过记录）
        ///   6) 书籍证据（1~4 本）、生涯工时跨度（300~1200h）
        ///   7) 勋章墙：成就勋章（Legend/MajorProject 联动）+ 随机 2~6 枚 Pawn 阈值勋章（清空重授）
        ///   8) 工坊快照（履历页/当前就职数据源）
        /// </summary>
        public static void CareerRandomize(Pawn pawn)
        {
            CareerRandomize(pawn, true);
        }

        public static void CareerRandomize(Pawn pawn, bool announce)
        {
            if (!Prefs.DevMode || pawn == null)
            {
                return;
            }
            PawnObject record = GetCareerPawnObject(pawn);
            if (record == null)
            {
                Messages.Message("PersonalChronicle.UI.Dev.PawnNotArchived".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (record.CareerData == null)
            {
                record.CareerData = new CareerData();
            }
            System.Random rng = new System.Random();
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            string pawnId = pawn.GetUniqueLoadID();
            CareerData cd = record.CareerData;

            // 0) 覆盖式重置职业相关全部容器（含勋章墙——HTML 语义：清空再随机）。
            cd.Events.Clear();
            if (cd.RecordCountByType != null) cd.RecordCountByType.Clear();
            cd.Professional = new ProfessionalState();
            cd.Qualification = new QualificationState();
            cd.Exams = new ExamData();
            cd.Books = new List<BookEvidence>();
            cd.Thesis = new ThesisData();
            cd.GrantedTitles = new List<GrantedTitle>();
            record.GrantedMedals = new List<string>();

            // 1) 原版 12 技能等级 + 兴趣（活读 pawn.skills；驱动 ProfessionalFitAnalyzer）。
            RandomizeVanillaSkills(pawn, rng);
            cd.Professional.primaryDirection = PickPrimaryDirectionDefName(pawn);

            // 2) 全部专业技能等级（48 条 Direction 切片）+ 能力 XP + 实践计数。
            int level = FillAllProfessionalSkills(pawn, cd, rng, now);

            // 3) 生涯跨度（资历 300~1200h → 事件首末跨度，驱动 CareerOverview.HoursText）。
            int hours = rng.Next(300, 1201);
            long spanTicks = (long)hours * 2400L;
            long startTick = now - spanTicks;
            long firstTick = startTick;

            // 4) 随机制造事实（品质 5 档含 Masterwork，对齐 HTML QUAL_W 权重表；传奇触发 P8 成就）。
            int itemCount = rng.Next(8, 30);
            string[] qualities = { "Normal", "Good", "Excellent", "Masterwork", "Legendary" };
            int legendary = 0;
            int major = 0;
            for (int i = 0; i < itemCount; i++)
            {
                string q = qualities[rng.Next(qualities.Length)];
                Dictionary<string, string> meta = null;
                if (q == "Legendary") legendary++;
                if (rng.Next(0, 5) == 0)
                {
                    meta = new Dictionary<string, string> { { "major", "1" } };
                    major++;
                }
                long t = startTick + (long)((i + 1) * spanTicks / (itemCount + 1));
                cd.Events.Add(new CareerEvent(
                    pawnId + ":" + t + ":item:" + i,
                    pawnId, t, CareerEventType.ItemProduced,
                    "DevTest_Product", "Crafting", q, "Make_ComponentIndustrial", 1, meta));
                IncrementCareerCount(cd, CareerEventType.ItemProduced);
            }
            // 建造/研究事实（驱动总览 metric 三格与荣誉贡献结构；均为真实计数）。
            int buildCount = rng.Next(0, 6);
            int researchCount = rng.Next(0, 6);
            for (int i = 0; i < buildCount; i++)
            {
                long t = startTick + (long)((i + 1) * spanTicks / (buildCount + 1));
                cd.Events.Add(new CareerEvent(pawnId + ":" + t + ":build:" + i, pawnId, t,
                    CareerEventType.ConstructionCompleted, "DevTest_Building", "Construction", null, null, 1, null));
                IncrementCareerCount(cd, CareerEventType.ConstructionCompleted);
            }
            for (int i = 0; i < researchCount; i++)
            {
                long t = startTick + (long)((i + 1) * spanTicks / (researchCount + 1));
                cd.Events.Add(new CareerEvent(pawnId + ":" + t + ":research:" + i, pawnId, t,
                    CareerEventType.ResearchCompleted, "DevTest_Research", "Intellectual", null, null, 1, null));
                IncrementCareerCount(cd, CareerEventType.ResearchCompleted);
            }

            // 5) 职称 5 档随机推导（对齐 HTML buildCareerOverview：等级区间 + 传奇门槛）。
            string tierKey = level >= 45 && legendary >= 1 ? "Master"
                : level >= 38 ? "Specialist"
                : level >= 25 ? "Senior"
                : level >= 15 ? "Assistant" : "Junior";
            string titleDefName = "Title_Precision_" + tierKey;
            string qualDefName = "Q_Precision_" + tierKey;
            // 下一档资格（考试/论文/答辩记录按此生成，匹配 ReadModel 判定契约）。
            string nextQualDefName = NextQualificationDefName(cd, qualDefName);
            long titleTick = now - 2000L;
            cd.Events.Add(new CareerEvent(pawnId + ":" + titleTick + ":title", pawnId, titleTick,
                CareerEventType.TitleGranted, titleDefName, "ProfessionalSkill_PrecisionManufacturing", null, null, 1, null));
            cd.GrantedTitles.Add(new GrantedTitle(titleDefName, qualDefName, titleTick));
            cd.DisplayedHonorKey = HonorDisplayKeys.ForTitle(titleDefName);
            IncrementCareerCount(cd, CareerEventType.TitleGranted);

            // 6) 资格进度（当前档全通过；评分随机 70~95，保证 CompositeScore 达标）。
            QualificationProgress qp = cd.Qualification.GetOrAdd(qualDefName);
            qp.Status = QualificationStatus.Granted;
            qp.PracticalPassed = true;
            qp.TheoryPassed = true;
            qp.ThesisPassed = true;
            qp.DefensePassed = true;
            qp.CompositeScore = 70f + (float)rng.Next(0, 26);
            qp.DecidedTick = titleTick;
            if (nextQualDefName != null)
            {
                // 考试/论文/答辩通过记录（针对下一档资格生成；BuildQualRows/PreCheck 按此判定）。
                cd.Events.Add(new CareerEvent(pawnId + ":" + now + ":exam", pawnId, now,
                    CareerEventType.ExamPassed, nextQualDefName, null, null, null, 1, null));
                IncrementCareerCount(cd, CareerEventType.ExamPassed);
                cd.Exams.Practical.Add(new PracticalExamRecord
                {
                    ExamId = pawnId + ":pexam",
                    QualificationDefName = nextQualDefName,
                    TargetRecipeDefNames = new List<string> { "Make_ComponentIndustrial" },
                    RequiredCount = 3,
                    MinQuality = "Excellent",
                    TimeLimitTicks = 100000L,
                    StartedTick = now - 50000L,
                    ProducedCount = 3,
                    ProducedQualities = new List<string> { "Excellent", "Excellent", "Excellent" },
                    Passed = true,
                    Score = 100f
                });
                cd.Exams.Theory.Add(new TheoryExamRecord
                {
                    QualificationDefName = nextQualDefName,
                    RequiredBookTopics = new List<string> { "Precision" },
                    RequiredResearchCount = 2,
                    BookScore = 90f,
                    ResearchScore = 80f,
                    SkillScore = 85f,
                    ActivityScore = 70f,
                    Passed = true,
                    Score = 85f
                });
                cd.Thesis.Theses.Add(new ThesisEvidence
                {
                    ThesisId = nextQualDefName,
                    QualificationDefName = nextQualDefName,
                    SourceBookIds = new List<string> { "book1" },
                    SourceResearchEventIds = new List<string> { "r1" },
                    BaseQuality = 85f,
                    ComputedScore = 88f,
                    Completed = true,
                    CompletedTick = now - 500L
                });
                cd.Thesis.Defenses.Add(new DefenseRecord
                {
                    ThesisId = nextQualDefName,
                    QualificationDefName = nextQualDefName,
                    CommitteePawnIds = new List<string> { "cm1", "cm2" },
                    CommitteeScore = 92f,
                    FinalScore = 90f,
                    Passed = true,
                    HeldTick = now - 200L
                });
            }

            // 7) 书籍证据 1~4 本（理论考试语义：Books.Count 驱动 Ov.Books 与摘要）。
            int bookCount = rng.Next(1, 5);
            for (int b = 0; b < bookCount; b++)
            {
                cd.Books.Add(new BookEvidence
                {
                    BookThingId = "book" + b,
                    AuthorPawnId = pawnId,
                    Topic = "Precision",
                    Quality = b < 2 ? "Good" : "Normal",
                    Field = "Manufacturing",
                    CreatedTick = now - 30000L - b * 1000L,
                    Relevance = 1f
                });
                IncrementCareerCount(cd, CareerEventType.BookProduced);
            }

            // 8) 勋章墙：成就勋章（传奇/重大联动）+ 随机 2~6 枚 Pawn 阈值勋章（清空重授）。
            record.GrantedMedals = new List<string>();
            if (legendary >= 1) record.AddGrantedMedal("Medal_Craft_Legend_Bronze");
            if (legendary >= 5) record.AddGrantedMedal("Medal_Craft_Legend_Silver");
            if (legendary >= 15) record.AddGrantedMedal("Medal_Craft_Legend_Gold");
            if (major >= 3) record.AddGrantedMedal("Medal_Craft_MajorProject_Gold");
            List<string> awarded = new List<string>();
            if (legendary >= 1) awarded.Add("Medal_Craft_Legend_Bronze");
            if (legendary >= 5) awarded.Add("Medal_Craft_Legend_Silver");
            if (legendary >= 15) awarded.Add("Medal_Craft_Legend_Gold");
            if (major >= 3) awarded.Add("Medal_Craft_MajorProject_Gold");
            int thresholdCount = rng.Next(2, 7);
            List<string> pool = new List<string>(PawnThresholdMedalDefNames);
            while (awarded.Count < 12 && thresholdCount > 0 && pool.Count > 0)
            {
                int pick = rng.Next(pool.Count);
                string defName = pool[pick];
                pool.RemoveAt(pick);
                if (awarded.Contains(defName)) continue;
                awarded.Add(defName);
                record.AddGrantedMedal(defName);
                thresholdCount--;
            }
            // 勋章授予事实回写（驱动「最近荣誉事件」时间线；每枚一条）。
            for (int m = 0; m < awarded.Count; m++)
            {
                long mt = now - 1000L - m * 500L;
                cd.Events.Add(new CareerEvent(pawnId + ":" + mt + ":medal:" + m, pawnId, mt,
                    CareerEventType.MedalGranted, awarded[m], null, null, null, 1, null));
                IncrementCareerCount(cd, CareerEventType.MedalGranted);
            }

            // 9) 工坊快照（履历页 resume-block / 工坊汇总 / 当前就职数据源）。
            record.Workplace = new WorkplaceSnapshot
            {
                BuildingDefName = "TableMachining",
                BuildingStableId = "TableMachining:1",
                CustomName = null,
                RoomRoleDefName = "Workshop",
                UseCount = rng.Next(200, 2101),
                LastUsedTick = now - 1000L,
                MapIndex = -1,

            };

            ChronicleGameComponent component = Current.Game?.GetComponent<ChronicleGameComponent>();
            component?.MarkChanged();
            if (announce)
            {
                Messages.Message("PersonalChronicle.UI.Dev.Randomized".Translate(tierKey, level.ToString(), legendary.ToString(), major.ToString(), awarded.Count.ToString()),
                    MessageTypeDefOf.NeutralEvent, false);
            }
        }

        /// <summary>
        /// 为全部 ProfessionalSkillDef 写入 XP/等级（原版 sourceSkills 取高 + 随机 XP）。
        /// 返回精密制造等级（职称档位推导仍用该值）。
        /// </summary>
        private static int FillAllProfessionalSkills(Pawn pawn, CareerData cd, System.Random rng, long now)
        {
            if (cd.Professional.skills == null)
            {
                cd.Professional.skills = new List<ProfessionalSkillData>();
            }
            else
            {
                cd.Professional.skills.Clear();
            }
            if (cd.Professional.practiceCountBySkill == null)
            {
                cd.Professional.practiceCountBySkill = new Dictionary<string, int>();
            }
            for (int s = 0; s < VanillaSkillDefNames.Count; s++)
            {
                string key = VanillaSkillDefNames[s];
                int v = rng.Next(0, 340);
                if (key == "Crafting") v = Mathf.Max(v, 180);
                if (key == "Construction") v = Mathf.Max(v, 120);
                cd.Professional.practiceCountBySkill[key] = v;
            }

            const float curvePower = 1.6f;
            int precisionLevel = 8;
            List<ProfessionalSkillDef> defs = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                ProfessionalSkillDef def = defs[i];
                if (def == null || string.IsNullOrEmpty(def.defName))
                {
                    continue;
                }
                int maxLevel = def.maxLevel > 0 ? def.maxLevel : 50;
                float xpCap = def.xpCap > 0f ? def.xpCap : 5000f;
                int vanilla = 0;
                if (pawn != null && pawn.skills != null && def.sourceSkills != null)
                {
                    for (int s = 0; s < def.sourceSkills.Count; s++)
                    {
                        SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(def.sourceSkills[s]);
                        if (sd == null) continue;
                        SkillRecord rec = pawn.skills.GetSkill(sd);
                        if (rec != null && rec.Level > vanilla) vanilla = rec.Level;
                    }
                }
                float xpTotal = 400f + (float)rng.NextDouble() * (xpCap - 400f);
                int fromXp = ProfessionalXpEvaluator.LevelFromXp(xpTotal, maxLevel, xpCap, curvePower);
                int level = fromXp;
                if (vanilla > level) level = vanilla;
                if (level < 5) level = 5;
                if (level > maxLevel) level = maxLevel;
                ProfessionalSkillData skill = new ProfessionalSkillData
                {
                    skillDefName = def.defName,
                    xp = xpTotal,
                    level = level,
                    mastery = ProfessionalXpEvaluator.MasteryFromLevel(level, maxLevel),
                    firstAcquiredTick = now - 1800000L,
                    lastPracticeTick = now - 1000L,
                    practiceCount = rng.Next(20, 200)
                };
                if (def.abilityKeys != null && def.abilityKeys.Count > 0)
                {
                    skill.abilityXp = new Dictionary<string, float>();
                    for (int a = 0; a < def.abilityKeys.Count; a++)
                    {
                        string ak = def.abilityKeys[a];
                        if (!string.IsNullOrEmpty(ak))
                        {
                            skill.abilityXp[ak] = rng.Next(100, 5000);
                        }
                    }
                }
                if (def.sourceSkills != null)
                {
                    for (int s = 0; s < def.sourceSkills.Count; s++)
                    {
                        skill.AddXpBySource(def.sourceSkills[s], xpTotal / Math.Max(1, def.sourceSkills.Count));
                    }
                }
                cd.Professional.skills.Add(skill);
                if (def.defName == "ProfessionalSkill_PrecisionManufacturing")
                {
                    precisionLevel = level;
                }
            }
            return precisionLevel;
        }

        /// <summary>按原版最高技能匹配一条 ProfessionalDirectionDef（矿工 → 采矿方向，而非写死精密制造）。</summary>
        private static string PickPrimaryDirectionDefName(Pawn pawn)
        {
            string bestVanilla = null;
            int bestLv = -1;
            if (pawn != null && pawn.skills != null)
            {
                for (int i = 0; i < VanillaSkillDefNames.Count; i++)
                {
                    SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(VanillaSkillDefNames[i]);
                    if (sd == null) continue;
                    SkillRecord rec = pawn.skills.GetSkill(sd);
                    if (rec != null && rec.Level > bestLv)
                    {
                        bestLv = rec.Level;
                        bestVanilla = sd.defName;
                    }
                }
            }
            if (!string.IsNullOrEmpty(bestVanilla))
            {
                List<ProfessionalSkillDef> skills = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
                for (int i = 0; i < skills.Count; i++)
                {
                    ProfessionalSkillDef def = skills[i];
                    if (def == null || def.sourceSkills == null || string.IsNullOrEmpty(def.direction)) continue;
                    if (!def.sourceSkills.Contains(bestVanilla)) continue;
                    if (DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(def.direction) != null)
                    {
                        return def.direction;
                    }
                }
            }
            return "Manufacturing_Precision";
        }

        /// <summary>
        /// 随机化原版 12 技能等级与兴趣（DevMode 测试用；驱动职业规划适配分析输入）。
        /// 对齐 HTML SAMPLE_SKILLS / SAMPLE_PASSION 语义：Crafting 保底 ≥12，其余 1~20。
        /// </summary>
        private static void RandomizeVanillaSkills(Pawn pawn, System.Random rng)
        {
            if (pawn == null || pawn.skills == null) return;
            for (int i = 0; i < VanillaSkillDefNames.Count; i++)
            {
                SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(VanillaSkillDefNames[i]);
                if (sd == null) continue;
                SkillRecord rec = pawn.skills.GetSkill(sd);
                if (rec == null) continue;
                int v = rng.Next(1, 21);
                if (VanillaSkillDefNames[i] == "Crafting") v = Mathf.Max(v, 12);
                rec.Level = v;
                int roll = rng.Next(0, 10);
                rec.passion = roll < 3 ? Passion.Major : (roll < 6 ? Passion.Minor : Passion.None);
            }
        }

        /// <summary>
        /// 按 order 升序取第一个「其职称未授予」的资格 defName（与 ReadModel FindNextQualification 同序），
        /// 用于把考试/论文/答辩记录挂到正确的下一档资格上。无则返回 null（已封顶）。
        /// </summary>
        private static string NextQualificationDefName(CareerData cd, string grantedQualDefName)
        {
            if (cd == null || cd.GrantedTitles == null || string.IsNullOrEmpty(grantedQualDefName)) return null;
            List<QualificationDef> all = DefDatabase<QualificationDef>.AllDefsListForReading
                .Where(q => q != null && !string.IsNullOrEmpty(q.titleDefName))
                .OrderBy(q => q.order)
                .ToList();
            for (int i = 0; i < all.Count; i++)
            {
                QualificationDef q = all[i];
                if (string.IsNullOrEmpty(q.titleDefName)) continue;
                bool granted = false;
                for (int k = 0; k < cd.GrantedTitles.Count; k++)
                {
                    GrantedTitle gt = cd.GrantedTitles[k];
                    if (gt != null && string.Equals(gt.TitleDefName, q.titleDefName, StringComparison.Ordinal))
                    {
                        granted = true;
                        break;
                    }
                }
                if (!granted) return q.defName;
            }
            return null;
        }

        /// <summary>
        /// 12 原版技能稳定键。[GOV-006] 单一真源：引用 <see cref="ProfessionalFitAnalyzer.VanillaSkillKeys"/>
        /// （由权重矩阵键反推），**不再维护第三份字面量副本** —— 此前 ProfessionalFitAnalyzer
        /// （权重矩阵）、ITab_Pawn_Career（UI 采集）、本文件（Dev 造数）三处各存一份，
        /// 新增技能需同步改三处、漏改即静默失配。现统一收敛到 Domain 层唯一真源。
        /// </summary>
        private static readonly IReadOnlyList<string> VanillaSkillDefNames = ProfessionalFitAnalyzer.VanillaSkillKeys;

        /// <summary>Pawn 归属阈值勋章池（21 枚；Legacy 传承系为 Thing 归属、Craft 系为成就类，不在此池）。</summary>
        private static readonly string[] PawnThresholdMedalDefNames =
        {
            "Medal_Labor_Model_Bronze", "Medal_Labor_Model_Silver", "Medal_Labor_Model_Gold",
            "Medal_Labor_Worker_Bronze", "Medal_Labor_Worker_Silver", "Medal_Labor_Worker_Gold",
            "Medal_Labor_TechAce_Bronze", "Medal_Labor_TechAce_Silver", "Medal_Labor_TechAce_Gold",
            "Medal_Combat_Hero_Bronze", "Medal_Combat_Hero_Silver", "Medal_Combat_Hero_Gold",
            "Medal_Combat_FirstClass_Bronze", "Medal_Combat_FirstClass_Silver", "Medal_Combat_FirstClass_Gold",
            "Medal_Combat_Enlistee_Bronze", "Medal_Combat_Enlistee_Silver", "Medal_Combat_Enlistee_Gold",
            "Medal_Support_Quartermaster_Bronze", "Medal_Support_Quartermaster_Silver", "Medal_Support_Quartermaster_Gold"
        };

        /// <summary>
        /// FE-001 Career Debug Inspector：在档案 ITab（DevMode）渲染职业事实 ledger
        /// 文本列表，用于验证后端是否正确记录游戏行为。仅展示，不写任何评价。
        /// </summary>
        public static void DrawCareerInspector(Rect rect, Pawn pawn)
        {
            if (!Prefs.DevMode || pawn == null)
            {
                return;
            }
            PawnObject record = GetCareerPawnObject(pawn);
            if (record == null || record.CareerData == null)
            {
                return;
            }
            List<CareerEvent> events = record.CareerData.Events;
            float lineH = 18f;
            // 标题
            string header = "Career Events (" + events.Count + ")";
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, lineH), header);
            float y = rect.y + lineH;
            int maxRows = Mathf.FloorToInt((rect.height - lineH) / lineH);
            int start = Mathf.Max(0, events.Count - maxRows);
            for (int i = start; i < events.Count && y < rect.y + rect.height - lineH; i++)
            {
                CareerEvent e = events[i];
                string line = e.Tick + "  " + e.EventType + "  " + e.DefName
                    + "  " + (e.Quality ?? "-") + "  " + (e.SkillDefName ?? "-");
                Widgets.Label(new Rect(rect.x, y, rect.width, lineH), line);
                y += lineH;
            }
        }
    }
}
