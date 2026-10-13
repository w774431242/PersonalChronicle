using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// v1.1.4 手动授勋对话框（Verse.Window 范式）。
    ///
    /// 列出当前殖民者「全部」勋章（含已授予 / 可授予 / 进行中 / 未开放四类），
    /// 每枚卡片显示档位、描述、当前进度、数值加成 buff 与状态标签，并附带悬停
    /// 提示完整信息。玩家只能对「达标且未授予」的勋章点击授予，其余状态仅作
    /// 浏览与提示。
    ///
    /// 与自动授勋共用 <see cref="MedalAwardService.AwardManual"/> 写入/公告链路，
    /// 授予后即时刷新列表并触发快照重建。
    ///
    /// UI 严格消费 UITheme 令牌层 + UIComponents 组件层；窗口内不出现散落 GUI.color /
    /// new Color，绘制态一律 prev 快照 + try/finally 配对恢复（rimworld-ui-standards 红线）。
    /// </summary>
    public class Dialog_ManualMedalAward : Window
    {
        private const float TitleH = 28f;
        private const float WinW = 520f;
        private const float WinH = 560f;
        private const float CardH = 88f;
        private const float CardGap = 8f;
        private const float PadX = 12f;
        private const float FooterH = 28f;
        private const float MedalIconSize = 40f;
        private const float MedalIconGap = 10f;

        private readonly Pawn pawn;
        private readonly System.Action onAwarded;
        private List<MedalEvaluation> allEvaluations;
        private Vector2 scroll;
        private string tip;

        public override Vector2 InitialSize
        {
            get { return new Vector2(WinW, WinH); }
        }

        /// <param name="pawn">受勋殖民者活读实例。</param>
        /// <param name="onAwarded">每成功授予一枚后的回调（用于触发详情页快照重建）。</param>
        public Dialog_ManualMedalAward(Pawn pawn, System.Action onAwarded)
        {
            this.pawn = pawn;
            this.onAwarded = onAwarded;
            this.scroll = Vector2.zero;
            this.tip = string.Empty;
            RefreshAll();
            doCloseX = true;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            drawShadow = true;
            forcePause = true;
        }

        private void RefreshAll()
        {
            allEvaluations = new List<MedalEvaluation>();
            if (pawn == null) return;
            PawnObject pawnObject = ChroniclePawnAccess.GetPawnObject(pawn);
            if (pawnObject == null) return;
            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(pawnObject);
            if (result.Items != null)
            {
                allEvaluations.AddRange(result.Items);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 原生 Window 默认浅色背景——覆盖为 UITheme.Window 深色，统一主题。
            UIComponents.TintedBox(inRect, UITheme.Window);

            float y = inRect.y;
            Rect titleRect = new Rect(inRect.x, y, inRect.width, TitleH);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, MedalTranslationKeys.ManualAwardTitle().Translate().ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            y += TitleH + 4f;

            Rect bodyRect = new Rect(inRect.x, y, inRect.width, inRect.height - (y - inRect.y) - FooterH);

            if (allEvaluations == null || allEvaluations.Count == 0)
            {
                Color prevColor = GUI.color;
                GameFont prevFont = Text.Font;
                TextAnchor prevAnchor = Text.Anchor;
                try
                {
                    GUI.color = UITheme.Dim;
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(bodyRect, MedalTranslationKeys.ManualAwardEmpty().Translate().ToString());
                }
                finally
                {
                    GUI.color = prevColor;
                    Text.Font = prevFont;
                    Text.Anchor = prevAnchor;
                }
            }
            else
            {
                float viewH = allEvaluations.Count * CardH + (allEvaluations.Count - 1) * CardGap;
                Rect viewRect = new Rect(0f, 0f, bodyRect.width - 16f, viewH);
                Rect scrollRect = new Rect(bodyRect.x, bodyRect.y, bodyRect.width, bodyRect.height);
                Widgets.BeginScrollView(scrollRect, ref scroll, viewRect);
                float cy = 0f;
                for (int i = 0; i < allEvaluations.Count; i++)
                {
                    MedalEvaluation ev = allEvaluations[i];
                    if (ev == null || ev.Def == null) continue;
                    Rect card = new Rect(0f, cy, viewRect.width, CardH);
                    DrawAwardCard(card, ev);
                    cy += CardH + CardGap;
                }
                Widgets.EndScrollView();
            }

            // Footer: 提示区（授予反馈）
            Rect footerRect = new Rect(inRect.x, inRect.yMax - FooterH, inRect.width, FooterH);
            if (!string.IsNullOrEmpty(tip))
            {
                Color prevColor2 = GUI.color;
                GameFont prevFont2 = Text.Font;
                TextAnchor prevAnchor2 = Text.Anchor;
                try
                {
                    GUI.color = UITheme.Muted;
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(footerRect, tip);
                }
                finally
                {
                    GUI.color = prevColor2;
                    Text.Font = prevFont2;
                    Text.Anchor = prevAnchor2;
                }
            }
        }

        private void DrawAwardCard(Rect card, MedalEvaluation ev)
        {
            MedalDef def = ev.Def;
            Color tierColor = UITheme.MedalTierColor(def.tier);
            string label = MedalTranslationKeys.Label(def.defName).Translate().ToString();
            string desc = MedalTranslationKeys.Desc(def.defName).Translate().ToString();
            string buff = ResolveBuffText(def);
            MedalState state = Classify(ev);

            float threshold = (float)def.threshold;
            float progress = threshold > 0f ? Mathf.Clamp01((float)(ev.CurrentValue / threshold)) : 0f;
            string ratio = FormatValue(ev.CurrentValue) + " / " + FormatValue(threshold);

            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // 已授予卡片整体降亮度，视觉区分。
                GUI.color = state == MedalState.Granted ? UITheme.Dim : Color.white;
                UIComponents.Border(card, state == MedalState.Eligible ? UITheme.PillGold : UITheme.BorderSoft);
                Widgets.DrawHighlightIfMouseover(card);

                // 勋章图像框（左）：优先使用 MedalDef.iconPath 纹理；缺失时回退档位色块。
                Rect iconRect = new Rect(card.x + 8f, card.y + (card.height - MedalIconSize) / 2f, MedalIconSize, MedalIconSize);
                Texture2D icon = !string.IsNullOrEmpty(def.iconPath)
                    ? ContentFinder<Texture2D>.Get(def.iconPath, false)
                    : null;
                if (icon != null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                    GUI.color = UITheme.BorderSoft;
                    Widgets.DrawBox(iconRect);
                }
                else
                {
                    GUI.color = tierColor;
                    Widgets.DrawBoxSolid(iconRect, tierColor);
                }

                float textX = iconRect.xMax + MedalIconGap;

                // 称号（上；长称号截断）
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Rect labelRect = new Rect(textX, card.y + 6f, card.width - 160f, 22f);
                GUI.color = UITheme.Text;
                Widgets.Label(labelRect, UIComponents.TruncateToWidth(label, labelRect.width, GameFont.Small));

                // 档位 Pill（右上）
                Rect tierPill = new Rect(card.x + card.width - 58f, card.y + 8f, 50f, 18f);
                UIComponents.Pill(tierPill, MedalTranslationKeys.Tier(def.tier).Translate().ToString(), tierColor);

                // 状态 Pill（档位下方）
                Rect statePill = new Rect(card.x + card.width - 72f, card.y + 30f, 64f, 18f);
                UIComponents.Pill(statePill, StateLabel(state), StateColor(state));

                // 描述（小字，截断）
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = UITheme.Muted;
                Rect descRect = new Rect(textX, card.y + 28f, card.width - 140f, 16f);
                Widgets.Label(descRect, UIComponents.TruncateToWidth(desc, descRect.width, GameFont.Tiny));

                // 进度条 + 值/阈值（底部）
                Rect bar = new Rect(textX, card.y + 50f, card.width - 160f, UITheme.ProgressbarH);
                UIComponents.ProgressBar(bar, progress, tierColor);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = UITheme.Muted;
                Rect cap = new Rect(bar.x, bar.yMax + 2f, bar.width, 14f);
                Widgets.Label(cap, ratio);

                // 增益文本（右下角）
                if (!string.IsNullOrEmpty(buff))
                {
                    Rect buffRect = new Rect(card.x + card.width - 80f, card.y + 50f, 72f, 30f);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleRight;
                    GUI.color = UITheme.Alive;
                    Widgets.Label(buffRect, buff);
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }

            // 悬停提示：完整勋章信息。
            TooltipHandler.TipRegion(card, BuildTooltip(ev, label, desc, buff, state));

            if (Widgets.ButtonInvisible(card))
            {
                TryAward(ev);
            }
        }

        private void TryAward(MedalEvaluation ev)
        {
            if (ev == null || ev.Def == null) return;
            if (!ev.IsNewAward)
            {
                tip = MedalTranslationKeys.ManualAwardBlocked().Translate(ev.Def.defName).ToString();
                return;
            }
            bool ok = MedalAwardService.AwardManual(pawn, ev.Def);
            if (ok)
            {
                // 即时刷新以反映新的已授予状态；列表仍保留全部勋章供浏览。
                RefreshAll();
                tip = MedalTranslationKeys.ManualAwardDone().Translate(ev.Def.defName).ToString();
                if (onAwarded != null) onAwarded();
            }
            else
            {
                tip = MedalTranslationKeys.ManualAwardBlocked().Translate(ev.Def.defName).ToString();
            }
        }

        private static string BuildTooltip(MedalEvaluation ev, string label, string desc, string buff, MedalState state)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine(label);
            sb.AppendLine(MedalTranslationKeys.Tier(ev.Def.tier).Translate().ToString()
                + " · " + StateLabel(state));
            sb.AppendLine();
            sb.AppendLine(desc);
            sb.AppendLine();
            sb.AppendLine("PersonalChronicle.UI.Career.Honor.Detail.Current".Translate().ToString()
                + ": " + FormatValue(ev.CurrentValue));
            sb.AppendLine("PersonalChronicle.UI.Career.Honor.Detail.Threshold".Translate().ToString()
                + ": " + FormatValue(ev.Def.threshold));
            float pct = ev.Def.threshold > 0f
                ? Mathf.RoundToInt(Mathf.Clamp01((float)(ev.CurrentValue / ev.Def.threshold)) * 100f)
                : 0;
            sb.AppendLine("PersonalChronicle.UI.Career.Honor.Detail.Progress".Translate().ToString()
                + ": " + pct + "%");
            sb.AppendLine();
            sb.Append("PersonalChronicle.UI.Career.Honor.Detail.Buff".Translate().ToString()
                + ": " + (string.IsNullOrEmpty(buff)
                    ? "PersonalChronicle.UI.Career.Honor.Detail.NoBuff".Translate().ToString()
                    : buff));
            if (state == MedalState.Eligible)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("PersonalChronicle.UI.Medal.ManualAward.ClickToAward".Translate().ToString());
            }
            return sb.ToString();
        }

        private static MedalState Classify(MedalEvaluation ev)
        {
            if (ev.IsGranted) return MedalState.Granted;
            if (!ev.IsApplicable) return MedalState.NotApplicable;
            if (ev.IsNewAward) return MedalState.Eligible;
            return MedalState.InProgress;
        }

        private static string StateLabel(MedalState state)
        {
            switch (state)
            {
                case MedalState.Granted:
                    return "PersonalChronicle.UI.Medal.ManualAward.State.Granted".Translate().ToString();
                case MedalState.Eligible:
                    return "PersonalChronicle.UI.Medal.ManualAward.State.Eligible".Translate().ToString();
                case MedalState.InProgress:
                    return "PersonalChronicle.UI.Medal.ManualAward.State.InProgress".Translate().ToString();
                case MedalState.NotApplicable:
                    return "PersonalChronicle.UI.Medal.ManualAward.State.NotApplicable".Translate().ToString();
                default:
                    return string.Empty;
            }
        }

        private static Color StateColor(MedalState state)
        {
            switch (state)
            {
                case MedalState.Granted: return UITheme.Alive;
                case MedalState.Eligible: return UITheme.PillGold;
                case MedalState.InProgress: return UITheme.Info;
                case MedalState.NotApplicable: return UITheme.Muted;
                default: return UITheme.Muted;
            }
        }

        private static string ResolveBuffText(MedalDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.buffDefName))
            {
                return null;
            }
            MedalBuffDef buff = DefDatabase<MedalBuffDef>.GetNamedSilentFail(def.buffDefName);
            if (buff == null || string.IsNullOrEmpty(buff.displayBonus))
            {
                return null;
            }
            return buff.displayBonus.Translate().ToString();
        }

        private static string FormatValue(double value)
        {
            if (value >= 10000d) return (value / 1000d).ToString("0.#") + "K";
            return value.ToString("0.#");
        }

        private enum MedalState
        {
            Granted,
            Eligible,
            InProgress,
            NotApplicable
        }
    }
}
