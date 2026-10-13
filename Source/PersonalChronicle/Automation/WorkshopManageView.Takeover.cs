using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间管理 · 设置页「接管原版工作」行（<c>CHG-20261003-TAKEOVER</c> · Phase 2 / EXC-2026-016）。
    ///
    /// <para><b>默认关闭</b>：本行只是<b>两个按钮</b>（接管 / 解除），不做任何自动触发。
    /// 玩家不点「接管」，原版工作面板永远不受影响（EXC-2026-016 的授权前提）。</para>
    ///
    /// <para><b>绘制纪律</b>：按钮态与文案在 <see cref="Refresh"/> 期算好（<c>takeoverLedgerEntries</c>），
    /// 绘制路径只读；点击后由 <see cref="Refresh"/> 让状态立刻反映（MATRIX-003：UI 只发命令）。</para>
    /// </summary>
    internal sealed partial class WorkshopManageView
    {
        /// <summary>接管按钮宽（与同页其它按钮同档）。</summary>
        private const float TakeoverBtnW = 150f;

        /// <summary>台账中可还原的项数（Refresh 期读取；&gt;0 ⇒ 「解除」可用）。</summary>
        private int takeoverLedgerEntries;

        /// <summary>上次操作的结果文案（Refresh 期翻译，Keyed）。</summary>
        private string takeoverResultKey = string.Empty;

        /// <summary>上次操作的项数（Refresh 期记录）。</summary>
        private int takeoverResultCount;

        /// <summary>绘制「接管原版工作」行：左文案 + 右两个按钮（接管 / 解除）。</summary>
        private void DrawTakeoverRow(Rect row)
        {
            float btnW = TakeoverBtnW + Gap;
            Rect textRect = new Rect(row.x, row.y, Mathf.Max(0f, row.width - btnW * 2f),
                row.height);
            string text = string.IsNullOrEmpty(takeoverResultKey)
                ? T("TakeoverIdle")
                : T1("TakeoverDone", takeoverResultCount) + T(takeoverResultKey);
            UIComponents.Label(textRect,
                UIComponents.TruncateToWidth(text, textRect.width, UITheme.FontLabel, textRect),
                UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);

            MapComponent_WorkshopManager manager = Manager;
            // ① 接管（默认不触发；玩家主动点才发起）
            Rect takeRect = new Rect(row.xMax - btnW * 2f + Gap, row.y, TakeoverBtnW, row.height);
            if (UIComponents.ThemedButton(takeRect, T("TakeoverDo")))
            {
                string reason = string.Empty;
                int n = 0;
                if (manager != null)
                {
                    n = manager.TakeOverVanillaWork(zone, out reason);
                }
                else
                {
                    reason = "zone_missing";
                }
                takeoverResultKey = n > 0 ? "TakeoverOk" : ("Takeover_" + reason);
                takeoverResultCount = n;
                Refresh();
            }
            TooltipHandler.TipRegion(takeRect,
                T("TakeoverDoTip"));
            // ② 解除（只还原台账里的项；无台账时禁用而非隐藏 —— 让玩家始终知道这个出口存在）
            Rect relRect = new Rect(row.xMax - TakeoverBtnW, row.y, TakeoverBtnW, row.height);
            bool canRelease = manager != null && takeoverLedgerEntries > 0;
            if (canRelease && UIComponents.ThemedButton(relRect, T("TakeoverRelease")))
            {
                int n = manager.ReleaseVanillaWork();
                takeoverResultKey = n > 0 ? "TakeoverReleased" : "TakeoverReleaseNone";
                takeoverResultCount = n;
                Refresh();
            }
            if (!canRelease)
            {
                UIComponents.Label(relRect, T("TakeoverRelease"), UITheme.FontLabel,
                    UITheme.Muted, TextAnchor.MiddleCenter);
            }
            TooltipHandler.TipRegion(relRect, T("TakeoverReleaseTip"));
        }
    }
}