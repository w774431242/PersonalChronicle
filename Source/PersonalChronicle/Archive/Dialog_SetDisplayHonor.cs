using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// 人物显示称号选择：从已获职称与勋章中选一条，写入 CareerData.DisplayedHonorKey。
    /// 地图名牌与殖民者栏只渲染这一条，避免多条荣誉叠加。
    /// </summary>
    public class Dialog_SetDisplayHonor : Window
    {
        private const float TitleH = 28f;
        private const float WinW = 440f;
        private const float WinH = 480f;
        private const float CardH = 44f;
        private const float CardGap = 6f;
        private const float FooterH = 30f;
        private const float TipH = 18f;

        private readonly Pawn pawn;
        private readonly System.Action onCommitted;
        private readonly List<HonorChoice> choices = new List<HonorChoice>();
        private string currentKey;
        private Vector2 scroll;
        private string tip;

        public override Vector2 InitialSize
        {
            get { return new Vector2(WinW, WinH); }
        }

        /// <summary>当前显示称号的短标签（按钮文案）；无档案返回「自动」。</summary>
        public static string CurrentLabel(Pawn pawn)
        {
            PawnObject po = ChroniclePawnAccess.GetArchivedPawnObject(pawn);
            if (po == null || po.CareerData == null)
            {
                return "PersonalChronicle.UI.Career.Honor.Select.Current.Auto".Translate();
            }
            string key = po.CareerData.DisplayedHonorKey;
            if (HonorDisplayKeys.IsNone(key))
            {
                return "PersonalChronicle.UI.Career.Honor.Select.Current.None".Translate();
            }
            string titleDefName;
            string medalDefName;
            if (HonorDisplayKeys.TryParseTitle(key, out titleDefName))
            {
                string tKey = "PersonalChronicle.Professional.Title." + titleDefName + ".Label";
                if (tKey.CanTranslate())
                {
                    return tKey.Translate();
                }
                ProfessionalTitleDef tDef = DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(titleDefName);
                return tDef != null ? tDef.LabelCap.ToString() : titleDefName;
            }
            if (HonorDisplayKeys.TryParseMedal(key, out medalDefName))
            {
                string mKey = MedalTranslationKeys.Label(medalDefName);
                if (mKey.CanTranslate())
                {
                    return mKey.Translate();
                }
                MedalDef mDef = DefDatabase<MedalDef>.GetNamedSilentFail(medalDefName);
                return mDef != null ? mDef.LabelCap.ToString() : medalDefName;
            }
            return "PersonalChronicle.UI.Career.Honor.Select.Current.Auto".Translate();
        }

        public Dialog_SetDisplayHonor(Pawn pawn, System.Action onCommitted)
        {
            this.pawn = pawn;
            this.onCommitted = onCommitted;
            this.scroll = Vector2.zero;
            this.tip = string.Empty;
            PawnObject po = ChroniclePawnAccess.GetArchivedPawnObject(pawn);
            this.currentKey = po != null && po.CareerData != null ? po.CareerData.DisplayedHonorKey : null;
            RefreshChoices(po);
            doCloseX = true;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            drawShadow = true;
            forcePause = true;
        }

        private sealed class HonorChoice
        {
            public string Key;
            public string Label;
            public string Group;
        }

        private void RefreshChoices(PawnObject po)
        {
            choices.Clear();
            choices.Add(new HonorChoice
            {
                Key = null,
                Label = "PersonalChronicle.UI.Career.Honor.Select.Auto".Translate(),
                Group = null
            });
            choices.Add(new HonorChoice
            {
                Key = HonorDisplayKeys.None,
                Label = "PersonalChronicle.UI.Career.Honor.Select.None".Translate(),
                Group = null
            });
            if (po == null)
            {
                return;
            }
            CareerData cd = po.CareerData;
            if (cd != null && cd.GrantedTitles != null)
            {
                string groupTitle = "PersonalChronicle.UI.Career.Honor.Select.Group.Title".Translate();
                HashSet<string> seen = new HashSet<string>();
                for (int i = 0; i < cd.GrantedTitles.Count; i++)
                {
                    GrantedTitle g = cd.GrantedTitles[i];
                    if (g == null || string.IsNullOrEmpty(g.TitleDefName) || !seen.Add(g.TitleDefName))
                    {
                        continue;
                    }
                    ProfessionalTitleDef def = DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(g.TitleDefName);
                    string key = "PersonalChronicle.Professional.Title." + g.TitleDefName + ".Label";
                    string label = key.CanTranslate()
                        ? key.Translate().ToString()
                        : (def != null ? def.LabelCap.ToString() : g.TitleDefName);
                    choices.Add(new HonorChoice
                    {
                        Key = HonorDisplayKeys.ForTitle(g.TitleDefName),
                        Label = label,
                        Group = groupTitle
                    });
                }
            }
            if (po.GrantedMedals != null)
            {
                string groupMedal = "PersonalChronicle.UI.Career.Honor.Select.Group.Medal".Translate();
                HashSet<string> seen = new HashSet<string>();
                for (int i = 0; i < po.GrantedMedals.Count; i++)
                {
                    string name = po.GrantedMedals[i];
                    if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    {
                        continue;
                    }
                    string key = MedalTranslationKeys.Label(name);
                    MedalDef def = DefDatabase<MedalDef>.GetNamedSilentFail(name);
                    string label = key.CanTranslate()
                        ? key.Translate().ToString()
                        : (def != null ? def.LabelCap.ToString() : name);
                    choices.Add(new HonorChoice
                    {
                        Key = HonorDisplayKeys.ForMedal(name),
                        Label = label,
                        Group = groupMedal
                    });
                }
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            UIComponents.TintedBox(inRect, UITheme.Window);

            float y = inRect.y;
            Rect titleRect = new Rect(inRect.x, y, inRect.width, TitleH);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, "PersonalChronicle.UI.Career.Honor.Select.Title".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            y += TitleH + 4f;

            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                GUI.color = UITheme.Muted;
                Text.Font = GameFont.Tiny;
                float hintH = Text.CalcHeight("PersonalChronicle.UI.Career.Honor.Select.Hint".Translate(), inRect.width);
                Rect hintRect = new Rect(inRect.x, y, inRect.width, Mathf.Max(16f, hintH));
                Widgets.Label(hintRect, "PersonalChronicle.UI.Career.Honor.Select.Hint".Translate());
                y += hintRect.height + 6f;
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }

            Rect bodyRect = new Rect(inRect.x, y, inRect.width, inRect.height - (y - inRect.y) - FooterH - TipH);
            if (choices.Count <= 2 && (pawn == null))
            {
                DrawCenteredNote(bodyRect, "PersonalChronicle.UI.Career.Honor.Select.Empty".Translate());
                DrawTip(inRect, inRect.yMax - FooterH - TipH, inRect.width);
                return;
            }

            float viewH = choices.Count * (CardH + CardGap);
            Rect viewRect = new Rect(0f, 0f, bodyRect.width - 16f, viewH);
            Widgets.BeginScrollView(new Rect(bodyRect.x, bodyRect.y, bodyRect.width, bodyRect.height), ref scroll, viewRect);
            float cy = 0f;
            for (int i = 0; i < choices.Count; i++)
            {
                HonorChoice c = choices[i];
                if (c == null)
                {
                    continue;
                }
                DrawChoiceCard(new Rect(0f, cy, viewRect.width, CardH), c);
                cy += CardH + CardGap;
            }
            Widgets.EndScrollView();
            DrawTip(inRect, inRect.yMax - FooterH - TipH, inRect.width);
        }

        private void DrawChoiceCard(Rect card, HonorChoice choice)
        {
            bool selected = KeysEqual(currentKey, choice.Key);
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                if (Widgets.ButtonInvisible(card))
                {
                    TrySelect(choice);
                    Event.current.Use();
                }
                UIComponents.TintedBox(card, selected ? UITheme.PanelRaised : UITheme.Panel);
                UIComponents.Border(card, selected ? UITheme.PillGold : UITheme.BorderSoft);
                Widgets.DrawHighlightIfMouseover(card);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = selected ? UITheme.Text : UITheme.Muted;
                Rect labelRect = new Rect(card.x + 12f, card.y + 6f, card.width - 40f, 20f);
                Widgets.Label(labelRect, choice.Label);

                if (!string.IsNullOrEmpty(choice.Group))
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = UITheme.Dim;
                    Widgets.Label(new Rect(card.x + 12f, card.y + 24f, card.width - 40f, 16f), choice.Group);
                }

                if (selected)
                {
                    Rect chk = new Rect(card.x + card.width - 26f, card.y + 8f, 18f, 18f);
                    GUI.color = UITheme.PillGold;
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(chk, "PersonalChronicle.UI.Career.SetDirection.Selected".Translate());
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private void TrySelect(HonorChoice choice)
        {
            if (choice == null || pawn == null)
            {
                return;
            }
            if (!ChroniclePawnAccess.SetDisplayedHonor(pawn, choice.Key))
            {
                tip = "PersonalChronicle.UI.Career.Honor.Select.NoCareer".Translate();
                return;
            }
            currentKey = choice.Key;
            if (HonorDisplayKeys.IsNone(choice.Key))
            {
                tip = "PersonalChronicle.UI.Career.Honor.Select.Hidden".Translate();
            }
            else if (HonorDisplayKeys.IsAuto(choice.Key))
            {
                tip = "PersonalChronicle.UI.Career.Honor.Select.Cleared".Translate();
            }
            else
            {
                tip = "PersonalChronicle.UI.Career.Honor.Select.Done".Translate(choice.Label);
            }
            if (onCommitted != null)
            {
                onCommitted();
            }
        }

        private static bool KeysEqual(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
            {
                return true;
            }
            return string.Equals(a, b, System.StringComparison.Ordinal);
        }

        private void DrawCenteredNote(Rect rect, string text)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                GUI.color = UITheme.Dim;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, text);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private void DrawTip(Rect inRect, float y, float width)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = UITheme.Dim;
                Widgets.Label(new Rect(inRect.x, y, width, TipH + FooterH), tip ?? string.Empty);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }
    }
}
