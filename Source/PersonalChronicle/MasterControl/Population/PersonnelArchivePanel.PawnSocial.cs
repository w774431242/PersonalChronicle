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
using ReadModels = PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>Partial of PersonnelArchivePanel —PawnDetail drawing (BUG-BASE-01 refactor).</summary>
    public sealed partial class PersonnelArchivePanel
    {

        // ═══════════════════ v5.x（对照清单 t4）社会关系子页度量 ═══════════════════

        /// <summary>Tiny 字体度量行高（组件绘制铁律 1：文本行高只准用字体度量，禁字面量）。</summary>
        private const float SocialTinyLineHeight = 18f;

        /// <summary>状态语义色点边长（预览 <c>.st::before</c> 为 6px 圆点；IMGUI 无圆角口径 ⇒ 6px 方点）。</summary>
        private const float SocialStatusDotSize = 6f;

        /// <summary>
        /// 空态块推进 = 一行 Small 度量 + 段间距。绘制（三段空态）与高度预算
        /// （<c>ComputeDetailPanelHeight</c> 的 Social 分支）共用同一常量，避免两处漂移。
        /// </summary>
        private const float SocialEmptyBlockHeight = UITheme.FontBodyLineHeight + UITheme.SpaceXxs;

        /// <summary>关系表表头推进 = Tiny 度量行高 + 表头下间距（绘制与高度预算共用）。</summary>
        private const float SocialTableHeaderAdvance = SocialTinyLineHeight + UITheme.SpaceXxs;

        // 「命运交织」chip 流式排布 latch。守卫 = (数据修订号, 详情对象 id, 可用宽度)：
        // 三者任一变化才重建一次 ⇒ 绘制路径零测量（Text.CalcSize）、零字符串拼接、零 new，
        // 只读两个与合作对象等长的定长列表（与非 Pawn 项占位，保持索引对齐）。
        private readonly List<float> socialChipWidths = new List<float>();
        private readonly List<string> socialChipTexts = new List<string>();
        private long socialChipRevision = long.MinValue;
        private string socialChipOwnerId;
        private float socialChipAvailWidth = -1f;

        /// <summary>
        /// t4 双栏列宽拆分（预览 HTML:1800 的 <c>grid-template-columns:1.1fr 1fr</c>）。
        /// 绘制（<see cref="DrawSocialTab"/>）与高度预算（<c>ComputeDetailPanelHeight</c>）
        /// **共用同一算法**，避免两处列宽口径漂移。
        /// </summary>
        /// <param name="totalWidth">可用总宽（滚动内容区宽，≈ panel.width - 16f）。</param>
        /// <param name="leftW">左栏宽（宗族树 / 关系网络）。</param>
        /// <param name="rightW">右栏宽（重要关系 + 关系事件 + 命运交织）。</param>
        /// <param name="gap">栏间距（= <see cref="UITheme.GridGap"/>）。</param>
        private static void SocialColumnWidths(float totalWidth, out float leftW, out float rightW,
            out float gap)
        {
            gap = UITheme.GridGap;
            float usableW = Mathf.Max(0f, totalWidth - gap);
            leftW = Mathf.Max(160f, usableW * (1.1f / 2.1f));
            // 窄面板保护：左栏最小 160f 但不得挤掉右栏（小屏/压缩窗口下按 55% 收缩）
            // ⇒ leftW + rightW 恒等于 usableW，两栏结构上不可能互相覆盖。
            leftW = Mathf.Min(leftW, usableW * 0.55f);
            rightW = Mathf.Max(0f, usableW - leftW);
        }

        private void DrawSocialTab(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
                PawnObject pawn = cachedDetailObject as PawnObject;

                // v5.x（对照清单 t4-①）：预览 t4 是 **1.1fr : 1fr 双栏**（HTML :1800）——
                //   左栏 = 宗族树（关系网络面板）；右栏 = 重要关系 → 关系事件 → 命运交织。
                //   两栏各自从自己的 y 起画；宽度之和恒等于可用宽 ⇒ 结构上不可能互相覆盖
                //   （铁律 4：先预算后绘制，右栏各段再按 rect.yMax 兜底 break）。
                float leftW;
                float rightW;
                float colGap;
                SocialColumnWidths(rect.width, out leftW, out rightW, out colGap);
                Rect leftCol = new Rect(rect.x, rect.y, leftW, rect.height);
                Rect rightCol = new Rect(rect.x + leftW + colGap, rect.y, rightW, rect.height);

                float leftY = leftCol.y;
                DrawSectionTitle(leftCol, ref leftY,
                    "PersonalChronicle.UI.RelationNetwork".Translate().ToString());
                DrawSocialNetwork(leftCol, leftY, pawn, service);

                float rightY = rightCol.y;
                // v4.14: significant-relation table (类型/人物/状态), matching the
                // v4.6 preview's importantRel section. Consumes the Read-Model rows.
                DrawSectionTitle(rightCol, ref rightY,
                    "PersonalChronicle.UI.ImportantRelations".Translate().ToString());
                rightY = DrawSocialRelationTable(rightCol, rightY);

                rightY += UITheme.SpaceXs;
                DrawSectionTitle(rightCol, ref rightY,
                    "PersonalChronicle.UI.RelationEvents".Translate().ToString());
                rightY = DrawSocialEventRows(rightCol, rightY, service);

                rightY += UITheme.SpaceXs;
                DrawSectionTitle(rightCol, ref rightY,
                    "PersonalChronicle.UI.Intertwined".Translate().ToString());
                DrawIntertwinedChips(rightCol, rightY, service);
            }
            finally
            {
                // v4.17 体检：恢复字体（表头/行/空态多处 Text.Font 置位，旧实现只还原 color）。
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        /// <summary>
        /// 「重要关系」三列表格（预览 t4 :1904-1917）：关系 / 人物 / 状态。
        /// </summary>
        /// <param name="rect">右栏矩形（高度 = 该栏可用高，底部按 <c>rect.yMax</c> 兜底）。</param>
        /// <param name="y">起始 y。</param>
        /// <returns>绘制后的底部 y（已含段尾间距）。</returns>
        /// <remarks>
        /// 与旧实现（手绘表头 + 20f 行 + 写死 120/rest/90 列宽 + 状态只着色）的差异：
        /// ① 列宽改为按可用宽的 <b>0.28 / rest / 0.22</b> 比例（预览 :1908 表宽自适应）；
        /// ② 状态单元格文字前补 6px 语义色方点（预览 <c>.st::before</c>，:289-291）；
        /// ③ 行高/表头高只取字体度量（Tiny 18f / Small 22f），不再用 20f 非度量值。
        /// 只在 FamilyTreeKin 上绘制（保持既有语义：非亲缘关系不进此表）。
        /// </remarks>
        private float DrawSocialRelationTable(Rect rect, float y)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                float padX = 6f;
                float availW = Mathf.Max(0f, rect.width - padX * 2f);
                float col1W = availW * 0.28f;
                float col3W = availW * 0.22f;
                float col2W = Mathf.Max(0f, availW - col1W - col3W);
                float col1X = rect.x + padX;
                float col2X = col1X + col1W;
                float col3X = col2X + col2W;

                if (cachedRelations == null || cachedRelations.Count == 0)
                {
                    UIComponents.Label(new Rect(rect.x, y, rect.width, UITheme.FontBodyLineHeight),
                        "PersonalChronicle.UI.NoRelationData".Translate().ToString(),
                        GameFont.Small, UITheme.Muted, TextAnchor.MiddleLeft);
                    // 空态推进 = SocialEmptyBlockHeight（一行 Small 度量 + 段间距，非度量行高已消除）。
                    return y + SocialEmptyBlockHeight;
                }

                int kinRelationCount = 0;
                for (int i = 0; i < cachedRelations.Count; i++)
                {
                    if (cachedRelations[i] != null && IsFamilyTreeKin(cachedRelations[i].RelationDefName))
                    {
                        kinRelationCount++;
                    }
                }
                if (kinRelationCount == 0)
                {
                    UIComponents.Label(new Rect(rect.x, y, rect.width, UITheme.FontBodyLineHeight),
                        "PersonalChronicle.UI.NoRelationData".Translate().ToString(),
                        GameFont.Small, UITheme.Muted, TextAnchor.MiddleLeft);
                    // 空态推进 = SocialEmptyBlockHeight（一行 Small 度量 + 段间距，非度量行高已消除）。
                    return y + SocialEmptyBlockHeight;
                }

                // 表头：Tiny 度量行高（预览 .dt thead 与表体同为 10-11px 档）。
                UIComponents.Label(new Rect(col1X, y, col1W, SocialTinyLineHeight),
                    "PersonalChronicle.UI.RelType".Translate().ToString(),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                UIComponents.Label(new Rect(col2X, y, col2W, SocialTinyLineHeight),
                    "PersonalChronicle.UI.RelPerson".Translate().ToString(),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                UIComponents.Label(new Rect(col3X, y, col3W, SocialTinyLineHeight),
                    "PersonalChronicle.UI.RelStatus".Translate().ToString(),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                y += SocialTableHeaderAdvance;

                string endedLabel = "PersonalChronicle.UI.RelEnded".Translate().ToString();
                float rowH = UITheme.FontBodyLineHeight;        // Small 字体度量
                float textYOff = (rowH - SocialTinyLineHeight) * 0.5f;
                for (int i = 0; i < cachedRelations.Count; i++)
                {
                    ReadModels.RelationView rel = cachedRelations[i];
                    if (rel == null || !IsFamilyTreeKin(rel.RelationDefName))
                    {
                        continue;
                    }
                    // 铁律 4：先判后画（IMGUI 无自动裁剪，溢出必被后画元素盖住）。
                    if (y + rowH > rect.yMax)
                    {
                        break;
                    }
                    // 状态语义色：存续 = Alive / 已结束 = Muted / 其他（亡故等）= Dead。
                    Color statusColor = rel.IsLive
                        ? UITheme.Alive
                        : (rel.StatusLabel == endedLabel ? UITheme.Muted : UITheme.Dead);

                    UIComponents.Label(new Rect(col1X, y + textYOff, col1W, SocialTinyLineHeight),
                        rel.RelationLabel ?? "—", GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                    UIComponents.Label(new Rect(col2X, y + textYOff, col2W, SocialTinyLineHeight),
                        rel.OtherLabel ?? "—", GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
                    // 6px 语义色方点（预览 .st::before 圆点，IMGUI 走工程既有直角口径）。
                    UIComponents.TintedBox(new Rect(col3X, y + (rowH - SocialStatusDotSize) * 0.5f,
                        SocialStatusDotSize, SocialStatusDotSize), statusColor);
                    UIComponents.Label(new Rect(col3X + SocialStatusDotSize + UITheme.SpaceXxs,
                        y + textYOff, Mathf.Max(0f, col3W - SocialStatusDotSize - UITheme.SpaceXxs),
                        SocialTinyLineHeight), rel.StatusLabel ?? "—",
                        GameFont.Tiny, statusColor, TextAnchor.MiddleLeft);

                    UIComponents.Rule(new Rect(rect.x, y + rowH - UITheme.RuleHeight,
                        rect.width, UITheme.RuleHeight), UITheme.BorderSoft);
                    y += rowH;
                }
                return y + UITheme.SpaceXs;
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        /// <summary>
        /// 「关系事件」列表（预览 t4 :1923-1927）：每行 = 紧凑三列 96 / 1fr / 78。
        /// </summary>
        /// <param name="rect">右栏矩形。</param>
        /// <param name="y">起始 y。</param>
        /// <param name="service">归档服务（行点击后打开事件详情）。</param>
        /// <returns>绘制后的底部 y。</returns>
        /// <remarks>
        /// 旧实现复用共享 <c>DrawEventRow</c>（Shell.cs:506，34f 行 + 146/162/182 三列）
        /// 与本页预览的紧凑变体不符；此处**在本文件内**自绘紧凑行，不动 Shell.cs 共享绘制。
        /// 行高取 Small 字体度量 22f：预览 24px / 11px 正文 ≈ 2.2 倍行距，22f 即 Small 档
        /// 等效（Tiny 18f 会裁掉 Small 标题行，Medium 30f 过疏）。
        /// </remarks>
        private float DrawSocialEventRows(Rect rect, float y, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
                float rowH = UITheme.FontBodyLineHeight;
                bool found = false;
                for (int i = 0; i < cachedDetailRawEvents.Count; i++)
                {
                    ChronicleEvent ev = cachedDetailRawEvents[i];
                    if (ev == null || !IsSocialEvent(ev))
                    {
                        continue;
                    }
                    found = true;
                    if (y + rowH > rect.yMax)
                    {
                        break;
                    }
                    string action = string.Empty;
                    string rel = string.Empty;
                    if (ev.Params != null)
                    {
                        ev.Params.TryGetValue(ChronicleEventParams.RelationAction, out action);
                        ev.Params.TryGetValue(ChronicleEventParams.Relation, out rel);
                    }
                    string title = FormatSocialEventTitle(action, rel, ev, service);
                    // 预览 :1926 高亮行 = 「结为…」类**缔结**事件（左侧竖条转 accent）。
                    bool highlight = action == ChronicleEventParams.RelationActionFormed;
                    Rect row = new Rect(rect.x, y, rect.width, rowH);
                    DrawCompactSocialEventRow(row, FormatDate(ev.Tick), title,
                        FormatParams(ev.Params), highlight);
                    if (Widgets.ButtonInvisible(row))
                    {
                        OpenEventDetail(service, ev);
                    }
                    y += rowH;
                }
                if (!found)
                {
                    UIComponents.Label(new Rect(rect.x, y, rect.width, UITheme.FontBodyLineHeight),
                        "PersonalChronicle.UI.NoRelationEvents".Translate().ToString(),
                        GameFont.Small, UITheme.Muted, TextAnchor.MiddleLeft);
                    y += SocialEmptyBlockHeight;
                }
                return y;
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        /// <summary>
        /// 单条紧凑关系事件行（预览 <c>grid-template-columns:96px 1fr 78px; height:24px</c>）。
        /// </summary>
        /// <param name="row">行矩形（高 = Small 字体度量 22f）。</param>
        /// <param name="dateText">日期列（Tiny）。</param>
        /// <param name="titleText">标题列（Small，超宽中间省略）。</param>
        /// <param name="rightText">右列参数（Tiny，右对齐）。</param>
        /// <param name="highlight">是否为缔结关系的高亮行（左竖条转 accent）。</param>
        /// <remarks>
        /// 左侧 7f 内缩（预览 .evrow <c>padding:0 7px</c>），竖条画在该内缩区内（3px 宽，
        /// 预览 <c>.evrow::before</c>）；列宽按 96:1fr:78 的名义比例随可用宽收缩，
        /// 窄栏不产生负宽。
        /// </remarks>
        private static void DrawCompactSocialEventRow(Rect row, string dateText, string titleText,
            string rightText, bool highlight)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                const float gutter = 7f;
                const float barW = 3f;
                float contentX = row.x + gutter;
                float contentW = Mathf.Max(0f, row.width - gutter);
                float dateW = Mathf.Min(96f, contentW * 0.34f);
                float rightW = Mathf.Min(78f, contentW * 0.28f);
                float titleW = Mathf.Max(0f, contentW - dateW - rightW);
                float textY = row.y + (row.height - SocialTinyLineHeight) * 0.5f;

                UIComponents.TintedBox(new Rect(row.x, row.y + 2f, barW,
                    Mathf.Max(1f, row.height - 4f)),
                    highlight ? UITheme.Accent : UITheme.BorderSoft);

                UIComponents.Label(new Rect(contentX, textY, dateW, SocialTinyLineHeight),
                    dateText ?? string.Empty, GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                UIComponents.Label(new Rect(contentX + dateW, textY, titleW, SocialTinyLineHeight),
                    UIComponents.TruncateToWidth(titleText ?? string.Empty, titleW, GameFont.Small),
                    GameFont.Small, UITheme.Text, TextAnchor.MiddleLeft);
                UIComponents.Label(new Rect(contentX + contentW - rightW, textY, rightW,
                    SocialTinyLineHeight), rightText ?? string.Empty,
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleRight);

                UIComponents.Rule(new Rect(row.x, row.yMax - UITheme.RuleHeight, row.width,
                    UITheme.RuleHeight), UITheme.BorderSoft);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        /// <summary>
        /// 「命运交织」chip 流式排布（预览 t4 :1929-1937：药丸横向排布、超出即换行）。
        /// </summary>
        /// <param name="rect">右栏矩形。</param>
        /// <param name="y">起始 y。</param>
        /// <param name="service">归档服务（chip 点击跳转关联对象）。</param>
        /// <returns>绘制后的底部 y。</returns>
        /// <remarks>
        /// 与旧实现（36f 逐行列表 + 底部横线 + 右对齐计数）的差异：改为 <c>Text.CalcSize</c>
        /// 预测量的药丸流式换行；chip 高 = Tiny 字体度量 18f，间距 SpaceXxs。
        /// 文本/宽度走 <see cref="EnsureSocialChipLatch"/> 的修订号 latch，绘制路径零测量。
        /// </remarks>
        private float DrawIntertwinedChips(Rect rect, float y, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                const float padX = 6f;   // 预览 .chip padding:1px 6px
                float chipH = SocialTinyLineHeight;
                EnsureSocialChipLatch(service, Mathf.Max(0f, rect.width));

                bool found = false;
                int drawn = 0;
                float x = rect.x;
                float rowTop = y;
                for (int i = 0; i < cachedLinkedObjects.Count && i < socialChipTexts.Count; i++)
                {
                    LinkedObjectView link = cachedLinkedObjects[i];
                    if (link.CategoryKey != ArchiveCategoryKeys.Pawn)
                    {
                        continue;
                    }
                    found = true;
                    if (rowTop + chipH > rect.yMax)
                    {
                        break;
                    }
                    float fullW = socialChipWidths[i];
                    float chipW = Mathf.Min(fullW, rect.width);
                    // 换行：本行已有 chip 且放不下 ⇒ 另起一行（高度不足即止）。
                    if (x > rect.x && x + chipW > rect.xMax)
                    {
                        x = rect.x;
                        rowTop += chipH + UITheme.SpaceXxs;
                        if (rowTop + chipH > rect.yMax)
                        {
                            break;
                        }
                    }
                    Rect chip = new Rect(x, rowTop, chipW, chipH);
                    UIComponents.TintedBox(chip, UITheme.PanelRaised);   // 预览 .chip 底 = --raised
                    UIComponents.Border(chip, UITheme.Border);
                    // 栏宽不足时才截断（常态 chip 宽 = 文本宽 + 12f，天然放得下）。
                    string label = chipW < fullW
                        ? UIComponents.TruncateToWidth(socialChipTexts[i], Mathf.Max(0f, chipW - padX * 2f), GameFont.Tiny)
                        : socialChipTexts[i];
                    UIComponents.Label(new Rect(chip.x + padX, chip.y, Mathf.Max(0f, chip.width - padX * 2f), chipH),
                        label, GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleCenter);
                    TooltipHandler.TipRegion(chip, socialChipTexts[i]);
                    if (link.Target != NavTarget.None && Widgets.ButtonInvisible(chip))
                    {
                        NavigateTarget(service, link.Target, link.StableId, null);
                    }
                    x += chipW + UITheme.SpaceXxs;
                    drawn++;
                }

                if (!found)
                {
                    UIComponents.Label(new Rect(rect.x, y, rect.width, UITheme.FontBodyLineHeight),
                        "PersonalChronicle.UI.NoSocialData".Translate().ToString(),
                        GameFont.Small, UITheme.Muted, TextAnchor.MiddleLeft);
                    // 空态推进 = SocialEmptyBlockHeight（一行 Small 度量 + 段间距，非度量行高已消除）。
                    return y + SocialEmptyBlockHeight;
                }
                return drawn > 0 ? rowTop + chipH : y;
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        /// <summary>
        /// 重建「命运交织」chip 的文本 / 宽度 latch（预览 chip 文案「名 · 共同事件 N」）。
        /// </summary>
        /// <param name="service">归档服务（取数据修订号作守卫）。</param>
        /// <param name="availWidth">当前栏宽（窗口缩放后换行点会变，故纳入守卫）。</param>
        /// <remarks>
        /// 守卫三元组 = (数据修订号, 详情对象 id, 可用宽度)，任一变化才重建一次：
        /// 对全部 <c>cachedLinkedObjects</c> 逐项预取文案并测量（Tiny），使绘制路径
        /// 零 <c>Text.CalcSize</c>、零 <c>Translate</c>、零字符串拼接、零 new。
        /// 非 Pawn 项写入宽度 0 / null 占位，保持与 <c>cachedLinkedObjects</c> 的索引对齐。
        /// </remarks>
        private void EnsureSocialChipLatch(IArchiveService service, float availWidth)
        {
            EnsureSocialChipLatch(service != null ? service.GetDataRevision() : socialChipRevision,
                availWidth);
        }

        /// <summary>
        /// chip latch 核心（显式修订号）。高度预算路径（<c>ComputeDetailPanelHeight</c>）
        /// 没有 service，故复用上次已知修订号——守卫退化为 (对象 id, 宽度, 条数)，
        /// 数据修订号变化由紧随其后的绘制路径（<see cref="DrawIntertwinedChips"/>）重建，
        /// 偏差最多一帧且只影响滚动高度（下一帧自愈）。
        /// </summary>
        private void EnsureSocialChipLatch(long revision, float availWidth)
        {
            if (socialChipRevision == revision
                && socialChipOwnerId == detailObjectId
                && Mathf.Abs(socialChipAvailWidth - availWidth) < 0.5f
                && socialChipWidths.Count == cachedLinkedObjects.Count)
            {
                return;
            }
            socialChipRevision = revision;
            socialChipOwnerId = detailObjectId;
            socialChipAvailWidth = availWidth;

            socialChipWidths.Clear();
            socialChipTexts.Clear();
            GameFont prevFont = Text.Font;
            try
            {
                Text.Font = GameFont.Tiny;
                for (int i = 0; i < cachedLinkedObjects.Count; i++)
                {
                    LinkedObjectView link = cachedLinkedObjects[i];
                    if (link.CategoryKey != ArchiveCategoryKeys.Pawn)
                    {
                        socialChipWidths.Add(0f);
                        socialChipTexts.Add(null);
                        continue;
                    }
                    // 「名 · 共同事件 N」——分隔符与共同事件文案均复用既有键/既有写法。
                    string text = (link.Label ?? string.Empty) + " · "
                        + "PersonalChronicle.UI.SharedEventsN".Translate(link.SharedCount).ToString();
                    socialChipTexts.Add(text);
                    socialChipWidths.Add(Text.CalcSize(text).x + 12f);
                }
            }
            finally
            {
                Text.Font = prevFont;
            }
        }

        /// <summary>
        /// 按 chip latch 的实测宽度回放换行，返回换行后的行数（高度预算唯一入口）。
        /// </summary>
        /// <param name="availWidth">右栏可用宽。</param>
        /// <returns>chip 流式排布占用的行数（无 Pawn 项时为 0）。</returns>
        /// <remarks>
        /// 换行判定与 <see cref="DrawIntertwinedChips"/> 完全同式：行内累计宽（含相邻
        /// <see cref="UITheme.SpaceXxs"/> 间距）再加本枚 chip 宽超过栏宽即换行；
        /// chip 宽超过栏宽时按栏宽夹取。行数只做加法与比较，零测量、零分配。
        /// </remarks>
        private int CountSocialChipRows(float availWidth)
        {
            EnsureSocialChipLatch(socialChipRevision, availWidth);
            int rows = 0;
            float used = 0f;
            for (int i = 0; i < cachedLinkedObjects.Count && i < socialChipWidths.Count; i++)
            {
                if (cachedLinkedObjects[i].CategoryKey != ArchiveCategoryKeys.Pawn)
                {
                    continue;
                }
                float chipW = Mathf.Min(socialChipWidths[i], availWidth);
                if (rows == 0 || used + chipW > availWidth)
                {
                    rows++;
                    used = chipW + UITheme.SpaceXxs;
                }
                else
                {
                    used += chipW + UITheme.SpaceXxs;
                }
            }
            return rows;
        }

        private float DrawSocialNetwork(Rect rect, float y, PawnObject pawn, IArchiveService service)
        {
            const float basePanelHeight = 246f;
            // Max visible relation nodes on the graph. The grid slot pool supports
            // 26 positions; 24 leaves headroom and keeps the auto-fit readable.
            const float maxNodeCount = 24f;
            Rect panel = new Rect(rect.x, y, rect.width, basePanelHeight);
            ArchiveUiStyle.DrawPanel(panel, ArchiveUiStyle.PanelRaised);

            List<SocialNodeView> nodes = new List<SocialNodeView>();
            HashSet<string> seen = new HashSet<string>();
            if (pawn != null && pawn.Relations != null)
            {
                // Rank first, then cap, so closest ties always win a slot.
                List<SignificantRelation> ranked = new List<SignificantRelation>();
                for (int i = 0; i < pawn.Relations.Count; i++)
                {
                    SignificantRelation candidate = pawn.Relations[i];
                    if (candidate != null && !string.IsNullOrEmpty(candidate.OtherStableId))
                    {
                        ranked.Add(candidate);
                    }
                }
                ranked.Sort((a, b) => SocialNodeRank(a).CompareTo(SocialNodeRank(b)));

                for (int i = 0; i < ranked.Count && nodes.Count < maxNodeCount; i++)
                {
                    SignificantRelation relation = ranked[i];
                    if (!IsFamilyTreeKin(relation.RelationDefName))
                    {
                        continue;
                    }
                    if (!seen.Add(relation.OtherStableId))
                    {
                        continue;
                    }
                    ArchiveObject other = service.GetObject(relation.OtherStableId);
                    nodes.Add(new SocialNodeView
                    {
                        StableId = relation.OtherStableId,
                        Label = other != null ? ObjectDisplayLabel(other) : relation.OtherLabel,
                        RelationLabel = RelationDefLabel(relation.RelationDefName),
                        RelationDefName = relation.RelationDefName,
                        Active = relation.IsActive
                    });
                }
            }

            // Importance-driven grid slots: spouses occupy the symmetric left/right
            // anchor (the "extension centre"), parents sit above, children below,
            // siblings on the sides and friends/rivals/others on the outer ring.
            // All relation cards share the SAME size — only positions change by
            // tier — so the layout reads as a calm family tree rather than a
            // weighted importance chart.
            (int col, int row)[] slots = GridSlotsFor(nodes);
            int maxAbsCol = 0;
            int maxAbsRow = 0;
            for (int s = 0; s < slots.Length; s++)
            {
                if (Mathf.Abs(slots[s].col) > maxAbsCol) maxAbsCol = Mathf.Abs(slots[s].col);
                if (Mathf.Abs(slots[s].row) > maxAbsRow) maxAbsRow = Mathf.Abs(slots[s].row);
            }

            // Grid spacing scales with the card size AND zoom so gaps grow together
            // with the cards: cards never overlap at any zoom level. The gap ratio
            // (30% of card size) plus a 22f minimum keeps the relation-row text
            // from spilling into the next card vertically.
            const float gapRatio = 0.30f;
            const float minNodeGap = 22f;
            float baseNodeW = Mathf.Min(160f, Mathf.Max(110f, panel.width * 0.22f));
            float baseCenterW = 176f;
            float baseCenterH = 50f;
            float baseNodeH = 60f;

            // First entry (not yet scrolled): auto-fit the whole graph inside the
            // panel so every relation is visible without dragging. Only the
            // horizontal extent is constrained by the panel width — the panel
            // height already grows to fit the vertical grid (panelHeight below),
            // so fitY must NOT cap against the fixed 246f base height (which would
            // shrink dense graphs into unreadable clutter).
            // The 0.2f floor lets even very narrow panels shrink a dense graph far
            // enough that no node overflows horizontally; a higher floor would
            // leave outer columns clipped on small windows. At 0.2f cards are
            // small but remain readable and never clip outside the panel.
            float zoom = socialNetworkZoom;
            if (!socialNetworkZoomTouched && nodes.Count > 0)
            {
                float fitW = (maxAbsCol * 2 + 1) * (baseNodeW * (1f + gapRatio) + minNodeGap)
                    + baseNodeW * 2f;
                float fitX = (panel.width - 24f) / Mathf.Max(1f, fitW);
                zoom = Mathf.Clamp(fitX, 0.2f, 1f);
                socialNetworkZoom = zoom;
            }

            // v4.17 体检：面板高度计算提为局部函数（zoom 依赖），供滚轮缩放后重算。
            float ComputePanelHeightFor(float z)
            {
                float ncH = baseNodeH * z;
                float rs = ncH + Mathf.Max(ncH * gapRatio, minNodeGap * z);
                return Mathf.Max(basePanelHeight, basePanelHeight * z,
                    (maxAbsRow * 2 + 1) * rs + ncH + 32f);
            }

            // 面板高度初值（基于当前 zoom），随后才处理滚轮事件——
            // 修复旧版：滚轮命中区固定在 246f 底板（实际有节点时面板 ≥338f），
            // 图下半部滚轮缩放失效且事件漏入外层滚动视图。
            float panelHeight = ComputePanelHeightFor(zoom);
            panel = new Rect(panel.x, panel.y, panel.width, panelHeight);

            // Scroll-wheel zoom: scale the whole graph around the panel centre so
            // dense relations separate instead of overlapping. Consume the scroll
            // event so it does not also scroll the surrounding detail view.
            if (nodes.Count > 0 && panel.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.ScrollWheel && Event.current.delta.y != 0f)
                {
                    socialNetworkZoomTouched = true;
                    socialNetworkZoom = Mathf.Clamp(
                        socialNetworkZoom * (Event.current.delta.y < 0f ? 1.1f : 0.9f), 0.6f, 2.4f);
                    zoom = socialNetworkZoom;
                    // zoom 变化 → 面板高度与几何全部重算。
                    panelHeight = ComputePanelHeightFor(zoom);
                    panel = new Rect(panel.x, panel.y, panel.width, panelHeight);
                    Event.current.Use();
                }
            }

            float nodeWidth = baseNodeW * zoom;
            float centerCardW = baseCenterW * zoom;
            float centerCardH = baseCenterH * zoom;
            float nodeCardH = baseNodeH * zoom;

            // Gap scales with zoom so enlarged cards keep a proportional gap:
            // spacing = cardSize + gap, gap = cardSize*gapRatio + minNodeGap*zoom.
            float colSpacing = nodeWidth + Mathf.Max(nodeWidth * gapRatio, minNodeGap * zoom);
            float rowSpacing = nodeCardH + Mathf.Max(nodeCardH * gapRatio, minNodeGap * zoom);

            ArchiveUiStyle.DrawPanel(panel, ArchiveUiStyle.PanelRaised);

            Vector2 center = new Vector2(panel.center.x, panel.center.y) + socialNetworkPan;
            Rect centerRect = new Rect(
                center.x - centerCardW / 2f,
                center.y - centerCardH / 2f,
                centerCardW,
                centerCardH);

            // Pre-compute node positions/rects so we can tell whether the mouse
            // is over an interactive card before deciding whether to pan.
            List<Rect> nodeRects = new List<Rect>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                (int col, int row) slot = slots[i];
                Vector2 nodeCenter = new Vector2(
                    center.x + slot.col * colSpacing,
                    center.y + slot.row * rowSpacing);
                nodeRects.Add(new Rect(
                    nodeCenter.x - nodeWidth / 2f,
                    nodeCenter.y - nodeCardH / 2f,
                    nodeWidth,
                    nodeCardH));
            }

            // Left-drag pan: move the graph when dragging on empty panel background.
            // Only pan if the cursor is not over the centre card or a node card; node
            // clicks are handled by Widgets.ButtonInvisible below.
            bool mouseOverInteractive = panel.Contains(Event.current.mousePosition)
                && (centerRect.Contains(Event.current.mousePosition)
                    || nodeRects.Any(r => r.Contains(Event.current.mousePosition)));
            if (!mouseOverInteractive && panel.Contains(Event.current.mousePosition)
                && Event.current.type == EventType.MouseDrag
                && Event.current.button == 0)
            {
                socialNetworkPan += Event.current.delta;
                Event.current.Use();
            }

            // Family-tree links: edge-to-edge, kin only. Spouse uses a double stroke;
            // blood kin a single gold line; ended ties fade to the border token.
            for (int i = 0; i < nodes.Count; i++)
            {
                SocialRelationTier tier = SocialRelationTierOf(nodes[i].RelationDefName);
                Color linkColor = nodes[i].Active ? ArchiveUiStyle.Accent : ArchiveUiStyle.Border;
                float linkThickness = nodes[i].Active ? 2.2f : 1f;
                bool spouseDouble = tier == SocialRelationTier.Spouse && nodes[i].Active;
                ArchivePanelBase.DrawFamilyTreeLink(centerRect, nodeRects[i], linkColor, linkThickness, spouseDouble);
            }

            if (nodes.Count == 0)
            {
                // 空态只画提示文字，不画中心卡（分支在中心卡绘制之前提前返回）。
                UIComponents.Label(panel, "PersonalChronicle.UI.NoSignificantRelations".Translate().ToString(),
                    UITheme.FontBody, UITheme.Muted, TextAnchor.MiddleCenter);
                return panel.yMax + UITheme.SpaceXs;
            }

            ArchiveUiStyle.DrawCard(centerRect, ArchiveUiStyle.Accent);
            UIComponents.Label(centerRect, pawn != null ? ObjectDisplayLabel(pawn) : "—",
                UITheme.FontBody, UITheme.Text, TextAnchor.MiddleCenter);

            for (int i = 0; i < nodes.Count; i++)
            {
                Rect nodeRect = nodeRects[i];
                ArchiveUiStyle.DrawCard(nodeRect, nodes[i].Active ? ArchiveUiStyle.Info : ArchiveUiStyle.Muted);
                // v4.17 体检：节点卡两行文字在缩放（auto-fit 典型 z≈0.66）下重叠——
                // 旧式 label1 高 22z / label2 顶 24z 在 z<0.8 时相交，且行高随 z 缩小
                // 低于字体真实行高被垂直裁切。改为：行高固定（Small 22f / Tiny 18f），
                // label2 贴卡底；卡片放不下两行（z 过小）时只显示 label1，关系文本进 Tooltip。
                float label1Top = nodeRect.y + UITheme.SpaceXxs;
                float label2Top = nodeRect.yMax - 18f - UITheme.SpaceXxs;
                bool showRel = label2Top >= label1Top + UITheme.FontBodyLineHeight + 2f;
                UIComponents.Label(new Rect(nodeRect.x + UITheme.CardPadX, label1Top,
                    nodeRect.width - UITheme.CardPadX * 2f, UITheme.FontBodyLineHeight),
                    nodes[i].Label, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
                if (showRel)
                {
                    UIComponents.Label(new Rect(nodeRect.x + UITheme.CardPadX, label2Top,
                        nodeRect.width - UITheme.CardPadX * 2f, 18f),
                        nodes[i].RelationLabel, UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
                }
                // 完整信息（姓名 + 关系）始终可经 Tooltip 查看（缩放隐藏关系行时兜底）。
                string tip = nodes[i].Label + (showRel ? "" : " · " + nodes[i].RelationLabel);
                TooltipHandler.TipRegion(nodeRect, tip);
                if (Widgets.ButtonInvisible(nodeRect))
                {
                    NavigateTarget(service, NavTarget.Pawn, nodes[i].StableId, null);
                }
            }

            return panel.yMax + UITheme.SpaceXs;
        }

        private static SocialRelationTier SocialRelationTierOf(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return SocialRelationTier.Other;
            }
            if (defName.Contains("Spouse") || defName.Contains("Lover") || defName.Contains("Fiance"))
            {
                return SocialRelationTier.Spouse;
            }
            if (defName.Contains("Parent") || defName.Contains("Mother") || defName.Contains("Father")
                || defName.Contains("Stepparent") || defName.Contains("InLaw")
                || defName.Contains("Grandparent") || defName.Contains("Uncle") || defName.Contains("Aunt"))
            {
                return SocialRelationTier.Parent;
            }
            if (defName.Contains("Child") || defName.Contains("Son") || defName.Contains("Daughter")
                || defName.Contains("Grandchild") || defName.Contains("Nephew") || defName.Contains("Niece"))
            {
                return SocialRelationTier.Child;
            }
            if (defName.Contains("Sibling") || defName.Contains("Brother") || defName.Contains("Sister")
                || defName.Contains("Cousin") || defName.Contains("Half"))
            {
                return SocialRelationTier.Sibling;
            }
            return SocialRelationTier.Other;
        }

        /// <summary>
        /// Family-tree membership: marriage + blood/in-law kin. Friends, rivals,
        /// opinion ties and co-occurrence links are not drawn on the tree.
        /// </summary>
        private static bool IsFamilyTreeKin(string defName)
        {
            return SocialRelationTierOf(defName) != SocialRelationTier.Other;
        }


    }
}
