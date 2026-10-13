using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 农机配置弹窗（点设备 gizmo「配置农场」打开）。
    ///
    /// 步骤化引导（PM 2026-09-07）：
    /// ① 选择作物 → ② 划定种植区（整片建区 / 画笔圈地，未选作物时禁用）→ ③ 管理（清除）。
    ///
    /// 布局纪律（PM 2026-09-07 第二轮：禁止细节挤兑/溢出）：
    /// - <b>零魔法行高</b>：每个文本块按 <c>Text.CalcHeight</c> 实测高度排版；
    /// - <b>常驻摘要行</b>：步骤②下的提示区不因状态增删而跳动——未选作物显示警告，
    ///   已选作物显示覆盖格数与产物（信息等价替换，高度按最坏文案测量）；
    /// - <b>窗口高度自适应</b>：内容需要多少给多少（clamp 320..560）；
    /// - 颜色 static readonly、Text.Anchor/GUI.color 快照 try/finally、单方法 &lt; 60 行。
    /// </summary>
    public class Dialog_AutoFarmSetup : Window
    {
        private readonly Building_AutoFarm device;
        private readonly CompAutoAgriculture comp;
        private ThingDef selectedCrop;

        /// <summary>画笔设计器单例：跨多次开窗复用，避免重复构造。</summary>
        private static Designator_AutoFarmGrowZone paintDesignator;

        // ---- 视觉常量（类级，一次分配；对齐 UI Design System 禁绘制期 new Color）----
        private static readonly Color CardBg = new Color(0.13f, 0.13f, 0.13f, 0.55f);
        private static readonly Color CardBorder = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        private static readonly Color StepBadgeBg = new Color(0.24f, 0.42f, 0.45f, 1f);
        private static readonly Color StepBadgeBgDim = new Color(0.28f, 0.28f, 0.28f, 1f);
        private static readonly Color WarnHint = new Color(0.85f, 0.65f, 0.3f, 1f);
        private static readonly Color InfoHint = new Color(0.62f, 0.68f, 0.72f, 1f);

        private const float StepBadgeSize = 22f;
        private const float CardPad = 8f;
        private const float SectionGap = 10f;
        private const float CropRowH = 36f;   // 图标 32 + 缓冲
        private const float ZoneBtnH = 34f;

        public Dialog_AutoFarmSetup(Building_AutoFarm device)
        {
            this.device = device;
            comp = device.GetComp<CompAutoAgriculture>();
            // 当前作物直接取设备缓存（SpawnSetup 时已从覆盖区种植区恢复，单口数据源）。
            selectedCrop = comp != null ? comp.CurrentCrop : null;
            doCloseX = true;
            draggable = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(480f, 420f);

        /// <summary>配置摘要行文本：未选作物=警告；已选=覆盖格数+产物（常驻占位，高度恒定语义）。</summary>
        private TaggedString SummaryHint
        {
            get
            {
                if (selectedCrop == null)
                {
                    return "PersonalChronicle.Automation.Farm.StepNeedCrop".Translate();
                }
                int cells = comp != null && comp.CoverageCells != null ? comp.CoverageCells.Count : 0;
                ThingDef product = comp != null ? comp.CurrentCropProduct : null;
                string productName = product != null ? product.LabelCap : "—";
                return "PersonalChronicle.Automation.Farm.ConfigSummary".Translate(
                    cells.ToString(), productName);
            }
        }

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
            Widgets.Label(header, "PersonalChronicle.Automation.Farm.DialogTitle".Translate());

            float innerW = inRect.width;
            float y = header.yMax + 4f;

            // ① 选择作物
            float title1H = TitleHeight("PersonalChronicle.Automation.Farm.Step1Crop".Translate(), innerW - CardPad * 2f);
            float card1H = CardPad + title1H + CropRowH + CardPad;
            Rect card1 = new Rect(inRect.x, y, innerW, card1H);
            DrawCard(card1);
            Rect c1 = card1.ContractedBy(CardPad);
            DrawStepHeader(new Rect(c1.x, c1.y, c1.width, title1H), 1,
                "PersonalChronicle.Automation.Farm.Step1Crop".Translate(), true);
            DrawCropRow(new Rect(c1.x, c1.y + title1H, c1.width, CropRowH));
            y = card1.yMax + SectionGap;

            // ② 划定种植区（警告/摘要行常驻——高度按当前文案实测，但不增删节点）
            float title2H = TitleHeight("PersonalChronicle.Automation.Farm.Step2Zone".Translate(), innerW - CardPad * 2f);
            TaggedString summary = SummaryHint;
            Text.Font = GameFont.Tiny;
            float summaryH = Mathf.Max(18f, Text.CalcHeight(summary.RawText, innerW - CardPad * 2f));
            Text.Font = GameFont.Small;
            float card2H = CardPad + title2H + ZoneBtnH + 6f + summaryH + CardPad;
            Rect card2 = new Rect(inRect.x, y, innerW, card2H);
            DrawCard(card2);
            Rect c2 = card2.ContractedBy(CardPad);
            DrawStepHeader(new Rect(c2.x, c2.y, c2.width, title2H), 2,
                "PersonalChronicle.Automation.Farm.Step2Zone".Translate(), selectedCrop != null);
            DrawZoneButtons(new Rect(c2.x, c2.y + title2H, c2.width, ZoneBtnH));
            DrawSummaryHint(new Rect(c2.x, c2.y + title2H + ZoneBtnH + 6f, c2.width, summaryH), summary,
                warn: selectedCrop == null);
            y = card2.yMax + SectionGap;

            // ③ 管理种植区
            float title3H = TitleHeight("PersonalChronicle.Automation.Farm.Step3Manage".Translate(), innerW - CardPad * 2f);
            float card3H = CardPad + title3H + 30f + CardPad;
            Rect card3 = new Rect(inRect.x, y, innerW, card3H);
            DrawCard(card3);
            Rect c3 = card3.ContractedBy(CardPad);
            DrawStepHeader(new Rect(c3.x, c3.y, c3.width, title3H), 3,
                "PersonalChronicle.Automation.Farm.Step3Manage".Translate(), true);
            if (Widgets.ButtonText(new Rect(c3.x, c3.y + title3H, c3.width, 30f),
                    "PersonalChronicle.Automation.Farm.Clear".Translate()))
            {
                RemoveExisting();
                Close();
            }
            y = card3.yMax + SectionGap;

            // 关闭（宽随文案实测，右对齐）
            TaggedString closeLabel = "PersonalChronicle.Automation.Tab.Close".Translate();
            Vector2 closeSize = Text.CalcSize(closeLabel.RawText);
            float closeW = Mathf.Max(90f, closeSize.x + 24f);
            if (Widgets.ButtonText(new Rect(inRect.xMax - closeW, y, closeW, 30f), closeLabel.RawText))
            {
                Close();
            }

            // 窗口高度自适应：内容需要多少给多少（避免固定高度造成底部空白或溢出）。
            float needed = Mathf.Clamp(y + 30f + 6f, 330f, 560f);
            if (Mathf.Abs(windowRect.height - needed) > 2f)
            {
                windowRect.height = needed;
            }
        }

        /// <summary>步骤标题实测高度（徽章 22 与文字换行高度取大者；长语言自动扩行）。</summary>
        private static float TitleHeight(TaggedString title, float width)
        {
            float textH = Text.CalcHeight(title.RawText, width - StepBadgeSize - 8f);
            return Mathf.Max(StepBadgeSize, textH);
        }

        /// <summary>卡片容器：半透明底 + 细边框（克制配色，弱化窗口 chrome、突出内容）。</summary>
        private static void DrawCard(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, CardBg);
            Widgets.DrawBox(rect);
        }

        /// <summary>步骤标题行：数字徽章 + 标题。未就绪时徽章转灰（状态一目了然）。</summary>
        private static void DrawStepHeader(Rect rect, int step, TaggedString title, bool ready)
        {
            float badgeY = rect.y + Mathf.Max(0f, (rect.height - StepBadgeSize) / 2f);
            Rect badge = new Rect(rect.x, badgeY, StepBadgeSize, StepBadgeSize);
            Color prev = GUI.color;
            try
            {
                GUI.color = ready ? StepBadgeBg : StepBadgeBgDim;
                Widgets.DrawBoxSolid(badge, Color.white);
                TextAnchor anchor = Text.Anchor;
                GameFont font = Text.Font;
                try
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(badge, step.ToString());
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Text.Font = GameFont.Small;
                    Widgets.Label(
                        new Rect(badge.xMax + 8f, rect.y, rect.width - badge.width - 8f, rect.height), title);
                }
                finally
                {
                    Text.Anchor = anchor;
                    Text.Font = font;
                }
            }
            finally
            {
                GUI.color = prev;
            }
        }

        /// <summary>作物行：图标 + 当前作物 + 「选择作物…」按钮（打开网格选择窗口）。</summary>
        private void DrawCropRow(Rect rect)
        {
            float iconSize = 32f;
            Rect iconRect = new Rect(rect.x + 4f, rect.y + (rect.height - iconSize) / 2f, iconSize, iconSize);
            float btnW = Mathf.Min(132f, rect.width * 0.42f);
            Rect btnRect = new Rect(rect.xMax - btnW, rect.y + (rect.height - 28f) / 2f, btnW, 28f);
            Rect textRect = new Rect(
                iconRect.xMax + 8f, rect.y,
                rect.width - iconRect.width - btnW - 16f, rect.height);
            if (selectedCrop != null)
            {
                Widgets.DefIcon(iconRect, selectedCrop);
            }
            TaggedString name = selectedCrop != null
                ? selectedCrop.LabelCap
                : "PersonalChronicle.Automation.Farm.NoCrop".Translate();
            TextAnchor anchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.Font = GameFont.Small;
                // 自动换行：作物名过长时折行而不是溢出/截断。
                Widgets.Label(textRect, "PersonalChronicle.Automation.Farm.CurrentCrop".Translate(name));
            }
            finally
            {
                Text.Anchor = anchor;
                Text.Font = GameFont.Small;
            }
            if (Widgets.ButtonText(btnRect, "PersonalChronicle.Automation.Farm.ChooseCrop".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ChooseCrop(selectedCrop, OnCropChosen));
            }
        }

        /// <summary>划定种植区按钮组：整片建区（一键铺满覆盖区）/ 画笔圈地（自由圈选）。
        /// 未选作物时 active=false（1.6 签名：drawBackground/doMouseoverSound/active，
        /// 反射核实——无旧版 disabled 参数，active=false 即灰态不可点）。</summary>
        private void DrawZoneButtons(Rect rect)
        {
            float bw = (rect.width - 12f) / 2f;
            bool ready = selectedCrop != null;
            Rect whole = new Rect(rect.x, rect.y, bw, rect.height);
            Rect paint = new Rect(rect.x + bw + 12f, rect.y, bw, rect.height);
            TaggedString wholeLabel = "PersonalChronicle.Automation.Farm.WholeCoverage".Translate();
            TaggedString paintLabel = "PersonalChronicle.Automation.Farm.PaintZone".Translate();
            if (Widgets.ButtonText(whole, wholeLabel.RawText, active: ready) && ready)
            {
                CreateZoneWhole();
            }
            if (Widgets.ButtonText(paint, paintLabel.RawText, active: ready) && ready)
            {
                StartPaint();
            }
            // 按钮文案被截断时仍可 hover 读全文。
            TooltipHandler.TipRegion(whole, wholeLabel.RawText);
            TooltipHandler.TipRegion(paint, paintLabel.RawText);
        }

        /// <summary>步骤②提示行（常驻）：未选作物=黄色警告；已选=覆盖格数与产物摘要。</summary>
        private static void DrawSummaryHint(Rect rect, TaggedString text, bool warn)
        {
            TextAnchor anchor = Text.Anchor;
            GameFont font = Text.Font;
            Color prev = GUI.color;
            try
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = warn ? WarnHint : InfoHint;
                Widgets.Label(rect, text);
            }
            finally
            {
                Text.Anchor = anchor;
                Text.Font = font;
                GUI.color = prev;
            }
        }

        /// <summary>作物选定后：记录选择，同步设备当前作物缓存（仓储/收集过滤立即跟随），并套到覆盖区内已存在的种植区上。</summary>
        private void OnCropChosen(ThingDef crop)
        {
            selectedCrop = crop;
            comp?.SetCrop(crop);
            ApplyCropToExistingZones(crop);
        }

        private void ApplyCropToExistingZones(ThingDef crop)
        {
            Map map = device.Map;
            if (map == null || comp == null || crop == null)
            {
                return;
            }
            foreach (Zone zone in map.zoneManager.AllZones)
            {
                Zone_Growing growing = zone as Zone_Growing;
                if (growing == null)
                {
                    continue;
                }
                foreach (IntVec3 cell in growing.Cells)
                {
                    if (comp.CoverageCells.Contains(cell))
                    {
                        ((IPlantToGrowSettable)growing).SetPlantDefToGrow(crop);
                        growing.allowSow = true;
                        break;
                    }
                }
            }
        }

        /// <summary>在全部覆盖格上生成种植区并套用作物（先清掉旧的，避免叠加重复区）。</summary>
        private void CreateZoneWhole()
        {
            Map map = device.Map;
            if (map == null || comp == null || selectedCrop == null)
            {
                return;
            }
            RemoveExisting();
            Zone_Growing zone = new Zone_Growing(map.zoneManager);
            foreach (IntVec3 c in comp.CoverageCells)
            {
                zone.AddCell(c);
            }
            map.zoneManager.RegisterZone(zone);
            ((IPlantToGrowSettable)zone).SetPlantDefToGrow(selectedCrop);
            zone.allowSow = true;
            Close();
        }

        /// <summary>进入地图画笔：把目标设备与作物注入设计器并选中，随后关窗交由玩家拖拽圈地。</summary>
        private void StartPaint()
        {
            if (selectedCrop == null)
            {
                return;
            }
            RemoveExisting();
            if (paintDesignator == null)
            {
                paintDesignator = new Designator_AutoFarmGrowZone();
            }
            paintDesignator.Configure(device, selectedCrop);
            Find.DesignatorManager.Select(paintDesignator);
            Close();
        }

        /// <summary>移除与本设备覆盖区相交的全部种植区（重定义前清理）。</summary>
        private void RemoveExisting()
        {
            Map map = device.Map;
            if (map == null || comp == null)
            {
                return;
            }
            List<Zone> toRemove = new List<Zone>();
            foreach (Zone z in map.zoneManager.AllZones)
            {
                Zone_Growing gz = z as Zone_Growing;
                if (gz == null)
                {
                    continue;
                }
                foreach (IntVec3 c in gz.Cells)
                {
                    if (comp.CoverageCells.Contains(c))
                    {
                        toRemove.Add(gz);
                        break;
                    }
                }
            }
            foreach (Zone z in toRemove)
            {
                map.zoneManager.DeregisterZone(z);
            }
        }
    }
}
