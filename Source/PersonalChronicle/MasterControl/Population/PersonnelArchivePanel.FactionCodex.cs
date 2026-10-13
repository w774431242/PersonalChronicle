using PersonalChronicle.Archive;
using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// [ARC-013] <see cref="PersonnelArchivePanel"/> 的「阵营图鉴（Faction Codex）」构建切片。
    ///
    /// 原 PersonnelArchivePanel.Cache.cs 单文件 1060 行，混合「缓存刷新」「主页快照重建」
    /// 「战斗数据扫描」「阵营图鉴」「事件缓存」多组职责，违反 [ARC-013] 单一职责与「单文件 ≤ 800 行」。
    /// 按 08 §3「排序→识别→剥离」，本文件剥离其中的阵营图鉴构建（BuildFactionCodex 及其比较器）。
    ///
    /// 剥离原则（[ARC-013] 零契约改动）：仅物理搬移，类名、方法签名、访问级别、
    /// 命名空间、翻译键一律未改；同 partial 类成员天然互相可见，跨文件调用无需改访问级别。
    /// </summary>
    public sealed partial class PersonnelArchivePanel
    {
        private void BuildFactionCodex(IArchiveService service)
        {
            cachedFactionCodex = new List<FactionCodexView>();
            if (cachedKillLines == null || cachedKillLines.Count == 0)
            {
                return;
            }

            Dictionary<string, FactionCodexView> byKey =
                new Dictionary<string, FactionCodexView>();
            Dictionary<string, HashSet<string>> battleIdsByFaction =
                new Dictionary<string, HashSet<string>>();

            for (int i = 0; i < cachedKillLines.Count; i++)
            {
                CombatLineView kv = cachedKillLines[i];
                ChronicleEvent ev = kv.TargetEvent;
                string victimStableId = ev != null && ev.Params.TryGetValue(ChronicleEventParams.VictimStableId, out string vsid) ? vsid : null;
                bool victimIsArchive = false;
                if (victimStableId != null && service != null
                    && service.GetObject(victimStableId) is PawnObject victimPawn)
                {
                    victimIsArchive = victimPawn.IsArchived;
                }

                string factionKey;
                string displayName;
                ArchiveUiStyle.FactionCodexKind kind;
                string relationKey;

                bool unknownKiller = ev != null
                    && ev.Params.TryGetValue(ChronicleEventParams.Killer, out string killerLabel)
                    && killerLabel == ChronicleEventParams.UnknownKillerLabel;

                string victimFactionDef = ev != null && ev.Params.TryGetValue(ChronicleEventParams.VictimFactionDefName, out string vfd) ? vfd : null;
                string victimFactionLabel = ev != null && ev.Params.TryGetValue(ChronicleEventParams.VictimFactionLabel, out string vfl) ? vfl : null;
                string victimCategory = ev != null && ev.Params.TryGetValue(ChronicleEventParams.VictimCategory, out string vcat) ? vcat : null;

                if (unknownKiller && string.IsNullOrEmpty(victimFactionDef))
                {
                    factionKey = FactionBucketUnknown;
                    displayName = "PersonalChronicle.UI.FactionUnknown".Translate().ToString();
                    kind = ArchiveUiStyle.FactionCodexKind.Unknown;
                    relationKey = FactionRelationUnresolved;
                }
                else if (victimIsArchive)
                {
                    factionKey = FactionBucketPlayer;
                    displayName = "PersonalChronicle.UI.FactionPlayer".Translate().ToString();
                    kind = ArchiveUiStyle.FactionCodexKind.Player;
                    relationKey = FactionRelationAlly;
                }
                else if (!string.IsNullOrEmpty(victimFactionDef))
                {
                    factionKey = victimFactionDef;
                    displayName = !string.IsNullOrEmpty(victimFactionLabel)
                        ? victimFactionLabel
                        : FactionDefLabel(victimFactionDef);
                    bool isMechanoid = victimCategory == ChronicleEventParams.VictimCategoryMechanoid;
                    bool isAnimal = victimCategory == ChronicleEventParams.VictimCategoryAnimal;
                    if (isMechanoid)
                    {
                        kind = ArchiveUiStyle.FactionCodexKind.Mechanoid;
                    }
                    else if (isAnimal)
                    {
                        kind = ArchiveUiStyle.FactionCodexKind.Animal;
                    }
                    else
                    {
                        kind = ArchiveUiStyle.FactionCodexKind.Enemy;
                    }
                    relationKey = FactionRelationHostile;
                }
                else
                {
                    // No faction: animal → wild bucket, otherwise generic factionless.
                    if (victimCategory == ChronicleEventParams.VictimCategoryAnimal)
                    {
                        factionKey = FactionBucketWild;
                        displayName = "PersonalChronicle.UI.FactionWild".Translate().ToString();
                        kind = ArchiveUiStyle.FactionCodexKind.Animal;
                    }
                    else
                    {
                        factionKey = FactionBucketFactionless;
                        displayName = "PersonalChronicle.UI.FactionFactionless".Translate().ToString();
                        kind = ArchiveUiStyle.FactionCodexKind.Unknown;
                    }
                    relationKey = FactionRelationNeutral;
                }

                if (!byKey.TryGetValue(factionKey, out FactionCodexView card))
                {
                    card = new FactionCodexView
                    {
                        FactionKey = factionKey,
                        DisplayName = displayName,
                        Kind = kind,
                        RelationKey = relationKey,
                        KillCount = 0,
                        RaidCount = 0,
                        BattleCount = 0,
                        OurLossCount = 0,
                        MemberLines = new List<CombatLineView>()
                    };
                    byKey[factionKey] = card;
                    battleIdsByFaction[factionKey] = new HashSet<string>();
                }

                card.KillCount++;
                card.MemberLines.Add(kv);
                if (victimIsArchive)
                {
                    // Our losses: a kill event whose victim is one of our archived pawns.
                    card.OurLossCount++;
                }

                // Battle participation: collect distinct battle subjects from the event.
                if (ev != null && ev.Subjects != null
                    && battleIdsByFaction.TryGetValue(factionKey, out HashSet<string> battleIds))
                {
                    for (int s = 0; s < ev.Subjects.Count; s++)
                    {
                        ObjectRef sub = ev.Subjects[s];
                        if (sub != null && sub.CategoryKey == ArchiveCategoryKeys.Battle
                            && !string.IsNullOrEmpty(sub.StableId))
                        {
                            battleIds.Add(sub.StableId);
                        }
                    }
                }
            }

            cachedFactionCodex = new List<FactionCodexView>(byKey.Values);
            // Build composition (victim kind breakdown) per card, apply distinct battle counts, then sort.
            for (int c = 0; c < cachedFactionCodex.Count; c++)
            {
                FactionCodexView cv = cachedFactionCodex[c];
                if (battleIdsByFaction.TryGetValue(cv.FactionKey, out HashSet<string> battleIds))
                {
                    cv.BattleCount = battleIds.Count;
                    cv.RaidCount = battleIds.Count;
                }
                Dictionary<string, int> kindCounts = new Dictionary<string, int>();
                for (int m = 0; m < cv.MemberLines.Count; m++)
                {
                    ChronicleEvent mev = cv.MemberLines[m].TargetEvent;
                    // Composition keys are grouping buckets only (never resolved as a Def).
                    // Use a synthetic sentinel instead of a translation key so the slot
                    // keeps a single, honest meaning: "a PawnKindDef name, or unknown".
                    string kindDef = mev != null && mev.Params.TryGetValue(ChronicleEventParams.VictimKindDefName, out string kd) && !string.IsNullOrEmpty(kd)
                        ? kd
                        : FactionBucketUnknown;
                    if (!kindCounts.TryGetValue(kindDef, out int kc))
                    {
                        kindCounts[kindDef] = 0;
                    }
                    kindCounts[kindDef]++;
                }
                cv.Composition = new List<KeyValuePair<string, int>>(kindCounts);
                cachedFactionCodex[c] = cv;
            }
            // Sort: by KillCount desc, player card pinned to bottom.
            cachedFactionCodex.Sort((a, b) =>
            {
                bool aPlayer = a.FactionKey == FactionBucketPlayer;
                bool bPlayer = b.FactionKey == FactionBucketPlayer;
                if (aPlayer != bPlayer)
                {
                    return aPlayer ? 1 : -1;
                }
                return b.KillCount.CompareTo(a.KillCount);
            });
        }
    }
}
