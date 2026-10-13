using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive.UI
{
    /// <summary>
    /// [ARC-013] <see cref="UIComponents"/> 的「侧边栏导航」切片（自动化车间管理面板引入）。
    ///
    /// 原 UIComponents.cs 承载基础组件（Label / Panel / Card / StatCell / …），
    /// 本文件剥离出「左侧分类导航」这一组职责，遵循 [ARC-013] 单一职责与
    /// 「单文件 ≤ 800 行」。零契约改动，仅新增组件。
    ///
    /// 本切片的组件<b>纯展示 + 命中测试</b>：不持有状态、不读游戏数据，
    /// 选中态与文案全部由调用方注入。
    /// </summary>
    internal static partial class UIComponents
    {
        /// <summary>侧边栏条目左侧的选中色条宽度。</summary>
        private const float SidebarStripeW = 3f;
        /// <summary>色条之后留给文字的缩进。</summary>
        private const float SidebarTextPad = 10f;

        /// <summary>
        /// 侧边栏导航项：填充底 + 选中左侧色条 + 单行文字 + 整行隐形按钮。
        /// </summary>
        /// <param name="rect">条目矩形（宽度由调用方按面板布局给）。</param>
        /// <param name="label">已翻译好的显示名（过长会自动截断加 …）。</param>
        /// <param name="selected">是否为当前选中项（决定底色与文字亮度）。</param>
        /// <returns>本帧是否被点击。</returns>
        internal static bool SidebarItem(Rect rect, string label, bool selected)
        {
            Widgets.DrawBoxSolid(rect, selected ? UITheme.CardHover : UITheme.Panel);
            // 审计修复（2026-09-30）：补**悬停反馈**（此前只有选中/未选中两态，鼠标经过无变化
            // ⇒ 玩家无法确认"这一行可点"）。选中态不变，未选中 + 悬停时提亮。
            if (!selected && Mouse.IsOver(rect))
            {
                Widgets.DrawHighlightIfMouseover(rect);
            }
            if (selected)
            {
                Widgets.DrawBoxSolid(
                    new Rect(rect.x, rect.y, SidebarStripeW, rect.height), UITheme.Accent);
            }
            Border(rect, selected ? UITheme.Border : UITheme.BorderSoft);

            float textX = rect.x + SidebarTextPad;
            Rect textRect = new Rect(textX, rect.y, rect.width - SidebarTextPad - 4f, rect.height);
            Label(textRect,
                TruncateToWidth(label ?? "", textRect.width, UITheme.FontBody),
                UITheme.FontBody,
                selected ? UITheme.Text : UITheme.Muted,
                TextAnchor.MiddleLeft);

            return Widgets.ButtonInvisible(rect);
        }

        /// <summary>
        /// 主题化按钮（面板底部操作条用）。原版 <c>Widgets.ButtonText</c> 是浅灰底，
        /// 在深色面板上突兀；这里用 UITheme 令牌自绘，配色随皮肤走。
        /// </summary>
        /// <returns>本帧是否被点击。</returns>
        /// <param name="enabled">false = 禁用态（降为 Dim 文字 + 不响应点击）。</param>
        internal static bool ThemedButton(Rect rect, string label, bool enabled = true)
        {
            Widgets.DrawBoxSolid(rect, UITheme.PanelRaised);
            Border(rect, UITheme.BorderSoft);
            // 审计修复（2026-09-30）：补悬停反馈 + 禁用态（此前两态皆无）。
            if (enabled)
            {
                Widgets.DrawHighlightIfMouseover(rect);
            }
            Label(rect, TruncateToWidth(label ?? "", rect.width, UITheme.FontBody),
                UITheme.FontBody, enabled ? UITheme.Text : UITheme.Dim, TextAnchor.MiddleCenter);
            return enabled && Widgets.ButtonInvisible(rect);
        }

        /// <summary>原版勾选框的边长（与 <c>Widgets.CheckboxLabeled</c> 内部常量一致）。</summary>
        private const float CheckboxBox = 24f;

        /// <summary>
        /// 勾选框（CHG-20261004-WORKTAB-SHIFT · 单元 B）。
        ///
        /// <para>为什么不在<see cref="ThemedButton"/> 上做「按下/抬起」态：语义不同。
        /// 按钮是<b>动作</b>（点了就换文案），勾选框是<b>状态</b>（勾上/取消同一件事）——
        /// 「按班次取消、勾选」要的正是后者的恒定控件 + 可见勾。</para>
        ///
        /// <para>实现选择：<b>不自绘</b>勾选图，而是走原版 <c>Widgets.Checkbox</c>。
        /// 它返回 <c>void</c> 且通过 <c>ref</c> 回写 ⇒ 必须读回局部变量来判定本帧是否翻转；
        /// 它同时自带 <c>Checkbox_TurnedOn/Off</c> 音效（自绘会丢这个反馈）。</para>
        ///
        /// <para>零尺寸早退（UI-006）：面板被压缩到行宽不足时直接返回，不注册控件。</para>
        /// </summary>
        /// <param name="rect">分配给勾选框的矩形（勾选图会在其中水平垂直居中）。</param>
        /// <param name="value">当前勾选态（值语义，不会被本方法修改）。</param>
        /// <returns>本帧是否发生了翻转。</returns>
        internal static bool ThemedCheckbox(Rect rect, bool value)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return false;
            }

            float cx = rect.x + (rect.width - CheckboxBox) * 0.5f;
            float cy = rect.y + (rect.height - CheckboxBox) * 0.5f;

            bool flipped = value;
            // 8 个参数显式传全：不依赖 C# 默认值（元数据里默认值不可见，猜错就是运行时行为漂移）。
            Widgets.Checkbox(cx, cy, ref flipped, CheckboxBox, false, true, null, null);
            return flipped != value;
        }
    }
}
