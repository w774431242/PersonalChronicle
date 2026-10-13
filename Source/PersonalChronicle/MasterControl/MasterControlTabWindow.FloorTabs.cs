using UnityEngine;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// Partial of <see cref="MasterControlTabWindow"/> —— ② 俯视面板的 **Tab 语义常量** 与底部斜纹条。
    ///
    /// <para>⚠ 2026-10-06（用户指令「移除底栏 Tab 栏整行」）：本文件原含三块内容，其唯一消费方是已删除的
    /// <c>AppShell.DrawAppTabBar</c>，故整块移除：
    /// ① <c>FloorTab</c> 结构 + <c>FloorTabs[]</c> 页签表（Keyed 标签 + 各面板模块自述的 <c>PanelCount</c> 计数）；
    /// ② <c>DrawFloorTabButton</c>（单页签按钮：选中态 / 悬停 / 点击切页签）；
    /// ③ <c>DegradedVisibleTabCount</c>（降级路径不提供第 7 页签「编排」的能力裁剪数）。</para>
    ///
    /// <para>下面是**保留**的 tab 语义常量：内容区分发（<c>AppShell.DrawActivePane</c>）、
    /// 计高（<c>Detail.TabPaneContentHeight</c>）、降级路径钳位（<c>Detail</c> 的
    /// <c>selectedFloorTab &gt; TabData</c>）仍按它们工作 —— 页签切换入口移除后
    /// <c>selectedFloorTab</c> 恒为 <see cref="TabCockpit"/>（总控），其余分支暂不可达。</para>
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        /// <summary>Tab 语义（写代码时用名字，不用魔数）。</summary>
        private const int TabCockpit = 0;
        private const int TabDispatch = 1;
        private const int TabAnalysis = 2;
        private const int TabOps = 3;
        private const int TabEvent = 4;
        private const int TabData = 5;

        /// <summary>第 7 页签「编排」（新版预览新增；编辑器本体 = B4）。</summary>
        private const int TabLayout = 6;

        /// <summary>面板底部警示斜纹条高（预览 <c>.hazard{height:8px}</c>，实测渲染高 8px ⇒ 取 8，1:1 对齐）。</summary>
        internal const float HazardStripHeight = 8f;

        /// <summary>
        /// 面板底部警示斜纹条（预览 <c>.hazard</c>）。
        /// 层级顺序为 <c>[平面图] → [tab 内容] → [.hazard]</c>（tab 栏一行已于 2026-10-06 移除）。
        /// </summary>
        private static void DrawFloorHazardStrip(Rect rect)
        {
            PersonalChronicle.Manufacturing.UI.WorkshopFloorPlan.DrawHazardStrip(rect);
        }
    }
}
