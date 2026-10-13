// 职业档案 ITab · 属性字段「透明结构」悬停浮层（partial）。
//
// 铁律 6.3 · 预划分 partial：浮层是独立子功能，单独成文件，不往 .Overview.cs 里堆。
// 铁律 6.2 · 单方法 > 60~80 行必须拆：浮层拆为
//     度量(Measure*) / 定位(Position) / 分区绘制(Header/Base/List/Final) / 单行绘制(PartRow)
// 六个小 helper，主方法只做编排，每个 helper 均 < 40 行。
//
// 布局风格 v2（参考原版 stat 面板 + 主流 RPG 属性面板）：
//   - 扁平列表，无树形符号（原版就是纯文本列表）
//   - 每行：标签(左) | 因子值(右对齐) | 增减百分比(最右)
//   - 可选比例条（因子偏离 1.0 时显示，视觉辅助）
//   - 紧凑行高，高信息密度
using PersonalChronicle.Archive.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    public partial class ITab_Pawn_Career
    {
        // ── 悬停态（IMGUI：状态只能存于面板实例字段，逐帧重建）──
        // DrawAttributePanel 逐帧探测命中并写入；DrawOverviewTab 在滚动区末尾统一绘制浮层。
        private OverviewFieldRegistry ovHoverField;
        private Rect ovHoverCell;
        private Pawn ovHoverPawn;

        /// <summary>浮层排版常量（v2 扁平布局，参考原版 stat 面板紧凑风格）。</summary>
        private static class Break
        {
            public const float PadX = 16f, PadY = 12f;
            /// <summary>行高 20f（紧凑但中文不裁剪）；标题行用 HeaderH=28f。</summary>
            public const float RowH = 20f, GapH = 4f;
            public const float HeaderH = 28f;
            /// <summary>分组标题行（「技能」/「健康」）高度 + 组内条目缩进量。</summary>
            public const float GroupH = 18f, GroupIndent = 12f;
            /// <summary>比例条：仅在因子显著偏离 1.0 时显示，作为视觉辅助而非主元素。</summary>
            public const float BarW = 60f, BarH = 6f;
            public const float FactorW = 52f, DeltaW = 46f;
            /// <summary>最小/最大宽度。</summary>
            public const float MinW = 300f, MaxW = 480f;
            /// <summary>T2 分区分隔符（全角冒号，中文 UI 自然）。</summary>
            public const string SectionSep = "：";
        }

        // ================= 编排入口（只做编排，绘制全在 helper）=================
        private void DrawBreakdownTooltip(Rect viewport, Vector2 scroll)
        {
            if (ovHoverField == null || ovHoverPawn == null) return;

            StatBreakdown bd = ovHoverField.BuildBreakdown(ovHoverPawn);
            if (bd == null) return;

            // IMGUI 铁律：所有可变 GUI 状态帧内捕获、帧末还原
            var gui = ArchivePanelBase.CaptureGuiState();

            float valW = MeasureValueWidth(bd, gui.font);
            float innerW = MeasureContentWidth(bd, valW, gui.font);
            float h = CalcBreakdownHeight(bd);

            Rect box = PositionBreakdown(viewport, scroll, innerW, h);
            UIComponents.TintedBox(box, UITheme.Window);
            UIComponents.Border(box, UITheme.Border);

            float cx = box.x + Break.PadX;
            float cy = box.y + Break.PadY;

            DrawBreakdownHeader(cx, cy, innerW, valW, bd);
            cy += Break.HeaderH + Break.GapH;
            cy = DrawBreakdownDescription(cx, cy, innerW, bd);
            cy = DrawBreakdownBase(cx, cy, innerW, valW, bd);
            cy = DrawBreakdownList(cx, cy, innerW, bd);
            DrawBreakdownFinal(cx, cy, innerW, valW, bd);

            ArchivePanelBase.RestoreGuiState(gui);
        }

        // ================= 度量（IMGUI 原生 Text.CalcSize）=================
        /// <summary>量最终值列宽（Medium 档——引擎只认 Tiny/Small/Medium，量前须先切档）。</summary>
        private static float MeasureValueWidth(StatBreakdown bd, GameFont restore)
        {
            Text.Font = GameFont.Medium;
            float v = Mathf.Clamp(Text.CalcSize(bd.FinalText).x + 6f, 48f, 110f);
            Text.Font = restore;
            return v;
        }

        /// <summary>量内容宽度：取「最宽明细行」与「标题行」的较大者，再钳制。</summary>
        private static float MeasureContentWidth(StatBreakdown bd, float valW, GameFont restore)
        {
            Text.Font = GameFont.Tiny;
            float maxLabelW = 80f;
            if (bd.HasRows)
                foreach (var r in bd.Rows)
                    maxLabelW = Mathf.Max(maxLabelW, Text.CalcSize(r.Label).x);

            Text.Font = GameFont.Small;
            float titleW = Text.CalcSize(bd.Title).x;
            float descW = 0f;
            if (!string.IsNullOrWhiteSpace(bd.Description))
                descW = Text.CalcSize(UIComponents.TruncateToWidth(bd.Description, Break.MaxW, GameFont.Small)).x;
            Text.Font = restore;

            // 布局：标签(含层级缩进与分组缩进) | [比例条] factor delta
            float maxIndent = 0f;
            if (bd.HasRows)
                foreach (var r in bd.Rows)
                    maxIndent = Mathf.Max(maxIndent, r.IndentLevel * Break.GroupIndent);
            float listW = maxIndent + maxLabelW + Break.BarW + Break.FactorW + Break.DeltaW + 16f;
            float w = Mathf.Max(listW, titleW + valW + 10f, descW + Break.PadX * 2f);
            return Mathf.Clamp(w, Break.MinW, Break.MaxW);
        }

        /// <summary>总高 = 头部 + [描述] + 基础值 + 明细列表（含分组标题行） + 最终值。</summary>
        private static float CalcBreakdownHeight(StatBreakdown bd)
        {
            float rowsH = bd.HasRows ? bd.Rows.Count * Break.RowH : 0f;
            float groupH = bd.GroupCount * Break.GroupH;   // 每个分组插一行标题

            return Break.PadY * 2f
                 + Break.HeaderH + Break.GapH          // T1 标题
                 + DescriptionHeight(bd)                // StatDef 描述
                 + Break.RowH + Break.GapH              // T2 基础值
                 + rowsH + groupH                       // 明细列表 + 分组标题
                 + (bd.HasRows ? Break.GapH : 0f)
                 + Break.GapH + Break.HeaderH           // T2 最终值
                 + (HasNote(bd) ? Break.GapH + Break.RowH : 0f);   // 补充说明行（派生字段：上限/品质）
        }

        /// <summary>描述行高度：有描述时占一行 + 间距，否则 0。</summary>
        private static float DescriptionHeight(StatBreakdown bd)
            => string.IsNullOrWhiteSpace(bd?.Description) ? 0f : Break.RowH + Break.GapH;

        /// <summary>定位：默认贴卡片右侧；越界翻转到左侧/上方，再钳制进滚动可视区。</summary>
        private Rect PositionBreakdown(Rect viewport, Vector2 scroll, float innerW, float h)
        {
            float w = innerW + Break.PadX * 2f;
            float x = ovHoverCell.xMax + 8f;
            float y = ovHoverCell.y;
            if (x + w > scroll.x + viewport.width) x = ovHoverCell.x - w - 8f;
            if (x < scroll.x) x = scroll.x + 4f;
            if (y + h > scroll.y + viewport.height) y = scroll.y + viewport.height - h - 4f;
            if (y < scroll.y) y = scroll.y + 4f;
            return new Rect(x, y, w, h);
        }

        // ================= 分区绘制（T1~T2，每个 < 25 行）=================
        /// <summary>T1 标题：字段名(左, Small) + 最终值(右, Medium, Accent)。</summary>
        private static void DrawBreakdownHeader(float cx, float cy, float innerW, float valW, StatBreakdown bd)
        {
            UIComponents.Label(new Rect(cx, cy, innerW - valW, Break.HeaderH),
                UIComponents.TruncateToWidth(bd.Title, innerW - valW, GameFont.Small),
                GameFont.Small, UITheme.Text, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(cx + innerW - valW, cy, valW, Break.HeaderH),
                bd.FinalText, GameFont.Medium, UITheme.Accent, TextAnchor.MiddleRight);
        }

        /// <summary>StatDef 描述行：位于标题与基础值之间，空时跳过。返回下一行 y。</summary>
        private static float DrawBreakdownDescription(float cx, float cy, float innerW, StatBreakdown bd)
        {
            if (string.IsNullOrWhiteSpace(bd.Description)) return cy;
            UIComponents.Label(new Rect(cx, cy, innerW, Break.RowH),
                UIComponents.TruncateToWidth(bd.Description, innerW, GameFont.Small),
                GameFont.Small, UITheme.Muted, TextAnchor.MiddleLeft);
            return cy + Break.RowH + Break.GapH;
        }

        /// <summary>T2 基础值：无缩进分区行。返回下一行 y。</summary>
        private static float DrawBreakdownBase(float cx, float cy, float innerW, float valW, StatBreakdown bd)
        {
            UIComponents.Label(new Rect(cx, cy, innerW - valW, Break.RowH),
                "PersonalChronicle.UI.Ov.Tip.Base".Translate().ToString() + Break.SectionSep,
                GameFont.Small, UITheme.Dim, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(cx + innerW - valW, cy, valW, Break.RowH),
                bd.BaseText, GameFont.Small, UITheme.SecondaryText, TextAnchor.MiddleRight);
            return cy + Break.RowH + Break.GapH;
        }

        /// <summary>T2 最终值：分隔线 + 无缩进分区行。</summary>
        private static void DrawBreakdownFinal(float cx, float cy, float innerW, float valW, StatBreakdown bd)
        {
            UIComponents.Rule(new Rect(cx, cy, innerW, 1f), UITheme.BorderSoft);
            cy += Break.GapH;
            UIComponents.Label(new Rect(cx, cy, innerW - valW, Break.HeaderH),
                "PersonalChronicle.UI.Ov.Tip.Final".Translate().ToString() + Break.SectionSep,
                GameFont.Small, UITheme.Text, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(cx + innerW - valW, cy, valW, Break.HeaderH),
                bd.FinalText, GameFont.Medium, UITheme.Accent, TextAnchor.MiddleRight);

            // 补充说明行（当前仅派生字段使用：如职业加成的「理论上限 / 品质偏移」）
            if (HasNote(bd))
            {
                UIComponents.Label(new Rect(cx, cy + Break.HeaderH + Break.GapH, innerW, Break.RowH),
                    UIComponents.TruncateToWidth(bd.FinalizeNote, innerW, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>是否有补充说明行（原版字段的 FinalizeNote 已在上游被清洗为空）。</summary>
        private static bool HasNote(StatBreakdown bd)
            => bd != null && !string.IsNullOrEmpty(bd.FinalizeNote);

        // ================= 明细列表（扁平风格，参考原版 stat 面板）=================
        /// <summary>明细列表：分组标题（技能/健康）+ 组内条目。返回下一行 y。</summary>
        private static float DrawBreakdownList(float cx, float cy, float innerW, StatBreakdown bd)
        {
            if (!bd.HasRows) return cy;

            string prevGroup = null;
            for (int i = 0; i < bd.Rows.Count; i++)
            {
                BreakdownRow r = bd.Rows[i];
                bool grouped = !string.IsNullOrEmpty(r.Group);
                // 分组切换 → 先插入一行分组标题（同原版「技能」/「健康」）
                if (grouped && r.Group != prevGroup)
                {
                    DrawGroupHeader(cx, cy, innerW, r.Group);
                    cy += Break.GroupH;
                    prevGroup = r.Group;
                }
                DrawBreakdownPartRow(cx, cy, innerW, r, grouped ? Break.GroupIndent : 0f);
                cy += Break.RowH;
            }
            return cy + Break.GapH;
        }

        /// <summary>分组标题行（如「技能」「健康」）：Dim 色，视觉上退到背景。</summary>
        private static void DrawGroupHeader(float cx, float cy, float innerW, string group)
        {
            UIComponents.Label(new Rect(cx, cy, innerW, Break.GroupH),
                group, GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
        }

        /// <summary>单条目：标签(左) | [比例条] 因子值(右对齐) | 增减百分比(最右)。
        /// 参考原版格式："Isekai DEX (16): ×1.33" → 清洗后 "Isekai DEX  ×1.33  +33%"</summary>
        private static void DrawBreakdownPartRow(float cx, float cy, float innerW, BreakdownRow r, float indent)
        {
            Color c = UITheme.StateColor(r.IsBuff, r.IsDebuff);

            // 铁律 7.3：叠加原版层级缩进（IndentLevel）与分组缩进（indent 参数）
            float hierarchyIndent = r.IndentLevel * Break.GroupIndent;
            float lx = cx + indent + hierarchyIndent;
            float avail = Mathf.Max(0f, innerW - indent - hierarchyIndent);
            // 右侧固定宽度区：factor + delta
            float rightZone = Break.FactorW + Break.DeltaW + 8f;
            // 如果有显著偏移才显示比例条
            float barExtra = (Mathf.Abs(r.BarShare - 0.5f) > 0.15f) ? Break.BarW + 4f : 0f;
            float labelW = avail - rightZone - barExtra - 4f;

            // 标签（左对齐，Muted 色退到背景）
            UIComponents.Label(new Rect(lx, cy, labelW, Break.RowH),
                UIComponents.TruncateToWidth(r.Label, labelW, GameFont.Tiny),
                GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);

            float rx = lx + labelW + 4f;

            // 可选比例条（仅显著偏移时显示）
            if (barExtra > 0f)
            {
                UIComponents.ProgressBar(
                    new Rect(rx, cy + (Break.RowH - Break.BarH) / 2f, Break.BarW, Break.BarH),
                    r.BarShare, c);
                rx += Break.BarW + 4f;
            }

            // 因子值（×1.26）
            UIComponents.Label(new Rect(rx, cy, Break.FactorW, Break.RowH),
                r.FactorText, GameFont.Tiny, c, TextAnchor.MiddleRight);

            // 增减百分比（+26%）
            UIComponents.Label(new Rect(cx + innerW - Break.DeltaW, cy, Break.DeltaW, Break.RowH),
                r.DeltaText, GameFont.Tiny, c, TextAnchor.MiddleRight);
        }
    }
}
