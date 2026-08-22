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
    /// v1.1.4 手动授勋对话框（Verse.Window 范式，与 Dialog_RenameWorkplace 一致）。
    ///
    /// 仅列出当前判定为「达标且未授予」（<see cref="MedalAwardEvaluator"/> 的
    /// NewAwards）的阈值类 Pawn 勋章；玩家点击某枚即经
    /// <see cref="MedalAwardService.AwardManual"/> 写入，授予后该枚从列表移除并即时
    /// 持久化（MarkChanged）。与自动授勋共用同一写入/公告链路，避免逻辑分叉。
    ///
    /// UI 严格消费 UITheme 令牌层 + UIComponents 组件层；窗口内不出现散落 GUI.color /
    /// new Color，绘制态一律 prev 快照 + try/finally 配对恢复（rimworld-ui-standards 红线）。
    /// </summary>
    public class Dialog_ManualMedalAward : Window
    {
        private const float TitleH = 28f;
        private const float WinW = 480f;
        private const float WinH = 440f;
        private const float CardH = 76f;
        private const float CardGap = 8f;
        private const float PadX = 12f;
        private const float FooterH = 28f;

        private readonly Pawn pawn;
        private readonly ChronicleGameComponent component;
        private readonly System.Action onAwarded;
        private List<MedalEvaluation> pending;
        private Vector2 scroll;
        private string tip;

        public override Vector2 InitialSize
        {
            get { return new Vector2(WinW, WinH); }
        }

        /// <param name="pawn">受勋殖民者活读实例。</param>
        /// <param name="component">持久化组件（取 PawnObject + MarkChanged）。</param>
        /// <param name="onAwarded">每成功授予一枚后的回调（用于触发详情页快照重建）。</param>
        public Dialog_ManualMedalAward(Pawn pawn, ChronicleGameComponent component, System.Action onAwarded)
        {
            this.pawn = pawn;
            this.component = component;
            this.onAwarded = onAwarded;
            this.scroll = Vector2.zero;
            this.tip = string.Empty;
            RefreshPending();
            doCloseX = true;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            drawShadow = true;
            forcePause = true;
        }

        private void RefreshPending()
        {
            pending = new List<MedalEvaluation>();
            if (pawn == null || component == null) return;
            IReadOnlyList<PawnRecord> records = component.GetRecordsFor(pawn);
            if (records == null || records.Count == 0) return;
            PawnObject pawnObject = records[0] as PawnObject;
            if (pawnObject == null) return;
            List<MedalEvaluation> newAwards = MedalAwardEvaluator.EvaluatePawn(pawnObject).NewAwards;
            if (newAwards != null)
            {
                pending.AddRange(newAwards);
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

            if (pending == null || pending.Count == 0)
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
                float viewH = pending.Count * CardH + (pending.Count - 1) * CardGap;
                Rect viewRect = new Rect(0f, 0f, bodyRect.width - 16f, viewH);
                Rect scrollRect = new Rect(bodyRect.x, bodyRect.y, bodyRect.width, bodyRect.height);
                Widgets.BeginScrollView(scrollRect, ref scroll, viewRect);
                float cy = 0f;
                for (int i = 0; i < pending.Count; i++)
                {
                    MedalEvaluation ev = pending[i];
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
            float threshold = (float)def.threshold;
            float progress = threshold > 0f ? Mathf.Clamp01((float)(ev.CurrentValue / threshold)) : 0f;
            string ratio = FormatValue(ev.CurrentValue) + " / " + FormatValue(threshold);

            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                UIComponents.Border(card, UITheme.BorderSoft);
                Widgets.DrawHighlightIfMouseover(card);

                // 徽章色块（左）
                Rect badge = new Rect(card.x + 8f, card.y + (card.height - 16f) / 2f, 14f, 16f);
                GUI.color = tierColor;
                Widgets.DrawBoxSolid(badge, tierColor);

                // 称号（上；v4.17 体检：长称号截断，不换行压住 desc）
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Rect labelRect = new Rect(card.x + 30f, card.y + 6f, card.width - 96f, 22f);
                GUI.color = UITheme.Text;
                Widgets.Label(labelRect, Truncate(label, labelRect.width, GameFont.Small));

                // 档位 Pill（右上）
                Rect pill = new Rect(card.x + card.width - 58f, card.y + 8f, 50f, 18f);
                UIComponents.Pill(pill, MedalTranslationKeys.Tier(def.tier).Translate().ToString(), tierColor);

                // 描述（小字，截断）
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = UITheme.Muted;
                Rect descRect = new Rect(card.x + 30f, card.y + 30f, card.width - 38f, 16f);
                Widgets.Label(descRect, Truncate(desc, descRect.width, GameFont.Tiny));

                // 进度条 + 值/阈值（底部）——v4.17 体检：卡高 64→76，四段内容不再重叠
                Rect bar = new Rect(card.x + 30f, card.y + 48f, card.width - 38f, UITheme.ProgressbarH);
                UIComponents.ProgressBar(bar, progress, tierColor);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = UITheme.Muted;
                Rect cap = new Rect(card.x + 30f, bar.yMax + 2f, card.width - 38f, 14f);
                Widgets.Label(cap, ratio);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }

            if (Widgets.ButtonInvisible(card))
            {
                TryAward(ev);
            }
        }

        private void TryAward(MedalEvaluation ev)
        {
            if (ev == null || ev.Def == null) return;
            bool ok = MedalAwardService.AwardManual(pawn, ev.Def, component);
            if (ok)
            {
                // 即时从列表移除并触发快照重建；列表空了下次刷新显示 Empty。
                pending.Remove(ev);
                tip = MedalTranslationKeys.ManualAwardDone().Translate(ev.Def.defName).ToString();
                if (onAwarded != null) onAwarded();
                if (pending.Count == 0)
                {
                    RefreshPending();
                }
            }
            else
            {
                tip = MedalTranslationKeys.ManualAwardBlocked().Translate(ev.Def.defName).ToString();
            }
        }

        private static string FormatValue(double value)
        {
            if (value >= 10000d) return (value / 1000d).ToString("0.#") + "K";
            return value.ToString("0.#");
        }

        // v4.17 体检：Truncate 逐字符 CalcSize 每帧分配（PERF-001）→ 委托 UIComponents
        // 的 TruncateToWidth（中段截断 + 字体配对恢复，设计系统唯一收敛点）。
        private static string Truncate(string text, float maxWidth, GameFont font)
        {
            return UIComponents.TruncateToWidth(text ?? string.Empty, maxWidth, font);
        }
    }
}
