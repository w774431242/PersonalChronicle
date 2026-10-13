using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间管理 · 大型弹窗（860×560，模态暂停）。
    ///
    /// 布局：标题栏（车间名 + 设备数徽章 + 三项统计副标题）+ 共享内容层
    /// <see cref="WorkshopManageView"/>（概览 / 设备表 / 配方 / 设置 + 底部操作条）。
    /// 布局实现见 <c>WorkshopManageView</c>，本类只负责窗口壳与标题栏。
    ///
    /// <para>⚠ CHG-20261005-WSDOCFIX：原文写"与 <c>ITab_WorkshopManage</c> 共用" ——
    /// 该类全工程无定义（残留 cref 引用，已改为纯文本）。</para>
    ///
    /// 数据纪律：标题栏读 <see cref="WorkshopManageView.Stats"/> 快照，不自行扫描；
    /// 车间在开窗期间被删除时由 view 的「车间已被删除」空态兜底。
    ///
    /// 绘制纪律：颜色/字体全走 <see cref="UITheme"/> + <see cref="UIComponents"/>，
    /// 禁止窗口内 new Color。
    /// </summary>
    public class Dialog_WorkshopManage : Window
    {
        /// <summary>窗口尺寸下限（= 原固定尺寸；小屏 / 高 UIScale 下不再缩小）。</summary>
        private const float WinW = 1080f;
        private const float WinH = 760f;

        /// <summary>窗口尺寸上限（CHG-20261005-WINUX：放大封顶，避免 4K 上拉伸到难以扫读）。</summary>
        private const float MaxW = 1600f;
        private const float MaxH = 1040f;
        private const float PadX = 18f;
        private const float HeaderH = 58f;
        private const float TitleH = 30f;

        private const string KeyPrefix = "PersonalChronicle.Automation.Tab.";

        private readonly Zone_Workshop zone;
        private readonly WorkshopManageView view;

        /// <summary>本窗口所属车间。供开窗去重比对（同一车间只保留一个面板）。</summary>
        public Zone_Workshop OwnerZone => zone;

        /// <summary>
        /// 窗口尺寸（CHG-20261005-WINUX，2026-10-05 玩家反馈"出现滚动"）：
        /// 按**屏幕可用区域**自适应放大 —— 原固定 1080×760 在 1080p 下只占约一半，
        /// 概览页新增大组件区后必然出现滚动。
        ///
        /// <para>⚠ <b>只放大、不放开手动缩放</b>：2026-10-02 审计 P1-H 已判定"缩窄会让
        /// 卡片网格 / 成员行 / chip 的固定宽块进入负宽区"（三列重叠），因此
        /// <c>resizeable</c> 保持 <c>false</c>；放大方向不存在该风险。</para>
        /// <para>留出屏幕边缘呼吸：宽 -80 / 高 -120（UI 空间，<c>UI.screenWidth</c> 已含 UIScale）。</para>
        /// </summary>
        public override Vector2 InitialSize
        {
            get
            {
                float w = Mathf.Clamp(UI.screenWidth - 80f, WinW, MaxW);
                float h = Mathf.Clamp(UI.screenHeight - 120f, WinH, MaxH);
                return new Vector2(w, h);
            }
        }

        /// <param name="zone">目标车间。布局与刷新都基于活引用（空态有兜底），
        /// 这样底部「刷新」才能反映玩家在开窗期间放下的新设备。</param>
        public Dialog_WorkshopManage(Zone_Workshop zone)
        {
            this.zone = zone;
            view = new WorkshopManageView(zone, () => Close());
            view.Refresh();

            doCloseX = true;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = true;
            // 2026-10-02 审计修复（P1-H）：禁用缩放 —— 卡片网格 / 成员行 / chip 内部存在固定宽块
            //（AvatarSize、MemberRightW、chip 文本间隙等），窗口缩窄会让算术进入负宽区，
            // 表现为三列重叠、chip 文字互压（截断防溢出但防不了负宽）。同模块其它三个对话框均已禁缩放。
            resizeable = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // 车间在开窗期间被删除 → 立即关窗，避免持续展示陈旧快照。
            if (!view.IsZoneAlive())
            {
                Close();
                return;
            }

            // 原生 Window 默认浅色背景——覆盖为 UITheme.Window 深色，统一主题。
            UIComponents.TintedBox(inRect, UITheme.Window);

            DrawHeader(inRect);

            Rect body = new Rect(
                inRect.x + PadX,
                inRect.y + HeaderH + UITheme.SpaceSm,
                inRect.width - PadX * 2f,
                inRect.height - HeaderH - UITheme.SpaceSm);
            view.Draw(body);
        }

        /// <summary>标题栏：accent 锚点竖条 + 车间名（左）+ 设备数徽章（右）
        /// + 三项统计副标题 + 分隔线。竖条与侧栏选中态同款 accent，建立视觉呼应。</summary>
        private void DrawHeader(Rect inRect)
        {
            Rect header = new Rect(inRect.x + PadX, inRect.y + UITheme.SpaceMd,
                inRect.width - PadX * 2f, HeaderH);

            UIComponents.TintedBox(new Rect(header.x, header.y + 3f, 4f, TitleH - 6f), UITheme.Accent);
            float textX = header.x + 14f;

            string label = zone != null ? zone.RenamableLabel : "--";
            UIComponents.Label(new Rect(textX, header.y, header.width - 130f, TitleH),
                label, UITheme.FontValue, UITheme.Text);

            DrawDeviceBadge(header);

            string sub = Subtitle();
            UIComponents.Label(new Rect(textX, header.y + TitleH, header.width - 130f, 18f),
                sub, UITheme.FontLabel, UITheme.Muted);

            UIComponents.Rule(new Rect(header.x, header.yMax, header.width, 1f), UITheme.BorderSoft);
        }

        private void DrawDeviceBadge(Rect header)
        {
            int count = view.Stats != null ? view.Stats.DeviceCount : 0;
            string badgeText = KeyPrefix + "HeaderDevices";
            Rect badge = new Rect(header.xMax - 110f, header.y + 4f, 110f, 22f);
            UIComponents.Pill(badge, badgeText.Translate(count).ToString(),
                count > 0 ? UITheme.Alive : UITheme.Muted);
        }

        private string Subtitle()
        {
            WorkshopStats s = view.Stats;
            if (s == null)
            {
                return "";
            }
            return (KeyPrefix + "HeaderSub").Translate(
                s.CellCount, s.FreeCells, s.DeviceCount).ToString();
        }
    }
}
