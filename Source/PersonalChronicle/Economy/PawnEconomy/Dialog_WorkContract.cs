using System;
using System.Globalization;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// CHG-20261012-ECON-CONTRACTIZE：「工作合同弹窗」—— 在「工作需求」表点击一行后，草拟**一份具体合同**
    /// （工作数量 / 允许工作时段 / 单件薪资；薪资预填**推荐价**，玩家可自主改）。确认即产出
    /// **Posted（已派发）** 合同，进入「工作合同」表等待殖民者承接。
    ///
    /// <para><b>层与依赖（为什么是"回调式"）</b>：本类位于 Economy/PawnEconomy（UI 层），
    /// **不查业务数据、不写任何状态** —— 可派发件数与推荐价由宿主在打开时算好传入（快照期口径），
    /// 写入由 <c>onConfirm</c> 回调完成（宿主调 <c>TryDraftVanillaWorkContract</c>）。
    /// 因此这里只有 UI 与**纯函数**校验（<see cref="WorkContractRules"/>），与 <c>Dialog_TimeRange</c>
    /// 的回调式范式一致；绘制路径零 Measure 之外的开销。</para>
    ///
    /// <para><b>零 Widgets/GUI</b>：全部走 RUIF 原子 —— 与 <c>verify-pawn-economy.ps1</c> 的 UI 面断言
    /// （"前端不得直触 Widgets/GUI"）一致。</para>
    /// </summary>
    internal sealed class Dialog_WorkContract : Window
    {
        private const float RowH = 30f;
        private const float Gap = 8f;
        private const long WageStepMinor = 10L;          // 0.10 银/次
        private const long WageMaximumMinor = 100000L;   // 1000.00 银（输入上限；域合法性仍由纯函数裁决）
        private const string KeyPrefix = "PC_PEA.Dialog.WorkContract.";

        private readonly string workTypeLabel;
        private readonly int availableJobs;
        private readonly long recommendedMinor;
        private readonly bool recommendedFromMarket;
        private readonly string configuredQuoteLabel;
        private readonly Func<int, long, int, int, string> onConfirm;

        private int jobs;
        private long wageMinor;
        private int startHour, endHour;
        /// <summary>上一次确认的拒绝原因（稳定 ID；null = 无）。**不关窗** —— 玩家改一改再试。</summary>
        private string failure;

        public override Vector2 InitialSize => new Vector2(440f, 340f);

        /// <summary><paramref name="onConfirm"/> 返回 null = 成功（关窗）；非 null = 稳定 reason ID（留在窗内如实显示）。</summary>
        internal Dialog_WorkContract(string workTypeLabel, int availableJobs, long recommendedWageMinor,
            bool recommendedFromMarket, Func<int, long, int, int, string> onConfirm, string configuredQuoteLabel = null)
        {
            this.workTypeLabel = workTypeLabel ?? string.Empty;
            this.availableJobs = availableJobs > 0 ? availableJobs : 0;
            this.recommendedMinor = recommendedWageMinor > 0L ? recommendedWageMinor : WorkContractRules.RecommendedBaseMinor;
            this.recommendedFromMarket = recommendedFromMarket;
            this.configuredQuoteLabel = configuredQuoteLabel;
            this.onConfirm = onConfirm;
            jobs = WorkContractRules.ClampJobs(8, this.availableJobs);      // 预填 8 件（可派发不足则取下限/上限）
            wageMinor = this.recommendedMinor;                              // 预填推荐价（玩家可改）
            startHour = WorkContractRules.DefaultWindowStartHour;
            endHour = WorkContractRules.DefaultWindowEndHour;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            doCloseX = true;
            draggable = true;
            resizeable = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;
            RUIF.Draw.Label(new Rect(inRect.x, y, inRect.width, RowH), T("Title"), RUIF.Tokens.FontLabel,
                RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            y += RowH;
            RUIF.Draw.Label(new Rect(inRect.x, y, inRect.width, RowH), workTypeLabel, RUIF.Tokens.FontLabel,
                RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            y += RowH + Gap;

            y = DrawJobsRow(inRect, y);
            y = DrawHourRow(inRect, y, T("StartHour"), ref startHour);
            y = DrawHourRow(inRect, y, T("EndHour"), ref endHour);
            y = DrawWageRow(inRect, y);

            // 推荐价出处如实标注：来自产出市价 ⇒ 显示"推荐市价"；不可得回落 ⇒ 显示"基准价"（诚实性）。
            string recommend = (configuredQuoteLabel ?? (recommendedFromMarket ? T("Recommended") : T("Baseline")))
                + " " + Silver(recommendedMinor);
            RUIF.Draw.Label(new Rect(inRect.x, y, inRect.width, RowH), recommend, RUIF.Tokens.FontLabel,
                RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            y += RowH;

            if (!string.IsNullOrEmpty(failure))
            {
                RUIF.Draw.Label(new Rect(inRect.x, y, inRect.width, RowH), ReasonText(failure),
                    RUIF.Tokens.FontLabel, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            }

            float footerY = inRect.yMax - RowH;
            float okW = 140f;
            if (RUIF.ButtonAtoms.PrimaryButton(new Rect(inRect.xMax - okW, footerY, okW, RowH),
                    T("Confirm"), availableJobs > 0, RUIF.ButtonTone.Accent))
            {
                Confirm();
            }
            if (RUIF.ButtonAtoms.PrimaryButton(new Rect(inRect.xMax - okW * 2f - Gap, footerY, okW, RowH),
                    T("Cancel"), true, RUIF.ButtonTone.Default))
            {
                Close();
            }
        }

        /// <summary>确认：先过**纯函数**校验（本地即时反馈），再交给宿主写入口 —— 双保险：
        /// 本地校验拦住明显非法值，宿主校验兜住竞态（件数被他人抢走 / 国库余额变化）。</summary>
        private void Confirm()
        {
            if (!WorkContractRules.ValidateDraft(jobs, wageMinor, startHour, endHour, availableJobs,
                    long.MaxValue, 0L, out string localReason))
            {
                failure = localReason;
                return;
            }
            string reason = onConfirm == null ? "NoHost" : onConfirm(jobs, wageMinor, startHour, endHour);
            if (reason == null) { Close(); return; }
            failure = reason;
        }

        private float DrawJobsRow(Rect rect, float y)
        {
            const float btnW = 30f;
            RUIF.Draw.Label(new Rect(rect.x, y, 130f, RowH), T("Jobs"), RUIF.Tokens.FontLabel,
                RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            var minus = new Rect(rect.x + 140f, y, btnW, RowH);
            if (RUIF.ButtonAtoms.PrimaryButton(minus, "-", jobs > WorkContractRules.MinimumJobs, RUIF.ButtonTone.Default))
                jobs = WorkContractRules.ClampJobs(jobs - 1, availableJobs);
            var plus = new Rect(rect.xMax - btnW, y, btnW, RowH);
            if (RUIF.ButtonAtoms.PrimaryButton(plus, "+", jobs < availableJobs, RUIF.ButtonTone.Default))
                jobs = WorkContractRules.ClampJobs(jobs + 1, availableJobs);
            var box = new Rect(minus.xMax + Gap, y, plus.x - minus.xMax - Gap * 2f, RowH);
            RUIF.Draw.Label(box, jobs.ToString(CultureInfo.InvariantCulture) + " / "
                + availableJobs.ToString(CultureInfo.InvariantCulture), RUIF.Tokens.FontLabel,
                RUIF.Tokens.Muted, TextAnchor.MiddleCenter);
            return y + RowH + Gap;
        }

        private float DrawHourRow(Rect rect, float y, string label, ref int value)
        {
            const float btnW = 30f;
            RUIF.Draw.Label(new Rect(rect.x, y, 130f, RowH), label, RUIF.Tokens.FontLabel,
                RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            var minus = new Rect(rect.x + 140f, y, btnW, RowH);
            if (RUIF.ButtonAtoms.PrimaryButton(minus, "-", true, RUIF.ButtonTone.Default))
                value = WorkContractRules.StepHour(value, -1);
            var plus = new Rect(rect.xMax - btnW, y, btnW, RowH);
            if (RUIF.ButtonAtoms.PrimaryButton(plus, "+", true, RUIF.ButtonTone.Default))
                value = WorkContractRules.StepHour(value, +1);
            var box = new Rect(minus.xMax + Gap, y, plus.x - minus.xMax - Gap * 2f, RowH);
            // 00:00 = 全天（起 == 止）；跨夜（止 < 起）由合同自身的 AllowsHour 判定 —— 与判定口径同源。
            string text = value.ToString("00", CultureInfo.InvariantCulture) + ":00";
            if (value == 0) text += " · " + T("Midnight");
            RUIF.Draw.Label(box, text, RUIF.Tokens.FontLabel, RUIF.Tokens.Muted, TextAnchor.MiddleCenter);
            return y + RowH + Gap;
        }

        private float DrawWageRow(Rect rect, float y)
        {
            const float btnW = 30f;
            RUIF.Draw.Label(new Rect(rect.x, y, 130f, RowH), T("Wage"), RUIF.Tokens.FontLabel,
                RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            var minus = new Rect(rect.x + 140f, y, btnW, RowH);
            if (RUIF.ButtonAtoms.PrimaryButton(minus, "-", wageMinor > WageStepMinor, RUIF.ButtonTone.Default))
                wageMinor = wageMinor - WageStepMinor < WageStepMinor ? WageStepMinor : wageMinor - WageStepMinor;
            var plus = new Rect(rect.xMax - btnW, y, btnW, RowH);
            if (RUIF.ButtonAtoms.PrimaryButton(plus, "+", wageMinor < WageMaximumMinor, RUIF.ButtonTone.Default))
                wageMinor = wageMinor + WageStepMinor > WageMaximumMinor ? WageMaximumMinor : wageMinor + WageStepMinor;
            var box = new Rect(minus.xMax + Gap, y, plus.x - minus.xMax - Gap * 2f, RowH);
            RUIF.Draw.Label(box, Silver(wageMinor), RUIF.Tokens.FontLabel, RUIF.Tokens.Muted, TextAnchor.MiddleCenter);
            return y + RowH + Gap;
        }

        private static string Silver(long minor)
            => (minor / 100.0).ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>稳定 reason ID → 用户可读文本（缺键回落原 ID，可解释；GOV-009）。</summary>
        /// <summary>失败原因 → 可读文本。<b>2026-10-11 实机修复</b>：原因可能是**复合码**（<c>code|细节</c>，
        /// 细节里还会带推荐定价等诊断串）—— 此前把**整串**拼进键 ⇒ 查不到 ⇒ 屏幕直接显示
        /// <c>PC_PEA.Dialog.WorkContract.Reason.NoDemand|…</c>（玩家看到的"乱码"）。
        /// 现在：① 只按 <c>code</c> 段查键；② 细节原样附在后面（不翻译，避免二次拼键）；
        /// ③ **缺键兜底显示 stable code**（而不是裸长键）—— 与 LOC-004「fallback 可解释」一致。</summary>
        private static string ReasonText(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return string.Empty;
            int split = reason.IndexOf('|');
            string code = split < 0 ? reason : reason.Substring(0, split);
            string detail = split < 0 ? string.Empty : reason.Substring(split + 1);
            string key = KeyPrefix + "Reason." + code;
            string text = key.Translate().ToString();
            if (string.IsNullOrEmpty(code)) return detail;
            if (text == key) text = code;                 // 缺键 ⇒ 显示稳定码，绝不把长键名打上屏
            return detail.Length == 0 ? text : text + " · " + detail;
        }

        private static string T(string suffix)
            => (KeyPrefix + suffix).Translate().ToString();
    }
}
