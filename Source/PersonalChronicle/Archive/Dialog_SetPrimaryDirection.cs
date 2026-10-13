using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// P2 缺口补全：玩家主方向选择对话框（V2.0 §9「推荐 ≠ 强制，玩家最终选择存
    /// ProfessionalState.primaryDirection」）。
    ///
    /// 列出全部 <see cref="ProfessionalDirectionDef"/>（按 order 排序），玩家选定后写入
    /// PawnObject.CareerData.Professional.primaryDirection 并经
    /// <see cref="PersonalChronicle.Application.ChroniclePawnAccess"/> 落盘（append-only 友好：null = 不限定方向）。
    /// 与 Dialog_RenameWorkplace / Dialog_ManualMedalAward 同范式（Verse.Window，UITheme 令牌层）。
    ///
    /// UI 不直写存储层细节：只通过 component + Pawn 取 PawnObject，经组件 MarkChanged 持久化。
    /// 绕 rimworld-ui-standards 红线：窗口内不散落 GUI.color / new Color，绘制态 prev 快照 + try/finally
    /// 配对恢复。
    /// </summary>
    public class Dialog_SetPrimaryDirection : Window
    {
        private const float TitleH = 28f;
        private const float WinW = 440f;
        private const float WinH = 460f;
        private const float CardH = 56f;
        private const float CardGap = 8f;
        private const float PadX = 12f;
        private const float FooterH = 30f;
        private const float TipH = 18f;

        private readonly Pawn pawn;
        private readonly System.Action onCommitted;
        private readonly string filterProfession;
        private List<ProfessionalDirectionDef> directions;
        private string currentDirection;
        private Vector2 scroll;
        private string tip;

        public override Vector2 InitialSize
        {
            get { return new Vector2(WinW, WinH); }
        }

        /// <param name="pawn">目标殖民者活读实例。</param>
        /// <param name="profession">一级专业稳定键（如 "Manufacturing"）；非空时只列出该专业下的二级方向。</param>
        /// <param name="currentDirection">当前已选主方向 defName（null/空 = 未限定）。</param>
        /// <param name="onCommitted">选择落盘后的回调（用于触发 ITab 快照重建）。</param>
        public Dialog_SetPrimaryDirection(Pawn pawn, string profession, string currentDirection, System.Action onCommitted)
        {
            this.pawn = pawn;
            this.filterProfession = string.IsNullOrEmpty(profession) ? null : profession;
            this.currentDirection = string.IsNullOrEmpty(currentDirection) ? null : currentDirection;
            this.onCommitted = onCommitted;
            this.scroll = Vector2.zero;
            this.tip = string.Empty;
            RefreshDirections();
            doCloseX = true;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            drawShadow = true;
            forcePause = true;
        }

        private void RefreshDirections()
        {
            directions = new List<ProfessionalDirectionDef>();
            List<ProfessionalDirectionDef> all = DefDatabase<ProfessionalDirectionDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                ProfessionalDirectionDef d = all[i];
                if (d == null) continue;
                // 一级专业过滤：profession 非空时只保留归属该专业的二级方向。
                if (filterProfession != null && !string.Equals(d.profession, filterProfession, System.StringComparison.Ordinal))
                {
                    continue;
                }
                directions.Add(d);
            }
            directions.Sort((a, b) => a.order.CompareTo(b.order));
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 原生 Window 默认是浅色 widget 背景——覆盖为 UITheme.Window 深色，
            // 避免二级方向选择卡片透出原生白底（与 ITab 深色主题一致）。
            UIComponents.TintedBox(inRect, UITheme.Window);

            float y = inRect.y;
            Rect titleRect = new Rect(inRect.x, y, inRect.width, TitleH);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, "PersonalChronicle.UI.Career.SetDirection.Title".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            y += TitleH + 4f;

            // 顶部说明：推荐由 ITab 规划分析给出，此处为「最终选择」（覆盖推荐）。
            // 主方向「完全锁定」语义（用户定稿）：一旦选定即永久固定，不可再改、不可清除。
            bool locked = !string.IsNullOrEmpty(currentDirection);
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                GUI.color = locked ? UITheme.Warn : UITheme.Muted;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                string hintKey = locked
                    ? "PersonalChronicle.UI.Career.SetDirection.HintLocked"
                    : "PersonalChronicle.UI.Career.SetDirection.Hint";
                float hintH = Text.CalcHeight(hintKey.Translate(), inRect.width);
                Rect hintRect = new Rect(inRect.x, y, inRect.width, Mathf.Max(16f, hintH));
                Widgets.Label(hintRect, hintKey.Translate());
                y += hintRect.height + 6f;
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }

            Rect bodyRect = new Rect(inRect.x, y, inRect.width, inRect.height - (y - inRect.y) - FooterH - TipH);

            if (directions == null || directions.Count == 0)
            {
                DrawCenteredNote(bodyRect, "PersonalChronicle.UI.Career.SetDirection.Empty".Translate());
                DrawFooter(inRect, inRect.yMax - FooterH - TipH, inRect.width);
                return;
            }

            float viewH = directions.Count * (CardH + CardGap);
            Rect viewRect = new Rect(0f, 0f, bodyRect.width - 16f, viewH);
            Rect scrollRect = new Rect(bodyRect.x, bodyRect.y, bodyRect.width, bodyRect.height);
            Widgets.BeginScrollView(scrollRect, ref scroll, viewRect);
            float cy = 0f;
            for (int i = 0; i < directions.Count; i++)
            {
                ProfessionalDirectionDef dir = directions[i];
                if (dir == null) continue;
                Rect card = new Rect(0f, cy, viewRect.width, CardH);
                DrawDirectionCard(card, dir);
                cy += CardH + CardGap;
            }
            Widgets.EndScrollView();

            DrawFooter(inRect, inRect.yMax - FooterH - TipH, inRect.width);
        }

        private void DrawDirectionCard(Rect card, ProfessionalDirectionDef dir)
        {
            bool selected = string.Equals(currentDirection, dir.defName, System.StringComparison.Ordinal);

            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // v1.1.5 patch7：BeginScrollView 内 ButtonInvisible 必须先注册再绘制——
                // 原版实现在 try/finally 外注册，滚动矩阵更新后 Event.current 矩阵已切换，
                // card.Contains(Event.current.mousePosition) 可能误判。挪到视觉绘制之前可避免。
                // 主方向「完全锁定」：已选定时整卡禁用点击（不可再改、不可清除），仅展示。
                bool locked = !string.IsNullOrEmpty(currentDirection);
                if (!locked && Widgets.ButtonInvisible(card))
                {
                    TrySelect(dir);
                    Event.current.Use();
                }

                UIComponents.TintedBox(card, selected ? UITheme.PanelRaised : UITheme.Panel);
                UIComponents.Border(card, selected ? UITheme.PillGold : UITheme.BorderSoft);
                Widgets.DrawHighlightIfMouseover(card);

                // 方向色徽章（左）。
                Rect badge = new Rect(card.x + 10f, card.y + (card.height - 18f) / 2f, 16f, 18f);
                Color badgeColor = ParseHex(dir.colorHex, UITheme.Accent);
                GUI.color = badgeColor;
                Widgets.DrawBoxSolid(badge, badgeColor);

                // 方向名（上）+ 所属一级专业 + 特化点（下，紧凑在 56px 卡内）。
                string label = dir.GetDisplayLabel().Translate().ToString();
                string profession = !string.IsNullOrEmpty(dir.profession)
                    ? ("PersonalChronicle.UI.Career.Major." + dir.profession).Translate().ToString()
                    : "PersonalChronicle.UI.Career.SetDirection.NoProfession".Translate();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = selected ? UITheme.Text : UITheme.Muted;
                Rect labelRect = new Rect(card.x + 34f, card.y + 6f, card.width - 64f, 22f);
                Widgets.Label(labelRect, label);

                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.Dim;
                Rect profRect = new Rect(card.x + 34f, card.y + 30f, card.width - 64f, 16f);
                Widgets.Label(profRect, profession);

                // §7.1 方向特化点：徽标（specializationKey 中文），放在第二行右侧，与专业同行不超卡高。
                string specKey = dir.specializationKey;
                if (!string.IsNullOrEmpty(specKey))
                {
                    string specLabel = SpecializationLabel(specKey);
                    Rect specBadge = new Rect(card.x + card.width - 78f, card.y + 30f, 64f, 16f);
                    UIComponents.Badge(specBadge, specLabel, UITheme.PillBlue);
                }

                // 选中态「✓」角标（右上）。
                if (selected)
                {
                    Rect chk = new Rect(card.x + card.width - 26f, card.y + 8f, 18f, 18f);
                    GUI.color = UITheme.PillGold;
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(chk, "PersonalChronicle.UI.Career.SetDirection.Selected".Translate());
                }

                // 鼠标悬停 → 显示具体数据加成详情（效果类型 + 强度 + 目标 Stat + 能力维度 + 成长曲线）。
                // 卡片本身精简，加成明细不常显（对齐需求：悬停显示）。
                TooltipHandler.TipRegion(card, new TipSignal(BuildDirectionBonusTip(dir)));
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private void TrySelect(ProfessionalDirectionDef dir)
        {
            // 主方向「完全锁定」：已选定后不可再改（用户定稿）。仅未选定时可写。
            if (!string.IsNullOrEmpty(currentDirection)) return;
            if (dir == null || pawn == null) return;
            if (!ChroniclePawnAccess.SetPrimaryDirection(pawn, dir))
            {
                tip = "PersonalChronicle.UI.Career.SetDirection.NoCareer".Translate();
                return;
            }
            currentDirection = dir.defName;
            tip = "PersonalChronicle.UI.Career.SetDirection.Done".Translate(dir.GetDisplayLabel().Translate().ToString());
            if (onCommitted != null)
            {
                onCommitted();
            }
        }

        private void DrawCenteredNote(Rect rect, string text)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                GUI.color = UITheme.Dim;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, text);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private void DrawFooter(Rect inRect, float y, float width)
        {
            // 反馈提示行（tip：选择/清除成功后的确认反馈，非空才绘制）。
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = UITheme.Dim;
                Widgets.Label(new Rect(inRect.x, y, width, TipH), tip ?? string.Empty);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }

            // 「清除选择」：null = 不限定方向（回落推荐）。
            // 主方向「完全锁定」：已选定后不显示清除按钮（不可改、不可回落）。
            Rect clearRect = new Rect(inRect.x, y + TipH, width, FooterH);
            if (!string.IsNullOrEmpty(currentDirection))
            {
                UIComponents.Label(clearRect,
                    "PersonalChronicle.UI.Career.SetDirection.LockedNote".Translate(),
                    UITheme.FontLabel, UITheme.Warn);
            }
            else if (Widgets.ButtonText(clearRect, "PersonalChronicle.UI.Career.SetDirection.Clear".Translate()))
            {
                ClearSelection();
            }
        }

        private void ClearSelection()
        {
            if (pawn == null) return;
            ChroniclePawnAccess.ClearPrimaryDirection(pawn);
            currentDirection = null;
            tip = "PersonalChronicle.UI.Career.SetDirection.Cleared".Translate();
            if (onCommitted != null)
            {
                onCommitted();
            }
        }

        private static Color ParseHex(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            if (ColorUtility.TryParseHtmlString(hex, out Color c)) return c;
            return fallback;
        }

        /// <summary>§7.1 方向特化语义 key → 展示文本（经翻译键；UI 展示层，不参与业务判定）。</summary>
        private static string SpecializationLabel(string key)
        {
            string full = "PersonalChronicle.UI.Career.SetDirection.Spec." + key;
            string v = full.Translate().ToString();
            return string.IsNullOrEmpty(v) || v == full ? key : v;
        }

        /// <summary>能力维度 key → 展示文本（经翻译键；UI 展示层；abilityKeys 为稳定内部标识）。</summary>
        private static string AbilityLabel(string key)
        {
            string full = "PersonalChronicle.UI.Career.SetDirection.Ability." + key;
            string v = full.Translate().ToString();
            return string.IsNullOrEmpty(v) || v == full ? key : v;
        }

        /// <summary>取方向首个已落地技能 Def（按 skillDefNames → DefDatabase 解析）。</summary>
        private static ProfessionalSkillDef FirstSkillOfDirection(ProfessionalDirectionDef dir)
        {
            if (dir == null || dir.skillDefNames == null) return null;
            List<ProfessionalSkillDef> all = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
            for (int i = 0; i < dir.skillDefNames.Count; i++)
            {
                string name = dir.skillDefNames[i];
                for (int j = 0; j < all.Count; j++)
                {
                    if (string.Equals(all[j].defName, name, System.StringComparison.Ordinal))
                    {
                        return all[j];
                    }
                }
            }
            return null;
        }

        /// <summary>取方向下全部技能 Def（按 skillDefNames 解析；供悬停加成汇总）。</summary>
        private static List<ProfessionalSkillDef> SkillsOfDirection(ProfessionalDirectionDef dir)
        {
            var list = new List<ProfessionalSkillDef>();
            if (dir == null || dir.skillDefNames == null) return list;
            List<ProfessionalSkillDef> all = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
            for (int i = 0; i < dir.skillDefNames.Count; i++)
            {
                string name = dir.skillDefNames[i];
                for (int j = 0; j < all.Count; j++)
                {
                    if (string.Equals(all[j].defName, name, System.StringComparison.Ordinal))
                    {
                        list.Add(all[j]);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 悬停加成明细（方向数值加成结构透视表）：方向 → 技能 → 效果（类型 + 技能级实际强度 + 目标 Stat）
        /// + 能力维度 + 成长曲线。
        /// 2026-08-22 P0 修复：① 读取技能级 effectOverrides 的实际值（共享效果被技能覆盖后显示覆盖值，
        /// 而非 Def 默认 value）；② 提升 public 供 ITab 已选方向悬停复用（不再维持一级专业适配悬停）。
        /// 2026-08-23 补注：本结构展示的数值加成最终注入原版官方 StatDef（如 recipe.workSpeedStat /
        /// ConstructionSpeed 等），即对原版 Stat 的加成增益 buff。
        /// </summary>
        public static string BuildDirectionBonusTip(ProfessionalDirectionDef dir)
        {
            if (dir == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>").Append(dir.GetDisplayLabel().Translate().ToString()).Append("</b>");

            List<ProfessionalSkillDef> skills = SkillsOfDirection(dir);
            if (skills.Count == 0)
            {
                sb.Append("\n").Append("PersonalChronicle.UI.Career.SetDirection.PendingSkill".Translate());
                return sb.ToString();
            }

            // 能力维度侧重（合并方向下技能 abilityKeys）。
            var abilitySet = new HashSet<string>();
            foreach (ProfessionalSkillDef s in skills)
            {
                if (s.abilityKeys != null)
                {
                    foreach (string a in s.abilityKeys) abilitySet.Add(a);
                }
            }
            if (abilitySet.Count > 0)
            {
                string abilities = string.Join(" / ", System.Linq.Enumerable.OrderBy(abilitySet, a => a).ToList().ConvertAll(AbilityLabel));
                sb.Append("\n").Append("PersonalChronicle.UI.Career.SetDirection.AbilityPrefix".Translate(abilities));
            }

            // 数据加成结构透视表：效果类型 + 技能级实际强度 + 目标 Stat。
            var seenEffects = new HashSet<string>();
            foreach (ProfessionalSkillDef s in skills)
            {
                if (s.effectDefNames == null) continue;
                foreach (string ename in s.effectDefNames)
                {
                    if (string.IsNullOrEmpty(ename) || !seenEffects.Add(ename)) continue;
                    ProfessionalEffectDef eff = DefDatabase<ProfessionalEffectDef>.GetNamedSilentFail(ename);
                    if (eff == null) continue;
                    string effLabel = !string.IsNullOrEmpty(eff.labelKey)
                        ? eff.labelKey.Translate().ToString()
                        : ename;
                    string statName = !string.IsNullOrEmpty(eff.statDefName)
                        ? OfficialStatLabel(eff.statDefName)
                        : "—";
                    // 实际生效值：技能级 effectOverrides 覆盖 → 覆盖值；否则回退 Def 默认 value。
                    float actualValue = eff.value;
                    EffectOverride ov = FindEffectOverride(s, ename);
                    if (ov != null && ov.hasValue)
                    {
                        actualValue = ov.value;
                    }
                    string valText = FormatEffectValue(eff, actualValue);
                    // 效果名（labelKey）已自带数值/单位时不再追加 valText，避免重复（如"收获效率 +10%" + "+10%"）。
                    bool labelHasValue = effLabel.Contains("%") || effLabel.Contains("+") || effLabel.Contains("−") || effLabel.Contains("-");
                    sb.Append("\n").Append(effLabel);
                    if (!labelHasValue)
                    {
                        sb.Append("  ").Append(valText);
                    }
                    sb.Append("  (").Append(statName).Append(")");
                }
            }

            // 成长曲线（首个有数据的技能）。
            ProfessionalSkillDef first = skills[0];
            sb.Append("\n").Append("PersonalChronicle.UI.Career.SetDirection.Growth".Translate(
                Mathf.RoundToInt(first.xpPerPracticeBase),
                Mathf.RoundToInt(first.xpCap),
                first.maxLevel));

            // 原版加成增益 buff：本悬停结构展示的数值加成（WorkSpeed / QualityBias / StatBonus /
            // CombatStat / YieldBonus 等）经 ProfessionalEffectResolver 最终注入原版官方 StatDef
            // （如 recipe.workSpeedStat / ConstructionSpeed / MeleeDamageFactor 等官方 Stat），
            // 即对原版 Stat 的加成增益 buff，而非 mod 自造的独立 buff。
            return sb.ToString();
        }

        /// <summary>取技能对某效果 Def 的覆盖项；无覆盖返回 null。</summary>
        private static EffectOverride FindEffectOverride(ProfessionalSkillDef skillDef, string effectDefName)
        {
            if (skillDef == null || skillDef.effectOverrides == null || skillDef.effectOverrides.Count == 0
                || string.IsNullOrEmpty(effectDefName))
            {
                return null;
            }
            for (int i = 0; i < skillDef.effectOverrides.Count; i++)
            {
                EffectOverride ov = skillDef.effectOverrides[i];
                if (ov != null && string.Equals(ov.effectDefName, effectDefName, System.StringComparison.Ordinal))
                {
                    return ov;
                }
            }
            return null;
        }

        /// <summary>
        /// Stat defName → 玩家语言下的 Stat 名称。
        /// RimWorld 官方 Stat 名称不通过 "Stat.&lt;defName&gt;.Label" 翻译键（该键不存在），
        /// 而是在 Def 加载时由 StatDef.label 字段本地化注入；故改读 StatDef 实例 label。
        /// 非官方 Stat（如自定义 XpGrowth）回退到 mod 自有翻译键 StatLabel.&lt;defName&gt;，
        /// 仍缺失则回退 defName（绝不显示乱码 key）。
        /// </summary>
        private static string OfficialStatLabel(string statDefName)
        {
            if (string.IsNullOrEmpty(statDefName))
            {
                return "—";
            }
            StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(statDefName);
            if (stat != null)
            {
                string label = (string)stat.LabelCap;
                if (!string.IsNullOrEmpty(label)) return label;
            }
            string customKey = "PersonalChronicle.UI.Career.SetDirection.StatLabel." + statDefName;
            if (customKey.CanTranslate()) return customKey.Translate().ToString();
            return statDefName;
        }

        /// <summary>
        /// 效果强度格式化：**统一采用原生的字段数值显示**。
        ///
        /// 凡声明了 <see cref="ProfessionalEffectDef.statDefName"/> 的效果，一律交给该官方
        /// StatDef 自己的 <see cref="StatWorker.ValueToString"/> 格式化，因此显示与原版属性面板
        /// 中该字段完全一致——自动继承 toStringStyle（百分号 / 小数位 / 单位）与本地化，
        /// 不再由本 Mod 手拼 "N%"。
        ///
        /// 语义映射：Def.value 是「相对修正量」（0.05 = +5%），而原生 Factor 语义以 1.0 = 100%，
        /// 故先转成因子再交给原生格式化器：+5% → 1.05 → "105%"（该 stat 为 PercentZero 时）。
        /// isReduction（"越低越好"，如食物中毒率）的正向减少量取负：0.05 → 0.95 → "95%"。
        /// </summary>
        private static string FormatEffectValue(ProfessionalEffectDef eff, float value)
        {
            if (eff == null) return "";

            float signed = eff.isReduction ? -value : value;

            if (!string.IsNullOrEmpty(eff.statDefName))
            {
                StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(eff.statDefName);
                if (stat?.Worker != null)
                {
                    try
                    {
                        // finalized=false：展示的是加成量本身，不套用该 stat 的 min/max 钳制。
                        string s = stat.Worker.ValueToString(1f + signed, false, ToStringNumberSense.Factor);
                        if (!string.IsNullOrEmpty(s)) return s;
                    }
                    catch { /* 个别 stat 的 Worker 可能抛错 → 落回手写回退格式 */ }
                }
            }
            return FormatEffectValueFallback(eff.kind, value, eff.isReduction);
        }

        /// <summary>
        /// 回退格式：无目标 StatDef 时（QualityBias 为整档偏移、或未声明 statDefName 的效果）
        /// 才由本 Mod 手写——这部分没有原生字段可对齐。
        /// </summary>
        private static string FormatEffectValueFallback(ProfessionalEffectKind kind, float value, bool isReduction)
        {
            switch (kind)
            {
                case ProfessionalEffectKind.WorkSpeed:
                case ProfessionalEffectKind.StatBonus:
                case ProfessionalEffectKind.CombatStat:
                case ProfessionalEffectKind.YieldBonus:
                case ProfessionalEffectKind.XpBonus:
                {
                    int pct = Mathf.RoundToInt(value * 100f);
                    if (isReduction)
                    {
                        return "-" + pct + "%"; // 食物中毒率等"越低越好"
                    }
                    return (pct >= 0 ? "+" : "") + pct + "%";
                }
                case ProfessionalEffectKind.QualityBias:
                    return (value >= 0 ? "+" : "") + value.ToString("0");
                default:
                    return value.ToString("0.##");
            }
        }
    }
}
