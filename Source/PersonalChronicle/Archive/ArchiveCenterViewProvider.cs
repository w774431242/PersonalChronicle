using PersonalChronicle.MasterControl.Population;
using System;
using PersonalChronicle.Api;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Application.SystemCenters;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// 「模块主界面承载」数据源实现（档案馆侧）——**只声明预算与可用性，不产出界面数据**。
    ///
    /// <para>依赖方向 Archive（表现层）→ Application（契约/读模型），与 Automation → Application 同向；
    /// Application/Domain **不反向依赖 UI**（ARC-001）。界面本体由 Archive.UI.PersonnelArchiveHost 绘制，
    /// 总控中心只按快照非空决定画不画（LAND-010：UI 不认中心名）。</para>
    ///
    /// <para>尺寸常量取 <see cref="ArchiveUiStyle"/>，用于人员中心内部面板的空间预算。
    /// 独立档案馆窗口已移除，面板实例由总控中心持有。</para>
    /// </summary>
    public sealed class ArchiveCenterViewProvider : IArchiveViewDataProvider
    {
        private readonly IArchiveQueryService queries;

        public ArchiveCenterViewProvider(IArchiveQueryService queries)
        {
            this.queries = queries;
        }

        public ArchiveViewSnapshot BuildArchiveView()
        {
            ArchiveViewSnapshot snapshot = new ArchiveViewSnapshot
            {
                DesignWidth = ArchiveUiStyle.DesignWidth,
                DesignHeight = ArchiveUiStyle.DesignHeight,
                CompactWidth = ArchiveUiStyle.CompactLayoutWidth,
                MinWidth = ArchiveUiStyle.MinLayoutWidth,
                MinHeight = ArchiveUiStyle.MinLayoutHeight
            };
            try
            {
                if (queries == null)
                {
                    // 服务未装配（例如早期加载）：如实标记不可承载，宿主画空态。
                    snapshot.IsEmpty = true;
                    return snapshot;
                }
                snapshot.TitleText =
                    "PersonalChronicle.UI.MasterControl.Archive.Title".Translate().ToString();
                snapshot.SubtitleText = "PersonalChronicle.UI.MasterControl.Archive.Subtitle".Translate(
                    (NamedArgument)queries.GetActiveSnapshotCount(),
                    (NamedArgument)queries.GetArchivedSnapshotCount()).ToString();
                snapshot.BuiltFromRevision = queries.GetDataRevision();
                snapshot.IsEmpty = false;
            }
            catch (Exception ex)
            {
                // 失败隔离：快照构建异常不得进入绘制循环（08c DL 契约）。
                ChronicleLog.Error(ChronicleLog.Category.Ui, "Archive center view snapshot failed: " + ex);
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }
    }
}
