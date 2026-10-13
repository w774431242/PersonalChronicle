using System.Collections.Generic;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>订单待办的**计算口径**（决定用哪一组 designation 计数）。</summary>
    internal enum OrderBacklogKind
    {
        /// <summary>`Mine` + `MineVein` 之和。</summary>
        MineAndVein,
        /// <summary>`HarvestPlant` 且目标是**树**（伐木）。</summary>
        TreeHarvest,
        /// <summary>`HarvestPlant` 且目标是**作物**（= 总数 − 树）。与伐木共享同一个 designation，必须相减。</summary>
        CropHarvest,
        /// <summary>单个 designation（按 defName 解析，缺失即 0）。</summary>
        SingleDesignation
    }

    /// <summary>
    /// 一条**原版订单**（Orders 菜单里的一张单）—— 识别注册表的一行。
    /// CHG-20261011-WORKCAT（S1）：把"识别"从散落字面量收成**一张表**，
    /// 于是①"要识别哪些订单"是一处可读的清单 ②新增订单只是加一行 ③**未接线的族照样可见**。
    /// </summary>
    internal sealed class VanillaOrder
    {
        /// <summary>稳定 ID（本地化键 `PC_PEA.Order.*` 的后缀 + 诊断标识）。</summary>
        public readonly string Key;
        /// <summary>对应的原版工作类型（`WorkTypeDef.defName`；mod 同构，故这张表天然含第三方）。</summary>
        public readonly string WorkTypeDefName;
        /// <summary>**完成信号是否已接线**（false = 识别到但尚未能计酬 ⇒ UI 必须如实标注，绝不静默漏付）。</summary>
        public readonly bool Wired;
        public readonly OrderBacklogKind Backlog;
        /// <summary>单 designation 口径时使用的 defName（其余口径为空）。</summary>
        public readonly string DesignationDefName;

        public VanillaOrder(string key, string workTypeDefName, bool wired, OrderBacklogKind backlog,
            string designationDefName = null)
        {
            Key = key; WorkTypeDefName = workTypeDefName; Wired = wired; Backlog = backlog;
            DesignationDefName = designationDefName;
        }
    }

    /// <summary>按**工作类型**聚合的订单组（合同按工作类型计价/派发，故"已派发 / 可派发"只有在这一级才精确）。</summary>
    internal sealed class VanillaOrderGroup
    {
        public readonly string WorkTypeDefName;
        public readonly VanillaOrder[] Orders;
        /// <summary>该组**全部**订单都已接线才算已接线（否则如实标"未接线"）。</summary>
        public readonly bool Wired;
        public VanillaOrderGroup(string workTypeDefName, VanillaOrder[] orders)
        {
            WorkTypeDefName = workTypeDefName; Orders = orders;
            bool wired = true;
            for (int i = 0; i < orders.Length; i++) if (!orders[i].Wired) { wired = false; break; }
            Wired = wired;
        }
    }

    /// <summary>
    /// **工作识别注册表**（原版订单族）—— 与「本 Mod 岗位族」并列构成"工作目录"的源表。
    ///
    /// <para><b>覆盖（2026-10-11 定稿 · 对照原版 Orders 菜单）</b>：开采 / 伐木 / 割除 / 收获 / 狩猎 / 宰杀 / 驯服 / 搬运。
    /// 八项均接入玩家指定前置快照及成功后完成证据（S3）。
    /// 显式收获由 PlantsCut giver 发出 HarvestDesignated，属于 PlantCutting；
    /// Growing 的种植区自动收获没有玩家订单，不纳入本表。
    /// 未接线 ≠ 不识别：待办照常计数并上屏，UI 以「未接线」徽章如实标注。</para>
    ///
    /// <para><b>为什么工作类型要分开写</b>：合同的单价与派发键都挂在 <c>WorkTypeDef</c> 上
    /// （`VanillaOpportunityKey(workType, anchorKey)`），所以"伐木/割除"同属 `PlantCutting`、
    /// "宰杀/驯服"同属 `Handling` —— 组的划分因此决定了哪一级能显示精确的"已派发 / 可派发"。</para>
    ///
    /// <para><b>数组顺序即展示顺序</b>：同一工作类型的订单**必须相邻**（<see cref="Groups"/> 按相邻归并，
    /// 不再排序，保持顺序可预测、零分配）。</para>
    /// </summary>
    internal static class VanillaOrderCatalogue
    {
        internal static readonly VanillaOrder[] Orders =
        {
            // ── 已接线（有既有完成捕获点：Patch_ItemFlowMining / Patch_ItemFlowPlant）──
            new VanillaOrder("Mine", "Mining", true, OrderBacklogKind.MineAndVein),
            new VanillaOrder("ChopWood", "PlantCutting", true, OrderBacklogKind.TreeHarvest),
            new VanillaOrder("CutPlants", "PlantCutting", true, OrderBacklogKind.SingleDesignation, "CutPlant"),
            new VanillaOrder("Harvest", "PlantCutting", true, OrderBacklogKind.CropHarvest),
            new VanillaOrder("Hunt", "Hunting", true, OrderBacklogKind.SingleDesignation, "Hunt"),
            new VanillaOrder("Slaughter", "Handling", true, OrderBacklogKind.SingleDesignation, "Slaughter"),
            new VanillaOrder("Tame", "Handling", true, OrderBacklogKind.SingleDesignation, "Tame"),
            new VanillaOrder("Haul", "Hauling", true, OrderBacklogKind.SingleDesignation, "Haul")
        };

        /// <summary>按相邻同工作类型归并出的组（静态派生一次；纯函数，可离线断言）。</summary>
        internal static readonly VanillaOrderGroup[] Groups = BuildGroups();

        private static VanillaOrderGroup[] BuildGroups()
        {
            var groups = new List<VanillaOrderGroup>(Orders.Length);
            var current = new List<VanillaOrder>(4);
            for (int i = 0; i < Orders.Length; i++)
            {
                if (current.Count > 0 && current[0].WorkTypeDefName != Orders[i].WorkTypeDefName)
                {
                    groups.Add(new VanillaOrderGroup(current[0].WorkTypeDefName, current.ToArray()));
                    current.Clear();
                }
                current.Add(Orders[i]);
            }
            if (current.Count > 0) groups.Add(new VanillaOrderGroup(current[0].WorkTypeDefName, current.ToArray()));
            return groups.ToArray();
        }
    }
}
