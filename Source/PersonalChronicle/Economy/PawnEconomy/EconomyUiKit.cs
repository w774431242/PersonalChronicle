using PersonalChronicle.Domain.PawnEconomy.Care;
using PersonalChronicle.Domain.PawnEconomy.Governance;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// **经济窗口 UI 公共件**（P0-1，2026-10-10）。
    ///
    /// <para>审计发现"状态语义四处四样"（机构页=语义色 chip + 色条；需求页=色点 + 着色文本；政策/照护/概览=纯文本无颜色）、
    /// "卡片骨架各写各的"。本类把机构页已验过的做法**升格为全窗唯一实现**：卡片骨架、状态徽章、
    /// 以及**唯一的两张状态→语义色映射表**。其余页签重构时直接调用，不再各自取色、各自拼骨架。</para>
    ///
    /// <para>纪律：本类只做绘制，**零状态**；所有几何由 <c>RUIF.Measure</c>/<c>Tokens</c> 派生；不读写任何经济数据。</para>
    /// </summary>
    internal static class EconomyUiKit
    {
        /// <summary>左侧语义色条宽度（装饰值，不参与布局预算）。</summary>
        public const float AccentBarWidth = 3f;

        /// <summary>卡片骨架：底 + 描边 + 可选左侧语义色条；返回**内容区**（已扣 <c>CardPadX/Y</c> 内边距）。
        /// 悬停时底与描边同时提升（六态中的 hover 表达）。</summary>
        public static Rect CardFrame(Rect rect, bool hovered, Color? accent)
        {
            RUIF.Draw.Box(rect, hovered ? RUIF.Tokens.CardHover : RUIF.Tokens.Card);
            RUIF.Draw.Border(rect, hovered ? RUIF.Tokens.Border : RUIF.Tokens.BorderSoft);
            if (accent.HasValue)
                RUIF.Draw.Box(new Rect(rect.x, rect.y, AccentBarWidth, rect.height), accent.Value);
            return RUIF.Layout.Inset(rect, RUIF.Tokens.CardPadX, RUIF.Tokens.CardPadY);
        }

        /// <summary>状态徽章：淡底（14% alpha）+ 同色描边（50%）+ 居中标签；宽度由 <c>Measure.Width</c> 精算。
        /// 返回徽章矩形，供调用方挂 tooltip（徽章只表达"状态色"，完整原因由悬停给出）。</summary>
        public static Rect StatusChip(Rect rect, string label, Color tint)
            => StatusChip(rect, label, tint, RUIF.Measure.Width(label, GameFont.Tiny) + RUIF.Tokens.SpaceSm);

        /// <summary>预量宽版本（绘制路径零 <c>Measure.Width</c>）：宽度由调用方在测量/快照期精算并缓存。</summary>
        public static Rect StatusChip(Rect rect, string label, Color tint, float measuredWidth)
        {
            var chip = new Rect(rect.x, rect.y, measuredWidth, rect.height);
            RUIF.Draw.Box(chip, RUIF.Draw.WithAlpha(tint, 0.14f));
            RUIF.Draw.Border(chip, RUIF.Draw.WithAlpha(tint, 0.5f));
            RUIF.Draw.Label(chip, label, GameFont.Tiny, tint, TextAnchor.MiddleCenter);
            return chip;
        }

        // CHG-20261012 · B2：`CapabilityColor(InstitutionCapability)` 已删 —— 机构能力徽章（及其五级原因码）
        // 随机构政策整体退役，卡片改为单行只读 ⇒ 该映射没有任何调用者。
        // 制度阶段的映射（下方 `PhaseColor`）**保留**：它是制度开关的读数，与政策无关。

        /// <summary>**制度阶段 → 语义色（唯一映射）**：运行中=Alive；准备中=Warn；故障=Dead；退出中=Info；其余=Muted。</summary>
        public static Color PhaseColor(InstitutionPhase phase)
        {
            switch (phase)
            {
                case InstitutionPhase.Active: return RUIF.Tokens.Alive;
                case InstitutionPhase.Preparing: return RUIF.Tokens.Warn;
                case InstitutionPhase.Faulted: return RUIF.Tokens.Dead;
                case InstitutionPhase.Draining: return RUIF.Tokens.Info;
                default: return RUIF.Tokens.Muted;
            }
        }

        /// <summary>**照护服务能力 → 语义色（唯一映射）**：就绪可收费=Alive；准备中=Warn；已暂停=Dead；
        /// 其余（原版公有：未纳入本经济体系）=Dim。与 <see cref="CapabilityColor"/> 同一纪律 —— 语义只有一张表。</summary>
        public static Color CareCapabilityColor(ServiceCapability capability)
        {
            switch (capability)
            {
                case ServiceCapability.ReadyPaid: return RUIF.Tokens.Alive;
                case ServiceCapability.Preparing: return RUIF.Tokens.Warn;
                case ServiceCapability.Suspended: return RUIF.Tokens.Dead;
                default: return RUIF.Tokens.Dim;
            }
        }

        /// <summary>**照护授权状态 → 语义色（唯一映射）**：已过期=Dim；额度用尽=Warn；仍可用=Alive。</summary>
        public static Color AuthorizationColor(bool expired, bool exhausted)
            => expired ? RUIF.Tokens.Dim : exhausted ? RUIF.Tokens.Warn : RUIF.Tokens.Alive;

        /// <summary>**柜台工作台语调 → 语义色（唯一映射）**：与上面四张**状态**表职责不同 ——
        /// 它表达 KPI 的强调程度（有值=Accent / 需注意=Warn / 未配置或为零=Dim），不承载状态语义。
        /// 应用层只给语义名（<c>WorkspaceTone</c>），颜色一律在这里映射，绘制代码不得自取色。</summary>
        public static Color ToneColor(PersonalChronicle.Application.PawnEconomy.WorkspaceTone tone)
        {
            switch (tone)
            {
                case PersonalChronicle.Application.PawnEconomy.WorkspaceTone.Accent: return RUIF.Tokens.Accent;
                case PersonalChronicle.Application.PawnEconomy.WorkspaceTone.Alive: return RUIF.Tokens.Alive;
                case PersonalChronicle.Application.PawnEconomy.WorkspaceTone.Warn: return RUIF.Tokens.Warn;
                case PersonalChronicle.Application.PawnEconomy.WorkspaceTone.Dead: return RUIF.Tokens.Dead;
                case PersonalChronicle.Application.PawnEconomy.WorkspaceTone.Muted: return RUIF.Tokens.Muted;
                case PersonalChronicle.Application.PawnEconomy.WorkspaceTone.Dim: return RUIF.Tokens.Dim;
                default: return RUIF.Tokens.Text;
            }
        }
    }
}
