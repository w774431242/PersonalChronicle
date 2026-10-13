using System;
using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Automation;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    internal static partial class WorkshopLayoutCanvas
    {
        // PERF：设备卡绘制失败日志节流（5s）；避免热路径无界刷日志（CHG-MFG-AUDIT-001 · P-1）
        private static long lastDeviceCardDrawFailLogTicks;

        /// <summary>
        /// 车间内第 <paramref name="index"/> 台设备的**默认网格位**（2 列；以车间锚点为原点）。
        /// 用户实机要求：画布不画 zone 容器 ⇒ 设备必须**自带坐标**，网格位就是"未手动摆放"时的坐标。
        /// </summary>
        private static Vector2 DeviceGridPos(Vector2 zoneDesign, int index)
        {
            float colOffset = index % 2 == 0 ? -0.5f : 0.5f;
            return new Vector2(
                zoneDesign.x + colOffset * (DeviceCardWidth + DeviceGridGapX),
                zoneDesign.y + (index / 2) * (DeviceCardHeight + DeviceGridGapY));
        }

        // （原 `TryGetDetachedPos` 已删除：车间内设备卡循环改为**跳过**独立设备、由独立设备层单独绘制，
        //   该查询不再有调用点 —— 留着就会变成"两处都能画独立设备"的隐患。）

        /// <summary>
        /// 设备库拖放落点：更新 ghost 位置；在画布内松开 ⇒ 记为独立设备。
        ///
        /// <para>⚠ 2026-10-02 审计 P2：原实现直接 <c>SetDetached</c>，**不拍撤销快照**
        /// ⇒ 设备放置本身不可撤销；现在统一走 <see cref="PlaceDevice"/>（带快照 + 状态反馈）。
        /// 落点判定与坐标换算统一走 <see cref="DesignAt"/>，不再各写一份。</para>
        /// </summary>
        private static void HandleDeviceDrop(Rect rect, Vector2 center, float k,
            ref CanvasState state)
        {
            if (!deviceDrag.Active)
            {
                return;
            }
            Event current = Event.current;
            if (current == null)
            {
                return;
            }
            deviceDragMousePos = current.mousePosition;
            if (current.type != EventType.MouseUp)
            {
                return;
            }
            if (Mouse.IsOver(rect))
            {
                PlaceDevice(ref state, deviceDrag.StableId, DesignAt(current.mousePosition, center, k));
            }
            deviceDrag = default;
            current.Use();
        }

        /// <summary>
        /// 放弃进行中的设备库拖拽（**只清状态、不落点**）。
        /// </summary>
        /// <remarks>
        /// BUG-2026-0001 · E-008：查看态不响应设备库落点，但**必须**清掉
        /// <c>deviceDrag</c> —— 否则「编辑态拖到一半切到查看态」会把静态拖拽态永久卡在
        /// Active，下次回到编辑态一进画布就幽灵落点。
        /// </remarks>
        internal static void CancelDeviceDrag()
        {
            deviceDrag = default;
        }

        /// <summary>
        /// 独立设备层：画脱离车间的设备（可点击开详情、可拖动、可 × 收回）。
        ///
        /// <para><b>2026-09-13 与车间内设备卡统一</b>：原实现按「屏幕下限」放大
        /// （<c>minW = 132/k</c>、<c>minH = 50/k</c>）让独立卡**不随缩放变化** ——
        /// 于是同一台设备在两处有两套尺寸规则。现在两处都走 <see cref="DrawDeviceCard"/>
        /// 的同一套规则（几何 ×k + 文字按真实行高堆叠 + 行数容量降级）。</para>
        ///
        /// <para><b>2026-10-02 审计 P1-1/P1-4</b>：① 把命中登记（悬停卡 / 抓取偏移）回传给调用方，
        /// 供 <c>HandleDeviceCardDrag</c> 使用（不另写一份卡位数学）；② 命中矩形改**裁剪后**，
        /// 画布外不可见区域不再能被点/悬停/弹提示。</para>
        /// </summary>
        private static string DrawDetachedLayer(Rect rect, Vector2 center, float k,
            List<PaletteEntry> palette, bool interactive, ref bool hoverCard,
            ref string hoverDeviceId, ref Vector2 hoverDeviceDesign, ref Vector2 hoverDeviceGrab,
            out string removedDeviceId, Vector2 mouseScreen)
        {
            string clicked = null;
            removedDeviceId = null;
            for (int i = 0; i < DetachedList.Count; i++)
            {
                DetachedDevice item = DetachedList[i];
                // 档里残留的失效条目（设备已拆除 / 已不在任何车间）⇒ 跳过，不画幽灵卡
                //（SAVE-003：缺失目标降级为"该项不存在"，不阻止绘制与加载）。
                DeviceEntry entry = FindDevice(palette, item.StableId);
                if (string.IsNullOrEmpty(entry.StableId))
                {
                    continue;
                }
                Rect box = DeviceCardBox(center, k, item.Position);
                if (!box.Overlaps(rect))
                {
                    continue;
                }
                Rect hitBox = IntersectRect(box, rect);
                bool cardHover = hitBox.width > 1f && hitBox.height > 1f && Mouse.IsOver(hitBox);
                if (cardHover)
                {
                    hoverCard = true;
                    hoverDeviceId = item.StableId;
                    hoverDeviceDesign = item.Position;
                    hoverDeviceGrab = item.Position - DesignAt(mouseScreen, center, k);
                }
                if (DrawClippedDeviceCard(rect, box, entry, cardScaleOf(k), 0f, cardHover,
                        flash: false, showZoneBadge: true, interactive: interactive,
                        removeClicked: out bool removeHit))
                {
                    clicked = item.StableId;
                }
                if (removeHit)
                {
                    removedDeviceId = item.StableId;
                }
            }
            return clicked;
        }

        /// <summary>
        /// 设备卡 —— 预览 `.machine` 的 1:1 落地 + **分档信息密度**（卡越大越详细）。
        ///
        /// <para><b>行池（行序 = 信息优先级，行数 N 由卡片可用高度反推）</b>：</para>
        /// <list type="number">
        /// <item>名称（<b>最小档 N=1 只有它</b>）—— 状态由灯色 + 描边表达，不占行；</item>
        /// <item><b>人员</b>（原版指定生产人员 + PC 当班岗位）—— <b>行 2</b>：默认缩放只有 2~3 行可见，
        /// 而"谁来做这台"是玩家最关心的信息；放到第 5 行等于"要放大 2 倍才看得到"（实机反馈"没变化"）；</item>
        /// <item><c>工单 · 功率</c>（唯一无法从灯色读出的核心数据）；</item>
        /// <item>状态文案（灯色画；灯色已表达状态，属可读性冗余）；</item>
        /// <item>作业者（此刻正在做这台活的 pawn —— 与行 2 的"被指定的人"是两回事）；</item>
        /// <item><b>技能</b>（设备对应技能 + 使用者等级）—— 行 6；</item>
        /// <item><b>生产</b>（当前工单配方 × 目标数）—— 行 7。</item>
        /// </list>
        /// <para>行数由 <b>可用高度反推</b>：`N = 1 + floor((h − 2·PAD − 进度条) − 名称行) / 行高`，
        /// 上限 <see cref="MaxDeviceCardRows"/>；配合 <see cref="MaxDeviceCardScale"/> = 2.5
        /// （260×150 时七行全解锁，填充 ≈94%）。</para>
        ///
        /// <para><b>两条硬规则（LAND-007/008，实机两轮缺陷的根因修复）</b>：
        /// ① <b>几何按设计单位 × k</b>（卡片尺寸 / 灯位 / 内缩 / 进度条全部取自
        /// <see cref="WorkshopFloorLayout"/> 的预览真源常量）；
        /// ② <b>文字按屏幕真实行高堆叠</b> —— 引擎字体是固定像素、不随 k 缩放，
        /// 所以行位/行距**绝不能用设计坐标乘 k**。此前两处都写成绝对像素魔数
        /// （`pad=3`、名称行 14、页脚 <c>yMax−13</c>）⇒ 高倍放大时卡内约 70% 空白、页脚贴边；
        /// 缩小时名称照样溢出（守卫只把页脚藏掉）。</para>
        ///
        /// <para><b>降级阶梯（D1/D2/D6）</b>：放不下的行**不画**（完整信息进悬停）；
        /// 连名称都放不下 ⇒ 只留几何与状态灯。名称字体档随几何自适应（卡宽 ≥ 132px ⇒ Small）。</para>
        ///
        /// <para><b>如实降级（B 类：引擎无对应能力，不冒充）</b>：预览的圆角 <c>rx:7</c>、投影 <c>.msh</c>、
        /// 选中发光 <c>drop-shadow</c> 均不画。设备图与 T1/T2/T3 徽标只读取设备声明；
        /// 未声明的普通设备沿用通用卡，不推断等级或填入样例值。</para>
        /// </summary>
        private static void DrawDeviceCard(Rect rect, DeviceEntry device, float k,
            bool showZoneBadge, float time, bool hover)
        {
            if (rect.width < 10f || rect.height < 6f)
            {
                return;
            }
            float safeK = Mathf.Max(0.01f, k);
            Color lamp = LampColorOf(device.StateKey);

            // ---- 层 1：容器（预览 `.machine .body` = **实色**钢底；悬停转琥珀 = `.machine:hover .body`）----
            // 实色而非半透明：半透明会让"画布外/下层内容"透进来（实机截图里卡片被顶栏文字穿透）。
            Widgets.DrawBoxSolid(rect, UITheme.Card);
            UIComponents.Border(rect, hover
                ? UITheme.Accent
                : UITheme.WithAlpha(lamp, 0.65f));

            // ---- 层 2：顶部高光条（预览 `.mgloss`：x+2 / y+1.5 / w−4 / h1.6）----
            Widgets.DrawBoxSolid(
                new Rect(rect.x + 2f * safeK, rect.y + 1.5f * safeK,
                    Mathf.Max(1f, rect.width - 4f * safeK), Mathf.Max(1f, 1.6f * safeK)),
                UITheme.WithAlpha(lamp, 0.30f));

            // ---- 层 3：状态灯（预览 cx=x+12 / cy=y+13 / r=4.5；屏幕下限 4px，防缩没）----
            float lampSize = Mathf.Max(4f, WorkshopFloorLayout.LampRadius * 2f * safeK);
            Rect lampRect = new Rect(
                rect.x + WorkshopFloorLayout.LampOffsetX * safeK - lampSize * 0.5f,
                rect.y + WorkshopFloorLayout.LampOffsetY * safeK - lampSize * 0.5f,
                lampSize, lampSize);
            // 改造 P3：状态灯走 RUIF `StatusAtoms.PulseDot`（脉冲呼吸）；RUIF 未装/异常 ⇒ 回落实心方块。
            if (!RuifBridge.TryPulseDot(lampRect, lamp, time))
            {
                Widgets.DrawBoxSolid(lampRect, lamp);
            }

            float artWidth = ProductionDeviceCardArt.Draw(rect, device.Card, lampRect.yMax - rect.y + 3f);

            // 屏幕像素常量（**文字空间**，不乘 k）：3px 内边距
            const float textPad = 3f;

            // ---- 车间角标（右上角；**仅独立设备** —— 在车间卡内的设备本来就在卡里）----
            // 用户要求："车间建议标注到设备角标"。规则：角标 ≤34% 宽，且必须给名称留 ≥52px，否则不画（名称优先）。
            float nameRight = rect.xMax - textPad;
            if (showZoneBadge && !string.IsNullOrEmpty(device.ZoneLabel) && rect.width >= 108f)
            {
                float badgeWidth = Mathf.Min(Measure(device.ZoneLabel) + 6f, rect.width * 0.34f);
                if (rect.width - badgeWidth - 16f >= 52f)
                {
                    Rect badge = new Rect(rect.xMax - textPad - badgeWidth, rect.y + 1f, badgeWidth, 12f);
                    Widgets.DrawBoxSolid(badge, UITheme.WithAlpha(UITheme.Info, 0.18f));
                    UIComponents.Border(badge, UITheme.Info);
                    UIComponents.Label(badge,
                        UIComponents.TruncateToWidth(device.ZoneLabel, badgeWidth - 4f, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Info, TextAnchor.MiddleCenter);
                    nameRight = badge.x - 3f;
                }
            }

            // ---- 进度条高度先算（文字行数要扣掉它）——预览 `.pbar`：x+10 / y=MH−5 / w=MW−20 / h3.5 ----
            bool hasProgress = device.Progress01 >= 0f;
            float progressH = hasProgress ? Mathf.Max(2f, WorkshopFloorLayout.ProgressH * safeK) : 0f;
            float progressRow = hasProgress ? progressH + textPad : 0f;

            // ---- 文字：从盒顶按**真实行高**逐行堆叠；行数由盒高反推，放不下的行不画 ----
            GameFont nameFont = WorkshopFloorLayout.MachineW * safeK >= 132f
                ? GameFont.Small
                : GameFont.Tiny;
            float tinyLine = Text.LineHeightOf(GameFont.Tiny);
            float nameLine = Mathf.Max(tinyLine, Text.LineHeightOf(nameFont));
            float available = rect.height - 2f * textPad - progressRow;
            if (available < nameLine)
            {
                // 连名称都放不下 ⇒ **只留几何与灯色**（预览在极小缩放下同样退化为纯色块）；
                // 悬停提示与命中检测由 DrawClippedDeviceCard 在裁剪外统一做。
                return;
            }
            int rows = 1;
            if (available >= nameLine + tinyLine)
            {
                rows = Mathf.Min(MaxDeviceCardRows,
                    1 + Mathf.FloorToInt((available - nameLine) / tinyLine));
            }

            // 文字左边界 = 设计内缩（让开灯）× k，但**不得压住灯**、也不得挤没宽度
            float textLeft = Mathf.Max(rect.x + WorkshopFloorLayout.TextOffsetX * safeK,
                lampRect.xMax + 3f);
            textLeft = Mathf.Max(textLeft, rect.x + artWidth);
            textLeft = Mathf.Min(textLeft, rect.xMax - 8f);
            float lineWidth = Mathf.Max(8f, rect.xMax - textLeft - textPad);
            float y = rect.y + textPad;

            // 行 1：名称（宽度让到角标 —— 角标不吃名称）
            float nameWidth = Mathf.Max(8f, nameRight - textLeft);
            UIComponents.Label(new Rect(textLeft, y, nameWidth, nameLine),
                UIComponents.TruncateToWidth(device.Label, nameWidth, nameFont),
                nameFont, UITheme.Text, TextAnchor.MiddleLeft);
            y += nameLine;

            // ---- 后续信息行：按高度顺序填充；智能设备额外显示速度，空内容不占位。----
            // ⚠️ 用「已画行数游标」而不是「第几行」守卫：否则某行为空（如无人员信息）时，
            //    后面的行会因为 `rows >= 3` 不满足而整块消失 —— 2 行档只剩名称（实机踩过）。
            int drawn = 1;
            // 智能设备优先显示真实速度；其他行按卡片高度降级，全部内容保留于悬停。
            if (drawn < rows && device.Card != null)
            {
                UIComponents.Label(new Rect(textLeft, y, lineWidth, tinyLine),
                    UIComponents.TruncateToWidth(device.Card.SpeedText, lineWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Info, TextAnchor.MiddleLeft);
                y += tinyLine;
                drawn++;
            }
            // 行 2：人员（原版指定生产人员 + PC 当班岗位）—— 见本方法文档的行池说明
            if (drawn < rows && !string.IsNullOrEmpty(device.CrewText))
            {
                UIComponents.Label(new Rect(textLeft, y, lineWidth, tinyLine),
                    UIComponents.TruncateToWidth(device.CrewText, lineWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                y += tinyLine;
                drawn++;
            }
            // 行 3：工单 · 功率（唯一无法从灯色读出的数据）
            if (drawn < rows && !string.IsNullOrEmpty(device.SubLine))
            {
                UIComponents.Label(new Rect(textLeft, y, lineWidth, tinyLine),
                    UIComponents.TruncateToWidth(device.SubLine, lineWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                y += tinyLine;
                drawn++;
            }
            // 行 4：状态文案（用灯色画；灯色已表达状态，此行属"写到字面上"的可读性冗余）
            if (drawn < rows && !string.IsNullOrEmpty(device.StateText))
            {
                UIComponents.Label(new Rect(textLeft, y, lineWidth, tinyLine),
                    UIComponents.TruncateToWidth(device.StateText, lineWidth, GameFont.Tiny),
                    GameFont.Tiny, lamp, TextAnchor.MiddleLeft);
                y += tinyLine;
                drawn++;
            }
            // 行 5：作业者（预览 `.op`；"此刻正在做的人"，与行 2 的"被指定的人"是两回事）
            if (drawn < rows && !string.IsNullOrEmpty(device.OperatorText))
            {
                UIComponents.Label(new Rect(textLeft, y, lineWidth, tinyLine),
                    UIComponents.TruncateToWidth(device.OperatorText, lineWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                y += tinyLine;
                drawn++;
            }
            // 行 6：技能（设备对应技能 + 使用者等级；口径与上岗评分同一 SkillOf）
            if (drawn < rows && !string.IsNullOrEmpty(device.SkillText))
            {
                UIComponents.Label(new Rect(textLeft, y, lineWidth, tinyLine),
                    UIComponents.TruncateToWidth(device.SkillText, lineWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                y += tinyLine;
                drawn++;
            }
            // 行 7：生产（当前工单配方 × 目标数）
            if (drawn < rows && !string.IsNullOrEmpty(device.RecipeText))
            {
                UIComponents.Label(new Rect(textLeft, y, lineWidth, tinyLine),
                    UIComponents.TruncateToWidth(device.RecipeText, lineWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            }

            // ---- 进度条（预览 `.pbar` + `.pfill`）----
            if (hasProgress)
            {
                float inset = Mathf.Max(2f, WorkshopFloorLayout.ProgressInsetX * safeK);
                float barWidth = rect.width - inset * 2f;
                if (barWidth >= 6f)
                {
                    Rect track = new Rect(rect.x + inset, rect.yMax - textPad - progressH,
                        barWidth, progressH);
                    Widgets.DrawBoxSolid(track, UITheme.BorderHair);
                    Widgets.DrawBoxSolid(new Rect(track.x, track.y,
                        track.width * Mathf.Clamp01(device.Progress01), track.height), UITheme.Alive);
                    UIComponents.Border(track, UITheme.BorderSoft);
                }
            }

        }

        /// <summary>
        /// 画一张设备卡（**带视口裁剪**）并返回是否被点击 —— 车间内卡片与独立设备层的**唯一入口**。
        ///
        /// <para><b>⛔ 为什么必须裁剪（2026-09-13 实机）</b>：IMGUI 没有自动裁剪 ⇒ 卡片被平移到画布外时
        /// 会**直接画在画布外的其它 UI 上**（实机截图：卡片压住顶栏与 `#cmdbar`）。</para>
        ///
        /// <para><b>坐标系</b>：`GUI.BeginClip` 把 (0,0) 移到裁剪框左上角 ⇒ 卡内绘制要减去
        /// <paramref name="viewport"/> 原点；而 <c>Mouse.IsOver</c> / <c>Widgets.ButtonInvisible</c> 按**当前坐标系**
        /// 比较命中 ⇒ <b>悬停与点击都在裁剪外用绝对矩形做</b>。</para>
        ///
        /// <para>⚠ 2026-10-02 审计 P1-4：命中矩形必须取 <c>box ∩ viewport</c>。
        /// 原实现只用 <c>Overlaps</c> 过滤**绘制**，却把**未裁剪**的 <c>box</c> 交给
        /// 悬停/点击/tooltip ⇒ 画布外那部分完全不可见，却仍能被点中（还会误弹设备详情）。</para>
        ///
        /// <para><b>裁剪安全前提</b>：卡内只用 <c>DrawBoxSolid</c> / <c>GUI.DrawTexture</c> 系
        /// （<c>UIComponents.Border</c> 走 <c>DrawLineHorizontal/Vertical</c> = <c>GUI.DrawTexture</c>，感知 matrix）；
        /// **禁用两点版 <c>Widgets.DrawLine</c>**（走 <c>Matrix4x4.TRS</c> + GL，不感知 matrix，clip 内必错位）。</para>
        /// </summary>
        /// <param name="interactive">true = 编排态：画单卡「× 收回」。</param>
        /// <param name="removeClicked">本帧点中了该卡的「× 收回」（显式用户动作 ⇒ 允许删存档项）。</param>
        private static bool DrawClippedDeviceCard(Rect viewport, Rect box, DeviceEntry device,
            float k, float time, bool hover, bool flash, bool showZoneBadge, bool interactive,
            out bool removeClicked)
        {
            removeClicked = false;
            if (box.width < 2f || box.height < 2f || string.IsNullOrEmpty(device.StableId))
            {
                return false;
            }
            Rect local = new Rect(box.x - viewport.x, box.y - viewport.y, box.width, box.height);
            // ⭐ 命中矩形 = 卡 ∩ 视口（画布外不可见部分不参与命中）。
            Rect hitBox = IntersectRect(box, viewport);
            if (hitBox.width < 2f || hitBox.height < 2f)
            {
                return false;
            }
            // ⚠ 「× 收回」按钮的**命中**必须在裁剪**外**做：`Mouse.IsOver` / `ButtonInvisible`
            //   按当前坐标系比较，而 `GUI.BeginClip` 会把绘制坐标系平移到裁剪框左上角。
            //   画在裁剪内、测在裁剪外，两者用同一个绝对矩形换算（见 RemoveButtonBox）。
            Rect removeBox = RemoveButtonBox(hitBox);
            GUI.BeginClip(viewport);
            try
            {
                DrawDeviceCard(local, device, k, showZoneBadge, time, hover);
                // 单卡「× 收回」（仅编排态）：显式用户动作，是删除存档项的合法入口之一。
                if (interactive && removeBox.width > 1f)
                {
                    DrawCardRemoveButton(new Rect(removeBox.x - viewport.x, removeBox.y - viewport.y,
                        removeBox.width, removeBox.height));
                }
                // 定位闪烁也画在裁剪内（`TryFlashOverlay` 走 RUIF `PanelAtoms.FlashOverlay`，
                // 内部只用 `Draw.Border` = `GUI.DrawTexture` 系 ⇒ 裁剪安全；回落的 Border 同）
                if (flash)
                {
                    float phase = (float)(DateTime.UtcNow.Ticks % TimeSpan.TicksPerSecond)
                        / TimeSpan.TicksPerSecond;
                    if (!RuifBridge.TryFlashOverlay(local, phase, UITheme.Accent, 2f))
                    {
                        UIComponents.Border(local, UITheme.Accent);
                    }
                }
            }
            catch (Exception ex)
            {
                // 逐件隔离：一处坏不能把整局 GUI 拖下水（EndClip 在 finally 里保证配对）
                // PERF：每帧每台卡的异常日志做 5 秒节流，避免热路径无界刷日志（CHG-MFG-AUDIT-001 · P-1）
                long now = DateTime.UtcNow.Ticks;
                if (now - lastDeviceCardDrawFailLogTicks > TimeSpan.TicksPerSecond * 5L)
                {
                    lastDeviceCardDrawFailLogTicks = now;
                    ChronicleLog.Error(ChronicleLog.Category.Ui, "device card draw failed: " + ex);
                }
            }
            finally
            {
                GUI.EndClip();
            }
            if (!string.IsNullOrEmpty(device.Tooltip))
            {
                TooltipHandler.TipRegion(hitBox, device.Tooltip);
            }
            // 命中检测统一在裁剪外：卡体点击（开详情）+ 「× 收回」（删该卡）。
            // 「×」优先 —— 它是更小的子区域，先判才不会点到卡体弹详情窗。
            if (removeBox.width > 1f && Mouse.IsOver(removeBox) && Widgets.ButtonInvisible(removeBox))
            {
                removeClicked = true;
                return false;
            }
            // ⚠ BUG-2026-0001 · S-1（修复）：**编排态不消费卡体按下**。
            //   `Widgets.ButtonInvisible` 在按下时调 `Event.Use()`，而本方法由绘制循环
            //   （`DrawCanvasBody` 内 `DrawClippedDeviceCard`）在**交互处理之前**调用
            //   ⇒ 事件到达 `HandleDeviceCardDrag` 时已被标记 Used，拖拽起点永不成立，
            //   卡片「怎么拖都不动」。2026-10-02 审计已修过同一缺陷的 MouseUp 变体
            //   （把 `HandleDeviceDrop` 前置），但 MouseDown 变体漏了。
            //   编排态改由 `HandleDeviceCardDrag` 独占按下：移动 = 拖拽，未移动 = 点击开详情。
            //   查看态保持原样（点击开详情，无拖拽语义）。
            if (interactive)
            {
                return false;
            }
            return Widgets.ButtonInvisible(hitBox);
        }

        /// <summary>
        /// 「× 收回」按钮的**绝对**矩形（基于裁剪后的命中框左上角）。
        /// 车间角标在右上角 ⇒ 收回按钮放左上角，两者不打架。
        /// 卡片太小放不下时返回零宽（不画也不测，避免遮挡唯一的状态灯）。
        /// </summary>
        private static Rect RemoveButtonBox(Rect hitBox)
        {
            const float size = 11f;
            if (hitBox.width < 34f || hitBox.height < 18f)
            {
                return new Rect(hitBox.x, hitBox.y, 0f, 0f);
            }
            return new Rect(hitBox.x + 1f, hitBox.y + 1f, size, size);
        }

        /// <summary>画「× 收回」小按钮（裁剪内绘制；命中在裁剪外由 <see cref="RemoveButtonBox"/> 负责）。</summary>
        private static void DrawCardRemoveButton(Rect box)
        {
            Widgets.DrawBoxSolid(box, UITheme.WithAlpha(UITheme.Dead, 0.30f));
            UIComponents.Border(box, UITheme.Dead);
            UIComponents.Label(box, "×", GameFont.Tiny, UITheme.Dead, TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// 工序链路（预览 `.route` 实线 + `.route-flow` 流动虚线）：按**已放置顺序**连接车间中心。
        /// 引擎无 CSS 动画 ⇒ 只画实线 + 末端箭头（如实降级，不冒充"流动"）。
        /// </summary>
        private static void DrawLayoutChain(Vector2 center, float k, in CanvasState state)
        {
            if (state.PlacedZoneIds.Count < 2)
            {
                return;
            }
            Color line = UITheme.WithAlpha(UITheme.Info, 0.5f);
            for (int i = 0; i < state.PlacedZoneIds.Count - 1; i++)
            {
                Vector2 a = state.PlacedPos[i];
                Vector2 b = state.PlacedPos[i + 1];
                Vector2 sa = new Vector2(center.x + a.x * k, center.y + a.y * k);
                Vector2 sb = new Vector2(center.x + b.x * k, center.y + b.y * k);
                if ((sb - sa).sqrMagnitude < 64f)
                {
                    continue;
                }
                DrawPolyline(sa, sb, line, 2f);
                Vector2 dir = (sb - sa).normalized;
                Vector2 tip = sb - dir * (BlockWidth * k * 0.5f);
                if ((tip - sa).sqrMagnitude > 64f)
                {
                    DrawPolyline(tip - dir * 8f, tip, UITheme.Info, 2.5f);
                }
            }
        }

        /// <summary>
        /// **裁剪安全的折线**：沿线用小方块串联。
        ///
        /// <para>⛔ 不能用两点版 <c>Widgets.DrawLine(a, b, …)</c>（反编译取证 2026-09-13）：它用
        /// <c>Matrix4x4.TRS</c> + GL 画旋转矩形，**不感知 <c>GUI.matrix</c>** ⇒ 放进
        /// <c>GUI.BeginClip</c> 内会整体错位（偏移 = 裁剪框原点）。而本工程的画布现在**必须裁剪**
        /// （见 <see cref="DrawClippedDeviceCard"/>），所以线段一律走这个 CPU 串联实现。</para>
        /// </summary>
        private static void DrawPolyline(Vector2 a, Vector2 b, Color color, float thickness)
        {
            float length = Vector2.Distance(a, b);
            if (length < 1f)
            {
                return;
            }
            float half = Mathf.Max(1f, thickness * 0.5f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(1f, half)));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                Widgets.DrawBoxSolid(new Rect(p.x - half, p.y - half, half * 2f, half * 2f), color);
            }
        }

        // ---- 几何指标 + 工具条（M2）----

        /// <summary>
        /// 指标 / 工具条行（撤销 · 叠加标注 · 坐标网格 · Save 结构预览）。
        /// <c>internal</c>：供 ② 外壳 `#lbar` 直接复用（编排态操作条）。
        /// </summary>
        internal static void DrawMetricsRow(Rect rect, List<PaletteEntry> palette, ref CanvasState state)
        {
            UIComponents.Rule(new Rect(rect.x, rect.y, rect.width, 1f), UITheme.BorderSoft);

            // 左：真实计数 + 最近操作
            int total = palette != null ? palette.Count : 0;
            string left = Translate2("PersonalChronicle.UI.Layout.Placed", state.PlacedZoneIds.Count, total);
            if (DetachedList.Count > 0)
            {
                // 设备卡是**独立持久化容器**（mfgDeviceLayout），不计入车间 entries
                // —— 但必须显示，否则玩家会以为设备卡没存（P0-4）。
                left += " · " + Translate1("PersonalChronicle.UI.Layout.DevicesPlaced",
                    DetachedList.Count);
            }
            if (!string.IsNullOrEmpty(state.LastAction))
            {
                left += " · " + state.LastAction;
            }
            UIComponents.Label(new Rect(rect.x, rect.y + 2f, rect.width * 0.46f, 16f), left,
                GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);

            // 右：撤销 / 坐标网格 / 叠加标注 / Save 预览（预览的 UndoToast + DensitySwitch + Save 结构预览）
            const float gap = 4f;
            float x = rect.xMax;
            string save = Translate2("PersonalChronicle.UI.Layout.SavePreview",
                state.PlacedZoneIds.Count, LayoutSchemaVersion);
            float saveWidth = Mathf.Min(Measure(save) + 14f, rect.width * 0.3f);
            x -= saveWidth;
            Rect saveRect = new Rect(x, rect.y + 2f, saveWidth, 16f);
            UIComponents.Label(saveRect, UIComponents.TruncateToWidth(save, saveWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Info, TextAnchor.MiddleRight);
            TooltipHandler.TipRegion(saveRect, BuildSavePreview(state, palette));
            x -= gap;

            // 注意：`state` 是 `ref` 参数，**不能**在 lambda 里捕获（CS1628）⇒ 开关用返回值驱动
            if (DrawToggle(x, rect.y + 2f, ref x, gap,
                Translate("PersonalChronicle.UI.Layout.ToggleAnnotations"), state.ShowAnnotations))
            {
                state.ShowAnnotations = !state.ShowAnnotations;
            }
            if (DrawToggle(x, rect.y + 2f, ref x, gap,
                Translate("PersonalChronicle.UI.Layout.ToggleGrid"), state.ShowGrid))
            {
                state.ShowGrid = !state.ShowGrid;
            }

            string undo = Translate("PersonalChronicle.UI.Layout.Undo");
            float undoWidth = Measure(undo) + 16f;
            x -= undoWidth;
            Rect undoRect = new Rect(x, rect.y + 2f, undoWidth, 16f);
            // ⚠ 2026-10-02 审计 P0-3：门禁统一为 `CanUndo`（= 真的持有一份快照）。
            //   旧条件 `UndoOrder.Count > 0 || PlacedOrder.Count > 0` 在"读档后从未拍快照"时为真
            //   ⇒ 点一次撤销即清空全部布局并删档，且此后两个门禁同时关闭、无法回滚。
            if (CanUndo(state))
            {
                if (Widgets.ButtonText(undoRect, undo))
                {
                    UndoLast(ref state);
                }
            }
            else
            {
                UIComponents.Label(undoRect, undo, GameFont.Tiny, UITheme.Muted,
                    TextAnchor.MiddleCenter);
                TooltipHandler.TipRegion(undoRect,
                    Translate("PersonalChronicle.UI.Layout.NothingToUndo"));
            }

            // 中部：指标缺口如实（需「工序序位」数据源）
            float midX = rect.x + rect.width * 0.46f;
            float midWidth = Mathf.Max(0f, x - 6f - midX);
            if (midWidth > 40f)
            {
                UIComponents.Label(new Rect(midX, rect.y + 2f, midWidth, 16f),
                    UIComponents.TruncateToWidth(
                        Translate("PersonalChronicle.UI.Layout.MetricsUnavailable"), midWidth,
                        GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>右对齐的小开关按钮：返回 **是否被点击**，由调用方翻转状态（避免 ref 捕获）。</summary>
    }
}
