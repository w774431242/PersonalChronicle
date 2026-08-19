using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Api;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Partial of <see cref="ArchiveService"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {

        public void OnColonistJoined(Pawn pawn)
        {
            // 无显式角色时按活读谓词判定（默认 FreeColonist）。
            PawnRole role = ChronicleColonistScanner.TryClassify(pawn, out PawnRole resolved)
                ? resolved
                : PawnRole.FreeColonist;
            OnColonistJoined(pawn, role);
        }

        public void OnColonistJoined(Pawn pawn, PawnRole role)
        {
            if (!IsRecordingEnabled() || pawn == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                string stableId = pawn.GetUniqueLoadID();
                string labelSnapshot = pawn.LabelShort;
                PawnObject record = new PawnObject
                {
                    StableId = stableId,
                    LabelSnapshot = labelSnapshot,
                    LabelShort = labelSnapshot,
                    KindDefName = pawn.kindDef != null ? pawn.kindDef.defName : null,
                    FactionDefName = pawn.Faction != null && pawn.Faction.def != null ? pawn.Faction.def.defName : null,
                    JoinTick = Find.TickManager.TicksGame,
                    DeathTick = -1L,
                    DeathCauseKey = null,
                    Role = role
                };
                PawnArchiveSnapshots.ApplyJoinSnapshots(record, pawn);
                if (!component.AddObject(record))
                {
                    return;
                }
                AddEvent(component, stableId, labelSnapshot, ChronicleEventType.Join);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record colonist join for " + (pawn != null ? pawn.LabelShort : "null") + ": " + ex.Message);
            }
        }

        public void OnRelationChanged(Pawn a, Pawn b, PawnRelationDef relationDef, bool formed)
        {
            if (!IsRecordingEnabled() || a == null || b == null || relationDef == null)
            {
                return;
            }
            if (!SocialRelationFilter.IsSignificant(relationDef))
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                // Only record when at least one party is a chronicle colonist.
                bool aIs = ChronicleColonistScanner.TryClassifyCurrent(a, out _);
                bool bIs = ChronicleColonistScanner.TryClassifyCurrent(b, out _);
                if (!aIs && !bIs)
                {
                    return;
                }

                // Keep the event primary on a real current-colony pawn. The
                // relation patch can be invoked with the non-current side as
                // argument A (especially during scenario initialization); that
                // side must never become an archive owner by accident.
                if (!aIs && bIs)
                {
                    Pawn swapPawn = a;
                    a = b;
                    b = swapPawn;
                    aIs = true;
                    bIs = false;
                }

                long now = Find.TickManager.TicksGame;
                string aId = a.GetUniqueLoadID();
                string bId = b.GetUniqueLoadID();
                string aLabel = a.LabelShort;
                string bLabel = b.LabelShort;
                string relName = relationDef.defName;
                string action = formed
                    ? ChronicleEventParams.RelationActionFormed
                    : ChronicleEventParams.RelationActionEnded;

                // Snapshot list on archived sides (ensure object exists when party is colony).
                if (aIs)
                {
                    EnsurePawnArchivedForSocial(component, a);
                    UpdateRelationSnapshot(component, aId, bId, bLabel, relName, now, formed);
                }
                if (bIs)
                {
                    EnsurePawnArchivedForSocial(component, b);
                    UpdateRelationSnapshot(component, bId, aId, aLabel, relName, now, formed);
                }

                // One Social event with Primary=a, Subject=b (both get indexed via edges).
                ChronicleEvent ev = BuildPawnEvent(aId, aLabel, ChronicleEventType.Social);
                ev.Params[ChronicleEventParams.Relation] = relName;
                ev.Params[ChronicleEventParams.RelationAction] = action;
                if (!SubjectContains(ev, bId))
                {
                    ev.Subjects.Add(ObjectRef.ForPawn(bId, bLabel));
                }
                // Cap against the primary party's budget.
                AddEvent(component, aId, ev);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record relation change: " + ex.Message);
            }
        }

        private static void EnsurePawnArchivedForSocial(ChronicleGameComponent component, Pawn pawn)
        {
            if (component == null || pawn == null)
            {
                return;
            }
            string id = pawn.GetUniqueLoadID();
            if (component.GetObject(id) != null)
            {
                return;
            }
            // Lightweight ensure: join-style record so Relations list can attach.
            // Prefer scanner role; JoinTick 走统一默认决策（新档=开局0 / 读档=当天起点）
            // ——绝不可硬编码 -1L，否则开局殖民者被社交事件先建档后永久定格为"中途加入"。
            if (!ChronicleColonistScanner.TryClassifyCurrent(pawn, out PawnRole role))
            {
                return;
            }
            PawnObject record = new PawnObject
            {
                StableId = id,
                LabelSnapshot = pawn.LabelShort,
                LabelShort = pawn.LabelShort,
                KindDefName = pawn.kindDef != null ? pawn.kindDef.defName : null,
                FactionDefName = pawn.Faction != null && pawn.Faction.def != null ? pawn.Faction.def.defName : null,
                JoinTick = component.ResolveDefaultJoinTick(),
                DeathTick = -1L,
                Role = role
            };
            PawnArchiveSnapshots.ApplyJoinSnapshots(record, pawn);
            component.AddObject(record);
        }

        private static void UpdateRelationSnapshot(
            ChronicleGameComponent component,
            string selfId,
            string otherId,
            string otherLabel,
            string relationDefName,
            long now,
            bool formed)
        {
            PawnObject self = component.GetObject(selfId) as PawnObject;
            if (self == null)
            {
                return;
            }
            if (self.Relations == null)
            {
                self.Relations = new List<SignificantRelation>();
            }
            if (formed)
            {
                // Close any still-active matching pair then append.
                for (int i = 0; i < self.Relations.Count; i++)
                {
                    SignificantRelation r = self.Relations[i];
                    if (r != null && r.IsActive
                        && r.RelationDefName == relationDefName
                        && r.OtherStableId == otherId)
                    {
                        r.EndedTick = now;
                    }
                }
                self.Relations.Add(new SignificantRelation
                {
                    RelationDefName = relationDefName,
                    OtherStableId = otherId,
                    OtherLabel = otherLabel,
                    FormedTick = now,
                    EndedTick = -1L
                });
                // Cap relation history.
                const int maxRel = 48;
                while (self.Relations.Count > maxRel)
                {
                    self.Relations.RemoveAt(0);
                }
            }
            else
            {
                for (int i = self.Relations.Count - 1; i >= 0; i--)
                {
                    SignificantRelation r = self.Relations[i];
                    if (r != null && r.IsActive
                        && r.RelationDefName == relationDefName
                        && r.OtherStableId == otherId)
                    {
                        r.EndedTick = now;
                        break;
                    }
                }
            }
            component.MarkChanged();
        }


    }
}
