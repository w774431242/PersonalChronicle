using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Profession;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// 总览数据面板 · 字段注册表（玩家可自定义选择显示）。
    /// 所有字段均为"具体派生数值"（非原始技能列表），取数来自：
    ///   - 原生 StatDef（pawn.GetStatValue）
    ///   - 本 mod 派生（PawnObject.CareerData / HealthValuation / CareerOverviewView）
    ///   - pawn.needs 直读
    /// 值通过 GetValue 委托实时取得；BarMax>0 时绘制比例条。
    ///
    /// partial 划分（铁律 6.3）：
    ///   本文件        —— 字段定义 + 来源分组 + 取值/格式化/分解入口
    ///   .Scan.cs      —— 启动期自动扫描（排重集 / 分类白名单 / StatDef+NeedDef 扫描）
    /// </summary>
    public sealed partial class OverviewFieldRegistry
    {
        /// <summary>约定来源 Id：原版 RimWorld 属性。</summary>
        public const string SourceVanilla = "vanilla";
        /// <summary>约定来源 Id：本 Mod（PersonalChronicle）。运行时由自身 PackageId 填充。</summary>
        public static readonly string SourceSelf =
            ModLister.GetActiveModWithIdentifier("colony.integratedmanagement")?.PackageId
            ?? "colony.integratedmanagement";

        public readonly string Key;
        public readonly string LabelKey;     // 翻译键；自动扫描注册的字段为 null
        public readonly string FallbackLabel; // LabelKey 为 null 时的兜底文本快照（兼容旧路径；新路径用 labelResolver）
        public readonly string SourceId;     // 来源 Id（vanilla / 本Mod PackageId / 第三方 Mod PackageId）
        public readonly float BarMax;        // >0 表示绘制比例条（按值/BarMax）
        public readonly Func<Pawn, PawnObject, CareerOverviewView, float?> GetValue;
        public readonly string Unit;         // 单位后缀（如 "×"," kg","%"," 格/s"）；空=无
        public readonly bool IsDefault;      // 默认核心字段
        public readonly StatDef StatDefRef;  // 原生 StatDef 引用（仅 vanilla stat 字段有值，用于百分比格式化+悬停提示）

        /// <summary>
        /// 实时标签解析器（延迟求值）。
        /// 必要性：自动扫描在 [StaticConstructorOnStartup] 只跑一次，若此刻把 Def.LabelCap
        /// 快照成字符串，玩家在游戏中切换语言（Options → Language）后 Def.label 虽被
        /// DefInjected 重新注入，快照却仍是旧语言 → 标签不跟随。
        /// 故扫描时只存委托，每次显示时才求值，天然跟随当前语言。
        /// </summary>
        private readonly Func<string> labelResolver;

        /// <summary>
        /// 派生字段（无原生 StatDef）的「透明结构」解析器。
        /// 原版 StatPart 管线只覆盖原生 stat；本 mod 派生数值（如职业加成）用它单独构建分解，
        /// 使浮层渲染路径与原生字段完全一致（StatBreakdown → 同一套绘制）。
        /// </summary>
        private readonly Func<Pawn, StatBreakdown> breakdownResolver;

        private OverviewFieldRegistry(string key, string labelKey, string fallbackLabel, string sourceId, float barMax,
            Func<Pawn, PawnObject, CareerOverviewView, float?> getter, string unit, bool isDefault,
            StatDef statDef = null, Func<string> labelResolver = null, Func<Pawn, StatBreakdown> breakdown = null)
        {
            Key = key; LabelKey = labelKey; FallbackLabel = fallbackLabel ?? ""; SourceId = sourceId; BarMax = barMax;
            GetValue = getter; Unit = unit; IsDefault = isDefault; StatDefRef = statDef;
            this.labelResolver = labelResolver;
            this.breakdownResolver = breakdown;
        }

        /// <summary>该字段是否有可展开的悬停浮层（原生 stat 或派生解析器任一可用）。</summary>
        public bool HasTooltip => StatDefRef != null || breakdownResolver != null;

        /// <summary>
        /// 约定式「补充翻译」键：<c>PersonalChronicle.FieldLabel.stat.&lt;defName&gt;</c>
        /// / <c>PersonalChronicle.FieldLabel.need.&lt;defName&gt;</c>（Key 形如 "stat:xxx" / "need:xxx"）。
        /// 用途：第三方 mod 若未提供 DefInjected 翻译，其字段会显示原语言。
        /// 本 mod 或玩家可在 Languages/&lt;lang&gt;/Keyed/ 下按此约定补一条翻译即可覆盖，
        /// 无需修改原 mod，也不影响该 mod 自带翻译（仅在自带翻译缺失时生效）。
        /// </summary>
        private string LabelOverrideKey
        {
            get
            {
                if (string.IsNullOrEmpty(Key)) return null;
                int i = Key.IndexOf(':');
                if (i <= 0 || i == Key.Length - 1) return null;
                return "PersonalChronicle.FieldLabel." + Key.Substring(0, i) + "." + Key.Substring(i + 1);
            }
        }

        // 取 PawnObject（含 CareerData），失败返回 null
        private static PawnObject Po(Pawn p) => ChroniclePawnAccess.GetArchivedPawnObject(p);

        private static HealthValuationResult EvalHealth(Pawn p)
        {
            if (p == null) return null;
            HealthValuationPolicyDef policy = DefDatabase<HealthValuationPolicyDef>.GetNamedSilentFail(
                HealthValuationPolicyDef.DefaultPolicyDefName);
            if (policy == null) return null;
            return HealthValuationEvaluator.Evaluate(p, policy);
        }

        // 用 DefDatabase 安全取 StatDef，绕开 StatDefOf 在不同 RimWorld 版本的生成差异
        private static StatDef StatNamed(string defName)
            => DefDatabase<StatDef>.GetNamedSilentFail(defName);

        // 安全取 Stat 值：StatDef 不存在（版本/DLC 差异）或对该 pawn 被禁用时返回 null，
        // 避免 GetStatValue 对 disabled stat 抛 "Attempted to calculate value for disabled stat" 一致性报错。
        private static float? StatVal(Pawn p, string defName)
        {
            if (p == null) return null;
            var s = StatNamed(defName);
            return SafeStat(p, s);
        }

        // 直接传入 StatDef（如 StatDefOf.*）的安全取数；禁用/缺失返回 null。
        private static float? StatValOf(Pawn p, StatDef s)
        {
            if (p == null) return null;
            return SafeStat(p, s);
        }

        private static float? SafeStat(Pawn p, StatDef s)
        {
            if (s == null || p == null) return null;
            try
            {
                StatWorker w = s.Worker;
                if (w == null) return null;
                bool disabled = false;
                try { disabled = w.IsDisabledFor(p); } catch { disabled = false; }
                if (disabled)
                {
                    // 智力禁用等会让 ResearchSpeed 变成「—」；仍给出未禁用前的理论值，避免字段看起来没接上。
                    try { return w.GetValueUnfinalized(StatRequest.For(p), true); }
                    catch { return s.defaultBaseValue; }
                }
                return p.GetStatValue(s);
            }
            catch
            {
                return s.defaultBaseValue;
            }
        }

        // 当前绘制期的最佳适配度结果（由 DrawAttributePanel 注入，避免静态重复计算）
        public static List<ProfessionalFitResult> CurrentFitResults;

        private static readonly List<OverviewFieldRegistry> All = new List<OverviewFieldRegistry>
        {
            // ════════════ 原版（Vanilla）：RimWorld 原生 StatDef / Need ════════════
            // ───────── 工作效率 ─────────
            F("work.global", "PersonalChronicle.UI.OvF.WorkGlobal", SourceVanilla, 2f,
                (p,o,v) => StatValOf(p, StatDefOf.WorkSpeedGlobal), "×", true, StatDefOf.WorkSpeedGlobal),
            F("work.construct", "PersonalChronicle.UI.OvF.Construct", SourceVanilla, 2f,
                (p,o,v) => StatValOf(p, StatDefOf.ConstructionSpeed), "×", false, StatDefOf.ConstructionSpeed),
            F("work.plant", "PersonalChronicle.UI.OvF.Plant", SourceVanilla, 2f,
                (p,o,v) => StatValOf(p, StatDefOf.PlantWorkSpeed), "×", false, StatDefOf.PlantWorkSpeed),
            F("work.research", "PersonalChronicle.UI.OvF.Research", SourceVanilla, 2f,
                (p,o,v) => StatVal(p, "ResearchSpeed"), "×", true, StatNamed("ResearchSpeed")),
            F("work.mining", "PersonalChronicle.UI.OvF.Mining", SourceVanilla, 2f,
                (p,o,v) => StatValOf(p, StatDefOf.MiningSpeed), "×", false, StatDefOf.MiningSpeed),
            F("work.cook", "PersonalChronicle.UI.OvF.Cook", SourceVanilla, 2f,
                (p,o,v) => StatVal(p, "CookSpeed"), "×", false, StatNamed("CookSpeed")),
            F("work.smith", "PersonalChronicle.UI.OvF.Smith", SourceVanilla, 2f,
                (p,o,v) => StatVal(p, "SmeltingSpeed"), "×", false, StatNamed("SmeltingSpeed")),
            F("work.tailor", "PersonalChronicle.UI.OvF.Tailor", SourceVanilla, 2f,
                (p,o,v) => StatVal(p, "SmoothingSpeed"), "×", false, StatNamed("SmoothingSpeed")),
            F("work.art", "PersonalChronicle.UI.OvF.Art", SourceVanilla, 2f,
                (p,o,v) => StatVal(p, "ReadingSpeed"), "×", false, StatNamed("ReadingSpeed")),

            // ───────── 移动与体能 ─────────
            F("move.speed", "PersonalChronicle.UI.OvF.MoveSpeed", SourceVanilla, 10f,
                (p,o,v) => StatValOf(p, StatDefOf.MoveSpeed), " 格/s", true, StatDefOf.MoveSpeed),
            // 注意：1.6 真实 defName 是 CarryingCapacity（带 ing），不是 CarryCapacity
            F("move.carry", "PersonalChronicle.UI.OvF.Carry", SourceVanilla, 80f,
                (p,o,v) => StatVal(p, "CarryingCapacity"), " kg", true, StatNamed("CarryingCapacity")),
            F("body.health", "PersonalChronicle.UI.OvF.HealthScore", SourceVanilla, 100f,
                (p,o,v) => { var hv = EvalHealth(p); return hv != null ? (float?)Mathf.Round(hv.HealthScore) : null; }, "", true),
            F("body.age", "PersonalChronicle.UI.OvF.Age", SourceVanilla, 100f,
                (p,o,v) => { var hv = EvalHealth(p); return hv != null ? (float?)hv.AgeYears : null; }, "岁", false),
            F("body.silver", "PersonalChronicle.UI.OvF.Silver", SourceVanilla, 5000f,
                (p,o,v) => { var hv = EvalHealth(p); return hv != null ? (float?)Mathf.Round(hv.SilverValue) : null; }, "", false),

            // ───────── 战斗生存 ─────────
            F("combat.meleeDps", "PersonalChronicle.UI.OvF.MeleeDps", SourceVanilla, 30f,
                (p,o,v) => StatValOf(p, StatDefOf.MeleeDPS), "", false, StatDefOf.MeleeDPS),
            F("combat.accuracy", "PersonalChronicle.UI.OvF.Accuracy", SourceVanilla, 12f,
                (p,o,v) => StatVal(p, "ShootingAccuracyPawn"), " 格", false, StatNamed("ShootingAccuracyPawn")),
            F("combat.armorSharp", "PersonalChronicle.UI.OvF.ArmorSharp", SourceVanilla, 1f,
                (p,o,v) => StatValOf(p, StatDefOf.ArmorRating_Sharp), "%", false, StatDefOf.ArmorRating_Sharp),
            F("combat.armorBlunt", "PersonalChronicle.UI.OvF.ArmorBlunt", SourceVanilla, 1f,
                (p,o,v) => StatValOf(p, StatDefOf.ArmorRating_Blunt), "%", false, StatDefOf.ArmorRating_Blunt),
            F("combat.dodge", "PersonalChronicle.UI.OvF.Dodge", SourceVanilla, 1f,
                (p,o,v) => StatVal(p, "MeleeDodgeChance"), "%", false, StatNamed("MeleeDodgeChance")),
            F("combat.painShock", "PersonalChronicle.UI.OvF.PainShock", SourceVanilla, 1f,
                (p,o,v) => StatValOf(p, StatDefOf.PainShockThreshold), "%", false, StatDefOf.PainShockThreshold),

            // ───────── 社交心智 ─────────
            F("social.impact", "PersonalChronicle.UI.OvF.SocialImpact", SourceVanilla, 2f,
                (p,o,v) => StatValOf(p, StatDefOf.SocialImpact), "×", false, StatDefOf.SocialImpact),
            F("social.trade", "PersonalChronicle.UI.OvF.Trade", SourceVanilla, 1f,
                (p,o,v) => StatValOf(p, StatDefOf.TradePriceImprovement), "%", false, StatDefOf.TradePriceImprovement),
            F("social.negotiate", "PersonalChronicle.UI.OvF.Negotiate", SourceVanilla, 2f,
                (p,o,v) => StatValOf(p, StatDefOf.NegotiationAbility), "×", false, StatDefOf.NegotiationAbility),
            F("social.medQuality", "PersonalChronicle.UI.OvF.MedQuality", SourceVanilla, 2f,
                (p,o,v) => StatValOf(p, StatDefOf.MedicalTendQuality), "×", false, StatDefOf.MedicalTendQuality),
            F("social.surgery", "PersonalChronicle.UI.OvF.Surgery", SourceVanilla, 1f,
                (p,o,v) => StatValOf(p, StatDefOf.MedicalSurgerySuccessChance), "%", false, StatDefOf.MedicalSurgerySuccessChance),
            F("social.diseaseRes", "PersonalChronicle.UI.OvF.DiseaseRes", SourceVanilla, 2f,
                (p,o,v) => StatVal(p, "ImmunityGainSpeed"), "×", false, StatNamed("ImmunityGainSpeed")),

            // ───────── 需求情绪（原版 Need） ─────────
            F("need.mood", "PersonalChronicle.UI.OvF.Mood", SourceVanilla, 1f,
                (p,o,v) => p?.needs?.mood != null ? (float?)p.needs.mood.CurLevelPercentage : null, "%", true),
            F("need.rest", "PersonalChronicle.UI.OvF.Rest", SourceVanilla, 1f,
                (p,o,v) => p?.needs?.rest != null ? (float?)p.needs.rest.CurLevelPercentage : null, "%", true),
            F("need.food", "PersonalChronicle.UI.OvF.Food", SourceVanilla, 1f,
                (p,o,v) => p?.needs?.food != null ? (float?)p.needs.food.CurLevelPercentage : null, "%", false),
            F("need.joy", "PersonalChronicle.UI.OvF.Joy", SourceVanilla, 1f,
                (p,o,v) => p?.needs?.joy != null ? (float?)p.needs.joy.CurLevelPercentage : null, "%", false),

            // ════════════ 本 Mod（PersonalChronicle）：编年史累计派生数据 ════════════
            F("mod.hours", "PersonalChronicle.UI.OvF.Hours", SourceSelf, 5000f,
                (p,o,v) => v != null ? (float?)(v.HoursText != null ? ParseHours(v.HoursText) : 0f) : null, "h", true),
            F("mod.made", "PersonalChronicle.UI.OvF.Made", SourceSelf, 1000f,
                (p,o,v) => v != null ? (float?)v.Made : null, " 件", true),
            F("mod.books", "PersonalChronicle.UI.OvF.Books", SourceSelf, 50f,
                (p,o,v) => v != null ? (float?)v.Books : null, " 本", true),
            F("mod.built", "PersonalChronicle.UI.OvF.Built", SourceSelf, 500f,
                (p,o,v) => v != null ? (float?)v.Built : null, " 处", false),
            F("mod.researched", "PersonalChronicle.UI.OvF.Researched", SourceSelf, 200f,
                (p,o,v) => v != null ? (float?)v.Researched : null, " 项", false),
            // 专业等级：数值=主方向技能等级；悬停浮层展开「职业数值加成」分解
            // （逐技能明细 + 当前生效合计 + 理论上限，见 StatBreakdownBuilder.BuildProfessionalBonus）。
            F("mod.proLevel", "PersonalChronicle.UI.OvF.ProLevel", SourceSelf, 50f,
                (p,o,v) => ProLevelValue(p), " Lv", true, null,
                (p) => StatBreakdownBuilder.BuildProfessionalBonus(p, ProLevelTooltipTitle())),
            F("mod.fit", "PersonalChronicle.UI.OvF.Fit", SourceSelf, 100f,
                (p,o,v) => { var fits = CurrentFitResults;
                    return (fits != null && fits.Count > 0) ? (float?)fits[0].Fit : null; }, " %", false),

            // ════════════ 其他 Mod（自动按来源成组） ════════════
            // 第三方 mod 通过 OverviewFieldRegistry.RegisterExternal(...) 注册字段，
            // 传入各自 PackageId 作为 sourceId，对话框自动按其来源分组并钻取。
        };

        private static OverviewFieldRegistry F(string key, string labelKey, string sourceId, float barMax,
            Func<Pawn, PawnObject, CareerOverviewView, float?> getter, string unit, bool isDefault, StatDef statDef = null)
            => new OverviewFieldRegistry(key, labelKey, null, sourceId, barMax, getter, unit, isDefault, statDef);

        /// <summary>重载：为派生字段附加「透明结构」解析器（无原生 StatDef 也能展开浮层）。</summary>
        private static OverviewFieldRegistry F(string key, string labelKey, string sourceId, float barMax,
            Func<Pawn, PawnObject, CareerOverviewView, float?> getter, string unit, bool isDefault, StatDef statDef,
            Func<Pawn, StatBreakdown> breakdown)
            => new OverviewFieldRegistry(key, labelKey, null, sourceId, barMax, getter, unit, isDefault, statDef, null, breakdown);

        /// <summary>主方向专业等级：档案 XP 与原版 sourceSkills 取高；无主方向则取生效技能最高级。</summary>
        private static float? ProLevelValue(Pawn p)
        {
            if (p == null) return null;
            PawnObject po = Po(p);
            string pd = po?.CareerData?.Professional?.primaryDirection;
            string skillName = null;
            if (!string.IsNullOrEmpty(pd))
            {
                ProfessionalDirectionDef dir = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(pd);
                if (dir != null && dir.skillDefNames != null && dir.skillDefNames.Count > 0)
                {
                    skillName = dir.skillDefNames[0];
                }
                else
                {
                    ProfessionalSkillDef asSkill = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(pd);
                    if (asSkill != null) skillName = asSkill.defName;
                }
            }
            List<ProfessionalSkillData> skills = PersonalChronicle.Application.Effects.ProfessionalEffectService.GetEffectiveSkills(p);
            if (skills == null || skills.Count == 0) return 0f;
            if (!string.IsNullOrEmpty(skillName))
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    if (skills[i] != null && string.Equals(skills[i].skillDefName, skillName, StringComparison.Ordinal))
                    {
                        return skills[i].level;
                    }
                }
            }
            int best = 0;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] != null && skills[i].level > best) best = skills[i].level;
            }
            return best;
        }

        /// <summary>专业等级浮层标题（延迟求值，跟随语言切换）。</summary>
        private static string ProLevelTooltipTitle()
        {
            const string key = "PersonalChronicle.UI.OvF.ProLevel";
            try { return key.CanTranslate() ? key.Translate().ToString() : "Professional"; }
            catch { return "Professional"; }
        }

        /// <summary>第三方 Mod 注册自定义属性字段。sourceId 传各自 PackageId，对话框自动按来源成组并支持钻取。</summary>
        public static void RegisterExternal(string key, string labelKey, string sourceId,
            Func<Pawn, PawnObject, CareerOverviewView, float?> getter, string unit = "", float barMax = 0f, bool isDefault = false)
        {
            RegisterExternal(key, labelKey, (string)null, sourceId, getter, unit, barMax, isDefault);
        }

        /// <summary>重载：允许直接传入 fallbackLabel（无翻译键时显示原语言 Def.LabelCap）。</summary>
        public static void RegisterExternal(string key, string labelKey, string fallbackLabel, string sourceId,
            Func<Pawn, PawnObject, CareerOverviewView, float?> getter, string unit = "", float barMax = 0f, bool isDefault = false)
        {
            if (string.IsNullOrEmpty(sourceId)) sourceId = SourceVanilla;
            if (ByKey(key) == null)
                All.Add(new OverviewFieldRegistry(key, labelKey, fallbackLabel, sourceId, barMax, getter, unit, isDefault));
        }

        /// <summary>重载：允许传入 StatDef 引用（用于百分比格式化+悬停提示）。</summary>
        public static void RegisterExternal(string key, string labelKey, string fallbackLabel, string sourceId,
            Func<Pawn, PawnObject, CareerOverviewView, float?> getter, string unit, float barMax, bool isDefault, StatDef statDef)
        {
            if (string.IsNullOrEmpty(sourceId)) sourceId = SourceVanilla;
            if (ByKey(key) == null)
                All.Add(new OverviewFieldRegistry(key, labelKey, fallbackLabel, sourceId, barMax, getter, unit, isDefault, statDef));
        }

        /// <summary>
        /// 重载：传入实时标签解析器 + StatDef 引用（自动扫描专用）。
        /// <paramref name="labelResolver"/> 每次显示时求值，玩家切换语言后标签自动跟随当前语言。
        /// </summary>
        public static void RegisterExternal(string key, string labelKey, Func<string> labelResolver, string sourceId,
            Func<Pawn, PawnObject, CareerOverviewView, float?> getter, string unit, float barMax, bool isDefault, StatDef statDef)
        {
            if (string.IsNullOrEmpty(sourceId)) sourceId = SourceVanilla;
            if (ByKey(key) != null) return;
            // 兜底快照仍保留一份（启动时求值一次），仅当 resolver 缺失/抛错时使用
            string fallback = null;
            try { fallback = labelResolver?.Invoke(); } catch { }
            All.Add(new OverviewFieldRegistry(key, labelKey, fallback, sourceId, barMax, getter, unit, isDefault, statDef, labelResolver));
        }

        /// <summary>重载：实时标签解析器（无 StatDef，NeedDef 等走此路径）。</summary>
        public static void RegisterExternal(string key, string labelKey, Func<string> labelResolver, string sourceId,
            Func<Pawn, PawnObject, CareerOverviewView, float?> getter, string unit = "", float barMax = 0f, bool isDefault = false)
        {
            RegisterExternal(key, labelKey, labelResolver, sourceId, getter, unit, barMax, isDefault, null);
        }

        private static float ParseHours(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            // HoursText 形如 "515 h" / "1.2k h"
            string num = s.Replace("h", "").Replace(" ", "").Trim();
            if (num.EndsWith("k")) { float.TryParse(num.Substring(0, num.Length - 1), out float v); return v * 1000f; }
            float.TryParse(num, out float r); return r;
        }

        /// <summary>
        /// 展示标签（四级优先链，全部实时求值以跟随语言切换）：
        ///   1. <see cref="LabelKey"/>          —— 本 Mod 硬编码精选字段的显式翻译键
        ///   2. <see cref="LabelOverrideKey"/>  —— 约定式补充翻译键（补第三方 mod 缺失的翻译）
        ///   3. <see cref="labelResolver"/>     —— 实时取 Def.LabelCap（原生 DefInjected，切语言自动跟随）
        ///   4. <see cref="FallbackLabel"/>     —— 兜底快照（旧注册路径兼容）
        /// </summary>
        public string DisplayLabel
        {
            get
            {
                string t = TryTranslate(LabelKey);
                if (t != null) return t;

                t = TryTranslate(LabelOverrideKey);
                if (t != null) return t;

                if (labelResolver != null)
                {
                    try
                    {
                        string r = labelResolver();
                        if (!string.IsNullOrEmpty(r)) return r;
                    }
                    catch { /* Def 已卸载等极端情况 → 落到兜底 */ }
                }
                return FallbackLabel ?? "";
            }
        }

        /// <summary>安全翻译：键为空 / 不存在翻译 / 翻译异常时返回 null，由调用方降级。</summary>
        private static string TryTranslate(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            try
            {
                if (!key.CanTranslate()) return null;
                string s = key.Translate().ToString();
                return string.IsNullOrEmpty(s) ? null : s;
            }
            catch { return null; }
        }

        /// <summary>
        /// 格式化数值为原生百分比风格（与 RimWorld 原生属性面板一致）。
        /// 有 StatDefRef 时使用 StatWorker.ValueToString（输出 "92%" / "140%" / "27.15" 等）；
        /// 无 StatDefRef（mod 派生/Need）时回退到原始 "F2 + Unit" 格式。
        /// </summary>
        public string GetFormattedValue(Pawn pawn, float? rawValue)
        {
            if (!rawValue.HasValue) return "—";
            if (StatDefRef != null)
            {
                try
                {
                    ToStringNumberSense sense = StatDefRef.toStringNumberSense;
                    return StatDefRef.Worker.ValueToString(rawValue.Value, true, sense);
                }
                catch { /* 回退 */ }
            }
            float v = rawValue.Value;
            if (Math.Abs(v - Math.Round(v)) < 0.05f)
            {
                return Math.Round(v).ToString("0") + (Unit ?? "");
            }
            return v.ToString("F1").TrimEnd('0').TrimEnd('.') + (Unit ?? "");
        }

        /// <summary>
        /// 构建「透明结构」分解模型（基础值 → 各 StatPart 贡献 → 最终值）。
        /// 实现下沉到 <see cref="StatBreakdownBuilder"/>（铁律 6.4：子功能独立成文件）。
        /// 非 stat 字段（派生/Need/mod 累计）返回 null。
        /// </summary>
        public StatBreakdown BuildBreakdown(Pawn pawn)
        {
            if (StatDefRef != null)
            {
                return StatBreakdownBuilder.Build(StatDefRef, pawn, DisplayLabel);
            }
            // 派生字段：走自定义解析器（无解析器 → null，与旧行为一致）
            try { return breakdownResolver != null ? breakdownResolver(pawn) : null; }
            catch { return null; }
        }

        /// <summary>纯文本兜底：无分解数据时返回空串。</summary>
        public string GetTooltip(Pawn pawn)
        {
            var bd = BuildBreakdown(pawn);
            return bd == null ? "" : bd.ToPlainText();
        }

        public static IReadOnlyList<OverviewFieldRegistry> AllFields => All;
        public static OverviewFieldRegistry ByKey(string key)
            => All.Find(f => f.Key == key);

        /// <summary>完全手动模式：默认不展示任何属性字段，全部由玩家在对话框中勾选添加。</summary>
        public static HashSet<string> DefaultKeys
        {
            get
            {
                // 专业等级默认可见：悬停即展开职业数值加成，避免空面板让人以为加成没生效。
                return new HashSet<string> { "mod.proLevel" };
            }
        }

        /// <summary>取得玩家当前生效的字段 key 集合。空集合 = 玩家未添加任何字段（完全手动）。</summary>
        public static HashSet<string> ActiveKeys(HashSet<string> saved)
            => (saved != null && saved.Count > 0) ? saved : new HashSet<string>();

        /// <summary>来源展示顺序：原版 → 本 Mod → 其余按 PackageId 字母序。</summary>
        public static IReadOnlyList<string> SourceOrder
        {
            get
            {
                var set = new HashSet<string>();
                foreach (var f in All) set.Add(f.SourceId);
                var list = set.ToList();
                list.Sort((a, b) =>
                {
                    int ra = a == SourceVanilla ? 0 : (a == SourceSelf ? 1 : 2);
                    int rb = b == SourceVanilla ? 0 : (b == SourceSelf ? 1 : 2);
                    if (ra != rb) return ra.CompareTo(rb);
                    return a.CompareTo(b);
                });
                return list;
            }
        }

        public static IReadOnlyList<OverviewFieldRegistry> FieldsOfSource(string sourceId)
            => All.FindAll(f => f.SourceId == sourceId);

        /// <summary>来源可读名：vanilla→原版属性；本Mod→"本 Mod 属性"；其他→尝试 mod 名，失败回退 PackageId。</summary>
        public static string SourceLabel(string sourceId)
        {
            if (sourceId == SourceVanilla) return "PersonalChronicle.UI.OvF.Group.Vanilla".Translate().ToString();
            if (sourceId == SourceSelf) return "PersonalChronicle.UI.OvF.Group.ModSelf".Translate().ToString();
            var mod = ModLister.GetActiveModWithIdentifier(sourceId);
            if (mod != null) return mod.Name;
            return sourceId;
        }

        // ════════════ 启动期自动扫描（第三方 Mod 贡献的 StatDef / NeedDef） ════════════
        // 实现在 OverviewFieldRegistry.Scan.cs（铁律 6.2/6.3：扫描是独立内聚区块，已拆 partial）：
        //   ScanExternal / ScanStatDefs / ScanNeedDefs / IsPawnStat / ResolveSourceId
        //   KnownVanillaStatDefs / KnownVanillaNeedDefs / PawnStatCategories / ExtraPawnStatDefs
    }
}
