// 属性字段「透明结构」分解模型与构建器（铁律 6.4：子功能独立成文件，不往注册表里堆）。
//
// 职责边界：
//   · BreakdownRow / StatBreakdown —— 纯数据模型（无引擎依赖，可离线单测）
//   · StatBreakdownBuilder         —— 走原版 StatPart 管线派生结构化分解（static 工具类，铁律 6.4）
// 数值与原版 100% 一致：用的是原版自己的 StatPart 管线，只是把结果结构化，
// 交给 UI 绘制成带分级缩进 / 配色 / 比例条的可视化浮层，而非一坨原版纯文本。
using System;
using System.Collections.Generic;
using PersonalChronicle.Application.Effects;
using PersonalChronicle.Application.ReadModels;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// 透明结构的一行：某个 StatPart 把值从 <see cref="Before"/> 改成 <see cref="After"/>。
    /// 乘性因子 = After/Before；加性 offset 型 part 也用同一比值表达，UI 统一显示。
    /// </summary>
    public struct BreakdownRow
    {
        public string Label;      // part 的可读说明（原版 ExplanationPart，已本地化）
        /// <summary>所属分组名（已本地化，如「技能」/「健康」）；空 = 顶层条目，不归入任何分组。</summary>
        public string Group;
        /// <summary>层级缩进（原版 ExplanationPart 前导空格反映的父子关系，如 Isekai Level 是 Isekai INT 子项）。</summary>
        public int IndentLevel;
        public float Before;
        public float After;
        public float Factor;      // After / Before
        public bool IsBuff;       // 因子 > 1（增益）
        public bool IsDebuff;     // 因子 < 1（减益）
        public float BarShare;    // 0~1，供比例条绘制（因子按 0~2 归一）

        public static BreakdownRow From(string label, float before, float after, string group = null, int indentLevel = 0)
        {
            float f = Math.Abs(before) > 1e-5f ? after / before : 1f;
            return new BreakdownRow
            {
                Label = label,
                Group = group,
                IndentLevel = indentLevel,
                Before = before,
                After = after,
                Factor = f,
                IsBuff = f > 1.0001f,
                IsDebuff = f < 0.9999f,
                // 0~2 映射到 0~1，钳制避免极端值撑爆条
                BarShare = Mathf.Clamp01(f * 0.5f),
            };
        }

        /// <summary>"×0.92" / "×1.15"（乘性因子文本）。</summary>
        public string FactorText => "×" + Factor.ToString("F2");

        /// <summary>"-8%" / "+15%"（相对增益）。</summary>
        public string DeltaText
        {
            get
            {
                float d = (Factor - 1f) * 100f;
                string sign = d > 0f ? "+" : "";
                return sign + d.ToString("F0") + "%";
            }
        }
    }

    /// <summary>
    /// 单个原生属性字段的完整「透明结构」分解：基础值 → 各 part 贡献 → 最终值。
    /// 由 <see cref="StatBreakdownBuilder"/> 从原版 StatPart 管线实时派生。
    /// </summary>
    public sealed class StatBreakdown
    {
        public string Title;
        /// <summary>StatDef.description（如"做任何类型工作时的速度乘数。"），展示在浮层标题下方。</summary>
        public string Description;
        public float BaseRaw;
        public float FinalRaw;
        public string BaseText;
        public string FinalText;
        public List<BreakdownRow> Rows = new List<BreakdownRow>();
        public string FinalizeNote;   // 后处理说明（min/max/曲线），可为空
        /// <summary>不同分组的数量（UI 需为每个分组插入一行标题，高度计算要用）。</summary>
        public int GroupCount;

        public bool HasRows => Rows != null && Rows.Count > 0;

        /// <summary>纯文本兜底（无可视化面板时使用）。</summary>
        public string ToPlainText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Title);
            sb.AppendLine("Base: " + BaseText);
            if (HasRows)
                foreach (var r in Rows)
                    sb.AppendLine("  " + r.Label + " " + r.FactorText + " (" + r.DeltaText + ")");
            if (!string.IsNullOrEmpty(FinalizeNote)) sb.AppendLine(FinalizeNote);
            sb.Append("Final: " + FinalText);
            return sb.ToString();
        }
    }

    /// <summary>
    /// 分解构建器（static 工具类，铁律 6.4）。
    /// 管线：<c>GetBaseValueFor</c> → 遍历 <c>StatDef.parts</c> 逐个 <c>TransformValue</c>
    /// → <c>FinalizeValue</c> → <c>GetExplanationFinalizePart</c>。
    /// 每个 part 单独 try/catch，单 part 异常不影响整体；无 StatDef（派生/Need 字段）返回 null。
    /// </summary>
    public static class StatBreakdownBuilder
    {
        // ── 标签清洗（P1：ExplanationPart 返回原版内部格式，含数值/括号/英文技术文本，不能直接展示）──
        // 实机截图证据："Isekai INT (7): 1.08" / "Isekai level (10): 0.19" —— 原始文本直接暴露给玩家。
        // 清洗策略：取冒号/括号前的可读描述部分；去掉尾随数值；fallback 用 part 类型名。

        /// <summary>
        /// 从原始 ExplanationPart 文本中提取可读标签。
        /// 铁律 7.2：保留第一个括号内的参数（光照百分比、技能等级等），只剥离尾部的
        /// "乘数 / multiplier / factor" 等无信息词；失败时回退到 part 类型名。
        /// </summary>
        public static string CleanLabel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim();

            // 模式1: "Something (key) 乘数: 1.08" → 取 "Something (key)"
            int colon = s.IndexOf(':');
            if (colon > 0)
            {
                string before = s.Substring(0, colon).TrimEnd();
                before = StripMultiplierSuffix(before);
                return before;
            }

            // 模式2: "Something (key)" → 取 "Something (key)"
            int paren2 = s.LastIndexOf('(');
            if (paren2 > 0) return s.Substring(0, paren2).Trim();

            // 模式3: 无特殊格式，原样返回（但截断过长文本）
            return s.Length > 40 ? s.Substring(0, 40) + "…" : s;
        }

        /// <summary>剥离 ExplanationPart 中尾部的 "乘数 / multiplier / factor / 系数" 等无信息词。</summary>
        private static string StripMultiplierSuffix(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string t = s.TrimEnd();
            string[] suffixes = { "乘数", "multiplier", "factor", "系数" };
            foreach (var suffix in suffixes)
            {
                if (t.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    t = t.Substring(0, t.Length - suffix.Length).TrimEnd();
            }
            return t;
        }

        /// <summary>过滤后处理说明：空值 / 纯技术文本返回空（不在 UI 展示原始 FinalizeNote）。</summary>
        public static string CleanNote(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string s = raw.Trim();
            // 后处理说明通常是 "Post processed to X~Y" 或 "Clamped by stat def" 等技术文本
            // 对玩家无信息量，直接隐藏。如果未来需要展示可在此处做美化。
            return "";   // P4: 隐藏原始技术文本
        }

        /// <summary>
        /// 构建完整分解（原版四阶段管线，缺一不可）。
        ///
        /// 取证来源：Core/Defs/Stats/Stats_Pawns_WorkGeneral.xml 中 MiningSpeed 定义，
        /// 原版一条 pawn stat 的完整求值顺序为：
        ///   基础值 → ①statFactors(父stat) → ②skillNeedFactors(技能) → ③capacityFactors(健康)
        ///          → ④parts(StatPart) → FinalizeValue
        /// 旧实现只做了「基础值 + ④parts」，漏掉 ①②③，
        /// 实机表现为比原版面板少「全局工作速度 / 技能 / 健康」等若干行（已修复）。
        /// </summary>
        public static StatBreakdown Build(StatDef statDef, Pawn pawn, string title)
        {
            if (statDef == null || pawn == null) return null;
            try
            {
                StatWorker w = statDef.Worker;
                StatRequest req = StatRequest.For(pawn);

                float baseVal = w.GetBaseValueFor(req);
                float cur = baseVal;
                var rows = new List<BreakdownRow>();

                cur = ApplyStatFactors(statDef, pawn, cur, rows);
                cur = ApplySkillNeeds(statDef, pawn, cur, rows);
                cur = ApplyCapacityFactors(statDef, pawn, cur, rows);
                try
                {
                    // ⚠ 2026-09-15（审计 P0 修复）：旧管线已删除；UI 侧**不再**触发注入
                    // （注入职责归 `Data/ChronicleGameComponent` 的 reconcile 单一触发点，ARC-004）。
                    // 此处保留一次幂等重校验调用是为「Def reload 后 UI 立即分解」的场景兜底。
                    PersonalChronicle.Effects.ProfessionalEffectStatPartInjector.EnsureInjected();
                }
                catch { /* 注入失败不阻断分解 */ }
                cur = ApplyParts(statDef, req, cur, rows);
                cur = ApplyProfessionalBonus(statDef, pawn, cur, rows);

                float finalVal = cur;
                try { w.FinalizeValue(req, ref finalVal, true); } catch { }

                string note = "";
                try
                {
                    note = w.GetExplanationFinalizePart(req, ToStringNumberSense.Factor, finalVal) ?? "";
                    note = CleanNote(note);   // P4: 过滤后处理技术文本
                }
                catch { }

                return new StatBreakdown
                {
                    Title = title,
                    Description = statDef.description ?? "",
                    BaseRaw = baseVal,
                    FinalRaw = finalVal,
                    BaseText = w.ValueToString(baseVal, false, ToStringNumberSense.Factor),
                    FinalText = w.ValueToString(finalVal, true, ToStringNumberSense.Factor),
                    Rows = rows,
                    FinalizeNote = note,
                    GroupCount = CountGroups(rows),
                };
            }
            catch { return null; }
        }

        /// <summary>
        /// 职业主修常驻加成。StatPart 已注入时 cur 已含该因子，只补一行「职业」来源；
        /// 未注入时当场乘算并出列，避免属性面板看不到主修方向加成。
        /// </summary>
        private static float ApplyProfessionalBonus(StatDef statDef, Pawn pawn, float cur, List<BreakdownRow> rows)
        {
            if (statDef == null || pawn == null) return cur;
            float factor;
            try { factor = ProfessionalEffectService.GetGenericStatFactor(pawn, statDef.defName); }
            catch { return cur; }
            if (Mathf.Abs(factor - 1f) <= 0.0001f) return cur;

            bool already = false;
            if (statDef.parts != null)
            {
                for (int i = 0; i < statDef.parts.Count; i++)
                {
                    StatPart p = statDef.parts[i];
                    if (p == null) continue;
                    string n = p.GetType().Name;
                    if (n == "StatPart_ProfessionalStatBonus" || n == "StatPart_ProfessionalWorkSpeed")
                    {
                        already = true;
                        break;
                    }
                }
            }

            int pct = (int)((factor - 1f) * 100f);
            string key = pct >= 0
                ? "PersonalChronicle.ProfessionalEffect.StatBonus.StatPart"
                : "PersonalChronicle.ProfessionalEffect.StatBonus.StatPartReduction";
            string label = key.CanTranslate()
                ? key.Translate(Mathf.Abs(pct)).ToString()
                : ("Profession +" + pct + "%");
            string group = Tx("PersonalChronicle.UI.Career.Ov.Chosen", "Profession");

            if (already)
            {
                float before = Math.Abs(factor) > 1e-5f ? cur / factor : cur;
                rows.Add(BreakdownRow.From(label, before, cur, group));
                return cur;
            }
            float from = cur;
            cur *= factor;
            rows.Add(BreakdownRow.From(label, from, cur, group));
            return cur;
        }

        /// <summary>统计不同分组数（UI 为每个分组插一行标题，需据此增高）。</summary>
        private static int CountGroups(List<BreakdownRow> rows)
        {
            var set = new HashSet<string>();
            foreach (var r in rows)
                if (!string.IsNullOrEmpty(r.Group)) set.Add(r.Group);
            return set.Count;
        }

        /// <summary>① statFactors：引用其他 StatDef 作为因子（如 MiningSpeed ← WorkSpeedGlobal）。</summary>
        private static float ApplyStatFactors(StatDef statDef, Pawn pawn, float cur, List<BreakdownRow> rows)
        {
            if (statDef.statFactors == null) return cur;
            foreach (StatDef f in statDef.statFactors)
            {
                if (f == null) continue;
                float before = cur;
                float v;
                try { v = pawn.GetStatValue(f); } catch { continue; }
                cur = before * v;
                if (Mathf.Abs(cur - before) < 1e-5f) continue;
                rows.Add(BreakdownRow.From(SafeLabel(f.LabelCap, f.defName), before, cur));
            }
            return cur;
        }

        /// <summary>② skillNeedFactors：技能等级因子，归入原版「技能」分组。</summary>
        private static float ApplySkillNeeds(StatDef statDef, Pawn pawn, float cur, List<BreakdownRow> rows)
        {
            if (statDef.skillNeedFactors == null) return cur;
            string group = Tx("StatsReport_Skills", "Skills");
            foreach (SkillNeed sn in statDef.skillNeedFactors)
            {
                if (sn?.skill == null) continue;
                float before = cur;
                float factor;
                try { factor = sn.ValueFor(pawn); } catch { continue; }
                cur = before * factor;
                if (Mathf.Abs(cur - before) < 1e-5f) continue;
                int lvl = 0;
                try { lvl = pawn.skills?.GetSkill(sn.skill)?.Level ?? 0; } catch { }
                // 原版格式："采矿 (10)"
                rows.Add(BreakdownRow.From(
                    SafeLabel(sn.skill.LabelCap, sn.skill.defName) + " (" + lvl + ")", before, cur, group));
            }
            return cur;
        }

        /// <summary>
        /// ③ capacityFactors：身体部位能力因子，归入原版「健康」分组。
        /// 公式 <c>cur *= Pow(min(level, max), weight)</c> —— 已用实机「采矿速度」逐行对账：
        /// 1.0(基础) ×1.26(全局) ×1.24(技能10) ×2.0(操作^1.0) ×1.0(视觉^0.5,max1) ×1.12(Isekai STR)
        /// = 3.4998 ≈ 350%，与原版面板「最终值: 350%」完全吻合。
        /// </summary>
        private static float ApplyCapacityFactors(StatDef statDef, Pawn pawn, float cur, List<BreakdownRow> rows)
        {
            if (statDef.capacityFactors == null) return cur;
            string group = Tx("StatsReport_Health", "Health");
            foreach (PawnCapacityFactor cf in statDef.capacityFactors)
            {
                if (cf?.capacity == null) continue;
                float before = cur;
                float level;
                try { level = pawn.health?.capacities?.GetLevel(cf.capacity) ?? 1f; } catch { continue; }
                if (cf.max > 0f) level = Mathf.Min(level, cf.max);
                cur = before * Mathf.Pow(level, cf.weight);
                if (Mathf.Abs(cur - before) < 1e-5f) continue;
                rows.Add(BreakdownRow.From(
                    SafeLabel(cf.capacity.LabelCap, cf.capacity.defName) + CapacitySuffix(cf), before, cur, group));
            }
            return cur;
        }

        /// <summary>
        /// ④ parts：StatPart 管线（第三方 mod 主要扩展点，如 Isekai STR 因子）。
        /// </summary>
        /// <remarks>
        /// 铁律 7.1/7.2：所有返回 ExplanationPart 的 part 都必须生成行，
        /// 不得因 cur≈before 而省略；零贡献行显示为 ×1.00 / 0%，保留结构透明。
        /// 铁律 7.3：从 ExplanationPart 原文的前导空格提取缩进层级（原版 2 空格一级）。
        /// </remarks>
        private static float ApplyParts(StatDef statDef, StatRequest req, float cur, List<BreakdownRow> rows)
        {
            if (statDef.parts == null) return cur;
            foreach (StatPart part in statDef.parts)
            {
                if (part == null) continue;
                float before = cur;
                try { part.TransformValue(req, ref cur); }
                catch { cur = before; continue; }   // 单 part 异常不影响整体

                string raw = null;
                try { raw = part.ExplanationPart(req); } catch { }
                // 数值已变但 ExplanationPart 为空时仍出一行，避免职业加成在字段浮层里「消失」。
                if (string.IsNullOrWhiteSpace(raw))
                {
                    if (Math.Abs(cur - before) < 1e-5f) continue;
                    int pct = 0;
                    if (Math.Abs(before) > 1e-5f)
                    {
                        pct = (int)((cur / before - 1f) * 100f);
                    }
                    string key = pct >= 0
                        ? "PersonalChronicle.ProfessionalEffect.StatBonus.StatPart"
                        : "PersonalChronicle.ProfessionalEffect.StatBonus.StatPartReduction";
                    raw = key.CanTranslate() ? key.Translate(Math.Abs(pct)).ToString() : "Professional";
                }

                // 铁律 7.3：提取原版缩进层级（原版用 2 空格一级）
                int indent = CountLeadingSpaces(raw) / 2;

                string label = CleanLabel(raw);
                if (string.IsNullOrWhiteSpace(label)) label = part.GetType().Name;

                rows.Add(BreakdownRow.From(label.Trim(), before, cur, null, indent));
            }
            return cur;
        }

        /// <summary>统计字符串前导空格数，用于识别原版 ExplanationPart 的层级缩进。</summary>
        private static int CountLeadingSpaces(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int n = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ' ') n++;
                else if (c == '\t') n += 2;
                else break;
            }
            return n;
        }

        /// <summary>能力行后缀：原版显示 " (权重100%, 最大值100%)"（仅 max&gt;0 时附最大值）。</summary>
        private static string CapacitySuffix(PawnCapacityFactor cf)
        {
            string w = Tx("PersonalChronicle.UI.Ov.Tip.Weight", "weight")
                     + " " + (cf.weight * 100f).ToString("F0") + "%";
            if (cf.max > 0f)
                w += ", " + Tx("PersonalChronicle.UI.Ov.Tip.Max", "max")
                   + " " + (cf.max * 100f).ToString("F0") + "%";
            return " (" + w + ")";
        }

        /// <summary>
        /// 职业加成分解（派生字段专用，2026-08-30 新增）。
        /// 原版 StatPart 管线对「专业技能加成」无效——该加成是配方/工作类型条件生效的，
        /// 不挂在 pawn stat 上（WorkSpeed 走 recipe.workSpeedStat 注入）。故单独构建：
        ///   · 明细行 = 逐个专业技能的贡献（当前生效组 / 未生效组）
        ///   · 最终值 = 当前生效合计；上限与品质偏移放 FinalizeNote
        /// 数值全部来自 ProfessionalBonusSnapshotBuilder（Application 快照）→
        /// ProfessionalEffectResolver.ResolveContributions，与真实注入值同一真源。
        /// </summary>
        public static StatBreakdown BuildProfessionalBonus(Pawn pawn, string title)
        {
            if (pawn == null) return null;
            try
            {
                ProfessionalBonusView view = ProfessionalBonusSnapshotBuilder.Build(pawn);
                if (view == null || !view.HasAny) return null;

                var rows = new List<BreakdownRow>();
                float cur = 1f;

                // 组1：当前生效（链式相乘 → 最终值即当前生效合计）
                string groupActive = Tx("PersonalChronicle.UI.Ov.Tip.ProActive", "Active now");
                string groupIdle = Tx("PersonalChronicle.UI.Ov.Tip.ProInactive", "Not active (needs matching work)");
                for (int i = 0; i < view.Lines.Count; i++)
                {
                    ProfessionalBonusLine line = view.Lines[i];
                    if (line == null || !line.ActiveNow) continue;
                    float before = cur;
                    cur += (line.SpeedPct + line.GenericPct) / 100f;
                    rows.Add(BreakdownRow.From(LineLabel(line), before, cur, groupActive));
                }
                // 组2：未生效（当前工作/配方未命中白名单，仅展示潜力，不进链式累加）
                for (int i = 0; i < view.Lines.Count; i++)
                {
                    ProfessionalBonusLine line = view.Lines[i];
                    if (line == null || line.ActiveNow) continue;
                    rows.Add(BreakdownRow.From(
                        LineLabel(line), 1f, 1f + (line.SpeedPct + line.GenericPct) / 100f, groupIdle));
                }

                // 上限 + 品质偏移（单行说明，UI 在最终值下方补绘）
                string note = Tx("PersonalChronicle.UI.Ov.Tip.ProMax", "Upper bound")
                              + " ×" + (1f + view.MaxSpeedPct / 100f).ToString("F2");
                if (view.MaxQualityLevels != 0)
                {
                    note += "   " + Tx("PersonalChronicle.UI.Ov.Tip.ProQuality", "Quality")
                            + " " + SignedInt(view.CurrentQualityLevels) + "/"
                            + SignedInt(view.MaxQualityLevels);
                }

                return new StatBreakdown
                {
                    Title = title,
                    BaseRaw = 1f,
                    FinalRaw = cur,
                    BaseText = "×1.00",
                    FinalText = "×" + cur.ToString("F2"),
                    Rows = rows,
                    FinalizeNote = note,
                    GroupCount = CountGroups(rows),
                };
            }
            catch { return null; }
        }

        /// <summary>单行标签："精密制造 Lv12 · 高级"（含品质偏移时追加）。</summary>
        private static string LineLabel(ProfessionalBonusLine line)
        {
            string s = line.DisplayLabel + " Lv" + line.Level;
            if (!string.IsNullOrEmpty(line.RatingLabel) && line.RatingLabel != "—")
            {
                s += " · " + line.RatingLabel;
            }
            if (line.GenericPct != 0f)
            {
                string stat = string.IsNullOrEmpty(line.GenericStatLabel)
                    ? Tx("PersonalChronicle.UI.Ov.Tip.ProStat", "Stat")
                    : line.GenericStatLabel;
                string sign = line.GenericPct > 0f ? "+" : "";
                s += "  " + stat + " " + sign + line.GenericPct.ToString("F0") + "%";
            }
            if (line.QualityLevels != 0)
            {
                s += " " + Tx("PersonalChronicle.UI.Ov.Tip.ProQuality", "Quality")
                     + SignedInt(line.QualityLevels);
            }
            return s;
        }

        private static string SignedInt(int v)
        {
            string sign = v > 0 ? "+" : "";
            return sign + v.ToString();
        }

        private static string SafeLabel(string label, string defName)
            => string.IsNullOrEmpty(label) ? defName : label;

        /// <summary>
        /// 安全翻译：优先复用原版 key（自动继承官方语言包，如「技能」/「健康」）；
        /// key 缺失或异常时回退 fallback，绝不因翻译问题中断分解。
        /// </summary>
        private static string Tx(string key, string fallback)
        {
            try
            {
                if (string.IsNullOrEmpty(key) || !key.CanTranslate()) return fallback;
                return key.Translate().ToString();
            }
            catch { return fallback; }
        }
    }
}
