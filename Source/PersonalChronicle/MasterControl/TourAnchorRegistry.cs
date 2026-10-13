using System;
using UnityEngine;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// **引导锚点注册表**（CHG-20261006-TOUR2）—— 跨窗口的"最近上报"表。
    ///
    /// <para><b>为什么必须是静态</b>：引导层是**独立全屏窗口**
    /// （<see cref="Window_TourOverlay"/>），锚点由**别的窗口**（总控中心）在自己绘制时上报。
    /// 两个窗口实例之间没有引用关系，只能靠一张共享表。这是**受控例外**（GATE-UI 的
    /// "零静态可变状态"）：<list type="bullet">
    /// <item>生命周期由引导层独占管理：<see cref="Begin"/>（启动）/ <see cref="End"/>（退出引导）；</item>
    /// <item>非激活态下 <see cref="Report"/> 是**一次 bool 判断**的 no-op；</item>
    /// <item>条目**带时间戳**，读取方按"新鲜度"判定（见下），不入存档、不缓存控件引用。</item>
    /// </list></para>
    ///
    /// <para><b>⚠ 为什么不能"每帧清空标记"</b>（首版 BLOCKED 的 P0）：引导层与总控中心是
    /// **同帧不同窗口**，绘制先后由窗口栈序决定且可能反转（玩家中途重开总控中心时会把它压到栈顶）。
    /// 「引导层帧初清空 → 帧中读取」在同一趟里必然读到空表 ⇒ 挖洞恒失效。
    /// 现在改为：上报只写"时间戳"，读取只要求**最近 <see cref="FreshWindowMs"/> ms 内被上报过**
    /// —— 与绘制顺序完全无关；控件消失（切页/关窗）后锚点自然过期，自动降级为居中气泡。
    /// 时间源用 BCL 时钟（绘制路径禁用 <c>Time.realtimeSinceStartup</c>）。</para>
    ///
    /// <para><b>坐标系</b>：一律**屏幕坐标**（`UI` 空间）。上报方负责换算：
    /// 窗口内控件 = <c>windowRect.x + local.x</c>。</para>
    /// </summary>
    internal static class TourAnchorRegistry
    {
        internal const int AnchorHeaderGuide = 0;   // 页头「新手教程」按钮
        internal const int AnchorThemeSwitch = 1;   // 页头主题控件
        internal const int AnchorContentArea = 2;   // 注册表内容区
        internal const int Count = 3;

        /// <summary>锚点新鲜度窗口（ms）：最近这段时间内被上报过才算可用。</summary>
        private const long FreshWindowMs = 250L;

        private static readonly Rect[] rects = new Rect[Count];
        private static readonly long[] stamps = new long[Count];
        private static bool active;

        /// <summary>引导是否激活（上报方用它做零开销短路）。</summary>
        internal static bool Active
        {
            get { return active; }
        }

        /// <summary>启动引导：置激活并清表（旧时间戳归零 ⇒ 不会误用上一局锚点）。</summary>
        internal static void Begin()
        {
            active = true;
            for (int i = 0; i < Count; i++)
            {
                stamps[i] = 0L;
            }
        }

        /// <summary>结束引导：清激活与时间戳（成对收口）。</summary>
        internal static void End()
        {
            active = false;
            for (int i = 0; i < Count; i++)
            {
                stamps[i] = 0L;
            }
        }

        /// <summary>上报一个锚点（屏幕坐标）。非激活态是 no-op。</summary>
        internal static void Report(int id, Rect screenRect)
        {
            if (!active || id < 0 || id >= Count)
            {
                return;
            }
            rects[id] = screenRect;
            stamps[id] = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// 取**最近上报过**的锚点；过期或尺寸过小 ⇒ false（引导层据此降级为居中气泡）。
        /// 不区分"哪一帧"：只要在新鲜度窗口内，与绘制顺序无关。
        /// </summary>
        internal static bool TryGet(int id, out Rect rect)
        {
            rect = default(Rect);
            if (!active || id < 0 || id >= Count || stamps[id] <= 0L)
            {
                return false;
            }
            long now = DateTime.UtcNow.Ticks;
            long age = now - stamps[id];
            if (age < 0L || age > TimeSpan.TicksPerMillisecond * FreshWindowMs)
            {
                return false;   // 控件已消失（切页 / 关窗）或时钟异常 ⇒ 当作不可用
            }
            Rect r = rects[id];
            if (r.width < 4f || r.height < 4f)
            {
                return false;
            }
            rect = r;
            return true;
        }
    }
}
