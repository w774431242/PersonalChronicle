using PersonalChronicle.MasterControl.Population;
using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// v1.2.0 工坊卡「全部工作 room」弹窗：列出该殖民者历史房间 + 当前地图其他制作台，
    /// 每行支持「改名（房间名）/ 定位 / 设为当前」；副行展示该房间 Top3 设备·使用次数。
    ///
    /// 设计边界（UI 规范 §5）：本窗口只消费由 <see cref="ArchiveUiDataProvider.BuildWorkplaceChoices"/>
    /// 已聚合的只读 <see cref="WorkplaceChoiceView"/> 列表，不在窗口内排序/聚合/查询派生。
    /// 改名经 <see cref="IArchiveService.SetRoomName"/>（per-pawn+roomType 粒度）、设为当前经
    /// <see cref="IArchiveService.SetCurrentWorkplace"/>（仅改档案记录，不指挥殖民者）。
    /// 所有绘制走 <see cref="UIComponents"/> / <see cref="UITheme"/>，绘制态配对恢复。
    /// </summary>
    public class Dialog_WorkplaceList : Window
    {
        private const float TitleH = 30f;
        private const float HintH = 34f;       // 中文 Tiny 两行安全高度（原 18f 截断）
        // v1.2.0 方案B：双列网格（info 区 + 右侧悬浮图标按钮列）
        private const float RowH = 54f;         // 单行高（名称+副行+分隔线）
        private const float RowGap = 6f;        // 行间距
        private const float WinW = 520f;
        private const float PadX = 14f;         // 窗口内边距
        private const float IconColW = 80f;     // 右侧图标按钮列宽
        private const float IconBtnW = 24f;     // 单个图标按钮边长
        private const float IconBtnGap = 4f;    // 图标按钮间距（3×24+2×4=80）
        // 图标字形：项目已验证 ✓/✗/⚔/▶ 在该字体下可渲染（PersonnelArchivePanel 先例）
        private const string GlyphRename = "✎";
        private const string GlyphJump = "⊕";
        private const string GlyphSetCurrent = "✓";

        private readonly string title;
        private readonly string hint;
        private readonly IArchiveService service;
        private readonly string pawnStableId;
        private readonly IReadOnlyList<WorkplaceChoiceView> choices;
        private readonly Action onChanged;

        private Vector2 scroll = Vector2.zero;
        private float winHeight;

        public override Vector2 InitialSize
        {
            get { return new Vector2(WinW, winHeight); }
        }

        public Dialog_WorkplaceList(string titleKey, string hintKey,
            IArchiveService service, string pawnStableId,
            IReadOnlyList<WorkplaceChoiceView> choices, Action onChanged)
        {
            this.title = titleKey.Translate();
            this.hint = hintKey.Translate();
            this.service = service;
            this.pawnStableId = pawnStableId;
            this.choices = choices ?? System.Array.Empty<WorkplaceChoiceView>();
            this.onChanged = onChanged;

            doCloseX = true;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            drawShadow = true;
            forcePause = true;

            float contentH = this.choices.Count * (RowH + RowGap);
            float needed = TitleH + HintH + contentH + PadX * 2f;
            float maxH = Mathf.Max(200f, Verse.UI.screenHeight * 0.8f);
            winHeight = Mathf.Min(needed, maxH);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            try
            {
                UIComponents.TintedBox(inRect, UITheme.Window);

                float y = inRect.y;
                Rect titleRect = new Rect(inRect.x, y, inRect.width, TitleH);
                UIComponents.Label(titleRect, title, GameFont.Medium, UITheme.Text, TextAnchor.MiddleLeft);
                y += TitleH;

                Rect hintRect = new Rect(inRect.x, y, inRect.width, HintH);
                UIComponents.Label(hintRect, hint, GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                y += HintH + 4f;

                float listTop = y;
                float listBottom = inRect.yMax;
                float listH = Mathf.Max(0f, listBottom - listTop);
                Rect listRect = new Rect(inRect.x, listTop, inRect.width, listH);

                if (choices.Count == 0)
                {
                    UIComponents.Label(new Rect(inRect.x, listTop, inRect.width, 40f),
                        "PersonalChronicle.UI.WorkplaceList.Empty".Translate().ToString(),
                        GameFont.Small, UITheme.Muted, TextAnchor.MiddleLeft);
                    return;
                }

                float innerW = inRect.width - UITheme.ScrollbarThickness;
                float contentH = choices.Count * (RowH + RowGap);
                Rect inner = new Rect(0f, 0f, Mathf.Max(0f, innerW), Mathf.Max(contentH, listH));
                Widgets.BeginScrollView(listRect, ref scroll, inner);
                for (int i = 0; i < choices.Count; i++)
                {
                    WorkplaceChoiceView c = choices[i];
                    if (c == null) continue;
                    float ry = i * (RowH + RowGap);
                    Rect rowRect = new Rect(0f, ry, inner.width, RowH);
                    DrawRow(rowRect, c);
                }
                Widgets.EndScrollView();
            }
            finally
            {
                GUI.color = prevColor;
                Verse.Text.Font = prevFont;
                Verse.Text.Anchor = prevAnchor;
            }
        }

        // v1.2.0 方案B：双列网格——左侧 info 区（房间名+当前角标 / Top3设备·次数 / 分隔线），
        // 右侧图标按钮列（改名 ✎ / 定位 ⊕ / 设为当前 ✓）垂直居中。操作按钮视觉权重降到最低。
        private void DrawRow(Rect rowRect, WorkplaceChoiceView c)
        {
            UIComponents.Card(rowRect, c.IsCurrent ? UITheme.Alive : UITheme.PillGold);

            float padX = 10f;
            float infoW = rowRect.width - IconColW - padX;

            // 名称行：工作房间名（RoomRoleLabel）优先，fallback 户外；当前项右侧加 Alive 角标
            string roomName = !string.IsNullOrEmpty(c.RoomRoleLabel) ? c.RoomRoleLabel
                : "PersonalChronicle.UI.WorkplaceList.Outdoor".Translate().ToString();

            Rect nameRect = new Rect(rowRect.x + padX, rowRect.y + 3f, infoW, 20f);
            if (c.IsCurrent)
            {
                Verse.Text.Font = GameFont.Tiny;
                string curText = "✓ " + "PersonalChronicle.UI.WorkplaceList.Current".Translate().ToString();
                float curW = Verse.Text.CalcSize(curText).x + 4f;
                Verse.Text.Font = GameFont.Small;
                float labelW = Mathf.Max(0f, infoW - curW - 4f);
                UIComponents.Label(new Rect(nameRect.x, nameRect.y, labelW, nameRect.height),
                    roomName, GameFont.Small, UITheme.Text, TextAnchor.UpperLeft);
                UIComponents.Label(new Rect(nameRect.x + labelW + 4f, nameRect.y, curW, nameRect.height),
                    curText, GameFont.Tiny, UITheme.Alive, TextAnchor.MiddleRight);
            }
            else
            {
                UIComponents.Label(nameRect, roomName, GameFont.Small, UITheme.Text, TextAnchor.UpperLeft);
            }

            // 副行：Top3 设备（label·次数），Tiny 18f 防中文截断
            string sub = BuildDeviceSubline(c.TopDevices, c.UseCount);
            UIComponents.Label(new Rect(rowRect.x + padX, rowRect.y + 24f, infoW, 18f),
                sub, GameFont.Tiny, UITheme.Muted, TextAnchor.UpperLeft);

            // 分隔线（info 区底部）
            UIComponents.Rule(new Rect(rowRect.x + padX, rowRect.y + 42f, infoW, UITheme.RuleHeight),
                UITheme.BorderSoft);

            // ---- 右侧图标按钮列（垂直居中） ----
            float btnY = rowRect.y + (RowH - IconBtnW) / 2f;
            float iconX = rowRect.x + infoW + padX + 6f;
            Rect renameRect = new Rect(iconX, btnY, IconBtnW, IconBtnW);
            Rect jumpRect = new Rect(iconX + IconBtnW + IconBtnGap, btnY, IconBtnW, IconBtnW);
            Rect setCurRect = new Rect(jumpRect.xMax + IconBtnGap, btnY, IconBtnW, IconBtnW);

            bool canJump = c.MapIndex >= 0 && c.Cell.IsValid;
            Map map = null;
            if (canJump)
            {
                foreach (Map m in Find.Maps)
                {
                    if (m != null && m.Index == c.MapIndex) { map = m; break; }
                }
                canJump = map != null && c.Cell.InBounds(map);
            }

            if (DrawIconButton(renameRect, GlyphRename,
                "PersonalChronicle.UI.WorkplaceList.Rename".Translate().ToString(), UITheme.Muted))
            {
                OpenRename(c);
            }
            if (canJump)
            {
                if (DrawIconButton(jumpRect, GlyphJump,
                    "PersonalChronicle.UI.WorkplaceList.Jump".Translate().ToString(), UITheme.Muted))
                {
                    CameraJumper.TryJump(c.Cell, map);
                    Close(true);
                }
            }
            else
            {
                // 无坐标：灰显禁用（不消费点击）
                UIComponents.Label(jumpRect, GlyphJump, GameFont.Tiny, UITheme.BorderSoft, TextAnchor.MiddleCenter);
            }
            // 非当前项才显示「设为当前」（当前项该按钮隐藏，避免无意义操作）
            if (!c.IsCurrent && DrawIconButton(setCurRect, GlyphSetCurrent,
                "PersonalChronicle.UI.WorkplaceList.SetCurrent".Translate().ToString(), UITheme.Alive))
            {
                SetCurrent(c);
            }
        }

        /// <summary>v1.2.0 构建副行文本：Top3 设备 label·次数，或 fallback 单设备名·总次数。</summary>
        private static string BuildDeviceSubline(DeviceUseItem[] topDevices, int totalUses)
        {
            if (topDevices != null && topDevices.Length > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < topDevices.Length; i++)
                {
                    if (i > 0) sb.Append(" · ");
                    sb.Append(topDevices[i].Label ?? "");
                    if (topDevices[i].Count > 0)
                        sb.Append(" ").Append(topDevices[i].Count.ToString());
                }
                return sb.ToString();
            }
            // 无 Top3 数据：fallback 显示总使用次数（兼容当前地图补充项/旧存档）
            if (totalUses > 0)
                return "PersonalChronicle.UI.WorkplaceList.Uses".Translate(totalUses).ToString();
            return "";
        }

        /// <summary>悬浮图标按钮：hover 高亮 + tooltip + 点击判定。绘制态经 UIComponents 配对恢复。</summary>
        private static bool DrawIconButton(Rect rect, string glyph, string tooltip, Color tint)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            UIComponents.Label(rect, glyph, GameFont.Small, tint, TextAnchor.MiddleCenter);
            TooltipHandler.TipRegion(rect, tooltip);
            return Widgets.ButtonInvisible(rect);
        }

        // v1.2.0 改名改房间名（SetRoomName，per-pawn+roomType 粒度），不再改设备名（SetBuildingAlias）。
        private void OpenRename(WorkplaceChoiceView c)
        {
            if (service == null || string.IsNullOrEmpty(c.RoomRoleDefName) || string.IsNullOrEmpty(pawnStableId)) return;
            // 当前房间名：优先 GetRoomName > RoomRoleLabel
            string currentRoomName = service.GetRoomName(pawnStableId, c.RoomRoleDefName);
            if (string.IsNullOrEmpty(currentRoomName))
                currentRoomName = c.RoomRoleLabel ?? c.RoomRoleDefName ?? "";
            Find.WindowStack.Add(new Dialog_RenameWorkplace(
                "PersonalChronicle.UI.RenameWorkplace.Title",
                "PersonalChronicle.UI.RenameWorkplace.Hint.Room",
                currentRoomName,
                name => service.SetRoomName(pawnStableId, c.RoomRoleDefName, name)));
        }

        private void SetCurrent(WorkplaceChoiceView c)
        {
            if (service == null || string.IsNullOrEmpty(pawnStableId)) return;
            service.SetCurrentWorkplace(pawnStableId, c.BuildingDefName, c.BuildingStableId,
                c.RoomRoleLabel, c.MapIndex, c.Cell);
            if (onChanged != null) onChanged();
            Close(true);
        }
    }
}
