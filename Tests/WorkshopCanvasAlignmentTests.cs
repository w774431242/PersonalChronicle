using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 编排画布「对齐吸附」纯函数的离线单测。
    ///
    /// <para>为什么这些语义必须钉死：吸附把「随手一拖」变成「反复微调对齐」，
    /// 一旦阈值换算漂移，玩家看到的是「吸上了但位置不对」；一旦仲裁不确定，
    /// 表现是卡片在临界点来回跳 —— 两者都无法靠观感察觉、只能靠单测拦。</para>
    ///
    /// <para>覆盖：阈值换算 / 同名线匹配 / 阈值门 / 优先级仲裁 / 并列确定性 /
    /// 自身排除 / 网格与原点 / 退化输入。</para>
    ///
    /// <para><b>测试构造要点</b>：画布两个轴独立求解，验证 X 时必须把 Y 拉远
    /// （否则 Y 会同时命中、用例断言互相污染）；要测<b>某一条具体线</b>（min/center/max）
    /// 必须用<b>不同尺寸</b>的邻居 —— 等尺寸卡的三条线 Δ 恒等，永远由 NeighborMin 胜出。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopCanvasAlignmentTests
    {
        private const float HalfW = 52f;   // 设备卡 104 宽的一半
        private const float HalfH = 30f;   // 设备卡 60 高的一半
        private const float Far = 1000f;   // 把某一轴拉远，使该轴必不命中

        private static List<CanvasAlignRect> Rects(params CanvasAlignRect[] items)
        {
            return new List<CanvasAlignRect>(items);
        }

        /// <summary>等尺寸设备卡（104×60）的 AABB。</summary>
        private static CanvasAlignRect At(float cx, float cy)
        {
            return CanvasAlignRect.FromCenter(cx, cy, HalfW, HalfH);
        }

        /// <summary>任意尺寸的邻居（用于让 min/center/max 三条线的 Δ 产生差异）。</summary>
        private static CanvasAlignRect Sized(float cx, float cy, float halfW, float halfH)
        {
            return CanvasAlignRect.FromCenter(cx, cy, halfW, halfH);
        }

        // ================= 阈值换算（zoom 感知） =================

        [Test]
        public void ScreenThresholdToDesign_InMainZoomBand_StaysEightPixels()
        {
            // 设计范围 900 宽 ⇒ k = viewport.width * Zoom / 900。取 viewport.width=600。
            float[] zooms = { 0.5f, 1f, 2f, 4f };
            foreach (float z in zooms)
            {
                float k = 600f * z / 900f;
                float t = CanvasAlignment.ScreenThresholdToDesign(CanvasAlignment.SnapScreenPx, k,
                    CanvasAlignment.MinSnapDesign, CanvasAlignment.MaxSnapDesign);
                Assert.AreEqual(CanvasAlignment.SnapScreenPx, t * k, 0.001f,
                    "缩放 " + z + " 下阈值必须恒为 8 屏幕像素（手感不随缩放漂移）");
            }
        }

        [Test]
        public void ScreenThresholdToDesign_BelowHalfZoom_ClampsToMax_DegradesGracefully()
        {
            // 25% 缩放：8px 换算成 48 设计单位 > 上限 30 ⇒ 截断 ⇒ 如实降级为近似对齐。
            float k = 600f * 0.25f / 900f;
            float t = CanvasAlignment.ScreenThresholdToDesign(CanvasAlignment.SnapScreenPx, k,
                CanvasAlignment.MinSnapDesign, CanvasAlignment.MaxSnapDesign);
            Assert.AreEqual(CanvasAlignment.MaxSnapDesign, t, 0.001f,
                "低于 50% 缩放必须被上限截断（不得宣称全区间恒定）");
            Assert.Less(t * k, CanvasAlignment.SnapScreenPx,
                "截断后实际屏幕距离必然小于目标值 —— 这就是降级，必须如实记录");
        }

        [Test]
        public void ScreenThresholdToDesign_NearZeroScale_StaysFinite_NoNaNOrInfinity()
        {
            foreach (float k in new[] { 0f, -1f, 0.0001f })
            {
                float t = CanvasAlignment.ScreenThresholdToDesign(CanvasAlignment.SnapScreenPx, k,
                    CanvasAlignment.MinSnapDesign, CanvasAlignment.MaxSnapDesign);
                Assert.IsFalse(float.IsNaN(t), "k=" + k + " 不得产出 NaN");
                Assert.IsFalse(float.IsInfinity(t), "k=" + k + " 不得产出 Infinity");
                Assert.GreaterOrEqual(t, CanvasAlignment.MinSnapDesign);
                Assert.LessOrEqual(t, CanvasAlignment.MaxSnapDesign);
            }
        }

        // ================= 同名线匹配 =================

        [Test]
        public void Resolve_EqualSizeNeighbor_IsRigidTranslation_MinLineWins()
        {
            // 等尺寸 ⇒ 三条线 Δ 恒等（-28），按来源优先级 NeighborMin 胜出。
            // 这不是巧合而是必然：同尺寸卡对齐本质是整体平移。
            List<CanvasAlignRect> n = Rects(At(0f, 0f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 28f, Far, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsTrue(r.X.Snapped);
            Assert.AreEqual(CanvasAlignKind.NeighborMin, r.X.Kind);
            Assert.AreEqual(-28f, r.X.Delta, 0.001f);
            Assert.AreEqual(-52f, r.X.Line, 0.001f);
            Assert.IsFalse(r.Y.Snapped, "Y 已拉远 1000，不该命中");
        }

        [Test]
        public void Resolve_DeltaIsCorrectionToCenter_NotToEdge()
        {
            List<CanvasAlignRect> n = Rects(At(0f, 0f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 28f, Far, HalfW, HalfH, 30f,
                false, 50f, false);

            float newCenter = 28f + r.X.Delta;
            Assert.AreEqual(-52f, newCenter - HalfW, 0.001f,
                "Delta 必须能直接加到中心点上（UI 不得再自行换算）");
        }

        [Test]
        public void Resolve_CenterLineWins_WhenCloserThanMinLine()
        {
            // 邻居半宽 20 ⇒ 线 -20 / 0 / +20；移动中心 9 ⇒ MinX=-43 / MaxX=61
            //   minDelta    = -20-(-43) = +23
            //   centerDelta = 0-9       =  -9   ← 更近，胜
            //   maxDelta    = 20-61     = -41   ← 超阈值
            List<CanvasAlignRect> n = Rects(Sized(0f, 0f, 20f, 30f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 9f, Far, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsTrue(r.X.Snapped);
            Assert.AreEqual(CanvasAlignKind.NeighborCenter, r.X.Kind);
            Assert.AreEqual(-9f, r.X.Delta, 0.001f);
            Assert.AreEqual(0f, r.X.Line, 0.001f);
        }

        [Test]
        public void Resolve_MaxLineWins_WhenMinAndCenterOutOfRange()
        {
            // 邻居半宽 80 ⇒ 线 -80 / 0 / +80；移动中心 32 ⇒ MinX=-20 / MaxX=84
            //   minDelta = -80-(-20) = -60 ✘   centerDelta = 0-32 = -32 ✘   maxDelta = 80-84 = -4 ✔
            List<CanvasAlignRect> n = Rects(Sized(0f, 0f, 80f, 30f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 32f, Far, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsTrue(r.X.Snapped);
            Assert.AreEqual(CanvasAlignKind.NeighborMax, r.X.Kind);
            Assert.AreEqual(-4f, r.X.Delta, 0.001f);
            Assert.AreEqual(80f, r.X.Line, 0.001f);
        }

        // ================= 阈值门 =================

        [Test]
        public void Resolve_BeyondThreshold_KeepsFreeCoordinate()
        {
            List<CanvasAlignRect> n = Rects(At(0f, 0f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 252f, Far, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsFalse(r.X.Snapped, "超出阈值的候选必须被拒绝（画布是自由坐标模型）");
            Assert.IsFalse(r.Any);
        }

        [Test]
        public void Resolve_ExactlyAtThreshold_StillSnaps()
        {
            // 移动中心 30 ⇒ MinX=-22 ⇒ minDelta = -52-(-22) = -30（恰在阈值上）
            List<CanvasAlignRect> n = Rects(At(0f, 0f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 30f, Far, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsTrue(r.X.Snapped, "阈值边界取闭区间（否则手感在临界点断续）");
            Assert.AreEqual(-30f, r.X.Delta, 0.001f);
        }

        [Test]
        public void Resolve_OneUnitBeyondThreshold_DoesNotSnap()
        {
            List<CanvasAlignRect> n = Rects(At(0f, 0f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 31f, Far, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsFalse(r.X.Snapped, "阈值外一单位即自由坐标（不得有隐藏容差）");
        }

        // ================= 优先级仲裁 =================

        [Test]
        public void Resolve_DistanceBeatsKindRank_GridBeatsCloserRankingNeighbor()
        {
            // 移动中心 9；邻居中心 0 ⇒ Δ=-9（NeighborCenter，rank 2）
            //           网格 step=10 ⇒ 点 10 ⇒ Δ=+1（Grid，rank 4）
            // ⇒ 严格更近者胜，即使来源优先级更低。
            List<CanvasAlignRect> n = Rects(Sized(0f, 0f, 20f, 30f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 9f, Far, HalfW, HalfH, 30f,
                true, 10f, false);

            Assert.IsTrue(r.X.Snapped);
            Assert.AreEqual(CanvasAlignKind.Grid, r.X.Kind, "|Δ|=1 的网格点严格更近 ⇒ 网格胜");
            Assert.AreEqual(10f, r.X.Line, 0.001f);
            Assert.AreEqual(1f, r.X.Delta, 0.001f);
        }

        [Test]
        public void Resolve_EqualDistance_KindBreaksTie_BeforeCoordinate()
        {
            // 移动中心 0；邻居（不同尺寸，半宽 20）中心线 0 ⇒ Δ=0
            //           网格 step=10 ⇒ 点 0 ⇒ Δ=0
            // 两者 |Δ| 都是 0 ⇒ 按来源序号：NeighborCenter(2) < Grid(4) ⇒ 邻居胜。
            List<CanvasAlignRect> n = Rects(Sized(0f, 0f, 20f, 30f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 0f, Far, HalfW, HalfH, 30f,
                true, 10f, false);

            Assert.IsTrue(r.X.Snapped);
            Assert.AreEqual(CanvasAlignKind.NeighborCenter, r.X.Kind,
                "等距时来源序号小者胜，不看坐标");
            Assert.AreEqual(0, r.X.SourceIndex);
        }

        [Test]
        public void Resolve_SameKindEqualDistance_BreaksBySmallerCoordinate_Deterministically()
        {
            // 邻居A 中心 -20、邻居B 中心 +20（等尺寸）⇒ minDelta 分别 -20 / +20，等距。
            // 两条都是 NeighborMin ⇒ 比坐标：-72 < -32 ⇒ A 胜。
            List<CanvasAlignRect> n = Rects(At(-20f, 0f), At(20f, 0f));
            CanvasAlignResult r1 = CanvasAlignment.Resolve(n, -1, 0f, Far, HalfW, HalfH, 30f,
                false, 50f, false);
            CanvasAlignResult r2 = CanvasAlignment.Resolve(n, -1, 0f, Far, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsTrue(r1.X.Snapped);
            Assert.AreEqual(-72f, r1.X.Line, 0.001f, "等距取坐标较小者（-72 而非 -32）");
            Assert.AreEqual(-20f, r1.X.Delta, 0.001f);
            Assert.AreEqual(0, r1.X.SourceIndex);

            Assert.AreEqual(r1.X.Line, r2.X.Line, 0.001f, "同一输入必须永远同一输出");
            Assert.AreEqual(r1.X.Kind, r2.X.Kind);
            Assert.AreEqual(r1.X.SourceIndex, r2.X.SourceIndex);
        }

        [Test]
        public void Resolve_AlreadyAligned_StaysOnZeroDelta_NoJitter()
        {
            // 移动物与邻居重合 ⇒ 各线 Δ 均为 0 ⇒ 反复求解结果必须恒定
            List<CanvasAlignRect> n = Rects(Sized(0f, 0f, 20f, 30f));
            for (int i = 0; i < 50; i++)
            {
                CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 0f, 0f, HalfW, HalfH, 30f,
                    true, 10f, true);
                Assert.IsTrue(r.X.Snapped);
                Assert.AreEqual(0f, r.X.Delta, 0.0001f, "已对齐状态不得漂移（否则卡片在临界点跳变）");
                Assert.IsTrue(r.Y.Snapped);
                Assert.AreEqual(0f, r.Y.Delta, 0.0001f);
            }
        }

        // ================= 自身排除 =================

        [Test]
        public void Resolve_SkipsSelfIndex_NoSelfSnap()
        {
            List<CanvasAlignRect> n = Rects(At(0f, 0f), At(300f, 0f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, 0, 0f, Far, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsFalse(r.Any,
                "被拖动的对象自身不得成为自己的对齐目标（否则松手瞬间自我吸附）");
        }

        [Test]
        public void Resolve_SkipIndexOutOfRange_IsIgnored()
        {
            List<CanvasAlignRect> n = Rects(Sized(0f, 0f, 20f, 30f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, 99, 9f, Far, HalfW, HalfH, 30f,
                false, 50f, false);
            Assert.IsTrue(r.X.Snapped, "越界 skipIndex 不得让求解崩掉或整体失效");
            Assert.AreEqual(CanvasAlignKind.NeighborCenter, r.X.Kind);
        }

        // ================= 网格 / 原点 =================

        [Test]
        public void Resolve_GridDisabled_ProducesNoGridCandidate()
        {
            List<CanvasAlignRect> n = Rects();
            CanvasAlignResult on = CanvasAlignment.Resolve(n, -1, 9f, Far, HalfW, HalfH, 30f,
                true, 10f, false);
            CanvasAlignResult off = CanvasAlignment.Resolve(n, -1, 9f, Far, HalfW, HalfH, 30f,
                false, 10f, false);

            Assert.IsTrue(on.X.Snapped, "网格吸附开启时应命中");
            Assert.AreEqual(CanvasAlignKind.Grid, on.X.Kind);
            Assert.IsFalse(off.X.Snapped, "网格吸附关闭时不得命中（与网格显示是两个独立开关）");
        }

        [Test]
        public void Resolve_GridCandidate_UsesCenterLineOnly()
        {
            // 网格只提供中心线：移动中心 4（半宽 52 ⇒ MinX=-48，贴近网格点 0 但≠0）
            // 若网格也提供边线，minDelta=0-(-48)=48 已被阈值排除；中心线 Δ=-4 才是唯一来源。
            List<CanvasAlignRect> n = Rects();
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 4f, Far, HalfW, HalfH, 30f,
                true, 10f, false);

            Assert.IsTrue(r.X.Snapped);
            Assert.AreEqual(CanvasAlignKind.Grid, r.X.Kind);
            Assert.AreEqual(-4f, r.X.Delta, 0.001f);
            Assert.AreEqual(0f, r.X.Line, 0.001f);
        }

        [Test]
        public void Resolve_OriginAlignsCenterOnly()
        {
            List<CanvasAlignRect> n = Rects();
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 5f, Far, HalfW, HalfH, 30f,
                false, 50f, true);

            Assert.IsTrue(r.X.Snapped);
            Assert.AreEqual(CanvasAlignKind.Origin, r.X.Kind);
            Assert.AreEqual(-5f, r.X.Delta, 0.001f);
            Assert.AreEqual(0f, r.X.Line, 0.001f);
        }

        [Test]
        public void Resolve_OriginLosesToCloserNeighbor()
        {
            // 邻居中心 3 ⇒ Δ=-2（NeighborCenter）；原点 ⇒ Δ=-5（Origin）⇒ 邻居胜
            List<CanvasAlignRect> n = Rects(Sized(3f, 0f, 20f, 30f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 5f, Far, HalfW, HalfH, 30f,
                false, 50f, true);

            Assert.AreEqual(CanvasAlignKind.NeighborCenter, r.X.Kind,
                "原点是兜底来源，必须输给更近的邻居");
            Assert.AreEqual(-2f, r.X.Delta, 0.001f);
        }

        [Test]
        public void Resolve_IllegalGridStep_Ignored_NoDivisionByZero()
        {
            foreach (float step in new[] { 0f, -10f })
            {
                List<CanvasAlignRect> n = Rects();
                CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 9f, Far, HalfW, HalfH, 30f,
                    true, step, false);
                Assert.IsFalse(r.Any, "非法网格步长 " + step + " 必须被忽略（不得除零/NaN）");
            }
        }

        [Test]
        public void Resolve_GridRoundsAwayFromZero_SymmetricAroundOrigin()
        {
            List<CanvasAlignRect> n = Rects();
            CanvasAlignResult pos = CanvasAlignment.Resolve(n, -1, 6f, Far, HalfW, HalfH, 30f,
                true, 10f, false);
            CanvasAlignResult neg = CanvasAlignment.Resolve(n, -1, -6f, Far, HalfW, HalfH, 30f,
                true, 10f, false);

            Assert.AreEqual(10f, pos.X.Line, 0.001f);
            Assert.AreEqual(-10f, neg.X.Line, 0.001f, "负半轴必须对称（用 AwayFromZero 舍入）");
            Assert.AreEqual(4f, pos.X.Delta, 0.001f);
            Assert.AreEqual(-4f, neg.X.Delta, 0.001f);
        }

        // ================= 退化输入 =================

        [Test]
        public void Resolve_NullOrEmptyNeighbors_ReturnsMiss()
        {
            CanvasAlignResult a = CanvasAlignment.Resolve(null, -1, 0f, 0f, HalfW, HalfH, 30f,
                false, 50f, false);
            CanvasAlignResult b = CanvasAlignment.Resolve(Rects(), -1, 0f, 0f, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsFalse(a.Any);
            Assert.IsFalse(b.Any);
            Assert.AreEqual(CanvasAlignKind.None, a.X.Kind);
            Assert.AreEqual(-1, a.X.SourceIndex);
        }

        [Test]
        public void Resolve_AxesAreIndependent()
        {
            // 邻居在 (0,0)；移动中心 (9, 400) ⇒ X 命中、Y 不命中，互不干扰。
            List<CanvasAlignRect> n = Rects(Sized(0f, 0f, 20f, 30f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 9f, 400f, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsTrue(r.X.Snapped, "X 命中不得因 Y 不命中而丢失（矩形对齐分解为两个一维问题）");
            Assert.IsFalse(r.Y.Snapped);
        }

        [Test]
        public void Resolve_NegativeCoordinatesSupported()
        {
            // 画布允许负坐标；邻居（等尺寸）在 -200 ⇒ MinX=-252
            List<CanvasAlignRect> n = Rects(At(-200f, 0f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, -209f, Far, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsTrue(r.X.Snapped);
            Assert.AreEqual(CanvasAlignKind.NeighborMin, r.X.Kind);
            Assert.AreEqual(9f, r.X.Delta, 0.001f);
            Assert.AreEqual(-252f, r.X.Line, 0.001f);
        }

        [Test]
        public void Resolve_BothAxesSnap_Simultaneously()
        {
            // 邻居小卡（半宽 20 半高 10）在 (0,0)；移动中心 (9, 6)
            //   X: minDelta=+14 centerDelta=-9 maxDelta=-51 ⇒ center 胜
            //   Y: minDelta=+4  centerDelta=-6 maxDelta=-25 ⇒ center 胜
            List<CanvasAlignRect> n = Rects(Sized(0f, 0f, 20f, 10f));
            CanvasAlignResult r = CanvasAlignment.Resolve(n, -1, 9f, 6f, HalfW, HalfH, 30f,
                false, 50f, false);

            Assert.IsTrue(r.Any);
            Assert.AreEqual(CanvasAlignKind.NeighborCenter, r.X.Kind);
            Assert.AreEqual(CanvasAlignKind.NeighborCenter, r.Y.Kind);
            Assert.AreEqual(-9f, r.X.Delta, 0.001f);
            Assert.AreEqual(-6f, r.Y.Delta, 0.001f);
        }

        [Test]
        public void FromCenter_BuildsExpectedAabb()
        {
            CanvasAlignRect r = CanvasAlignRect.FromCenter(10f, -20f, 52f, 30f);
            Assert.AreEqual(-42f, r.MinX, 0.001f);
            Assert.AreEqual(-50f, r.MinY, 0.001f);
            Assert.AreEqual(62f, r.MaxX, 0.001f);
            Assert.AreEqual(10f, r.MaxY, 0.001f);
            Assert.AreEqual(10f, r.CenterX, 0.001f);
            Assert.AreEqual(-20f, r.CenterY, 0.001f);
        }

        // ================= AABB 并集（M3：车间块几何） =================

        [Test]
        public void Union_ExpandsInEveryDirection()
        {
            CanvasAlignRect a = new CanvasAlignRect(0f, 0f, 10f, 10f);
            CanvasAlignRect u = a.Union(new CanvasAlignRect(-5f, 4f, 3f, 20f));
            Assert.AreEqual(-5f, u.MinX, 0.001f, "向左扩张");
            Assert.AreEqual(0f, u.MinY, 0.001f, "上方不该被下边界 4 拉低");
            Assert.AreEqual(10f, u.MaxX, 0.001f, "右边界取较大者");
            Assert.AreEqual(20f, u.MaxY, 0.001f, "向下扩张");
            Assert.AreEqual(0f, a.MinX, 0.001f, "原实例不可被就地修改（readonly 语义）");
        }

        [Test]
        public void Union_ContainedRect_ShrinksNothing()
        {
            CanvasAlignRect outer = new CanvasAlignRect(-100f, -100f, 100f, 100f);
            CanvasAlignRect u = outer.Union(new CanvasAlignRect(0f, 0f, 10f, 10f));
            Assert.AreEqual(-100f, u.MinX, 0.001f);
            Assert.AreEqual(100f, u.MaxX, 0.001f);
        }

        [Test]
        public void UnionWith_IsOrderIndependent()
        {
            var rects = new List<CanvasAlignRect>
            {
                new CanvasAlignRect(-60f, 0f, -8f, 42f),
                new CanvasAlignRect(0f, 50f, 52f, 92f),
                new CanvasAlignRect(-20f, -30f, 44f, 10f),
            };
            CanvasAlignRect forward, backward;
            Assert.IsTrue(CanvasAlignRect.TryUnionAll(rects, out forward));

            var rev = new List<CanvasAlignRect>(rects);
            rev.Reverse();
            Assert.IsTrue(CanvasAlignRect.TryUnionAll(rev, out backward));

            Assert.AreEqual(forward.MinX, backward.MinX, 0.001f, "并集必须与累加顺序无关");
            Assert.AreEqual(forward.MinY, backward.MinY, 0.001f);
            Assert.AreEqual(forward.MaxX, backward.MaxX, 0.001f);
            Assert.AreEqual(forward.MaxY, backward.MaxY, 0.001f);
        }

        [Test]
        public void TryUnionAll_SingleRect_ReturnsItself()
        {
            CanvasAlignRect one = new CanvasAlignRect(1f, 2f, 3f, 4f);
            CanvasAlignRect r;
            Assert.IsTrue(CanvasAlignRect.TryUnionAll(new List<CanvasAlignRect> { one }, out r));
            Assert.AreEqual(1f, r.MinX, 0.001f);
            Assert.AreEqual(4f, r.MaxY, 0.001f);
        }

        [Test]
        public void TryUnionAll_NullOrEmpty_ReturnsFalse()
        {
            // ⭐ 这是「空车间不产生候选线」的数学基础：空集合没有并集 ⇒ 调用方返回 false
            CanvasAlignRect r;
            Assert.IsFalse(CanvasAlignRect.TryUnionAll(null, out r),
                "null 无并集（空车间/车间区不存在 ⇒ 不可命中、不产生候选线）");
            Assert.IsFalse(CanvasAlignRect.TryUnionAll(new List<CanvasAlignRect>(), out r),
                "空表无并集（同上）");
        }

        [Test]
        public void TryUnionAll_MatchesIncrementalUnionWith()
        {
            // UI 侧用的是**增量** UnionWith（省一次 List 分配），必须与批量版结果一致，
            // 否则「命中用的并集」与「吸附用的并集」会出现双源漂移。
            var rects = new List<CanvasAlignRect>
            {
                new CanvasAlignRect(-52f, -30f, 52f, 30f),
                new CanvasAlignRect(68f, -18f, 172f, 42f),
                new CanvasAlignRect(12f, 44f, 116f, 104f),
            };
            CanvasAlignRect batch;
            Assert.IsTrue(CanvasAlignRect.TryUnionAll(rects, out batch));

            CanvasAlignRect incremental = rects[0];
            for (int i = 1; i < rects.Count; i++)
            {
                incremental = incremental.Union(rects[i]);
            }
            Assert.AreEqual(batch.MinX, incremental.MinX, 0.001f);
            Assert.AreEqual(batch.MinY, incremental.MinY, 0.001f);
            Assert.AreEqual(batch.MaxX, incremental.MaxX, 0.001f);
            Assert.AreEqual(batch.MaxY, incremental.MaxY, 0.001f);
        }

        [Test]
        public void HalfExtents_DeriveFromAabb()
        {
            CanvasAlignRect r = CanvasAlignRect.FromCenter(0f, 0f, 52f, 30f);
            Assert.AreEqual(52f, r.HalfWidth, 0.001f);
            Assert.AreEqual(30f, r.HalfHeight, 0.001f);
        }

        // ================= M3：车间块整体吸附 =================

        [Test]
        public void Resolve_ZoneBlockSnapsToOtherZoneUnion()
        {
            // 邻居 = 另一车间 3 张卡的并集 ⇒ X 跨 -52..172（CenterX=60），Y 跨 -30..42（CenterY=6）
            CanvasAlignRect zone;
            Assert.IsTrue(CanvasAlignRect.TryUnionAll(new List<CanvasAlignRect>
            {
                new CanvasAlignRect(-52f, -30f, 52f, 30f),
                new CanvasAlignRect(68f, -18f, 172f, 42f),
                new CanvasAlignRect(12f, -30f, 116f, 42f),
            }, out zone), "测试前提：并集必须成立");
            Assert.AreEqual(-52f, zone.MinX, 0.001f);
            Assert.AreEqual(172f, zone.MaxX, 0.001f);

            // 移动块：中心 (13, 300)，尺寸 104×60 ⇒ MinX=-39
            //   minDelta    = -52-(-39) = -13 ✔（阈值 30 内）
            //   centerDelta = 60-13     = +47 ✘
            //   maxDelta    = 172-65    = +107 ✘
            CanvasAlignResult r = CanvasAlignment.Resolve(
                new List<CanvasAlignRect> { zone }, -1, 13f, 300f, 52f, 30f, 30f,
                false, 0f, false);

            Assert.IsTrue(r.X.Snapped, "车间块应能对齐到另一车间的外轮廓左边线");
            Assert.AreEqual(CanvasAlignKind.NeighborMin, r.X.Kind);
            Assert.AreEqual(-13f, r.X.Delta, 0.001f, "Δ 是「加到锚点」的刚体平移量");
            Assert.IsFalse(r.Y.Snapped, "Y 相距 300 远超阈值，不应命中（两轴独立）");
        }

        [Test]
        public void Resolve_ZoneBlockAlreadyAligned_KeepsZeroDelta()
        {
            // 移动块左边线已在 -52 ⇒ Δ 必须为 0 且稳定（不产生「吸附到看不见的别处」）
            CanvasAlignRect zone = new CanvasAlignRect(-52f, -30f, 172f, 42f);
            for (int i = 0; i < 20; i++)
            {
                CanvasAlignResult r = CanvasAlignment.Resolve(
                    new List<CanvasAlignRect> { zone }, -1, 0f, 300f, 52f, 30f, 30f,
                    false, 0f, false);
                Assert.IsTrue(r.X.Snapped);
                Assert.AreEqual(0f, r.X.Delta, 0.0001f, "已对齐的块不得被拖走（Δ 恒 0）");
            }
        }

        [Test]
        public void Resolve_ZoneBlockDeltaAppliedToAnchor_MovesWholeBlock()
        {
            // 刚体平移不变性：把同一 Δ 加到锚点，块的并集 AABB 应恰好平移 Δ。
            var zone = new CanvasAlignRect(-52f, -30f, 172f, 42f);
            CanvasAlignResult r = CanvasAlignment.Resolve(
                new List<CanvasAlignRect> { zone }, -1, 0f, 5f, zone.HalfWidth, zone.HalfHeight,
                30f, false, 0f, false);

            float delta = r.X.Delta;
            // 模拟：锚点加 Δ 后重算并集（形状不变、位置平移）
            CanvasAlignRect moved = new CanvasAlignRect(
                zone.MinX + delta, zone.MinY, zone.MaxX + delta, zone.MaxY);
            Assert.AreEqual(zone.MinX + delta, moved.MinX, 0.001f);
            Assert.AreEqual(zone.MaxX + delta, moved.MaxX, 0.001f);
            Assert.AreEqual(zone.HalfWidth, moved.HalfWidth, 0.001f,
                "刚体平移后半宽不变 ⇒ 把 Δ 加到锚点即整块平移（M3 的正确性依据）");
        }

        [Test]
        public void Resolve_EmptyZoneBlockHasNoBounds_ProducesNoCandidate()
        {
            // 空车间：TryUnionAll 返回 false ⇒ UI 侧不产生候选线。
            // 这里验证「若真把空块（零尺寸）喂进 Resolve，会命中但无意义」——
            // 所以 UI 必须在调用 Resolve **之前**就用 TryGetZoneDeviceBounds 拦掉。
            CanvasAlignRect bounds;
            bool hasBounds = CanvasAlignRect.TryUnionAll(
                new List<CanvasAlignRect>(), out bounds);
            Assert.IsFalse(hasBounds, "空车间必须没有 AABB，UI 据此跳过（防幽灵吸附）");
        }
    }
}
