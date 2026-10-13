using System;
using PersonalChronicle.Archive;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>在人员中心内容矩形内绘制档案面板；不查询主按钮 Def，也不创建独立窗口。</summary>
    internal static class PersonnelArchiveHost
    {
        internal static void Draw(PersonnelArchivePanel panel, Rect rect, bool pawnDetailView = false)
        {
            if (panel == null || rect.width < 1f || rect.height < 1f)
            {
                return;
            }
            (Color color, GameFont font, TextAnchor anchor, bool wordWrap) gui =
                ArchivePanelBase.CaptureGuiState();
            try
            {
                GUI.BeginGroup(rect);
                try
                {
                    panel.DrawEmbeddedView(new Rect(0f, 0f, rect.width, rect.height), pawnDetailView);
                }
                finally
                {
                    GUI.EndGroup();
                }
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Ui, "Personnel archive panel failed: " + ex);
            }
            finally
            {
                ArchivePanelBase.RestoreGuiState(gui);
            }
        }
    }
}
