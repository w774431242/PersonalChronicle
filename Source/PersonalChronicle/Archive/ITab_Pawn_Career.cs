// 职业档案 ITab（独立于六宫格 ITab_Pawn_Chronicle 的第二个 Pawn inspect 页）。
// v1.1.4+ 高度还原 docs/UI预览/人物档案视窗/职业档案Tab预览.html：
//   · 4 子页 = 总览（职业规划 12 专业适配分析 + 职业身份 + 当前资格状态 + 资格预检 + 下一职称）
//              / 履历（工作经历 resume-block + 工坊汇总 + 当前就职）/ 勋章（勋章墙 + 贡献结构 + 最近荣誉事件）
//              / 资格（P9：职称阶梯 + 实践/理论/论文/答辩 + 职称记录）
//   · 顶部身份卡（徽章块 + 姓名 + 职业规划 + 当前职称 + 职业资历）
//   · Tab 尺寸自适应屏幕（640 上限；低分辨率/高 UI 缩放自动收缩，内容滚动）
// 边界契约（与 ITab_Pawn_Chronicle 一致，架构 §3.1 / UI 标准 §5）：
//   · 档案数据只消费 <see cref="DetailSnapshot"/>（IArchiveUiDataProvider 派生），绝不自行查询 + 排序。
//   · 职业规划适配分析的输入（原版技能等级/兴趣/实践/品质）是活读游戏事实（等同显示 pawn 姓名），
//     算法纯逻辑在 <see cref="ProfessionalFitAnalyzer"/>（Domain，可单测）；本窗口只取输入并渲染。
//   · 全部绘制走 UIComponents + UITheme，不在本文件散落 GUI.color / new Color。
// 文件治理（ARC-013）：本文件为主文件（字段/入口/公共 helper），绘制方法按子页拆分为
// ITab_Pawn_Career.Overview.cs / .Resume.cs / .Honor.cs / .Fit.cs 四个 partial。
using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.ReadModels;
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
    public partial class ITab_Pawn_Career : ITab
    {
        // ---- Layout metrics（对齐预览 HTML：身份卡 + 3 子页；CJK 行高见 UI 标准 §4）----
        private const float TabWidth = 640f;
        private const float TabHeight = 720f;
        private const float Pad = UITheme.PanelPadding;
        private const float HeaderH = 96f;
        private const float TabBarH = 30f;
        private const float ButtonH = 34f;
        private const float BadgeW = 56f;
        private const float ChipW = 96f;
        private const float ChipH = 26f;
        private const float MedIconW = 46f;
        private const float MedIconH = 56f;

        // 4 子页（对齐 HTML：总览/履历/勋章/资格；资格页承载 P9 考试/论文/答辩/评级评审流程）。
        private static readonly string[] SubTabs = { "Overview", "Career", "Honor", "Qualification" };
        private static readonly string[] SubTabKeys =
        {
            "PersonalChronicle.UI.Career.Sub.Overview",
            "PersonalChronicle.UI.Career.Sub.Career",
            "PersonalChronicle.UI.Career.Sub.Honor",
            "PersonalChronicle.UI.Career.Sub.Qualification"
        };

        // 12 一级专业稳定键（对齐 ProfessionalFitAnalyzer.MAJOR_WEIGHTS）。
        private static readonly string[] MajorKeys =
        {
            "Engineering", "Manufacturing", "Agriculture", "Forestry",
            "AnimalHusbandry", "Medicine", "Weapons", "Mining",
            "Research", "Cooking", "Art", "Management"
        };

        // 12 原版技能稳定键（ProfessionalFitAnalyzer 输入键 = SkillDef.defName）。
        private static readonly string[] VanillaSkills =
        {
            "Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants",
            "Animals", "Crafting", "Artistic", "Medicine", "Social", "Intellectual"
        };

        private int subTabIndex;
        private Vector2 scroll;

        // 职业规划（会话态）：当前选中专业 key；null = 未选择（职称链兜底真实 QualificationDef）。
        private string currentMajorKey;
        private List<ProfessionalFitResult> fitResults = new List<ProfessionalFitResult>();
        private string fitPawnId;
        private long fitRevision = -1L;

        private readonly ArchiveUiDataProvider uiDataProvider = new ArchiveUiDataProvider();
        private DetailSnapshot cachedSnapshot;
        private string cachedPawnId;
        private long cachedRevision = -1L;

        public ITab_Pawn_Career()
        {
            size = new Vector2(TabWidth, TabHeight);
            labelKey = "PersonalChronicle.UI.CareerTab";
            tutorTag = "PersonalChronicleCareer";
        }

        private Pawn SelPawnSafe
        {
            get
            {
                Thing thing = SelThing;
                Pawn pawn = thing as Pawn;
                if (pawn != null) return pawn;
                Corpse corpse = thing as Corpse;
                return corpse != null ? corpse.InnerPawn : null;
            }
        }

        public override bool IsVisible
        {
            get
            {
                try
                {
                    Pawn pawn = SelPawnSafe;
                    if (pawn == null || pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return false;
                    IArchiveService service = PersonalChronicleMod.ArchiveService;
                    if (service == null) return false;
                    string stableId = pawn.GetUniqueLoadID();
                    if (service.GetObject(stableId) != null) return true;
                    return pawn.Faction != null && pawn.Faction.IsPlayer;
                }
                catch (Exception ex)
                {
                    Log.WarningOnce("PersonalChronicle: ITab_Pawn_Career.IsVisible failed: " + ex.Message, 0x5C12A1);
                    return false;
                }
            }
        }

        protected override void FillTab()
        {
            // Tab 尺寸自适应：宽度不超过 640 且留屏幕边距；高度不超过 720 且不顶出屏幕
            // （TabRect = 底部标签行向上生长：上限 screenHeight - 230）。
            float w = Mathf.Min(TabWidth, Verse.UI.screenWidth - 24f);
            float h = Mathf.Min(TabHeight, Verse.UI.screenHeight - 240f);
            if (Mathf.Abs(size.x - w) > 1f || Mathf.Abs(size.y - h) > 1f)
            {
                size = new Vector2(w, h);
            }

            Pawn pawn = SelPawnSafe;
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            Rect outer = new Rect(0f, 0f, size.x, size.y).ContractedBy(Pad);

            if (pawn == null || service == null)
            {
                UIComponents.Label(outer, "PersonalChronicle.UI.NoService".Translate(),
                    UITheme.FontBody, UITheme.Muted);
                return;
            }

            EnsureSnapshot(service, pawn);
            DetailSnapshot snap = cachedSnapshot;
            // 会话态主方向高亮：对齐已持久化的 primaryDirection（仅前端展示，不写后端）。
            PawnObject po = snap != null ? snap.DetailObject as PawnObject : null;
            currentMajorKey = (po != null && po.CareerData != null && po.CareerData.Professional != null)
                ? po.CareerData.Professional.primaryDirection
                : null;
            if (snap == null)
            {
                UIComponents.Label(outer, "PersonalChronicle.UI.NoService".Translate(),
                    UITheme.FontBody, UITheme.Muted);
                return;
            }

            EnsureFit(pawn, snap);

            float y = outer.y;
            y = DrawSubTabs(new Rect(outer.x, y, outer.width, TabBarH), pawn, service);

            Rect body = new Rect(outer.x, y, outer.width, outer.yMax - y - UITheme.SpaceXs);
            switch (SubTabs[subTabIndex])
            {
                case "Overview": DrawOverviewTab(body, pawn, snap); break;
                case "Career": DrawCareerResumeTab(body, snap); break;
                case "Honor": DrawHonorTab(body, pawn, snap); break;
                case "Qualification": DrawQualificationTab(body, pawn, snap); break;
            }
        }

        // ================= 顶部身份卡（personbar）=================
        // v1.1.5：删除顶部头像/徽章/姓名/职业规划容器（视觉冗余 — 该信息已覆盖在子页头区块）；
        // 释放顶部 96f+SpaceXs → 子页 Tab 直接上移，内容区更大。
        private float DrawHeader(Rect outer, float y, Pawn pawn, DetailSnapshot snap)
        {
            // 已废弃占位（v1.1.5 删除顶部头像/徽章/姓名/职业规划容器；旧接口保留以免外部调用残留编译报错，不再渲染任何内容）。
            return y;
        }

        // ================= 4 子页切换条（右侧嵌入 DevMode 齿轮，避免污染首屏）=================
        private float DrawSubTabs(Rect rect, Pawn pawn, IArchiveService service)
        {
            // 右侧 DevMode 齿轮：仅开发者模式可见，不占子页内容高度。
            float gearW = Prefs.DevMode ? 28f : 0f;
            float tabAreaW = rect.width - gearW;
            float tabW = tabAreaW / SubTabs.Length;
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            try
            {
                for (int i = 0; i < SubTabs.Length; i++)
                {
                    Rect tabRect = new Rect(rect.x + i * tabW, rect.y, tabW - 2f, rect.height);
                    bool active = i == subTabIndex;
                    UIComponents.Panel(tabRect, active ? UITheme.PanelRaised : UITheme.Panel);
                    if (active) UIComponents.Border(tabRect, UITheme.PillGold);
                    Verse.Text.Font = GameFont.Small;
                    Verse.Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = active ? UITheme.Text : UITheme.Muted;
                    Widgets.Label(tabRect, SubTabKeys[i].Translate().ToString());
                }

                // DevMode 齿轮：点击随机职业数据（对齐 HTML randomizeCareerData）。
                if (Prefs.DevMode)
                {
                    Rect gearRect = new Rect(rect.xMax - gearW, rect.y, gearW, rect.height);
                    UIComponents.TintedBox(gearRect, UITheme.PanelRaised);
                    UIComponents.Border(gearRect, UITheme.BorderSoft);
                    Verse.Text.Font = GameFont.Small;
                    Verse.Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = UITheme.Muted;
                    Widgets.Label(gearRect, "⚙");
                    TooltipHandler.TipRegion(gearRect, "PersonalChronicle.UI.Career.DevRandomize".Translate().ToString());
                    if (Widgets.ButtonInvisible(gearRect))
                    {
                        DevTestButtons.CareerRandomize(pawn);
                        EnsureSnapshot(service, pawn);
                        EnsureFit(pawn, cachedSnapshot);
                        subTabIndex = 0;
                        scroll = Vector2.zero;
                    }
                }
            }
            finally
            {
                GUI.color = prevColor;
                Verse.Text.Font = prevFont;
                Verse.Text.Anchor = prevAnchor;
            }
            for (int i = 0; i < SubTabs.Length; i++)
            {
                Rect tabRect = new Rect(rect.x + i * tabW, rect.y, tabW - 2f, rect.height);
                if (Widgets.ButtonInvisible(tabRect))
                {
                    if (subTabIndex != i) { subTabIndex = i; scroll = Vector2.zero; }
                }
            }
            return rect.y + rect.height + UITheme.SpaceXs;
        }

        // ================= 通用 helper =================
        private static long CareerSpanTicks(CareerData cd)
        {
            if (cd == null || cd.Events == null || cd.Events.Count == 0) return 0L;
            long min = long.MaxValue, max = long.MinValue;
            for (int i = 0; i < cd.Events.Count; i++)
            {
                CareerEvent ev = cd.Events[i];
                if (ev == null) continue;
                if (ev.Tick < min) min = ev.Tick;
                if (ev.Tick > max) max = ev.Tick;
            }
            return max >= min ? max - min : 0L;
        }

        private static ProfessionalDirectionDef TryGetDirectionDef(DetailSnapshot snap)
        {
            PawnObject po = snap != null ? snap.DetailObject as PawnObject : null;
            if (po == null || po.CareerData == null || po.CareerData.Professional == null
                || po.CareerData.Professional.skills == null)
            {
                return null;
            }
            for (int i = 0; i < po.CareerData.Professional.skills.Count; i++)
            {
                ProfessionalSkillData sd = po.CareerData.Professional.skills[i];
                if (sd == null || string.IsNullOrEmpty(sd.skillDefName)) continue;
                ProfessionalSkillDef sdef = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(sd.skillDefName);
                if (sdef == null || string.IsNullOrEmpty(sdef.direction)) continue;
                return DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(sdef.direction);
            }
            return null;
        }

        private static Color ParseHex(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            Color c;
            if (ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return fallback;
        }

        private static string FirstChar(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Substring(0, 1);
        }

        private static string FirstGlyph(string label)
        {
            if (string.IsNullOrEmpty(label)) return "?";
            return label.Substring(0, 1);
        }

        private static string MajorLabel(string majorKey)
        {
            if (string.IsNullOrEmpty(majorKey)) return "--";
            return ("PersonalChronicle.UI.Career.Major." + majorKey).Translate().ToString();
        }

        private static string MajorDirLabel(string majorKey)
        {
            if (string.IsNullOrEmpty(majorKey)) return "--";
            return ("PersonalChronicle.UI.Career.MajorDir." + majorKey).Translate().ToString();
        }

        private static string MajorGlyph(string majorKey)
        {
            if (string.IsNullOrEmpty(majorKey)) return "";
            string label = MajorLabel(majorKey);
            return FirstChar(label);
        }

        /// <summary>该一级专业是否为玩家已选主方向（经 primaryDirection 匹配其 ProfessionalDirectionDef.profession）。</summary>
        private bool IsMajorChosen(string majorKey)
        {
            if (string.IsNullOrEmpty(majorKey)) return false;
            PawnObject po = cachedSnapshot != null ? cachedSnapshot.DetailObject as PawnObject : null;
            if (po == null || po.CareerData == null || po.CareerData.Professional == null) return false;
            string pd = po.CareerData.Professional.primaryDirection;
            if (string.IsNullOrEmpty(pd)) return false;
            ProfessionalDirectionDef ddef = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(pd);
            if (ddef == null || string.IsNullOrEmpty(ddef.profession)) return false;
            return ddef.profession == majorKey;
        }

        private static string TierLabel(MedalTier tier)
        {
            switch (tier)
            {
                case MedalTier.Bronze: return "PersonalChronicle.UI.Career.Honor.Tier.Bronze".Translate().ToString();
                case MedalTier.Silver: return "PersonalChronicle.UI.Career.Honor.Tier.Silver".Translate().ToString();
                default: return "PersonalChronicle.UI.Career.Honor.Tier.Gold".Translate().ToString();
            }
        }

        private static string FormatValue(double v)
        {
            if (v >= 10000.0) return (v / 1000.0).ToString("0.0") + "K";
            if (v == (long)v) return ((long)v).ToString();
            return v.ToString("0.##");
        }

        // ---- 快照生命周期（与 ITab_Pawn_Chronicle 同模式）----
        private void EnsureSnapshot(IArchiveService service, Pawn pawn)
        {
            try
            {
                string stableId = pawn.GetUniqueLoadID();
                long revision = service.GetDataRevision();
                if (cachedSnapshot != null && cachedPawnId == stableId && cachedRevision == revision)
                {
                    return;
                }
                if (cachedPawnId != stableId) scroll = Vector2.zero;
                cachedSnapshot = uiDataProvider.BuildDetail(service, stableId, revision);
                cachedPawnId = stableId;
                cachedRevision = revision;
            }
            catch (Exception ex)
            {
                Log.WarningOnce("PersonalChronicle: career tab snapshot build failed: " + ex.Message, 0x5C12A2);
                cachedSnapshot = null;
            }
        }
    }
}