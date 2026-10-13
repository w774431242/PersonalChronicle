using PersonalChronicle.MasterControl.Population;
using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Dev
{
    /// <summary>
    /// **开发者测试数据**：生成一个**非空的档案馆承载快照外壳**，供总控「测试数据」开关在
    /// ① 控制总览 / ② 人物档案 两个视图下同样生效（原 <see cref="DevPopulationFixtures"/> 只覆盖 ① 概览）。
    ///
    /// <para><b>边界（与 <see cref="DevPopulationFixtures"/> 同规）</b>：只产出 POCO 快照、不写存档、
    /// 不触碰 Provider 路径；关→开立即恢复真实数据（参考 MasterControl 既有 devPreview 模式：
    /// 快照 / UI 态，不入存档）。</para>
    ///
    /// <para><b>为何只造外壳、不造内容</b>：被承载界面的**内部内容**由档案馆主界面本体
    /// （<c>PersonnelArchiveHost</c>）实时读取 <c>IArchiveService</c> 绘制，与概览快照走的是
    /// 同一份真实数据。伪造内部内容必须写存档 / 侵入 Provider，违反 dev 数据「不入存档、不触碰
    /// Provider」铁律（P0/P1 风险）。本快照的意义 = 让未注册档案馆承载的中心也**能展示该视图**
    /// （使 hasArchive 判据为真、tab 不再灰显），内部仍显示真实的（可能为空的）档案内容。</para>
    ///
    /// <para>尺寸常量 = 镜像 <c>ArchiveUiStyle</c> 设计基准（1100×720 / 900 / 640 / 360）。
    /// 因 Application 层不得反向依赖 Archive/UI 内部常量，此处按 GOV-006 显式登记并标注来源。</para>
    /// </summary>
    public static class DevArchiveFixtures
    {
        // [GOV-006] 镜像 Archive/UI/ArchiveUiStyle 的设计基准（Application 不反向引用 Archive/UI）。
        private const float DevDesignWidth = 1100f;
        private const float DevDesignHeight = 720f;
        private const float DevCompactWidth = 900f;
        private const float DevMinWidth = 640f;
        private const float DevMinHeight = 360f;

        /// <summary>
        /// 构建非空档案馆承载外壳（同一 <paramref name="seed"/> 结果稳定，便于复现截图）。
        /// <paramref name="seed"/> 仅用于与 <see cref="DevPopulationFixtures.BuildRandom"/> 保持同形签名，
        /// 外壳本身无随机维度。
        /// </summary>
        public static ArchiveViewSnapshot BuildRandom(int seed)
        {
            ArchiveViewSnapshot snapshot = new ArchiveViewSnapshot
            {
                DesignWidth = DevDesignWidth,
                DesignHeight = DevDesignHeight,
                CompactWidth = DevCompactWidth,
                MinWidth = DevMinWidth,
                MinHeight = DevMinHeight,
                TitleText = "PersonalChronicle.UI.MasterControl.Archive.Title".Translate().ToString(),
                SubtitleText = string.Empty,
                IsEmpty = false
            };
            return snapshot;
        }
    }
}
