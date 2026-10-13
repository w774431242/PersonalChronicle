using NUnit.Framework;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 「模块主界面承载」空间档位判定的离线单测。
    ///
    /// <para>只测纯函数 <see cref="ArchiveViewLayout.Resolve"/> 与两个「默认值即安全值」的语义，
    /// 不触碰 Unity/DefDatabase：档位决定「折叠侧栏 / 直接渲染 / 拒绝渲染」，
    /// 一旦判据漂移（例如阈值缺失时改成按猜的尺寸硬画），表现是嵌入界面重叠或越界，
    /// 属于必须钉死的语义（LAND-006/009）。</para>
    /// </summary>
    [TestFixture]
    public sealed class ArchiveViewLayoutTests
    {
        /// <summary>与 <c>ArchiveUiStyle</c> 声明一致的预算样本（数值仅用于本测试的边界计算）。</summary>
        private static ArchiveViewSnapshot Budget()
        {
            return new ArchiveViewSnapshot
            {
                DesignWidth = 1100f,
                DesignHeight = 720f,
                CompactWidth = 900f,
                MinWidth = 640f,
                MinHeight = 360f
            };
        }

        [Test]
        public void NullSnapshot_IsTooSmall()
        {
            Assert.AreEqual(ArchiveViewTier.TooSmall,
                ArchiveViewLayout.Resolve(1200f, 700f, null));
        }

        [Test]
        public void MissingBudget_NeverGuesses()
        {
            ArchiveViewSnapshot empty = new ArchiveViewSnapshot();
            Assert.AreEqual(ArchiveViewTier.TooSmall,
                ArchiveViewLayout.Resolve(4000f, 3000f, empty),
                "预算字段缺失时必须拒绝渲染：宁可提示空间不足，也不按猜的尺寸硬画");
        }

        [Test]
        public void WidthBelowMinimum_IsTooSmall()
        {
            Assert.AreEqual(ArchiveViewTier.TooSmall,
                ArchiveViewLayout.Resolve(639f, 720f, Budget()));
        }

        [Test]
        public void HeightBelowMinimum_IsTooSmall()
        {
            Assert.AreEqual(ArchiveViewTier.TooSmall,
                ArchiveViewLayout.Resolve(1200f, 359f, Budget()));
        }

        [Test]
        public void ExactMinimumWidth_IsCompact()
        {
            Assert.AreEqual(ArchiveViewTier.Compact,
                ArchiveViewLayout.Resolve(640f, 360f, Budget()));
        }

        [Test]
        public void WidthBetweenMinimumAndCompact_IsCompact()
        {
            Assert.AreEqual(ArchiveViewTier.Compact,
                ArchiveViewLayout.Resolve(899f, 720f, Budget()));
        }

        [Test]
        public void ExactCompactWidth_IsFull()
        {
            Assert.AreEqual(ArchiveViewTier.Full,
                ArchiveViewLayout.Resolve(900f, 720f, Budget()));
        }

        [Test]
        public void WidthAboveCompact_IsFull()
        {
            Assert.AreEqual(ArchiveViewTier.Full,
                ArchiveViewLayout.Resolve(1100f, 720f, Budget()));
        }

        [Test]
        public void HugeWidthButTinyHeight_IsStillTooSmall()
        {
            Assert.AreEqual(ArchiveViewTier.TooSmall,
                ArchiveViewLayout.Resolve(4000f, 120f, Budget()),
                "宽度足够不代表能画：高度不足同样必须降级为提示，不能压成一条缝");
        }

        [Test]
        public void DetailSnapshot_ArchiveView_DefaultsToNull()
        {
            SystemCenterDetailSnapshot detail = new SystemCenterDetailSnapshot();
            Assert.IsNull(detail.ArchiveView,
                "默认不得带承载视图：漏赋值时宁可不画，也不能画一个空区块（非 null 即渲染）");
        }

        [Test]
        public void PopulationCenterId_IsStableKey()
        {
            Assert.AreEqual("population_center", SystemCenterIds.Population,
                "人员中心 ID 是逻辑主键：必须与 Defs/SystemCenterDefs.xml 的 defName 一致");
        }
    }
}
