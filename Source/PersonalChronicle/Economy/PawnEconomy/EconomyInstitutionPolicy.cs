using System;
using System.Collections.Generic;
using PersonalChronicle.Automation;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>Read-only reference to one spawned public institution. Game-layer only: it holds the
    /// building so adapters can read hours and reachability; nothing above this layer receives it.</summary>
    internal sealed class InstitutionRef
    {
        public string AnchorKey { get; }
        public int MapId { get; }
        public InstitutionKind Kind { get; }
        public Building_EconomyInstitution Anchor { get; }
        public InstitutionRef(string anchorKey, int mapId, InstitutionKind kind, Building_EconomyInstitution anchor)
        { AnchorKey = anchorKey; MapId = mapId; Kind = kind; Anchor = anchor; }
    }

    /// <summary>New-engine institution surface. CHG-20261012-ECON-CONTRACTIZE · B2：<b>机构政策整体净删</b> ——
    /// 本文件历史上叫 `EconomyInstitutionPolicy`（政策读法门面），现在只剩**机构查询 + 服务价（建筑字段）**；
    /// 文件名保留以少动索引与标注，语义以本注释为准。政策数据的读/写路径（`InstitutionPolicy` /
    /// `InstitutionPolicySaveData` / 7 个 Query* / Apply）已全部移除。</summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        /// <summary>Read-only pawn lookup on the same perception directory the planning loop uses.
        /// Adapters must never create records, open accounts or wake the legacy Account/Person factory.</summary>
        internal bool TryGetPlanningPawn(string pawnId, int mapId, out Pawn pawn)
        { pawn = null; return perceptionSource != null && perceptionSource.TryGetPlanningPawn(pawnId, mapId, out pawn); }
        /// <summary>CHG-20261012-ECON-CONTRACTIZE · B2（用户裁决⑥）：体检中心**建筑字段**承载的服务价 ——
        /// 与旧政策读法**同一纪律**：确定性单一投影（值 &gt; 0 的体检中心里取 anchor 键最小者胜出，
        /// 多台从不相加/取平均）。<c>null</c> = **未挂牌** ⇒ 调用方必须拒绝结算，绝不发明价格（M5「未知不当零」）。</summary>
        internal long? QueryServicePriceMinor(int mapId)
        {
            long? best = null;
            string bestKey = null;
            var rows = QueryInstitutions(mapId);
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Kind != InstitutionKind.ExamCenter) continue;
                long price = rows[i].Anchor != null ? rows[i].Anchor.ServicePricePerUnitMinor : 0L;
                if (price <= 0L) continue;
                if (best == null || StringComparer.Ordinal.Compare(rows[i].AnchorKey, bestKey) < 0)
                { best = price; bestKey = rows[i].AnchorKey; }
            }
            return best;
        }

        /// <summary>Spawned institutions on one map, de-duplicated by anchor key. Backed by the same
        /// anchors list the runtime registers on spawn/despawn.</summary>
        internal IReadOnlyList<InstitutionRef> QueryInstitutions(int mapId)
        {
            var rows = new List<InstitutionRef>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null || !anchor.Spawned || anchor.Map == null || anchor.Map.uniqueID != mapId) continue;
                var extension = anchor.def?.GetModExtension<EconomyInstitutionExtension>();
                if (extension == null) continue;
                var key = anchor.GetUniqueLoadID();
                if (!seen.Add(key)) continue;
                rows.Add(new InstitutionRef(key, mapId, extension.kind, anchor));
                if (rows.Count >= PlanningPolicyMaximumInstitutions) break;
            }
            return rows.AsReadOnly();
        }
        private const int PlanningPolicyMaximumInstitutions = 64;

        /// <summary>Real reserve-backed treasury balance (M11). Null means the ledger cannot be read and
        /// must never be treated as zero.</summary>
        internal long? QueryTreasuryAvailableMinor()
        {
            var audit = settlement.ReadAudit();
            if (audit == null || audit.Fault != null) return null;
            var treasury = settlement.ReadAccount(SettlementLedgerNames.Treasury);
            return treasury?.AvailableMinor;
        }
        internal static Zone_Workshop QueryWorkshopZone(Map map, int zoneId)
        {
            if (map == null || zoneId <= 0) return null;
            var zones = map.zoneManager?.AllZones;
            if (zones == null) return null;
            for (int i = 0; i < zones.Count; i++)
                if (zones[i] is Zone_Workshop workshop && workshop.ID == zoneId) return workshop;
            return null;
        }
    }
}
