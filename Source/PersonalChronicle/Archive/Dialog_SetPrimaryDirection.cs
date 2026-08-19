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
        private const float CardH = 96f;
        private const float CardGap = 8f;
        private const float PadX = 12f;
        private const float FooterH = 30f;

        private readonly Pawn pawn;
        private readonly ChronicleGameComponent component;
        private readonly System.Action onCommitted;
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
        /// <param name="currentDirection">当前已选主方向 defName（null/空 = 未限定）。</param>
        /// <param name="onCommitted">选择落盘后的回调（用于触发 ITab 快照重建）。</param>
        public Dialog_SetPrimaryDirection(Pawn pawn, ChronicleGameComponent component, string currentDirection, System.Action onCommitted)
        {
            this.pawn = pawn;
            this.component = component;
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
                if (all[i] != null)
                {
                    directions.Add(all[i]);
                }
            }
            directions.Sort((a, b) => a.order.CompareTo(b.order));
        }

        public override void DoWindowContents(Rect inRect)
        {
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

            Rect bodyRect = new Rect(inRect.x, y, inRect.width, inRect.height - (y - inRect.y) - FooterH);

            if (directions == null || directions.Count == 0)
            {
                DrawCenteredNote(bodyRect, "PersonalChronicle.UI.Career.SetDirection.Empty".Translate());
                DrawFooter(inRect, inRect.yMax - FooterH, inRect.width);
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

            DrawFooter(inRect, inRect.yMax - FooterH, inRect.width);
        }

        private void DrawDirectionCard(Rect card, ProfessionalDirectionDef dir)
        {
            bool selected = string.Equals(currentDirection, dir.defName, System.StringComparison.Ordinal);

            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                UIComponents.Border(card, selected ? UITheme.PillGold : UITheme.BorderSoft);
                Widgets.DrawHighlightIfMouseover(card);

                // 方向色徽章（左）。
                Rect badge = new Rect(card.x + 10f, card.y + (card.height - 18f) / 2f, 16f, 18f);
                Color badgeColor = ParseHex(dir.colorHex, UITheme.Accent);
                GUI.color = badgeColor;
                Widgets.DrawBoxSolid(badge, badgeColor);

                // 方向名（上）+ 所属一级专业（下）。
                string label = dir.LabelCap;
                string profession = !string.IsNullOrEmpty(dir.profession)
                    ? dir.profession
                    : "PersonalChronicle.UI.Career.SetDirection.NoProfession".Translate();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = selected ? UITheme.Text : UITheme.Muted;
                Rect labelRect = new Rect(card.x + 34f, card.y + 6f, card.width - 50f, 22f);
                Widgets.Label(labelRect, label);

                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.Dim;
                Rect profRect = new Rect(card.x + 34f, card.y + 28f, card.width - 50f, 16f);
                Widgets.Label(profRect, profession);

                // §7.1 方向特化点：徽标（specializationKey 中文）+ 一句话说明，让 4 方向可区分。
                string specKey = dir.specializationKey;
                if (!string.IsNullOrEmpty(specKey))
                {
                    string specLabel = SpecializationLabel(specKey);
                    Rect specBadge = new Rect(card.x + 34f, card.y + 44f, 46f, 16f);
                    UIComponents.Badge(specBadge, specLabel, UITheme.PillBlue);
                    Rect specDesc = new Rect(card.x + 84f, card.y + 44f, card.width - 96f, 16f);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = UITheme.Muted;
                    Widgets.Label(specDesc, dir.specializationDescKey.Translate());
                }

                // 技能数据行：能力维度侧重 + 成长曲线（仅当方向已落地技能 Def；其余诚实占位）。
                ProfessionalSkillDef skill = FirstSkillOfDirection(dir);
                float rowY = card.y + 62f;
                if (skill != null)
                {
                    string abilities = string.Join(" / ", skill.abilityKeys.ConvertAll(AbilityLabel));
                    Rect abRect = new Rect(card.x + 34f, rowY, card.width - 50f, 16f);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = UITheme.Dim;
                    Widgets.Label(abRect, "能力侧重：" + abilities);
                    Rect curveRect = new Rect(card.x + 34f, rowY + 16f, card.width - 50f, 14f);
                    GUI.color = UITheme.Dim;
                    Widgets.Label(curveRect, string.Format("成长：基础 {0} / 上限 {1} XP / 满级 {2}",
                        Mathf.RoundToInt(skill.xpPerPracticeBase), Mathf.RoundToInt(skill.xpCap), skill.maxLevel));
                }
                else
                {
                    Rect pendRect = new Rect(card.x + 34f, rowY, card.width - 50f, 16f);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = UITheme.Dim;
                    Widgets.Label(pendRect, "技能数据待垂直切片落地（仅特化语义已定）");
                }

                // 选中态「✓」角标（右上）。
                if (selected)
                {
                    Rect chk = new Rect(card.x + card.width - 26f, card.y + 8f, 18f, 18f);
                    GUI.color = UITheme.PillGold;
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(chk, "✓");
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }

            if (Widgets.ButtonInvisible(card))
            {
                TrySelect(dir);
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
            tip = "PersonalChronicle.UI.Career.SetDirection.Done".Translate(dir.LabelCap);
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
            // 「清除选择」：null = 不限定方向（回落推荐）。
            Rect clearRect = new Rect(inRect.x, y, width, FooterH);
            if (Widgets.ButtonText(clearRect, "PersonalChronicle.UI.Career.SetDirection.Clear".Translate()))
            {
                ClearSelection();
            }
            // 反馈提示区（右侧小字，避免与按钮重叠：仅在有 tip 时显示于按钮上方不现实，
            // 这里直接复用 tip 在按钮文本下方不可行，故省略独立提示行，tip 已通过 Done 反馈给玩家）。
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

        /// <summary>§7.1 方向特化语义 key → 中文展示（UI 展示层，不参与业务判定）。</summary>
        private static string SpecializationLabel(string key)
        {
            switch (key)
            {
                case "Quality": return "品质";
                case "Throughput": return "产量";
                case "Material": return "材料";
                case "Volume": return "批量";
                default: return key;
            }
        }

        /// <summary>能力维度 key → 中文展示（UI 展示层；abilityKeys 为稳定内部标识）。</summary>
        private static string AbilityLabel(string key)
        {
            switch (key)
            {
                case "machining": return "加工能力";
                case "precisionControl": return "精度控制";
                case "processKnowledge": return "工艺理解";
                case "materialApplication": return "材料应用";
                case "qualityControl": return "质量控制";
                default: return key;
            }
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
    }
}
