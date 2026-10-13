using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Api;
using PersonalChronicle.Api.DomainProviders;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;
using UnityEngine;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// [ARC-013] <see cref="ArchiveUiDataProvider"/> 的「社会关系（Social Ties）Read Model」切片
    /// （v4.6，merges live state + archived snapshots）。
    ///
    /// 原 ArchiveUiDataProvider.Legacy.cs 单文件 816 行，混合「装备溯源 Legacy」
    /// 与「社会关系 Read Model」两组职责，违反 [ARC-013] 单一职责与「单文件 ≤ 800 行」。
    /// 按 08 §3「排序→识别→剥离」，本文件剥离其中的社会关系 Read Model（自包含、纯展示数据组装）。
    ///
    /// 剥离原则（[ARC-013] 零契约改动）：仅物理搬移，类名、方法签名、访问级别、
    /// 命名空间、翻译键一律未改；同 partial 类成员天然互相可见，跨文件调用无需改访问级别。
    /// </summary>
    public sealed partial class ArchiveUiDataProvider
    {
        // ---- v4.6 social ties (Read Model; merges live state + archived snapshots) ----

        private static IReadOnlyList<RelationView> BuildRelations(IArchiveService service, PawnObject pawn)
        {
            List<RelationView> rows = new List<RelationView>();
            HashSet<string> seen = new HashSet<string>();
            if (service == null || pawn == null)
            {
                return rows;
            }

            Pawn livePawn = service.GetLivePawn(pawn.StableId);

            // 1) Current live relations.
            if (livePawn?.relations?.DirectRelations != null)
            {
                List<DirectPawnRelation> directRelations = livePawn.relations.DirectRelations;
                for (int i = 0; i < directRelations.Count; i++)
                {
                    DirectPawnRelation rel = directRelations[i];
                    if (rel?.def == null || rel.otherPawn == null)
                    {
                        continue;
                    }
                    if (!SocialRelationFilter.IsSignificant(rel.def))
                    {
                        continue;
                    }
                    string otherId = rel.otherPawn.GetUniqueLoadID();
                    string key = MakeRelationKey(rel.def.defName, otherId);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    rows.Add(new RelationView
                    {
                        OtherLabel = rel.otherPawn.LabelShort,
                        RelationDefName = rel.def != null ? rel.def.defName : null,
                        RelationLabel = RelationLabelFor(rel.def, rel.otherPawn),
                        StatusLabel = rel.otherPawn.Dead
                            ? "PersonalChronicle.UI.Dead".Translate().ToString()
                            : "PersonalChronicle.UI.Alive".Translate().ToString(),
                        IsLive = true
                    });
                }
            }

            // 2) Archived relations (initial ties captured at join, plus historical changes).
            if (pawn.Relations != null)
            {
                for (int i = 0; i < pawn.Relations.Count; i++)
                {
                    SignificantRelation rel = pawn.Relations[i];
                    if (rel == null)
                    {
                        continue;
                    }
                    string key = MakeRelationKey(rel.RelationDefName, rel.OtherStableId);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    Pawn otherLive = service.GetLivePawn(rel.OtherStableId);
                    bool otherDead = otherLive != null && otherLive.Dead;
                    string status = otherDead
                        ? "PersonalChronicle.UI.Dead".Translate().ToString()
                        : (rel.IsActive
                            ? "PersonalChronicle.UI.Alive".Translate().ToString()
                            : "PersonalChronicle.UI.RelEnded".Translate().ToString());
                    PawnRelationDef def = DefDatabase<PawnRelationDef>.GetNamedSilentFail(rel.RelationDefName);
                    rows.Add(new RelationView
                    {
                        OtherLabel = !string.IsNullOrEmpty(rel.OtherLabel) ? rel.OtherLabel : rel.OtherStableId,
                        RelationDefName = rel.RelationDefName,
                        RelationLabel = RelationLabelFor(def, otherLive, rel.RelationDefName),
                        StatusLabel = status,
                        IsLive = !otherDead && rel.IsActive
                    });
                }
            }

            // Stable presentation order: partners → blood kin → friends → rivals →
            // ended ties. Sorting belongs to the Read Model, never to the window.
            // Labels are resolved once here rather than per comparison, since a
            // sort evaluates the comparator O(n log n) times.
            string endedLabel = "PersonalChronicle.UI.RelEnded".Translate().ToString();
            string friendLabel = FriendLabel();
            string rivalLabel = RivalLabel();
            rows.Sort((a, b) =>
            {
                int ra = RelationSortRank(a, endedLabel, friendLabel, rivalLabel);
                int rb = RelationSortRank(b, endedLabel, friendLabel, rivalLabel);
                if (ra != rb)
                {
                    return ra.CompareTo(rb);
                }
                return string.Compare(a?.OtherLabel ?? string.Empty,
                    b?.OtherLabel ?? string.Empty, System.StringComparison.CurrentCulture);
            });

            return rows;
        }

        /// <summary>
        /// Presentation rank for the social list. Ended ties always sink to the
        /// bottom so the current social circle reads first.
        /// </summary>
        private static int RelationSortRank(
            RelationView row, string endedLabel, string friendLabel, string rivalLabel)
        {
            if (row == null)
            {
                return 99;
            }
            if (row.StatusLabel == endedLabel)
            {
                return 50;
            }
            string label = row.RelationLabel ?? string.Empty;
            if (label == friendLabel)
            {
                return 20;
            }
            if (label == rivalLabel)
            {
                return 30;
            }
            return row.IsLive ? 0 : 10;
        }

        private static string FriendLabel()
        {
            return "PersonalChronicle.Relation.Friend".Translate().ToString();
        }

        private static string RivalLabel()
        {
            return "PersonalChronicle.Relation.Rival".Translate().ToString();
        }

        private static string MakeRelationKey(string relationDefName, string otherStableId)
        {
            return (relationDefName ?? string.Empty) + "::" + (otherStableId ?? string.Empty);
        }

        private static string RelationLabelFor(PawnRelationDef def, Pawn otherPawn)
        {
            return RelationLabelFor(def, otherPawn, null);
        }

        /// <summary>
        /// Resolves a display label. <paramref name="relationDefName"/> lets
        /// synthesized opinion ties (friend/rival) resolve to a translated label
        /// even though they have no backing PawnRelationDef.
        /// </summary>
        private static string RelationLabelFor(PawnRelationDef def, Pawn otherPawn, string relationDefName)
        {
            if (def == null)
            {
                if (relationDefName == SocialRelationFilter.FriendRelationKey)
                {
                    return FriendLabel();
                }
                if (relationDefName == SocialRelationFilter.RivalRelationKey)
                {
                    return RivalLabel();
                }
                return string.Empty;
            }
            if (otherPawn != null)
            {
                string gendered = def.GetGenderSpecificLabel(otherPawn);
                if (!string.IsNullOrEmpty(gendered))
                {
                    return gendered;
                }
            }
            if (!string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return def.defName;
        }
    }
}
