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
    /// <see cref="ChronicleGameComponent.MarkChanged"/> 落盘（append-only 友好：null = 不限定方向）。
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
        private readonly ChronicleGameComponent component;
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
        /// <param name="component">持久化组件（取 PawnObject + MarkChanged）。</param>
        /// <param name="profession">一级专业稳定键（如 "Manufacturing"）；非空时只列出该专业下的二级方向。</param>
        /// <param name="currentDirection">当前已选主方向 defName（null/空 = 未限定）。</param>
        /// <param name="onCommitted">选择落盘后的回调（用于触发 ITab 快照重建）。</param>
        public Dialog_SetPrimaryDirection(Pawn pawn, ChronicleGameComponent component, string profession, string currentDirection, System.Action onCommitted)
        {
            this.pawn = pawn;
            this.component = component;
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
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                GUI.color = UITheme.Muted;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                float hintH = Text.CalcHeight("PersonalChronicle.UI.Career.SetDirection.Hint".Translate(), inRect.width);
                Rect hintRect = new Rect(inRect.x, y, inRect.width, Mathf.Max(16f, hintH));
                Widgets.Label(hintRect, "PersonalChronicle.UI.Career.SetDirection.Hint".Translate());
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
                if (Widgets.ButtonInvisible(card))
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
            if (dir == null || pawn == null || component == null) return;
            PawnObject po = component.GetObject(pawn.GetUniqueLoadID()) as PawnObject;
            if (po == null || po.CareerData == null)
            {
                tip = "PersonalChronicle.UI.Career.SetDirection.NoCareer".Translate();
                return;
            }
            if (po.CareerData.Professional == null)
            {
                po.CareerData.Professional = new ProfessionalState();
            }
            po.CareerData.Professional.primaryDirection = dir.defName;
            component.MarkChanged();
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
            Rect clearRect = new Rect(inRect.x, y + TipH, width, FooterH);
            if (Widgets.ButtonText(clearRect, "PersonalChronicle.UI.Career.SetDirection.Clear".Translate()))
            {
                ClearSelection();
            }
        }

        private void ClearSelection()
        {
            if (pawn == null || component == null) return;
            PawnObject po = component.GetObject(pawn.GetUniqueLoadID()) as PawnObject;
            if (po != null && po.CareerData != null && po.CareerData.Professional != null)
            {
                po.CareerData.Professional.primaryDirection = null;
                component.MarkChanged();
            }
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

        /// <summary>悬停加成明细：方向 → 技能 → 效果（类型 + 强度 + 目标 Stat）+ 能力维度 + 成长曲线。</summary>
        private static string BuildDirectionBonusTip(ProfessionalDirectionDef dir)
        {
            if (dir == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            sb.Append(dir.GetDisplayLabel().Translate().ToString());

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
                string abilities = string.Join(" / ", System.Linq.Enumerable.ToList(abilitySet).ConvertAll(AbilityLabel));
                sb.Append("\n").Append("PersonalChronicle.UI.Career.SetDirection.AbilityPrefix".Translate(abilities));
            }

            // 数据加成：效果类型 + 强度 + 目标 Stat。
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
                        ? eff.statDefName
                        : "—";
                    // value：WorkSpeed/QualityBias 等按语义格式化（+5% / +1 档）。
                    string valText = FormatEffectValue(eff.kind, eff.value);
                    sb.Append("\n").Append(effLabel)
                        .Append("  ").Append(valText)
                        .Append("  (").Append(statName).Append(")");
                }
            }

            // 成长曲线（首个有数据的技能）。
            ProfessionalSkillDef first = skills[0];
            sb.Append("\n").Append("PersonalChronicle.UI.Career.SetDirection.Growth".Translate(
                Mathf.RoundToInt(first.xpPerPracticeBase),
                Mathf.RoundToInt(first.xpCap),
                first.maxLevel));

            return sb.ToString();
        }

        /// <summary>效果强度按类型格式化展示（WorkSpeed 为乘算 %，QualityBias 为整档偏移，其余原值）。</summary>
        private static string FormatEffectValue(ProfessionalEffectKind kind, float value)
        {
            switch (kind)
            {
                case ProfessionalEffectKind.WorkSpeed:
                    return (value >= 0 ? "+" : "") + Mathf.RoundToInt(value * 100f) + "%";
                case ProfessionalEffectKind.QualityBias:
                    return (value >= 0 ? "+" : "") + value.ToString("0");
                default:
                    return value.ToString("0.##");
            }
        }
    }
}
