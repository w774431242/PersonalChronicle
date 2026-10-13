using System;
using PersonalChronicle.Application.PawnEconomy.Perception;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// Pawn 检视窗「个人需求」页（第 3 个 mod 页签，与「档案」「职业档案」同级）。
    ///
    /// 定位：玩家向的**需求循环**视图 —— 整页由 RUIF 绘制，呈现经济需求驱动 AI 的循环环节
    /// （观察 → 缺口 → 目标 → 仲裁 → 预算 → 政策 → 明细）。原版只作为**数据真值来源**，
    /// 本页**不绘制任何原版需求 UI**（PM 裁决 2026-10-09：移除原版需求列）。
    ///
    /// 边界契约：
    ///   · **零原版 UI 依赖**：不调 `NeedsCardUtility`，不接管、不替换原版 `RimWorld.ITab_Pawn_Needs`，
    ///     无 Harmony 补丁 ⇒ 原版 UI 改版不影响本页（只影响数据面）。
    ///   · **不复制需求真值**：只消费同一只读门面
    ///     (<see cref="GameComponent_PawnEconomy.QueryNeedDashboardSingle"/>) 与同一内容层
    ///     (<see cref="NeedsCyclePanel"/>)，不新增数据源、不新增窗口。
    ///   · **不新增跨域引用**：本页与 <see cref="NeedsCyclePanel"/> 只依赖 Economy/Application/Domain
    ///     + RUIF + Verse + RimWorld；不引用 Archive 的 UITheme。
    ///   · 会话态（测量缓存/滚动/折叠）只存本实例；ITab 由引擎共享实例持有，故切换 Pawn 必须
    ///     按 pawnId 键控并复位（见 <see cref="FillTab"/> 的 cachedPawnId）。
    ///   · 类名刻意避开原版 <c>RimWorld.ITab_Pawn_Needs</c>：原版 Def XML 直接用短名引用
    ///     （Core/Defs/ThingDefs_Races/Races_Animal_Base.xml），重名会引入解析歧义。
    /// </summary>
    public sealed class ITab_Pawn_PersonalNeeds : ITab
    {
        /// <summary>页签基准宽（本页自有；原版需求列已移除，故不再继承原版度量）。</summary>
        private const float TabWidth = 580f;
        private const float TabHeight = 640f;
        private const float MinimumBodyWidth = 260f;
        /// <summary>本页自有内边距，不引用 Archive 的 UITheme（避免跨域引用）。</summary>
        private const float Pad = 8f;
        private const int NeedRefreshTicks = 60;

        private readonly NeedPanelState state = new NeedPanelState();
        private string cachedPawnId;
        /// <summary>查询失败后的重试水位（会话态）。避免视图尚未建立时每帧重试异常路径。</summary>
        private int nextRetryTick;

        public ITab_Pawn_PersonalNeeds()
        {
            size = new Vector2(TabWidth, TabHeight);
            // labelKey 必须是 raw key：引擎惰性翻译，不得在此 Translate。
            labelKey = "PC_PEA.Needs.TabTitle";
            tutorTag = "PersonalChroniclePersonalNeeds";
        }

        private Pawn SelPawnSafe
        {
            get
            {
                Thing thing = SelThing;
                Pawn pawn = thing as Pawn;
                if (pawn != null)
                {
                    return pawn;
                }
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
                    // 死体 / 非人形 / 非玩家方（访客、敌对、客居）不显示；囚犯与奴隶属玩家方，正常显示。
                    return pawn != null && !pawn.Dead && pawn.RaceProps != null && pawn.RaceProps.Humanlike
                        && pawn.Faction != null && pawn.Faction.IsPlayer;
                }
                catch (Exception ex)
                {
                    Log.WarningOnce("PersonalChronicle: ITab_Pawn_PersonalNeeds.IsVisible failed: " + ex.Message, 0x5C13A1);
                    return false;
                }
            }
        }

        protected override void FillTab()
        {
            float height = Mathf.Min(TabHeight, Mathf.Max(320f, PaneTopY - 8f));
            try
            {
                Pawn pawn = SelPawnSafe;
                GameComponent_PawnEconomy runtime = GameComponent_PawnEconomy.Current;
                if (pawn == null || runtime == null)
                {
                    RUIF.FeedbackAtoms.EmptyState(new Rect(0f, 0f, size.x, height).ContractedBy(Pad), N("Unavailable"), N("UnavailableHint"));
                    return;
                }

                float width = Mathf.Min(TabWidth, Verse.UI.screenWidth - 24f);
                if (Mathf.Abs(size.x - width) > 1f || Mathf.Abs(size.y - height) > 1f)
                {
                    size = new Vector2(width, height);
                }
                Rect body = new Rect(0f, 0f, size.x, height).ContractedBy(Pad);
                if (body.width < MinimumBodyWidth)
                {
                    RUIF.FeedbackAtoms.EmptyState(body, N("Unavailable"), N("UnavailableHint"));
                    return;
                }

                string pawnId = pawn.GetUniqueLoadID();
                if (!string.Equals(cachedPawnId, pawnId, StringComparison.Ordinal))
                {
                    cachedPawnId = pawnId;
                    state.ResetForNewSubject();
                }

                Map map = pawn.MapHeld ?? pawn.Map ?? Find.CurrentMap;
                int now = Find.TickManager?.TicksGame ?? 0;
                bool available = runtime.IsPerceptionMapAvailable(map);
                bool stale = state.View == null || state.View.Snapshot.Epoch != runtime.PerceptionSessionEpoch
                    || state.MapAvailable != available || !ReferenceEquals(state.Language, LanguageDatabase.activeLanguage)
                    || now < state.Tick || now - state.Tick >= NeedRefreshTicks;
                // 尚无可用视图时按刷新间隔退避（见 nextRetryTick），已有视图则照常按门控刷新。
                if (stale && (state.View != null || now >= nextRetryTick))
                {
                    state.Language = LanguageDatabase.activeLanguage;
                    state.Tick = now;
                    state.MapAvailable = available;
                    state.SelectedId = pawnId;
                    state.Page = 0;
                    state.View = runtime.QueryNeedDashboardSingle(map, pawnId);
                    state.MeasuredView = null;
                    state.Cycle = null;
                }

                NeedsCyclePanel.Draw(body, state.View, state);
            }
            catch (Exception ex)
            {
                Log.WarningOnce("PersonalChronicle: ITab_Pawn_PersonalNeeds.FillTab failed: " + ex.Message, 0x5C13A2);
                nextRetryTick = (Find.TickManager?.TicksGame ?? 0) + NeedRefreshTicks;
                // 用页面级文案而非"请先进入殖民地地图"：后者只在读取就绪度不足时成立，对异常路径是误导。
                RUIF.FeedbackAtoms.EmptyState(new Rect(0f, 0f, size.x, height).ContractedBy(Pad), N("Cycle.Error"), N("UnavailableHint"));
            }
        }

        /// <summary>整键回落（P0-4 同一纪律，唯一实现 <see cref="EconomyLabels.TranslatedOrId"/>）：
        /// 缺键显示标识本身，绝不把 <c>PC_PEA.Needs.*</c> 裸键打上屏。</summary>
        private static string N(string suffix) => EconomyLabels.TranslatedOrId("PC_PEA.Needs." + suffix, suffix);
    }
}
