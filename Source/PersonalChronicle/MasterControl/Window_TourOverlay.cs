using System;
using System.Collections.Generic;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Automation;
using RimWorld;
using RUIF;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// **引导式新手引导 · 全屏引导层**（CHG-20261006-TOUR2）。
    ///
    /// <para><b>为什么是独立窗口</b>：用户要求「详细引导玩家从建立车间、设备放置……全链路」——
    /// 其中「划车间 / 放设备 / 加工单」必须**离开总控中心、回到游戏世界**去做。
    /// 旧版把引导层画在总控中心窗口里 ⇒ 窗口一关引导就没了。本层是独立的**全屏透明窗口**：
    /// 主窗口关掉后它仍在栈上继续引导；`absorbInputAroundWindow = false` 且除自己的按钮外不消费事件
    /// ⇒ 玩家照常操作游戏（划区、建造、点标签页），引导只是"在旁边指"。</para>
    ///
    /// <para><b>锚点</b>：三类来源 —— ① 总控中心窗口内的控件（由该窗口每帧经
    /// <see cref="TourAnchorRegistry"/> 上报**屏幕坐标**）；② 地图上的车间（本层自己算：
    /// <c>Zone_Workshop.cells</c> 包围盒 → <c>UI.MapToUIPosition</c>）；③ 无锚点 ⇒ 居中气泡。</para>
    ///
    /// <para><b>跟随进度</b>：多数步骤带**完成条件**（<see cref="TourGate"/>）——
    /// 车间画好了 / 放了设备 / 加了带配料的工单 / 进入了制造中心 —— 一旦满足即**自动推进**；
    /// 未满足时「下一步」为禁用态并显示条件提示，「跳过本步」仍可用（绝不卡死）。</para>
    ///
    /// <para><b>状态</b>：全部是**窗口实例字段**（会话态、不入存档）；静态只有只读步骤表与
    /// <see cref="TourAnchorRegistry"/>（受控例外，见其类注释）。时钟一律 BCL
    /// （<c>DateTime.UtcNow.Ticks</c>，绘制路径禁用 <c>Time.realtimeSinceStartup</c>）。</para>
    /// </summary>
    internal sealed class Window_TourOverlay : Window
    {
        /// <summary>步骤的自动推进条件。</summary>
        private enum TourGate
        {
            None = 0,
            WorkshopExists,          // 地图上已有车间（Zone_Workshop）
            DevicesPlaced,           // 已有工作台/设备（IBillGiver）
            BillsWithIngredients,    // 已有带配料的工单
            RegistryView,            // 总控中心处于注册表页
            ManufacturingDetail      // 总控中心处于制造中心详情页
        }

        private struct TourStep
        {
            public string TitleKey;
            public string BodyKey;
            public int AnchorId;         // -1 = 无锚点（居中气泡）
            public bool MapWorkshop;     // true = 高亮地图上的车间包围盒
            public bool MapBench;        // true = 高亮车间内的第一个工作台（S5 用）
            /// <summary>
            /// true = 这一步玩家**要在地图上动手**（划区 / 放置 / 点建筑）。
            ///
            /// <para>⚠ 必须用它把引导层缩成"气泡小窗"：`WindowStack.GetWindowAt(pos)` 只按
            /// `windowRect.Contains(pos)` 判定（IL 取证），而 `Verse.CameraDriver::CameraDriverOnGUI`
            /// 正是它的消费者 ⇒ **全屏窗口会让相机驱动认为"鼠标一直在窗口上"，地图拖框划区被完全屏蔽**
            /// （用户实证："这一步无法进行划区，因为无法点击"）。缩窗后鼠标落在地图上时
            /// `GetWindowAt` 返回 null，地图交互恢复正常；代价是这些步骤不画全屏遮罩。</para>
            /// </summary>
            public bool MapInteractive;
            public TourGate Gate;
            public string GateHintKey;   // 条件提示（Gate != None 时用于"下一步"禁用态说明）
        }

        // ---- 步骤表（构建期常量；文案全部键驱动，zh/en 双侧）----
        private const string K = "PersonalChronicle.UI.Guide.Tour2.";
        private static readonly TourStep[] Steps =
        {
            new TourStep { TitleKey = K + "S0.Title", BodyKey = K + "S0.Body", AnchorId = TourAnchorRegistry.AnchorContentArea, MapWorkshop = false, Gate = TourGate.None },
            new TourStep { TitleKey = K + "S1.Title", BodyKey = K + "S1.Body", AnchorId = TourAnchorRegistry.AnchorHeaderGuide, MapWorkshop = false, Gate = TourGate.None },
            new TourStep { TitleKey = K + "S2.Title", BodyKey = K + "S2.Body", AnchorId = TourAnchorRegistry.AnchorThemeSwitch, MapWorkshop = false, Gate = TourGate.None },
            // S3/S4/S5 = **地图交互步**（划区 / 放置 / 点工作台）：引导层缩成气泡小窗，让地图可点
            new TourStep { TitleKey = K + "S3.Title", BodyKey = K + "S3.Body", AnchorId = -1, MapWorkshop = true, MapInteractive = true, Gate = TourGate.WorkshopExists, GateHintKey = K + "Gate.Workshop" },
            new TourStep { TitleKey = K + "S4.Title", BodyKey = K + "S4.Body", AnchorId = -1, MapWorkshop = true, MapInteractive = true, Gate = TourGate.DevicesPlaced, GateHintKey = K + "Gate.Device" },
            // S5 的高亮目标是**车间内的工作台**（比整区包围盒更精确：玩家要点的就是它）
            new TourStep { TitleKey = K + "S5.Title", BodyKey = K + "S5.Body", AnchorId = -1, MapWorkshop = false, MapBench = true, MapInteractive = true, Gate = TourGate.BillsWithIngredients, GateHintKey = K + "Gate.Bill" },
            new TourStep { TitleKey = K + "S6.Title", BodyKey = K + "S6.Body", AnchorId = TourAnchorRegistry.AnchorContentArea, MapWorkshop = false, Gate = TourGate.ManufacturingDetail, GateHintKey = K + "Gate.OpenCenter" },
            // S7/S8/S9（左轨 / 右轨 / 页签）**暂不挖洞**：那三个控件在滚动/嵌套容器里，
            // 逐点上报需要"组内局部坐标 → 屏幕坐标"换算，本批未接线 ⇒ 用 AnchorId = -1
            // 诚实降级为居中气泡 + 定位文字（守门 P3：不得把"未接线"写成"已支持"）。
            new TourStep { TitleKey = K + "S7.Title", BodyKey = K + "S7.Body", AnchorId = -1, MapWorkshop = false, Gate = TourGate.None },
            new TourStep { TitleKey = K + "S8.Title", BodyKey = K + "S8.Body", AnchorId = -1, MapWorkshop = false, Gate = TourGate.None },
            new TourStep { TitleKey = K + "S9.Title", BodyKey = K + "S9.Body", AnchorId = -1, MapWorkshop = false, Gate = TourGate.None },
            new TourStep { TitleKey = K + "S10.Title", BodyKey = K + "S10.Body", AnchorId = -1, MapWorkshop = false, Gate = TourGate.None },
            new TourStep { TitleKey = K + "S11.Title", BodyKey = K + "S11.Body", AnchorId = TourAnchorRegistry.AnchorContentArea, MapWorkshop = false, Gate = TourGate.RegistryView },
            new TourStep { TitleKey = K + "S12.Title", BodyKey = K + "S12.Body", AnchorId = -1, MapWorkshop = false, Gate = TourGate.None }
        };

        // ---- 几何与时长（统一视觉规范：间距全部由字体度量派生，见 §气泡布局）----
        private const float BubbleWidth = 400f;      // 目标宽度（窄屏按屏宽夹取）
        private const float BubblePad = 14f;         // 内边距（四周一致）
        private const float BubbleMargin = 20f;      // 气泡与屏幕右下边的距离
        private const float GapTight = 6f;           // 标题↔正文 / 正文↔条件行
        private const float GapRow = 10f;            // 条件行↔进度行
        private const float GapBottom = 12f;         // 进度行↔按钮行
        private const float HoleInset = 6f;          // 仅全屏遮罩模式使用
        private const float BubbleGap = 10f;         // 仅全屏遮罩模式使用
        /// <summary>屏幕底部主按钮栏条带高度：气泡**避让**该带（那里不属于窗口栈，会抢点击）。</summary>
        private const float MainBarReserved = 40f;
        private const float FadeInMs = 200f;
        private const float StepFadeInMs = 160f;

        /// <summary>按钮行高（比 Tiny 行高多 8px 触感余量）。</summary>
        private static float ButtonRowHeight()
        {
            return Measure.RowHeight(GameFont.Tiny) + 8f;
        }

        /// <summary>进度文本「n / N」缓存（步号变化时才重算 —— 绘制路径不每帧拼字符串）。</summary>
        private int stepTextForStep = -1;
        private string stepTextCache = string.Empty;

        private string StepText()
        {
            if (stepTextForStep != step)
            {
                stepTextForStep = step;
                stepTextCache = (step + 1).ToString() + " / " + Steps.Length.ToString();
            }
            return stepTextCache;
        }

        // ---- 会话状态（实例字段，不入存档）----
        private int step;
        private long tourStartTicks;
        private long stepStartTicks;

        // 地图车间包围盒缓存（只在同一步内复用；车间变了就重算）
        private int mapRectCacheZoneId = -1;
        private int mapRectCacheCellCount = -1;
        private Rect mapRectCache;
        private bool mapRectCacheValid;

        public Window_TourOverlay()
        {
            doWindowBackground = false;
            drawShadow = false;
            doCloseX = false;
            // ⚠ ESC 走**引擎的取消键路由**（用户实证的 bug 修正）：原先本层 closeOnCancel=false
            //   并在绘制里自己检测 ESC —— 结果引擎先把 ESC 交给了下面那层仍 closeOnCancel=true 的
            //   总控中心（一次按键只喂一个窗口），于是"按一次 ESC 关掉的是总控中心，要按第二次才退引导"。
            //   现在本层**声明接管**取消键（closeOnCancel = true）并 override OnCancelKeyPressed
            //   ⇒ 引擎在栈顶就把它消化掉，ESC 只退引导、绝不外溢给任何下层窗口。
            closeOnCancel = true;
            forcePause = false;
            absorbInputAroundWindow = false;   // 全屏透明层：不吸输入，玩家照常操作游戏
        }

        /// <summary>
        /// ESC = 退出引导（**唯一入口**）。引擎的取消键路由会把 ESC 交给本层，
        /// 本层消化后不再下传 ⇒ 不会连带关闭总控中心或其它窗口。
        /// </summary>
        public override void OnCancelKeyPressed()
        {
            // 精准吞掉这次 ESC：除窗口栈之外，`MainTabsRoot` 等快捷键层也可能在同一帧读同一事件
            // （它一旦消费就会关掉当前标签页 —— 正是用户看到的"ESC 退出了管理系统"）。
            // 只在"当前事件确实是那一次 Esc KeyDown"时 Use()，避免误吞其它事件。
            if (Event.current != null && Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
            }
            Close();
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(UI.screenWidth, UI.screenHeight); }
        }

        protected override void SetInitialSizeAndPosition()
        {
            // 全屏覆盖（透明）：不居中、不留边。
            windowRect = new Rect(0f, 0f, UI.screenWidth, UI.screenHeight);
        }

        /// <summary>已经执行过"自动导航"的步骤号（-1 = 无）；每步只导航一次，避免每帧抢相机。</summary>
        private int lastNavigatedStep = -1;

        /// <summary>已经把 windowRect 切换成本步形态的步骤号（-1 = 未切）；见 <see cref="ApplyStepWindow"/>。</summary>
        private int lastWindowAppliedStep = -1;

        /// <summary>
        /// 是否启用**全屏遮罩 + 挖洞高亮**（默认关闭 —— 见 <see cref="ApplyStepWindow"/> 的 IL 取证）：
        /// 全屏 `windowRect` 会同时屏蔽**地图**（`CameraDriverOnGUI`）与**下层窗口的鼠标事件**
        /// （`GetWindowAt` 只看 `windowRect.Contains`），与"玩家要能点"直接冲突。
        /// 留此开关，是为了将来若把遮罩改到 `UIRoot` 的 MonoBehaviour 上绘制（不产生窗口）时可直接启用。
        /// </summary>
        private static bool fullScreenMaskEnabled = false;

        internal void Begin()
        {
            step = 0;
            tourStartTicks = DateTime.UtcNow.Ticks;
            stepStartTicks = tourStartTicks;
            mapRectCacheValid = false;
            lastNavigatedStep = -1;
            TourAnchorRegistry.Begin();
            SelfCheckAllSteps();
        }

        /// <summary>
        /// 启动时的**一次性全步骤自检**（用户 2026-10-06："认真检查每一步"）。
        /// 逐步骤把「气泡高度 / 是否地图交互步 / Gate / 文案缺键」写进日志 ——
        /// 目的是让"某一步按钮被裁、文案为空、条件写错"这类问题**在日志里可直接定位**，
        /// 而不是靠玩家截图肉眼比对。13 行、只在启动引导时输出一次。
        /// </summary>
        private static void SelfCheckAllSteps()
        {
            float width = Mathf.Min(BubbleWidth, Mathf.Max(220f, UI.screenWidth - BubbleMargin * 2f));
            for (int i = 0; i < Steps.Length; i++)
            {
                TourStep s = Steps[i];
                string title = UIComponents.Loc(s.TitleKey);
                string body = UIComponents.Loc(s.BodyKey);
                bool titleMissing = string.IsNullOrEmpty(title) || title == s.TitleKey;
                bool bodyMissing = string.IsNullOrEmpty(body) || body == s.BodyKey;
                Log.Message("[PersonalChronicle] tour step " + i
                    + ": h=" + MeasureBubbleHeight(s, width).ToString("F0")
                    + " interactive=" + s.MapInteractive
                    + " gate=" + s.Gate
                    + (titleMissing ? " TITLE-MISSING" : string.Empty)
                    + (bodyMissing ? " BODY-MISSING" : string.Empty));
            }
        }

        public override void PreClose()
        {
            base.PreClose();
            // 收口：注册表失活（否则总控中心会继续上报锚点）
            TourAnchorRegistry.End();
        }

        // ------------------------------------------------------------ 绘制

        public override void DoWindowContents(Rect inRect)
        {
            // ⚠ 这里**不**清锚点表（首版那样写会 P0：清空与读取在同一趟 ⇒ 恒读空）。
            //   锚点带时间戳、按新鲜度判定，与两窗口的绘制先后无关（见 TourAnchorRegistry）。
            long now = DateTime.UtcNow.Ticks;

            // ESC 不再在这里自检：改由引擎的取消键路由调用 `OnCancelKeyPressed()`（见构造函数注释），
            // 否则会出现"一次 ESC 关掉下层窗口、要按第二次才退引导"（用户实证）。
            TourStep current = Steps[step];
            // 遮罩/挖洞只在"全屏形态"下有意义；默认关闭（见 fullScreenMaskEnabled 的取证说明）
            Rect hole = default(Rect);   // 短路时 TryGetHole 不执行 ⇒ 必须先初始化（CS0165）
            bool hasHole = fullScreenMaskEnabled && !current.MapInteractive
                && TryGetHole(current, inRect, out hole);

            // Gate 满足 ⇒ 自动推进（用户做到了就往前走，不让他多点一次）
            if (current.Gate != TourGate.None && GateSatisfied(current.Gate))
            {
                if (step >= Steps.Length - 1)
                {
                    Close();
                    return;
                }
                step++;
                stepStartTicks = now;
                mapRectCacheValid = false;
                current = Steps[step];
                hasHole = fullScreenMaskEnabled && !current.MapInteractive
                    && TryGetHole(current, inRect, out hole);
            }

            // 步骤变了 ⇒ 重设窗口形态（全屏 ↔ 气泡小窗）。**统一入口**：Begin / 自动推进 /
            // "下一步" / "上一步" 四条路径都经这里，不会有漏改的切换点。
            // 注：本帧改 windowRect、下一帧生效；本帧绘制仍用引擎传入的 inRect，两者自洽。
            if (step != lastWindowAppliedStep)
            {
                lastWindowAppliedStep = step;
                ApplyStepWindow(step);
            }

            // 进入新步骤时**自动导航一次**（用户要求："应该自动跳转到该位置去继续引导"）：
            // 打开对应的游戏面板 / 把相机对准车间与工作台 / 自动打开总控中心。
            NavigateIfNeeded(step);

            float mask = 0.62f * Ease01(tourStartTicks, now, FadeInMs);
            Color maskColor = UITheme.WithAlpha(UITheme.Window, mask);
            // 全屏遮罩只在全屏形态下启用（默认关闭）：小窗形态下画它会把自己压暗
            if (fullScreenMaskEnabled && hasHole)
            {
                if (hole.y > inRect.y)
                {
                    Draw.Box(new Rect(inRect.x, inRect.y, inRect.width, hole.y - inRect.y), maskColor);
                }
                if (hole.yMax < inRect.yMax)
                {
                    Draw.Box(new Rect(inRect.x, hole.yMax, inRect.width, inRect.yMax - hole.yMax), maskColor);
                }
                if (hole.x > inRect.x)
                {
                    Draw.Box(new Rect(inRect.x, hole.y, hole.x - inRect.x, hole.height), maskColor);
                }
                if (hole.xMax < inRect.xMax)
                {
                    Draw.Box(new Rect(hole.xMax, hole.y, inRect.xMax - hole.xMax, hole.height), maskColor);
                }
            }
            else if (fullScreenMaskEnabled)
            {
                Draw.Box(inRect, maskColor);
            }

            if (hasHole)
            {
                float t = Seconds(now);
                Draw.Border(hole, UITheme.WithAlpha(UITheme.Accent, Pulse(t, 0.55f)));
                Shapes.CornerFrame(hole.ExpandedBy(2f), UITheme.WithAlpha(UITheme.Accent, 0.95f), 14f, 2f);
            }

            DrawBubble(inRect, hasHole ? hole : default(Rect), hasHole, current, now);
        }

        private void DrawBubble(Rect screen, Rect hole, bool hasHole, TourStep current, long now)
        {
            float line = Measure.RowHeight(GameFont.Small);
            float tinyLine = Measure.RowHeight(GameFont.Tiny);
            float btnHeight = ButtonRowHeight();
            bool gated = current.Gate != TourGate.None && !GateSatisfied(current.Gate);
            // ⚠ 宽度必须与 `ApplyStepWindow` 定窗口时**逐值一致**：
            //   小窗形态下 `screen` 就是窗口本身，再减 20 会让正文多折一行 ⇒ 总高超出窗口 ⇒
            //   按钮行被 GUI group 裁掉（用户实证："怎么没有下一步"）。
            float bubbleWidth = !fullScreenMaskEnabled
                ? screen.width
                : Mathf.Min(BubbleWidth, Mathf.Max(220f, screen.width - BubbleMargin * 2f));
            float innerWidth = Mathf.Max(40f, bubbleWidth - BubblePad * 2f);
            // 高度与"小窗尺寸"（ApplyStepWindow）**共用同一算式**：算一套画一套会让小窗裁掉按钮。
            // 正文高度用**同一个分段测量**（`MeasureBodyHeight`）—— 绘制逐段累加与之同源。
            float bubbleHeight = MeasureBubbleHeight(current, bubbleWidth);
            // 记下"绘制期实测高"：窗口尺寸下一帧据此对齐（消除测量与渲染的任何出入）
            measuredStep = step;
            measuredHeight = bubbleHeight;
            float bodyHeight = MeasureBodyHeight(UIComponents.Loc(current.BodyKey), innerWidth);

            float appear = Ease01(stepStartTicks, now, StepFadeInMs);
            Rect bubble;
            if (!fullScreenMaskEnabled)
            {
                // 小窗形态（默认）：窗口**就是**气泡（ApplyStepWindow 已把 windowRect 设成气泡大小），
                // 铺满窗口即可 —— 鼠标落在地图 / 下层窗口上时 GetWindowAt 不命中，一切点击正常。
                bubble = new Rect(0f, (1f - appear) * 6f, screen.width,
                    Mathf.Min(bubbleHeight, screen.height));
            }
            else
            {
                // 底部条带（主按钮栏）预留：气泡落在那儿会被主按钮栏抢点击（守门 P2）。
                float bottomLimit = screen.yMax - MainBarReserved;
                float bx = Mathf.Clamp((hasHole ? hole.center.x : screen.center.x) - bubbleWidth * 0.5f,
                    screen.x + 6f, Mathf.Max(screen.x + 6f, screen.xMax - bubbleWidth - 6f));
                float by;
                if (hasHole && hole.yMax + BubbleGap + bubbleHeight <= bottomLimit - 6f)
                {
                    by = hole.yMax + BubbleGap;
                }
                else if (hasHole && hole.y - BubbleGap - bubbleHeight >= screen.y + 6f)
                {
                    by = hole.y - BubbleGap - bubbleHeight;
                }
                else
                {
                    by = Mathf.Clamp(screen.center.y - bubbleHeight * 0.5f,
                        screen.y + 6f, Mathf.Max(screen.y + 6f, bottomLimit - bubbleHeight - 6f));
                }
                bubble = new Rect(bx, by + (1f - appear) * 6f, bubbleWidth, bubbleHeight);
            }
            Color prevGui = GUI.color;
            // ⚠ 淡入落在"各自传参的颜色"上：外层 `GUI.color` 对 RUIF 组件与显式传色的
            //   原版 `Widgets.Label` 都不生效（守门 U-2 实证）。
            try
            {
                UIComponents.Card(bubble, UITheme.WithAlpha(UITheme.Accent, appear));

                float x = bubble.x + BubblePad;
                float rightEdge = bubble.xMax - BubblePad;

                // ── ① 先把**底部三行**从气泡底边反推定位（自下而上排布）──
                //    按钮行 → 进度行 →（有 Gate 时的）条件行。这样无论正文多长，
                //    这三行都不会被"挤走或压住"。
                float navY = Mathf.Min(bubble.yMax, screen.yMax) - BubblePad - btnHeight;
                float progressY = navY - GapBottom - tinyLine;
                float hintY = progressY - GapRow - tinyLine;
                float bodyBottom = (current.Gate != TourGate.None ? hintY : progressY) - GapTight;

                // ── ② 标题行：左侧 3px 竖条作视觉锚点（不用图标 —— 规避字体缺字风险）+ 标题 ──
                float y = bubble.y + BubblePad;
                Draw.Box(new Rect(x, y + 4f, 3f, Mathf.Max(6f, line - 8f)),
                    UITheme.WithAlpha(UITheme.Accent, appear));
                Rect titleRect = new Rect(x + 9f, y, Mathf.Max(40f, rightEdge - (x + 9f)), line);
                UIComponents.Label(titleRect,
                    UIComponents.TruncateToWidth(UIComponents.Loc(current.TitleKey), titleRect.width,
                        GameFont.Small, titleRect),
                    GameFont.Small, UITheme.WithAlpha(UITheme.Text, appear), TextAnchor.MiddleLeft);
                float bodyTop = y + line + GapTight;

                // ── ③ 正文：按 `\n` 分段绘制，并**裁剪在 [bodyTop, bodyBottom] 之内** ──
                //    ⚠ 这是"重叠"的根治手段（用户实证 4~7 步重叠）：中文换行测量不可能 100% 精确
                //    （`CalcHeight` 与整段宽估算都可能偏差），一旦偏差，本裁剪保证结果最多是
                //    "正文少显示一点"，**绝不会**压住条件行 / 进度行 / 按钮行。
                //    裁剪高取"测量值"与"可用空间"的**较小**者 ⇒ 与算式同源且绝不过界。
                string[] segments = SplitBody(UIComponents.Loc(current.BodyKey));
                float bodyAvail = Mathf.Max(line, bodyBottom - bodyTop);
                Rect bodyClip = new Rect(x, bodyTop, innerWidth, Mathf.Min(bodyHeight, bodyAvail));
                // 文本全局态一律"保存 → 就地改 → 恢复"（守门 P3：不写回猜测的默认值）
                GameFont prevBodyFont = Text.Font;
                TextAnchor prevBodyAnchor = Text.Anchor;
                bool prevBodyWrap = Text.WordWrap;
                Color prevText = GUI.color;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                GUI.color = UITheme.WithAlpha(UITheme.Text, appear);
                GUI.BeginClip(bodyClip);
                try
                {
                    // ⚠ `GUI.BeginClip` 之后坐标系以裁剪区左上角为原点 ⇒ 段内 y 从 0 起累加
                    float clipY = 0f;
                    for (int i = 0; i < segments.Length; i++)
                    {
                        float segmentHeight = Mathf.Max(line, Text.CalcHeight(segments[i], innerWidth));
                        Widgets.Label(new Rect(0f, clipY, innerWidth, segmentHeight), segments[i]);
                        clipY += segmentHeight;
                    }
                }
                finally
                {
                    GUI.EndClip();
                }
                GUI.color = prevText;
                Text.Font = prevBodyFont;
                Text.Anchor = prevBodyAnchor;
                Text.WordWrap = prevBodyWrap;

                // ── ④ 条件行（有 Gate 才有；位置已由底部反推确定，不参与正文累加）──
                if (current.Gate != TourGate.None)
                {
                    string hint = UIComponents.Loc(current.GateHintKey);
                    Rect hintRect = new Rect(x, hintY, innerWidth, tinyLine);
                    UIComponents.Label(hintRect,
                        UIComponents.TruncateToWidth(hint, innerWidth, GameFont.Tiny, hintRect),
                        GameFont.Tiny, gated ? UITheme.Muted : UITheme.Accent, TextAnchor.MiddleLeft);
                }

                // 进度行：条形进度（左）+「n / N」（中）+「ESC 退出」（右）
                // ⚠ 对比度（用户反馈"进度行看不见"）：条高 5px、底色 60% Dim、填充 Accent（不低于 50%），
                //   步序文本改用**正文色** —— 原先 4px 细条 + 45% Dim 在深色面板上等于隐形。
                const float barThickness = 5f;
                float barWidth = Mathf.Min(innerWidth * 0.4f, 140f);
                Rect barBg = new Rect(x, progressY + (tinyLine - barThickness) * 0.5f, barWidth, barThickness);
                Draw.Box(barBg, UITheme.WithAlpha(UITheme.Dim, 0.6f));
                Draw.Box(new Rect(barBg.x, barBg.y,
                        Mathf.Max(barThickness, barBg.width * ((step + 1f) / Steps.Length)), barBg.height),
                    UITheme.WithAlpha(UITheme.Accent, Mathf.Max(0.5f, appear)));
                Rect stepTextRect = new Rect(barBg.xMax + 8f, progressY, 56f, tinyLine);
                UIComponents.Label(stepTextRect, StepText(),
                    GameFont.Tiny, UITheme.WithAlpha(UITheme.Text, Mathf.Max(0.7f, appear)),
                    TextAnchor.MiddleLeft);
                string escHint = UIComponents.Loc(K + "EscHint");
                float escWidth = Mathf.Min(110f, innerWidth * 0.35f);
                Rect escRect = new Rect(rightEdge - escWidth, progressY, escWidth, tinyLine);
                UIComponents.Label(escRect,
                    UIComponents.TruncateToWidth(escHint, escRect.width, GameFont.Tiny, escRect),
                    GameFont.Tiny, UITheme.WithAlpha(UITheme.Muted, appear), TextAnchor.MiddleRight);

                // 按钮组：跳过本步（左，**弱化**=无焦点环） / 上一步 · 下一步（右，主按钮）
                bool last = step >= Steps.Length - 1;
                float skipWidth = Mathf.Min(96f, innerWidth * 0.3f);
                float navWidth = Mathf.Min(116f, innerWidth * 0.32f);
                float prevWidth = Mathf.Min(96f, innerWidth * 0.28f);
                Rect skipRect = new Rect(x, navY, skipWidth, btnHeight);
                Rect prevRect = new Rect(rightEdge - navWidth - prevWidth - 8f, navY, prevWidth, btnHeight);
                Rect navRect = new Rect(rightEdge - navWidth, navY, navWidth, btnHeight);

                // 「跳过本步」用**弱化样式**（无焦点环）：与右侧主按钮形成层级，不再抢视线
                if (ButtonAtoms.PrimaryButton(skipRect, UIComponents.Loc(K + "Skip"), true,
                    ButtonTone.Default, false))
                {
                    if (last)
                    {
                        Close();
                        return;
                    }
                    Advance(now);
                    return;
                }
                if (ButtonAtoms.PrimaryButton(prevRect, UIComponents.Loc(K + "Prev"), step > 0))
                {
                    if (step > 0)
                    {
                        step--;
                        stepStartTicks = DateTime.UtcNow.Ticks;
                        mapRectCacheValid = false;
                    }
                }
                string navLabel = UIComponents.Loc(last ? K + "Finish" : K + "Next");
                // ⚠ 末步恒可点；中间步在条件未满足时禁用（并显示条件提示），但「跳过本步」始终可用
                bool navEnabled = last || !gated;
                if (ButtonAtoms.PrimaryButton(navRect, navLabel, navEnabled))
                {
                    if (last)
                    {
                        Close();
                        return;
                    }
                    Advance(now);
                }
            }
            finally
            {
                GUI.color = prevGui;
            }
        }

        private void Advance(long now)
        {
            if (step < Steps.Length - 1)
            {
                step++;
                stepStartTicks = now;
                mapRectCacheValid = false;
            }
        }

        // ------------------------------------------------------------ 锚点

        private bool TryGetHole(TourStep current, Rect screen, out Rect hole)
        {
            hole = default(Rect);
            Rect raw;
            if (current.MapWorkshop)
            {
                if (!TryGetWorkshopScreenRect(out raw))
                {
                    return false;   // 没有车间 ⇒ 降级为中心气泡 + 文字引导
                }
            }
            else if (current.MapBench)
            {
                if (!TryGetBenchScreenRect(out raw))
                {
                    return false;   // 还没建工作台 ⇒ 同上降级
                }
            }
            else
            {
                if (current.AnchorId < 0 || !TourAnchorRegistry.TryGet(current.AnchorId, out raw))
                {
                    return false;
                }
            }

            Rect expanded = raw.ExpandedBy(HoleInset);
            float x1 = Mathf.Clamp(expanded.x, screen.x, screen.xMax);
            float y1 = Mathf.Clamp(expanded.y, screen.y, screen.yMax);
            float x2 = Mathf.Clamp(expanded.xMax, screen.x, screen.xMax);
            float y2 = Mathf.Clamp(expanded.yMax, screen.y, screen.yMax);
            if (x2 - x1 < 4f || y2 - y1 < 4f)
            {
                return false;
            }
            hole = new Rect(x1, y1, x2 - x1, y2 - y1);
            return true;
        }

        /// <summary>
        /// 地图上**第一个车间**的屏幕包围盒（<c>UI.MapToUIPosition</c> 逐格映射后取 min/max）。
        /// 逐格是 O(格数)，故按（车间 ID + 格数）缓存 —— 同一车间内容不变时不重算。
        /// </summary>
        private bool TryGetWorkshopScreenRect(out Rect rect)
        {
            rect = default(Rect);
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return false;
            }
            Zone_Workshop workshop = FindAnyWorkshop(map);
            if (workshop == null || workshop.cells == null || workshop.cells.Count == 0)
            {
                return false;
            }
            if (mapRectCacheValid && mapRectCacheZoneId == workshop.ID
                && mapRectCacheCellCount == workshop.cells.Count)
            {
                rect = mapRectCache;
                return true;
            }

            List<IntVec3> cells = workshop.cells;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 p = UI.MapToUIPosition(cells[i].ToVector3Shifted());
                if (p.x < minX) { minX = p.x; }
                if (p.y < minY) { minY = p.y; }
                if (p.x > maxX) { maxX = p.x; }
                if (p.y > maxY) { maxY = p.y; }
            }
            Rect computed = new Rect(minX, minY, Mathf.Max(8f, maxX - minX), Mathf.Max(8f, maxY - minY));
            mapRectCacheZoneId = workshop.ID;
            mapRectCacheCellCount = cells.Count;
            mapRectCache = computed;
            mapRectCacheValid = true;
            rect = computed;
            return true;
        }

        // ------------------------------------------------------------ 步骤窗口（全屏 ↔ 气泡小窗）

        /// <summary>
        /// 按当前步骤设置 `windowRect`：
        /// **地图交互步**（S3/S4/S5）⇒ 缩成右下角的**气泡小窗**（不覆盖地图 ⇒ `GetWindowAt` 不命中
        /// ⇒ 地图拖框划区正常）；其余步骤 ⇒ 全屏（遮罩 + 挖洞 + 气泡）。
        ///
        /// <para>调用点与"步骤切换"绑定（Begin / 自动推进 / 下一步 / 上一步）：引擎每帧用当前
        /// `windowRect` 开 group，本帧改、下一帧生效 —— 而绘制用引擎传入的 `inRect`，
        /// 两者始终自洽，不会坐标错位。</para>
        /// </summary>
        /// <summary>上一次"绘制期实测"的气泡高度（连同步骤号）—— 见 <see cref="ApplyStepWindow"/>。</summary>
        private int measuredStep = -1;
        private float measuredHeight;

        private void ApplyStepWindow(int stepIndex)
        {
            // ⚠ **恒用"气泡小窗"**（用户两次实证后的定论）：
            //   `WindowStack.GetWindowAt` 只按 `windowRect.Contains` 判定，其消费者既有
            //   `CameraDriverOnGUI`（地图划区/点建筑）也有 `WindowStack.HandleEventsHighPriority`；
            //   全屏窗口 ⇒ 鼠标永远算"落在窗口上" ⇒ **地图与下层窗口的点击一起失效**
            //   （"无法划区"、"这一步无法点击"）。遮罩必须全屏、可点击必须不全屏 —— 二者互斥，
            //   取"可操作"，高亮改由相机对准 + 文案承担；并留 `fullScreenMaskEnabled` 开关备用。
            if (stepIndex < 0 || stepIndex >= Steps.Length)
            {
                return;
            }
            float width = Mathf.Min(BubbleWidth, Mathf.Max(220f, UI.screenWidth - BubbleMargin * 2f));
            // 高度**优先用上一次本步绘制时的实测值**：测量依赖 `Text` 全局态与字体度量，
            // 在"步骤切换那一刻"与"绘制那一刻"理论上同值、实践中可能有出入 —— 以绘制期实测为准，
            // 从第二帧起窗口与内容**严格相等**（第一帧用同式估算，一帧后自动纠正）。
            float height = stepIndex == measuredStep
                ? measuredHeight
                : MeasureBubbleHeight(Steps[stepIndex], width);
            float x = Mathf.Max(6f, UI.screenWidth - width - 16f);
            float y = Mathf.Max(6f, UI.screenHeight - MainBarReserved - height - 16f);
            windowRect = new Rect(x, y, width, height);
        }

        /// <summary>正文分段缓存（按 `\n` 切；同文本只切一次 —— 绘制路径零分配）。</summary>
        private static string bodySplitCacheKey;
        private static string[] bodySplitCache;

        private static string[] SplitBody(string text)
        {
            if (string.Equals(bodySplitCacheKey, text, StringComparison.Ordinal) && bodySplitCache != null)
            {
                return bodySplitCache;
            }
            bodySplitCacheKey = text;
            bodySplitCache = text.Split('\n');
            return bodySplitCache;
        }

        /// <summary>
        /// 正文高度（**唯一算式**：测量与绘制逐段同源）。按 `\n` 分段、逐段 <c>Text.CalcHeight</c>：
        /// ① 分段是为了让 Keyed 文案里的 <c>\n</c> 也计入高度（S3 就是两段）；
        /// ② 必须开 <c>Text.WordWrap</c> —— 关闭状态下原版 <c>CalcHeight</c> 返回的是**单行高**，
        ///    会把窗口算矮、底部进度与按钮被裁（用户两次实证的真根因）。
        /// </summary>
        private static float MeasureBodyHeight(string text, float width)
        {
            float line = Measure.RowHeight(GameFont.Small);
            if (string.IsNullOrEmpty(text))
            {
                return line;
            }
            string[] segments = SplitBody(text);
            GameFont prevFont = Text.Font;
            bool prevWrap = Text.WordWrap;
            Text.Font = GameFont.Small;
            Text.WordWrap = true;
            float total = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                float estimated = Mathf.Max(line, Text.CalcHeight(segment, width));
                float charWrapped = Mathf.Max(line, EstimateWrappedHeight(segment, width, line));
                // 取两者较大：估算函数在中文（无空格）下会少算行数，取多数值 ⇒ 气泡略高、
                // 绝不把进度行/按钮压掉（用户两次实证"正文盖住下面的行"）。
                total += Mathf.Max(estimated, charWrapped);
            }
            Text.WordWrap = prevWrap;
            Text.Font = prevFont;
            return total;
        }

        /// <summary>
        /// 按**整段实测宽度**估算折行高度：`整段宽 / 行宽` 向上取整**再加一行余量**。
        ///
        /// <para>为什么需要它与 <c>CalcHeight</c> 取较大值：中文没有空格，原版
        /// <c>Text.CalcHeight</c> 的换行判定对"不可断的长串"会低估行数（视作一行），
        /// 于是窗口算矮、正文压住进度行与按钮（用户实证："还是存在布局参数问题"）。
        /// 本估算只会**多算**（安全方向），代价是气泡偶尔高一两行。</para>
        /// </summary>
        private static float EstimateWrappedHeight(string text, float width, float line)
        {
            float totalWidth = Text.CalcSize(text).x;
            if (totalWidth <= width)
            {
                return line;
            }
            int lines = Mathf.CeilToInt(totalWidth / Mathf.Max(1f, width)) + 1;
            return lines * line;
        }

        /// <summary>
        /// 气泡总高（**唯一算式**：绘制与"小窗尺寸"共用，LAND-002 不得"算一套画一套"）。
        /// 布局：pad → 标题行 → 正文 → [条件行] → 进度行 → 按钮行 → pad。
        /// </summary>
        private static float MeasureBubbleHeight(TourStep step, float width)
        {
            float line = Measure.RowHeight(GameFont.Small);
            float tinyLine = Measure.RowHeight(GameFont.Tiny);
            float innerWidth = Mathf.Max(40f, width - BubblePad * 2f);
            float bodyHeight = MeasureBodyHeight(UIComponents.Loc(step.BodyKey), innerWidth);
            // 尾部与 `DrawBubble` 的"自下而上反推"**逐项同式**：
            //   [条件行(tiny+GapRow)] → 进度行(tiny) → GapBottom → 按钮行 → pad
            float hintRowHeight = step.Gate == TourGate.None ? 0f : tinyLine + GapRow;
            return BubblePad * 2f
                + line + GapTight
                + bodyHeight
                + GapTight + hintRowHeight + tinyLine
                + GapBottom + ButtonRowHeight();
        }

        // ------------------------------------------------------------ 自动导航（每步进入时一次）

        /// <summary>
        /// 「应该自动跳转到该位置去继续引导」（用户 2026-10-06 要求）：进入新步骤时**替玩家走一步**
        /// —— 打开建造菜单 / 把相机对准车间与工作台 / 自动打开总控中心。每步只做一次
        /// （<see cref="lastNavigatedStep"/>），不抢相机、不重复打扰；「上一步」回退时会重新导航。
        /// </summary>
        private void NavigateIfNeeded(int stepIndex)
        {
            if (stepIndex == lastNavigatedStep)
            {
                return;
            }
            lastNavigatedStep = stepIndex;
            Map map = Find.CurrentMap;
            switch (stepIndex)
            {
                case 3:   // 划车间区：把建造菜单打开（玩家只需点「区域」→「车间区」）
                    OpenTab(MainButtonDefOf.Architect);
                    break;
                case 4:   // 放设备：保持建造菜单，并把相机对到车间
                    OpenTab(MainButtonDefOf.Architect);
                    JumpToWorkshop(map);
                    break;
                case 5:   // 加工单：相机对到车间内的工作台（高亮的就是它）
                    JumpToBench(map);
                    break;
                case 6:   // 回看板：自动打开总控中心（玩家只需点「制造系统中心」卡片）
                    OpenTab(FindMasterControlDef());
                    break;
            }
        }

        private static void OpenTab(MainButtonDef def)
        {
            if (def != null && Find.MainTabsRoot != null)
            {
                Find.MainTabsRoot.SetCurrentTab(def, true);
            }
        }

        /// <summary>本工程总控中心的主按钮 Def：按**窗口类型**找，不写死 defName（LAND-010）。</summary>
        private static MainButtonDef FindMasterControlDef()
        {
            List<MainButtonDef> defs = DefDatabase<MainButtonDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].tabWindowClass == typeof(MasterControlTabWindow))
                {
                    return defs[i];
                }
            }
            return null;
        }

        private static void JumpToWorkshop(Map map)
        {
            Zone_Workshop workshop = map != null ? FindAnyWorkshop(map) : null;
            if (workshop != null && workshop.cells != null && workshop.cells.Count > 0
                && Find.CameraDriver != null)
            {
                Find.CameraDriver.JumpToCurrentMapLoc(workshop.cells[workshop.cells.Count / 2]);
            }
        }

        private static void JumpToBench(Map map)
        {
            Building bench = FindBenchInWorkshop(map);
            if (bench != null && Find.CameraDriver != null)
            {
                Find.CameraDriver.JumpToCurrentMapLoc(bench.Position);
                return;
            }
            JumpToWorkshop(map);
        }

        /// <summary>
        /// 车间内**第一个工作台**的屏幕方框（S5 的高亮目标 —— 比整区包围盒更精确：
        /// 玩家这一步要点的就是它）。
        /// </summary>
        private static bool TryGetBenchScreenRect(out Rect rect)
        {
            rect = default(Rect);
            Building bench = FindBenchInWorkshop(Find.CurrentMap);
            if (bench == null)
            {
                return false;
            }
            Vector2 p = UI.MapToUIPosition(bench.Position.ToVector3Shifted());
            const float half = 42f;
            rect = new Rect(p.x - half, p.y - half, half * 2f, half * 2f);
            return true;
        }

        private static Building FindBenchInWorkshop(Map map)
        {
            if (map == null || map.listerBuildings == null)
            {
                return null;
            }
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] is IBillGiver
                    && WorkshopZoneUtility.WorkshopAt(buildings[i].Position, map) != null)
                {
                    return buildings[i];
                }
            }
            return null;
        }

        // ------------------------------------------------------------ 完成条件

        private static bool GateSatisfied(TourGate gate)
        {
            if (gate == TourGate.None)
            {
                return true;
            }
            Map map = Find.CurrentMap;
            switch (gate)
            {
                case TourGate.WorkshopExists:
                    return map != null && FindAnyWorkshop(map) != null;
                case TourGate.DevicesPlaced:
                    return CountBillGivers(map) > 0;
                case TourGate.BillsWithIngredients:
                    return CountIngredientBills(map) > 0;
                case TourGate.RegistryView:
                    return IsHostInRegistry();
                case TourGate.ManufacturingDetail:
                    return IsHostInManufacturingDetail();
                default:
                    return true;
            }
        }

        private static Zone_Workshop FindAnyWorkshop(Map map)
        {
            List<Zone> zones = map.zoneManager.AllZones;
            for (int i = 0; i < zones.Count; i++)
            {
                Zone_Workshop workshop = zones[i] as Zone_Workshop;
                if (workshop != null)
                {
                    return workshop;
                }
            }
            return null;
        }

        private static int CountBillGivers(Map map)
        {
            if (map == null || map.listerBuildings == null)
            {
                return 0;
            }
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            int count = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                // 只在**车间范围内**计数（守门 P2）：全地图判据会让"开局已有工作台"的存档
                // 一进引导就自动越步，玩家看不到"去建工作台"这一步。
                if (buildings[i] is IBillGiver
                    && WorkshopZoneUtility.WorkshopAt(buildings[i].Position, map) != null)
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountIngredientBills(Map map)
        {
            if (map == null || map.listerBuildings == null)
            {
                return 0;
            }
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            int count = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                IBillGiver giver = buildings[i] as IBillGiver;
                if (giver == null || WorkshopZoneUtility.WorkshopAt(buildings[i].Position, map) == null)
                {
                    continue;   // 同上：只认**车间内**的工单
                }
                BillStack stack = giver.BillStack;
                if (stack == null)
                {
                    continue;
                }
                for (int b = 0; b < stack.Count; b++)
                {
                    Bill bill = stack[b];
                    if (bill != null && bill.recipe != null && bill.recipe.ingredients != null
                        && bill.recipe.ingredients.Count > 0)
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// 总控中心是否显示注册表页 / 制造中心详情页（引导的"到了没"判据）。
        /// 用遍历窗口栈取只读探针，不做任何写入。
        /// </summary>
        private static MasterControlTabWindow FindHost()
        {
            IList<Window> windows = Find.WindowStack.Windows;
            if (windows == null)
            {
                return null;
            }
            for (int i = 0; i < windows.Count; i++)
            {
                MasterControlTabWindow host = windows[i] as MasterControlTabWindow;
                if (host != null)
                {
                    return host;
                }
            }
            return null;
        }

        private static bool IsHostInRegistry()
        {
            MasterControlTabWindow host = FindHost();
            return host != null && host.TourShowsRegistry;
        }

        private static bool IsHostInManufacturingDetail()
        {
            MasterControlTabWindow host = FindHost();
            return host != null && host.TourShowsManufacturingDetail;
        }

        // ------------------------------------------------------------ 工具

        private static float Ease01(long startTicks, long nowTicks, float durationMs)
        {
            if (durationMs <= 0f)
            {
                return 1f;
            }
            long elapsed = nowTicks - startTicks;
            if (elapsed <= 0L)
            {
                return 0f;
            }
            double ratio = elapsed / (double)(TimeSpan.TicksPerMillisecond * durationMs);
            return ratio >= 1.0 ? 1f : (float)ratio;
        }

        private float Seconds(long nowTicks)
        {
            double seconds = (nowTicks - tourStartTicks) / (double)TimeSpan.TicksPerSecond;
            return seconds <= 0d ? 0f : (float)seconds;
        }

        /// <summary>呼吸 alpha，与 RUIF <c>*Fmt</c> 的 PulseAlpha/FlashAlpha 同式（其未随 DLL 导出）。</summary>
        private static float Pulse(float seconds, float minAlpha)
        {
            return minAlpha + (1f - minAlpha) * (0.5f + 0.5f * Mathf.Sin(seconds * 4f));
        }
    }
}
