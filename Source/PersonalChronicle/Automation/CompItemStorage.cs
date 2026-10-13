using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 物品储物仓参数（Def 级，按设备差异化容量）。
    /// ⚠️ <c>capacity &lt;= 0</c> = 无上限（PM 2026-09-06 要求：设备存储不设限）。
    /// </summary>
    public class CompProperties_ItemStorage : CompProperties
    {
        /// <summary>仓储总容量（件）。&lt;=0 = 无上限；S/M/L 各自配置。</summary>
        public int capacity = 250;

        /// <summary>
        /// 只收农作物产物（白名单 = <see cref="CompAutoAgriculture.HarvestProducts"/>）。
        /// 农机 Def 显式开启：无论收割、掉落物收集还是管道送入，非作物一律拒收
        /// （PM 实测：覆盖区内 mod 金属作物让钢铁混进了粮食仓）。
        /// </summary>
        public bool cropOnly = false;

        public CompProperties_ItemStorage()
        {
            compClass = typeof(CompItemStorage);
        }
    }

    /// <summary>
    /// 自动化设备内置物品储物仓。
    ///
    /// 产出先进仓、满了才落地；玩家经检视面板看存量、互动键一键取出。
    /// 实现 <see cref="IItemStorage"/> —— 未来管道运输只对该接口读写（预留接入口）。
    ///
    /// 铁律对齐：缓冲只存 ThingDef+count（产线铁律①）；落地失败 Destroy 兜底（铁律④）；
    /// 互动键 Gizmo 实例级缓存，每帧不 new（UI 设计系统铁律）。
    /// </summary>
    public class CompItemStorage : ThingComp, IItemStorage
    {
        // 平行表：ThingDef 走 Reference、数量走 Value。⚠️ 不能 readonly：
        // Scribe_Collections.Look(ref ...) 需要可写引用（产线既有纪律）。
        private List<ThingDef> bufferDefs = new List<ThingDef>();
        private List<int> bufferCounts = new List<int>();

        private Command_Action unloadGizmo;

        /// <summary>cropOnly 拒收告警的节流时间戳（static：多机共用一条告警通道即可）。</summary>
        private static int lastCropRejectTick = int.MinValue;

        /// <summary>
        /// cropOnly 白名单判定：只收「设备当前选定作物」的产物。
        /// 农机挂 <see cref="CompAutoAgriculture"/> 时用其当前作物；其余设备
        /// （无农业 comp 却配 cropOnly）退化为拒收一切（配置即错误，宁紧勿漏）。
        /// </summary>
        private bool IsCurrentCropProduct(ThingDef def)
        {
            CompAutoAgriculture agri = parent.TryGetComp<CompAutoAgriculture>();
            ThingDef product = agri != null ? agri.CurrentCropProduct : null;
            return product != null && def == product;
        }

        /// <summary>检视摘要的复用缓冲区（避免每帧 new StringBuilder）。</summary>
        private StringBuilder summaryBuilder;

        public CompProperties_ItemStorage Props => (CompProperties_ItemStorage)props;

        /// <summary>容量上限（props.capacity &lt;= 0 = 无上限，用 int.MaxValue 表示）。</summary>
        public int Capacity => Props.capacity > 0 ? Props.capacity : int.MaxValue;

        public int StoredTotal
        {
            get
            {
                int total = 0;
                for (int i = 0; i < bufferCounts.Count; i++)
                {
                    total += bufferCounts[i];
                }
                return total;
            }
        }

        public IEnumerable<ThingDefCountClass> Buffer
        {
            get
            {
                for (int i = 0; i < bufferDefs.Count; i++)
                {
                    yield return new ThingDefCountClass(bufferDefs[i], bufferCounts[i]);
                }
            }
        }

        public int AcceptQuantity(ThingDef def, int count)
        {
            if (def == null || count <= 0)
            {
                return 0;
            }
            if (Props.capacity <= 0)
            {
                return count; // 无上限。
            }
            return Mathf.Min(count, Capacity - StoredTotal);
        }

        public int Push(ThingDef def, int count)
        {
            if (Props.cropOnly && !IsCurrentCropProduct(def))
            {
                // ⭐ 白名单=「设备当前选定作物」的产物（PM 2026-09-07 实机反馈：
                // 旧全局白名单把覆盖区里的原木/钢铁全吸了进来）。
                // 节流告警，帮助定位是哪条路径塞进来的。
                int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
                if (now - lastCropRejectTick > 1800)
                {
                    lastCropRejectTick = now;
                    ChronicleLog.Warning(ChronicleLog.Category.Automation,
                        "ItemStorage cropOnly rejected " + (def != null ? def.defName : "null")
                        + " on " + parent.LabelShortCap);
                }
                return 0;
            }
            int accepted = AcceptQuantity(def, count);
            if (accepted <= 0)
            {
                return 0;
            }
            int idx = bufferDefs.IndexOf(def);
            if (idx >= 0)
            {
                bufferCounts[idx] += accepted;
            }
            else
            {
                bufferDefs.Add(def);
                bufferCounts.Add(accepted);
            }
            return accepted;
        }

        public Thing TakeOut(ThingDef def, int maxCount)
        {
            if (def == null || maxCount <= 0)
            {
                return null;
            }
            int idx = bufferDefs.IndexOf(def);
            if (idx < 0 || bufferCounts[idx] <= 0)
            {
                return null;
            }
            int take = Mathf.Min(maxCount, Mathf.Min(bufferCounts[idx], def.stackLimit));
            Thing stack = ThingMaker.MakeThing(def);
            stack.stackCount = take;
            bufferCounts[idx] -= take;
            if (bufferCounts[idx] <= 0)
            {
                bufferDefs.RemoveAt(idx);
                bufferCounts.RemoveAt(idx);
            }
            return stack;
        }

        /// <summary>
        /// 把缓冲全部落地（玩家手动取出 / 撤机兜底）。
        /// <paramref name="map"/> 显式传入：<see cref="PostDestroy"/> 时
        /// <c>parent.Map</c> 已为 null，必须用 previousMap（与 PostDeSpawn 同一个坑）。
        /// ⚠️ 按 <c>def.stackLimit</c> 切分：缓冲是 def+count，可远超原版堆叠上限
        /// （250 水稻 vs 上限 75），整批物化会造出超限单栈，破坏原版合堆/搬运/货架假设。
        /// </summary>
        public void DropAll()
        {
            DropAll(null);
        }

        public void DropAll(Map map)
        {
            if (map == null)
            {
                map = parent.Map;
            }
            if (map == null || bufferDefs.Count == 0)
            {
                return;
            }
            for (int i = bufferDefs.Count - 1; i >= 0; i--)
            {
                ThingDef def = bufferDefs[i];
                int left = bufferCounts[i];
                if (def == null || left <= 0)
                {
                    continue;
                }
                int limit = Mathf.Max(1, def.stackLimit);
                while (left > 0)
                {
                    int n = Mathf.Min(left, limit);
                    Thing stack = ThingMaker.MakeThing(def);
                    stack.stackCount = n;
                    if (!GenPlace.TryPlaceThing(stack, parent.Position, map, ThingPlaceMode.Near))
                    {
                        stack.Destroy();
                        break; // 放不下：已落地的部分保住，不再硬塞（零丢失优先于全量）。
                    }
                    left -= n;
                }
            }
            bufferDefs.Clear();
            bufferCounts.Clear();
        }

        /// <summary>
        /// 拆除/焚毁兜底：缓冲只存 def+count，建筑一销毁数据就没了——
        /// 若不物化，仓内几十到数百件作物会凭空蒸发（违反反压零丢失铁律）。
        /// 卸载（minified）走 DeSpawn、Thing 对象保留，故不在这里处理。
        /// </summary>
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            DropAll(previousMap);
            base.PostDestroy(mode, previousMap);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            // ⭐ ThingDef 是 Def → LookMode.Def（存 defName）。原 LookMode.Reference
            // 要求元素实现 ILoadReferenceable → 仓内有货时保存必炸（InvalidOperationException），
            // 炸点后的 comp 数据整段丢失——与农机地形表同一颗雷（2026-09-08 定案）。
            Scribe_Collections.Look(ref bufferDefs, "itemBufferDefs", LookMode.Def);
            Scribe_Collections.Look(ref bufferCounts, "itemBufferCounts", LookMode.Value);
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }
            if (bufferDefs == null)
            {
                bufferDefs = new List<ThingDef>();
            }
            if (bufferCounts == null)
            {
                bufferCounts = new List<int>();
            }
            // 两表必须等长；读档异常时截断到较短一侧，防索引错位。
            int n = Math.Min(bufferDefs.Count, bufferCounts.Count);
            if (n != bufferDefs.Count)
            {
                bufferDefs.RemoveRange(n, bufferDefs.Count - n);
            }
            if (n != bufferCounts.Count)
            {
                bufferCounts.RemoveRange(n, bufferCounts.Count - n);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (StoredTotal == 0)
            {
                return Tr("PersonalChronicle.Automation.Farm.Storage.Empty");
            }
            TaggedString detail = Tr("PersonalChronicle.Automation.Farm.Storage.Inspect", Summary());
            if (Props.capacity > 0 && StoredTotal >= Capacity)
            {
                detail += Tr("PersonalChronicle.Automation.Farm.Storage.Full");
            }
            return detail;
        }

        /// <summary>「水稻 ×120 / 玉米 ×5」式摘要。</summary>
        private string Summary()
        {
            // 性能体检 P0-4：检视面板每帧调用，复用实例级 StringBuilder 不再每次 new。
            StringBuilder sb = summaryBuilder ?? (summaryBuilder = new StringBuilder());
            sb.Length = 0;
            for (int i = 0; i < bufferDefs.Count; i++)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" / ");
                }
                // 空 def 防御：移除/改名作物 mod 后读档，Def 引用解析失败为 null，
                // 直接 .label 会让检视面板每帧 NRE 刷屏。
                ThingDef def = bufferDefs[i];
                sb.Append(def != null ? def.label : "?").Append(" ×").Append(bufferCounts[i]);
            }
            return sb.ToString();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (unloadGizmo == null)
            {
                unloadGizmo = new Command_Action
                {
                    defaultDesc = Tr("PersonalChronicle.Automation.Farm.Storage.UnloadDesc"),
                    icon = ContentFinder<Texture2D>.Get("UI/Buttons/Drop", false) ?? TexButton.OpenStatsReport,
                    action = DropAll
                };
            }
            // 空仓不显示；有货时标签带上存量（实例缓存，每帧只改标签字符串）。
            if (StoredTotal > 0)
            {
                unloadGizmo.defaultLabel = Tr("PersonalChronicle.Automation.Farm.Storage.Unload", StoredTotal.ToString());
                yield return unloadGizmo;
            }
        }

        /// <summary>取翻译并按 {0}/{1} 占位符回填（避开 1.6 已过时的 Translate(params object[])）。</summary>
        private static TaggedString Tr(string key, params object[] args)
        {
            string raw = key.Translate().ToString();
            return args.Length == 0 ? (TaggedString)raw : string.Format(raw, args);
        }
    }
}
