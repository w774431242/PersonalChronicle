using RimWorld;
using System.Collections.Generic;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// 总览数据面板 · 字段自定义对话框（二级钻取）。
    /// 第一层：按来源列出分类（原版 / 本 Mod / 其他 Mod-1 / 其他 Mod-2 …），显示字段计数。
    /// 第二层：点击某分类 → 进入该来源的字段勾选页，可返回。
    /// 勾选结果写回 ChronicleSettings.OverviewPanelFields（空=未添加任何字段，完全手动模式）。
    /// </summary>
    public class Dialog_OverviewFields : Window
    {
        private readonly ChronicleSettings settings;
        // 编辑期副本：避免每次勾选都写盘，关闭时统一提交
        private readonly HashSet<string> draft;
        // null = 分类列表页；非 null = 该来源字段勾选页
        private string drillSource = null;

        public Dialog_OverviewFields(ChronicleSettings settings)
        {
            this.settings = settings;
            this.draft = new HashSet<string>(
                settings?.OverviewPanelFields != null && settings.OverviewPanelFields.Count > 0
                    ? settings.OverviewPanelFields
                    : new HashSet<string>());
            this.closeOnClickedOutside = true;
            this.doCloseX = true;
            this.forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(420f, 540f);

        public override void DoWindowContents(Rect inRect)
        {
            // IMGUI 边界：所有可变 GUI 状态（color/font/anchor）必须在帧内捕获并在帧末还原，
            // 不得泄漏到下一帧——这是 Immediate Mode 的核心约束。
            var guiState = ArchivePanelBase.CaptureGuiState();

            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "PersonalChronicle.UI.OvF.ConfigTitle".Translate().ToString());
            Text.Font = GameFont.Small;

            // 面包屑 / 返回行（自顶向下累加布局，不依赖外部布局引擎）
            Rect crumbRect = new Rect(inRect.x, inRect.y + 34f, inRect.width, 22f);
            if (drillSource == null)
            {
                // 次级提示文字：经 UITheme 令牌着色，而非硬编码 new Color / GUI.color=
                GUI.color = UITheme.Dim;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(crumbRect, "PersonalChronicle.UI.OvF.GroupHint".Translate().ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = guiState.color; // 立即还原，避免影响后续控件
            }
            else
            {
                if (Widgets.ButtonText(crumbRect, "< " + "PersonalChronicle.UI.OvF.Back".Translate().ToString()))
                    drillSource = null;
            }

            Rect listRect = new Rect(inRect.x, inRect.y + 60f, inRect.width, inRect.height - 60f - 40f);
            Rect btnRect = new Rect(inRect.x, inRect.y + inRect.height - 32f, inRect.width, 32f);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(listRect);
            listing.Gap(4f);

            if (drillSource == null)
                DrawSourceList(listing);
            else
                DrawSourceFields(listing, drillSource);

            listing.End();

            // 底部按钮：清空 / 关闭保存
            Rect resetR = new Rect(btnRect.x, btnRect.y, btnRect.width * 0.46f, btnRect.height);
            Rect closeR = new Rect(btnRect.x + btnRect.width * 0.54f, btnRect.y, btnRect.width * 0.46f, btnRect.height);
            if (Widgets.ButtonText(resetR, "PersonalChronicle.UI.OvF.ResetDefault".Translate().ToString()))
            {
                // 完全手动模式：重置 = 清空所有已选字段（无默认项）
                draft.Clear();
            }
            if (Widgets.ButtonText(closeR, "PersonalChronicle.UI.OvF.Close".Translate().ToString()))
            {
                Commit();
                Close();
            }

            ArchivePanelBase.RestoreGuiState(guiState);
        }

        /// <summary>第一层：来源分类列表，点击钻取进入对应字段页。</summary>
        private void DrawSourceList(Listing_Standard listing)
        {
            var order = OverviewFieldRegistry.SourceOrder;
            foreach (var src in order)
            {
                var fields = OverviewFieldRegistry.FieldsOfSource(src);
                int total = fields.Count;
                int selected = 0;
                foreach (var f in fields) if (draft.Contains(f.Key)) selected++;
                string label = OverviewFieldRegistry.SourceLabel(src)
                    + "    " + selected + " / " + total;
                Rect row = listing.GetRect(26f);
                if (Widgets.ButtonText(row, label))
                    drillSource = src;
                listing.Gap(2f);
            }
        }

        /// <summary>第二层：某来源下的字段勾选。</summary>
        private void DrawSourceFields(Listing_Standard listing, string src)
        {
            Text.Font = GameFont.Medium;
            listing.Label(OverviewFieldRegistry.SourceLabel(src), -1f);
            Text.Font = GameFont.Small;
            listing.Gap(2f);
            foreach (var f in OverviewFieldRegistry.FieldsOfSource(src))
            {
                bool on = draft.Contains(f.Key);
                listing.CheckboxLabeled(f.DisplayLabel, ref on);
                if (on && !draft.Contains(f.Key)) draft.Add(f.Key);
                else if (!on && draft.Contains(f.Key)) draft.Remove(f.Key);
            }
        }

        private void Commit()
        {
            if (settings == null) return;
            // 若未勾选任何字段则存为空（语义：未添加任何字段，完全手动）
            bool sameAsDefault = draft.Count == OverviewFieldRegistry.DefaultKeys.Count;
            if (sameAsDefault)
            {
                foreach (var k in OverviewFieldRegistry.DefaultKeys)
                    if (!draft.Contains(k)) { sameAsDefault = false; break; }
            }
            settings.OverviewPanelFields = sameAsDefault ? null : new HashSet<string>(draft);
            LoadedModManager.GetMod<PersonalChronicleMod>()?.WriteSettings();
        }
    }
}
