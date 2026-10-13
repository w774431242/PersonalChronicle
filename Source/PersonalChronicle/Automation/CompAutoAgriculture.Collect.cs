using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 掉落物自动收集 + 向设备收敛的粒子动画（PM 需求 2026-09-06）。
    /// 拆成 partial：主文件已 800+ 行，收集逻辑独立成文件，单文件长度受控。
    ///
    /// 设计纪律：
    /// - <b>逻辑先行，动画纯装饰</b>：物品先 <see cref="IItemStorage.Push"/> 入仓再从地图上移除，
    ///   动画只是表现；动画丢帧、上限削峰、切换地图都不会造成物品丢失（对齐反压零丢失铁律）。
    /// - <b>分片扫描</b>：每波只扫固定数量 cell（轮转游标），工作量与覆盖区大小无关
    ///   （L 档 21×21=441 格也不会一次全扫），与播种/收割同一套节拍纪律。
    /// - <b>削峰</b>：每波最多收 <see cref="CollectPerWave"/> 堆、动画并发不超过
    ///   <see cref="MaxConcurrentFx"/>；超上限只跳过表现，不跳过收集。
    /// - <b>非当前地图零开销</b>：不在玩家视野的地图不播动画也不留残留。
    /// </summary>
    public partial class CompAutoAgriculture : ThingComp
    {
        /// <summary>收集节拍（tick）：与传送带/端口同拍，观感一致。</summary>
        private const int CollectIntervalTicks = 90;

        /// <summary>一波最多收集几堆（削峰，避免大丰收瞬间爆量）。</summary>
        private const int CollectPerWave = 4;

        /// <summary>一波最多扫描多少格（边界固定，与覆盖区大小无关）。</summary>
        private const int CollectScanCells = 96;

        /// <summary>单条收敛动画时长（tick）。</summary>
        private const int FxDurationTicks = 30;

        /// <summary>动画每隔几 tick 播一颗粒子（越小越连贯、越费）。4 ≈ 每条约 7 颗。</summary>
        private const int FxStepTicks = 4;

        /// <summary>本机同时活跃的收敛动画上限（超了就只入仓不播动画）。</summary>
        private const int MaxConcurrentFx = 12;

        private int collectQuota;
        private int collectCursor;

        /// <summary>活跃动画（纯视觉，不持久化：读档后动画丢失不影响任何物品）。</summary>
        private readonly List<CollectFx> collectFx = new List<CollectFx>();

        private struct CollectFx
        {
            public Vector3 From;
            public Vector3 To;
            public int Elapsed;
            public int Step;
        }

        // ⭐ 已删除全局「全部植物产物」白名单 HarvestProducts：它会吸走覆盖区内
        // 任何植物的产物（树→原木、mod 金属作物→钢铁，PM 实机图证 2026-09-07）。
        // 仓储与收集改认实例级 CurrentCropProduct——设备当前选定作物的产物。

        private void TickCollect(int interval)
        {
            Map map = parent.Map;
            AdvanceCollectFx(map, interval);
            if (!Props.collectDrops || itemStorage == null)
            {
                return;
            }
            collectQuota += interval;
            if (collectQuota < CollectIntervalTicks)
            {
                return;
            }
            collectQuota = 0;
            CollectWave(map);
        }

        /// <summary>扫一小片覆盖区，把可收的掉落物收进内置仓（每格每波最多一堆，轮转保证公平）。</summary>
        private void CollectWave(Map map)
        {
            if (coverage.Count == 0)
            {
                return;
            }
            // 玩家可能不经配置窗直接改种植区作物：每波刷新一次当前作物缓存
            // （O(zones×cells)，90 tick 一次，开销可忽略）。
            RefreshCropFromZones();
            int taken = 0;
            int scanned = 0;
            while (scanned < CollectScanCells && taken < CollectPerWave)
            {
                scanned++;
                collectCursor = (collectCursor + 1) % coverage.Count;
                IntVec3 cell = coverage[collectCursor];
                if (!cell.InBounds(map))
                {
                    continue;
                }
                List<Thing> things = map.thingGrid.ThingsListAt(cell);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    Thing thing = things[i];
                    if (!IsCollectable(thing, cell, map))
                    {
                        continue;
                    }
                    int accepted = itemStorage.Push(thing.def, thing.stackCount);
                    if (accepted <= 0)
                    {
                        // 仓满（含被端口反压顶住）：本波到此为止，物品留在原地。
                        taken = CollectPerWave;
                        break;
                    }
                    Vector3 from = thing.DrawPos;
                    thing.stackCount -= accepted;
                    if (thing.stackCount <= 0)
                    {
                        thing.Destroy(DestroyMode.Vanish);
                    }
                    StartCollectFx(from);
                    taken++;
                    break;
                }
            }
        }

        /// <summary>
        /// 可收判定：种植区内的地面农作物掉落物。
        /// - 必须在种植区（<see cref="Zone_Growing"/>）内：种植区外玩家随手放的
        ///   金属/材料/建材绝不吸走（PM 实测教训）；
        /// - 已在仓库/货架上的不动（只收散落物）；玩家标记禁用的不动（尊重意图）；
        /// - 只收农作物产物。
        /// </summary>
        private bool IsCollectable(Thing thing, IntVec3 cell, Map map)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != parent.Map)
            {
                return false;
            }
            if (thing.def == null || thing.def.category != ThingCategory.Item)
            {
                return false;
            }
            if (!(cell.GetZone(map) is Zone_Growing))
            {
                return false;
            }
            if (thing.IsForbidden(Faction.OfPlayer))
            {
                return false;
            }
            if (StoreUtility.IsInValidStorage(thing))
            {
                return false;
            }
            // ⭐ 只收「设备当前选定作物」的产物（PM 2026-09-07：原木/钢铁混仓根因
            // 就是这里用了全局全植物白名单）。无作物配置时不收集任何东西。
            ThingDef product = CurrentCropProduct;
            return product != null && thing.def == product;
        }

        private void StartCollectFx(Vector3 from)
        {
            if (!Props.collectVisual || parent.Map != Find.CurrentMap)
            {
                return;
            }
            if (collectFx.Count >= MaxConcurrentFx)
            {
                return; // 削峰：只省表现，不省收集。
            }
            collectFx.Add(new CollectFx
            {
                From = from,
                To = parent.TrueCenter(),
                Elapsed = 0,
                Step = 0
            });
        }

        /// <summary>
        /// 推进动画：沿「掉落物 → 设备中心」的<b>最短直径</b>插值播尘粒。
        /// 该直线即两点间最短路径，也正好是设备为圆心的那条半径/直径所在直线——
        /// 四周掉落物各自沿自己的直径向心汇聚，整体即「圆形收敛」。
        /// 观感强化：t² 加速（越近越快，吸积感）+ 粒子随进度缩小（越近越小，像被吸进去）。
        /// 全程只调 <see cref="FleckMaker"/> —— Fleck 不是 Thing：不进 thingGrid、
        /// 不 tick、不存档，原版雨雪/血迹/尘土同款机制，是本效果的性能前提。
        /// </summary>
        private void AdvanceCollectFx(Map map, int interval)
        {
            if (collectFx.Count == 0)
            {
                return;
            }
            if (map != Find.CurrentMap)
            {
                collectFx.Clear();
                return;
            }
            for (int i = collectFx.Count - 1; i >= 0; i--)
            {
                CollectFx f = collectFx[i];
                f.Elapsed += interval;
                if (f.Elapsed > FxDurationTicks)
                {
                    collectFx.RemoveAt(i);
                    continue;
                }
                f.Step += interval;
                if (f.Step < FxStepTicks)
                {
                    collectFx[i] = f;
                    continue;
                }
                f.Step = 0;
                float t = (float)f.Elapsed / FxDurationTicks;
                // 沿最短直径向心：位置 t² 加速，尺寸随之收缩（吸入感）。
                Vector3 at = Vector3.Lerp(f.From, f.To, t * t);
                FleckMaker.Static(at, map, FleckDefOf.DustPuff, Mathf.Lerp(1f, 0.35f, t));
                collectFx[i] = f;
            }
        }
    }
}
