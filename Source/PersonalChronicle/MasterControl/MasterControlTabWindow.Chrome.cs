using PersonalChronicle.MasterControl.Population;
using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// Partial of <see cref="MasterControlTabWindow"/> ——**页面外壳**（对齐预览 `#top` 顶栏 + `#ticker` 底栏）。
    ///
    /// <para>预览整页 = 顶栏（品牌 / 游戏时钟 / 电网负载）+ ①~⑦ 分区 + 底栏（工厂广播跑马灯）。
    /// 只落分区而缺顶栏/底栏，就是「整体布局与预览对不上」的根因，因此这里把两者补齐：</para>
    /// <list type="bullet">
    /// <item><b>顶栏</b>：画在中心详情页内容顶部（品牌 + `Y{n} · D{n}` + 时钟 + 电网读数 + 储能水位条），
    /// 与预览 `.ptitle` 之外的 `#top` 同构；</item>
    /// <item><b>底栏</b>：画在窗口最底部通栏（`工厂广播` 标签 + 一行真实厂况播报），
    /// 对应预览 <c>#ticker{position:fixed;bottom:0;height:30px}</c>。</item>
    /// </list>
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        /// <summary>顶栏高（品牌/时钟/电网同一行）。</summary>
        // 预览 #top 最终值（HTML 590，覆盖 31/555 两次声明）：
        //   `#top{padding:7px 15px;gap:13px}` + `#top .grp button{padding:3px 9px;font-size:11px;line-height:1.25}`
        //   ⇒ 上下各 7 + 内容行高 ≈ 24 ⇒ 条高 38（旧值 44 基于已被覆盖的 `padding:10px 20px`）
        private const float ChromeBarHeight = 38f;

        /// <summary>顶栏左右内边距（预览 `#top{padding:7px 15px}`）。</summary>
        private const float ChromePadX = 15f;

        /// <summary>底栏高（预览 <c>#ticker{height:26px}</c>；HTML 575 覆盖了首版的 30px）。</summary>
        private const float TickerBarHeight = 26f;

        /// <summary>
        /// 构建标记：顶栏右下角显示 + 开窗写日志。
        ///
        /// <para><b>为什么必须有</b>：RimWorld 只在**启动时**加载程序集 —— dll 换掉后未重启的会话
        /// 仍跑旧代码，表现为"改了却没有任何变动"（本次实机就踩了这个坑：会话跨过了两次同步）。
        /// 有标记即可一眼确认在跑哪一版。**每次改这批 UI 都要递增它。**</para>
        /// </summary>
        internal const string BuildStamp =
            // 0913k = 前端 UI 美化重构·**切片一**（本轮）：① 新增 `UIComponents.ThemeSwitch`
            //         （**总控中心与档案馆唯一实现**）：分段控件（标签段 + 1px 分隔 + 值段）+
            //         **按可用宽驱动 D1/D2/D6 降级**（只值段 → 只图标 → 不画但保留悬停）+ 五态
            //         （默认/悬停/按下/值段高亮/禁用；**键盘焦点态 = B 类降级登记**）+ 文案按
            //         `ActiveThemeId` 失效的**有界缓存**（消除两窗口每帧 `Translate` + 拼接，PERF-001）；
            //         ⛔ 修掉「固定最小宽 120 + 右锚」造成的**文字被硬切**（实机嵌入档 `题 · 暗黑哥特`）；
            //         ② 总控页头主题控件分段化（旧实现把「标签 · 主题名」拼成**单串** ⇒ 被读成"主题暗黑哥特"一个词）；
            //         ③ 页头说明行改**提示条容器**（复用既有 `DrawNoticeBox`）+ 新增 `PageHeaderBlockHeight()`
            //         让「绘制 / 中心详情页 / 注册表页」三处高度**同源**（LAND-002）；
            //         ④ `v-` 瑕疵：Def 未登记版本（哨兵 `-`）时不再拼版本段（新增 Keyed `Detail.ApiNoVersion`）；
            //         ⑤ 承载区标题行改**工具条**（3px 竖标 + Tiny/Muted）：消除与嵌入页大标题同字重复，
            //         右侧改报嵌入档位（新增 Keyed `Archive.Tier.Full/Compact`），并停止绘制与之重复的 `SubtitleText`。
            // 0913j = 殖民地档案馆主界面**植入人员系统中心**：新增契约 `IArchiveViewDataProvider`（Application）
            //         + 注册表 `Register/GetArchiveView` + 组合根绑定 `population_center`（Archive→Application 同向；
            //         **零 Harmony、零中心名特判**，唯一硬编码是 `MainButtonDefName` 稳定 ID 且已按 GOV-006 登记）；
            //         快照 `SystemCenterDetailSnapshot.ArchiveView`（**非 null 即渲染**，LAND-010）；
            //         承载实现 `Archive/UI/PersonnelArchiveHost`：复用原版标签页**同一实例**（`MainButtonDef.TabWindow`
            //         惰性解析，不新建/不自建静态单例），在 `GUI.BeginGroup` 内驱动其 `DoWindowContents`，
            //         try/finally 配对 + 异常隔离 —— 反编译取证支撑：`Verse.Window` 无 `def` 字段、
            //         `MainTabWindow` 唯一字段是 `MainButtonDef def`、`PersonnelArchivePanel` 不读 `windowRect/def/WindowStack`；
            //         三档空间预算（Full / Compact 折叠侧栏→顶部导航条 / TooNarrow 只提示不画）走纯函数
            //         `ArchiveViewLayout.Resolve`；家族树连线改「矩阵+裁剪安全」的小方块串联（两点版 `Widgets.DrawLine`
            //         走 GL，不认 `GUI.matrix`）；`BuiltinPopulationProvider`（4 张档案规模 KPI）注册后
            //         `ResolveWiring` 自然把该中心判为「运行中」；新增 Keyed 10 键 × 中英 + 11 条离线单测（305→316）。
            // 0913i = 系统中心「接入状态」三态：新增 `SystemCenterWiring{Running, NotWired, Reserved}`，
            //         由**运行期事实**推导（Def 的 reserved + `SystemCenterDataRegistry` 里有没有该中心的 Provider），
            //         **不写进 Def 手填**（手填迟早漂移）；文案/配色各留唯一映射点（`StatusKeyOf` / `StatusColor`：
            //         运行中=绿 · **数据未接入=琥珀** · 预留=灰）；未接入/预留一律**不取 KPI**（LAND-009）；
            //         `population_center` 显示名改「**人员系统中心** / Personnel Center」且 `reserved: true→false`
            //         （模块=殖民地档案馆已存在，缺的只是"经 Provider 接入中心框架" ⇒ 显示"数据未接入"才如实）；
            //         启动期补「reserved=true 却注册了 Provider」的 Warning（真不一致，Provider 成死代码）；
            //         新增 Keyed `…MasterControl.Status.NotWired`（中英各 1 条）。顺带修掉 `policy_center` 的**假绿**
            //         （它 `reserved=false` 且无 Provider，旧两态逻辑必显示「运行中」）。
            // 0913h = 行序重排：实机"没有变化"的**真正原因不是同步**（Player.log 显示确实在跑
            //         0913g、发布区哈希也一致）—— 而是**解锁阈值太高**：人员/技能/生产分别在第 5/6/7 行，
            //         默认缩放只有 2~3 行可见 ⇒ 必须放大到 2.0×/2.3×/2.5× 才见。
            //         现把「人员」（原版指定 + 当班）提到**行 2**、工单·功率→行 3、状态→行 4、作业者→行 5
            //         ⇒ 默认缩放下第 2 行就能看到"谁来做这台"；技能/生产仍按尺寸递进（行 6/7）。
            // 0913g = 实机两问题修复：
            //         ① **原版指定生产人员不显示** —— 卡只读了 PC 侧排班，漏了原版 `Bill.PawnRestriction`
            //            （工单里的"工人限制"）；现读入 `WorkshopDeviceReading.AssignedText`，
            //            行 5 由「岗位」改为「**人员**」= `指定 王 · 岗位 张/李`（两路合并，都没有才画 —）。
            //         ② **清空画布后拖放设备看不到（只能撤销）** —— 设备卡循环以「车间已在画布上」为前提，
            //            `ClearAll` 把车间全收回托盘 ⇒ 独立设备整个消失；而 `DrawDetachedLayer`
            //            早已是**死代码**（注释说"已并入上面的循环"）。修法：循环内跳过独立设备、
            //            恢复独立层单独绘制（不依赖车间是否已放置），`ClearAll` 连独立设备一起清
            //            （`Snapshot/UndoLast` 增独立设备快照），`TryGetDetachedPos` 随之删除。
            // 0913f = 设备卡分档信息密度：行池扩到 7 行，行数由卡片可用高度反推
            //         ⇒ 卡越大显示越详细：① 名称（最小档只有它）② 工单·功率 ③ 状态 ④ 作业者
            //         ⑤ **岗位**（该车间当班人员，`MapComponent_WorkshopManager.DutyZoneFor` 反向索引）
            //         ⑥ **技能**（当前工单 `RecipeDef.workSkill` → 车间类型主技能 → Crafting，
            //            等级走 `WorkshopDutyScoring.ScoreOf`，与上岗评分同口径）
            //         ⑦ **生产**（当前工单配方 + 目标数）；`MaxDeviceCardScale` 1.6 → **2.5**
            //         （七行全解锁 = 260×150，填充 ≈94%）；新增 1 个 Keyed（`Layout.Card.Post`）。
            // 0913e = 画布裁剪 + 截断算法：① `UIComponents.TruncateToWidth` 的候选循环**从最短起步**
            //         ⇒ 只要省略号放得下就 return "…"，**凡是截断就只剩三个点**（设备卡名称/车间角标全中，
            //         用户报「设备信息丢失」）⇒ 改为**从长到短二分**；② 主画布与独立设备层补
            //         `GUI.BeginClip` 视口裁剪（IMGUI 无自动裁剪 ⇒ 卡片被平移到画布外就会画到顶栏/
            //         `#cmdbar` 上，用户报「缩小过大时设备卡跑出画布」），悬停/点击在裁剪外用绝对矩形做；
            //         ③ 工序链路改 `DrawPolyline`（两点版 `Widgets.DrawLine` 走 GL，不感知 `GUI.matrix`，
            //         clip 内必错位）；④ 卡片底色改**实色**（半透明会被上层文字穿透）；⑤ 极小卡不再整体消失
            //         （下限 26→10px，走"只留几何与灯色"）。
            // 0913d = 画布设备卡：① 卡尺寸回归预览真源 `104×60`（原自造 `124×36` 扁条装不下内容）；
            //         ② **框乘 k、字不乘 k** 的老毛病修掉 —— 内距/灯位/进度条按设计单位 ×k，
            //            文字按屏幕真实行高堆叠 + 行数容量反推（放不下不画，信息进悬停）；
            //         ③ 卡片**屏幕尺寸封顶 1.6×**（超过不再变大 ⇒ 高倍下不再满卡空白）；
            //         ④ 参数补齐（工单 · 功率 / 状态 / 作业者 / 进度条，全部来自原版真实数据）；
            //         ⑤ 读数与俯视图**共用** `Domain/WorkshopDeviceReader`（消灭两处实现）；
            //         ⑥ 网格间距 120 / 72、悬停描边、顶部高光条；独立设备层与车间内卡片尺寸规则统一。
            // 0913b = 表盘再修：① RUIF 抬到 **v1.1.2** —— 增加 15px 标题带（弧顶不得进入 ⇒ 修
            //         「标题被遮挡」）、刻度 12.5% → **5% 一格（21 位）**、标签按可用宽截断、
            //         Gauge 缓存键越界（阈值 6085 装不进 11 位槽）改用 `TexCache.KeyData`；
            //         ② `RuifBridge.GaugeFixedVersion` = **1.1.2**；③ `DrawKpiDial` 按同几何对齐
            // 0913a = 表盘修复：① `RuifBridge.GaugeFixedVersion` 抬到 RUIF v1.1.1（v1.1.0 的贴图烘焙
            //         y 轴镜像 ⇒ 表盘倒置）；② `DrawKpiDial` 补主刻度 25% / 次刻度 12.5%（两轮实机缺陷各修一处）
            // 0912x = RUIF v1.0.2 修复 `Draw.Rotate` 的 pivot 语义（局部空间后乘矩阵）⇒ PC 按**版本闸门**
            //         接回 RUIF 原生表盘，自绘表盘降为回落
            "0913k";

        /// <summary>当前中心详情页是否带外壳（有 Chrome 快照才画顶栏/底栏）。</summary>
        private ManufacturingChromeSnapshot CurrentChrome
        {
            get { return cachedDetail != null ? cachedDetail.Chrome : null; }
        }

        /// <summary>开窗写一条带构建标记的日志（每次开窗一行，便于实机核对版本）。</summary>
        public override void PostOpen()
        {
            base.PostOpen();
            ChronicleLog.Info(ChronicleLog.Category.Ui,
                "MasterControl window opened · build " + BuildStamp);
        }

        /// <summary>底栏占用的高度（无外壳时为 0，内容区不吃亏）。</summary>
        private float TickerHeightForCurrentView
        {
            get
            {
                ManufacturingChromeSnapshot chrome = CurrentChrome;
                return view == ViewKind.CenterDetail && chrome != null && !chrome.IsEmpty
                    ? TickerBarHeight + 4f
                    : 0f;
            }
        }

        /// <summary>
        /// 顶栏（预览 `#top`）：品牌 + 游戏日期时刻 + 电网负载读数 + 储能水位条。
        /// 返回新的 y。
        /// </summary>
        private float DrawChromeBar(Rect rect, float y, ManufacturingChromeSnapshot chrome)
        {
            Rect bar = new Rect(rect.x, y, rect.width, ChromeBarHeight);
            // 与底栏同构的朴素面板（此前用 UIComponents.Card 会画成一条高亮色带，观感像"被选中"）
            UIComponents.Panel(bar);
            UIComponents.Rule(new Rect(bar.x, bar.yMax - 1f, bar.width, 1f), UITheme.BorderSoft);
            float padX = ChromePadX;   // 预览 #top{padding:7px 15px}
            float x = bar.x + padX;
            float lineH = ChromeBarHeight - 14f;   // 内容行高（上下各 7）
            float lineY = bar.y + 7f;

            // 品牌（预览 #top .brand b：16px 琥珀加字距；`.brand .en{display:none}` ⇒ 无英文副标）
            string brand = string.IsNullOrEmpty(chrome.Brand)
                ? Translate("PersonalChronicle.UI.Chrome.Brand")
                : chrome.Brand;
            float brandWidth = Mathf.Min(MeasureWidth(brand, GameFont.Small) + 4f, rect.width * 0.3f);
            UIComponents.Label(new Rect(x, lineY, brandWidth, lineH),
                UIComponents.TruncateToWidth(brand, brandWidth, GameFont.Small),
                GameFont.Small, UITheme.Accent);
            x += brandWidth + 13f;   // 预览 #top{gap:13px}

            // 游戏时钟（预览 #clockbox：日期 dim / 时刻 amber2 加粗）
            float clockWidth = Mathf.Min(150f, rect.width * 0.2f);
            UIComponents.Label(new Rect(x, lineY, clockWidth * 0.55f, lineH),
                chrome.DateText, GameFont.Tiny, UITheme.Dim);
            UIComponents.Label(new Rect(x + clockWidth * 0.55f, lineY, clockWidth * 0.45f, lineH),
                chrome.TimeText, GameFont.Tiny, UITheme.Accent);
            x += clockWidth + 13f;

            // 电网（预览 #powerbox：单行标签 + 读数 + 150×10 分段条 + 右端容量刻线）
            // `#powerbox .plbl br{display:none}` ⇒ 标签压成一行；`.pnum{color:var(--amber2)}`
            const float barWidth = 86f;    // 预览 #pbar 终值 width:86px（基線 §0a A6；原 150 已被覆盖）
            const float barHeight = 10f;   // 预览 #pbar{height:10px}（HTML 615 覆盖首版 14px）
            float powerBoxWidth = Mathf.Min(360f, rect.width * 0.42f);
            Rect powerBox = new Rect(rect.xMax - padX - powerBoxWidth, lineY, powerBoxWidth, lineH);

            // ⚠ 2026-10-06（用户指令「kpi 也要移除」）：顶栏读数带（预览 `#kpiline` = ① 区九卡的单行
            //   摘抄）**整段删除** —— 时钟之后直接进入电网块。九卡已不存在，该带无数据源。

            if (powerBox.x > x)
            {
                float labelWidth = Mathf.Min(MeasureWidth(chrome.PowerLabel, GameFont.Tiny) + 6f,
                    powerBox.width - barWidth - 70f);
                if (labelWidth > 24f)
                {
                    UIComponents.Label(new Rect(powerBox.x, powerBox.y, labelWidth, lineH),
                        UIComponents.TruncateToWidth(chrome.PowerLabel, labelWidth, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Dim);
                }

                Rect track = new Rect(powerBox.xMax - barWidth, powerBox.y + (lineH - barHeight) * 0.5f,
                    barWidth, barHeight);
                Widgets.DrawBoxSolid(track, UITheme.BorderHair);
                // 预览 #pfill 按 60%/85% 分段着色：这里按同一语义取语气色
                Color fill = chrome.PowerShare01 >= 0.85f
                    ? UITheme.Alive
                    : (chrome.PowerShare01 >= 0.60f ? UITheme.Threat : UITheme.Dead);
                Widgets.DrawBoxSolid(new Rect(track.x, track.y,
                    track.width * Mathf.Clamp01(chrome.PowerShare01), track.height), fill);
                UIComponents.Border(track, UITheme.BorderSoft);
                // 预览 #pbar .capmark{position:absolute;right:0;width:2px;background:var(--bad)}
                Widgets.DrawBoxSolid(new Rect(track.xMax - 2f, track.y, 2f, track.height), UITheme.Dead);

                float valueWidth = Mathf.Max(20f, powerBox.xMax - barWidth - 6f - powerBox.x - labelWidth);
                UIComponents.Label(new Rect(powerBox.x + labelWidth, powerBox.y, valueWidth, lineH),
                    UIComponents.TruncateToWidth(chrome.PowerText, valueWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleRight);
                TooltipHandler.TipRegion(powerBox, chrome.PowerLabel + "\n" + chrome.PowerText);
            }

            // 构建标记（右下角，极轻量）：用于实机核对"现在跑的是哪一版 dll"。
            string stamp = "build " + BuildStamp;
            float stampWidth = MeasureWidth(stamp, GameFont.Tiny) + 8f;
            Rect stampRect = new Rect(bar.xMax - stampWidth - 6f, bar.y + 6f, stampWidth, 18f);
            UIComponents.Label(stampRect, stamp, GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
            TooltipHandler.TipRegion(stampRect,
                Translate("PersonalChronicle.UI.Chrome.BuildTip") + " " + BuildStamp);

            return y + ChromeBarHeight;
        }

        /// <summary>
        /// 底栏（预览 `#ticker`）：「工厂广播」标签 + 一行真实厂况播报。
        /// 画在窗口最底部、通栏，绘制前已由 <see cref="TickerHeightForCurrentView"/> 预留高度。
        /// </summary>
        private void DrawTicker(Rect rect, ManufacturingChromeSnapshot chrome)
        {
            Rect bar = new Rect(rect.x, rect.yMax - TickerBarHeight, rect.width, TickerBarHeight);
            UIComponents.Panel(bar);
            UIComponents.Rule(new Rect(bar.x, bar.y, bar.width, 1f), UITheme.BorderSoft);

            string caption = Translate("PersonalChronicle.UI.Chrome.Broadcast");
            // 预览 `#ticker .tk-cap{padding:0 14px}` ⇒ 标签宽 = 文字 + 28
            float capWidth = Mathf.Min(MeasureWidth(caption, GameFont.Tiny) + 28f, bar.width * 0.25f);
            UIComponents.Label(new Rect(bar.x + 14f, bar.y + 2f, capWidth, TickerBarHeight - 4f),
                caption, GameFont.Tiny, UITheme.Accent);
            UIComponents.Rule(new Rect(bar.x + 14f + capWidth, bar.y + 5f, 1f, TickerBarHeight - 10f),
                UITheme.BorderSoft);

            float textX = bar.x + 14f + capWidth + 10f;
            float textWidth = Mathf.Max(20f, bar.xMax - textX - 10f);
            UIComponents.Label(new Rect(textX, bar.y + 3f, textWidth, TickerBarHeight - 4f),
                UIComponents.TruncateToWidth(chrome.TickerText, textWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Text);
            TooltipHandler.TipRegion(bar, chrome.TickerText);
        }

        // ⚠ 2026-10-06（用户指令「kpi 也要移除。确保干净」）：原 `DrawKpiLine`（顶栏 `#kpiline` 读数带绘制：
        //   逐项横排 / 第 5 项前竖直分隔线 / 附注窄屏隐藏）与 `KpiLineToneColor`（g/w/b 语气着色）
        //   **整块删除** —— 制造中心不再产出 KPI，读数带无源（cmdbar 里的调用段已同步删除）。
        //   恢复方式：从 git 历史取回本段 + `ManufacturingChromeSnapshot.KpiLine/KpiLineItem`
        //   + `SystemCenterReadModelProvider.FillKpiLine/KpiLineTone/ParsePercent` + cmdbar 绘制段。
    }
}
