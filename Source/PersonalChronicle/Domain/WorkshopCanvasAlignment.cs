using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>对齐命中的来源类别（<see cref="CanvasAlignment"/> 的仲裁优先级依据）。</summary>
    public enum CanvasAlignKind : byte
    {
        None = 0,

        /// <summary>命中邻居的最小边线。</summary>
        NeighborMin = 1,

        /// <summary>命中邻居的中心线。</summary>
        NeighborCenter = 2,

        /// <summary>命中邻居的最大边线。</summary>
        NeighborMax = 3,

        /// <summary>命中网格点阵（**只走中心线**）。</summary>
        Grid = 4,

        /// <summary>命中画布原点（**只走中心线**）。</summary>
        Origin = 5,
    }

    /// <summary>
    /// 画布上一个可参与对齐的矩形（**设计单位 AABB**）。
    ///
    /// <para>语义沿用车间俯视画布约定：原点居中、+X→右 / +Y→下、允许负值、**不取整**。
    /// 本类型**只用 float 分量**，刻意不复用引擎的 <c>Rect</c> —— 便于在无 Unity/Verse
    /// 宿主的单测里直接跑（与 <c>WorkshopDeviceFitEvaluator</c> 同范式）。</para>
    /// </summary>
    public readonly struct CanvasAlignRect
    {
        public readonly float MinX;
        public readonly float MinY;
        public readonly float MaxX;
        public readonly float MaxY;

        public CanvasAlignRect(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>由「中心点 + 半尺寸」构造（设备卡就是这种语义：存中心点坐标）。</summary>
        public static CanvasAlignRect FromCenter(float centerX, float centerY,
            float halfWidth, float halfHeight)
        {
            return new CanvasAlignRect(
                centerX - halfWidth, centerY - halfHeight,
                centerX + halfWidth, centerY + halfHeight);
        }

        public float CenterX
        {
            get { return (MinX + MaxX) * 0.5f; }
        }

        public float CenterY
        {
            get { return (MinY + MaxY) * 0.5f; }
        }

        /// <summary>半宽 / 半高（吸附求解需要「中心 + 半尺寸」形态）。</summary>
        public float HalfWidth
        {
            get { return (MaxX - MinX) * 0.5f; }
        }

        public float HalfHeight
        {
            get { return (MaxY - MinY) * 0.5f; }
        }

        /// <summary>
        /// 并集：返回同时包含本矩形与 <paramref name="other"/> 的最小 AABB（**返回新实例**）。
        /// </summary>
        /// <remarks>
        /// 刻意**不就地修改**：本类型是 <c>readonly struct</c>（值语义 + 无防御性拷贝），
        /// 保持 readonly 才能让「拷贝即快照」成立。返回值只有 4 个 float，复制成本可忽略。
        /// </remarks>
        public CanvasAlignRect Union(CanvasAlignRect other)
        {
            return new CanvasAlignRect(
                other.MinX < MinX ? other.MinX : MinX,
                other.MinY < MinY ? other.MinY : MinY,
                other.MaxX > MaxX ? other.MaxX : MaxX,
                other.MaxY > MaxY ? other.MaxY : MaxY);
        }

        /// <summary>
        /// 求一组矩形的 AABB 并集（**纯函数，可离线单测**）。
        /// </summary>
        /// <returns>
        /// <c>false</c> = <paramref name="rects"/> 为 null / 空 ⇒ <paramref name="bounds"/> 无效。
        /// 调用方据此表达「该对象在画布上不可见」⇒ 不应成为对齐候选（否则是幽灵吸附）。
        /// </returns>
        public static bool TryUnionAll(IList<CanvasAlignRect> rects, out CanvasAlignRect bounds)
        {
            bounds = default(CanvasAlignRect);
            if (rects == null || rects.Count == 0)
            {
                return false;
            }
            bounds = rects[0];
            for (int i = 1; i < rects.Count; i++)
            {
                bounds = bounds.Union(rects[i]);
            }
            return true;
        }
    }

    /// <summary>单轴（X 或 Y）的对齐结果。</summary>
    public struct CanvasAlignAxis
    {
        /// <summary>是否命中（<c>false</c> ⇒ 该轴保持自由坐标）。</summary>
        public bool Snapped;

        /// <summary>施加到**移动物中心点**的修正量（设计单位）；未命中恒为 0。</summary>
        public float Delta;

        /// <summary>命中的设计坐标 —— 参考线就画在这条线上。</summary>
        public float Line;

        /// <summary>命中来源。</summary>
        public CanvasAlignKind Kind;

        /// <summary>命中的邻居下标；<c>-1</c> = 网格 / 原点（无邻居来源）。</summary>
        public int SourceIndex;

        public static CanvasAlignAxis Miss
        {
            get
            {
                return new CanvasAlignAxis
                {
                    Snapped = false, Delta = 0f, Line = 0f,
                    Kind = CanvasAlignKind.None, SourceIndex = -1,
                };
            }
        }
    }

    /// <summary>矩形对齐结果（X / Y 两轴独立求解，互不耦合）。</summary>
    public struct CanvasAlignResult
    {
        public CanvasAlignAxis X;
        public CanvasAlignAxis Y;

        public bool Any
        {
            get { return X.Snapped || Y.Snapped; }
        }

        public static CanvasAlignResult None
        {
            get { return new CanvasAlignResult { X = CanvasAlignAxis.Miss, Y = CanvasAlignAxis.Miss }; }
        }
    }

    /// <summary>
    /// 编排画布的**对齐吸附**求值（**纯函数，可离线单测**）。
    ///
    /// <para><b>为什么在 Domain 而不是 UI</b>：对齐是纯标量数学，与 IMGUI 无关。
    /// 阈值换算还依赖缩放系数，放 UI 层就无法在单测里跑。本类**不引用引擎任何类型**
    /// （禁 <c>UnityEngine</c> / <c>Verse</c> / <c>RimWorld</c>），因此
    /// NUnit 可直接调用（与 <c>WorkshopDeviceFitEvaluator</c> / <c>WorkshopCrewCycle</c> 同范式）。</para>
    ///
    /// <para><b>候选线</b>：每邻居每轴各 3 条（min / center / max），移动物用**同名线**去匹配；
    /// 网格与原点只提供**中心线**。矩形对齐由此分解为两个独立的一维问题 ⇒ O(N)，
    /// <b>不做</b> O(N²) 候选线配对。</para>
    ///
    /// <para><b>仲裁是确定性的</b>：同距离时按 <see cref="CanvasAlignKind"/> 序号、再按坐标较小者裁决，
    /// 因此「同一输入永远同一输出」—— 这既消除了临界点抖动，也让行为可单测复现。
    /// 由此**不需要迟滞带（hysteresis）**。</para>
    ///
    /// <para>调用方（UI）**禁止**自行实现任何 <c>|a-b| &lt; T</code> 形式的吸附判断；
    /// 唯一入口是 <see cref="Resolve"/>。</para>
    /// </summary>
    public static class CanvasAlignment
    {
        /// <summary>吸附阈值的目标屏幕距离（像素）—— 缩放无关的手感常量。</summary>
        public const float SnapScreenPx = 8f;

        /// <summary>阈值下限（设计单位）：防止极高缩放下阈值趋零而完全吸不上。</summary>
        public const float MinSnapDesign = 1f;

        /// <summary>
        /// 阈值上限（设计单位）= 设备卡高的一半。低于 50% 缩放时阈值会被此上限截断，
        /// 吸附退化为**近似**对齐（此时卡宽仅十余像素，精确对齐无实际意义）—— 属如实降级。
        /// </summary>
        public const float MaxSnapDesign = 30f;

        /// <summary>并列判定容差（设计单位）：小于此差视作"等距"，转由来源优先级裁决。</summary>
        private const float TieEps = 1e-3f;

        private static float Abs(float v)
        {
            return v < 0f ? -v : v;
        }

        private static int Rank(CanvasAlignKind kind)
        {
            return (int)kind;
        }

        /// <summary>
        /// 屏幕像素阈值 → 设计单位阈值（**zoom 感知**）。
        /// </summary>
        /// <param name="screenPx">目标屏幕距离（通常取 <see cref="SnapScreenPx"/>）。</param>
        /// <param name="k">设计单位 → 屏幕像素的缩放系数。</param>
        /// <param name="minDesign">阈值下限。</param>
        /// <param name="maxDesign">阈值上限。</param>
        /// <remarks>
        /// 结果被 <paramref name="minDesign"/> / <paramref name="maxDesign"/> 双向钳制。
        /// 在 <c>k ∈ [0.333, 2.667]</c>（约 50%~400% 缩放）区间内，恒等于
        /// <paramref name="screenPx"/> 像素；更低缩放下被上限截断（近似对齐）。
        /// </remarks>
        public static float ScreenThresholdToDesign(float screenPx, float k,
            float minDesign, float maxDesign)
        {
            float scale = k > 0.01f ? k : 0.01f;
            float t = screenPx / scale;
            if (t < minDesign)
            {
                t = minDesign;
            }
            if (t > maxDesign)
            {
                t = maxDesign;
            }
            return t;
        }

        /// <summary>
        /// 求一个矩形的对齐修正量。
        /// </summary>
        /// <param name="neighbors">候选邻居 AABB 列表（画布上其它已放置对象）。</param>
        /// <param name="skipIndex">要跳过的邻居下标（**被拖动的对象自身**，-1 = 不跳过）。</param>
        /// <param name="centerX">移动物中心 X（设计单位）。</param>
        /// <param name="centerY">移动物中心 Y（设计单位）。</param>
        /// <param name="halfWidth">移动物半宽。</param>
        /// <param name="halfHeight">移动物半高。</param>
        /// <param name="thresholdDesign">吸附阈值（设计单位，见 <see cref="ScreenThresholdToDesign"/>）。</param>
        /// <param name="gridEnabled">是否启用网格吸附（与网格**显示**是两个独立开关）。</param>
        /// <param name="gridStep">网格步长（设计单位），<paramref name="gridEnabled"/> 为 true 时有效。</param>
        /// <param name="originEnabled">是否启用画布原点对齐。</param>
        public static CanvasAlignResult Resolve(
            IList<CanvasAlignRect> neighbors,
            int skipIndex,
            float centerX, float centerY,
            float halfWidth, float halfHeight,
            float thresholdDesign,
            bool gridEnabled, float gridStep,
            bool originEnabled)
        {
            float minX = centerX - halfWidth;
            float minY = centerY - halfHeight;
            float maxX = centerX + halfWidth;
            float maxY = centerY + halfHeight;

            CanvasAlignAxis x = ResolveAxis(
                minX, maxX, neighbors, skipIndex, false, thresholdDesign, gridEnabled, gridStep,
                originEnabled);
            CanvasAlignAxis y = ResolveAxis(
                minY, maxY, neighbors, skipIndex, true, thresholdDesign, gridEnabled, gridStep,
                originEnabled);

            return new CanvasAlignResult { X = x, Y = y };
        }

        /// <summary>
        /// 单轴求解。<paramref name="useY"/> = true 取 Y 分量，false 取 X 分量。
        /// </summary>
        public static CanvasAlignAxis ResolveAxis(
            float movingMin, float movingMax,
            IList<CanvasAlignRect> neighbors, int skipIndex,
            bool useY, float thresholdDesign,
            bool gridEnabled, float gridStep, bool originEnabled)
        {
            float movingCenter = (movingMin + movingMax) * 0.5f;
            CanvasAlignAxis best = CanvasAlignAxis.Miss;

            if (neighbors != null)
            {
                for (int i = 0; i < neighbors.Count; i++)
                {
                    if (i == skipIndex)
                    {
                        continue;
                    }
                    CanvasAlignRect n = neighbors[i];
                    float nMin = useY ? n.MinY : n.MinX;
                    float nCenter = useY ? n.CenterY : n.CenterX;
                    float nMax = useY ? n.MaxY : n.MaxX;

                    float minDelta = nMin - movingMin;
                    float centerDelta = nCenter - movingCenter;
                    float maxDelta = nMax - movingMax;

                    if (Abs(minDelta) <= thresholdDesign)
                    {
                        Consider(ref best, minDelta, nMin, CanvasAlignKind.NeighborMin, i);
                    }
                    if (Abs(centerDelta) <= thresholdDesign)
                    {
                        Consider(ref best, centerDelta, nCenter, CanvasAlignKind.NeighborCenter, i);
                    }
                    if (Abs(maxDelta) <= thresholdDesign)
                    {
                        Consider(ref best, maxDelta, nMax, CanvasAlignKind.NeighborMax, i);
                    }
                }
            }

            if (gridEnabled && gridStep > 0.01f)
            {
                float line = RoundTo(movingCenter, gridStep);
                float delta = line - movingCenter;
                if (Abs(delta) <= thresholdDesign)
                {
                    Consider(ref best, delta, line, CanvasAlignKind.Grid, -1);
                }
            }

            if (originEnabled)
            {
                float delta = 0f - movingCenter;
                if (Abs(delta) <= thresholdDesign)
                {
                    Consider(ref best, delta, 0f, CanvasAlignKind.Origin, -1);
                }
            }

            return best;
        }

        private static float RoundTo(float value, float step)
        {
            return (float)System.Math.Round(value / step, System.MidpointRounding.AwayFromZero) * step;
        }

        private static void Consider(ref CanvasAlignAxis best, float delta, float line,
            CanvasAlignKind kind, int sourceIndex)
        {
            float ad = Abs(delta);
            if (best.Snapped)
            {
                float bd = Abs(best.Delta);
                if (ad > bd + TieEps)
                {
                    return;     // 明显更差
                }
                if (ad >= bd - TieEps)
                {
                    // 等距（或同为 0）⇒ 按来源优先级，再按坐标较小者裁决（确定性）。
                    // 同为 0 时也走这条路径：已对齐状态必须稳定，否则会在临界点跳变。
                    int rank = Rank(kind);
                    int bestRank = Rank(best.Kind);
                    if (rank > bestRank)
                    {
                        return;
                    }
                    if (rank == bestRank && line >= best.Line)
                    {
                        return;
                    }
                }
            }

            best.Snapped = true;
            best.Delta = delta;
            best.Line = line;
            best.Kind = kind;
            best.SourceIndex = sourceIndex;
        }
    }
}
