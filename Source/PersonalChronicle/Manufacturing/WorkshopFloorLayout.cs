using PersonalChronicle.Application.ReadModels;
using UnityEngine;

namespace PersonalChronicle.Manufacturing
{
    /// <summary>
    /// 车间俯视框架的**设计坐标系与全部几何常量** —— 逐行取自预览
    /// <c>系统中心蓝图/制造系统中心预览_RUIF单页统一版.html</c> 的 <c>buildFloor()</c>。
    ///
    /// <para><b>2026-09-10 二次实测（LAND-001）</b>：预览已演进为 <b>viewBox 0 0 1500 600</b>
    /// （原 1500×780），机箱间距、车间格高、通道、支持区、图例坐标全部随之改变；
    /// 本文件的全部数值均为**按最新预览重测**后的结果，不要再引用 780 时代的旧值。</para>
    ///
    /// <para>设计坐标系 = SVG <c>viewBox 0 0 1500 600</c>；绘制时统一乘 <c>k = canvasWidth / 1500</c>。</para>
    /// </summary>
    internal static class WorkshopFloorLayout
    {
        // ---- 画布 ----
        internal const float CanvasW = 1500f;
        internal const float CanvasH = 600f;

        // ---- L1 厂房外壳（buildFloor: BX/BY/BW/BH；wall-in 内缩 7）----
        internal const float Bx = 150f;
        internal const float By = 22f;
        internal const float Bw = 1200f;
        internal const float Bh = 556f;
        internal const float WallThickness = 6f;
        internal const float SlabInset = 7f;

        // ---- L2 柱网（colX/colY；柱 10×10；轴线内缩 10）----
        internal static readonly float[] ColX = { 165f, 461f, 764f, 1067f, 1335f };
        internal static readonly float[] ColY = { 78f, 218f, 352f, 492f };
        internal const float ColumnSize = 10f;
        internal const float GridlineInset = 10f;

        // ---- L3 母线管廊（主横 y=26，竖支线 26→34）----
        internal const float BusbarY = 26f;
        internal const float BusbarStubBottom = 34f;
        internal const float BusbarInset = 14f;
        internal static readonly float[] BusbarStubX = { 461f, 764f, 1067f };

        // ---- L4 通道（次通道 y=215/350 高 10；主通道 y=492 高 44）----
        internal const float AisleBandX = 170f;
        internal const float AisleBandW = 1170f;
        internal const float AisleBandH = 10f;
        internal static readonly float[] AisleBandY = { 215f, 350f };
        /// <summary>次通道标注的文字基线 y（绝对设计坐标，贴在带下沿）。</summary>
        internal static readonly float[] AisleBandTextY = { 224f, 359f };
        internal const float MainAisleY = 492f;
        internal const float MainAisleH = 44f;
        internal const float MainAisleTextY = 532f;
        internal const float AisleLabelRightX = 1352f;
        internal const float AisleLabelLeftX = 180f;

        // ---- L5 支持功能区（4 个 260×48，y=30；英文 y=46 / 中文 y=66；文字左内缩 10）----
        internal const float SupY = 30f;
        internal const float SupW = 260f;
        internal const float SupH = 48f;
        internal const float SupEnY = 46f;
        internal const float SupCnY = 66f;
        /// <summary>支持区文字左内缩（预览 <c>el('text',x:s.x+10)</c>）。</summary>
        internal const float SupPadX = 10f;
        internal static readonly float[] SupX = { 180f, 483f, 786f, 1089f };
        internal static readonly string[] SupNameKeys =
        {
            "PersonalChronicle.UI.Floor.Sup.TeamCorner",
            "PersonalChronicle.UI.Floor.Sup.Utility",
            "PersonalChronicle.UI.Floor.Sup.Rework",
            "PersonalChronicle.UI.Floor.Sup.Scrap"
        };
        internal static readonly string[] SupEnLabels = { "TEAM CORNER", "UTILITY", "REWORK / NC", "SCRAP" };

