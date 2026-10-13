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
    /// [ARC-013] <see cref="ArchiveService"/> 的「查询门面」切片
    /// （GetAllRecords / GetActiveRecords / GetArchivedRecords / GetEventsFor /
    /// GetObject / GetLinkedObjects / GetObjectsOfCategory / GetDataRevision /
    /// GetRecentEvents / GetAllEvents / GetCategoryBehavior）。
    ///
    /// 原 ArchiveService.cs 单文件 818 行，超出 [ARC-013] 「单文件 ≤ 800 行」上限。
    /// 按 08 §3「排序→识别→剥离」，本文件剥离其中的只读查询方法组：全部委托
    /// ChronicleGameComponent，无写入副作用，是 UI / Api 的统一读取入口。
    ///
    /// 剥离原则（[ARC-013] 零契约改动）：仅物理搬移，类名、方法签名、访问级别、
    /// 命名空间一律未改；同 partial 类成员天然互相可见，跨文件调用无需改访问级别。
    /// </summary>
    public sealed partial class ArchiveService
    {
        public IReadOnlyList<PawnRecord> GetAllRecords()
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<PawnRecord>();
            }
            return component.GetAllRecords();
        }

        public IReadOnlyList<PawnRecord> GetActiveRecords()
        {
            IReadOnlyList<PawnRecord> all = GetAllRecords();
            List<PawnRecord> result = new List<PawnRecord>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].IsArchived)
                {
                    result.Add(all[i]);
                }
            }
            return result;
        }

        public IReadOnlyList<PawnRecord> GetArchivedRecords()
        {
            IReadOnlyList<PawnRecord> all = GetAllRecords();
            List<PawnRecord> result = new List<PawnRecord>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsArchived)
                {
                    result.Add(all[i]);
                }
            }
            return result;
        }

        public IReadOnlyList<ChronicleEvent> GetEventsFor(string stableId)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ChronicleEvent>();
            }
            return component.GetEventsFor(stableId);
        }

        public ArchiveObject GetObject(string stableId)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return null;
            }
            return component.GetObject(stableId);
        }

        public IReadOnlyList<ArchiveObject> GetLinkedObjects(string stableId)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ArchiveObject>();
            }
            return component.GetLinkedObjects(stableId);
        }

        public IReadOnlyList<ArchiveObject> GetObjectsOfCategory(string categoryKey)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ArchiveObject>();
            }
            return component.GetObjectsOfCategory(categoryKey);
        }

        /// <summary>
        /// Runtime-only invalidation token for UI read models. The archive
        /// contents remain owned by ChronicleGameComponent; this value only
        /// tells consumers that a fresh snapshot is available.
        /// </summary>
        public long GetDataRevision()
        {
            ChronicleGameComponent component = Component;
            return component != null ? component.DataRevision : -1L;
        }

        /// <summary>
        /// Resolves the live Pawn matching a stable id (null if absent or dead).
        /// UI tabs (Skills/Health/Relations) call this every frame while open;
        /// the O(N) pawn scan is therefore cached: a full rebuild runs at most
        /// once per <see cref="LivePawnCacheWindow"/> ticks, and per-frame
        /// lookups are O(1) dictionary hits (no GetUniqueLoadID allocations).
        ///
        /// The cache is keyed by thingIDNumber parsed from the stableId tail
        /// ("Thing_&lt;defName&gt;_&lt;number&gt;"), matched against
        /// Pawn.thingIDNumber — zero per-pawn string allocation during scans.
        /// Dead/destroyed pawns are evicted on read; a game switch (new
        /// game/load) drops the whole cache via component-instance comparison.
        /// </summary>

        /// <summary>
        /// v5.x "在册"判定：存活 且 属于当前殖民地人口 = 在册。
        ///
        /// 判定不依赖 DeathTick（那是"归档"语义，≠ 在册）。一个存档中已有快照
        /// 但尚未死亡的殖民者，只要还活在本殖民地就应显示"在册"；死亡归档或已
        /// 离开殖民地（被放逐/卖掉/转派系）则不在册。
        ///
        /// 实现：GetLivePawn 已驱逐 dead/destroyed pawn → 能解析到即存活；
        /// TryClassifyCurrent 判定"当前殖民地人口"（地图 spawned 成员 + 商队成员）
        /// → 两条件 AND 即"存活且属于殖民地"。
        /// </summary>

        /// <summary>
        /// Parses a Thing stable id "Thing_&lt;defName&gt;_&lt;thingIDNumber&gt;".
        /// Returns false for anything not in that shape (defensive fallback).
        /// </summary>

        /// <summary>
        /// Full rebuild of the live-pawn map: every non-dead pawn on every map
        /// plus alive world pawns, keyed by thingIDNumber. thingIDNumber is a
        /// per-session unique counter, so int keys cannot collide across pawns.
        /// </summary>

        /// <summary>
        /// Exact-string fallback for stable ids that are not in the standard
        /// "Thing_&lt;defName&gt;_&lt;number&gt;" shape (same logic as the
        /// pre-v2.2 implementation).
        /// </summary>

        public IReadOnlyList<ChronicleEvent> GetRecentEvents(int count)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ChronicleEvent>();
            }
            return component.GetRecentEvents(count);
        }

        public IReadOnlyList<ChronicleEvent> GetAllEvents()
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ChronicleEvent>();
            }
            return component.GetAllEvents();
        }

        /// <summary>事件总数（O(1)：直读事件列表长度，不复制不排序）——供热路径的读数组使用。</summary>
        public int GetEventCount()
        {
            ChronicleGameComponent component = Component;
            return component != null && component.Events != null ? component.Events.Count : 0;
        }

        /// <summary>事件按时间分桶计数（转发到组件；组件侧 O(N) 单次遍历、零排序）。</summary>
        public int[] GetEventBuckets(int bucketCount, long bucketTicks)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new int[0];
            }
            return component.GetEventBuckets(bucketCount, bucketTicks);
        }

        /// <summary>按事件类别分桶计数（转发到组件；类别经 Def.kind 判定，GOV-009）。</summary>
        public int[] GetEventBucketsByKind(ChronicleEventKind kind, int bucketCount, long bucketTicks)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new int[0];
            }
            return component.GetEventBucketsByKind(kind, bucketCount, bucketTicks);
        }

        /// <summary>分身份存量趋势（按季回推，转发到组件）。</summary>
        public int[][] GetIdentityTrends(int bucketCount, long bucketTicks, int[] currentByRole)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                int[][] empty = new int[3][];
                for (int r = 0; r < 3; r++)
                {
                    empty[r] = new int[bucketCount > 0 ? bucketCount : 0];
                }
                return empty;
            }
            return component.GetIdentityTrends(bucketCount, bucketTicks, currentByRole);
        }

        public ArchiveDepthBehavior GetCategoryBehavior(string categoryKey)
        {
            if (string.IsNullOrEmpty(categoryKey))
            {
                return ArchiveDepthBehavior.Record;
            }
            // Def-driven: categoryKey bridges data keys to ArchiveCategoryDef.
            // Unknown category falls back to Record (conservative default) so a
            // missing Def degrades to archive-record behavior, never to nothing.
            ArchiveCategoryDef def = DefDatabase<ArchiveCategoryDef>.AllDefs
                .FirstOrDefault(d => d.categoryKey == categoryKey);
            if (def == null)
            {
                return ArchiveDepthBehavior.Record;
            }
            return def.behavior;
        }
    }
}
