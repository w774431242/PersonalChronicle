// 专业成长蓝图 Read Model 快照（Overview Tab 的"做什么/加多少"区块专用）。
//
// 铁律：UI 只消费快照，不在窗口内做 Def 查询/排序/重算。
// 本快照只承载**展示口径的派生**，数值一律复用 ProfessionalEffectResolver
// （单一真源 GOV-006）；不会与「专业加成面板」/真实 StatPart 注入值漂移。
//
// 渲染内容：
//   · XpSources        —— 怎么做可获得 XP（原版源技能 + 实践白名单 + 单次 +N XP）
//   · RatingLadder     —— 4 档评级档位（minLevel/速度%/品质档）按当前方向 skillDefNames[0] 的
//                          满级 value × (level/maxLevel) × (1 + workSpeedWeight) 派生
using System.Collections.Generic;
using PersonalChronicle.Application.Effects;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>XP 来源的单条 chip（用于紧凑 chip 链）。</summary>
    public sealed class ProfessionalXpSourceChip
    {
        /// <summary>类别："Skill"（原版源技能）/"WorkType"（实践工作类型）/"Recipe"（配方白名单）。</summary>
        public string Kind;

        /// <summary>已本地化的展示名（原生 SkillDef.LabelCap / WorkTypeDef.LabelCap / RecipeDef.LabelCap）。</summary>
        public string Label;

        /// <summary>原生 defName（无翻译键时兜底显示）。</summary>
        public string DefName;
    }

    /// <summary>XP 来源聚合：按"原版源技能 / 实践白名单"两类分组，配单次基础 XP。</summary>
    public sealed class ProfessionalXpSources
    {
        /// <summary>XP 来源 chip 链（实际渲染顺序：先 Skill，再 WorkType，最后 Recipe）。</summary>
        public List<ProfessionalXpSourceChip> Chips = new List<ProfessionalXpSourceChip>();

        /// <summary>单次基础 XP（ProfessionalSkillDef.xpPerPracticeBase）。</summary>
        public float XpPerPractice;

        /// <summary>该技能所属方向名（如「精密制造」；null = 缺方向，回退一级专业 key）。</summary>
        public string DirectionLabel;

        /// <summary>是否有可用数据（false = 卡牌显示空态）。</summary>
        public bool HasAny => Chips != null && Chips.Count > 0;

        /// <summary>是否有可渲染的 chip（用于决定是否画这一行；空态时隐藏整行而不是显示空盒子）。</summary>
        public bool HasData => XpPerPractice > 0f || HasAny;
    }

    /// <summary>4 档评级档位中"对当前方向技能"实际生效的数值。</summary>
    public sealed class ProfessionalRatingLadderRow
    {
        /// <summary>ProfessionalRatingDef.defName（UI 失败时兜底显示）。</summary>
        public string RatingDefName;

        /// <summary>已翻译的评级名（优先 "PersonalChronicle.Professional.Rating.&lt;defName&gt;.Label"，无则 defName）。</summary>
        public string Label;

        /// <summary>触发本评级的最低等级（含）。</summary>
        public int MinLevel;

        /// <summary>该档下的速度加成百分比（基础 value × (1 + workSpeedWeight)，按方向技能满级口径）。</summary>
        public float SpeedPct;

        /// <summary>该档下的品质档位偏移（含评级权重后取整，对应"+N 档"）。</summary>
        public int QualityLevels;

        /// <summary>该档常驻 Stat 加成百分比（同 SpeedPct 口径，独立列以便显示主目标 Stat 名）。</summary>
        public float GenericPct;

        /// <summary>该技能是否确实带"速度类"效果（WorkSpeed/StatBonus/CombatStat/YieldBonus）。
        /// false = 该专业不提供速度加成，UI 显示"—"而非误导性的 0%。</summary>
        public bool HasSpeed;

        /// <summary>该技能是否确实带"品质类"效果（QualityBias）。
        /// false = 该专业不提供品质偏移，UI 显示"—"而非误导性的 +0▲。</summary>
        public bool HasQuality;

        /// <summary>该技能是否确实带"通用常驻加成"效果（StatBonus/CombatStat/YieldBonus）。
        /// false = 该专业不提供通用加成，UI 显示"—"而非 0%。</summary>
        public bool HasGeneric;

        /// <summary>通用加成目标 StatDef defName（如 ShootingAccuracyPawn），供 UI 显示"射击精度 +5%"。</summary>
        public string GenericStatDefName;
    }

    /// <summary>主修加成单项（按方向 primer.effectDefNames 顺序 = 档位解锁顺序）。</summary>
    public sealed class ProfessionalMajorBonusItem
    {
        /// <summary>ProfessionalEffectDef.defName。</summary>
        public string EffectDefName;

        /// <summary>已本地化展示名（StatDef.LabelCap 优先；无 Stat 用 labelKey；再回退 defName）。</summary>
        public string StatLabel;

        /// <summary>效果分类标签（速度/通用/品质/经验），UI 用。</summary>
        public string KindLabel;

        /// <summary>满级大师档口径加成百分比（isReduction 已翻转成负值，如 -8% = 瞄准延迟降低）。</summary>
        public float ValuePct;

        /// <summary>是否"越低越好"语义（瞄准延迟等）：true = 减益式正向。</summary>
        public bool IsReduction;
    }

    /// <summary>
    /// 专业成长蓝图快照：XP 来源 + 评级档位表。
    /// 单 pawn / 单方向为粒度；统一为前向渲染（不与 CareerOverviewView 持久状态耦合）。
    /// </summary>
    public sealed class ProfessionalGuideView
    {
        public ProfessionalXpSources XpSources = new ProfessionalXpSources();

        public List<ProfessionalRatingLadderRow> RatingLadder = new List<ProfessionalRatingLadderRow>();

        /// <summary>主修加成列表（primer.effectDefNames 顺序 = 档位解锁顺序；满级大师口径数值）。</summary>
        public List<ProfessionalMajorBonusItem> MajorBonuses = new List<ProfessionalMajorBonusItem>();

        /// <summary>当前 pawn 已解锁的评级档位数量（1~4）；决定主修加成展示几条。0 = 未学该方向。</summary>
        public int UnlockedTierCount;

        /// <summary>该蓝图的展示标题（取方向名；fallback 一级专业）。</summary>
        public string Title;

        /// <summary>该主方向是否真有配置专业技能 Def（按 direction.skillDefNames 解析）。
        /// false = 罕见数据缺漏（如方向引用的技能 Def 未定义）；UI 显示「暂未配置专业」提示而非崩溃/误导。</summary>
        public bool DirectionConfigured;

        /// <summary>是否有任何可展示内容（false = 卡牌显示空态整行）。</summary>
        public bool HasAny =>
            (XpSources != null && XpSources.HasData)
            || (RatingLadder != null && RatingLadder.Count > 0);
    }

    /// <summary>
    /// 专业成长蓝图快照构建器（Application 静态工具）。
    /// 输入：当前 pawn + 当前主方向 ProfessionalDirectionDef；输出：读模型快照。
    /// 数值一律走 ProfessionalEffectResolver.ScaleByLevel（单一真源）。
    /// </summary>
    public static class ProfessionalGuideSnapshotBuilder
    {
        /// <summary>
        /// 构建指定方向/pawn 的成长蓝图。
        /// <paramref name="direction"/> 缺省时回退 pawn.profileData.primaryDirection，否则返回空快照。
        /// </summary>
        public static ProfessionalGuideView Build(Pawn pawn, ProfessionalDirectionDef direction)
        {
            ProfessionalGuideView view = new ProfessionalGuideView();
            if (pawn == null || direction == null) return view;

            // 1) 方向展示标题
            view.Title = DirectionLabel(direction);
            if (string.IsNullOrEmpty(view.Title))
            {
                view.Title = direction.profession;
            }

            // 2) 取方向下属技能列表（按 direction.skillDefNames 解析，与运行时 FirstSkillOfDirection 完全对齐）。
            //    空 → 标记未配置，仍能返回 view 供 UI 显示「暂未配置」提示，不崩溃。
            List<ProfessionalSkillDef> skillDefs = SkillDefsForDirection(direction);
            view.DirectionConfigured = skillDefs != null && skillDefs.Count > 0;
            if (!view.DirectionConfigured) return view;

            // 3) XP 来源 chip 链：合并首个有效技能的 sourceSkills + practiceWorkTypeDefNames + practiceRecipeDefNames
            ProfessionalSkillDef primer = skillDefs[0];
            BuildXpSources(view.XpSources, primer, direction);

            // 4) 评级档位表：4 档按 order 升序遍历，套上 primer 的 maxLevel + 第一 WorkSpeed/QualityBias 的 value
            BuildRatingLadder(view.RatingLadder, primer);

            // 5) 主修加成列表（primer.effectDefNames 顺序 = 档位解锁顺序；满级大师口径数值）。
            //    展示条数 = 当前已解锁档位数量（每升一档 +1，大师档共 4 个；未学则该方向不展示）。
            int primerLevel = CurrentPrimerLevel(pawn, primer);
            view.UnlockedTierCount = CountUnlockedTiers(primerLevel);
            BuildMajorBonuses(view.MajorBonuses, primer);

            return view;
        }

        /// <summary>由方向 defName 派生增长蓝图；缺方向/pawn 时返回空快照（与 Build 等价入口）。</summary>
        public static ProfessionalGuideView Build(Pawn pawn, string directionDefName)
        {
            if (pawn == null || string.IsNullOrEmpty(directionDefName)) return new ProfessionalGuideView();
            ProfessionalDirectionDef dir = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(directionDefName);
            return Build(pawn, dir);
        }

        // ─────────────────────────── 私有：方向取技能列表 ───────────────────────────
        // 取方向下属技能列表：严格按 direction.skillDefNames 解析 DefDatabase。
        // 与运行时唯一真源（Dialog_SetPrimaryDirection.FirstSkillOfDirection / OverviewFieldRegistry.ProLevelValue）一致，
        // 不再用 def.direction == 方向defName 反查（那会与运行时失配，导致某些方向漏显）。
        private static List<ProfessionalSkillDef> SkillDefsForDirection(ProfessionalDirectionDef direction)
        {
            List<ProfessionalSkillDef> result = new List<ProfessionalSkillDef>();
            if (direction == null || direction.skillDefNames == null) return result;
            for (int i = 0; i < direction.skillDefNames.Count; i++)
            {
                string sname = direction.skillDefNames[i];
                if (string.IsNullOrEmpty(sname)) continue;
                ProfessionalSkillDef def = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(sname);
                if (def != null) result.Add(def);
            }
            return result;
        }

        private static string DirectionLabel(ProfessionalDirectionDef direction)
        {
            if (direction == null) return null;
            if (!string.IsNullOrEmpty(direction.labelKey))
            {
                try
                {
                    string t = direction.labelKey.Translate().ToString();
                    if (!string.IsNullOrEmpty(t) && t != direction.labelKey) return t;
                }
                catch { /* 翻译异常 → 回退 defName */ }
            }
            return direction.defName;
        }

        // ─────────────────────────── 私有：XP 来源聚合 ───────────────────────────
        private static void BuildXpSources(ProfessionalXpSources out_, ProfessionalSkillDef primer, ProfessionalDirectionDef direction)
        {
            if (primer == null) return;
            out_.XpPerPractice = primer.xpPerPracticeBase > 0f ? primer.xpPerPracticeBase : 0f;
            out_.DirectionLabel = DirectionLabel(direction);

            // 原版源技能（sourceSkills）
            if (primer.sourceSkills != null)
            {
                for (int i = 0; i < primer.sourceSkills.Count; i++)
                {
                    string sname = primer.sourceSkills[i];
                    if (string.IsNullOrEmpty(sname)) continue;
                    SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(sname);
                    out_.Chips.Add(new ProfessionalXpSourceChip
                    {
                        Kind = "Skill",
                        Label = SkillLabel(sd, sname),
                        DefName = sname,
                    });
                }
            }

            // 实践白名单：优先按 WorkTypeDef（玩家更易理解"干什么活"），其次配方名（取 1-2 个样例）
            bool anyWorkType = primer.practiceWorkTypeDefNames != null && primer.practiceWorkTypeDefNames.Count > 0;
            if (anyWorkType)
            {
                for (int i = 0; i < primer.practiceWorkTypeDefNames.Count; i++)
                {
                    string wname = primer.practiceWorkTypeDefNames[i];
                    if (string.IsNullOrEmpty(wname)) continue;
                    WorkTypeDef wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(wname);
                    out_.Chips.Add(new ProfessionalXpSourceChip
                    {
                        Kind = "WorkType",
                        Label = WorkTypeLabel(wt, wname),
                        DefName = wname,
                    });
                }
            }

            // 配方白名单：WorkType 缺省时才填（互斥），并只取前 2 个样例，避免 chip 链过长
            if (primer.practiceRecipeDefNames != null && primer.practiceRecipeDefNames.Count > 0)
            {
                int maxRecipes = !anyWorkType ? 2 : 0;
                for (int i = 0; i < primer.practiceRecipeDefNames.Count && i < maxRecipes; i++)
                {
                    string rname = primer.practiceRecipeDefNames[i];
                    if (string.IsNullOrEmpty(rname)) continue;
                    RecipeDef rd = DefDatabase<RecipeDef>.GetNamedSilentFail(rname);
                    out_.Chips.Add(new ProfessionalXpSourceChip
                    {
                        Kind = "Recipe",
                        Label = RecipeLabel(rd, rname),
                        DefName = rname,
                    });
                }
            }
        }

        // ─────────────────────────── 私有：评级档位表 ───────────────────────────
        private static void BuildRatingLadder(List<ProfessionalRatingLadderRow> out_, ProfessionalSkillDef primer)
        {
            List<ProfessionalRatingDef> ratingDefs = DefDatabase<ProfessionalRatingDef>.AllDefsListForReading;
            if (ratingDefs == null || ratingDefs.Count == 0) return;
            ratingDefs.Sort((a, b) =>
            {
                int oa = a != null ? a.order : int.MaxValue;
                int ob = b != null ? b.order : int.MaxValue;
                return oa.CompareTo(ob);
            });

            // 诚实抽取：按效果 kind 取每类最大值。同一技能可能只声明一种/部分效果类型，
            // 不能假设 WorkSpeed + QualityBias 双全；缺类的档位 UI 显示"—"而非误导性的 0%。
            float speedValue = 0f;             // WorkSpeed 最大值
            float genericValue = 0f;           // StatBonus / CombatStat / YieldBonus 最大值
            string genericStatDefName = null;  // 通用加成目标 Stat 名（取第一个非空的）
            float qualityValue = 0f;           // QualityBias 最大值
            if (primer.effectDefNames != null && primer.effectDefNames.Count > 0)
            {
                Dictionary<string, ProfessionalEffectDef> effectMap = ProfessionalEffectIndex.EffectDefs;
                if (effectMap == null || effectMap.Count == 0)
                {
                    List<ProfessionalEffectDef> list = DefDatabase<ProfessionalEffectDef>.AllDefsListForReading;
                    if (list != null)
                    {
                        effectMap = new Dictionary<string, ProfessionalEffectDef>();
                        for (int i = 0; i < list.Count; i++)
                        {
                            ProfessionalEffectDef ed = list[i];
                            if (ed != null && !string.IsNullOrEmpty(ed.defName)) effectMap[ed.defName] = ed;
                        }
                    }
                }
                if (effectMap != null)
                {
                    for (int i = 0; i < primer.effectDefNames.Count; i++)
                    {
                        if (!effectMap.TryGetValue(primer.effectDefNames[i], out ProfessionalEffectDef ed) || ed == null)
                            continue;
                        switch (ed.kind)
                        {
                            case ProfessionalEffectKind.WorkSpeed:
                                if (ed.value > speedValue) speedValue = ed.value;
                                break;
                            case ProfessionalEffectKind.StatBonus:
                            case ProfessionalEffectKind.CombatStat:
                            case ProfessionalEffectKind.YieldBonus:
                                if (ed.value > genericValue)
                                {
                                    genericValue = ed.value;
                                    if (string.IsNullOrEmpty(genericStatDefName) && !string.IsNullOrEmpty(ed.statDefName))
                                        genericStatDefName = ed.statDefName;
                                }
                                break;
                            case ProfessionalEffectKind.QualityBias:
                                if (ed.value > qualityValue) qualityValue = ed.value;
                                break;
                        }
                    }
                }
            }
            bool hasSpeed = speedValue > 0f;
            bool hasGeneric = genericValue > 0f;
            bool hasQuality = qualityValue > 0f;
            int maxLevel = primer.maxLevel > 0 ? primer.maxLevel : 50;

            for (int i = 0; i < ratingDefs.Count; i++)
            {
                ProfessionalRatingDef rd = ratingDefs[i];
                if (rd == null) continue;
                // 速度/通用 = 该档位 minLevel 强度 × (1 + workSpeedWeight)；呈现真实成长曲线，
                // 不再把四档都按满级算导致数值完全相同。
                float speedEffective = hasSpeed
                    ? ProfessionalEffectResolver.ScaleByLevel(speedValue, rd.minLevel, maxLevel, rd.workSpeedWeight)
                    : 0f;
                float genericEffective = hasGeneric
                    ? ProfessionalEffectResolver.ScaleByLevel(genericValue, rd.minLevel, maxLevel, rd.workSpeedWeight)
                    : 0f;
                // 品质档位 = value × (1 + qualityBiasWeight) 取整（与 Resolver.ResolveQualityLevels 口径一致）。
                int qualityLevels = hasQuality
                    ? (int)(qualityValue * (1f + rd.qualityBiasWeight))
                    : 0;

                out_.Add(new ProfessionalRatingLadderRow
                {
                    RatingDefName = rd.defName,
                    Label = RatingLabel(rd),
                    MinLevel = rd.minLevel,
                    SpeedPct = speedEffective * 100f,
                    QualityLevels = qualityLevels,
                    GenericPct = genericEffective * 100f,
                    GenericStatDefName = genericStatDefName,
                    HasSpeed = hasSpeed,
                    HasQuality = hasQuality,
                    HasGeneric = hasGeneric,
                });
            }
        }

        // ─────────────────────────── 私有：主修加成列表 ───────────────────────────
        private static void BuildMajorBonuses(List<ProfessionalMajorBonusItem> out_, ProfessionalSkillDef primer)
        {
            if (primer == null || primer.effectDefNames == null || primer.effectDefNames.Count == 0) return;
            // 大师档口径：minLevel 最高档；其余档位只是"解锁更多项"，数值统一用满级口径展示蓝图。
            ProfessionalRatingDef master = GetMasterRating();
            int maxLevel = primer.maxLevel > 0 ? primer.maxLevel : 50;
            float masterWeight = master != null ? master.workSpeedWeight : 0f;
            int masterMin = master != null ? master.minLevel : maxLevel;

            Dictionary<string, ProfessionalEffectDef> effectMap = ProfessionalEffectIndex.EffectDefs;
            if (effectMap == null || effectMap.Count == 0)
            {
                List<ProfessionalEffectDef> list = DefDatabase<ProfessionalEffectDef>.AllDefsListForReading;
                if (list != null)
                {
                    effectMap = new Dictionary<string, ProfessionalEffectDef>();
                    for (int i = 0; i < list.Count; i++)
                    {
                        ProfessionalEffectDef ed = list[i];
                        if (ed != null && !string.IsNullOrEmpty(ed.defName)) effectMap[ed.defName] = ed;
                    }
                }
            }
            if (effectMap == null) return;

            for (int i = 0; i < primer.effectDefNames.Count; i++)
            {
                if (!effectMap.TryGetValue(primer.effectDefNames[i], out ProfessionalEffectDef ed) || ed == null)
                    continue;
                float baseValue = ed.value;
                EffectOverride ov = FindOverrideInPrimer(primer, ed.defName);
                if (ov != null && ov.hasValue) baseValue = ov.value;
                float scaled = ProfessionalEffectResolver.ScaleByLevel(baseValue, masterMin, maxLevel, masterWeight) * 100f;
                if (ed.isReduction) scaled = -scaled;
                out_.Add(new ProfessionalMajorBonusItem
                {
                    EffectDefName = ed.defName,
                    StatLabel = ResolveStatOrLabel(ed),
                    KindLabel = KindLabel(ed.kind),
                    ValuePct = scaled,
                    IsReduction = ed.isReduction,
                });
            }
        }

        /// <summary>当前 pawn 在 primer 上的等级（未学=0）。</summary>
        private static int CurrentPrimerLevel(Pawn pawn, ProfessionalSkillDef primer)
        {
            if (pawn == null || primer == null) return 0;
            List<ProfessionalSkillData> skills = ProfessionalEffectService.GetPrimaryDirectionSkills(pawn);
            if (skills != null)
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    if (skills[i] != null && skills[i].skillDefName == primer.defName)
                        return skills[i].level;
                }
            }
            return 0;
        }

        /// <summary>已解锁评级档位数（minLevel ≤ 当前等级），上限 4。未学=0。</summary>
        private static int CountUnlockedTiers(int currentLevel)
        {
            if (currentLevel < 1) return 0;
            List<ProfessionalRatingDef> ratings = DefDatabase<ProfessionalRatingDef>.AllDefsListForReading;
            int cnt = 0;
            if (ratings != null)
            {
                for (int i = 0; i < ratings.Count; i++)
                {
                    ProfessionalRatingDef rd = ratings[i];
                    if (rd != null && rd.minLevel <= currentLevel) cnt++;
                }
            }
            return cnt > 4 ? 4 : cnt;
        }

        /// <summary>取 minLevel 最高的评级档（大师档），作为主修加成满级口径基准。</summary>
        private static ProfessionalRatingDef GetMasterRating()
        {
            List<ProfessionalRatingDef> ratings = DefDatabase<ProfessionalRatingDef>.AllDefsListForReading;
            if (ratings == null || ratings.Count == 0) return null;
            ProfessionalRatingDef best = null;
            for (int i = 0; i < ratings.Count; i++)
            {
                ProfessionalRatingDef rd = ratings[i];
                if (rd == null) continue;
                if (best == null || rd.minLevel > best.minLevel) best = rd;
            }
            return best;
        }

        /// <summary>技能级效果覆盖查询（与 Resolver.FindOverride 同口径）。</summary>
        private static EffectOverride FindOverrideInPrimer(ProfessionalSkillDef primer, string effectDefName)
        {
            if (primer == null || primer.effectOverrides == null) return null;
            for (int i = 0; i < primer.effectOverrides.Count; i++)
            {
                EffectOverride ov = primer.effectOverrides[i];
                if (ov != null && ov.effectDefName == effectDefName) return ov;
            }
            return null;
        }

        /// <summary>主修加成展示名：StatDef.LabelCap 优先，其次 labelKey 翻译，再回退 defName。</summary>
        private static string ResolveStatOrLabel(ProfessionalEffectDef ed)
        {
            if (ed == null) return "?";
            if (!string.IsNullOrEmpty(ed.statDefName))
            {
                try
                {
                    StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(ed.statDefName);
                    if (stat != null && !string.IsNullOrEmpty(stat.LabelCap)) return stat.LabelCap;
                }
                catch { }
            }
            if (!string.IsNullOrEmpty(ed.labelKey))
            {
                try
                {
                    string t = ed.labelKey.Translate().ToString();
                    if (!string.IsNullOrEmpty(t) && t != ed.labelKey) return t;
                }
                catch { }
            }
            return ed.defName ?? "?";
        }

        /// <summary>
        /// 效果分类标签（UI 用）。
        /// ⚠ 2026-09-15（审计 LOC-001 修复）：原为**硬编码中文**（`"速度"/"品质"/"经验"/"通用"`）
        /// ⇒ 非中英语种下不可翻译且无法审计。现走 Keyed（`…Professional.Kind.*`，中英各 4 键），
        /// 译文可整体替换（GOV-010）。
        /// </summary>
        private static string KindLabel(ProfessionalEffectKind kind)
        {
            switch (kind)
            {
                case ProfessionalEffectKind.WorkSpeed:
                    return "PersonalChronicle.UI.Professional.Kind.WorkSpeed".Translate().ToString();
                case ProfessionalEffectKind.QualityBias:
                    return "PersonalChronicle.UI.Professional.Kind.QualityBias".Translate().ToString();
                case ProfessionalEffectKind.XpBonus:
                    return "PersonalChronicle.UI.Professional.Kind.XpBonus".Translate().ToString();
                default:
                    return "PersonalChronicle.UI.Professional.Kind.Generic".Translate().ToString();
            }
        }

        private static string RatingLabel(ProfessionalRatingDef rd)
        {
            if (rd == null) return "";
            string key = "PersonalChronicle.Professional.Rating." + rd.defName + ".Label";
            try
            {
                if (key.CanTranslate())
                {
                    string t = key.Translate().ToString();
                    if (!string.IsNullOrEmpty(t) && t != key) return t;
                }
            }
            catch { /* 翻译异常 → 回退 defName */ }
            return rd.defName;
        }

        private static string SkillLabel(SkillDef sd, string fallback)
        {
            if (sd == null) return fallback;
            try
            {
                string cap = sd.LabelCap;
                if (!string.IsNullOrEmpty(cap)) return cap;
            }
            catch { /* Label 抛错 → 回退 defName */ }
            return sd.defName ?? fallback;
        }

        private static string WorkTypeLabel(WorkTypeDef wt, string fallback)
        {
            if (wt == null) return fallback;
            try
            {
                string cap = wt.LabelCap;
                if (!string.IsNullOrEmpty(cap)) return cap;
            }
            catch { /* 同上 */ }
            return wt.defName ?? fallback;
        }

        private static string RecipeLabel(RecipeDef rd, string fallback)
        {
            if (rd == null) return fallback;
            try
            {
                string cap = rd.LabelCap;
                if (!string.IsNullOrEmpty(cap)) return cap;
            }
            catch { /* 同上 */ }
            return rd.defName ?? fallback;
        }
    }
}
