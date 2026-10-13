using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 自动化农业参数（Def 级，S/M/L 各自配置）。
    /// </summary>
    public class CompProperties_AutoAgriculture : CompProperties
    {
        /// <summary>覆盖半径（格）：9×9→4 / 15×15→7 / 21×21→10。</summary>
        public int coverageRadius = 4;

        /// <summary>是否播放灌溉水花（D5：纯视觉，M/L 档开）。</summary>
        public bool irrigationVisual = false;

        /// <summary>
        /// 是否开启地形改造（D1：地形替换，S/M/L 全档）。覆盖 cell 换成玩家选定的
        /// 目标地形（默认沃土）并记录原值，撤机还原；目标可随时更换（无限替换）。
        /// </summary>
        public bool fertilize = false;

        /// <summary>是否自动收集覆盖范围内的农作物掉落物（PM 2026-09-06）。默认开。</summary>
        public bool collectDrops = true;

        /// <summary>收集时是否播放「向设备收敛」的粒子动画（纯视觉，不影响逻辑）。默认开。</summary>
        public bool collectVisual = true;

        public CompProperties_AutoAgriculture()
        {
            compClass = typeof(CompAutoAgriculture);
        }

        /// <summary>
        /// 详情页（i 图标）规格条目（PM 2026-09-07：把自动农场的详细规格写进
        /// 原版详情界面）。1.6 原生入口 <c>CompProperties.SpecialDisplayStats(StatRequest)</c>
        /// （反射核实虚签名）——零 Harmony。只输出 props 自有规格；
        /// 跨 comp 的信息（生长灯/燃料）已在 description 文案中。
        /// </summary>
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req))
            {
                yield return entry;
            }
            int size = coverageRadius * 2 + 1;
            yield return new StatDrawEntry(
                StatCategoryDefOf.Building,
                "PersonalChronicle.Automation.Farm.Spec.Coverage".Translate().RawText,
                string.Format("{0}×{0}", size),
                "PersonalChronicle.Automation.Farm.Spec.CoverageDesc".Translate().RawText,
                4100, null, null, false, false);
            if (fertilize)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Building,
                    "PersonalChronicle.Automation.Farm.Spec.Fertilize".Translate().RawText,
                    "PersonalChronicle.Automation.Farm.Spec.FertilizeValue".Translate().RawText,
                    "PersonalChronicle.Automation.Farm.Spec.FertilizeDesc".Translate().RawText,
                    4090, null, null, false, false);
            }
            if (collectDrops)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Building,
                    "PersonalChronicle.Automation.Farm.Spec.Collect".Translate().RawText,
                    "PersonalChronicle.Automation.Farm.Spec.CollectValue".Translate().RawText,
                    "PersonalChronicle.Automation.Farm.Spec.CollectDesc".Translate().RawText,
                    4080, null, null, false, false);
            }
            yield return new StatDrawEntry(
                StatCategoryDefOf.Building,
                "PersonalChronicle.Automation.Farm.Spec.Storage".Translate().RawText,
                "PersonalChronicle.Automation.Farm.Spec.StorageValue".Translate().RawText,
                "PersonalChronicle.Automation.Farm.Spec.StorageDesc".Translate().RawText,
                4070, null, null, false, false);
            // 电力规格（光伏/本体功耗/应急电池）：数值从 def 实配读取，改 XML 自动同步。
            if (req.Def is ThingDef thingDef)
            {
                CompProperties_PowerTraderWithSolar load =
                    thingDef.GetCompProperties<CompProperties_PowerTraderWithSolar>();
                CompProperties_Battery battery =
                    thingDef.GetCompProperties<CompProperties_Battery>();
                if (load != null)
                {
                    // basePowerConsumption 是 CompProperties_Power 的私有字段，公开入口为
                    // PowerConsumption 属性（负值=发电峰值）。
                    yield return new StatDrawEntry(
                        StatCategoryDefOf.Building,
                        "PersonalChronicle.Automation.Farm.Spec.Power".Translate().RawText,
                        "PersonalChronicle.Automation.Farm.Spec.PowerValue".Translate(
                            (-load.PowerConsumption).ToString("0"),
                            load.extraPowerConsumption.ToString("0")).RawText,
                        "PersonalChronicle.Automation.Farm.Spec.PowerDesc".Translate().RawText,
                        4060, null, null, false, false);
                }
                if (battery != null)
                {
                    yield return new StatDrawEntry(
                        StatCategoryDefOf.Building,
                        "PersonalChronicle.Automation.Farm.Spec.Battery".Translate().RawText,
                        "PersonalChronicle.Automation.Farm.Spec.BatteryValue".Translate(
                            battery.storedEnergyMax.ToString("0")).RawText,
                        "PersonalChronicle.Automation.Farm.Spec.BatteryDesc".Translate().RawText,
                        4050, null, null, false, false);
                }
            }
        }
    }

    /// <summary>
    /// 自动化农业状态机（v2.1 §6-§9）：
    /// 低频轮转扫描覆盖区 →
    ///   空位且在 Growing Zone 内 → 播种该 Zone 的默认作物（D2，spawn 直出）
    ///   成熟作物 → 原版收获逻辑（D4：PlantCollected，产物落原地靠原版搬运）
    ///   随机水花 → 灌溉视觉（D5）。
    ///
    /// 性能纪律：
    /// - 覆盖 cell 只在 Spawn 时缓存一次（世界重建，不持久化——Scribe 纪律）。
    /// - 1.6 自适应口径：<see cref="CompTickInterval"/> 累加 interval 计量唤醒，
    ///   每次唤醒只处理固定时间片（CellsPerWork），工作量与 tick 率无关。
    /// - 运行条件：通电 AND 有燃料 AND 未关闭，不满足时零扫描。
    /// - 计数器供管理面板快照读取（打开面板时取，不参与 tick）。
    /// </summary>
    public partial class CompAutoAgriculture : ThingComp
    {
        private const int WorkIntervalTicks = 120;
        private const int CellsPerWork = 12;

        /// <summary>
        /// 生长驱动节拍（tick）。原版植物走 Long ticker（每 250 tick 一次 TickLong），
        /// 农机以同节拍巡检并兜底推进，保证与原版速率一致。
        /// </summary>
        private const int GrowDriveIntervalTicks = 250;

        /// <summary>诊断日志节流间隔（tick）。开发测试期定位"作物不长"用。</summary>
        private const int DiagLogIntervalTicks = 1800;
        private const string DiagPrefix = "AutoAgri";

        /// <summary>改造推进节拍（tick）：每到一个节拍，改造前沿向外扩一圈。</summary>
        private const int RenovationRingIntervalTicks = 90;

        private readonly List<IntVec3> coverage = new List<IntVec3>();

        /// <summary>
        /// 每格的圈层半径（欧氏距离四舍五入），与 <see cref="coverage"/> 按索引一一对应。
        /// 性能 P1-3：改造推进原本每拍对每格重算一次 <c>Mathf.Sqrt</c>（L 档 441 次/1.5 秒），
        /// 改为 RebuildCoverage 时算一次。同 coverage 一样不持久化（Spawn 重建）。
        /// </summary>
        private readonly List<int> coverageRing = new List<int>();
        private int cursor;
        private int quota;

        private CompPowerTrader powerTrader;
        private CompRefuelable refuelable;
        private CompFlickable flickable;

        /// <summary>内置储物仓（收获产物先进仓，满了才落地）。
        /// 自家 parent 上的 comp 在 Spawn 后固定不变，可安全缓存。</summary>
        private CompItemStorage itemStorage;

        // ---- 施肥（D1：地形替换）----------------------------------------------
        // 改良过的 cell 与其原地形，两表严格按索引一一对应（平行表，避免
        // Dictionary<IntVec3,TerrainDef> 的 Scribe 复杂度与装箱开销）。
        // ⚠️ 不能 readonly：Scribe_Collections.Look(ref ...) 需要可写引用。
        private List<IntVec3> fertilizedCells = new List<IntVec3>();
        private List<TerrainDef> fertilizedOriginal = new List<TerrainDef>();

        /// <summary>各已改造 cell 当前生效的目标地形（平行表第三列）。
        /// 撤机还原以它核对：玩家事后自己铺的其它地砖原样保留。</summary>
        private List<TerrainDef> fertilizedApplied = new List<TerrainDef>();

        /// <summary>施肥目标地形：原版沃土（fertility 1.4）。加载时解析一次。</summary>
        private TerrainDef fertileTerrain;

        /// <summary>玩家改选的改造目标地形（地形互动键选择）。null = 默认沃土。持久化。</summary>
        private TerrainDef targetTerrain;

        /// <summary>
        /// 目标地形 defName 的字符串冗余锚点（持久化）：
        /// LookMode.Def 跨引用解析链路一旦丢失（实测曾整键消失），从纯字符串恢复，
        /// 玩家选择的地形不再回退默认沃土。
        /// </summary>
        private string targetTerrainDefName;

        /// <summary>诊断日志节流：上次输出的 tick。</summary>
        private int lastDiagTick = int.MinValue;

        // ---- 一圈圈往外改造（D1 的渐进式呈现）--------------------------------
        /// <summary>已改造标记，与 <see cref="coverage"/> 按索引一一对应。</summary>
        private readonly List<bool> renovated = new List<bool>();
        /// <summary>当前已改造到的圈层半径（欧氏距离四舍五入）。持久化。</summary>
        private int renovationRing;
        /// <summary>覆盖区最大圈层半径（RebuildCoverage 时算出），不持久化。</summary>
        private int maxRing;
        /// <summary>改造节拍累加器。</summary>
        private int renovationQuota;

        /// <summary>
        /// 生长驱动的成长度快照，与 <see cref="coverage"/> 按索引一一对应。
        /// -1 = 该格当前没有植物。用于自愈检测：若两次采样间成长度在涨，说明
        /// 原版 TickLong 正常工作，农机不干预；若一直不变，说明原版生长失效，
        /// 由农机按 <see cref="Plant.GrowthRate"/> 兜底推进（速率与原版一致）。
        /// </summary>
        private readonly List<float> growthSnapshot = new List<float>();

        private int growQuota;

        private bool DiagReady()
        {
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (now - lastDiagTick < DiagLogIntervalTicks)
            {
                return false;
            }
            lastDiagTick = now;
            return true;
        }

        /// <summary>播种被拒时把"为什么拒绝"打进 Player.log（节流 30 秒一条）。</summary>
        private void LogSowSkip(string reason, IntVec3 cell, Map map)
        {
            if (!DiagReady())
            {
                return;
            }
            TerrainDef terrain = cell.GetTerrain(map);
            ChronicleLog.Info(ChronicleLog.Category.Automation,
                DiagPrefix + " sow skipped @ " + cell + " reason=" + reason
                + " terrain=" + (terrain != null ? terrain.defName : "null")
                + " fertility=" + (terrain != null ? terrain.fertility.ToString("0.00") : "?"));
        }

        /// <summary>对一株已存在作物采样：成长度 / 生命阶段 / 实际光照 vs 门槛 / 肥力。</summary>
        private void LogGrowth(Plant plant, IntVec3 cell, Map map)
        {
            if (!DiagReady())
            {
                return;
            }
            PlantProperties props = plant.def.plant;
            float glow = map.glowGrid.GroundGlowAt(cell);
            TerrainDef terrain = cell.GetTerrain(map);
            // 生长速率 = 光照 × 肥力 × 温度 三个因子相乘，任一为 0 即完全不长。
            // 逐因子打印，一眼看出卡在哪一项。
            float fLight = plant.GrowthRateFactor_Light;
            float fFertility = plant.GrowthRateFactor_Fertility;
            float fTemp = plant.GrowthRateFactor_Temperature;
            float rate = plant.GrowthRate;
            ChronicleLog.Info(ChronicleLog.Category.Automation,
                DiagPrefix + " growth sample: def=" + plant.def.defName
                + " growth=" + plant.Growth.ToString("0.0000")
                + " stage=" + plant.LifeStage
                + " rate=" + rate.ToString("0.00") + "%"
                + " | light=" + fLight.ToString("0.00")
                + " fertility=" + fFertility.ToString("0.00")
                + " temp=" + fTemp.ToString("0.00")
                + " | glow=" + glow.ToString("0.00")
                + " (growMinGlow=" + (props != null ? props.growMinGlow.ToString("0.00") : "?")
                + " growOptimalGlow=" + (props != null ? props.growOptimalGlow.ToString("0.00") : "?") + ")"
                + " terrain=" + (terrain != null ? terrain.defName : "null")
                + " fert=" + (terrain != null ? terrain.fertility.ToString("0.00") : "?")
                + " (fertilityMin=" + (props != null ? props.fertilityMin.ToString("0.00") : "?") + ")");
        }

        public CompProperties_AutoAgriculture Props => (CompProperties_AutoAgriculture)props;

        /// <summary>覆盖 cell 数（管理面板用）。</summary>
        public int CoverageCount => coverage.Count;

        /// <summary>覆盖 cell 列表（选中设备时的范围可视化直接消费，勿修改）。</summary>
        public List<IntVec3> CoverageCells => coverage;

        /// <summary>本档是否开启地形改造（D1）。</summary>
        public bool RenovationEnabled => Props.fertilize;

        /// <summary>当前改造目标地形：玩家选择优先，否则默认沃土。</summary>
        public TerrainDef TargetTerrain => targetTerrain != null ? targetTerrain : fertileTerrain;

        /// <summary>目标地形名（地形互动键标签用）。</summary>
        public string TargetTerrainLabel => TargetTerrain != null ? TargetTerrain.LabelCap : "…";

        /// <summary>
        /// 更换改造目标（地形互动键）。清空圈层进度，让改造从设备中心
        /// 一圈圈重新铺开——已改的格子换上新目标，其余照常推进，
        /// 即「无限次替换」：同一片地可反复换成任意可选地形。
        /// </summary>
        public void SetTargetTerrain(TerrainDef def)
        {
            if (def == null || def == TargetTerrain)
            {
                return;
            }
            // 选回默认沃土时存 null，存档里不留冗余引用。
            targetTerrain = def == fertileTerrain ? null : def;
            targetTerrainDefName = targetTerrain != null ? targetTerrain.defName : null;
            for (int i = 0; i < renovated.Count; i++)
            {
                renovated[i] = false;
            }
            renovationRing = 0;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerTrader = parent.TryGetComp<CompPowerTrader>();
            refuelable = parent.TryGetComp<CompRefuelable>();
            flickable = parent.TryGetComp<CompFlickable>();
            itemStorage = parent.TryGetComp<CompItemStorage>();
            // 施肥目标地形按 defName 解析（原版沃土）。走 DefDatabase 而非
            // TerrainDefOf.SoilRich，避免对原版 DefOf 字段名的强耦合。
            // 不再以 Props.fertilize 为前置：即便该档未开施肥，撤机还原时
            // 仍需 fertileTerrain 比对当前地形，故始终解析。
            if (fertileTerrain == null)
            {
                fertileTerrain = DefDatabase<TerrainDef>.GetNamed("SoilRich", false);
            }
            if (respawningAfterLoad)
            {
                MigrateClaimRegistry();
            }
            RebuildCoverage();
            // ⭐ 载入/重装后从覆盖区种植区恢复「当前作物」（作物本体持久化在
            // 原版 Zone_Growing.PlantDefToGrow，设备侧只是缓存视图，不重复存档）。
            RefreshCropFromZones();
        }

        // ---- 当前作物（PM 2026-09-07：仓储/收集过滤必须跟随设备当前作物） ----------

        /// <summary>设备当前选定作物（原版 Zone_Growing.PlantDefToGrow 的缓存视图）。</summary>
        private ThingDef currentCrop;

        /// <summary>设备当前选定作物（null = 未配置；配置窗与检视共用）。</summary>
        public ThingDef CurrentCrop => currentCrop;

        /// <summary>当前作物的收获产物（作物无产物时为 null = 不收任何掉落物）。</summary>
        public ThingDef CurrentCropProduct => currentCrop != null ? currentCrop.plant.harvestedThingDef : null;

        /// <summary>配置窗选定作物时的显式入口（同时立即生效，不等下一拍刷新）。</summary>
        public void SetCrop(ThingDef crop)
        {
            currentCrop = crop;
            InvalidateInspectCache();
        }

        /// <summary>
        /// 扫描覆盖区内的种植区恢复当前作物（与配置窗 <c>FindCurrentCrop</c> 同口径）。
        /// 节流：仅在收集节拍与显式入口调用——zone 遍历是 O(zones×cells)，不宜每帧。
        /// </summary>
        public void RefreshCropFromZones()
        {
            Map map = parent.Map;
            if (map == null)
            {
                return;
            }
            ThingDef found = null;
            // 2026-09-11 性能体检：coverage 是 List（L 档 441 格），内层 Contains 是 O(n)
            // ⇒ 原实现是 O(#zone × #cell × #coverage)。改为先建一次 HashSet（O(coverage)），
            // 内层查询降到 O(1)。
            HashSet<IntVec3> covered = coverage != null ? new HashSet<IntVec3>(coverage) : null;
            foreach (Zone zone in map.zoneManager.AllZones)
            {
                Zone_Growing gz = zone as Zone_Growing;
                if (gz == null || gz.PlantDefToGrow == null)
                {
                    continue;
                }
                foreach (IntVec3 c in gz.Cells)
                {
                    if (covered != null && covered.Contains(c))
                    {
                        found = gz.PlantDefToGrow;
                        break;
                    }
                }
                if (found != null)
                {
                    break;
                }
            }
            currentCrop = found;
        }

        /// <summary>旧存档迁移：把已有改造记录补登进全图归属表（先到先得），
        /// 防止老档里多机重复记录的格子在新代码下继续互相覆写。</summary>
        private void MigrateClaimRegistry()
        {
            Map map = parent.Map;
            if (map == null || fertilizedCells == null)
            {
                return;
            }
            MapComponent_AutoFarmTerrain registry = MapComponent_AutoFarmTerrain.Get(map);
            for (int i = 0; i < fertilizedCells.Count; i++)
            {
                registry.TryRecord(fertilizedCells[i],
                    i < fertilizedOriginal.Count ? fertilizedOriginal[i] : null, parent);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            // 撤机/被毁/搬迁一律先把地形还原，再做常规清理。
            // map 参数必须用（此时 parent.Map 已为 null）。
            if (map != null)
            {
                RestoreTerrain(map);
            }
            coverage.Clear();
            growthSnapshot.Clear();
            renovated.Clear();
            coverageRing.Clear(); // 平行表第四列，撤机时一并清，防长度瞬时错位。
            renovationRing = 0;
            cursor = 0;
            base.PostDeSpawn(map, mode);
        }

        /// <summary>
        /// 施肥记录持久化。设计文档 §10 存档纪律：
        /// <b>覆盖 cell 缓存不持久化</b>（Spawn 时重建），但<b>已改良 cell 的原地形
        /// 必须持久化</b>——否则读档后撤机无从还原，玩家脚下凭空多出一片沃土。
        /// </summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            // 平行表：IntVec3 是值类型 → LookMode.Value；TerrainDef 是 Def →
            // LookMode.Def（存 defName、读档跨引用解析）。
            // ⭐⭐ 2026-09-08 保存总雷定案：LookMode.Reference 要求元素实现
            // ILoadReferenceable，Def 不实现 → 存档时 InvalidOperationException
            // → 保存线程被 abort → 本 comp 炸点之后的全部数据（含目标地形键、
            // 输出端口绑定）整段丢失 → 读档「地形回默认+端口解绑」两大顽案真凶。
            Scribe_Collections.Look(ref fertilizedCells, "fertilizedCells", LookMode.Value);
            Scribe_Collections.Look(ref fertilizedOriginal, "fertilizedOriginal", LookMode.Def);
            Scribe_Collections.Look(ref fertilizedApplied, "fertilizedApplied", LookMode.Def);
            Scribe_Defs.Look(ref targetTerrain, "targetTerrain");
            // ⭐ 字符串冗余锚点（PM 2026-09-07 实机：读档后目标地形回正沃土，且存档
            // 取证显示 targetTerrain 键整体消失）。LookMode.Def 依赖跨引用解析链路，
            // 纯字符串键是第二保险：解析失败时从 defName 直接恢复。
            // forceSave：default(null) 会被 Scribe_Values 跳过写键，必须强制落盘。
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                targetTerrainDefName = targetTerrain != null ? targetTerrain.defName : null;
            }
            Scribe_Values.Look(ref targetTerrainDefName, "targetTerrainDefName", null, forceSave: true);
            // 改造圈层进度持久化：读档后从 renovationRing 续推，不从头再来。
            Scribe_Values.Look(ref renovationRing, "renovationRing", 0);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // 保存诊断（一次一局一条）：直接看到三表计数与目标——失位当场留痕。
                ChronicleLog.Info(ChronicleLog.Category.Automation,
                    "AutoFarm save " + parent.ThingID + ": target="
                    + (targetTerrain != null ? targetTerrain.defName : "null(default)")
                    + " cells=" + fertilizedCells.Count
                    + " original=" + fertilizedOriginal.Count
                    + " applied=" + fertilizedApplied.Count);
            }
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
            {
                return;
            }
            if (fertilizedCells == null)
            {
                fertilizedCells = new List<IntVec3>();
            }
            if (fertilizedOriginal == null)
            {
                fertilizedOriginal = new List<TerrainDef>();
            }
            if (fertileTerrain == null)
            {
                fertileTerrain = DefDatabase<TerrainDef>.GetNamed("SoilRich", false);
            }
            // ⭐ 目标地形字符串兜底（PM 2026-09-07 实机：targetTerrain 键在存档里
            // 消失、读档回正沃土）。Def 跨引用恢复失败时从 defName 直接恢复；
            // 选沃土语义仍是 null（与 SetTargetTerrain 一致）。
            if (targetTerrain == null && !targetTerrainDefName.NullOrEmpty())
            {
                TerrainDef restored = DefDatabase<TerrainDef>.GetNamed(targetTerrainDefName, false);
                if (restored != null)
                {
                    targetTerrain = restored == fertileTerrain ? null : restored;
                    ChronicleLog.Info(ChronicleLog.Category.Automation,
                        "AutoFarm target terrain restored from defName: " + restored.defName);
                }
            }
            // ⭐ 三表对齐以 cells 为准回填（PM 2026-09-07 存档取证：13 号档
            // cells=425 而 original/applied 全空——旧逻辑 RemoveRange 会把
            // cells 截断归零，改造记录整段蒸发）。
            // - original 缺失=null=「原值未知」：RestoreTerrain 已判 want!=null 跳过该格；
            // - applied 缺失=按当前目标回填，保证重铺/换目标判定正确。
            if (fertilizedApplied == null)
            {
                fertilizedApplied = new List<TerrainDef>();
            }
            while (fertilizedOriginal.Count < fertilizedCells.Count)
            {
                fertilizedOriginal.Add(null);
            }
            if (fertilizedOriginal.Count > fertilizedCells.Count)
            {
                fertilizedOriginal.RemoveRange(fertilizedCells.Count, fertilizedOriginal.Count - fertilizedCells.Count);
            }
            while (fertilizedApplied.Count < fertilizedCells.Count)
            {
                fertilizedApplied.Add(TargetTerrain);
            }
            if (fertilizedApplied.Count > fertilizedCells.Count)
            {
                fertilizedApplied.RemoveRange(fertilizedCells.Count, fertilizedApplied.Count - fertilizedCells.Count);
            }
            ChronicleLog.Info(ChronicleLog.Category.Automation,
                "AutoFarm load " + parent.ThingID + ": target="
                + (targetTerrain != null ? targetTerrain.defName : "null(default)")
                + " cells=" + fertilizedCells.Count
                + " original=" + fertilizedOriginal.Count
                + " applied=" + fertilizedApplied.Count);
        }

        /// <summary>
        /// 待机原因（null = 运行条件满足）。单一真源：<see cref="IsRunning"/> 与
        /// 检视状态行（<see cref="CompInspectStringExtra"/>）共用，避免
        /// 「面板说待机、提示说运行」的口径分裂。
        /// </summary>
        private string IdleReasonKey()
        {
            if (powerTrader != null && !powerTrader.PowerOn)
            {
                return "PersonalChronicle.Automation.Farm.Idle.NoPower";
            }
            if (refuelable != null && !refuelable.HasFuel)
            {
                return "PersonalChronicle.Automation.Farm.Idle.NoFuel";
            }
            if (flickable != null && !flickable.SwitchIsOn)
            {
                return "PersonalChronicle.Automation.Farm.Idle.Off";
            }
            return null;
        }

        /// <summary>运行条件三连：通电 AND 有燃料 AND 未关闭。</summary>
        private bool IsRunning()
        {
            return IdleReasonKey() == null;
        }

        // ---- 检视/悬停信息（PM 2026-09-08）------------------------------------
        // 第三方 UI mod 的悬停提示读的是 Thing.GetInspectString()，它会自动汇总各
        // comp 的 CompInspectStringExtra；农机此前只有电力/储物/端口 comp 出条目，
        // 状态机自己一行不出 → 悬停看不到「在不在跑 / 种的啥 / 多少可收」。
        private const int InspectCacheFrames = 30;
        private readonly StringBuilder inspectBuilder = new StringBuilder();
        private string inspectCache;
        private int inspectCacheFrame = int.MinValue;

        /// <summary>取翻译并按 {0}… 占位符回填（避开 1.6 已过时的 Translate(params object[])）。</summary>
        private static string Tr(string key, params object[] args)
        {
            string raw = key.Translate();
            return args.Length == 0 ? raw : string.Format(raw, args);
        }

        /// <summary>状态被外部改写时调用，令检视缓存下一帧重建（不等节流窗口）。</summary>
        private void InvalidateInspectCache()
        {
            inspectCache = null;
        }

        /// <summary>
        /// 检视/悬停条目：状态行（含待机原因与无电引导）+ 当前作物 + 覆盖/播种/可收。
        /// 性能：每帧调用（检视面板与悬停都在渲染循环里），故按帧缓存 30 帧重建一次——
        /// <see cref="GetStats"/> 是 O(coverage)，不能每帧跑。
        /// </summary>
        public override string CompInspectStringExtra()
        {
            if (inspectCache != null && Time.frameCount - inspectCacheFrame < InspectCacheFrames)
            {
                return inspectCache;
            }
            inspectCacheFrame = Time.frameCount;
            StringBuilder sb = inspectBuilder;
            sb.Length = 0;

            string idleKey = IdleReasonKey();
            sb.Append(Tr("PersonalChronicle.Automation.Farm.Inspect.State",
                idleKey == null
                    ? Tr("PersonalChronicle.Automation.Farm.Inspect.Running")
                    : Tr(idleKey)));

            // ⭐ 无电分两类：PowerNet == null（真没接进电网→提示铺电缆）
            // vs 电网供不应求（→提示加发电）。旧版都报成「未接电」，误导排查。
            if (idleKey == "PersonalChronicle.Automation.Farm.Idle.NoPower" && powerTrader != null)
            {
                sb.Append("\n").Append(Tr(powerTrader.PowerNet == null
                    ? "PersonalChronicle.Automation.Farm.NoGrid"
                    : "PersonalChronicle.Automation.Farm.PowerShortage"));
            }

            ThingDef crop = CurrentCrop;
            sb.Append("\n").Append(Tr("PersonalChronicle.Automation.Farm.Inspect.Crop",
                crop != null ? crop.LabelCap : Tr("PersonalChronicle.Automation.Farm.Inspect.CropNone")));

            if (crop == null)
            {
                sb.Append("\n").Append(Tr("PersonalChronicle.Automation.Farm.Inspect.Coverage",
                    coverage.Count));
            }
            else
            {
                GetStats(out int sown, out int mature);
                sb.Append("\n").Append(Tr("PersonalChronicle.Automation.Farm.Inspect.Work",
                    coverage.Count, sown, mature));
            }

            inspectCache = sb.ToString();
            return inspectCache;
        }

        /// <summary>
        /// 覆盖范围 = 以设备包围盒中心为原点的<b>正方形</b>（切比雪夫距离 ≤
        /// <see cref="CompProperties_AutoAgriculture.coverageRadius"/>），不再用
        /// <see cref="GenRadial.RadialCellsAround"/>（那画出来是菱形圆）。
        /// 设备自身占用的格子不纳入（ impassable 无可处理内容，且避免在机器脚下施肥）。
        /// </summary>
        private void RebuildCoverage()
        {
            coverage.Clear();
            growthSnapshot.Clear();
            renovated.Clear();
            coverageRing.Clear();
            maxRing = 0;
            Map map = parent.Map;
            if (map == null)
            {
                return;
            }
            // 性能 P1-5：已改造标记原本用 List.Contains 在双重循环内判定（O(n²)，
            // L 档 441² 量级）。覆盖区重建是低频事件，建一次性 HashSet 即可。
            HashSet<IntVec3> fert = fertilizedCells.Count > 0
                ? new HashSet<IntVec3>(fertilizedCells) : null;
            CellRect own = parent.OccupiedRect();
            IntVec3 center = own.CenterCell;
            int r = Props.coverageRadius;
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    IntVec3 cell = new IntVec3(center.x + dx, 0, center.z + dz);
                    if (cell.InBounds(map) && !own.Contains(cell))
                    {
                        coverage.Add(cell);
                        growthSnapshot.Add(-1f);
                        // 圈层半径 = 以设备中心为原点的欧氏距离（四舍五入）。
                        // 既是改造前沿的判定依据，也用于存档后从持久化的
                        // renovationRing 续推（不持久化 maxRing 本身）。
                        int d = (int)Mathf.RoundToInt(Mathf.Sqrt(dx * dx + dz * dz));
                        coverageRing.Add(d);
                        if (d > maxRing)
                        {
                            maxRing = d;
                        }
                        // 已改造标记 = 该格当前地形是否已是目标地形。
                        // ⚠️ 不能改用 fertilizedCells.Contains：那等于"登记过即算完成"，
                        //    玩家换地形目标后（SetTargetTerrain 的内存级清零）一存读档，
                        //    已登记格会被重新标成已完成 → 中心区永远不换、玩家怎么重选都没用；
                        //    玩家手工铲掉/烧掉已改地形后也不会补。以"当前地形==目标"为准
                        //    三种情况一并覆盖（换新目标 / 被铲掉 / 玩家自己铺了同款）。
                        renovated.Add(cell.GetTerrain(map) == TargetTerrain);
                    }
                }
            }
            if (renovationRing > maxRing)
            {
                renovationRing = maxRing;
            }
        }

        public override void CompTickInterval(int interval)
        {
            base.CompTickInterval(interval);
            if (!parent.Spawned || parent.Map == null)
            {
                return;
            }
            if (!IsRunning())
            {
                return;
            }
            // 播种/收割时间片：每 WorkIntervalTicks 处理一批，避免每 tick 全量扫描。
            quota += interval;
            if (quota >= WorkIntervalTicks)
            {
                quota = 0;
                if (coverage.Count == 0)
                {
                    RebuildCoverage();
                }
                if (coverage.Count > 0)
                {
                    DoWorkSlice();
                }
            }
            // 生长驱动：与原版植物 Long ticker 同节拍（250 tick）。
            growQuota += interval;
            if (growQuota >= GrowDriveIntervalTicks)
            {
                growQuota = 0;
                if (coverage.Count > 0)
                {
                    DriveGrowth();
                }
            }
            // 一圈圈往外改造：每 RenovationRingIntervalTicks 推进一圈前沿。
            renovationQuota += interval;
            if (renovationQuota >= RenovationRingIntervalTicks)
            {
                renovationQuota = 0;
                DriveRenovation();
            }
            // 掉落物自动收集 + 收敛动画（分片扫描 + 节流，见 .Collect.cs）。
            TickCollect(interval);
        }

        /// <summary>时间片：每次唤醒只处理固定数量 cell（轮转游标），全周期覆盖一次。</summary>
        private void DoWorkSlice()
        {
            long perf = PerfProbe.Start();
            Map map = parent.Map;
            for (int n = 0; n < CellsPerWork; n++)
            {
                if (coverage.Count == 0)
                {
                    return;
                }
                cursor %= coverage.Count;
                IntVec3 cell = coverage[cursor];
                cursor = (cursor + 1) % coverage.Count;
                ProcessCell(cell, map);
            }
            PerfProbe.End("agri.work", perf);
        }

        /// <summary>
        /// 生长驱动（自愈式 + 昼夜生长）。
        ///
        /// 背景：实机发现农机播下的作物长期停在 Growth=0（Sowing 阶段 → 无模型、
        /// 检视无生长数据），且已排除光照/肥力/机型/接电因素。与其继续追查原版
        /// <c>Plant.TickLong</c> 为何不生效，不如由农机直接推进。
        ///
        /// <b>夜间也生长</b>：原版植物有休眠期（<c>Plant.Resting</c>，夜间
        /// <see cref="Plant.GrowthRate"/> 为 0——这正是原版太阳灯自带"夜间关闭"
        /// 日程的原因）。农机驱动的速率只取 <b>光照 × 肥力 × 温度</b> 三因子，
        /// 不含休眠因子：生长灯（overlight）照到的地方，夜间同样生长。
        ///
        /// 自愈判定：两次采样间成长度若已上涨 → 原版生长正常，农机让位不重复加速；
        /// 若纹丝不动 → 原版失效（或处于休眠），农机按三因子接管。
        /// </summary>
        private void DriveGrowth()
        {
            long perf = PerfProbe.Start();
            Map map = parent.Map;
            if (map == null)
            {
                PerfProbe.End("agri.growth", perf);
                return;
            }
            float scale = GrowDriveIntervalTicks / 60000f;
            for (int i = 0; i < coverage.Count && i < growthSnapshot.Count; i++)
            {
                Plant plant = coverage[i].GetPlant(map);
                if (plant == null || plant.Destroyed || !plant.Spawned)
                {
                    growthSnapshot[i] = -1f;
                    continue;
                }
                float now = plant.Growth;
                if (now >= 1f)
                {
                    growthSnapshot[i] = now;
                    continue;
                }
                if (now > growthSnapshot[i] + 0.0001f)
                {
                    // 原版在正常生长（白天）→ 不干预，避免双倍加速。
                    growthSnapshot[i] = now;
                    continue;
                }
                PlantProperties props = plant.def.plant;
                if (props == null || plant.Blighted)
                {
                    growthSnapshot[i] = now;
                    continue;
                }
                // 农机驱动速率 = 光照 × 肥力 × 温度（不含夜间休眠因子）。
                float rate = plant.GrowthRateFactor_Light
                           * plant.GrowthRateFactor_Fertility
                           * plant.GrowthRateFactor_Temperature;
                if (rate <= 0f)
                {
                    growthSnapshot[i] = now;
                    continue;
                }
                float next = Mathf.Min(1f, now + rate * scale);
                plant.Growth = next;
                growthSnapshot[i] = next;
            }
            PerfProbe.End("agri.growth", perf);
        }

        private void ProcessCell(IntVec3 cell, Map map)
        {
            // 灌溉视觉（D5：纯视觉，不进生长公式）——**必须在本方法最开头**：
            // 原先放在各分支早退之后，而"有作物"与"在种植区"两分支都会 return，
            // 结果只有"既无植物也不在种植区"的格子才可能触发，M/L 档开了也看不到水花。
            if (Props.irrigationVisual && Rand.Chance(0.05f))
            {
                FleckMaker.Static(cell, map, FleckDefOf.WaterSplash);
            }
            Zone_Growing growing = cell.GetZone(map) as Zone_Growing;
            Plant plant = cell.GetPlant(map);

            if (plant != null)
            {
                // 只收「玩家给这片种植区指定的作物」。覆盖区里的野生植物/mod 植物
                // 一律不动（PM 实测：覆盖区内有 mod 金属作物，农机见成熟就割，
                // 钢铁混进了粮食仓——收割必须以 Zone_Growing.PlantDefToGrow 为准）。
                bool targetCrop = growing != null && growing.PlantDefToGrow != null
                    && plant.def == growing.PlantDefToGrow;
                if (targetCrop && plant.HarvestableNow && plant.Spawned && !plant.Destroyed)
                {
                    PlayEffect(cell, map, EffecterDefOf.Harvest_Plant);
                    HarvestPlant(plant, cell, map);
                }
                else
                {
                    LogGrowth(plant, cell, map);
                }
                return;
            }

            if (growing != null && growing.PlantDefToGrow != null)
            {
                if (growing.allowSow)
                {
                    TrySow(cell, map, growing);
                }
                else
                {
                    LogSowSkip("zone has sowing disabled", cell, map);
                }
                return;
            }

        }

        /// <summary>
        /// D1 地形改造：把目标 cell 换成当前目标地形（玩家可选），记录原地形供撤机还原。
        /// 返回 true 表示本 cell 本次已处理（含「已是目标地形，无事可做」）。
        ///
        /// 两条铁律：
        /// ① <b>原值只记一次</b>——首次改造记录原始地形，之后无论换多少次目标都不覆盖
        ///    （撤机还原到玩家划区前的样子）；
        /// ② <b>还原按生效目标核对</b>——只还原「当前仍是本机所铺地形」的格子，
        ///    玩家事后自己铺的其它地砖原样保留。
        ///
        /// 无限替换（2026-09-06）：目标地形可随时更换，已改的格子照常重铺；
        /// 覆盖区内地形由农机接管（含玩家地板）。不可改变的地形
        /// （<c>TerrainDef.changeable == false</c>，如水体/基座）始终跳过。
        /// </summary>
        private bool TryFertilize(IntVec3 cell, Map map)
        {
            TerrainDef target = TargetTerrain;
            if (target == null)
            {
                return false;
            }
            TerrainDef current = map.terrainGrid.TerrainAt(cell);
            if (current == null)
            {
                return false;
            }
            if (current == target)
            {
                return true;
            }
            if (!current.changeable)
            {
                return false;
            }

            // 让位判断必须在 idx 判定**之前**：本机登记过（idx>=0）不代表此刻仍是认领方
            // ——旧档迁移时两机可能同时持有同一格，若只在 idx<0 分支让位，后手机就会
            // 无条件覆写先手机铺的地形、并污染双方的还原链。
            MapComponent_AutoFarmTerrain registry = MapComponent_AutoFarmTerrain.Get(map);
            if (registry.IsClaimedByOther(cell, parent))
            {
                return false;
            }

            int idx = fertilizedCells.IndexOf(cell);
            if (idx < 0)
            {
                registry.TryRecord(cell, current, parent);
                fertilizedCells.Add(cell);
                fertilizedOriginal.Add(current);
                fertilizedApplied.Add(target);
            }
            else
            {
                fertilizedApplied[idx] = target;
            }
            map.terrainGrid.SetTerrain(cell, target);
            return true;
        }

        /// <summary>
        /// 撤机还原：只还原「当前仍是本机所铺地形」的格子，玩家改动原样保留。
        /// 原始地形以全图归属登记表为准（避免多机交叠时读到别机铺的地形当原值），
        /// 还原后释放认领，其它农机之后可正常接管。
        /// </summary>
        private void RestoreTerrain(Map map)
        {
            MapComponent_AutoFarmTerrain registry = MapComponent_AutoFarmTerrain.Get(map);
            for (int i = fertilizedCells.Count - 1; i >= 0; i--)
            {
                IntVec3 cell = fertilizedCells[i];
                TerrainDef applied = i < fertilizedApplied.Count ? fertilizedApplied[i] : fertileTerrain;
                TerrainDef want = registry.OriginalAt(cell) ?? fertilizedOriginal[i];
                if (want != null && applied != null && map.terrainGrid.TerrainAt(cell) == applied)
                {
                    map.terrainGrid.SetTerrain(cell, want);
                }
                registry.Release(cell, parent); // 只释放本机的认领，别动他机的。
            }
            fertilizedCells.Clear();
            fertilizedOriginal.Clear();
            fertilizedApplied.Clear();
        }

        /// <summary>
        /// 一圈圈往外改造：以设备中心为原点，按欧氏距离分层（ring）。每次被 tick 驱动
        /// 调用时，先把<b>已抵达前沿（d ≤ renovationRing）且属于种植区</b>的格子改造为
        /// 沃土（复用 <see cref="TryFertilize"/> 的全部安全铁律），再把前沿向外扩一圈。
        ///
        /// 设计要点：
        /// - <b>只改玩家选中的种植区地砖</b>（cell 必须在 <see cref="Zone_Growing"/> 内），
        ///   未划区的覆盖格不动——契合"选哪些地砖耕犁"。
        /// - 前沿推进与"实际改造"解耦：即便某圈层此刻没有种植区，前沿仍照常外扩；
        ///   玩家后补的种植区只要落在已抵达的圈层内，下一次驱动即被改造（不会卡死）。
        /// - 全部格改完后（renovationRing ≥ maxRing）前沿停止推进，但扫描仍跑，
        ///   以承接玩家后续新增的种植区格子。
        /// - 改造目标可由玩家经「改造地形」互动键任选（默认沃土 SoilRich），
        ///   候选覆盖原版地板与第三方地形 mod（按 designationCategory 判定，零白名单）。
        ///   更换目标时圈层进度清零、从中心重新铺开——即无限次替换。
        /// </summary>
        private void DriveRenovation()
        {
            long perf = PerfProbe.Start();
            if (!Props.fertilize || TargetTerrain == null)
            {
                PerfProbe.End("agri.renovation", perf);
                return;
            }
            Map map = parent.Map;
            if (map == null || coverage.Count == 0)
            {
                PerfProbe.End("agri.renovation", perf);
                return;
            }
            for (int i = 0; i < coverage.Count; i++)
            {
                if (renovated[i])
                {
                    continue;
                }
                // 圈层半径已在 RebuildCoverage 算好（P1-3），不再每格重算 sqrt。
                int d = i < coverageRing.Count ? coverageRing[i] : 0;
                if (d > renovationRing)
                {
                    continue;
                }
                IntVec3 c = coverage[i];
                Zone_Growing growing = c.GetZone(map) as Zone_Growing;
                if (growing == null)
                {
                    continue;
                }
                if (TryFertilize(c, map))
                {
                    renovated[i] = true;
                }
            }
            if (renovationRing < maxRing)
            {
                renovationRing++;
            }
            PerfProbe.End("agri.renovation", perf);
        }

        /// <summary>
        /// D4 收获：<b>先落产出，再走原版收尾</b>。
        ///
        /// 根因（实机确诊 + IL 反编译）：1.6 的 <see cref="Plant.PlantCollected"/> 只做
        /// 记录/销毁植株/残桩处理，<b>不生成产物</b>——产物由原版 JobDriver_PlantWork 的
        /// 收割 Toil 负责（MakeThing + GenPlace.Near）。农机此前只调 PlantCollected，
        /// 植株凭空消失、零产出，随即被重播成幼苗（「成熟后消失回到幼苗」即此）。
        /// 产物走 GenPlace 直出，不经 GenRecipe，不触发职业捕获（PM 契约：机器收成
        /// 无人力，语义诚实）。产物落地失败按产线铁律 Destroy 兜底。
        /// </summary>
        private void HarvestPlant(Plant plant, IntVec3 cell, Map map)
        {
            ThingDef productDef = plant.def.plant.harvestedThingDef;
            int yield = plant.YieldNow();
            if (productDef != null && yield > 0)
            {
                // 产出先进内置储物仓（IItemStorage：未来管道运输的预留接口），满了才落地。
                int toGround = yield;
                if (itemStorage != null)
                {
                    toGround = yield - itemStorage.Push(productDef, yield);
                }
                if (toGround > 0)
                {
                    // 按 stackLimit 切分：单株产量可能超过原版堆叠上限（mod 高产作物），
                    // 整批物化会造出超限单栈。放不下时保住已落地的部分。
                    int left = toGround;
                    int limit = Mathf.Max(1, productDef.stackLimit);
                    while (left > 0)
                    {
                        int n = Mathf.Min(left, limit);
                        Thing product = ThingMaker.MakeThing(productDef);
                        product.stackCount = n;
                        if (!GenPlace.TryPlaceThing(product, cell, map, ThingPlaceMode.Near))
                        {
                            product.Destroy();
                            break;
                        }
                        left -= n;
                    }
                }
            }
            plant.PlantCollected(null, PlantDestructionMode.Cut);
        }

        private void TrySow(IntVec3 cell, Map map, Zone_Growing growing)
        {
            if (cell.Fogged(map))
            {
                LogSowSkip("cell is fogged", cell, map);
                return;
            }
            if (cell.GetEdifice(map) != null)
            {
                LogSowSkip("cell occupied by an edifice", cell, map);
                return;
            }
            if (cell.GetTerrain(map).fertility <= 0f)
            {
                LogSowSkip("terrain fertility is zero (rock?)", cell, map);
                return;
            }
            ThingDef crop = growing.PlantDefToGrow;
            if (crop.plant == null)
            {
                LogSowSkip("crop def has no plant properties", cell, map);
                return;
            }
            if (!PlantUtility.CanEverPlantAt(crop, cell, map, true, true))
            {
                LogSowSkip("CanEverPlantAt rejected: " + crop.defName, cell, map);
                return;
            }
            // 与原版 JobDriver_PlantSow 逐字段对齐：
            // MakeThing → Growth=0 → sown=true → Spawn(Rot4.North)。
            // ⚠️ 缺 sown=true 时植株不算"人工种植"，生长与渲染行为与原版不一致。
            Plant newPlant = (Plant)ThingMaker.MakeThing(crop);
            newPlant.Growth = 0f;
            newPlant.sown = true;
            GenSpawn.Spawn(newPlant, cell, map, Rot4.North);
            PlayEffect(cell, map, EffecterDefOf.Sow);
            // 诊断：确认生成对象确实是 Plant、def 带植物属性（定位"无模型无数据"）。
            // ⚠️ 节流（性能体检 P0-3）：一台 L 档每波可播多格，原本每次播种都打一条，
            // Player.log 被 sowed 行淹没且白白付出拼接+IO 成本；先判断再拼接。
            if (ChronicleLog.Throttled("AutoAgri.Sowed", 600))
            {
                ChronicleLog.Info(ChronicleLog.Category.Automation,
                    DiagPrefix + " sowed " + crop.defName
                    + " runtimeType=" + newPlant.GetType().Name
                    + " hasPlantProps=" + (newPlant.def.plant != null)
                    + " growth=" + newPlant.Growth.ToString("0.000")
                    + " lifeStage=" + newPlant.LifeStage);
            }
        }

        /// <summary>
        /// 原版一次性表现（零自研贴图/音效）：
        /// 播种 = <see cref="EffecterDefOf.Sow"/>（Mote_Sow 符号 + 尘土），
        /// 收割 = <see cref="EffecterDefOf.Harvest_Plant"/>（Mote_Harvest + 土屑）。
        /// 与原版小人播种/收割同一套 Effecter，观感一致。
        /// </summary>
        private static void PlayEffect(IntVec3 cell, Map map, EffecterDef def)
        {
            if (def == null || !cell.InBounds(map))
            {
                return;
            }
            Effecter effecter = def.Spawn(cell, map, Vector3.zero, 1f);
            effecter.EffectTick(new TargetInfo(cell, map), new TargetInfo(cell, map));
            effecter.Cleanup();
        }

        /// <summary>全量统计（管理面板打开时一次性调用，不参与 tick）。</summary>
        public void GetStats(out int sown, out int mature)
        {
            sown = 0;
            mature = 0;
            Map map = parent.Map;
            if (map == null)
            {
                return;
            }
            for (int i = 0; i < coverage.Count; i++)
            {
                Plant plant = coverage[i].GetPlant(map);
                if (plant == null)
                {
                    continue;
                }
                sown++;
                if (plant.HarvestableNow)
                {
                    mature++;
                }
            }
        }
    }
}
