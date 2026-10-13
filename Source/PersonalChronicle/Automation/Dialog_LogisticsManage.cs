using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 物流管理弹窗（PM 2026-09-07：把「设定输出目标 / 解除输出目标 / 输出阈值」
    /// 三件事统一进一个「物流管理」互动键，替代原先散落的两个 gizmo）。
    ///
    /// 布局纪律：零魔法行高（Text.CalcHeight 实测）；颜色 static readonly；
    /// Text.Anchor/GUI.color 快照 try/finally；单方法 &lt; 60 行。
    /// </summary>
    public class Dialog_LogisticsManage : Window
    {
        private readonly Thing parent;
        private readonly CompItemPort port;

        private static readonly Color CardBg = new Color(0.13f, 0.13f, 0.13f, 0.55f);
        private static readonly Color CardBorder = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        private static readonly Color SectionTitle = new Color(0.75f, 0.82f, 0.85f, 1f);
        private static readonly Color OkState = new Color(0.55f, 0.85f, 0.55f, 1f);
        private static readonly Color WarnState = new Color(0.85f, 0.65f, 0.3f, 1f);
        private static readonly Color InfoHint = new Color(0.62f, 0.68f, 0.72f, 1f);

        private const float CardPad = 8f;
        private const float SectionGap = 10f;

        /// <summary>阈值滑条上限（件）。仓储本身无上限，500 已覆盖常规攒批需求。</summary>
        private const int ThresholdMax = 500;

        /// <summary>带整数占位的翻译（2026-09-21 CHG-SHOP-AUDIT-001 · L-3：原为硬编码「 件」）。</summary>
        private static string T1(string key, int arg)
        {
            return key.Translate(arg).ToString();
        }

        public Dialog_LogisticsManage(Thing parent, CompItemPort port)
        {
            this.parent = parent;
            this.port = port;
            doCloseX = true;
            draggable = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(440f, 372f);

        public override void DoWindowContents(Rect inRect)
        {
            TextAnchor anchor = Text.Anchor;
            GameFont font = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                DrawContent(inRect);
            }
            finally
            {
                Text.Anchor = anchor;
                Text.Font = font;
            }
        }

        private void DrawContent(Rect inRect)
        {
            Rect header = inRect.TopPartPixels(30f);
            Widgets.Label(header, "PersonalChronicle.Automation.Port.ManageTitle".Translate());
            float y = header.yMax + 4f;
            float innerW = inRect.width;

            // ① 输出目标（已绑定时多一行「解除绑定」，卡片高度前置计算避免溢出）
            bool hasTarget = port.HasTarget;
            float title1H = TitleHeight("PersonalChronicle.Automation.Port.ManageTarget".Translate(), innerW - CardPad * 2f);
            float card1H = CardPad + title1H + 30f + CardPad + (hasTarget ? 32f : 0f);
            Rect card1 = new Rect(inRect.x, y, innerW, card1H);
            DrawCard(card1);
            Rect c1 = card1.ContractedBy(CardPad);
            DrawSectionTitle(new Rect(c1.x, c1.y, c1.width, title1H),
                "PersonalChronicle.Automation.Port.ManageTarget".Translate());
            TaggedString targetText = hasTarget
                ? "PersonalChronicle.Automation.Port.Inspect.Target".Translate(port.TargetDescription)
                : "PersonalChronicle.Automation.Port.Inspect.None".Translate();
            DrawStateLine(new Rect(c1.x, c1.y + title1H, c1.width - 150f, 30f), targetText,
                hasTarget ? InfoHint : WarnState);
            if (Widgets.ButtonText(new Rect(c1.xMax - 146f, c1.y + title1H, 146f, 30f),
                    "PersonalChronicle.Automation.Port.Manage.SetTarget".Translate()))
            {
                // 进入选图模式前关窗（原版 Targeter 需要完整地图视野），绑完重开即见结果。
                port.BeginBinding();
                Close();
            }
            if (hasTarget && Widgets.ButtonText(
                    new Rect(c1.x, c1.y + title1H + 34f, c1.width, 28f),
                    "PersonalChronicle.Automation.Port.Manage.ClearTarget".Translate()))
            {
                port.Unbind();
            }
            y = card1.yMax + SectionGap;

            // ② 输出阈值（滑条 + 快捷值）
            float title2H = TitleHeight("PersonalChronicle.Automation.Port.ManageThreshold".Translate(), innerW - CardPad * 2f);
            float card2H = CardPad + title2H + 34f + 26f + CardPad;
            Rect card2 = new Rect(inRect.x, y, innerW, card2H);
            DrawCard(card2);
            Rect c2 = card2.ContractedBy(CardPad);
            DrawSectionTitle(new Rect(c2.x, c2.y, c2.width, title2H),
                "PersonalChronicle.Automation.Port.ManageThreshold".Translate());
            Rect sliderRect = new Rect(c2.x + 90f, c2.y + title2H + 4f, c2.width - 90f - 64f, 22f);
            int threshold = port.OutputThreshold;
            float slider = Widgets.HorizontalSlider(sliderRect, threshold, 0f, ThresholdMax, true,
                threshold.ToString(), "0", ThresholdMax.ToString(), 1f);
            int newVal = Mathf.RoundToInt(slider);
            if (newVal != threshold)
            {
                port.SetOutputThreshold(newVal);
                threshold = newVal;
            }
            Rect thresholdNum = new Rect(sliderRect.xMax + 6f, c2.y + title2H + 4f, 58f, 22f);
            TextAnchor numAnchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(thresholdNum, T1("PersonalChronicle.Automation.Port.UnitPieces", threshold));
            }
            finally
            {
                Text.Anchor = numAnchor;
            }
            // 快捷值按钮（0/50/100/250），一行四枚。
            float qw = (c2.width - 36f) / 4f;
            int[] quickValues = { 0, 50, 100, 250 };
            for (int i = 0; i < quickValues.Length; i++)
            {
                Rect qr = new Rect(c2.x + i * (qw + 12f), c2.y + title2H + 32f, qw, 24f);
                string tag = quickValues[i].ToString();
                if (Widgets.ButtonText(qr, tag))
                {
                    port.SetOutputThreshold(quickValues[i]);
                }
                TooltipHandler.TipRegion(qr, T1("PersonalChronicle.Automation.Port.UnitPieces", quickValues[i]));
            }
            y = card2.yMax + SectionGap;

            // ③ 状态（仓储量与推送状态实时刷新）
            float title3H = TitleHeight("PersonalChronicle.Automation.Port.ManageStatus".Translate(), innerW - CardPad * 2f);
            float card3H = CardPad + title3H + 24f + CardPad;
            Rect card3 = new Rect(inRect.x, y, innerW, card3H);
            DrawCard(card3);
            Rect c3 = card3.ContractedBy(CardPad);
            DrawSectionTitle(new Rect(c3.x, c3.y, c3.width, title3H),
                "PersonalChronicle.Automation.Port.ManageStatus".Translate());
            IItemStorage storage = ItemStorageUtility.GetStorage(parent);
            int stored = storage != null ? storage.StoredTotal : 0;
            bool running = threshold > 0
                ? stored >= threshold
                : stored > 0;
            TaggedString state = running
                ? "PersonalChronicle.Automation.Port.Manage.Ready".Translate(stored.ToString(), threshold.ToString())
                : "PersonalChronicle.Automation.Port.Manage.Waiting".Translate(stored.ToString(), threshold.ToString());
            DrawStateLine(new Rect(c3.x, c3.y + title3H, c3.width, 24f), state, running ? OkState : InfoHint);

            // 窗口高度自适应（阈值行常驻，高度仅随语言变化微调）。
            float needed = Mathf.Clamp(card3.yMax + 10f + 30f, 340f, 560f);
            if (Mathf.Abs(windowRect.height - needed) > 2f)
            {
                windowRect.height = needed;
            }
        }

        private static float TitleHeight(TaggedString title, float width)
        {
            return Mathf.Max(22f, Text.CalcHeight(title.RawText, width));
        }

        private static void DrawCard(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, CardBg);
            Widgets.DrawBox(rect);
        }

        private static void DrawSectionTitle(Rect rect, TaggedString title)
        {
            Color prev = GUI.color;
            TextAnchor anchor = Text.Anchor;
            try
            {
                GUI.color = SectionTitle;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(rect, title);
            }
            finally
            {
                GUI.color = prev;
                Text.Anchor = anchor;
            }
        }

        private static void DrawStateLine(Rect rect, TaggedString text, Color color)
        {
            Color prev = GUI.color;
            TextAnchor anchor = Text.Anchor;
            try
            {
                GUI.color = color;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(rect, text);
            }
            finally
            {
                GUI.color = prev;
                Text.Anchor = anchor;
            }
        }
    }
}
