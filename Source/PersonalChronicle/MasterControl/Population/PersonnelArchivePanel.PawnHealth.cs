using PersonalChronicle.Archive;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using RimWorld;
using UnityEngine;
using Verse;
using ReadModels = PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of <see cref="PersonnelArchivePanel"/> ——人物档案的**健康 Tab**（预览
    /// <c>#v-pawn</c> 第 5 个子页 t5，HTML 1942-2007，自注「末项承载原版健康模块：
    /// PawnCapacityDef 11 项 + Hediff 清单」）。
    ///
    /// <para><b>v5.x 重写（子页行级对照清单 P0）</b>：预览是 <c>1.05fr 1fr 双栏</c> ——
    /// 左 = 整体健康（三态徽章 + 3 格）→ 能力清单（原版 <see cref="PawnCapacityDef"/> 双列）
    /// → 接入说明；右 = 健康状态（<see cref="Hediff"/> 清单 + 疤痕 / 疼痛 / 感染计数）。
    /// 旧实现单栏，且把「三维度条」（Body/Spirit/Youth，属 t1 健康残值块）重复画在本页 ⇒
    /// 本版**移除该重复内容**（同一个人不该有两套三维条）。</para>
    ///
    /// <para><b>口径</b>：健康估值相关数字（综合分 / 身体完整度 / 残值）全部来自既有
    /// <c>cachedHealth</c>（Read Model），与「健康残值 · 资产折旧」块**同源**；能力与 Hediff
    /// 为**实时活读**（<c>pawn.health.capacities.GetLevel</c> / <c>health.hediffSet</c>，
    /// 1.6 程序集已核实：无 <c>GetCapacity</c>，只有 <c>GetLevel</c>）。
    /// 无活体 pawn（已亡 / 离场）⇒ 如实画空态，不伪造读数（LAND-009）。</para>
    /// </summary>
    public sealed partial class PersonnelArchivePanel
    {
        /// <summary>能力清单行高 —— 预览 `grid-auto-rows:16px` 小于 Tiny 行高 18（LAND-007 违规），
        /// 按既有清单 §5-3 口径取 **18**（组件绘制铁律 1：行高是字体度量，不可压缩）。</summary>
        private const float CapacityRowHeight = 18f;

        /// <summary>Hediff 清单最多绘制行数（预览示例 5 行）。</summary>
        private const int HediffRowLimit = 5;

        private void DrawPawnHealth(Rect panel, IArchiveService service)
        {
            ReadModels.HealthView health = cachedHealth;
            if (health == null || !health.IsDefined)
            {
                // 无健康估值（旧档 / 数据被清理）⇒ 如实说明，不画 0 分假值（LAND-009）。
                UIComponents.Label(new Rect(panel.x, panel.y, panel.width, 24f),
                    "PersonalChronicle.UI.Health.Unavailable".Translate().ToString(),
                    GameFont.Small, UITheme.Muted, TextAnchor.MiddleLeft);
                return;
            }

            Pawn livePawn = service != null ? service.GetLivePawn(detailObjectId) : null;
            float colGap = UITheme.GridGap;
            float leftW = Mathf.Max(160f, (panel.width - colGap) * (1.05f / 2.05f));
            Rect left = new Rect(panel.x, panel.y, leftW, panel.height);
            Rect right = new Rect(panel.x + leftW + colGap, panel.y,
                Mathf.Max(160f, panel.width - leftW - colGap), panel.height);

            float ly = DrawOverallHealth(left, left.y, health, livePawn);
            ly = DrawCapacityList(left, ly + UITheme.BlockGap, livePawn);
            DrawHealthAccessNote(left, ly + UITheme.BlockGap);
            DrawHediffList(right, right.y, livePawn);
        }

        /// <summary>
        /// 整体健康：三态徽章（良好 / 受伤 / 濒危，取原版 <c>summaryHealth</c>）+ 3 格
        /// （综合健康分 / 身体完整度 / 伤口·旧伤）—— 预览 HTML 1944-1956。
        /// </summary>
        private float DrawOverallHealth(Rect rect, float y, ReadModels.HealthView health, Pawn pawn)
        {
            float headH = 26f;
            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.OverallHealth");
            float bodyY = y + headH + UITheme.SpaceXxs;

            float summary = pawn != null && pawn.health != null && pawn.health.summaryHealth != null
                ? pawn.health.summaryHealth.SummaryHealthPercent
                : 1f;
            string stateKey = summary > HealthGoodThreshold
                ? "PersonalChronicle.UI.HealthGood"
                : (summary > HealthInjuredThreshold
                    ? "PersonalChronicle.UI.HealthInjured"
                    : "PersonalChronicle.UI.HealthCritical");
            Color stateColor = summary > HealthGoodThreshold
                ? UITheme.Alive
                : (summary > HealthInjuredThreshold ? UITheme.Info : UITheme.Blood);
            Rect badge = new Rect(rect.x + UITheme.CardPadX, bodyY, 76f, 20f);
            UIComponents.Pill(badge, stateKey.Translate().ToString(), stateColor);

            int woundCount = CountWoundsOf(pawn);
            float rowY = badge.yMax + 6f;
            float innerW = rect.width - UITheme.CardPadX * 2f;
            float gap = UITheme.GridGap;
            float cellW = (innerW - gap * 2f) / 3f;
            float cellH = 46f;
            DrawHealthStatCell(new Rect(rect.x + UITheme.CardPadX, rowY, cellW, cellH),
                "PersonalChronicle.UI.Health.Score".Translate().ToString(),
                Mathf.RoundToInt(health.HealthScore).ToString(), stateColor);
            DrawHealthStatCell(new Rect(rect.x + UITheme.CardPadX + cellW + gap, rowY, cellW, cellH),
                "PersonalChronicle.UI.HealthValuation.Body".Translate().ToString(),
                Mathf.RoundToInt(health.BodyPercent * 100f).ToString() + "%", UITheme.Text);
            DrawHealthStatCell(new Rect(rect.x + UITheme.CardPadX + (cellW + gap) * 2f, rowY, cellW, cellH),
                "PersonalChronicle.UI.Health.Wounds".Translate().ToString(),
                woundCount.ToString(), woundCount > 0 ? UITheme.Threat : UITheme.Alive);
            return rowY + cellH;
        }

        /// <summary>3 格统计单元（预览 `.stat3 .statc`：小标签 + 大数值）。</summary>
        private static void DrawHealthStatCell(Rect rect, string label, string value, Color valueColor)
        {
            Widgets.DrawBoxSolid(rect, UITheme.Card);
            UIComponents.Border(rect, UITheme.BorderSoft);
            float inner = Mathf.Max(0f, rect.width - 20f);
            UIComponents.Label(new Rect(rect.x + 10f, rect.y + 5f, inner, 16f),
                UIComponents.TruncateToWidth(label, inner, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
            UIComponents.Label(new Rect(rect.x + 10f, rect.y + 23f, inner, 20f),
                UIComponents.TruncateToWidth(value, inner, GameFont.Small),
                GameFont.Small, valueColor, TextAnchor.UpperLeft);
        }

        /// <summary>
        /// 能力清单：原版 <see cref="PawnCapacityDef"/> 双列（预览 HTML 1959-1981，实测 11 项
        /// 两列 row-major、列内 `52px 1fr 34px`）。满值显绿，&lt; 85% 警示。
        /// 无活体 pawn ⇒ 空态一行（不画假读数）。
        /// </summary>
        private float DrawCapacityList(Rect rect, float y, Pawn pawn)
        {
            float headH = 26f;
            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.Health.Capacity.Title");
            float bodyY = y + headH + UITheme.SpaceXxs;

            if (pawn == null || pawn.health == null || pawn.health.capacities == null)
            {
                UIComponents.Label(new Rect(rect.x + UITheme.CardPadX, bodyY, rect.width - UITheme.CardPadX * 2f, 18f),
                    "PersonalChronicle.UI.Health.Unavailable".Translate().ToString(),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
                return bodyY + 18f;
            }

            List<PawnCapacityDef> defs = CapacityDefsForDisplay();
            if (defs.Count == 0)
            {
                return bodyY;
            }
            float innerW = rect.width - UITheme.CardPadX * 2f;
            float colGap = 16f;
            float colW = (innerW - colGap) * 0.5f;
            float labelW = 52f;
            float valueW = 34f;
            float barW = Mathf.Max(20f, colW - labelW - valueW - 4f);
            int rows = (defs.Count + 1) / 2;
            for (int i = 0; i < defs.Count; i++)
            {
                // row-major：先填满左列再填右列（与预览实测一致）。
                int column = i % 2;
                int rowIndex = i / 2;
                float rowY = bodyY + rowIndex * CapacityRowHeight;
                float cx = rect.x + UITheme.CardPadX + column * (colW + colGap);
                PawnCapacityDef def = defs[i];
                float value = Mathf.Clamp01(pawn.health.capacities.GetLevel(def));
                string label = def.LabelCap.ToString();
                UIComponents.Label(new Rect(cx, rowY, labelW, CapacityRowHeight),
                    UIComponents.TruncateToWidth(label, labelW - 2f, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                // 【RUIF 组件 · 1:1 补全 C3】能力条改走 `RUIF.ProgressAtoms.CapacityBar`
                //   （轨道 `BorderSoft` + 按比例填充 + 溢出警示段）—— 旧实现是自绘 `UIComponents.ProgressBar`。
                //   颜色显式传入（RUIF 默认是「容量占用」语义：>85% 取 Warn，与本视图的三档语义相反）。
                //   本视图 value 已 Clamp01 ⇒ 恒不触发溢出段。
                Rect bar = new Rect(cx + labelW, rowY + (CapacityRowHeight - 8f) * 0.5f, barW, 8f);
                Color barColor = value >= 0.999f
                    ? UITheme.Alive
                    : (value < 0.85f ? UITheme.Threat : UITheme.Info);
                if (!RuifBridge.TryCapacityBar(bar, value, 1f, barColor))
                {
                    UIComponents.ProgressBar(bar, value, barColor);
                }
                UIComponents.Label(new Rect(cx + labelW + barW + 4f, rowY, valueW, CapacityRowHeight),
                    Mathf.RoundToInt(value * 100f).ToString() + "%",
                    GameFont.Tiny, UITheme.Text, TextAnchor.MiddleRight);
            }
            return bodyY + rows * CapacityRowHeight;
        }

        /// <summary>
        /// 可展示的能力项（原版顺序）：取 `showOnHumanlikes` 的 <see cref="PawnCapacityDef"/>
        /// （含原版「疼痛 / Pain」——原版健康卡同样按能力条呈现）。Def 顺序 = 加载序（原版已按
        /// `displayOrder` 排列），不自行排序（GOV-009：不按 label 判定）。
        /// <para>⚠ 程序集核实（1.6 本 Build）：<c>PawnCapacityDef</c> **无 `isBad` 字段**
        /// （`isBad` 属 `HediffDef`）；亦无 `GetCapacity` / `GetMaxCapacity`
        /// ⇒ 读数只能走 <c>capacities.GetLevel(def)</c>。</para>
        /// </summary>
        private static List<PawnCapacityDef> CapacityDefsForDisplay()
        {
            List<PawnCapacityDef> defs = new List<PawnCapacityDef>();
            List<PawnCapacityDef> all = DefDatabase<PawnCapacityDef>.AllDefsListForReading;
            if (all == null)
            {
                return defs;
            }
            for (int i = 0; i < all.Count; i++)
            {
                PawnCapacityDef def = all[i];
                if (def != null && def.showOnHumanlikes)
                {
                    defs.Add(def);
                }
            }
            return defs;
        }

        /// <summary>
        /// 健康状态：<see cref="Hediff"/> 清单（前 <see cref="HediffRowLimit"/> 行，
        /// 「部位·病症」+ 严重度/旧伤）+ 疤痕 / 疼痛 / 感染计数（预览 HTML 1985-1995）。
        /// </summary>
        private void DrawHediffList(Rect rect, float y, Pawn pawn)
        {
            float headH = 26f;
            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.Hediffs");
            float bodyY = y + headH + UITheme.SpaceXxs;

            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                DrawDetailRow(rect.x, bodyY, rect.width,
                    "PersonalChronicle.UI.NoHediffs".Translate().ToString(), string.Empty);
                return;
            }
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            if (hediffs == null || hediffs.Count == 0)
            {
                DrawDetailRow(rect.x, bodyY, rect.width,
                    "PersonalChronicle.UI.NoHediffs".Translate().ToString(), string.Empty);
                return;
            }

            float rowY = bodyY;
            int shown = 0;
            for (int i = 0; i < hediffs.Count && shown < HediffRowLimit; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff == null)
                {
                    continue;
                }
                string label = hediff.LabelBase;
                if (string.IsNullOrEmpty(label) && hediff.def != null)
                {
                    label = hediff.def.label;
                }
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }
                string part = hediff.Part != null && hediff.Part.def != null ? hediff.Part.def.label : null;
                string text = string.IsNullOrEmpty(part) ? label : part + " · " + label;
                string value = hediff.IsPermanent()
                    ? "PersonalChronicle.UI.Health.Permanent".Translate().ToString()
                    : hediff.Severity.ToString("0.00");
                rowY = DrawDetailRow(rect.x, rowY, rect.width, text, value);
                shown++;
            }

            // 计数行（口径与预览同序）：疤痕总数 / 当前疼痛 / 感染·疾病。
            int scars = 0;
            int infections = 0;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff == null || hediff.def == null || !hediff.def.isBad)
                {
                    continue;
                }
                if (hediff.Part != null)
                {
                    scars++;
                }
                else
                {
                    infections++;
                }
            }
            float pain = pawn.health.hediffSet.PainTotal;
            rowY = DrawDetailRow(rect.x, rowY, rect.width,
                "PersonalChronicle.UI.Health.Scars".Translate().ToString(), scars.ToString());
            rowY = DrawDetailRow(rect.x, rowY, rect.width,
                "PersonalChronicle.UI.Health.Pain".Translate().ToString(),
                Mathf.RoundToInt(Mathf.Clamp01(pain) * 100f).ToString() + "%");
            DrawDetailRow(rect.x, rowY, rect.width,
                "PersonalChronicle.UI.Health.Infections".Translate().ToString(), infections.ToString());
        }

        /// <summary>伤口·旧伤计数（= 带部位的有害 Hediff，含疤痕），用于整体健康 3 格。</summary>
        private static int CountWoundsOf(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null
                || pawn.health.hediffSet.hediffs == null)
            {
                return 0;
            }
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            int count = 0;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff != null && hediff.def != null && hediff.def.isBad && hediff.Part != null)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>接入说明（预览 HTML 1997-2005）：如实写明本页取数来源与空态口径。</summary>
        private void DrawHealthAccessNote(Rect rect, float y)
        {
            float headH = 26f;
            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.Health.Access.Title");
            UIComponents.Label(new Rect(rect.x + UITheme.CardPadX, y + headH + UITheme.SpaceXxs,
                    rect.width - UITheme.CardPadX * 2f, 54f),
                "PersonalChronicle.UI.Health.Access.Note".Translate().ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
        }
    }
}
