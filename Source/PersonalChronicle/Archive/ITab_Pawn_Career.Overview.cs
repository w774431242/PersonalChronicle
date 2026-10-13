// 职业档案 ITab · 总览子页（partial）。
// v4 重构（前端体检）：按"单一 RPG 卡牌 + 单数据面板"范式精简：
//   · 上半区 = 一张 RPG 风格职业身份卡牌（头像 + 职称 + 专业等级进度条 + 晋升百分比
//              + 同行关键指标 + 仅显示推荐一级专业 + "规划"按钮唤出界面）。
//   · 下半区 = 数据面板，展示当前人物的原版属性（技能/激情/特质/需求，直读 pawn）。
//   · 其余分组（资格状态/预检/下一职称）已迁移到对应子页，不堆在总览。
// 设计系统：全部经 UIComponents + UITheme；禁止散落 GUI.color / new Color。
//
// [普检 2026-09-02] 范围：六宫格职业总览卡（块1 头像 / 块2 职称+晋升% / 块3 等级条 /
//   块4 KPI 四联 / 块5 专业成长蓝图 / 块6 主方向 chip+规划按钮）+ 辅助方法
//   （CalcGuideRowHeight / TryBuildGuide / DrawGuideXpChips / DrawGuideRatingLadder /
//   DrawRatingLadderCell / FitValueFont / DrawQualCell / CalcOverviewHeight）。
//   对照 rimworld-ui-standards §2~§6。
//   修复：① NRE 风险——dirDef.skillDefNames 空护（块3、ReadPrimarySkillRatingLabel 两处）；
//         ② §2.1 绘制态泄漏——CalcChipWidth 设 Text.Font 未恢复，加 try/finally；
//         ③ §6 魔法像素——KPI 第二行高度 18f → UITheme.FontBodyLineHeight。
//   遗留（待重构，未擅自大改）：§5 边界——块3 / ReadPrimarySkillRatingLabel 仍窗口内挖
//   po.CareerData.Professional 取等级/技能名/评级，应聚合进 CareerOverviewView 由 Read Model 提供。
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Application.ReadModels;
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
    public partial class ITab_Pawn_Career
    {
        // 卡片内边距（紧凑高信息率）
        private const float OvCardPadX = 10f;
        private const float OvCardPadY = 8f;
        // 卡片之间强制垂直间距
        private const float OvCardGap = 12f;

        // ================= 总览主入口（单卡牌 + 单数据面板）=================
        private float DrawOverviewTab(Rect rect, Pawn pawn, DetailSnapshot snap)
        {
            CareerOverviewView ov = snap != null ? snap.CareerOverview : null;
            float width = rect.width - 16f;
            float viewH = CalcOverviewHeight(ov, pawn);
            float viewHeight = Mathf.Max(rect.height, viewH);
            // viewRect 必须用内容相对坐标 (0,0)，否则滚动偏移会与窗口绝对坐标叠加。
            Rect viewRect = new Rect(0f, 0f, width, viewHeight);
            Widgets.BeginScrollView(rect, ref scroll, viewRect);
            try
            {
                float y = 0f;
                // 上半区：RPG 职业身份卡牌（含头像/职称/等级条/晋升%/指标/规划按钮）
                y = DrawCareerCard(viewRect, y, width, pawn, ov);
                y += OvCardGap;
                // 下半区：原版属性数据面板
                y = DrawAttributePanel(viewRect, y, width, pawn);
                // 属性卡片「透明结构」悬停浮层：必须最后绘制才能盖在所有卡片之上，
                // 且仍在滚动组内（与卡片同坐标系），故需按可视区钳制位置防裁剪。
                // 实现在 ITab_Pawn_Career.Breakdown.cs（铁律 6.3 预划分 partial）。
                DrawBreakdownTooltip(rect, scroll);
            }
            finally
            {
                Widgets.EndScrollView();
            }
            return viewH;
        }

        // ================= 上半区：RPG 职业身份卡牌 =================
        // 单卡承载：头像(pawn 原生渲染) + 职称 + 专业等级进度条 + 晋升% (职称右侧)
        //          + 同行关键指标(工时/制造/论文/评级…) + 仅显示推荐一级专业 + [规划]按钮
        //          + 「专业成长蓝图」一行（XP 来源 chip 链 + 每档评级加成阶梯表）
        private float DrawCareerCard(Rect view, float y, float width, Pawn pawn, CareerOverviewView ov)
        {
            float padX = OvCardPadX;
            float padY = OvCardPadY;
            float cardX = view.x + 0f;
            float innerX = cardX + padX + 4f;            // 留 4f 左边条
            float innerW = width - (padX + 4f) * 2f;
            // padX 只用于 innerX / innerW 偏移（下方 helper 不再需要它），但保留为成本最低的"双坐标系共享值"，便于后续块独立编辑。
            _ = padX;

            // ---- 卡牌总高（自底向上累加；与 CalcOverviewHeight / 各 helper 同步）----
            float portraitSz = 64f;
            float rowTitleH = 28f;
            float rowLevelH = 18f;
            float rowStatH  = 40f;
            float rowGuideH = CalcGuideRowHeight(innerW, pawn);
            float rowMajorH = 90f;   // 块6：主方向名+按钮(26) / 获取XP工作(20) / 主修加成网格(36) + 间距
            float cardH = padY + portraitSz + 8f + rowTitleH + 6f + rowLevelH + 6f + rowStatH + 6f
                + rowGuideH + 6f + rowMajorH + padY;

            Rect card = new Rect(cardX, y, width, cardH);
            UIComponents.TintedBox(card, UITheme.PanelRaised);
            UIComponents.Border(card, UITheme.BorderSoft);
            // 左边条（按当前主方向颜色着色，未选则用默认 Accent）
            Color accent = TryGetDirectionDef(cachedSnapshot) is ProfessionalDirectionDef d
                ? ParseHex(d.colorHex, UITheme.Accent)
                : UITheme.Accent;
            UIComponents.TintedBox(new Rect(card.x, card.y, 4f, card.height), accent);

            // 1) 头像 + 2) 职称/晋升% + 3) 等级进度条 —— 三段挤在一行内
            Rect titleR, lvlLabel, barR;
            float textW;
            float byIdentity = DrawCareerIdentityBlock(card, padX, padY, innerX, innerW, pawn, ov,
                rowTitleH, rowLevelH,
                out titleR, out lvlLabel, out barR, out textW);

            // 4) 同行 KPI 4 联（沿用 4 个 statKeys 文本）
            float byKpi = DrawCareerKpiBlock(textX: lvlLabel.x,
                by: byIdentity + 6f,
                textW: textW,
                rowStatH: rowStatH,
                ov: ov, profRatingText: ReadPrimarySkillRatingLabel(pawn));

            // 5) 专业成长蓝图：XP 来源 chip 链 + 4 档评级加成阶梯表（无数据则自动隐藏整行）
            float byGuide = byKpi + 6f;
            DrawCareerGuideBlock(new Rect(innerX, byGuide, innerW, rowGuideH), pawn);

            // 6) 已选主方向 / 推荐 chip + 规划按钮
            float byMajor = byGuide + rowGuideH + 6f;
            DrawCareerMajorBlock(new Rect(innerX, byMajor, innerW, rowMajorH), pawn);
            return y + cardH;
        }

        // ───────── 块 1+2+3：头像 + 职称 + 等级条（合一行）─────────
        // 拆出来为 helper：draws identity / level 条 + planBtn 弹出 用到的核心字段（textX/textW/levelLabel/level bar）。
        private float DrawCareerIdentityBlock(Rect card, float padX, float padY, float innerX, float innerW,
            Pawn pawn, CareerOverviewView ov,
            float rowTitleH, float rowLevelH,
            out Rect titleR, out Rect lvlLabel, out Rect barR, out float textW)
        {
            float portraitSz = 64f;
            // 1) 头像（原生 pawn 渲染）
            float by = card.y + padY;
            Rect portrait = new Rect(innerX, by, portraitSz, portraitSz);
            if (pawn != null)
            {
                GUI.DrawTexture(portrait, PortraitsCache.Get(pawn, portrait.size, Rot4.South, default, 1f));
                UIComponents.Border(portrait, UITheme.Border);
            }
            textX_ = portrait.xMax + 10f;
            textW_ = innerW - portraitSz - 10f;
            float textX = textX_;
            textW = textW_;

            // 2) 职称 + 晋升% （同行：职称左，晋升%右）
            string role = string.IsNullOrEmpty(ov?.RoleName)
                ? "PersonalChronicle.UI.Career.Ov.Undefined".Translate().ToString()
                : ov.RoleName;
            int promotePct = ov != null ? ov.Progress : 0;
            titleR = new Rect(textX, by, textW - 64f, rowTitleH);
            UIComponents.Label(titleR, UIComponents.TruncateToWidth(role, titleR.width, GameFont.Medium),
                UITheme.FontValue, ReadAccentForPrimary(pawn), TextAnchor.MiddleLeft);
            Rect promoR = new Rect(titleR.xMax, by, 64f, rowTitleH);
            UIComponents.Label(promoR, promotePct + "%", UITheme.FontValue, UITheme.PillGold, TextAnchor.MiddleRight);
            by += rowTitleH + 6f;

            // 3) 专业等级进度条
            int profLevel = 0;
            string profName = "—";
            if (pawn != null && GetPawnObject(pawn) is PawnObject po && po.CareerData?.Professional != null)
            {
                string pd = po.CareerData.Professional.primaryDirection;
                ProfessionalDirectionDef dirDef = !string.IsNullOrEmpty(pd)
                    ? DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(pd) : null;
                string skillDefName = dirDef != null && dirDef.skillDefNames != null && dirDef.skillDefNames.Count > 0 ? dirDef.skillDefNames[0] : null;
                if (!string.IsNullOrEmpty(skillDefName))
                {
                    ProfessionalSkillData sd = po.CareerData.Professional.GetSkill(skillDefName);
                    profLevel = sd != null ? sd.level : 0;
                    string skillLabelKey = "PersonalChronicle.Professional.Skill." + skillDefName + ".Label";
                    string translated = skillLabelKey.Translate().ToString();
                    profName = (translated != null && translated != skillLabelKey) ? translated : skillDefName;
                }
            }
            lvlLabel = new Rect(textX, by, 200f, rowLevelH);
            UIComponents.Label(lvlLabel, "Lv." + profLevel + " " + profName,
                UITheme.FontLabel, UITheme.Text, TextAnchor.MiddleLeft);
            barR = new Rect(lvlLabel.xMax + 6f, by + 3f, (textX + textW) - lvlLabel.xMax - 6f, 10f);
            UIComponents.ProgressBar(barR, Mathf.Clamp01((float)profLevel / 50f), ReadAccentForPrimary(pawn));
            // 悬停：主方向逐项加成明细
            ProfessionalDirectionDef hoverDir = ChosenDirectionDef();
            if (hoverDir != null)
            {
                TooltipHandler.TipRegion(new Rect(textX, by, textW, rowLevelH),
                    new TipSignal(Dialog_SetPrimaryDirection.BuildDirectionBonusTip(hoverDir)));
            }
            return by + rowLevelH;
        }

        // 拆出 helper 用：组件字段式 textX / textW（KPI 块与 Guide 块同坐标系消费）
        private float textX_;
        private float textW_;

        // 取主方向 accent；缺方向 fallback Accent；供身份块左条/职称色/进度条色统一取色
        private Color ReadAccentForPrimary(Pawn pawn)
        {
            if (TryGetDirectionDef(cachedSnapshot) is ProfessionalDirectionDef d)
            {
                return ParseHex(d.colorHex, UITheme.Accent);
            }
            return UITheme.Accent;
        }

        // 读取主方向首个关联技能的评级名（"初级"/"熟练"/…/无）
        private string ReadPrimarySkillRatingLabel(Pawn pawn)
        {
            if (pawn == null) return "—";
            PawnObject po = GetPawnObject(pawn);
            string pd = po?.CareerData?.Professional?.primaryDirection;
            ProfessionalDirectionDef dirDef = !string.IsNullOrEmpty(pd)
                ? DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(pd) : null;
            string skillDefName = dirDef != null && dirDef.skillDefNames != null && dirDef.skillDefNames.Count > 0 ? dirDef.skillDefNames[0] : null;
            if (string.IsNullOrEmpty(skillDefName) || po?.CareerData?.Professional == null) return "—";
            ProfessionalSkillData sd = po.CareerData.Professional.GetSkill(skillDefName);
            ProfessionalRatingDef rd = ProfessionalRatingEvaluator.ResolveRating(sd != null ? sd.level : 0);
            return rd != null ? rd.LabelCap : "—";
        }

        // ───────── 块 4：4 联 KPI（工时/产出/著作/评级）─────────
        private float DrawCareerKpiBlock(float textX, float by, float textW, float rowStatH,
            CareerOverviewView ov, string profRatingText)
        {
            string hoursText = ov != null && !string.IsNullOrEmpty(ov.HoursText) ? ov.HoursText : "0 h";
            string[] statVals =
            {
                hoursText,
                FormatValue(ov?.Made ?? 0),
                FormatValue(ov?.Books ?? 0),
                profRatingText
            };
            string[] statKeys =
            {
                "PersonalChronicle.UI.Career.Ov.Metric.Hours",
                "PersonalChronicle.UI.Career.Ov.Metric.Made",
                "PersonalChronicle.UI.Career.Ov.Books",
                "PersonalChronicle.UI.Career.Ov.Rating"
            };
            float sGap = 6f;
            float sW = (textW - sGap * (statVals.Length - 1)) / statVals.Length;
            for (int i = 0; i < statVals.Length; i++)
            {
                Rect cell = new Rect(textX + i * (sW + sGap), by, sW, rowStatH);
                UIComponents.TintedBox(cell, UITheme.Card);
                UIComponents.Label(new Rect(cell.x, cell.y, cell.width, UITheme.FontBodyLineHeight),
                    UIComponents.TruncateToWidth(statVals[i], cell.width, GameFont.Small),
                    UITheme.FontBody, UITheme.Text, TextAnchor.MiddleCenter);
                UIComponents.Label(new Rect(cell.x, cell.y + UITheme.FontBodyLineHeight, cell.width, UITheme.FontBodyLineHeight),
                    UIComponents.TruncateToWidth(statKeys[i].Translate().ToString(), cell.width, GameFont.Tiny),
                    UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleCenter);
            }
            return by + rowStatH;
        }

        // ───────── 块 5：专业成长蓝图（XP 来源 + 评级档位表）─────────
        // 数据来源：ProfessionalGuideSnapshotBuilder（Read Model，单一真源 Resolver）。
        // 无数据时整行折叠不绘制，自然走回原卡片高度。
        private float CalcGuideRowHeight(float innerW, Pawn pawn)
        {
            ProfessionalGuideView guide = TryBuildGuide(pawn);
            // 始终占一行：已配置方向画完整蓝图，未配置方向画「暂未配置」提示（数据缺漏可见化，不折叠隐藏）。
            if (guide == null) return 0f;
            return GuideRowH;
        }

        private const float GuideRowH = 80f;   // XP 来源 chip 链 + 4 档评级表 一行紧凑高度（与 .Breakdown 一致行高经验）

        private ProfessionalGuideView TryBuildGuide(Pawn pawn)
        {
            if (pawn == null) return null;
            ProfessionalDirectionDef dir = ChosenDirectionDef();
            if (dir == null)
            {
                // 未选主方向 → 用推荐第一 fit 的 Major 兜底（保持信息密度，避免空卡）
                if (fitResults != null && fitResults.Count > 0 && fitResults[0] != null)
                {
                    string pk = fitResults[0].Major;
                    string firstDir = FirstDirectionForMajor(pk);
                    if (!string.IsNullOrEmpty(firstDir))
                    {
                        dir = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(firstDir);
                    }
                }
            }
            return ProfessionalGuideSnapshotBuilder.Build(pawn, dir);
        }

        private static string FirstDirectionForMajor(string majorKey)
        {
            if (string.IsNullOrEmpty(majorKey)) return null;
            List<ProfessionalDirectionDef> all = DefDatabase<ProfessionalDirectionDef>.AllDefsListForReading;
            if (all == null || all.Count == 0) return null;
            ProfessionalDirectionDef best = null;
            for (int i = 0; i < all.Count; i++)
            {
                ProfessionalDirectionDef d = all[i];
                if (d == null || d.profession != majorKey) continue;
                if (best == null || d.order < best.order) best = d;
            }
            return best != null ? best.defName : null;
        }

        private void DrawCareerGuideBlock(Rect rect, Pawn pawn)
        {
            ProfessionalGuideView guide = TryBuildGuide(pawn);
            if (guide == null) return;
            UIComponents.TintedBox(rect, UITheme.Card);

            // 方向未配置专业技能 Def（底层数据缺漏：方向引用的技能 Def 未定义）→ 显示提示行，
            // 不崩溃、不误导为 0% 加成。
            if (!guide.DirectionConfigured)
            {
                UIComponents.Label(new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height),
                    "PersonalChronicle.UI.Career.Ov.Guide.NotConfigured".Translate().ToString(),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleCenter);
                return;
            }

            float padInnerX = 2f;
            float padInnerY = 4f;
            float innerX = rect.x + padInnerX;
            float innerY = rect.y + padInnerY;
            float innerW = rect.width - padInnerX * 2f;
            float innerH = rect.height - padInnerY * 2f;

            // 上半：XP 来源 chip 链（"做什么拿 XP"）
            float halfH = (innerH - 4f) / 2f;
            Rect xpRect = new Rect(innerX, innerY, innerW, halfH);
            DrawGuideXpChips(xpRect, guide);

            // 下半：4 档评级阶梯表
            Rect rateRect = new Rect(innerX, innerY + halfH + 4f, innerW, halfH);
            DrawGuideRatingLadder(rateRect, guide);
        }

        // ───────── 子 helper：XP 来源 chip 链（左对齐 + 自动换行）─────────
        private const float GuideChipPadX = 6f;
        private const float GuideChipH = 18f;
        private const float GuideChipGap = 4f;

        private void DrawGuideXpChips(Rect rect, ProfessionalGuideView guide)
        {
            // 左侧标签：占用固定 64f 宽
            const float labelW = 64f;
            UIComponents.Label(new Rect(rect.x, rect.y, labelW, GuideChipH),
                "PersonalChronicle.UI.Career.Ov.Guide.XpFrom".Translate().ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);

            // 右侧可用区
            float chipsX = rect.x + labelW + 4f;
            float chipsW = Mathf.Max(0f, rect.width - (chipsX - rect.x));
            float chipsY = rect.y + (rect.height - GuideChipH) / 2f;
            float cursor = chipsX;

            // 优先 Skill → WorkType → Recipe；空状态显示 "[ — ]"
            List<ProfessionalXpSourceChip> chips = guide.XpSources?.Chips;
            int count = chips != null ? chips.Count : 0;
            if (count == 0)
            {
                UIComponents.Label(new Rect(cursor, chipsY, chipsW, GuideChipH),
                    "PersonalChronicle.UI.Career.Ov.Guide.XpFromEmpty".Translate().ToString(),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                return;
            }

            // 单次 +XP 数字放在最后
            for (int i = 0; i < count; i++)
            {
                ProfessionalXpSourceChip ch = chips[i];
                if (ch == null) continue;
                string text = ch.Label ?? ch.DefName ?? "";
                float w = CalcChipWidth(text);
                if (cursor + w > chipsX + chipsW - 90f)   // 给尾部 +XP 留位
                {
                    if (cursor > chipsX + 4f) break;
                }
                DrawGuideChip(new Rect(cursor, chipsY, w, GuideChipH), text, ch.Kind);
                cursor += w + GuideChipGap;
            }

            // 末尾："+10 XP/次"——只在有数据时绘
            if (guide.XpSources.XpPerPractice > 0f)
            {
                string xpText = "+" + Mathf.RoundToInt(guide.XpSources.XpPerPractice)
                    + " XP/" + "PersonalChronicle.UI.Career.Ov.Guide.XpPerPractice".Translate();
                float xpW = CalcChipWidth(xpText);
                float xpX = Mathf.Min(chipsX + chipsW - xpW, Mathf.Max(cursor + GuideChipGap, chipsX));
                DrawGuideChip(new Rect(xpX, chipsY, xpW, GuideChipH), xpText, "Xp");
            }
        }

        private static float CalcChipWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 24f;
            GameFont prev = Verse.Text.Font;
            try
            {
                Verse.Text.Font = GameFont.Tiny;
                return Mathf.Min(160f, Mathf.Max(28f, Verse.Text.CalcSize(text).x + GuideChipPadX * 2f));
            }
            finally
            {
                Verse.Text.Font = prev;
            }
        }

        private void DrawGuideChip(Rect r, string text, string kind)
        {
            Color c = UITheme.Card;
            Color textColor = UITheme.Text;
            switch (kind)
            {
                case "Skill": c = UITheme.Card; textColor = UITheme.Text; break;
                case "WorkType": c = UITheme.PanelRaised; textColor = UITheme.Accent; break;
                case "Recipe": c = UITheme.PanelRaised; textColor = UITheme.Alive; break;
                case "Xp": c = UITheme.PillGold; textColor = UITheme.Accent; break;
            }
            UIComponents.TintedBox(r, c);
            UIComponents.Border(r, UITheme.BorderSoft);
            UIComponents.Label(new Rect(r.x + GuideChipPadX, r.y, r.width - GuideChipPadX * 2f, r.height),
                UIComponents.TruncateToWidth(text, r.width - GuideChipPadX * 2f, GameFont.Tiny),
                GameFont.Tiny, textColor, TextAnchor.MiddleCenter);
        }

        // ───────── 子 helper：4 档评级阶梯表（横向 4 列）─────────
        private void DrawGuideRatingLadder(Rect rect, ProfessionalGuideView guide)
        {
            const float labelW = 64f;
            UIComponents.Label(new Rect(rect.x, rect.y, labelW, GuideChipH),
                "PersonalChronicle.UI.Career.Ov.Guide.Ratings".Translate().ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);

            float cellsX = rect.x + labelW + 4f;
            float cellsW = Mathf.Max(0f, rect.width - (cellsX - rect.x));
            float cellsY = rect.y;
            float cellsH = rect.height;

            List<ProfessionalRatingLadderRow> rows = guide.RatingLadder;
            int n = rows != null ? rows.Count : 0;
            if (n == 0)
            {
                UIComponents.Label(new Rect(cellsX, cellsY, cellsW, cellsH),
                    "PersonalChronicle.UI.Career.Ov.Guide.RatingsEmpty".Translate().ToString(),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                return;
            }
            float gap = 4f;
            float cellW = (cellsW - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                ProfessionalRatingLadderRow row = rows[i];
                float cx = cellsX + i * (cellW + gap);
                Rect cell = new Rect(cx, cellsY, cellW, cellsH);
                UIComponents.TintedBox(cell, i == 0 ? UITheme.Card : UITheme.PanelRaised);
                UIComponents.Border(cell, UITheme.BorderSoft);
                DrawRatingLadderCell(cell, row);
            }
        }

        private void DrawRatingLadderCell(Rect cell, ProfessionalRatingLadderRow row)
        {
            if (row == null) return;
            // 顶：评级名 + minLevel（小行）
            string title = (row.Label ?? row.RatingDefName ?? "") + " Lv≥" + row.MinLevel;
            UIComponents.Label(new Rect(cell.x, cell.y, cell.width, GuideChipH),
                UIComponents.TruncateToWidth(title, cell.width, GameFont.Tiny),
                GameFont.Tiny, UITheme.Text, TextAnchor.MiddleCenter);
            // 底：速度% + 通用加成% + 品质档（缺类显示"—"而非误导性的 0%/+0▲）
            string speedStr = row.HasSpeed ? ("+" + Mathf.RoundToInt(row.SpeedPct) + "%") : "—";
            string genericStr = row.HasGeneric ? ("+" + Mathf.RoundToInt(row.GenericPct) + "%") : "—";
            string qualityStr = row.HasQuality ? ((row.QualityLevels >= 0 ? "+" : "") + row.QualityLevels + "▲") : "—";
            string body = speedStr + " · " + genericStr + " · " + qualityStr;
            UIComponents.Label(new Rect(cell.x, cell.y + GuideChipH, cell.width, cell.height - GuideChipH),
                UIComponents.TruncateToWidth(body, cell.width, GameFont.Tiny),
                GameFont.Tiny, UITheme.PillGold, TextAnchor.MiddleCenter);
        }

        // ───────── 块 6：主方向 chip + 规划按钮 + 明确 XP 来源工作 + 数字加成一行 ─────────
        // 行A 主方向名+已选/推荐 Badge+规划按钮；行B「获取XP需做哪些具体工作」；行C「主修方向数字加成」一行。
        // 数据来源：TryBuildGuide（Read Model 单一真源，与块5 专业成长蓝图同口径，避免双源漂移）。
        private void DrawCareerMajorBlock(Rect rect, Pawn pawn)
        {
            ProfessionalGuideView guide = TryBuildGuide(pawn);
            const float rowA = 26f;
            const float gap = 4f;
            const float rowB = 20f;
            const float rowC = 36f;   // 主修加成多列网格（最多 4 项 / 2 列）

            // 行A：主方向名 + 已选/推荐 Badge + 规划按钮
            ProfessionalDirectionDef chosen = ChosenDirectionDef();
            ProfessionalFitResult topFit = (fitResults != null && fitResults.Count > 0) ? fitResults[0] : null;
            string shownMajor;
            string badgeText;
            Color badgeColor;
            if (chosen != null && !string.IsNullOrEmpty(chosen.profession))
            {
                shownMajor = MajorLabel(chosen.profession);
                badgeText = "PersonalChronicle.UI.Career.Ov.Chosen".Translate();
                badgeColor = UITheme.PillGold;
            }
            else
            {
                shownMajor = topFit != null ? MajorLabel(topFit.Major) : "—";
                badgeText = "PersonalChronicle.UI.Career.Ov.Recommend".Translate();
                badgeColor = UITheme.Alive;
            }
            float btnW = 70f;
            Rect recR = new Rect(rect.x, rect.y, rect.width - btnW - 6f, rowA);
            UIComponents.TintedBox(recR, UITheme.Card);
            UIComponents.Badge(new Rect(recR.x + 4f, recR.y + 4f, 36f, rowA - 8f), badgeText, badgeColor);
            UIComponents.Label(new Rect(recR.x + 44f, rect.y, recR.width - 48f, rowA),
                UIComponents.TruncateToWidth(shownMajor, recR.width - 48f, GameFont.Small),
                UITheme.FontLabel, UITheme.Text, TextAnchor.MiddleLeft);
            if (chosen != null)
            {
                TooltipHandler.TipRegion(recR, new TipSignal(Dialog_SetPrimaryDirection.BuildDirectionBonusTip(chosen)));
            }
            Rect planBtn = new Rect(recR.xMax + 6f, rect.y, btnW, rowA);
            if (Widgets.ButtonText(planBtn, "PersonalChronicle.UI.Career.Ov.Plan".Translate().ToString()))
            {
                string filterProf = chosen != null ? chosen.profession
                    : (topFit != null ? topFit.Major : (MajorKeys.Count > 0 ? MajorKeys[0] : null));
                Find.WindowStack.Add(new Dialog_SetPrimaryDirection(
                    pawn,
                    filterProf,
                    chosen != null ? chosen.defName : currentMajorKey,
                    () => { scroll = Vector2.zero; }));
                Event.current.Use();
            }

            // 行B：获取XP需做的具体工作（来自 guide.XpSources）
            DrawMajorXpWorks(new Rect(rect.x, rect.y + rowA + gap, rect.width, rowB), guide);

            // 行C：主修方向数字加成一行（来自 guide.RatingLadder 满级档）
            DrawMajorBonus(new Rect(rect.x, rect.y + rowA + gap + rowB + gap, rect.width, rowC), guide);
        }

        // 行B：明确标注「当前主修方向需要做哪些具体工作来获取XP」（取前 3 个 XP 来源 chip 名，用 · 连接）。
        private void DrawMajorXpWorks(Rect rect, ProfessionalGuideView guide)
        {
            const float labelW = 64f;
            UIComponents.TintedBox(rect, UITheme.Card);
            UIComponents.Label(new Rect(rect.x, rect.y, labelW, rect.height),
                "PersonalChronicle.UI.Career.Ov.Major.XpWorks".Translate().ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            float txtX = rect.x + labelW + 4f;
            float txtW = Mathf.Max(0f, rect.width - labelW - 4f);
            List<ProfessionalXpSourceChip> chips = guide != null && guide.XpSources != null ? guide.XpSources.Chips : null;
            if (chips == null || chips.Count == 0)
            {
                UIComponents.Label(new Rect(txtX, rect.y, txtW, rect.height),
                    "PersonalChronicle.UI.Career.Ov.Guide.XpFromEmpty".Translate().ToString(),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                return;
            }
            int take = Mathf.Min(3, chips.Count);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < take; i++)
            {
                ProfessionalXpSourceChip ch = chips[i];
                if (ch == null) continue;
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(ch.Label ?? ch.DefName ?? "");
            }
            UIComponents.Label(new Rect(txtX, rect.y, txtW, rect.height),
                UIComponents.TruncateToWidth(sb.ToString(), txtW, GameFont.Tiny),
                GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
        }

        // 行C：主修加成多列网格（按当前已解锁档位数量展示，最多 4 项；满级大师口径数值）。
        private void DrawMajorBonus(Rect rect, ProfessionalGuideView guide)
        {
            const float labelW = 64f;
            UIComponents.TintedBox(rect, UITheme.Card);
            UIComponents.Label(new Rect(rect.x, rect.y, labelW, rect.height),
                "PersonalChronicle.UI.Career.Ov.Major.Bonus".Translate().ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);

            float txtX = rect.x + labelW + 4f;
            float txtW = Mathf.Max(0f, rect.width - labelW - 4f);
            List<ProfessionalMajorBonusItem> all = guide != null ? guide.MajorBonuses : null;
            if (all == null || all.Count == 0)
            {
                UIComponents.Label(new Rect(txtX, rect.y, txtW, rect.height), "—", GameFont.Tiny, UITheme.PillGold, TextAnchor.MiddleLeft);
                return;
            }
            // 每升一档 +1 个，大师档共 4 个（未学则该方向未达解锁档位）。
            int showCount = Mathf.Min(guide.UnlockedTierCount, all.Count, 4);
            if (showCount <= 0)
            {
                UIComponents.Label(new Rect(txtX, rect.y, txtW, rect.height),
                    "PersonalChronicle.UI.Career.Ov.Major.Locked".Translate().ToString(),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }

            const int cols = 2;
            float lineH = 16f;
            int rowCount = (showCount + cols - 1) / cols;
            float gridH = rowCount * lineH;
            float gridY0 = rect.y + (rect.height - gridH) / 2f;
            float colW = txtW / cols;
            for (int i = 0; i < showCount; i++)
            {
                ProfessionalMajorBonusItem it = all[i];
                int r = i / cols;
                int c = i % cols;
                float x = txtX + c * colW;
                float y = gridY0 + r * lineH;
                UIComponents.Label(new Rect(x, y, colW - 4f, lineH),
                    UIComponents.TruncateToWidth(MajorBonusText(it), colW - 4f, GameFont.Tiny),
                    GameFont.Tiny, UITheme.PillGold, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>主修加成单项文本：Stat名 + 带符号百分比（isReduction 已翻为负）。</summary>
        private static string MajorBonusText(ProfessionalMajorBonusItem it)
        {
            if (it == null) return "—";
            string stat = it.StatLabel ?? it.EffectDefName ?? "?";
            string sign = it.ValuePct >= 0f ? "+" : "";
            return stat + " " + sign + Mathf.RoundToInt(it.ValuePct) + "%";
        }

        // ================= 下半区：原版属性数据面板（玩家可自定义字段）=================
        // 仅渲染玩家在 ChronicleSettings.OverviewPanelFields 中选中的字段；
        // 悬停态字段声明在 ITab_Pawn_Career.Breakdown.cs（铁律 6.3：partial 各自持有子功能状态）。

        // 空集合 = 注册表默认核心字段。顶部 [自定义字段] 按钮打开多选 Dialog。
        private float DrawAttributePanel(Rect view, float y, float width, Pawn pawn)
        {
            const float titleH = 22f;
            const float btnH = 22f;

            // 悬停态每帧重建：先清空，命中卡片时再赋值（IMGUI 无状态，不缓存）
            ovHoverField = null;
            ovHoverPawn = null;
            float padY = OvCardPadY;
            float padX = OvCardPadX;
            float innerX = view.x + padX;
            float innerW = width - padX * 2f;

            UIComponents.SectionTitle(new Rect(view.x, y, width, titleH), y,
                "PersonalChronicle.UI.Career.Ov.Attr".Translate());

            // [自定义字段] 按钮（右对齐于标题行）
            Rect cfgBtn = new Rect(view.x + width - 110f, y + 1f, 104f, btnH);
            if (Widgets.ButtonText(cfgBtn, "PersonalChronicle.UI.OvF.Config".Translate().ToString()))
            {
                Find.WindowStack.Add(new Dialog_OverviewFields(LoadModSettings()));
                Event.current.Use();
            }

            float by = y + titleH + 4f;

            if (pawn == null)
            {
                float h = titleH + 4f + 36f + padY * 2f;
                UIComponents.Label(new Rect(innerX, by, innerW, 36f),
                    "PersonalChronicle.UI.Career.Ov.NoData".Translate().ToString(),
                    UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
                return y + h;
            }

            HashSet<string> active = OverviewFieldRegistry.ActiveKeys(LoadModSettings()?.OverviewPanelFields);
            PawnObject po = GetPawnObject(pawn);
            CareerOverviewView ov = cachedSnapshot != null ? cachedSnapshot.CareerOverview : null;
            OverviewFieldRegistry.CurrentFitResults = fitResults;

            // 字段网格：每个字段 = 一张带边框背景的小卡（字段标签在上 / 数值在下），
            // 每行固定 5 个，卡片间固定间距，行高固定。
            const int colsPerRow = 5;
            const float cellGap = 8f;     // 卡片水平/垂直固定间距
            const float cellH = 54f;      // 单卡固定高度（Tiny 标签 18f + Medium 数值 28f + 上下 padding）

            var selected = new List<OverviewFieldRegistry>();
            foreach (var f in OverviewFieldRegistry.AllFields)
                if (active.Contains(f.Key)) selected.Add(f);

            if (selected.Count == 0)
            {
                float emptyH = 36f;
                UIComponents.Label(new Rect(innerX, by, innerW, emptyH),
                    "PersonalChronicle.UI.Career.Ov.NoData".Translate().ToString(),
                    UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
                by += emptyH;
            }
            else
            {
                float cellW = (innerW - cellGap * (colsPerRow - 1)) / colsPerRow;
                int rows = (selected.Count + colsPerRow - 1) / colsPerRow;
                for (int i = 0; i < selected.Count; i++)
                {
                    var f = selected[i];
                    int col = i % colsPerRow;
                    int row = i / colsPerRow;
                    float cx = innerX + col * (cellW + cellGap);
                    float cy = by + row * (cellH + cellGap);
                    Rect cell = new Rect(cx, cy, cellW, cellH);

                    float? val = f.GetValue(pawn, po, ov);
                    string valStr = f.GetFormattedValue(pawn, val);
                    // 字段标签（Tiny, 顶部）+ 数值（Medium→Small→Tiny 自适应，底部），统一令牌色与边框。
                    // 行高对齐中文经验：Tiny 标签 18f、Medium 数值 28f，防止中文裁剪。
                    UIComponents.TintedBox(cell, UITheme.PanelRaised);
                    UIComponents.Border(cell, UITheme.BorderSoft);
                    float cPadX = 7f;
                    float cPadY = 4f;
                    float labelH = 18f;
                    float valueH = 28f;
                    float valueW = cell.width - cPadX * 2f;
                    UIComponents.Label(new Rect(cell.x + cPadX, cell.y + cPadY, valueW, labelH),
                        UIComponents.TruncateToWidth(f.DisplayLabel, valueW, GameFont.Tiny),
                        UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
                    Color vColor = val.HasValue ? UITheme.Text : UITheme.Muted;
                    GameFont valueFont = FitValueFont(valStr, valueW);
                    UIComponents.Label(new Rect(cell.x + cPadX, cell.y + cell.height - cPadY - valueH,
                            valueW, valueH),
                        UIComponents.TruncateToWidth(valStr, valueW, valueFont),
                        valueFont, vColor, TextAnchor.MiddleLeft);
                    // 悬停探测：原生 StatDef 字段走 StatPart 管线；派生字段走自定义 breakdown 解析器
                    // （如「专业等级」展开职业数值加成）。二者任一可用即弹出浮层。
                    if (Mouse.IsOver(cell) && f.HasTooltip)
                    {
                        ovHoverField = f;
                        ovHoverCell = cell;
                        ovHoverPawn = pawn;
                    }
                }
                by += rows * cellH + (rows - 1) * cellGap;
            }

            float contentH = by - (y + titleH + 4f);
            float cardH = contentH + padY * 2f + titleH;
            return y + cardH;
        }

        private static GameFont FitValueFont(string text, float maxWidth)
        {
            GameFont prev = Verse.Text.Font;
            try
            {
                GameFont[] order = { GameFont.Medium, GameFont.Small, GameFont.Tiny };
                for (int i = 0; i < order.Length; i++)
                {
                    Verse.Text.Font = order[i];
                    if (Verse.Text.CalcSize(text ?? "").x <= maxWidth)
                    {
                        return order[i];
                    }
                }
                return GameFont.Tiny;
            }
            finally
            {
                Verse.Text.Font = prev;
            }
        }

        private ChronicleSettings LoadModSettings()
            => PersonalChronicleMod.Settings;

        // 取 PawnObject（含 CareerData），失败返回 null
        private static PawnObject GetPawnObject(Pawn p) => ChroniclePawnAccess.GetArchivedPawnObject(p);

        // ================= 高度公式（与两个区块同步）=================
        private float CalcOverviewHeight(CareerOverviewView ov, Pawn pawn)
        {
            float h = 0f;

            // 上半：RPG 卡牌（行高与 DrawCareerCard 同步，含「专业成长蓝图」自适应）
            float portraitSz = 64f;
            float rowTitleH = 28f, rowLevelH = 18f, rowStatH = 40f, rowMajorH = 22f;
            float rowGuideH = CalcGuideRowHeight(0f, pawn);   // innerW 不影响开关，只决定是否折叠
            float cardH = OvCardPadY + portraitSz + 8f + rowTitleH + 6f + rowLevelH + 6f + rowStatH + 6f
                + rowGuideH + 6f + rowMajorH + OvCardPadY;
            h += cardH + OvCardGap;

            // 下半：可自定义属性面板（每行 5 个字段卡片网格，尺寸与 DrawAttributePanel 同步）
            const float titleH = 22f;
            const int colsPerRow = 5;
            const float cellGap = 8f;
            const float cellH = 54f;
            ChronicleSettings s = LoadModSettings();
            HashSet<string> active = OverviewFieldRegistry.ActiveKeys(s?.OverviewPanelFields);
            int selCount = 0;
            foreach (var f in OverviewFieldRegistry.AllFields)
                if (active.Contains(f.Key)) selCount++;
            int rows = selCount > 0 ? (selCount + colsPerRow - 1) / colsPerRow : 1;
            float gridH = rows * cellH + (rows - 1) * cellGap;
            float panelH = titleH + 4f + gridH + OvCardPadY * 2f;
            h += panelH;

            h += OvCardGap;
            return h;
        }

        // ================= 共享：资格条件单元格（供资格子页复用）=================
        private static void DrawQualCell(Rect r, CareerQualView q)
        {
            UIComponents.TintedBox(r, UITheme.Card);
            UIComponents.Border(r, UITheme.BorderSoft);

            bool ok = q?.StateKey == "ok";
            Rect dot = new Rect(r.x + 6f, r.y + (r.height - 12f) / 2f, 12f, 12f);
            UIComponents.TintedBox(dot, ok ? UITheme.PillGreen : UITheme.Dead);

            float usableW = r.width - dot.xMax - 6f - r.x;
            float labelW2 = usableW * 0.38f;
            float noteW2 = usableW * 0.40f;
            float stateW2 = usableW * 0.22f;
            Rect labelRect = new Rect(dot.xMax + 6f, r.y, labelW2, r.height);
            UIComponents.Label(labelRect, UIComponents.TruncateToWidth(q?.Label ?? "", Mathf.Max(0f, labelW2 - 4f), GameFont.Small),
                UITheme.FontLabel, ok ? UITheme.Text : UITheme.Muted, TextAnchor.MiddleLeft);

            Rect noteRect = new Rect(labelRect.xMax + 4f, r.y, noteW2, r.height);
            UIComponents.Label(noteRect, UIComponents.TruncateToWidth(q?.Note ?? "", Mathf.Max(0f, noteW2 - 4f), GameFont.Small),
                UITheme.FontLabel, ok ? UITheme.Alive : UITheme.Muted, TextAnchor.MiddleLeft);

            Rect stateRect = new Rect(noteRect.xMax + 4f, r.y, stateW2, r.height);
            UIComponents.Label(stateRect, UIComponents.TruncateToWidth((ok ? "✓ " : "○ ") + (q?.StateText ?? ""), Mathf.Max(0f, stateW2 - 4f), GameFont.Small),
                UITheme.FontLabel, ok ? UITheme.Alive : UITheme.Dead, TextAnchor.MiddleRight);

            if (!string.IsNullOrEmpty(q?.Tooltip))
            {
                TooltipHandler.TipRegion(r, new TipSignal(q.Tooltip));
            }
        }
    }
}
