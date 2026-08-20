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
        // v1.1.4 高度加大：hint 可能换行两行（类型名示例文案），WinH 需容纳。
        private const float WinH = 196f;

        private readonly string title;
        private readonly string hint;
        private readonly Action<string> onCommit;
        private string input;

        public override Vector2 InitialSize
        {
            get { return new Vector2(WinW, WinH); }
        }

        /// <param name="titleKey">标题翻译键。</param>
        /// <param name="hintKey">提示翻译键。</param>
        /// <param name="currentName">当前别名（null/空 = 未改名）。</param>
        /// <param name="onCommit">提交回调，收到归一化别名（null = 清除）；确定时调用。</param>
        public Dialog_RenameWorkplace(string titleKey, string hintKey, string currentName, Action<string> onCommit)
        {
            this.title = titleKey.Translate();
            this.hint = hintKey.Translate();
            this.onCommit = onCommit;
            input = string.IsNullOrEmpty(currentName) ? string.Empty : currentName;
            doCloseX = true;
            doCloseButton = false;
            closeOnAccept = true;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            drawShadow = true;
            forcePause = true;
        }

        // v4.17 体检：closeOnAccept=true 但旧实现未提交输入——玩家按回车窗口直接
        // 关闭且输入被静默丢弃。引擎该版本 Window 无 OnAcceptKey 可重写，改为在
        // DoWindowContents 内检测回车键（IMGUI 对话框惯例回车=确定）。
        public override void DoWindowContents(Rect inRect)
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                CommitAndClose();
                Event.current.Use();
                return;
            }

            float y = inRect.y;
            Rect titleRect = new Rect(inRect.x, y, inRect.width, TitleH);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, title);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            y += TitleH;

            Rect fieldRect = new Rect(inRect.x, y, inRect.width, FieldH);
            input = Widgets.TextField(fieldRect, input);
            y += FieldH + 6f;

            // v1.1.4 提示区：Tiny 字体 + 多行自动换行（CalcHeight 动态计算高度），
            // 避免长提示（如类型名示例）被单行高度截断。
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            float hintH = Text.CalcHeight(hint, inRect.width);
            float hintBoxH = Mathf.Max(16f, hintH + 2f);
            Rect hintRect = new Rect(inRect.x, y, inRect.width, hintBoxH);
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
            y += hintRect.height + 8f;

            // 按钮行：确定 / 取消
            float btnW = (inRect.width - ButtonGap) / 2f;
            Rect okRect = new Rect(inRect.x, y, btnW, ButtonH);
            Rect cancelRect = new Rect(inRect.x + btnW + ButtonGap, y, btnW, ButtonH);
            if (Widgets.ButtonText(okRect, "PersonalChronicle.UI.RenameWorkplace.OK".Translate()))
            {
                CommitAndClose();
            }
            if (Widgets.ButtonText(cancelRect, "PersonalChronicle.UI.RenameWorkplace.Cancel".Translate()))
            {
                Close(true);
            }
        }

        private void CommitAndClose()
        {
            try
            {
                // 清空输入 = 清除别名（null）；否则去首尾空白。
                string normalized = string.IsNullOrWhiteSpace(input) ? null : input.Trim();
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
