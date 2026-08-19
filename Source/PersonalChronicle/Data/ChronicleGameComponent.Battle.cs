// ChronicleGameComponent partial：战役 Lord 链接（v4.11 跨存档防污染）。
// 按 MATRIX-010 治理：raidLordToBattle 链接表与 LinkRaidLords 从 Application 下沉到 Data 层，
// Application 经本类访问，依赖方向恢复单向（UI→Application→Domain←Data/Capture）。
using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace PersonalChronicle.Data
{
    public sealed partial class ChronicleGameComponent
    {
        /// <summary>
        /// 将当前地图上敌对的 raid Lord 链接到刚开始的战役，并快照来袭规模。
        /// 只统计对玩家敌对的 Lord（排除商队/访客/动物群/己方 Lord）。
        /// </summary>
        public void LinkRaidLords(BattleObject battle)
        {
            if (battle == null || string.IsNullOrEmpty(battle.StableId))
            {
                return;
            }
            try
            {
                int total = 0;
                bool linkedAny = false;
                List<Map> maps = Find.Maps;
                if (maps != null)
                {
                    for (int mi = 0; mi < maps.Count; mi++)
                    {
                        Map map = maps[mi];
                        if (map == null || map.lordManager == null || map.lordManager.lords == null)
                        {
                            continue;
                        }
                        for (int li = 0; li < map.lordManager.lords.Count; li++)
                        {
                            Lord lord = map.lordManager.lords[li];
                            if (lord == null || lord.faction == null)
                            {
                                continue;
                            }
                            // Only enemy raid Lords count toward the force size: the
                            // faction must be hostile to the player. This excludes
                            // caravans, visitors, animal herds and the player's own
                            // Lords, which would otherwise inflate RaidCount.
                            if (!lord.faction.HostileTo(Faction.OfPlayer))
                            {
                                continue;
                            }
                            // Skip Lords already attributed to a battle.
                            if (ChronicleGameComponent.RaidLordToBattle.ContainsKey(lord.loadID))
                            {
                                continue;
                            }
                            if (lord.ownedPawns == null || lord.ownedPawns.Count == 0)
                            {
                                continue;
                            }
                            ChronicleGameComponent.RaidLordToBattle[lord.loadID] = battle.StableId;
                            total += lord.ownedPawns.Count;
                            linkedAny = true;
                        }
                    }
                }
                if (linkedAny)
                {
                    battle.RaidCount = total;
                    battle.RemainingRaidCount = total;
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to link raid lords: " + ex.Message);
            }
        }
    }
}