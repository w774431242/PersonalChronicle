using PersonalChronicle.MasterControl.Population;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// Partial of <see cref="MasterControlTabWindow"/> ——**模块主界面承载区块**。
    ///
    /// <para>当中心详情快照带 <see cref="SystemCenterDetailSnapshot.ArchiveView"/>（非 null 且非空）时，
    /// 本区块把该模块的**主界面本体**嵌进详情页 —— 不是「跳转按钮」，而是真的把界面画在这里
    /// （本项目对「植入」的定义）。</para>
    ///
    /// <para><b>边界</b>：本文件只做「分档 + 定位 + 交给宿主绘制」，不认中心名、不读 Def、
    /// 不重算任何数据；空间预算（设计尺寸 / 折叠阈值 / 最小尺寸）全部来自快照，由提供方声明
    /// （LAND-006/010/011）。高度估算与绘制共用 <see cref="ArchiveBlockHeight"/>，两处不会漂移。</para>
    ///
    /// <para><b>降级顺序</b>（给不够时的处理，见 <see cref="ArchiveViewLayout"/>）：
    /// ① 折叠左侧导航为顶部横条（由被承载界面自己按宽度判定）→ ② 内容区内层滚动 →
    /// ③ 高度压缩到快照声明的最小高 → ④ 仍不足则只画一行「空间不足」提示，
    /// 绝不含糊硬画（不硬挤、不重叠、不越界）。</para>
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        // 面板属于当前总控中心实例，不依赖已移除的档案馆 MainButtonDef。
        private readonly PersonnelArchivePanel personnelArchivePanel = new PersonnelArchivePanel();
        /// <summary>
        /// 区块标题行高 —— **保留常量但恒为 0**：预览 `#v-archive` 里承载的就是一层 `.arc`，
        /// **没有外层标题条**（旧实现的「殖民地档案馆」小字工具条 = 同屏第二个标题，
        /// 用户实机判定为「旧内容未删除干净」）。保留常量是为了让高度估算与绘制仍共用同一符号。
        /// </summary>
        private const float ArchiveBlockTitleHeight = 0f;

        /// <summary>承载区四周内缩（绘制与高度估算共用，避免两处漂移）。</summary>
        private const float ArchiveBlockInset = 6f;

        /// <summary>承载区高度上限 = 屏幕高 × 本比例：区块不该长到把整页撑成超长滚动。</summary>
        private const float ArchiveBlockMaxScreenRatio = 0.78f;

        /// <summary>
        /// 承载区高度：期望 = 设计基准高，封顶 = 屏幕高比例，兜底 = 快照声明的最小高。
        /// </summary>
        private static float ArchiveViewportHeight(ArchiveViewSnapshot snapshot)
        {
            float wanted = snapshot.DesignHeight > 0f ? snapshot.DesignHeight : snapshot.MinHeight;
            float cap = Screen.height * ArchiveBlockMaxScreenRatio;
            return Mathf.Max(snapshot.MinHeight, Mathf.Min(wanted, cap));
        }

        /// <summary>
        /// 区块总高（**绘制与 <c>ComputeDetailHeight</c> 共用**：两处不同源必然表现为滚动异常）。
        /// </summary>
        private float ArchiveBlockHeight(ArchiveViewSnapshot snapshot, float width)
        {
            if (snapshot == null || snapshot.IsEmpty)
            {
                return 0f;
            }
            float viewport = ArchiveViewportHeight(snapshot);
            ArchiveViewTier tier = ArchiveViewLayout.Resolve(
                Mathf.Max(0f, width - ArchiveBlockInset * 2f), viewport, snapshot);
            float body = tier == ArchiveViewTier.TooSmall ? NoticeBoxHeight : viewport + ArchiveBlockInset * 2f;
            return ArchiveBlockTitleHeight + body + SectionGap;
        }

        /// <summary>
        /// 绘制承载区块；返回下一个可用的 y。
        /// <paramref name="pawnDetailView"/> = <c>true</c> ⇒ 承载区画**人物档案详情**
        /// （预览 <c>#v-pawn</c>），<c>false</c> ⇒ 画**档案总览**（预览 <c>#v-archive</c>）。
        ///
        /// <para>⚠ 2026-09-15 落地：总控的「档案总览」与「人物档案」两个 tab 此前**共用**本区块
        /// ⇒ 两个 tab 显示同一界面（用户报「tab 内容未落地」的根因）。现以参数区分，透传到
        /// <see cref="PersonnelArchiveHost.Draw"/>。</para>
        /// </summary>
        private float DrawArchiveBlock(Rect rect, float y, ArchiveViewSnapshot snapshot,
            bool pawnDetailView = false)
        {
            if (snapshot == null || snapshot.IsEmpty)
            {
                return y;
            }
            float viewport = ArchiveViewportHeight(snapshot);
            float innerWidth = Mathf.Max(0f, rect.width - ArchiveBlockInset * 2f);
            ArchiveViewTier tier = ArchiveViewLayout.Resolve(innerWidth, viewport, snapshot);

            // ⛔ 外层标题条**整块删除**（CHG-20260914-002 实机判定）：
            //    预览 #v-archive 里承载的就是一层 .arc —— 头部标题在被承载界面**内部**（arc-hd），
            //    外面再画一条「殖民地档案馆 / 档案总览」+「嵌入视图·完整档」的工具条，
            //    既与 arc-hd 的标题同字重复，也不是预览里的任何组件（用户：「旧内容未删除干净」）。
            //    这里保持 y 不变（ArchiveBlockTitleHeight = 0），直接进入承载区。

            if (tier == ArchiveViewTier.TooSmall)
            {
                // 降级档 4：如实说明所需最小尺寸（LAND-009 诚实 > 观感），不做裁剪式硬画。
                y = DrawNoticeBox(rect, y, Translate2("PersonalChronicle.UI.MasterControl.Archive.TooNarrow",
                    (NamedArgument)Mathf.RoundToInt(snapshot.MinWidth),
                    (NamedArgument)Mathf.RoundToInt(snapshot.MinHeight)));
                return y + SectionGap;
            }

            Rect body = new Rect(rect.x, y, rect.width, viewport + ArchiveBlockInset * 2f);
            UIComponents.Panel(body);
            PersonnelArchiveHost.Draw(personnelArchivePanel, body.ContractedBy(ArchiveBlockInset), pawnDetailView);
            return body.yMax + SectionGap;
        }
    }
}
