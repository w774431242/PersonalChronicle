using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 自动化物品仓储契约 —— 未来管道运输系统的<b>预留接入口</b>。
    ///
    /// 设计原则（产线铁律①）：缓冲只存 <c>ThingDef + count</c>，绝不存 Thing 引用——
    /// Thing 会随存档重载/焚毁失效，def+count 才是跨存档稳定表示。
    ///
    /// 管道设备落地后只依赖本接口对接，不引用任何具体设备类型：
    /// - 灌入 = <see cref="Push"/>（先 <see cref="AcceptQuantity"/> 问余量）；
    /// - 取走 = <see cref="TakeOut"/>（返回整栈 Thing，管道口负责落地）；
    /// - 查询 = <see cref="Buffer"/> / <see cref="StoredTotal"/> / <see cref="Capacity"/>。
    /// </summary>
    public interface IItemStorage
    {
        /// <summary>仓储总容量（件，按物品个数计）。</summary>
        int Capacity { get; }

        /// <summary>当前存量（件）。</summary>
        int StoredTotal { get; }

        /// <summary>缓冲快照（UI / 管道查询用；调用方勿修改元素）。</summary>
        IEnumerable<ThingDefCountClass> Buffer { get; }

        /// <summary>还能接收 def 多少件（容量余量；不收该类物品返回 0）。</summary>
        int AcceptQuantity(ThingDef def, int count);

        /// <summary>压入 count 件 def，返回实际接收数（可能被容量截断，最小 0）。</summary>
        int Push(ThingDef def, int count);

        /// <summary>取出至多 maxCount 件 def，打包成一栈 Thing 返回（受 stackLimit 限制）；
        /// 无货返回 null。调用方负责把返回的 Thing 落地（GenPlace / 管道口）。</summary>
        Thing TakeOut(ThingDef def, int maxCount);

        /// <summary>把缓冲全部落地到设备附近（玩家手动取出 / 撤机兜底）。</summary>
        void DropAll();
    }
}
