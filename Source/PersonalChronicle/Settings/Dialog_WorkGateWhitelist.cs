using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Settings
{
    /// <summary>工作合同闸门 · **白名单可视化窗口**（CHG-20261012-WORKGATE · W3）。
    ///
    /// <para>逐工种一行：`名称 · 状态 · 开关`；顶部三个快捷（恢复默认 / 全部纳入 / 全部放行）；
    /// 改动立刻 <see cref="WorkAuthorityInvalidator"/> 让所有殖民者的作业表重建（≤1 帧生效，无需等 250 tick）。</para>
    ///
    /// <para>状态列只显示 "白名单 / 需合同授权" 两态 + 订单类标记（Def 数据判定）：车间班次托管是**地图级**读数，
    /// 需要地图与车间管理者，不在此处求值（避免 Settings→Automation 依赖边）。</para></summary>
    internal sealed class Dialog_WorkGateWhitelist : Window
    {
        private Vector2 scroll;
        private List<string> observedWhitelist;
        private float measuredWidth = -1f;
        private readonly List<WorkTypeDef> workTypes = new List<WorkTypeDef>();
        private readonly List<string> allowedLabels = new List<string>(), gatedLabels = new List<string>();
        private string[] labels, previews;
        private bool[] allowedRows;

        public Dialog_WorkGateWhitelist()
        {
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = false;
            var all = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                var wt = all[i];
                if (wt == null || string.IsNullOrEmpty(wt.defName)) continue;
                workTypes.Add(wt);
                allowedLabels.Add(wt.LabelCap + " · " + Status(wt, true));
                gatedLabels.Add(wt.LabelCap + " · " + Status(wt, false));
            }
            labels = new string[workTypes.Count]; previews = new string[workTypes.Count]; allowedRows = new bool[workTypes.Count];
        }

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(620f, Mathf.Max(1f, Verse.UI.screenWidth - 80f)),
            Mathf.Min(640f, Mathf.Max(1f, Verse.UI.screenHeight - 80f)));

        public override void DoWindowContents(Rect inRect)
        {
            ChronicleSettings settings = PersonalChronicleMod.Settings;
            if (settings == null) return;
            if (settings.WorkGateWhitelist == null) settings.WorkGateWhitelist = WorkAuthority.Defaults();

            float y = inRect.y;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 28f),
                "PersonalChronicle.Settings.WorkGate.WhitelistTitle".Translate().ToString());
            y += 32f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 40f),
                "PersonalChronicle.Settings.WorkGate.WhitelistHint".Translate().ToString()
                + "  " + settings.WorkGateWhitelist.Count.ToString());
            y += 44f;

            float bw = (inRect.width - 12f) / 3f;
            if (Widgets.ButtonText(new Rect(inRect.x, y, bw, 28f),
                    "PersonalChronicle.Settings.WorkGate.ResetDefault".Translate().ToString()))
                Apply(settings, WorkAuthority.Defaults());
            if (Widgets.ButtonText(new Rect(inRect.x + bw + 6f, y, bw, 28f),
                    "PersonalChronicle.Settings.WorkGate.AllGated".Translate().ToString()))
                Apply(settings, new List<string>());
            if (Widgets.ButtonText(new Rect(inRect.x + (bw + 6f) * 2f, y, bw, 28f),
                    "PersonalChronicle.Settings.WorkGate.AllAllowed".Translate().ToString()))
                Apply(settings, WorkAuthority.Normalize(AllNames()));
            y += 34f;

            var body = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            List<WorkTypeDef> all = workTypes;
            float rowH = 26f, view = all.Count * rowH + 8f;
            var viewRect = new Rect(0f, 0f, body.width - 18f, Mathf.Max(view, body.height));
            if (!ReferenceEquals(observedWhitelist, settings.WorkGateWhitelist) || Math.Abs(measuredWidth - body.width) > 0.01f)
            {
                observedWhitelist = settings.WorkGateWhitelist; measuredWidth = body.width;
                float labelWidth = Mathf.Max(1f, viewRect.width - 32f); // 原版 CheckboxLabeled 右侧24px勾选框+8px间隙
                for (int i = 0; i < all.Count; i++)
                {
                    allowedRows[i] = WorkAuthority.IsWhitelisted(all[i].defName, settings.WorkGateWhitelist);
                    labels[i] = allowedRows[i] ? allowedLabels[i] : gatedLabels[i];
                    previews[i] = EconomyTableLayout.DetailSummary(labels[i], labelWidth, s => RUIF.Measure.Width(s, GameFont.Small));
                }
            }
            Widgets.BeginScrollView(body, ref scroll, viewRect);
            try
            {
              for (int i = 0; i < all.Count; i++)
              {
                WorkTypeDef wt = all[i];
                if (wt == null || string.IsNullOrEmpty(wt.defName)) continue;
                var row = new Rect(viewRect.x, i * rowH, viewRect.width, rowH);
                bool on = allowedRows[i];
                bool next = on;
                Widgets.CheckboxLabeled(row, previews[i], ref next);   // ⚠ 原版签名返回 void（不是 bool）
                if (Mouse.IsOver(row)) TooltipHandler.TipRegion(row, labels[i]);
                if (next != on)
                {
                    var updated = new List<string>(settings.WorkGateWhitelist);
                    if (next) updated.Add(wt.defName);
                    else updated.RemoveAll(n => n == wt.defName);
                    Apply(settings, updated);
                    break; // 新白名单下一帧重新缓存，避免本帧继续用旧标签
                }
              }
            }
            catch (Exception error) { Log.ErrorOnce("[PC][UI] work gate whitelist failed: " + error, 0x1EC0E22); }
            finally { Widgets.EndScrollView(); }
        }

        private static void Apply(ChronicleSettings settings, List<string> whitelist)
        {
            settings.WorkGateWhitelist = WorkAuthority.Normalize(whitelist);
            WorkAuthorityInvalidator.InvalidateAll();   // 立即生效
        }

        private static string Status(WorkTypeDef wt, bool whitelisted)
        {
            string state = whitelisted
                ? "PersonalChronicle.Settings.WorkGate.State.Whitelist".Translate().ToString()
                : "PersonalChronicle.Settings.WorkGate.State.ContractOnly".Translate().ToString();
            List<WorkGiverDef> givers = wt.workGiversByPriority;
            for (int i = 0; givers != null && i < givers.Count; i++)
            {
                if (givers[i]?.giverClass != null && typeof(WorkGiver_DoBill).IsAssignableFrom(givers[i].giverClass))
                {
                    state += " · " + "PersonalChronicle.Settings.WorkGate.Tag.Bill".Translate().ToString();
                    break;
                }
            }
            return state;
        }

        private static List<string> AllNames()
        {
            var names = new List<string>();
            List<WorkTypeDef> all = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && !string.IsNullOrEmpty(all[i].defName)) names.Add(all[i].defName);
            return names;
        }
    }
}
