using System;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// v1.1.4 建筑/房间类型改名对话框（自制轻量 Window）。
    ///
    /// 引擎核验（反射 2026-08-14）：原版 <c>RimWorld.RenameUIUtility.DrawRenameButton</c>
    /// 依赖 <c>Verse.IRenameable</c>（RenamableLabel get/set + BaseLabel + InspectLabel），
    /// 但普通 Building_WorkTable 不实现 IRenameable；且 <c>Dialog_Rename`1</c> 泛型构造
    /// 可见性未核验。故不依赖原版改名 UI，自建对话框，由 <see cref="onCommit"/> 回调
    /// 决定写入哪张全局别名表（经 <see cref="IArchiveService"/>，UI 不直写存储层）。
    ///
    /// 行为：玩家输入别名 → 确定触发 onCommit(归一化名)；清空输入 → onCommit(null) 清除别名
    /// （展示层回落 DefDatabase.LabelCap）。
    /// </summary>
    public class Dialog_RenameWorkplace : Window
    {
        /// <summary>对话框内边距（对齐 UITheme.PanelPadding）。</summary>
        private const float TitleH = 28f;
        private const float FieldH = 30f;
        private const float ButtonH = 34f;
        private const float ButtonGap = 10f;
        private const float WinW = 420f;
        // v4.17 体检：窗口高度不再固定——按 hint 实际换行高度动态计算（见 winHeight）。

        private readonly string title;
        private readonly string hint;
        private readonly Action<string> onCommit;
        /// <summary>
        /// CHG-20261005-DEVRENAME：提交前校验钩子（<b>可拒绝</b>）。null = 不校验
        /// （既有三个调用点行为逐字不变）。拒绝时<b>不关窗、不提交</b>，并弹出 reason。
        /// </summary>
        private readonly Func<string, AcceptanceReport> validate;
        private string input;
        /// <summary>v4.17 体检：窗口高度按 hint 实际换行高度动态计算（旧固定 WinH=196，
        /// 长提示 3+ 行时按钮行被窗口底边裁掉）。</summary>
        private float winHeight;

        public override Vector2 InitialSize
        {
            get { return new Vector2(WinW, winHeight); }
        }

        /// <param name="titleKey">标题翻译键。</param>
        /// <param name="hintKey">提示翻译键。</param>
        /// <param name="currentName">当前别名（null/空 = 未改名）。</param>
        /// <param name="onCommit">提交回调，收到归一化别名（null = 清除）；确定时调用。</param>
        public Dialog_RenameWorkplace(string titleKey, string hintKey, string currentName, Action<string> onCommit)
            : this(titleKey, hintKey, currentName, onCommit, null, null, false)
        {
        }

        /// <summary>
        /// CHG-20261005-DEVRENAME <b>加性重载</b>：补「提交前校验」（可拒绝、不关窗）与「提示参数通道」。
        ///
        /// <para><b>为什么必须加参数通道</b>（Gate-LOC P1-6）：原构造器用 <c>hintKey.Translate()</c>
        /// —— <b>无参</b>，故形如「当前名：{0}」的提示里 <c>{0}</c> 永远注入不进去，会原样显示成
        /// <c>{0}</c>。此重载允许随附一个参数。</para>
        ///
        /// <para><b>为什么必须能拒绝</b>（Gate-UI P2-6 / REQ-WORKSHOP-004）：本对话框原先
        /// 「确定即关窗」，若重名/超长只弹消息仍关窗，玩家刚输入的名字会被静默丢弃。</para>
        /// </summary>
        public Dialog_RenameWorkplace(string titleKey, string hintKey, string currentName, Action<string> onCommit,
            Func<string, AcceptanceReport> validate, string hintArg)
            : this(titleKey, hintKey, currentName, onCommit, validate, hintArg, true)
        {
        }

        /// <summary>实际实现（两个公开构造器链到此处，避免复制下面那段按 hint 实测高度算窗高的逻辑）。</summary>
        /// <remarks>
        /// ⚠ <paramref name="hintArg"/> 的类型**必须是 <c>string</c>**：`Translator.Translate(string, string)`
        /// 是非过时重载，而 `Translate(string, params object[])` 已标 `[Obsolete]` ——
        /// 形参写成 <c>object</c> 会强制绑定到后者，触发 CS0618（本工程要求 0 警告）。
        /// </remarks>
        private Dialog_RenameWorkplace(string titleKey, string hintKey, string currentName, Action<string> onCommit,
            Func<string, AcceptanceReport> validate, string hintArg, bool isResolvedOverload)
        {
            this.title = titleKey.Translate();
            this.hint = hintArg == null ? hintKey.Translate() : hintKey.Translate(hintArg);
            this.onCommit = onCommit;
            this.validate = validate;
            input = string.IsNullOrEmpty(currentName) ? string.Empty : currentName;
            doCloseX = true;
            doCloseButton = false;
            closeOnAccept = true;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            drawShadow = true;
            forcePause = true;
            // 与 DoWindowContents 布局同口径（hint 宽度 = 内容区可用宽）。
            // v6 修复：hint 宽度须与 DoWindowContents 中实际可用宽一致，否则构造器算出的 hintH
            // 偏小会让按钮行被窗口底边裁切。
            // CHG-20261005-DEVRENAME-RUIF：RUIF ≥1.3 时几何取 RUIF 窗口骨架（WindowFmt），
            // 否则用本工程既有几何 —— 两条路径各算各的宽度，不可混用。
            RuifBridge.ChromeMetric chrome = RuifBridge.ChromeMetrics();
            float innerW = chrome.Ready
                ? WinW - 2f * chrome.ContentInset
                : WinW - 2f * UITheme.PanelPadding;
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            float hintH = Mathf.Max(16f, Text.CalcHeight(this.hint, innerW) + 2f);
            Text.Font = prevFont;
            // 底部额外余量 16f：防止 hint 在不同 DPI/字体渲染下微差导致按钮溢出。
            winHeight = chrome.Ready
                ? chrome.TitleHeight + 8f + FieldH + 6f + hintH + 8f + chrome.FooterHeight + 10f + 16f
                : TitleH + FieldH + 6f + hintH + 8f + ButtonH + 10f + 16f;
        }

        // v4.17 体检：closeOnAccept=true 但旧实现未提交输入——玩家按回车窗口直接
        // 关闭且输入被静默丢弃。引擎该版本 Window 无 OnAcceptKey 可重写，改为在
        // DoWindowContents 内检测回车键（IMGUI 对话框惯例回车=确定）。
        public override void DoWindowContents(Rect inRect)
        {
            // 原生 Window 默认浅色背景——覆盖为 UITheme.Window 深色。
            // ⚠ RUIF 的 `WindowFrame` **不画窗口底/外框**（框架红线：背景归宿主 Window）⇒ 两条路径都要铺底。
            UIComponents.TintedBox(inRect, UITheme.Window);

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                CommitAndClose();
                Event.current.Use();
                return;
            }

            // CHG-20261005-DEVRENAME-RUIF：优先用 RUIF 窗口骨架（标题条 + 两条分隔线 + 交出内容区/底栏矩形）。
            // RUIF 缺失 / <1.3 / 抛异常 ⇒ ruifFrame=false，走下面的既有排版（B1 回落，观感与改造前一致）。
            Rect contentRect;
            Rect footerRect;
            bool ruifFrame = RuifBridge.TryWindowFrame(inRect, title, 2, out contentRect, out footerRect);
            if (!ruifFrame)
            {
                contentRect = new Rect(inRect.x + UITheme.PanelPadding, inRect.y + TitleH + 4f,
                    inRect.width - 2f * UITheme.PanelPadding, inRect.height - TitleH - 4f);
                footerRect = new Rect(inRect.x, inRect.yMax - ButtonH - 6f, inRect.width, ButtonH);
                // 回落路径自绘标题（RUIF 路径的标题已由 WindowFrame 画过，不要重复画）。
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), title);
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }

            float y = contentRect.y;
            Rect fieldRect = new Rect(contentRect.x, y, contentRect.width, FieldH);
            input = Widgets.TextField(fieldRect, input);
            y += FieldH + 6f;

            // 提示区：Tiny + 多行自动换行。⚠ RUIF **没有**多行高度助手（Measure 只有单行 Width/Clamp/Fits）
            // ⇒ 高度仍用原版 `Text.CalcHeight`，这不是"没上 RUIF"，是框架确实没有该件。
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            float hintH = Text.CalcHeight(hint, contentRect.width);
            float hintBoxH = Mathf.Max(16f, hintH + 2f);
            Rect hintRect = new Rect(contentRect.x, y, contentRect.width, hintBoxH);
            Color prevHintColor = GUI.color;
            try
            {
                GUI.color = UITheme.Muted;
                Widgets.Label(hintRect, hint);
            }
            finally
            {
                GUI.color = prevHintColor;
            }
            Text.Font = GameFont.Small;

            // 按钮行：取消（左） / 确定（右，主按钮 Accent）。RUIF `WindowFmt` 约定**主按钮在末位（最右）**。
            string okLabel = "PersonalChronicle.UI.RenameWorkplace.OK".Translate();
            string cancelLabel = "PersonalChronicle.UI.RenameWorkplace.Cancel".Translate();
            Rect okRect;
            Rect cancelRect;
            if (!ruifFrame
                || !RuifBridge.TryFooterButtonRect(footerRect, 2, 1, out okRect)
                || !RuifBridge.TryFooterButtonRect(footerRect, 2, 0, out cancelRect))
            {
                float btnW = (footerRect.width - ButtonGap) / 2f;
                cancelRect = new Rect(footerRect.x, footerRect.y, btnW, ButtonH);
                okRect = new Rect(footerRect.x + btnW + ButtonGap, footerRect.y, btnW, ButtonH);
            }

            bool okHandled;
            bool okClicked = RuifBridge.TryButton(okRect, okLabel, 1, true, out okHandled);
            if (!okHandled)
            {
                okClicked = Widgets.ButtonText(okRect, okLabel);   // B1 回落
            }
            bool cancelHandled;
            bool cancelClicked = RuifBridge.TryButton(cancelRect, cancelLabel, 0, true, out cancelHandled);
            if (!cancelHandled)
            {
                cancelClicked = Widgets.ButtonText(cancelRect, cancelLabel);   // B1 回落
            }
            if (okClicked)
            {
                CommitAndClose();
            }
            else if (cancelClicked)
            {
                Close(true);
            }
        }

        private void CommitAndClose()
        {
            // 清空输入 = 清除别名（null）；否则去首尾空白。
            string normalized = string.IsNullOrWhiteSpace(input) ? null : input.Trim();
            // CHG-20261005-DEVRENAME：校验钩子（可拒绝）。拒绝时**不关窗、不提交**并保留输入。
            if (validate != null)
            {
                AcceptanceReport report = validate(normalized);
                if (!report.Accepted)
                {
                    Messages.Message(report.Reason, RimWorld.MessageTypeDefOf.RejectInput, false);
                    return;
                }
            }
            try
            {
                if (onCommit != null)
                {
                    onCommit(normalized);
                }
            }
            finally
            {
                Close(true);
            }
        }
    }
}
