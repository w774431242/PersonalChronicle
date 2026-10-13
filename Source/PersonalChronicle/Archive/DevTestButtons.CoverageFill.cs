using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Capture;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// Dev-only: one-click fill of every coverage dimension, including real
    /// map things (workbench, weapon, exam items) bound to archived pawns.
    /// </summary>
    public static partial class DevTestButtons
    {
        private static readonly string[] ExamThingDefNames =
        {
            "PC_Exam_PracticeKit", "PC_Exam_Thesis", "PC_Exam_Diploma", "PC_Exam_Practical",
            "PC_Exam_Textbook", "PC_Exam_ResearchDoc", "PC_Exam_SkillBook", "PC_Exam_TheoryBook",
            "PC_Exam_ThesisProposal",
            "PC_Exam_PracticeKit_Engineering", "PC_Exam_PracticeKit_Manufacturing",
            "PC_Exam_PracticeKit_Agriculture", "PC_Exam_PracticeKit_Forestry",
            "PC_Exam_PracticeKit_Medicine", "PC_Exam_PracticeKit_Mining"
        };

        /// <summary>
        /// Fill every archived humanlike on loaded maps, then open the coverage report.
        /// </summary>
        internal static void FillColonyAndReport(Pawn hint)
        {
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            if (service == null || Current.Game == null)
            {
                return;
            }
            List<Pawn> targets = CollectAndArchiveAllHumanlikes(service, hint);
            int filled = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                try
                {
                    Generate(targets[i], i == 0, false);
                    filled++;
                }
                catch (Exception ex)
                {
                    ChronicleLog.Warning(ChronicleLog.Category.Ui, "Dev fill pawn failed: " + ex.Message);
                }
            }
            ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
            EnsureAllArchivedHaveRelations(targets);
            if (component != null)
            {
                component.MarkChanged();
            }
            Messages.Message("PersonalChronicle.UI.Dev.FillAllDone".Translate(filled.ToString()).ToString(),
                MessageTypeDefOf.NeutralEvent, false);
            try
            {
                DevCoverageProbe.CoverageReport report = DevCoverageProbe.ProbeColony();
                Find.WindowStack.Add(new Dialog_DevCoverageReport(report));
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Ui, "Dev coverage after fill failed: " + ex.Message);
            }
        }

        private static void BindWorldCoverage(Pawn pawn, IArchiveService service, Random rng)
        {
            if (pawn == null || service == null)
            {
                return;
            }
            PawnObject record = GetCareerPawnObject(pawn);
            if (record == null)
            {
                return;
            }
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            ForceFillLedgers(record, rng, now);
            BindKinRelation(pawn, record, now);
            if (pawn.Map != null)
            {
                BindWorkplace(pawn, record, service, rng, now);
                BindResidence(pawn, record, now);
                BindPlaceHistory(pawn, record, now);
                SpawnAndBindWeapon(pawn, record, service, now);
                SpawnExamItems(pawn);
            }
            else
            {
                BindResidenceOffMap(record, now);
                BindPlaceHistoryOffMap(record, now);
            }
            BindDeepCoverage(pawn, record, rng, now);
        }

        /// <summary>
        /// 工时/产出/参战不走 RecordSample（该入口拒绝访客且拒绝已建档冻结），直接写累加器。
        /// </summary>
        private static void ForceFillLedgers(PawnObject record, Random rng, long now)
        {
            if (record.WorkTime == null) record.WorkTime = new WorkTimeAccumulator();
            if (record.WorkTime.TicksByWorkType == null || record.WorkTime.TicksByWorkType.Count == 0)
            {
                record.WorkTime.AddSample("Mining", rng.Next(8000, 40000), now);
                record.WorkTime.AddSample("Construction", rng.Next(4000, 20000), now);
                record.WorkTime.AddSample("Crafting", rng.Next(4000, 20000), now);
            }
            if (record.Production == null) record.Production = new ProductionAccumulator();
            if (record.Production.QuantityByDef == null || record.Production.QuantityByDef.Count == 0)
            {
                record.Production.Add("Steel", rng.Next(10, 80), rng.Next(50, 400), now);
                record.Production.Add("ComponentIndustrial", rng.Next(2, 20), rng.Next(40, 200), now);
            }
            if (record.ParticipatedBattles <= 0)
            {
                record.ParticipatedBattles = rng.Next(1, 4);
            }
            if (record.MeleeKills + record.RangedKills <= 0)
            {
                record.MeleeKills = rng.Next(2, 12);
                record.RangedKills = rng.Next(2, 12);
                record.DamageDealtTotal = rng.Next(200, 4000);
            }
        }

        private static void BindResidenceOffMap(PawnObject record, long now)
        {
            if (record.Residence == null) record.Residence = new ResidenceSnapshot();
            if (string.IsNullOrEmpty(record.Residence.RoomRoleDefName))
            {
                record.Residence.RecordSeen("Bedroom", now);
            }
        }

        private static void BindPlaceHistoryOffMap(PawnObject record, long now)
        {
            if (record.PlaceHistory == null) record.PlaceHistory = new List<PlaceVisit>();
            if (record.PlaceHistory.Count == 0)
            {
                record.PlaceHistory.Add(new PlaceVisit("tile:dev", "Map", now - 60000L));
            }
        }

        private static List<Pawn> CollectAndArchiveAllHumanlikes(IArchiveService service, Pawn hint)
        {
            List<Pawn> list = new List<Pawn>();
            HashSet<string> seen = new HashSet<string>();
            if (Find.Maps != null)
            {
                for (int m = 0; m < Find.Maps.Count; m++)
                {
                    Map map = Find.Maps[m];
                    if (map == null || map.mapPawns == null) continue;
                    List<Pawn> all = map.mapPawns.AllPawns;
                    for (int i = 0; i < all.Count; i++)
                    {
                        Pawn p = all[i];
                        if (p == null || p.RaceProps == null || !p.RaceProps.Humanlike) continue;
                        string id = p.GetUniqueLoadID();
                        if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                        if (!(service.GetObject(id) is PawnObject))
                        {
                            try { service.OnColonistJoined(p); }
                            catch { }
                        }
                        list.Add(p);
                    }
                }
            }
            if (list.Count == 0 && hint != null)
            {
                try { service.OnColonistJoined(hint); } catch { }
                list.Add(hint);
            }
            return list;
        }

        /// <summary>
        /// 深度灌满：技能快照、背景、工坊历史、别名、地点图、消耗类目。
        /// 专业技能本体由 CareerRandomize.FillAllProfessionalSkills 写入。
        /// </summary>
        private static void BindDeepCoverage(Pawn pawn, PawnObject record, Random rng, long now)
        {
            if (pawn.story != null)
            {
                if (pawn.story.Childhood != null)
                {
                    record.ChildhoodBackstoryDefName = pawn.story.Childhood.defName;
                }
                if (pawn.story.Adulthood != null)
                {
                    record.AdulthoodBackstoryDefName = pawn.story.Adulthood.defName;
                }
            }
            if (pawn.Map != null && pawn.Map.Biome != null)
            {
                record.PrimaryPlaceDefName = pawn.Map.Biome.defName;
            }
            if (record.SkillSnapshot == null)
            {
                record.SkillSnapshot = new Dictionary<string, int>();
            }
            if (pawn.skills != null && pawn.skills.skills != null)
            {
                for (int i = 0; i < pawn.skills.skills.Count; i++)
                {
                    SkillRecord rec = pawn.skills.skills[i];
                    if (rec != null && rec.def != null)
                    {
                        record.SkillSnapshot[rec.def.defName] = rec.Level;
                    }
                }
            }
            if (record.Workplace != null)
            {
                if (record.WorkplaceHistory == null)
                {
                    record.WorkplaceHistory = new List<WorkplaceHistoryEntry>();
                }
                bool has = false;
                for (int i = 0; i < record.WorkplaceHistory.Count; i++)
                {
                    if (record.WorkplaceHistory[i] != null
                        && record.WorkplaceHistory[i].BuildingStableId == record.Workplace.BuildingStableId)
                    {
                        has = true;
                        break;
                    }
                }
                if (!has)
                {
                    WorkplaceHistoryEntry hist = new WorkplaceHistoryEntry(
                        record.Workplace.BuildingDefName,
                        record.Workplace.BuildingStableId,
                        record.Workplace.RoomRoleDefName,
                        record.Workplace.MapIndex,
                        record.Workplace.Cell,
                        now);
                    hist.UseCount = record.Workplace.UseCount;
                    hist.LastUsedTick = record.Workplace.LastUsedTick;
                    record.WorkplaceHistory.Add(hist);
                }
                ChronicleGameComponent comp = Current.Game != null
                    ? Current.Game.GetComponent<ChronicleGameComponent>() : null;
                if (comp != null && !string.IsNullOrEmpty(record.Workplace.BuildingStableId))
                {
                    try
                    {
                        comp.SetBuildingAlias(record.Workplace.BuildingStableId,
                            "PersonalChronicle.UI.Dev.FillAll".Translate() + "-" + pawn.LabelShort);
                    }
                    catch
                    {
                    }
                }
            }
            if (record.Consumption != null)
            {
                record.Consumption.Add("Foods", 40f + rng.Next(10, 80), now);
                record.Consumption.Add("Medicine", 15f + rng.Next(5, 40), now);
                record.Consumption.Add("Drugs", 8f + rng.Next(1, 20), now);
            }
            try
            {
                LocationAtlasCapture.Reconcile(
                    Current.Game.GetComponent<ChronicleGameComponent>(), now);
            }
            catch
            {
            }
        }

        private static void BindWorkplace(Pawn pawn, PawnObject record, IArchiveService service, Random rng, long now)
        {
            Building_WorkTable table = FindOrSpawnWorkTable(pawn);
            if (table == null)
            {
                return;
            }
            try
            {
                service.OnWorkplaceUsed(pawn, table);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Ui, "Dev workplace bind failed: " + ex.Message);
            }
            string defName = table.def != null ? table.def.defName : "TableMachining";
            string stable = defName + ":" + table.thingIDNumber;
            string role = null;
            Room room = table.GetRoom();
            if (room != null && room.Role != null)
            {
                role = room.Role.defName;
            }
            record.Workplace = new WorkplaceSnapshot
            {
                BuildingDefName = defName,
                BuildingStableId = stable,
                RoomRoleDefName = string.IsNullOrEmpty(role) ? "Workshop" : role,
                UseCount = rng.Next(20, 400),
                LastUsedTick = now,
                MapIndex = pawn.Map.Index,
                Cell = table.Position
            };
        }

        private static Building_WorkTable FindOrSpawnWorkTable(Pawn pawn)
        {
            List<Building> buildings = pawn.Map.listerBuildings.allBuildingsColonist;
            if (buildings != null)
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building_WorkTable existing = buildings[i] as Building_WorkTable;
                    if (existing != null && existing.Spawned)
                    {
                        return existing;
                    }
                }
            }
            string[] names = { "ElectricSmithy", "FueledSmithy", "TableSculpting", "TableMachining", "CraftingSpot" };
            ThingDef def = null;
            for (int i = 0; i < names.Length; i++)
            {
                def = DefDatabase<ThingDef>.GetNamedSilentFail(names[i]);
                if (def != null)
                {
                    break;
                }
            }
            if (def == null)
            {
                return null;
            }
            IntVec3 cell;
            if (!CellFinder.TryFindRandomCellNear(pawn.Position, pawn.Map, 8,
                c => c.Standable(pawn.Map) && pawn.Map.thingGrid.ThingsListAtFast(c).Count == 0, out cell))
            {
                cell = pawn.Position;
            }
            Thing spawned = GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, pawn.Map);
            return spawned as Building_WorkTable;
        }

        private static void BindResidence(Pawn pawn, PawnObject record, long now)
        {
            if (record.Residence == null)
            {
                record.Residence = new ResidenceSnapshot();
            }
            string role = "Bedroom";
            IntVec3 cell = pawn.Position;
            if (pawn.ownership != null && pawn.ownership.OwnedRoom != null
                && pawn.ownership.OwnedRoom.Role != null)
            {
                role = pawn.ownership.OwnedRoom.Role.defName;
                foreach (IntVec3 c in pawn.ownership.OwnedRoom.Cells)
                {
                    cell = c;
                    break;
                }
            }
            record.Residence.RecordSeen(role, now, pawn.Map.Index, cell);
        }

        private static void BindPlaceHistory(Pawn pawn, PawnObject record, long now)
        {
            if (record.PlaceHistory == null)
            {
                record.PlaceHistory = new List<PlaceVisit>();
            }
            string key = pawn.Map.Biome != null ? pawn.Map.Biome.defName : "tile:0";
            for (int i = 0; i < record.PlaceHistory.Count; i++)
            {
                if (record.PlaceHistory[i] != null && record.PlaceHistory[i].PlaceKey == key)
                {
                    return;
                }
            }
            record.PlaceHistory.Add(new PlaceVisit(key, "Map", now - 60000L));
        }

        private static void BindKinRelation(Pawn pawn, PawnObject record, long now)
        {
            if (record == null)
            {
                return;
            }
            Pawn other = FindOtherHumanlike(pawn);
            if (other == null)
            {
                return;
            }
            AddSiblingRelation(record, other.GetUniqueLoadID(), other.LabelShort, now);
        }

        /// <summary>
        /// 灌满后兜底：每个已归档 humanlike 至少一条亲缘关系。
        /// BindKinRelation 在单人图 / 无 FreeColonist 时会空过，导致社交覆盖停在部分（如 75%）。
        /// </summary>
        private static void EnsureAllArchivedHaveRelations(List<Pawn> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return;
            }
            List<PawnObject> records = new List<PawnObject>();
            for (int i = 0; i < targets.Count; i++)
            {
                PawnObject rec = GetCareerPawnObject(targets[i]);
                if (rec != null)
                {
                    records.Add(rec);
                }
            }
            if (records.Count == 0)
            {
                return;
            }
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            for (int i = 0; i < records.Count; i++)
            {
                PawnObject rec = records[i];
                if (rec.Relations != null && rec.Relations.Count > 0)
                {
                    continue;
                }
                PawnObject other = records[(i + 1) % records.Count];
                if (other == rec)
                {
                    AddSiblingRelation(rec, rec.StableId + ":kin", rec.LabelShort ?? rec.LabelSnapshot, now);
                    continue;
                }
                AddSiblingRelation(rec, other.StableId, other.LabelShort ?? other.LabelSnapshot, now);
            }
        }

        private static Pawn FindOtherHumanlike(Pawn pawn)
        {
            if (pawn == null || Find.Maps == null)
            {
                return null;
            }
            for (int m = 0; m < Find.Maps.Count; m++)
            {
                Map map = Find.Maps[m];
                if (map == null || map.mapPawns == null)
                {
                    continue;
                }
                List<Pawn> all = map.mapPawns.AllPawns;
                if (all == null)
                {
                    continue;
                }
                for (int i = 0; i < all.Count; i++)
                {
                    Pawn p = all[i];
                    if (p != null && p != pawn && p.RaceProps != null && p.RaceProps.Humanlike)
                    {
                        return p;
                    }
                }
            }
            return null;
        }

        private static void AddSiblingRelation(PawnObject record, string otherId, string otherLabel, long now)
        {
            if (record == null || string.IsNullOrEmpty(otherId))
            {
                return;
            }
            if (record.Relations == null)
            {
                record.Relations = new List<SignificantRelation>();
            }
            for (int i = 0; i < record.Relations.Count; i++)
            {
                if (record.Relations[i] != null && record.Relations[i].OtherStableId == otherId)
                {
                    return;
                }
            }
            record.Relations.Add(new SignificantRelation
            {
                RelationDefName = "Sibling",
                OtherStableId = otherId,
                OtherLabel = otherLabel,
                FormedTick = now - 120000L,
                EndedTick = -1L
            });
        }

        private static void SpawnAndBindWeapon(Pawn pawn, PawnObject record, IArchiveService service, long now)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_Knife")
                ?? DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_ShortSword")
                ?? DefDatabase<ThingDef>.GetNamedSilentFail("Gun_Revolver");
            if (def == null)
            {
                return;
            }
            ThingDef stuff = def.MadeFromStuff ? ThingDefOf.Steel : null;
            Thing weapon;
            try
            {
                weapon = ThingMaker.MakeThing(def, stuff);
            }
            catch
            {
                return;
            }
            if (weapon == null)
            {
                return;
            }
            bool equipped = false;
            ThingWithComps eq = weapon as ThingWithComps;
            if (eq != null && pawn.equipment != null && pawn.equipment.Primary == null)
            {
                try
                {
                    pawn.equipment.AddEquipment(eq);
                    equipped = true;
                }
                catch
                {
                    equipped = false;
                }
            }
            if (!equipped)
            {
                GenPlace.TryPlaceThing(weapon, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
            try
            {
                service.OnThingCrafted(weapon, pawn);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Ui, "Dev weapon craft failed: " + ex.Message);
            }
        }

        private static void SpawnExamItems(Pawn pawn)
        {
            for (int i = 0; i < ExamThingDefNames.Length; i++)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(ExamThingDefNames[i]);
                if (def == null)
                {
                    continue;
                }
                Thing item;
                try
                {
                    item = ThingMaker.MakeThing(def);
                }
                catch
                {
                    continue;
                }
                if (item == null)
                {
                    continue;
                }
                bool stored = false;
                if (pawn.inventory != null)
                {
                    stored = pawn.inventory.innerContainer.TryAdd(item, true);
                }
                if (!stored)
                {
                    GenPlace.TryPlaceThing(item, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }
            }
        }
    }
}
