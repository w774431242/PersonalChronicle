using PersonalChronicle.Archive;
using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// [ARC-013] <see cref="PersonnelArchivePanel"/> 的「详情/时间轴度量计算」切片
    /// （ComputeDetailPanelHeight / TimelineLineHeight / FormatParams）。
    ///
    /// 原 PersonnelArchivePanel.cs 单文件 969 行，混装「字段声明」「切片残留的孤儿注释」
    /// 「详情面板高度计算」「时间轴行高计算」「参数格式化」多组职责，违反 [ARC-013]
    /// 单一职责与「单文件 ≤ 800 行」。按 08 §3「排序→识别→剥离」，本文件剥离其中的
    /// 纯度量计算方法（不与绘制耦合，仅消费 Read-Model 快照字段）。
    ///
    /// 剥离原则（[ARC-013] 零契约改动）：仅物理搬移，类名、方法签名、访问级别、
    /// 命名空间、翻译键一律未改；同 partial 类成员天然互相可见，跨文件调用无需改访问级别。
    /// </summary>
    public sealed partial class PersonnelArchivePanel
    {
        private float ComputeDetailPanelHeight(Rect panel)
        {
            string tab = CurrentTabKey();
            if (tab == "Career")
            {
                // v5.x 生涯子页是 **1fr 1fr 双栏**（预览 #v-pawn t2，HTML 1561-1695；见 PawnWork.DrawCareerTab）——
                // 两栏顶部对齐、各自向下增长 ⇒ 内容高 = 两栏较大者。以下公式与绘制路径**同源**：
                // 每一段都取自 PawnWork.cs 的命名常量与 DrawCareerTab/DrawCareerLeftColumn/
                // DrawCareerRightColumn 的推进语句（段题 30 / 块间距 12 / SpaceXs 8 / SpaceXxs 4 均来自 UITheme）。
                //
                // ⚠ 旧公式是**单栏**累加式（`intensityRows*120 + productionRows*58 + 16*26`）并硬抬
                //   `Mathf.Max(980f, …)` 下限：双栏后实际内容约 530px，而旧值恒 ≥ 980（典型 1500+）
                //   ⇒ 生涯页滚动条下方留约 900px 空白。故按本页真实布局常量重推。
                int productionTypes = cachedProductionSummary != null && cachedProductionSummary.Types != null
                    ? cachedProductionSummary.Types.Count : 0;
                // 物品格 2 列一行（DrawProductionGroup 用 i / 2 分行）。
                int itemRows = productionTypes > 0 ? (productionTypes + 1) / 2 : 0;

                // —— 左栏：生涯摘要 → 工作累计工时 → 工时投入趋势 ——
                float leftHeight =
                    // ① 生涯摘要 = 段题 + hero(40)+SpaceXs + 3 格(84)+SpaceXs
                    //    + 阶梯标题行(18)+SpaceXxs + chips(18)+SpaceXs
                    UITheme.SectionTitleHeight
                    + CareerHeroHeight + UITheme.SpaceXs
                    + CareerMetricCardHeight + UITheme.SpaceXs
                    + CareerTinyLineHeight + UITheme.SpaceXxs
                    + CareerChipHeight + UITheme.SpaceXs
                    + UITheme.BlockGap;
                // ② 工作累计工时 = 段题 + 口径行(18)+SpaceXxs + 行 (无数据时只画 1 行空态 Small 22)
                int barRows = cachedIntensityWorkTypes != null && cachedIntensityWorkTypes.Count > 0
                    ? Mathf.Min(cachedIntensityWorkTypes.Count, CareerBarRowLimit)
                    : 0;
                leftHeight += UITheme.SectionTitleHeight
                    + CareerTinyLineHeight + UITheme.SpaceXxs
                    + (barRows > 0 ? barRows * CareerBarRowHeight : UITheme.FontBodyLineHeight)
                    + UITheme.BlockGap;
                // ③ 工时投入趋势 = 子题(22)+SpaceXxs + 摘要行(22)+SpaceXxs + plot(52)+SpaceXxs + 口径行(18)
                //    摘要行仅在 cachedWorkHasData 时绘制 ⇒ 此处按**有数据**计（宁可多留 26px，不裁切）。
                leftHeight += UITheme.FontBodyLineHeight + UITheme.SpaceXxs
                    + UITheme.FontBodyLineHeight + UITheme.SpaceXxs
                    + CareerTrendPlotHeight + UITheme.SpaceXxs
                    + CareerTinyLineHeight;

                // —— 右栏：生产 → 职业事实计数 → 累计产出价值趋势 ——
                float rightHeight = productionTypes > 0
                    // ① 生产 = 段题 + 2 格(74)+SpaceXs + 物品格 ceil(k/2)×60
                    ? UITheme.SectionTitleHeight + 74f + UITheme.SpaceXs + itemRows * CareerItemCellPitch
                    // 空态只画一行 Small 22
                    : UITheme.SectionTitleHeight + UITheme.FontBodyLineHeight;
                // ② 职业事实计数 = BlockGap + 段题 + max(左 5 行, 右 4 行)×18 + SpaceXxs + BlockGap
                rightHeight += UITheme.BlockGap
                    + UITheme.SectionTitleHeight
                    + Mathf.Max(CareerFactLeftCount, CareerFactCount - CareerFactLeftCount) * CareerTinyLineHeight
                    + UITheme.SpaceXxs
                    + UITheme.BlockGap;
                // ③ 累计产出价值趋势 = 子题(22)+SpaceXxs + 摘要行(22)+SpaceXxs + plot(52)+SpaceXxs + 口径行(18)
                rightHeight += UITheme.FontBodyLineHeight + UITheme.SpaceXxs
                    + UITheme.FontBodyLineHeight + UITheme.SpaceXxs
                    + CareerTrendPlotHeight + UITheme.SpaceXxs
                    + CareerTinyLineHeight;

                // 尾距 SpaceXs：末行口径文字不贴裁剪边。下限取视口高 ⇒ 内容装得下时不出现滚动条。
                return Mathf.Max(panel.height, Mathf.Max(leftHeight, rightHeight) + UITheme.SpaceXs);
            }
            if (tab == "CombatLog")
            {
                // v4.17 体检：按实际绘制布局估算——旧实现按单列累加每张卡高，
                // 而 DrawFactionCodex 在宽面板（≥720f）双列排布 → 估算约 2 倍，
                // 滚动区底部大段空白。按行分组取行内最大卡高。
                float h = 260f;
                int cols = panel.width >= FactionCodexTwoColumnWidth ? 2 : 1;
                for (int i = 0; i < cachedFactionCodex.Count; i += cols)
                {
                    float rowH = 0f;
                    for (int j = i; j < i + cols && j < cachedFactionCodex.Count; j++)
                    {
                        FactionCodexView card = cachedFactionCodex[j];
                        bool expanded = expandedFactions.Contains(card.FactionKey);
                        rowH = Mathf.Max(rowH, FactionCodexCardHeight(card, expanded));
                    }
                    h += rowH + FactionCodexGap;
                }
                return h;
            }
            if (tab == "Social")
            {
                // v5.x（对照清单 t4）：Social 子页已改为 **1.1fr : 1fr 双栏**（预览 HTML:1800）——
                //   左栏 = 宗族树（关系网络面板）；右栏 = 重要关系 → 关系事件 → 命运交织。
                //   故内容高 = **max(左栏, 右栏) + 尾距**，而不再是右栏三块纵向相加
                //   （旧式按单栏累加 ⇒ 双栏后高估整段右栏，滚动区底部留大段空白）。
                //   逐段常量与 PawnSocial.cs 绘制路径**共用**（SocialTinyLineHeight /
                //   SocialEmptyBlockHeight / SocialTableHeaderAdvance / SocialColumnWidths /
                //   CountSocialChipRows）⇒ 列宽与 chip 换行算法单一来源，避免两处漂移。
                int relationCount = cachedDetailObject is PawnObject pawn && pawn.Relations != null
                    ? pawn.Relations.Count : 0;

                // ① 关系网络面板高：与 DrawSocialNetwork.ComputePanelHeightFor 同式的 zoom 驱动
                //    估算（网格行数由关系数粗判 1/2 行）。baseNodeW 只决定节点宽（绘制侧 auto-fit
                //    zoom），不进入面板高 ⇒ 沿用 panel.width 近似不影响本项。
                float zoom = Mathf.Max(socialNetworkZoom, 0.4f);
                float baseNodeW = Mathf.Min(160f, Mathf.Max(110f, panel.width * 0.22f));
                float baseNodeH = 60f;
                float rowSpacing = baseNodeH * zoom
                    + Mathf.Max(baseNodeH * zoom * 0.30f, 22f * zoom);
                int maxAbsRow = relationCount <= 2 ? 1 : 2;
                float panelH = Mathf.Max(
                    246f, 246f * zoom,
                    (maxAbsRow * 2 + 1) * rowSpacing + baseNodeH * zoom + 32f);

                // ② 双栏列宽：滚动内容宽 = panel.width - 16f（对齐 Shell.DrawDetailContent 的
                //    viewRect 宽），拆分算法与 DrawSocialTab 共用。
                float leftColW;
                float rightColW;
                float colGap;
                SocialColumnWidths(Mathf.Max(1f, panel.width - 16f),
                    out leftColW, out rightColW, out colGap);

                // ③ 左栏 = 段题(30f) + 关系网络面板高 + 面板后 SpaceXs。
                float leftHeight = UITheme.SectionTitleHeight + panelH + UITheme.SpaceXs;

                // ④ 右栏 = 重要关系 / 关系事件 / 命运交织（各含段题 30f，段间 SpaceXs）。
                float rightHeight = UITheme.SectionTitleHeight;   // 重要关系段题
                int kinRows = 0;   // 只数家族树亲缘（与 DrawSocialRelationTable 同一过滤器）
                if (cachedRelations != null)
                {
                    for (int i = 0; i < cachedRelations.Count; i++)
                    {
                        if (cachedRelations[i] != null
                            && IsFamilyTreeKin(cachedRelations[i].RelationDefName))
                        {
                            kinRows++;
                        }
                    }
                }
                rightHeight += kinRows > 0
                    // 表头(Tiny 18f + SpaceXxs) + 行×Small 22f + 段尾 SpaceXs
                    ? SocialTableHeaderAdvance + kinRows * UITheme.FontBodyLineHeight + UITheme.SpaceXs
                    : SocialEmptyBlockHeight;

                // 关系事件：行高 = 紧凑三列行（Small 度量，非旧 TimelineRowHeight 34f）
                int socialEvents = 0;
                for (int i = 0; i < cachedDetailRawEvents.Count; i++)
                {
                    if (cachedDetailRawEvents[i] != null && IsSocialEvent(cachedDetailRawEvents[i]))
                    {
                        socialEvents++;
                    }
                }
                rightHeight += UITheme.SpaceXs + UITheme.SectionTitleHeight
                    + (socialEvents > 0
                        ? socialEvents * UITheme.FontBodyLineHeight
                        : SocialEmptyBlockHeight);

                // 命运交织：chip 流式换行行数（回放 CountSocialChipRows 的实测换行结果）
                int chipRows = CountSocialChipRows(rightColW);
                rightHeight += UITheme.SpaceXs + UITheme.SectionTitleHeight
                    + (chipRows > 0
                        ? chipRows * (SocialTinyLineHeight + UITheme.SpaceXxs) - UITheme.SpaceXxs
                        : SocialEmptyBlockHeight);

                // 尾距 SpaceXs：末行不贴裁剪边。下限取视口高 ⇒ 内容装得下时不出现滚动条。
                return Mathf.Max(panel.height,
                    Mathf.Max(leftHeight, rightHeight) + UITheme.SpaceXs);
            }
            if (tab == "Legacy")
            {
                // Epithet + verdict + summary KPI + current-holder + table header +
                // (rows up to the cap, expanded when toggled) + spacing. Per-row
                // height is measured from the From/To text widths (v4.9.1: CJK date
                // strings can wrap to 2 lines), matching the adaptive logic in
                // DrawLegacyTab.
                Application.ReadModels.LegacyView legacy = cachedLegacy ?? new Application.ReadModels.LegacyView();
                float h = 26f; // epithet
                if (!string.IsNullOrEmpty(legacy.TitleText)) h += UITheme.FontBodyLineHeight + 8f;
                if (!string.IsNullOrEmpty(legacy.VerdictText)) h += UITheme.FontBodyLineHeight * 2f + UITheme.SpaceSm;
                if (legacy.IsEmpty) return h + 26f;
                h += UITheme.SectionTitleHeight + UIComponents.StatCellMinHeight + UITheme.SpaceSm; // summary
                h += 26f; // current holder row
                h += UITheme.SectionTitleHeight; // holders title
                // Table column widths must mirror DrawLegacyTab to keep the height
                // estimate aligned with the actual rendering. DrawLegacyTab renders
                // into viewRect (panel.width minus the 16f scrollbar), so the remark
                // column here must use the same reduced width or the estimate drifts.
                float wColGen = 46f;
                float wColHolder = 100f;
                float wColFrom = 110f;
                float wColTo = 110f;
                float wColDur = 74f;
                float wColKills = 54f;
                float wColRemark = (panel.width - 16f)
                    - (wColGen + wColHolder + wColFrom + wColTo + wColDur + wColKills);
                if (wColRemark < 80f) wColRemark = 80f;
                float baseRowH = UITheme.FontBodyLineHeight + 6f;
                int shownRows = legacy.Holders != null
                    ? Mathf.Min(legacy.Holders.Count, legacyExpanded ? int.MaxValue : 5)
                    : 0;
                for (int i = 0; i < shownRows; i++)
                {
                    Application.ReadModels.LegacyHolderView hr = legacy.Holders[i];
                    if (hr == null) { h += baseRowH; continue; }
                    // v4.9.1: pin the font to Tiny and measure at the render width
                    // (col width minus the 4f inner padding) — same as DrawLegacyTab.
                    // v4.17 体检：测量期间改字体包 try/finally（CalcHeight 抛异常时
                    // 字体泄漏到后续绘制）。
                    GameFont measurePrevFont = Verse.Text.Font;
                    float fromH;
                    float toH;
                    try
                    {
                        Verse.Text.Font = GameFont.Tiny;
                        float fromRenderW = Mathf.Max(1f, wColFrom - 4f);
                        float toRenderW = Mathf.Max(1f, wColTo - 4f);
                        fromH = !string.IsNullOrEmpty(hr.FromText)
                            ? Verse.Text.CalcHeight(hr.FromText, fromRenderW)
                            : baseRowH;
                        toH = !string.IsNullOrEmpty(hr.ToText)
                            ? Verse.Text.CalcHeight(hr.ToText, toRenderW)
                            : baseRowH;
                    }
                    finally
                    {
                        Verse.Text.Font = measurePrevFont;
                    }
                    float rowH = Mathf.Max(baseRowH, Mathf.Max(fromH, toH) + 6f);
                    if (rowH > baseRowH) rowH += 4f;
                    h += rowH;
                }
                if (legacy.Holders != null && legacy.Holders.Count > 5) h += 30f; // toggle
                return Mathf.Max(panel.height, h + 12f);
            }
            if (tab == "Origin")
            {
                Application.ReadModels.ThingOriginView origin = cachedOrigin ?? new Application.ReadModels.ThingOriginView();
                Application.ReadModels.MakerChainView maker = cachedMakerChain ?? new Application.ReadModels.MakerChainView();
                if (origin.IsEmpty) return Mathf.Max(panel.height, 120f);
                float h = UITheme.SectionTitleHeight; // title
                h += 30f; // kind pill
                h += UITheme.FontBodyLineHeight + 8f; // from row
                h += UITheme.FontBodyLineHeight + 8f; // where row
                if (!string.IsNullOrEmpty(origin.NoteText)) h += UITheme.FontBodyLineHeight * 2f + UITheme.SpaceSm;
                if (!maker.IsEmpty)
                {
                    h += UITheme.SectionTitleHeight + UITheme.FontBodyLineHeight + 8f;
                }
                return Mathf.Max(panel.height, h + 12f);
            }
            if (tab == "CoUse")
            {
                Application.ReadModels.CoUseView coUse = cachedCoUse ?? new Application.ReadModels.CoUseView();
                if (coUse.IsEmpty) return Mathf.Max(panel.height, 120f);
                float h = UITheme.SectionTitleHeight + 22f; // title + hint
                int rows = coUse.Rows != null ? coUse.Rows.Count : 0;
                h += rows * (UITheme.FontBodyLineHeight + 8f);
                return Mathf.Max(panel.height, h + 12f);
            }
            if (tab == "Decommission")
            {
                Application.ReadModels.DecommissionView d = cachedDecommission ?? new Application.ReadModels.DecommissionView();
                if (!d.HasRecord) return Mathf.Max(panel.height, 120f);
                float h = UITheme.SectionTitleHeight; // title
                h += 26f; // stamp
                // DrawDecommissionTab renders 5 DetailRow rows (holder / place /
                // days / battle / date); DrawDetailRow advances by 26f each. Use the
                // real row height and count all 5 so the Date row is never clipped.
                h += 26f * 5f;
                return Mathf.Max(panel.height, h + 12f);
            }
            // Overview: cover(83, +20 when a tier medal row is shown) + ledger(98) + output(≥36) + health(~250) + spacing.
            bool tierShown = cachedWorkIntensity != null && cachedWorkIntensity.IsDefined;
            float coverH = 83f + (tierShown ? 20f : 0f);
            float ledgerH = 26f + UIComponents.StatCellMinHeight + 8f;
            int typeRows = cachedProductionSummary != null && cachedProductionSummary.Types != null
                ? Mathf.Min(cachedProductionSummary.Types.Count, 5) : 0;
            if (typeRows == 0) typeRows = 1; // empty placeholder
            float outputH = 26f + typeRows * 22f + 8f;
            int evCount = cachedHealth != null && cachedHealth.Events != null
                ? Mathf.Min(cachedHealth.Events.Count, 6) : 0;
            float eventsH = evCount > 0 ? (evCount * 22f + 4f) : 18f;
            // v4.14: +34f verdict blurb under the health valuation block.
            bool verdictShown = cachedHealth != null && !string.IsNullOrEmpty(cachedHealth.VerdictText);
            float healthH = 26f + 80f + 8f + 56f + 8f + 18f + eventsH + 6f + 22f + 12f
                + (verdictShown ? 34f : 0f);
            // v1.1.4: +medal wall (勋章墙) after health valuation block.
            float medalH = MedalWallHeight(cachedMedals);
            return Mathf.Max(panel.height, coverH + 12f + ledgerH + outputH + healthH
                + UITheme.BlockGap + medalH);
        }

        private static float TimelineLineHeight(EventLineView line, float width)
        {
            float descHeight = string.IsNullOrEmpty(line.DescriptionText)
                ? 0f
                : Text.CalcHeight(line.DescriptionText, Mathf.Max(120f, width - 8f)) + 4f;
            float chipsHeight = line.Chips != null && line.Chips.Count > 0 ? ChipRowHeight : 0f;
            return TimelineRowHeight + descHeight + chipsHeight;
        }





        // ---- Detail: pawn tabs ------------------------------------------------

        /// <summary>
        /// v3.1 archive cover: KPI strip + lifecycle + career blurb + footprint + key events.
        /// Stats tab content is absorbed here (no separate Stats button).
        /// </summary>
        /// <summary>
        /// v4.4 Pawn Overview (content-reset, layout B two-column on wide screens).
        /// Consumes only the read-model derived views (cachedLifePhases / cachedCareerBars
        /// / cachedFootprint / cachedMilestones / cachedKeyEvents). No raw query/sort here.
        /// </summary>


        /// <summary>
        /// Display priority for the social network graph: partners and blood kin
        /// outrank opinion-derived ties, and ended ties sink last. Needed because
        /// the node list is capped at 8 — without ranking, the capture order
        /// (direct → implied → opinion) would let acquaintances crowd out family.
        /// </summary>



        // v4.7 Legacy (传承) tab — the ownership-transfer chain for equipment.
        // Consumes the read-model snapshot (cachedLegacy) only: no queries, no
        // sorting, no null-guards here. Layout: epithet → verdict → summary KPI →
        // holder table (collapsible past a small cap). All drawing via UIComponents.

        // ---- v4.9 equipment legacy extension tabs (溯源 / 同袍 / 退役) ----

        /// <summary>
        /// 溯源 (Origin): where the thing came from + the maker-chain double
        /// narrative. Consumes only read-model derived views; no raw queries here.
        /// </summary>

        /// <summary>
        /// 同袍共用网络 (CoUse): colonists who used this equipment in parallel with
        /// the current holder, ranked by shared tenure with a share bar.
        /// </summary>

        /// <summary>
        /// 退役仪式 (Decommission): the thing's death record — last holder, last
        /// place, service days, final battle, retire date.
        /// </summary>




        // ---- Detail: placeholder tabs -----------------------------------------







        /// <summary>
        /// Stat cell with a Tiny-font breakdown line under the value. Forwards to
        /// the shared UIComponents.StatCell so all KPI cells share one renderer.
        /// </summary>

        private static Color AlivePill => UITheme.PillGreen;
        private static Color DeadPill => UITheme.Dead;
        private static Color BluePill => UITheme.PillBlue;




        // ---- Display helpers ---------------------------------------------------

        private static readonly string[] CategoryKeys =
        {
            ArchiveCategoryKeys.Pawn,
            ArchiveCategoryKeys.Thing,
            ArchiveCategoryKeys.Battle,
            ArchiveCategoryKeys.Location
        };




        /// <summary>角色本地化标签（自由殖民者 / 奴隶 / 囚犯）。</summary>

        /// <summary>角色徽标颜色：自由=绿 / 奴隶=琥珀 / 囚犯=砖红。</summary>






        /// <summary>
        /// v4.13: localized label for a chronicle event type. Driven by the
        /// ChronicleEventDef taxonomy (LabelCap), never magic per-typeKey strings;
        /// unknown defs fall back to the generic EvOther translation.
        /// </summary>

        /// <summary>
        /// v4.0 timeline glyph per event kind. Driven by ChronicleEventKind so it stays
        /// data-coherent with the Def taxonomy; unknown kinds fall back to a neutral dot.
        /// No magic per-typeKey strings — only the four canonical kinds are branched.
        /// </summary>

        /// <summary>
        /// v4.0 timeline node color by kind (mirrors Def taxonomy, no per-typeKey magic).
        /// </summary>




        /// <summary>
        /// Label for a production card. The production summary is aggregated by
        /// ThingCategory (e.g. "WeaponsRanged") since v4.6.5, so the defName may be
        /// either a concrete ThingDef or a ThingCategoryDef — resolve both before
        /// falling back to the raw key. A category key that no longer exists (mod
        /// removed) degrades to the raw defName without red-text.
        /// </summary>



        /// <summary>
        /// Missing-Def detection helpers (LD-7, P4-6): when an archived object
        /// references a Def that no longer exists (e.g. the user uninstalled
        /// Medieval Overhaul after the object was archived), the UI must render
        /// the raw defName as a placeholder instead of crashing. Each missing
        /// defName is logged at most once per session so the log surfaces the
        /// drift without spam.
        /// </summary>
        private static readonly HashSet<string> loggedMissingDefs = new HashSet<string>();




        private static string FormatParams(IReadOnlyDictionary<string, string> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return string.Empty;
            }
            List<string> parts = new List<string>(parameters.Count);
            foreach (KeyValuePair<string, string> pair in parameters)
            {
                if (string.IsNullOrEmpty(pair.Value))
                {
                    continue;
                }
                // Known params render through a translation mapping; unknown
                // keys are filtered out (zero hardcoded copy in code).
                if (pair.Key == ChronicleEventParams.Killer)
                {
                    parts.Add("PersonalChronicle.UI.KillerWithName".Translate(pair.Value).ToString());
                }
                else if (pair.Key == ChronicleEventParams.Relation)
                {
                    parts.Add(RelationDefLabel(pair.Value));
                }
                else if (pair.Key == ChronicleEventParams.RelationAction)
                {
                    if (pair.Value == ChronicleEventParams.RelationActionFormed)
                    {
                        parts.Add("PersonalChronicle.UI.RelActionFormed".Translate().ToString());
                    }
                    else if (pair.Value == ChronicleEventParams.RelationActionEnded)
                    {
                        parts.Add("PersonalChronicle.UI.RelActionEnded".Translate().ToString());
                    }
                }
                else if (pair.Key == ChronicleEventParams.Victim)
                {
                    parts.Add("PersonalChronicle.UI.VictimWithName".Translate(pair.Value).ToString());
                }
            }
            return string.Join("  ", parts.ToArray());
        }
    }
}