        // ---- L6 车间格（3 行 × 4 列；BAY 260×126；ROWY=88/222/356）----
        internal const float BayW = 260f;
        internal const float BayH = 126f;
        internal static readonly float[] BayX = { 180f, 483f, 786f, 1089f };
        internal static readonly float[] BayY = { 88f, 222f, 356f };
        internal const int BayColumns = 4;
        internal const int BayCount = WorkshopFloorSnapshot.BaySlots;
        internal const float CornerArm = 18f;
        internal const float BayNumberRadius = 9f;
        internal const float BayNumberOffset = 17f;
        internal const float BayNameOffsetX = 32f;
        internal const float BayNameOffsetY = 21f;
        internal const float BayMetaOffsetY = 38f;
        internal const float BayEdgeInset = 12f;

        // ---- L7 设备（每格 2 台；MW=104 MH=60）----
        internal const float MachineW = 104f;
        internal const float MachineH = 60f;
        internal static readonly float[] MachineLocalX = { 18f, 138f };
        internal const float MachineLocalY = 64f;
        internal const int MachinesPerBay = WorkshopFloorSnapshot.DevicesPerBay;
        internal const float FoundInflate = 4f;
        internal const float BoltInset = 3f;
        internal const float BoltRadius = 1.6f;
        internal const float HazardStripInset = 3f;
        internal const float HazardStripH = 3f;
        internal const float LampOffsetX = 12f;
        internal const float LampOffsetY = 13f;
        internal const float LampRadius = 4.5f;
        internal const float TextOffsetX = 22f;
        internal const float TextOffsetY = 17f;
        internal const float SubTextOffsetY = 29f;
        internal const float PowerTextOffsetX = 62f;
        internal const float StatusOffsetY = 40f;
        internal const float OperatorOffsetY = MachineH - 8f;
        internal const float ProgressOffsetY = MachineH - 5f;
        internal const float ProgressInsetX = 10f;
        internal const float ProgressH = 3.5f;

        // ---- 线尾控制台（`S-00`）常量**已删除**：建筑 `PC_ControlTerminal` 未落地，
        //      预览的绘制（灯/状态）属冒充数据（基線 §5c-B/D13）；落地后按真实 Thing 重画。 ----

        // ---- L8 工人（圆 r8，首字 y+3.5）----
        internal const float WorkerRadius = 8f;
        internal const int MaxWorkersPerBay = 4;
        internal const float WorkerRowOffsetY = 20f;
        internal const float WorkerSpacing = 24f;
        internal const float WorkerStartX = 18f;

        // ---- L9 图例 + 底注：**已删除**（新预览 2026-09-11 把图例整组迁入 `#cmdbar .lgbar`，
        //      平面轴注按 LAND-009 无后端，一并清掉；常量随之作废）----

        // ---- 底纹 / 警示纹（预览 pattern 原文参数）----
        // 网格：pattern width=48 height=48，path stroke-width .7 opacity .26（HTML 1037-1038）
        internal const float GridStep = 48f;
        internal const float GridOpacity = 0.26f;
        // 警示斜纹：pattern 14×14 rotate(45)，rect 7×14 #c9a52c + 7×14 #181818（HTML 1039-1041）
        internal const float HazardStep = 14f;
        internal const float HazardHalf = 7f;

        // ---- 工序链路（角标 = 机箱右上偏内 9；折线走格下部）----
        internal const float RouteBadgeInset = 9f;
        internal const float RouteLineYRatio = 0.78f;

        // ---- 纯几何换算 ----

        /// <summary>第 <paramref name="slot"/> 格（0..11）的设计坐标矩形。</summary>
        internal static Rect BayRect(int slot)
        {
            int col = slot % BayColumns;
            int row = slot / BayColumns;
            return new Rect(BayX[col], BayY[row], BayW, BayH);
        }

        /// <summary>格内第 <paramref name="index"/> 台设备的设计坐标矩形（0/1）。</summary>
        internal static Rect MachineRect(int slot, int index)
        {
            Rect bay = BayRect(slot);
            int i = index < 0 || index >= MachinesPerBay ? 0 : index;
            return new Rect(bay.x + MachineLocalX[i], bay.y + MachineLocalY, MachineW, MachineH);
        }

        /// <summary>格内第 <paramref name="index"/> 个工人圆点的设计坐标中心。</summary>
        internal static Vector2 WorkerCenter(int slot, int index)
        {
            Rect bay = BayRect(slot);
            int i = index < 0 || index >= MaxWorkersPerBay ? 0 : index;
            return new Vector2(
                bay.x + WorkerStartX + i * WorkerSpacing,
                bay.y + BayH - WorkerRowOffsetY);
        }
    }
}
