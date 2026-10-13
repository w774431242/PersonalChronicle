using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>银单位文本→整数分；只接受不带符号/分组的小数点金额，禁止静默舍入与溢出。</summary>
    internal static class WorkPriceInput
    {
        internal static bool TryParse(string text, out long minor)
        {
            minor = 0L;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string value = text.Trim();
            bool point = false;
            int decimals = 0, digits = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '.' && !point && digits > 0) { point = true; continue; }
                if (c < '0' || c > '9') return false;
                digits++;
                if (point && ++decimals > 2) return false;
            }
            if (digits == 0 || (point && decimals == 0)) return false;
            if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal silver))
                return false;
            if (silver > WorkPriceRules.MaximumMinor / 100m) return false;
            minor = (long)(silver * 100m);
            return true;
        }

        internal static string Format(long minor) => (minor / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>两级新合同定价。打开时读配置，保存时走唯一写入口；绘制不扫描运行时/订单/Def。
    /// 窗口状态只存输入与只读标签，不持业务真值。0/清除恢复继承；既有合同的单价不在此写入。</summary>
    internal sealed class Dialog_WorkPrices : Window
    {
        private sealed class PriceRow
        {
            internal string Work, Order, Label, Preview, Buffer, Query, Status, StatusPreview;
            internal float LastInput;
            internal long Configured;
        }

        private readonly string anchorKey;
        private readonly List<PriceRow> rows = new List<PriceRow>();
        private readonly string title, hint, inherit, save, clear, invalid, failed, saved;
        private readonly float textHeight, controlHeight, statusHeight, saveWidth, clearWidth;
        private Vector2 scroll;
        private float measuredWidth = -1f;
        private string hintPreview;

        private static string T(string key) => EconomyLabels.TranslatedOrId("PC_PEA.WorkPrices." + key, key);

        internal Dialog_WorkPrices(string anchorKey)
        {
            this.anchorKey = anchorKey;
            doCloseX = true; closeOnClickedOutside = true; absorbInputAroundWindow = true;
            draggable = true; forcePause = true;
            title = T("Title"); hint = T("Hint"); inherit = T("Inherit");
            save = T("Save"); clear = T("Clear"); invalid = T("Invalid"); failed = T("Failed"); saved = T("Saved");
            textHeight = RUIF.Measure.RowHeight(GameFont.Small);
            statusHeight = RUIF.Measure.RowHeight(GameFont.Tiny);
            controlHeight = RUIF.Measure.ControlHeight(GameFont.Small);
            saveWidth = EconomyRowList.RowButtonWidth(save);
            clearWidth = EconomyRowList.RowButtonWidth(clear);
            var runtime = GameComponent_PawnEconomy.Current;
            foreach (var group in VanillaOrderCatalogue.Groups)
            {
                string work = group.WorkTypeDefName;
                string label = DefDatabase<WorkTypeDef>.GetNamedSilentFail(work)?.LabelCap.ToString() ?? work;
                AddRow(runtime, work, null, label + " · " + T("Default"));
                foreach (var order in group.Orders)
                {
                    string orderLabel = EconomyLabels.TranslatedOrId("PC_PEA.Order." + order.Key, order.Key);
                    AddRow(runtime, work, order.Key, label + " · " + orderLabel + " · " + T("Override"));
                }
            }
        }

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(780f, Mathf.Max(1f, Verse.UI.screenWidth - 80f)),
            Mathf.Min(720f, Mathf.Max(1f, Verse.UI.screenHeight - 80f)));

        private void AddRow(GameComponent_PawnEconomy runtime, string work, string order, string label)
        {
            long configured = runtime?.ReadWorkPrice(anchorKey, work, order) ?? 0L;
            string value = WorkPriceInput.Format(configured);
            rows.Add(new PriceRow { Work = work, Order = order, Label = label, Configured = configured,
                Buffer = value, Query = value, Status = runtime == null || !runtime.WorkPricesSupported
                    ? failed : configured == 0L ? inherit : value });
        }

        private bool StackedControls(float width)
            => width < saveWidth + clearWidth + RUIF.Tokens.SpaceSm * 2f + 120f;

        private float RowHeight(float width)
            => textHeight + controlHeight + statusHeight + RUIF.Tokens.CardPadY * 2f + RUIF.Tokens.SpaceXs * 2f
                + (StackedControls(width) ? controlHeight + RUIF.Tokens.SpaceXs : 0f);

        private void PrepareLayout(float width)
        {
            if (Math.Abs(measuredWidth - width) < 0.01f) return;
            measuredWidth = width;
            hintPreview = EconomyTableLayout.DetailSummary(hint, width, s => RUIF.Measure.Width(s, GameFont.Small));
            float labelWidth = Mathf.Max(1f, width - RUIF.Tokens.ScrollbarThickness - RUIF.Tokens.CardPadX * 2f);
            foreach (var row in rows)
            {
                row.Preview = EconomyTableLayout.DetailSummary(row.Label, labelWidth, s => RUIF.Measure.Width(s, GameFont.Small));
                row.StatusPreview = EconomyTableLayout.DetailSummary(row.Status, labelWidth, s => RUIF.Measure.Width(s, GameFont.Tiny));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            PrepareLayout(inRect.width);
            var column = RUIF.Layout.Column(inRect, RUIF.Tokens.SpaceXs);
            RUIF.Draw.Label(column.Take(textHeight), title, GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            Rect hintRect = column.Take(textHeight);
            RUIF.Draw.Label(hintRect, hintPreview, GameFont.Small, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            if (Mouse.IsOver(hintRect)) TooltipHandler.TipRegion(hintRect, hint);
            Rect body = column.Rest;
            float innerWidth = Mathf.Max(1f, body.width - RUIF.Tokens.ScrollbarThickness - RUIF.Tokens.CardPadX * 2f);
            float rowHeight = RowHeight(innerWidth);
            float extent = rows.Count * (rowHeight + RUIF.Tokens.SpaceXs);
            Rect content = RUIF.Layout.BeginScroll(body, Mathf.Max(body.height, extent), ref scroll);
            try
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    float y = i * (rowHeight + RUIF.Tokens.SpaceXs);
                    if (y + rowHeight < scroll.y || y > scroll.y + body.height) continue;
                    var card = new Rect(content.x, y, content.width, rowHeight);
                    DrawPriceRow(EconomyUiKit.CardFrame(card, Mouse.IsOver(card), null), rows[i]);
                }
            }
            catch (Exception error) { Log.ErrorOnce("[PC][UI] work prices failed: " + error, 0x1EC0E21); }
            finally { RUIF.Layout.EndScroll(); }
        }

        private void DrawPriceRow(Rect inner, PriceRow row)
        {
            var label = new Rect(inner.x, inner.y, inner.width, textHeight);
            RUIF.Draw.Label(label, row.Preview, GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            if (Mouse.IsOver(label)) TooltipHandler.TipRegion(label, row.Label);
            float y = label.yMax + RUIF.Tokens.SpaceXs;
            bool stacked = StackedControls(inner.width);
            float inputWidth = stacked ? inner.width : inner.width - saveWidth - clearWidth - RUIF.Tokens.SpaceSm * 2f;
            RUIF.InputAtoms.SearchField(new Rect(inner.x, y, Mathf.Max(1f, inputWidth), controlHeight),
                ref row.Query, ref row.Buffer, inherit, ref row.LastInput);
            float buttonsY = stacked ? y + controlHeight + RUIF.Tokens.SpaceXs : y;
            float buttonsX = stacked ? inner.x : inner.x + inputWidth + RUIF.Tokens.SpaceSm;
            float availableButton = stacked ? Mathf.Max(1f, (inner.width - RUIF.Tokens.SpaceSm) * 0.5f) : saveWidth;
            float sw = Mathf.Min(saveWidth, availableButton);
            float cw = stacked ? Mathf.Min(clearWidth, availableButton) : clearWidth;
            Rect saveRect = new Rect(buttonsX, buttonsY, sw, controlHeight);
            if (RUIF.ButtonAtoms.PrimaryButton(saveRect, save, true, RUIF.ButtonTone.Accent))
                Save(row, false);
            if (Mouse.IsOver(saveRect)) TooltipHandler.TipRegion(saveRect, save);
            Rect clearRect = new Rect(buttonsX + sw + RUIF.Tokens.SpaceSm, buttonsY, cw, controlHeight);
            if (RUIF.ButtonAtoms.PrimaryButton(clearRect, clear, true, RUIF.ButtonTone.Default)) Save(row, true);
            if (Mouse.IsOver(clearRect)) TooltipHandler.TipRegion(clearRect, clear + " · " + inherit);
            Rect status = new Rect(inner.x, buttonsY + controlHeight + RUIF.Tokens.SpaceXs, inner.width, statusHeight);
            RUIF.Draw.Label(status, row.StatusPreview, GameFont.Tiny, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            if (Mouse.IsOver(status)) TooltipHandler.TipRegion(status, row.Status);
        }

        private void Save(PriceRow row, bool clearing)
        {
            long minor = 0L;
            if (!clearing && !WorkPriceInput.TryParse(row.Buffer, out minor)) { SetStatus(row, invalid); return; }
            var runtime = GameComponent_PawnEconomy.Current;
            if (runtime == null || !runtime.SetWorkPrice(anchorKey, row.Work, row.Order, minor))
            { SetStatus(row, failed); return; }
            row.Configured = minor;
            row.Buffer = row.Query = WorkPriceInput.Format(minor);
            SetStatus(row, saved + " · " + (minor == 0L ? inherit : row.Buffer));
        }

        private void SetStatus(PriceRow row, string text)
        {
            row.Status = text;
            float width = Mathf.Max(1f, measuredWidth - RUIF.Tokens.ScrollbarThickness - RUIF.Tokens.CardPadX * 2f);
            row.StatusPreview = EconomyTableLayout.DetailSummary(text, width, s => RUIF.Measure.Width(s, GameFont.Tiny));
        }
    }
}
