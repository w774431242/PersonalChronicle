using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// Def 选择窗口通用骨架（农机的「作物选择」「改造地形选择」共用）。
    ///
    /// 布局：左侧「搜索框 + 图标网格」，右侧「名称 + 信息卡 + 详情面板」。
    ///
    /// 继承原版 <c>RimWorld.Dialog_Search&lt;T&gt;</c>（public abstract 泛型 Window），复用其原生搜索控件
    /// <see cref="QuickSearchWidget"/> 与高亮状态（<c>Highlighted</c>）。基类默认只渲染竖排列表、且每次
    /// 搜索都会按结果条数重排窗口，因此这里重写 <c>DoWindowContents</c> 换成图标网格，并重写
    /// <c>SetInitialSizeAndPosition</c> 固定窗口尺寸。
    ///
    /// 子类只提供数据：<see cref="Collect"/>（候选集）、<see cref="SearchText"/>（搜索字段）、
    /// <see cref="PassesFilter"/>（额外门控）、<see cref="DrawDetails"/>（右侧详情）、
    /// <see cref="OnPicked"/>（选中回调）。⚠️ <see cref="Collect"/> 在基类构造器里被调用，
    /// 只能依赖静态数据与 DefDatabase，不得使用子类构造器主体里才赋值的实例字段。
    ///
    /// 全部使用原版资产与 API（<see cref="Widgets.DefIcon"/> / <see cref="QuickSearchFilter"/>），
    /// 不依赖任何第三方 mod（RimHUD 等一律不引用）。
    /// </summary>
    public abstract class Dialog_DefPicker<T> : Dialog_Search<T> where T : Def
    {
        private const float SearchH = 24f;
        private const float LabelW = 92f;
        private const float CellW = 76f;
        private const float CellH = 78f;
        private const float IconSize = 40f;
        private const float DescW = 272f;
        private const float Gap = 8f;
        private const float HeadH = 30f;

        private readonly Action onFilterChanged;
        private readonly List<T> all;
        private readonly List<T> visible = new List<T>();
        protected Vector2 descScroll = Vector2.zero;

        protected Dialog_DefPicker(T current)
        {
            onFilterChanged = Notify_CommonSearchChanged;
            all = Collect();
            highlightedElement = current;
            RebuildVisible();

            doCloseX = true;
            draggable = true;
            forcePause = true;
            preventCameraMotion = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        // ===== 子类契约 =====

        /// <summary>候选全集（已排序）。⚠️ 在基类构造器内调用，只准用静态数据。</summary>
        protected abstract List<T> Collect();

        /// <summary>参与搜索匹配的文本（通常 label，可再拼 defName）。</summary>
        protected abstract string SearchText(T element);

        /// <summary>是否列入候选（研究解锁等门控；搜索过滤之外的第二道闸）。</summary>
        protected abstract bool PassesFilter(T element);

        protected abstract TaggedString TitleText { get; }

        /// <summary>未悬停任何条目时右侧面板的提示文案。</summary>
        protected abstract TaggedString HintText { get; }

        protected abstract TaggedString NoMatchText { get; }

        /// <summary>右侧详情（属性行 + 说明）。rect 从标题行下方一直到底。</summary>
        protected abstract void DrawDetails(T element, Rect rect);

        protected abstract void OnPicked(T element);

        /// <summary>是否显示原版信息卡按钮（右下角 i）。</summary>
        protected virtual bool ShowInfoCard(T element) => true;

        // ===== Dialog_Search<T> 契约 =====

        protected override TaggedString SearchLabel => TitleText;

        protected override List<T> SearchSet => all;

        protected override bool ShouldClose => false;

        protected override bool ShouldSkipElement(T element) => false;

        protected override void OnHighlightUpdate(T element)
        {
            // 选择器只改本地状态，不需要像原版地图搜索那样把镜头跳到目标上。
        }

        protected override void DoIcon(T element, Rect iconRect) => Widgets.DefIcon(iconRect, element);

        protected override void DoLabel(T element, Rect labelRect) => Widgets.Label(labelRect, element.LabelCap);

        protected override void ClikedOnElement(T element)
        {
            if (element == null)
            {
                return;
            }
            OnPicked(element);
            Close();
        }

        protected override void TryAddElement(T element)
        {
            if (searchResultsSet.Contains(element) || !TextMatch(SearchText(element)))
            {
                return;
            }
            searchResults.Add(UniqueKey(element), element);
            searchResultsSet.Add(element);
        }

        protected override void TryRemoveElement(T element)
        {
            if (!searchResultsSet.Remove(element))
            {
                return;
            }
            int index = searchResults.IndexOfValue(element);
            if (index >= 0)
            {
                searchResults.RemoveAt(index);
            }
        }

        // ===== 生命周期与布局 =====

        public override void PostOpen()
        {
            base.PostOpen();
            quickSearchWidget.Focus();
        }

        public override void Notify_CommonSearchChanged()
        {
            base.Notify_CommonSearchChanged();
            RebuildVisible();
        }

        /// <summary>固定窗口尺寸：基类会按结果条数重排 windowRect，网格布局下必须关掉。</summary>
        protected override void SetInitialSizeAndPosition()
        {
            Vector2 size = InitialSize;
            windowRect = new Rect(
                (UI.screenWidth - size.x) / 2f,
                (UI.screenHeight - size.y) / 2f,
                size.x,
                size.y).Rounded();
        }

        protected virtual Vector2 PickerSize => new Vector2(760f, 540f);

        public override Vector2 InitialSize => PickerSize;

        public override void DoWindowContents(Rect inRect)
        {
            float descW = Mathf.Min(DescW, inRect.width * 0.4f);
            Rect left = new Rect(inRect.x, inRect.y, inRect.width - descW - Gap, inRect.height);
            Rect right = new Rect(left.xMax + Gap, inRect.y, descW, inRect.height);

            DrawSearchRow(left);
            Rect gridRect = new Rect(left.x, left.y + SearchH + Gap, left.width, left.height - SearchH - Gap);
            DrawGrid(gridRect);
            DrawDescription(right);
        }

        /// <summary>左侧顶栏：标题 + 原生搜索控件。</summary>
        private void DrawSearchRow(Rect left)
        {
            Rect labelRect = new Rect(left.x, left.y, LabelW, SearchH);
            Rect searchRect = new Rect(labelRect.xMax + 6f, left.y, left.width - LabelW - 6f, SearchH);
            TextAnchor anchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, SearchLabel);
            }
            finally
            {
                Text.Anchor = anchor;
            }
            quickSearchWidget.OnGUI(searchRect, onFilterChanged, onFilterChanged);
        }

        private void DrawGrid(Rect outRect)
        {
            if (visible.Count == 0)
            {
                DrawCentered(outRect, NoMatchText);
                return;
            }
            int cols = Mathf.Max(1, Mathf.FloorToInt((outRect.width - 16f) / CellW));
            int rows = Mathf.CeilToInt((float)visible.Count / cols);
            Rect viewRect = new Rect(0f, 0f, cols * CellW, Mathf.Max(rows * CellH, outRect.height));
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect, true);
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                UpdateHighlight(cols);
                for (int i = 0; i < visible.Count; i++)
                {
                    DrawCell(visible[i], i % cols, i / cols);
                }
            }
            finally
            {
                Text.Font = font;
                Text.Anchor = anchor;
                Widgets.EndScrollView();
            }
        }

        /// <summary>先定高亮再统一绘制，避免同一帧出现两个高亮格。
        /// 必须在 BeginScrollView 内调用，否则鼠标坐标不在滚动组空间里。</summary>
        private void UpdateHighlight(int cols)
        {
            for (int i = 0; i < visible.Count; i++)
            {
                if (Mouse.IsOver(CellRect(i, cols)))
                {
                    highlightedElement = visible[i];
                    return;
                }
            }
        }

        private static Rect CellRect(int index, int cols)
        {
            return new Rect((index % cols) * CellW, (index / cols) * CellH, CellW, CellH);
        }

        private void DrawCell(T def, int col, int row)
        {
            Rect cell = new Rect(col * CellW, row * CellH, CellW, CellH);
            Rect iconRect = new Rect(cell.x + (CellW - IconSize) / 2f, cell.y + 4f, IconSize, IconSize);
            Rect labelRect = new Rect(cell.x + 3f, iconRect.yMax + 2f, CellW - 6f, 30f);
            if (def == Highlighted)
            {
                Widgets.DrawHighlightSelected(cell);
            }
            Widgets.DefIcon(iconRect, def);
            Widgets.Label(labelRect, def.LabelCap);
            if (Widgets.ButtonInvisible(cell, false))
            {
                ClikedOnElement(def);
            }
        }

        /// <summary>右侧详情面板外壳：名称 + 信息卡，内容交给 <see cref="DrawDetails"/>。</summary>
        private void DrawDescription(Rect rect)
        {
            T def = Highlighted;
            if (def == null)
            {
                DrawCentered(rect, HintText);
                return;
            }
            Widgets.DrawLineVertical(rect.x - Gap / 2f, rect.y, rect.height);
            Rect head = new Rect(rect.x, rect.y, rect.width - 28f, HeadH);
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(head, def.LabelCap);
                DrawDetails(def, new Rect(rect.x, head.yMax + 4f, rect.width, rect.yMax - head.yMax - 4f));
            }
            finally
            {
                Text.Font = font;
                Text.Anchor = anchor;
            }
            if (ShowInfoCard(def))
            {
                Widgets.InfoCardButton(new Rect(rect.xMax - 24f, rect.y + 3f, 24f, 24f), def);
            }
        }

        // ===== 供子类复用的绘制/数据工具 =====

        /// <summary>按当前搜索词重建可见列表（候选量级几十个，直接全量过滤，不依赖基类增量索引）。</summary>
        private void RebuildVisible()
        {
            visible.Clear();
            QuickSearchFilter filter = quickSearchWidget.filter;
            bool active = filter.Active;
            for (int i = 0; i < all.Count; i++)
            {
                T def = all[i];
                if (PassesFilter(def) && (!active || filter.Matches(SearchText(def)) || filter.Matches(def.defName)))
                {
                    visible.Add(def);
                }
            }
        }

        private string UniqueKey(T def)
        {
            string key = def.label.ToLower();
            return searchResults.ContainsKey(key) ? key + " " + def.defName : key;
        }

        protected static void DrawStatRow(Rect rect, int index, float rowH, TaggedString label, TaggedString value)
        {
            float y = rect.y + index * rowH;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, y, rect.width * 0.55f, rowH), label);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rect.x + rect.width * 0.55f, y, rect.width * 0.45f, rowH), value);
        }

        protected static void DrawCentered(Rect rect, TaggedString text)
        {
            TextAnchor anchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, text);
            }
            finally
            {
                Text.Anchor = anchor;
            }
        }

        /// <summary>取翻译并按 {0}/{1} 占位符回填。
        /// 不用 <c>Translate(params object[])</c>——它在 1.6 已标记过时。
        /// 注意方法名不能叫 T：与泛型参数 T 冲突（CS0102）。</summary>
        protected static TaggedString Tr(string key, params object[] args)
        {
            string raw = key.Translate().ToString();
            return args.Length == 0 ? (TaggedString)raw : string.Format(raw, args);
        }

        protected static string Num(float value) => value.ToString("0.##");

        /// <summary>研究前置全部完成才可选（未解锁的候选直接不列出，防绕过研究）。</summary>
        protected static bool ResearchUnlocked(List<ResearchProjectDef> reqs)
        {
            if (reqs == null)
            {
                return true;
            }
            for (int i = 0; i < reqs.Count; i++)
            {
                if (reqs[i] == null || !reqs[i].IsFinished)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
