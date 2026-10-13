using System.Collections.Generic;
using PersonalChronicle.Api;
using PersonalChronicle.Application;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Application.Workshop;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 把「产出品质分布」契约（<see cref="WorkshopQualityAccess"/>）绑到职业台账实现上。
    ///
    /// <para><b>口径与 KPI 良品率同源</b>：同一条 <c>ItemProduced</c> 事件流、同一个
    /// <c>ExamScoring.QualityRank</c> 判档；本方法只是**保留每档计数**而不是只算一个比例
    /// （不另立门槛表 —— 否则两处判定迟早分叉）。</para>
    ///
    /// <para>注入方式照工程既定范式（<c>WorkshopOpsAccessBinder</c> 同族）：
    /// <c>[StaticConstructorOnStartup]</c> + <c>SetSource</c>，方向为 Automation → Application。</para>
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class WorkshopQualityAccessBinder
    {
        static WorkshopQualityAccessBinder()
        {
            WorkshopQualityAccess.SetSource(BuildDigest);
        }

        private static WorkshopQualityDigest BuildDigest(Map map, string recipeDefName, int windowDays)
        {
            WorkshopQualityDigest digest = default;
            digest.Bins = new int[WorkshopQualityAccess.BinKeys.Length];
            if (map == null
                || !PersonalChronicleApi.TryGet(out IPersonalChronicleApi api)
                || api.Queries == null)
            {
                return digest;   // HasData = false ⇒ 调用方走空态
            }

            IReadOnlyList<ArchiveObject> objects;
            try
            {
                objects = api.Queries.GetObjectsOfCategory(ArchiveCategoryKeys.Pawn);
            }
            catch (System.Exception)
            {
                return digest;
            }
            if (objects == null)
            {
                return digest;
            }
            digest.HasData = true;   // 查询成功（**与"有没有产出"无关**：无产出时 Total = 0）

            int days = windowDays > 0 ? windowDays : 15;
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            long windowStart = now - (long)days * GenDate.TicksPerDay;
            bool filterByRecipe = !string.IsNullOrEmpty(recipeDefName);

            int total = 0;
            int good = 0;
            for (int i = 0; i < objects.Count; i++)
            {
                CareerData career = (objects[i] as PawnObject)?.CareerData;
                if (career == null || career.Events == null)
                {
                    continue;
                }
                for (int e = 0; e < career.Events.Count; e++)
                {
                    CareerEvent ev = career.Events[e];
                    if (ev == null || ev.EventType != CareerEventType.ItemProduced)
                    {
                        continue;
                    }
                    if (ev.Tick < windowStart)
                    {
                        continue;
                    }
                    // 按配方过滤：旧存档事件 `RecipeDefName` 为 null ⇒ **不归入任何配方**
                    // （宁可少算，也不能把别的配方产出算到这台头上）。
                    if (filterByRecipe
                        && !string.Equals(ev.RecipeDefName, recipeDefName, System.StringComparison.Ordinal))
                    {
                        continue;
                    }
                    int rank = ExamScoring.QualityRank(ev.Quality);
                    if (rank < 0 || rank >= digest.Bins.Length)
                    {
                        // 无品质产物（食材/原料/零件）：不进分母（与 KPI 良品率同一处理）
                        continue;
                    }
                    digest.Bins[rank]++;
                    total++;
                    if (rank >= (int)QualityCategory.Good)
                    {
                        good++;
                    }
                }
            }
            digest.Total = total;
            digest.Good = good;
            return digest;
        }
    }
}
