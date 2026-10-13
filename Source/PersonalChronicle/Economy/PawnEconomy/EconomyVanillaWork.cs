using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// Phase 1（CHG-20261010-VANILLA-JOBS）：**原版工作合同**（劳务站侧）——
    /// 识别玩家下达的工作动作 ⇒ 发布为劳务站机会 ⇒ 签约 ⇒ 完成计件 ⇒ 国库付个人账户。
    ///
    /// <para><b>识别方式</b>：玩家的动作在 <c>DesignationManager</c> 里已是权威状态
    /// （<c>designationsByDef</c> 是 <c>DefMap</c>，对**任意 Def（含第三方）**自动定容），读它 O(1) 且零轮询；
    /// "完成"信号由**既有**捕获点上报（<c>Patch_ItemFlowPlant</c> / <c>Patch_ItemFlowMining</c>
    /// → <c>ReportVanillaWorkCompletion</c>）。
    /// **来源**（玩家下达 vs 建筑/植物自维护）自 CHG-20261010-ECON-JOB-INTENT 起由
    /// <c>Patch_DesignationSource</c> 在 <c>AddDesignation</c> 处的一个只读挂点捕获 ——
    /// 本文件只消费该判定的结果（<see cref="playerDesignations"/>），自身不挂 Harmony。</para>
    ///
    /// <para><b>合同形态</b>：<c>PositionKey = "vanilla:{workTypeDefName}:{anchorKey}"</c>
    /// （一次定型；域层不解析格式，解析只在本层与 <c>EconomyEmployment</c> 的分派处）。
    /// 一纸合同覆盖该工种的当前待办（上限 <see cref="VanillaMaxJobsPerContract"/>，且不超过国库可用），
    /// 每完成一件由证据链核销一件。</para>
    ///
    /// <para><b>不新建资金链</b>：签约走 <c>Employment.Sign</c>，付款走既有
    /// <c>AcceptEvidence → WageObligation → TrySettlePayable</c>（幂等 / 欠薪补付 / 读档保留全部免费获得）。</para>
    /// </summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        /// <summary>Phase 1 识别的工作类型（原版 <c>WorkTypeDef.defName</c> 稳定 ID）—— **唯一来源**：
        /// 机会源数组、计数分派与 UI 组装都引用这两个常量，不再散落字面量（ARCH R1）。
        /// 两者都有既有的对象级完成捕获点；更广的工种在 Phase 2 逐项接入（建造/拆除/收获/狩猎）。</summary>
        internal const string VanillaWorkTypePlantCutting = "PlantCutting";
        internal const string VanillaWorkTypeMining = "Mining";
        internal static readonly string[] VanillaWorkTypes = { VanillaWorkTypePlantCutting, VanillaWorkTypeMining };
        /// <summary>待办计数上限（有界早退；超过只当作"很多"）。</summary>
        internal const int VanillaWorkBacklogCap = 64;
        /// <summary>一纸合同的件数**固定上限**（与国库可用额取较小者；防止一份合同吃掉全部现金流）。
        /// CHG-20261012-ECON-CONTRACTIZE：件数改由**「工作合同弹窗」逐份设定**（用户裁决
        /// "点击工作需求的项目弹出工作合同弹窗，设置工作数量"）⇒ 原先的"每份 ±"档位步进
        /// （预设序列 + 运行期档位）已整体移除，本常量是件数上限的唯一来源。</summary>
        internal const int VanillaMaxJobsPerContract = 32;
        private const int VanillaBacklogCacheTicks = 60;
        private readonly Dictionary<string, int> vanillaBacklogCache = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> vanillaBacklogTick = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>玩家**手动下达**的 designation 键集合（CHG-20261010-ECON-JOB-INTENT）。
        /// 键 = <c>defName|目标键</c>（Thing 用 <c>GetUniqueLoadID</c>；Cell 用坐标）。
        /// 写入方**只有** <see cref="RecordPlayerDesignation"/>（由 Capture 层判定来源后调用）；
        /// 读取方按"键在集合 ∧ designation 仍存在于地图"双条件统计 ⇒ 取消天然生效。</summary>
        private readonly HashSet<string> playerDesignations = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>**自动来源**的 designation 键集合（建筑 / 植物自维护）。审计 P1 修复：计酬侧
        /// 用它做排除判定（见 <see cref="IsAutoIssuedTarget"/>）—— 与玩家白名单**互斥维护**
        /// （同一条下达只会落在其中一边）。</summary>
        private readonly HashSet<string> autoDesignations = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>存档载体（<c>Scribe_Collections</c> 需要 List；读档后转 HashSet 并置空）。</summary>
        private List<string> playerDesignationsWire;
        private List<string> autoDesignationsWire;
        /// <summary>来源集合上限（有界；触顶时先清理死键）。</summary>
        internal const int PlayerDesignationCap = 4096;

        /// <summary>designation 的稳定键：目标为 Thing 用其唯一加载 ID，目标为 Cell 用坐标
        /// （坐标不含地图 ⇒ 由调用场景保证同图统计；跨图误配只可能命中同名坐标，最多导致"多算一件"，
        /// 不会越出"玩家来源"这一层 —— 可接受且比引入地图维度更省）。</summary>
        internal static string DesignationKey(Designation designation)
        {
            if (designation?.def == null) return null;
            var target = designation.target;
            string targetKey = target.HasThing
                ? (target.Thing != null ? target.Thing.GetUniqueLoadID() : "thing:unknown")
                : "cell:" + target.Cell.x.ToString(CultureInfo.InvariantCulture) + ","
                  + target.Cell.z.ToString(CultureInfo.InvariantCulture);
            return designation.def.defName + "|" + targetKey;
        }

        /// <summary>记录一条**玩家下达**的 designation（来源判定在 Capture 层，见 <c>Patch_DesignationSource</c>）。
        /// 触顶时先清理"当前已不存在"的死键（取消后遗留），仍满则丢弃本次（保底有界，绝不无界增长）。</summary>
        internal void RecordPlayerDesignation(Designation designation)
        {
            string key = DesignationKey(designation);
            if (string.IsNullOrEmpty(key)) return;
            autoDesignations.Remove(key);           // 玩家接管：不再是"自动来源"
            if (playerDesignations.Count >= PlayerDesignationCap)
            {
                TrimPlayerDesignations();
                if (playerDesignations.Count >= PlayerDesignationCap)
                {
                    // 审计 P2-④：丢弃必须可观测（旧实现静默吞掉玩家下达）。
                    Log.WarningOnce("[PC][Economy] player designation set is full (" + PlayerDesignationCap
                        + "); further designations will not be tracked until existing ones are cancelled.", 0x1EC0E02);
                    return;
                }
            }
            playerDesignations.Add(key);
            InvalidateVanillaBacklog();
        }
        /// <summary>让待办缓存**立即失效**：来源集合变化（玩家下达 / 自动重标）后下一次查询立刻重算 ——
        /// 否则"取消一条立即下架"要等最多 60 tick（审计 P3-⑤：旧实现只有 TTL，没有失效点）。
        /// 变更量 = 每条下达一次（低频），清两个小字典可忽略。</summary>
        private void InvalidateVanillaBacklog()
        {
            vanillaBacklogCache.Clear(); vanillaBacklogTick.Clear();
        }

        /// <summary>记录一条**自动来源**的 designation（建筑 / 植物自维护），并从玩家集合移除同名键。
        ///
        /// <para><b>为什么要有这个集合（审计 P1 修复）</b>：待办口径用"玩家白名单"（宁少不多，精确反映玩家意图），
        /// 而**计酬**口径用"自动黑名单"（工人确实干了活就该付，除非确认该目标是自维护产生的）——
        /// 两者各司其职：<c>ReportVanillaWorkCompletion</c> 只排除黑名单内的目标。
        /// 这样"玩家用 gizmo 下达但走非设计器路径"（如 `Plant.GetGizmos` 的「全砍枯萎」，其调用栈内没有
        /// Designator 实例帧 ⇒ 不进白名单）**不会**被误拒付 —— 它也不在黑名单里。</para></summary>
        internal void RecordAutoDesignation(Designation designation)
        {
            string key = DesignationKey(designation);
            if (string.IsNullOrEmpty(key)) return;
            playerDesignations.Remove(key);
            if (autoDesignations.Count >= PlayerDesignationCap)
            {
                TrimPlayerDesignations();
                if (autoDesignations.Count >= PlayerDesignationCap)
                {
                    // 链路审计（P3-②）：黑名单触顶必须**可观测** —— 它与玩家集合不同：
                    // 丢弃玩家键只少算待办（保守），而丢弃黑名单键会让该目标**被误付**。
                    Log.WarningOnce("[PC][Economy] auto-issued designation set is full (" + PlayerDesignationCap
                        + "); some automatic targets may be paid as if player-issued.", 0x1EC0E03);
                    return;
                }
            }
            autoDesignations.Add(key);
            InvalidateVanillaBacklog();
        }

        /// <summary>该完成事件的目标是否为**自动来源**（用于计酬侧的排除判定）。
        /// 植物按 <c>CutPlant</c> / <c>HarvestPlant</c> 两个 Thing 键判；矿按 <c>Mine</c> / <c>MineVein</c>
        /// 的 Cell 键判（矿的 designation 目标是格，不是 Thing）。</summary>
        internal bool IsAutoIssuedTarget(Thing target, string workTypeDefName)
        {
            if (target == null || autoDesignations.Count == 0) return false;
            if (workTypeDefName == VanillaWorkTypeMining)
            {
                var cell = target.Position;
                return autoDesignations.Contains(CellKey(VanillaMineDefName, cell))
                    || autoDesignations.Contains(CellKey(VanillaMineVeinDefName, cell));
            }
            string id = target.GetUniqueLoadID();
            return autoDesignations.Contains(VanillaCutPlantDefName + "|" + id)
                || autoDesignations.Contains(VanillaHarvestPlantDefName + "|" + id);
        }
        /// <summary>designation defName 的稳定常量（键构造的唯一来源，避免两处格式漂移）。</summary>
        internal const string VanillaCutPlantDefName = "CutPlant";
        internal const string VanillaHarvestPlantDefName = "HarvestPlant";
        internal const string VanillaMineDefName = "Mine";
        internal const string VanillaMineVeinDefName = "MineVein";
        private static string CellKey(string defName, IntVec3 cell)
            => defName + "|cell:" + cell.x.ToString(CultureInfo.InvariantCulture) + ","
               + cell.z.ToString(CultureInfo.InvariantCulture);

        /// <summary>清理死键：玩家取消后集合里的键不会自动消失，这里按"地图上当前仍存在"重建。
        /// 低频（仅触顶时调用）且遍历量有界（本图已加载的 designation）。</summary>
        private void TrimPlayerDesignations()
        {
            var live = new HashSet<string>(StringComparer.Ordinal);
            var maps = Find.Maps;
            if (maps == null) return;
            for (int i = 0; i < maps.Count; i++)
            {
                var manager = maps[i]?.designationManager;
                if (manager == null) continue;
                var all = manager.AllDesignations;
                if (all == null) continue;
                for (int j = 0; j < all.Count; j++)
                {
                    string key = DesignationKey(all[j]);
                    if (key != null) live.Add(key);
                }
            }
            playerDesignations.RemoveWhere(key => !live.Contains(key));
            // 黑名单同样只保留"当前仍存在"的键：目标消失后该键再也不会命中，
            // 且删掉它可以避免"同键在新目标上复用"被误排除（审计 B 席：跨图 Cell 键复用面）。
            autoDesignations.RemoveWhere(key => !live.Contains(key));
        }

        /// <summary>存档往返的唯一入口（由 <c>GameComponent_PawnEconomy.ExposeData</c> 调用）：
        /// 保存前把运行时集合导出到载体；读档后把载体转回运行时集合并置空载体。
        /// 旧档没有该节点 ⇒ 载体为 null ⇒ 集合为空（诚实语义：我们从未记录过旧档的来源）。</summary>
        internal void ExposePlayerDesignations()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                playerDesignationsWire = new List<string>(playerDesignations);
                autoDesignationsWire = new List<string>(autoDesignations);
            }
            Scribe_Collections.Look(ref playerDesignationsWire, "pawnEconomicPlayerDesignations", LookMode.Value);
            Scribe_Collections.Look(ref autoDesignationsWire, "pawnEconomicAutoDesignations", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                FillFromWire(playerDesignations, playerDesignationsWire);
                FillFromWire(autoDesignations, autoDesignationsWire);
                playerDesignationsWire = null; autoDesignationsWire = null;
            }
        }
        /// <summary>读档回填：过滤空项 + **载入侧二次封顶**（审计 P3-②：外部编辑/损坏的存档
        /// 可能注入超限条目 ⇒ 在这里夹住，保证"上限"这一不变量在载入后同样成立）。</summary>
        private static void FillFromWire(HashSet<string> target, List<string> wire)
        {
            target.Clear();
            if (wire == null) return;
            for (int i = 0; i < wire.Count && target.Count < PlayerDesignationCap; i++)
                if (!string.IsNullOrEmpty(wire[i])) target.Add(wire[i]);
        }

        internal static string VanillaOpportunityKey(string workTypeDefName, string anchorKey, string orderKey = null)
            => WorkPriceRules.Position(workTypeDefName, anchorKey, orderKey);

        internal static bool IsVanillaOpportunity(string opportunityKey)
            => !string.IsNullOrEmpty(opportunityKey) && opportunityKey.StartsWith("vanilla:", StringComparison.Ordinal);

        /// <summary>解析 <c>vanilla:{workType}:{anchorKey}</c>。workType 与 anchorKey 都不含 '<c>:</c>'。</summary>
        internal static bool TryParseVanillaOpportunity(string opportunityKey, out string workTypeDefName, out string anchorKey)
        {
            return WorkPriceRules.Parse(opportunityKey, out workTypeDefName, out anchorKey, out _);
        }

        /// <summary>本图**劳务站**机构（原版工作合同的发布方；车间岗位源不在此列）。</summary>
        internal List<InstitutionRef> QueryVanillaWorkInstitutions(int mapId)
        {
            var rows = new List<InstitutionRef>();
            var all = QueryInstitutions(mapId);
            for (int i = 0; i < all.Count; i++)
                if (all[i].Kind == InstitutionKind.LaborOffice) rows.Add(all[i]);
            return rows;
        }

        /// <summary>
        /// 待办数（= 玩家已下达、尚未完成的该类工作对象数）。<b>60 tick 缓存</b>：机会源会在规划循环里
        /// 逐 Pawn 读取，而 enumeration 是有界但非零成本 —— 缓存把成本压到每图每工种 60 tick 一次。
        /// 缓存是**实例字段**（随 GameComponent 生命周期，不入存档）。
        /// </summary>
        internal int QueryVanillaWorkBacklog(Map map, string workTypeDefName, int cap)
        {
            if (map == null || string.IsNullOrEmpty(workTypeDefName) || cap <= 0) return 0;
            string cacheKey = map.uniqueID.ToString(CultureInfo.InvariantCulture) + "|" + workTypeDefName;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (vanillaBacklogCache.TryGetValue(cacheKey, out int cached)
                && vanillaBacklogTick.TryGetValue(cacheKey, out int at) && now - at < VanillaBacklogCacheTicks)
                return cached;
            int count = CountVanillaBacklog(map, workTypeDefName, cap);
            vanillaBacklogCache[cacheKey] = count; vanillaBacklogTick[cacheKey] = now;
            return count;
        }

        /// <summary>
        /// CHG-20261010-ECON-JOB-INTENT（用户需求「要精准识别，工作任务的发布和取消」）：
        /// 待办只统计**玩家自己下达且当前仍存在**的 designation —— 两者缺一不可。
        /// <list type="bullet">
        /// <item><b>玩家下达</b>：由 Capture 层 <c>Patch_DesignationSource</c> 在
        ///   <c>DesignationManager.AddDesignation</c> 处判定来源后记入 <see cref="playerDesignations"/>，
        ///   因此 <c>CompAutoCut</c>（风力发电机自动清障，每 TickLong 重标）、播种清障、建造清障、
        ///   屋顶清障等**自动来源一个都不计**。</item>
        /// <item><b>取消即下架</b>：统计时二次校验"该 designation 仍存在于地图"
        ///   （<c>SpawnedDesignationsOfDef</c> 的当前集合 ∩ 玩家集合）⇒ 玩家取消后立刻不再计入，
        ///   **不需要挂任何移除路径**（原版自己的集合就是权威事实）。</item>
        /// </list>
        /// 工种口径与计酬口径同源：<c>PlantCutting</c> = 割除（<c>CutPlant</c>，任意植物）+ 伐木
        /// （<c>HarvestPlant</c> 且目标是树）；<c>Mining</c> = <c>Mine</c> + <c>MineVein</c>。
        /// </summary>
        private int CountVanillaBacklog(Map map, string workTypeDefName, int cap)
        {
            // S1（CHG-20261011-WORKCAT）：口径**委托注册表**（唯一实现）—— 原 switch 里的两条分支
            // 已变成 VanillaOrderCatalogue 的两行声明，本函数只做"按工作类型求组内订单之和"。
            // 逐段相减的剩余预算纪律保留（旧写法两段各传 cap 会让合计达 2×cap）。
            if (playerDesignations.Count == 0 || cap <= 0) return 0;
            var groups = VanillaOrderCatalogue.Groups;
            for (int g = 0; g < groups.Length; g++)
            {
                if (!string.Equals(groups[g].WorkTypeDefName, workTypeDefName, StringComparison.Ordinal)) continue;
                int total = 0;
                for (int o = 0; o < groups[g].Orders.Length; o++)
                {
                    int remaining = cap - total;
                    if (remaining <= 0) break;
                    total += QueryOrderBacklog(map, groups[g].Orders[o], remaining);
                }
                return total;
            }
            return 0;
        }
        /// <summary>玩家来源 ∩ 当前存在 的计数。<paramref name="treesOnly"/> = 只数**树**
        /// （伐木口径：非树收获属农场工作，Phase 2 单独裁决）。</summary>
        private int CountPlayerDesignations(DesignationManager manager, DesignationDef def, bool treesOnly, int cap)
        {
            if (def == null || cap <= 0) return 0;
            int count = 0;
            foreach (var designation in manager.SpawnedDesignationsOfDef(def))
            {
                if (designation == null) continue;
                if (!playerDesignations.Contains(DesignationKey(designation))) continue;
                if (treesOnly)
                {
                    var plant = designation.target.Thing as Plant;
                    if (plant?.def?.plant == null || !plant.def.plant.IsTree) continue;
                }
                count++;
                if (count >= cap) break;
            }
            return count;
        }

        /// <summary>该机构下所有 **Posted（已派发待承接）** 原版工作合同（有界）——
        /// 机会源（<c>EconomyVanillaWorkSource</c>）与合同表共用的唯一读入口。</summary>
        internal IReadOnlyList<WorkContract> ReadPostedContractsFor(string anchorKey)
            => Employment.ReadPostedContractsFor(anchorKey);

        /// <summary>CHG-20261012：「工作合同弹窗」的**推荐单价**（每次打开采样一次，**有界 ≤8 个目标**）——
        /// 口径 = 该工作**每件产出物市价**（植物：<c>harvestedThingDef.BaseMarketValue × harvestYield</c>；
        /// 矿脉：<c>mineableThing.BaseMarketValue × mineableYield</c>）的均值，再经
        /// <see cref="WorkContractRules.RecommendUnitWageMinor"/> 取 50%。采样为空 / 该工种无产出市价
        /// ⇒ 返回基准价且 <paramref name="fromMarket"/> = false（UI **如实标注"基准价"**，不伪装成市价）。</summary>
        internal long RecommendWageMinor(Map map, string workType, out bool fromMarket)
        {
            fromMarket = false;
            if (map?.designationManager == null) return WorkContractRules.RecommendedBaseMinor;
            bool mining = string.Equals(workType, "Mining", StringComparison.Ordinal);
            bool cutting = string.Equals(workType, "PlantCutting", StringComparison.Ordinal);
            if (!mining && !cutting) return WorkContractRules.RecommendedBaseMinor;
            double sum = 0d;
            int samples = 0;
            var designations = map.designationManager.AllDesignations;
            for (int i = 0; i < designations.Count && samples < WorkContractRules.MaximumPriceSamples; i++)
            {
                var designation = designations[i];
                if (designation?.def == null) continue;
                var thing = designation.target.Thing;
                if (thing == null || thing.Destroyed) continue;
                float perPiece = 0f;
                if (mining && (designation.def == DesignationDefOf.Mine || designation.def == DesignationDefOf.MineVein))
                {
                    var props = (thing as Mineable)?.def?.building;
                    if (props?.mineableThing != null && props.mineableYield > 0)
                        perPiece = props.mineableThing.BaseMarketValue * props.mineableYield;
                }
                else if (cutting && (designation.def == DesignationDefOf.HarvestPlant || designation.def == DesignationDefOf.CutPlant))
                {
                    var props = (thing as Plant)?.def?.plant;
                    if (props?.harvestedThingDef != null && props.harvestYield > 0)
                        perPiece = props.harvestedThingDef.BaseMarketValue * props.harvestYield;
                }
                if (perPiece <= 0f) continue;
                sum += perPiece;
                samples++;
            }
            if (samples == 0) return WorkContractRules.RecommendedBaseMinor;
            return WorkContractRules.RecommendUnitWageMinor(sum / samples, out fromMarket);
        }

        /// <summary>
        /// CHG-20261012-ECON-CONTRACTIZE：「工作合同弹窗」的**唯一写入口** —— 起草一份 **Posted（已派发待承接）**
        /// 原版工作合同。成功返回 true 并给出 <paramref name="contractId"/>；失败返回 false 并给出**稳定 reason ID**
        /// （呈现层负责翻译，GOV-009）。
        ///
        /// <para><b>校验全部走纯函数</b>（<see cref="WorkContractRules.ValidateDraft"/>，离线可断言）：
        /// 数量 ∈ [1, 可派发]、薪资域、时段域、**国库预留**（可用 − 既有承诺 ≥ 数量 × 薪资）。
        /// 可派发 = 待办 − 已派发（与表列/旧签约路径**同一算式**，避免"表上显示 5 却只能发 2"）。</para>
        ///
        /// <para><b>为什么合同 ID 带 now</b>：同一机构+工种**允许多份并行合同**（用户需求"再点一次发第二份"），
        /// 因此 ID 必须随起草时刻变化；同一 tick 的重复提交自然命中 <c>Sign</c> 的 Replay 幂等。</para>
        /// </summary>
        internal bool TryDraftVanillaWorkContract(string anchorKey, string workType, int jobs, long unitWageMinor,
            int startHour, int endHour, out string contractId, out string reason, string orderKey = null)
        {
            contractId = null; reason = null;
            if (!workPrices.Supported) { reason = "PriceUnavailable"; return false; }
            var anchor = anchors.FirstOrDefault(a => a != null && a.Spawned && !a.Destroyed && a.GetUniqueLoadID() == anchorKey);
            if (anchor == null || anchor.Map == null) { reason = "AnchorUnavailable"; return false; }
            var map = anchor.Map;
            var type = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workType);
            if (type == null) { reason = "NoManagedWorkType"; return false; }
            var order = string.IsNullOrEmpty(orderKey) ? null : FindVanillaOrder(orderKey);
            if (!string.IsNullOrEmpty(orderKey) && (order == null || !order.Wired || order.WorkTypeDefName != workType))
            { reason = "OrderUnavailable"; return false; }
            int backlog = order == null ? QueryVanillaWorkBacklog(map, workType, VanillaWorkBacklogCap)
                : QueryOrderBacklog(map, order, VanillaWorkBacklogCap);
            int available = backlog - VanillaJobsCommitted(map, workType, anchorKey, orderKey);
            long treasuryAvailable = QueryPayrollFundingMinor(map, anchorKey);
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!WorkContractRules.ValidateDraft(jobs, unitWageMinor, startHour, endHour, available,
                    treasuryAvailable, Employment.OutstandingCommitments, out reason)) return false;
            var request = new SignRequest
            {
                ContractId = "contract:" + anchorKey + "|" + workType + "|" + (orderKey ?? "all") + "|" + Guid.NewGuid().ToString("N"),
                PawnKey = string.Empty,   // Posted：尚无持约人（承接时写入）
                InstitutionKey = anchorKey,
                PositionKey = VanillaOpportunityKey(workType, anchorKey, orderKey),
                WorkTypeDefNames = new List<string> { workType },
                WagePerJobMinor = unitWageMinor, MaxJobs = jobs,
                PositionCapacity = 0, Now = now, TermTicks = WorkContractRules.DraftLifetimeTicks,
                WindowStartHour = startHour, WindowEndHour = endHour,
                State = ContractState.Posted,
                // CHG-20261011-LABOR-VISUAL（§5.3「模式在签约时冻结」）：**弹窗起草即冻结新模式** ——
                // 起草者是玩家、发布走劳务站，因此这份合同的生命周期就是"到站签领 → 交验后付款"。
                DeliveryMode = SettlementDeliveryMode.PhysicalSubmission
            };
            var drafted = Employment.Sign(request);
            if (drafted.Code != EmploymentCode.Applied) { reason = drafted.Code.ToString(); return false; }
            contractId = drafted.ObligationId;   // WageResult.ObligationId 装的是 ContractId（既有命名遗留）
            return true;
        }

        /// <summary>该工种在该机构下、**当前可承接**的 Posted 合同（时段允许 + 未过期 + 含该工种）；无则 null。
        /// 判据与机会源同源（<c>AllowsHour</c> + <c>ExpiresTick</c>），避免"机会源看得到、签约说不行"的错位。</summary>
        private WorkContract FindPostedContractFor(Pawn pawn, string workType, string anchorKey, int now, string opportunityKey)
        {
            var posted = Employment.ReadPostedContractsFor(anchorKey);
            if (posted == null || posted.Count == 0) return null;
            int hour = GenLocalDate.HourOfDay(pawn.Map);
            for (int i = 0; i < posted.Count; i++)
            {
                var contract = posted[i];
                if (contract == null || contract.State != ContractState.Posted) continue;
                if (contract.ExpiresTick <= now) continue;
                if (!contract.AllowsHour(hour)) continue;
                if (!ContainsWorkType(contract, workType)) continue;
                if (!string.Equals(contract.PositionKey, opportunityKey, StringComparison.Ordinal)) continue;
                return contract;
            }
            return null;
        }
        /// <summary>承接失败留痕（有界去重）：`[PC][OCCUPY]` 前缀便于 grep；上限 32 条后静默（不刷屏、不无界增长）。</summary>
        private static readonly HashSet<string> OccupyFailureSeen = new HashSet<string>();
        private static void LogOccupyFailure(string contractId, string pawnLabel, string workType, string reason)
        {
            string fingerprint = contractId + "|" + reason;
            if (OccupyFailureSeen.Contains(fingerprint)) return;
            if (OccupyFailureSeen.Count >= 32) return;
            OccupyFailureSeen.Add(fingerprint);
            Log.Message("[PC][OCCUPY] 承接失败：合同=" + contractId + " 人选=" + pawnLabel
                + " 工种=" + workType + " 原因=" + (string.IsNullOrEmpty(reason) ? "(未给出)" : reason));
        }

        private static bool ContainsWorkType(WorkContract contract, string workType)
        {
            for (int i = 0; i < contract.WorkTypeDefNames.Count; i++)
                if (string.Equals(contract.WorkTypeDefNames[i], workType, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// 签约一份**原版工作合同**（由 <c>TrySignForOpportunity</c> 按 <c>vanilla:</c> 前缀分派）。
        /// 与车间岗位签约的差别：**不要求绑定车间区 / 班次**，只要求劳务站营业 + 已发布工资 + 有待办。
        /// 件数 = min(待办, 硬顶, 国库可用额 / 单价)；国库一分钱都不够时**不签**（InsufficientBacking）。
        ///
        /// <para><b>CHG-20261012 优先序</b>：同一机会键下**先承接 Posted 合同**（弹窗草拟、先到先得），
        /// 无 Posted 才走下方政策路径（两套并存；净删政治是独立批次 —— 任何时刻不出现"无人能派工"的断链态）。</para>
        /// </summary>
        private bool TrySignVanillaWork(string pawnId, int mapId, string opportunityKey, int now, out string reason)
        {
            reason = null;
            if (!TryParseVanillaOpportunity(opportunityKey, out string workType, out string anchorKey))
            { reason = "OpportunityMalformed"; return false; }
            if (!TryGetPlanningPawn(pawnId, mapId, out var pawn) || pawn == null || pawn.Map == null)
            { reason = "PawnUnknown"; return false; }
            // ── CHG-20261012：**优先承接**弹窗草拟的 Posted 合同（同一份合同 Posted→Active，
            // 条款/时段/薪资原样保留）；无 Posted 才走下方政策路径（两套并存）。──
            var posted = FindPostedContractFor(pawn, workType, anchorKey, now, opportunityKey);
            if (posted != null)
            {
                bool occupied = TryOccupyPostedContract(pawnId, pawn, workType, posted, now, out reason);
                // CHG-20261012-WORKGATE · W4：承接失败此前只体现在返回值里 ⇒ 玩家侧"派了没人接"无从定位
                //（2026-10-11 实机报障）。这里对"**确实有 Posted 合同**却没接成"留一条可 grep 的日志；
                // **成功不记**（否则每份合同都会刷一行）。去重按 合同号+原因，最多记 32 条（有界，防刷屏）。
                if (!occupied) LogOccupyFailure(posted.ContractId, pawn.LabelShort, workType, reason);
                return occupied;
            }
            WorkPriceRules.Parse(opportunityKey, out _, out _, out string requestedOrder);
            if (!string.IsNullOrEmpty(requestedOrder)) { reason = "ContractAlreadyTaken"; return false; }
            // CHG-20261012-ECON-CONTRACTIZE：**机构政策不再参与派工**（用户裁决「移除全部的机构政策」）——
            // 旧政策的三道门槛（已发布工资 / 营业中 / 营业时段）整体退役：合同化路径下工资与时段的
            // 权威来源是**合同自身条款**（弹窗逐份设定）。本分支仅作**老档兜底**保留，并统一使用
            // 登记基准价（政策缺失时也必须能派工，否则老档会永久停摆）。
            long wageMinor = QuotedWorkPrice(pawn.Map, anchorKey, workType, null, out _);
            if (wageMinor <= 0) { reason = "PriceUnavailable"; return false; }
            var type = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workType);
            if (type == null) { reason = "NoManagedWorkType"; return false; }
            var anchor = anchors.FirstOrDefault(a => a != null && a.Spawned && a.Map == pawn.Map && a.GetUniqueLoadID() == anchorKey);
            if (anchor == null || anchor.Destroyed || anchor.IsBurning()) { reason = "ZoneUnavailable"; return false; }
            int backlog = QueryVanillaWorkBacklog(pawn.Map, workType, VanillaWorkBacklogCap);
            if (backlog <= 0) { reason = "JobCapReached"; return false; }   // 无待办：不签空合同
            var current = ActiveContractFor(pawnId);
            if (current != null && current.PositionKey == opportunityKey) return true;
            if (current != null) { Employment.Terminate(current.ContractId); ReleaseWorkTypes(pawn); }
            // CHG-20261010-ECON-VANILLA-CUT（用户需求「允许玩家修改并派发多个不同数量的合同」）：
            // **同工种待办预留** —— 别的 Pawn 的活跃合同里"已承诺但尚未交付"的件数从待办中扣除，
            // 于是多份合同可以并行而不超总量：100 件割除会自然拆成多份（32+32+32+4），
            // 多个求职殖民者各签一份、同时开工。待办是**实时**的（完成一件 designation 即消失），
            // 合同剩余件数同样实时递减 ⇒ 两者相减不会双重计数。
            // 本 Pawn 的旧合同已在上面终止（不再计入 committed）。
            int committed = VanillaJobsCommitted(pawn.Map, workType, anchorKey);
            int availableJobs = backlog - committed;
            if (availableJobs <= 0) { reason = "JobsAlreadyCommitted"; return false; }
            // ECON-R05 同源预留：可用余额 = 国库 Available − 既有活跃合同剩余承诺；不足则**部分承接**。
            // CHG-20261012-ECON-CONTRACTIZE：件数上限 = 固定常量（"每份 ±"已删；合同化路径的件数由
            // 弹窗逐份给定），其余由上面的待办预留约束 ⇒ 100 件会自然拆成多份、多人并行承接。
            int maxJobs = Math.Min(availableJobs, VanillaMaxJobsPerContract);
            long available = QueryPayrollFundingMinor(pawn.Map, anchorKey) - Employment.OutstandingCommitments;
            if (wageMinor > 0)
            {
                long affordable = available / wageMinor;
                if (affordable < maxJobs) maxJobs = affordable <= 0L ? 0 : (int)Math.Min(affordable, int.MaxValue);
            }
            if (maxJobs <= 0) { reason = "InsufficientBacking"; return false; }
            var request = new SignRequest
            {
                ContractId = "contract:" + pawnId + "|" + opportunityKey + "|w" + wageMinor.ToString(CultureInfo.InvariantCulture) + "|" + now.ToString(CultureInfo.InvariantCulture),
                PawnKey = pawnId, InstitutionKey = anchorKey, PositionKey = opportunityKey,
                WorkTypeDefNames = new List<string> { workType },
                WagePerJobMinor = wageMinor, MaxJobs = maxJobs,
                PositionCapacity = 0,   // 件数型合同：不限份数（总量由上面的待办预留约束，而非岗位空位）
                Now = now, TermTicks = EmploymentPolicy.ContractTermTicks,
                // CHG-20261011-LABOR-VISUAL：老档兜底分支（政策路径）同样冻结新模式 ——
                // 地图劳务链一律"到站交验后付款"，不得因走哪条分支而静默退回即时付款。
                DeliveryMode = SettlementDeliveryMode.PhysicalSubmission
            };
            var signed = Employment.Sign(request);
            if (signed.Code != EmploymentCode.Applied) { reason = signed.Code.ToString(); return false; }
            var live = Employment.ReadContract(signed.ObligationId);
            if (live == null || live.State != ContractState.Active) { reason = "ContractNotActive"; return false; }
            ApplyWorkTypes(pawn, new List<WorkTypeDef> { type }, now);
            // FA-11 同构复核（ARCH R2）：启用之后必须复核 —— 合同仍活跃，且该工作类型在该 Pawn 上
            // **确实可用**（未被原版禁用且优先级 > 0）。否则当场终止并释放工作类型，绝不留下
            // "活跃合同 + 工作类型未启用"的静默状态（那会让合同既做不完又不退出）。
            if (Employment.ReadContract(signed.ObligationId)?.State != ContractState.Active
                || pawn.WorkTypeIsDisabled(type) || (pawn.workSettings?.GetPriority(type) ?? 0) <= 0)
            {
                Employment.Terminate(signed.ObligationId);
                ReleaseWorkTypes(pawn);
                reason = "WorkTypeUnavailable";
                return false;
            }
            return true;
        }

        /// <summary>承接一份 **Posted** 合同：**先校验**（该合同仍可承接）→ 终止旧约 → <c>TryOccupy</c>
        /// （同一 ContractId 转 Active）→ 经既有所有权通道启用该工种 → FA-11 同构复核（失败则当场终止并释放，
        /// **绝不留**"活跃合同 + 工作类型未启用"的静默滞留）。竞态（另一人已接走）返回稳定 reason。
        ///
        /// <para><b>CHG-20261012 · B1（P2-2）为什么校验必须在终止之前</b>：旧实现"先终止旧约 + 释放工种、
        /// 再去占用"，一旦占用失败（合同已被他人接走 / 已被到期清理终结）就把该 Pawn 的**既有合同与工种
        /// 一起丢掉** —— 而这次承接本来只是"换一份更好的合同"，失败却成了净损失（不可逆）。现在失败路径
        /// **不动**任何既有状态：校验判据与 <c>EmploymentService.TryOccupy</c> **同源**（只判 <c>State == Posted</c>，
        /// 不加时段/过期判据，否则会造出"复核拒、占用收"的不对称）。</para></summary>
        private bool TryOccupyPostedContract(string pawnId, Pawn pawn, string workType, WorkContract posted, int now, out string reason)
        {
            reason = null;
            var type = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workType);
            if (type == null) { reason = "NoManagedWorkType"; return false; }
            // ① 先校验（只读）：合同仍处于待承接态才允许动旧约。
            if (Employment.ReadContract(posted.ContractId)?.State != ContractState.Posted)
            { reason = "ContractAlreadyTaken"; return false; }
            // ② 校验通过才终止旧约 + 释放工种（此刻起失败面只剩下列的原子占用，而它与本判据同源）。
            var current = ActiveContractFor(pawnId);
            if (current != null) { Employment.Terminate(current.ContractId); ReleaseWorkTypes(pawn); }
            // CHG-20261011-LABOR-VISUAL（§5.4）：承接**与状态转换同一项写入**把付款模式设为 PhysicalSubmission ——
            // 旧档的 Posted 缺该字段（读到 LegacyImmediate），若不在这一刻原子改写，承接后就会静默走即时付款。
            if (!Employment.TryOccupy(posted.ContractId, pawnId, now, EmploymentPolicy.ContractTermTicks,
                    SettlementDeliveryMode.PhysicalSubmission))
            { reason = "ContractAlreadyTaken"; return false; }
            ApplyWorkTypes(pawn, new List<WorkTypeDef> { type }, now);
            if (Employment.ReadContract(posted.ContractId)?.State != ContractState.Active
                || pawn.WorkTypeIsDisabled(type) || (pawn.workSettings?.GetPriority(type) ?? 0) <= 0)
            {
                Employment.Terminate(posted.ContractId);
                ReleaseWorkTypes(pawn);
                reason = "WorkTypeUnavailable";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 原版工作合同的**退出判据**（与车间岗位分开：它没有区/班次，因此不能用 ZoneId/Shift 解析）。
        /// 合同在以下情况退出：机构消失/燃烧、政策不再营业或不再发布工资、玩家主动关闭制度、
        /// 或合同到期/件数做满（由 <c>ContractExitRules</c> 统一裁决）。
        /// <b>待办变 0 不是退出理由</b> —— 那是"暂时没活干"，合同应保留到件数或期限。
        ///
        /// <para><b>CHG-20261012（合同化）</b>：**政策完全缺失**不再算"机构关闭" —— 弹窗草拟的合同
        /// 其存续依据是**合同自身条款**（起草即承诺），不能被"该机构没有政策记录"当场清退；
        /// 政策**存在但玩家关闭/停发工资**时仍按退出处理（那是玩家的明确意图，不是缺省）。</para>
        ///
        /// <para><b>B2（政策净删）</b>：政策这条来源整体移除 ⇒ "机构营业"只剩**制度阶段**这一个闸门
        /// （下面 <c>mode.Desired</c>）；起草口径（上文）继续成立。存量合同**不被政策清退**，
        /// 由制度阶段 / 岗位可用性 / 件数 / 到期四条原有判据处置。</para>
        /// </summary>
        private ContractExitCode EvaluateVanillaContractExit(WorkContract contract, int now)
        {
            if (contract == null || !TryParseVanillaOpportunity(contract.PositionKey, out _, out string anchorKey))
                return ContractExitCode.None;
            bool institutionOpen = true;
            var mode = QueryInstitutionMode();
            if (institutionOpen && mode != null && !mode.Desired) institutionOpen = false;
            var anchor = anchors.FirstOrDefault(a => a != null && a.Spawned && a.GetUniqueLoadID() == anchorKey);
            bool positionAvailable = anchor != null && !anchor.Destroyed && !anchor.IsBurning();
            // CHG-20261010-ECON-LINKFIX-B（链路审计 A 席 P1-②）：与**车间路径对称** ——
            // 持约者失去该工种（被玩家禁用 / 优先级归零 / 伤残致不可用）时合同必须退出，
            // 否则留下"活跃合同 + 无人可做"的滞留（锁住待办与他人的签约名额，最长到到期）。
            // 判据复用车间路径的同一实现（`CanStillWork`，纯只读）；Pawn 离图/不可寻时不在此判
            // （没有实例可判，交给 `TrimExpiredContracts` 的到期兜底）。
            if (positionAvailable)
            {
                var holder = PawnByKey(contract.PawnKey);
                if (holder != null && !CanStillWork(holder, contract)) positionAvailable = false;
            }
            return ContractExitRules.ShouldExit(contract, now, institutionOpen, positionAvailable);
        }

        /// <summary>按**工作识别注册表**（<see cref="VanillaOrderCatalogue"/>）计算某订单的待办（有界）。
        /// **未接线族同样计数** —— "识别到"必须可见（S1 的诚实性要求：先看得见，再接通）。</summary>
        internal int QueryOrderBacklog(Map map, VanillaOrder order, int cap)
        {
            if (order == null || cap <= 0 || playerDesignations.Count == 0) return 0;
            var manager = map?.designationManager;
            if (manager == null) return 0;
            switch (order.Backlog)
            {
                case OrderBacklogKind.MineAndVein:
                    {
                        // 预算逐段相减（与 CountVanillaBacklog 的既有纪律一致：两段不得各传 cap）。
                        int mine = CountPlayerDesignations(manager, DesignationDefOf.Mine, false, cap);
                        return mine + CountPlayerDesignations(manager, DesignationDefOf.MineVein, false, cap - mine);
                    }
                case OrderBacklogKind.TreeHarvest:
                    return CountPlayerDesignations(manager, DesignationDefOf.HarvestPlant, true, cap);
                case OrderBacklogKind.CropHarvest:
                    {
                        // 伐木与收获**共享** `HarvestPlant` 标记 ⇒ 收获 = 全部 − 树（否则同一张单被数两次）。
                        int trees = CountPlayerDesignations(manager, DesignationDefOf.HarvestPlant, true, cap);
                        int all = CountPlayerDesignations(manager, DesignationDefOf.HarvestPlant, false, cap);
                        return all > trees ? all - trees : 0;
                    }
                default:
                    // 单指定口径：defName 由注册表给出；mod 未安装/def 缺失即 0（fail-closed，不猜）。
                    return CountPlayerDesignations(manager,
                        DefDatabase<DesignationDef>.GetNamedSilentFail(order.DesignationDefName), false, cap);
            }
        }
        /// <summary>注册表口径的待办汇总：<paramref name="payable"/> = 已接线族之和（**可计酬**），
        /// <paramref name="unwired"/> = 未接线族之和（**识别到但尚不能付**）—— 两者都上屏，差距可见。</summary>
        internal void QueryCatalogueBacklogSums(Map map, int cap, out int payable, out int unwired)
        {
            payable = 0; unwired = 0;
            var groups = VanillaOrderCatalogue.Groups;
            for (int g = 0; g < groups.Length; g++)
            {
                int sum = 0;
                for (int o = 0; o < groups[g].Orders.Length; o++) sum += QueryOrderBacklog(map, groups[g].Orders[o], cap);
                if (groups[g].Wired) payable += sum; else unwired += sum;
            }
        }
        /// <summary>本图该工种**已派发件数** = Σ(**已派发待承接**合同剩余) + Σ(活跃 vanilla 合同剩余)。
        /// CHG-20261010-ECON-VANILLA-CUT：与 <c>QueryVanillaContractRows</c> 同源遍历（本图自由殖民者 +
        /// <c>QueryActiveContract</c>），因此**不需要任何新的 Save 读取 API**，也不引入新的持久化面。
        /// 用途：签约前把待办扣掉本值（多份合同不超总量），以及 UI 显示"已派发 N 件"。
        ///
        /// <para><b>CHG-20261012（GATE-SAVE 2026-10-12 P1-1 修复）</b>：**Posted（已派发待承接）也计入** ——
        /// 弹窗派发后表格的"已派发 / 就业率"与弹窗的"可派发"必须立刻反映该承诺，否则可对同一批待办
        /// 重复派发、合计超量（与资金预留口径 <c>SumOutstandingCommitments</c> 的"已含 Posted"对称）。</para></summary>
        internal int VanillaJobsCommitted(Map map, string workType, string anchorKey, string orderKey = null)
        {
            if (map == null || string.IsNullOrEmpty(workType) || string.IsNullOrEmpty(anchorKey)) return 0;
            string expected = VanillaOpportunityKey(workType, anchorKey);
            int committed = PostedJobsPending(anchorKey, expected, orderKey);
            var pawns = map.mapPawns?.FreeColonistsSpawned;
            if (pawns == null) return committed;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                if (pawn == null) continue;
                var contract = QueryActiveContract(pawn.GetUniqueLoadID());
                if (contract == null || contract.State != ContractState.Active) continue;
                if (!WorkPriceRules.Reserves(contract.PositionKey, workType, anchorKey, orderKey)) continue;
                int remaining = contract.MaxJobs - contract.CompletedJobs;
                if (remaining > 0) committed += remaining;
            }
            return committed;
        }
        /// <summary>该机构下 **Posted（已派发待承接）** 合同里本工种的剩余件数（有界枚举，与资金预留同源口径）。</summary>
        private int PostedJobsPending(string anchorKey, string expectedPositionKey, string orderKey = null)
        {
            var posted = Employment.ReadPostedContractsFor(anchorKey);
            int pending = 0;
            for (int i = 0; i < posted.Count; i++)
            {
                var contract = posted[i];
                if (contract == null || contract.State != ContractState.Posted) continue;
                WorkPriceRules.Parse(expectedPositionKey, out string work, out _, out _);
                if (!WorkPriceRules.Reserves(contract.PositionKey, work, anchorKey, orderKey)) continue;
                int remaining = contract.MaxJobs - contract.CompletedJobs;
                if (remaining > 0) pending += remaining;
            }
            return pending;
        }

        /// <summary>B（CHG-20261010-ECON-LABOR-UI2）：劳务站的**完成统计** —— 只读、有界。
        /// 已完成件数 = 本图该柜台活跃 vanilla 合同的 Σ <c>CompletedJobs</c>（与合同行同源）；
        /// 已付工资 = **这些合同**对应的应付账里已付部分之和（按 ContractId 归属过滤，
        /// 因此不会把车间岗位的工资算进劳务站）。</summary>
        internal void VanillaWorkStats(Map map, string anchorKey, out int donePieces, out long paidMinor,
            out long unpaidMinor)
        {
            donePieces = 0; paidMinor = 0L; unpaidMinor = 0L;
            var pawns = map?.mapPawns?.FreeColonistsSpawned;
            if (pawns == null) return;
            var contractIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                if (pawn == null) continue;
                var contract = QueryActiveContract(pawn.GetUniqueLoadID());
                if (contract == null || !IsVanillaOpportunity(contract.PositionKey)
                    || !string.Equals(contract.InstitutionKey, anchorKey, StringComparison.Ordinal)) continue;
                contractIds.Add(contract.ContractId);
                donePieces += contract.CompletedJobs;
            }
            if (contractIds.Count == 0) return;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                if (pawn == null) continue;
                var obligations = Employment.ReadObligationsFor(pawn.GetUniqueLoadID());
                if (obligations == null) continue;
                for (int j = 0; j < obligations.Count; j++)
                {
                    var obligation = obligations[j];
                    if (obligation == null || !contractIds.Contains(obligation.ContractId)) continue;
                    paidMinor += obligation.PaidMinor;
                    long due = obligation.AmountMinor - obligation.PaidMinor;
                    if (due > 0L) unpaidMinor += due;
                }
            }
        }

        /// <summary>该劳务站的**原版工作合同**行（读模型用；只读，不写任何状态）。
        /// 两型同表：**Posted（已派发待承接，排最前）** + **Active（承接中）** —— 二者共用
        /// <see cref="VanillaContractRow.Compose"/>，列集**逐格同构**（分叉 = 整列静默串列）。
        ///
        /// <para>S2b（CHG-20261011-WORKCAT）：**列化**（Cells/CellSorts）+ **搜索键**（承接人 · 合同号）
        /// + **展开详情**（合同号 / 签订 / 到期 / 时段 / 岗位键）。</para>
        ///
        /// <para><b>B1（CHG-20261012 · P1-2）</b>：Posted 行上表并携带**撤销载荷** —— 此前它们**完全不可见**
        /// （只列已承接的活跃合同）⇒ 派发出去的合同既看不到也收不回，只能等到期。口径与机会源/资金预留
        /// 同源（同一读入口 <c>ReadPostedContractsFor</c>）；已付 / 未付与 <c>VanillaWorkStats</c> **同源**
        /// （obligations 按 ContractId 逐合同归属过滤 —— 不混入车间岗位工资、不新造资金读数）；
        /// Posted 无持约人 ⇒ 已付 / 未付恒为 0.00（如实：这份合同确实还没产生任何计酬，不是"不适用"）。</para></summary>
        internal List<EconomyRow> QueryVanillaContractRows(Map map, string anchorKey)
        {
            var rows = new List<EconomyRow>();
            if (map == null || string.IsNullOrEmpty(anchorKey)) return rows;
            string postedLabel = T("Workspace.Contract.Posted");
            string revokeLabel = T("Workspace.Contract.Revoke");
            // ── ① Posted（待承接）行排首：它们是需要玩家动作的行（有人接走，或就地撤销退回件数与预留）──
            var posted = Employment.ReadPostedContractsFor(anchorKey);
            for (int i = 0; i < posted.Count; i++)
            {
                var contract = posted[i];
                if (contract == null || contract.State != ContractState.Posted) continue;
                if (!IsVanillaOpportunity(contract.PositionKey)) continue;   // 本表只承载原版工作合同
                if (!TryParseVanillaOpportunity(contract.PositionKey, out string postedType, out _)) continue;
                string postedWork = ContractWorkLabel(contract, postedType);
                string zero = Money(0L);
                rows.Add(VanillaContractRow.Compose(contract.ContractId, postedLabel, postedWork,
                    contract.CompletedJobs, contract.MaxJobs, 0L, 0L, contract.WagePerJobMinor,
                    zero, zero,
                    ContractRowText(postedLabel, postedWork, contract.CompletedJobs, contract.MaxJobs,
                        contract.WagePerJobMinor, zero, zero),
                    ContractDetailText(contract), postedLabel + " " + contract.ContractId, revokeLabel));
            }
            // ── ② Active（承接中）行：按本图自由殖民者逐个查活跃合同（复用 QueryActiveContract，零新增读 API）──
            var pawns = map.mapPawns?.FreeColonistsSpawned;
            if (pawns == null) return rows;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                if (pawn == null) continue;
                var contract = QueryActiveContract(pawn.GetUniqueLoadID());
                if (contract == null || !IsVanillaOpportunity(contract.PositionKey)) continue;
                if (!string.Equals(contract.InstitutionKey, anchorKey, StringComparison.Ordinal)) continue;
                if (!TryParseVanillaOpportunity(contract.PositionKey, out string workType, out _)) continue;
                long paidMinor = 0L, unpaidMinor = 0L;
                var obligations = Employment.ReadObligationsFor(pawn.GetUniqueLoadID());
                if (obligations != null)
                {
                    for (int j = 0; j < obligations.Count; j++)
                    {
                        var obligation = obligations[j];
                        if (obligation == null
                            || !string.Equals(obligation.ContractId, contract.ContractId, StringComparison.Ordinal)) continue;
                        paidMinor += obligation.PaidMinor;
                        long due = obligation.AmountMinor - obligation.PaidMinor;
                        if (due > 0L) unpaidMinor += due;
                    }
                }
                string pawnLabel = pawn.LabelShortCap;
                string workLabel = ContractWorkLabel(contract, workType);
                string paidText = Money(paidMinor);
                string unpaidText = Money(unpaidMinor);
                rows.Add(VanillaContractRow.Compose(contract.ContractId, pawnLabel, workLabel,
                    contract.CompletedJobs, contract.MaxJobs, paidMinor, unpaidMinor, contract.WagePerJobMinor,
                    paidText, unpaidText,
                    ContractRowText(pawnLabel, workLabel, contract.CompletedJobs, contract.MaxJobs,
                        contract.WagePerJobMinor, paidText, unpaidText),
                    ContractDetailText(contract), pawnLabel + " " + contract.ContractId, null));
            }
            return rows;
        }

        /// <summary>合同行的**悬停全量文本**（唯一拼装点：Posted 与 Active 同式，只有承接人一处不同）。</summary>
        private static string ContractRowText(string holderLabel, string workLabel, int completedJobs, int maxJobs,
            long wageMinor, string paidText, string unpaidText)
            => holderLabel + " · " + workLabel + " · "
               + completedJobs.ToString(CultureInfo.InvariantCulture)
               + "/" + maxJobs.ToString(CultureInfo.InvariantCulture)
               + " · " + T("Workspace.ContractWage") + " " + Money(wageMinor)
               + " · " + T("Workspace.Contract.Paid") + " " + paidText
               + " · " + T("Workspace.Contract.Col.Unpaid") + " " + unpaidText;

        /// <summary>合同行的**展开详情**（只读）。B1 起含「时段」—— 它是 Posted 行"要不要撤销"的判断依据，
        /// 且与 <c>WorkContract.AllowsHour</c> 同源语义（起 == 止 ⇒ 全天）。
        /// CHG-20261011-LABOR-VISUAL：追加**真实履约阶段 + 付款模式 + 阻断原因 + 下一步** ——
        /// 劳务站与工资结算处是**同一张表同一组装点**（<c>QueryVanillaContractRows</c>），故两处口径天然同源。</summary>
        private static string ContractWorkLabel(WorkContract contract, string workType)
        {
            if (WorkPriceRules.Parse(contract.PositionKey, out _, out _, out string order) && order != null)
                return T("Order." + order);
            return WorkTypeLabel(workType);
        }
        private string ContractDetailText(WorkContract contract)
        {
            string text = T("Workspace.Contract.Detail.Id") + ": " + contract.ContractId
               + "\n" + T("Workspace.Contract.Detail.Signed") + ": "
               + GenDate.DateFullStringAt(contract.SignedTick, UnityEngine.Vector2.zero)
               + "\n" + T("Workspace.Contract.Detail.Expires") + ": "
               + GenDate.DateFullStringAt(contract.ExpiresTick, UnityEngine.Vector2.zero)
               + "\n" + T("Workspace.Contract.Detail.Window") + ": " + ContractWindowText(contract)
               + "\n" + T("Workspace.Contract.Detail.Position") + ": " + contract.PositionKey;
            var flow = QueryLaborWorkflow(contract.ContractId);
            if (flow != null)
            {
                text += "\n" + T("LaborVisual.Contract.Stage") + ": " + LaborVisualText.StageLabel(flow.Stage)
                    + " · " + LaborVisualText.ModeLabel(flow.Mode);
                if (flow.Hold != FlowHold.None)
                    text += " · " + LaborVisualText.HoldLabel(flow.Hold)
                        + " (" + LaborVisualText.ReasonLabel(flow.ReasonCode) + ")";
                string nextAction = LaborVisualText.NextActionLabel(flow.NextAction);
                if (!string.IsNullOrEmpty(nextAction))
                    text += "\n" + T("LaborVisual.Contract.Next") + ": " + nextAction;
            }
            else if (contract.State == ContractState.Posted)
            {
                // 待承接合同**还没有**在办流程（流程随意图产生）—— 如实标"未在办"，不假装已在办理。
                text += "\n" + T("LaborVisual.Contract.Stage") + ": "
                    + LaborVisualText.StageLabel(LaborFlowStage.None) + " · " + LaborVisualText.ModeLabel(contract.DeliveryMode);
            }
            else if (contract.DeliveryMode == SettlementDeliveryMode.Unknown)
            {
                // 旧档/损坏行的付款模式无法识别（fail-closed）：明确告知已停止新增计酬，绝不静默。
                text += "\n" + T("LaborVisual.Contract.Stage") + ": "
                    + LaborVisualText.ModeLabel(SettlementDeliveryMode.Unknown);
            }
            return text;
        }

        /// <summary>时段文本（与 <c>WorkContract.AllowsHour</c> 同源：起 == 止 = 全天；否则原样显示，跨夜如 22:00–06:00）。</summary>
        private static string ContractWindowText(WorkContract contract)
            => contract.WindowStartHour == contract.WindowEndHour
                ? T("Workspace.Contract.AllDay")
                : contract.WindowStartHour.ToString("00", CultureInfo.InvariantCulture) + ":00–"
                  + contract.WindowEndHour.ToString("00", CultureInfo.InvariantCulture) + ":00";

        /// <summary>该劳务站当前有几人承接了原版工作合同（KPI 用）。</summary>
        internal int CountVanillaContracts(Map map, string anchorKey)
        {
            if (map?.mapPawns == null || string.IsNullOrEmpty(anchorKey)) return 0;
            var pawns = map.mapPawns.FreeColonistsSpawned;
            int count = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                var contract = pawns[i] == null ? null : QueryActiveContract(pawns[i].GetUniqueLoadID());
                if (contract != null && IsVanillaOpportunity(contract.PositionKey)
                    && string.Equals(contract.InstitutionKey, anchorKey, StringComparison.Ordinal)) count++;
            }
            return count;
        }

        /// <summary>工作类型标签：用原版 <c>WorkTypeDef</c> 的 Def 标签（自动本地化，含第三方 Mod 的 DefInjected）
        /// ⇒ 不新增键、不硬编码文本（GOV-010 / GOV-009）。Def 缺失时回落稳定 defName。</summary>
        internal static string WorkTypeLabel(string workTypeDefName)
            => DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeDefName)?.labelShort ?? workTypeDefName;
    }
}
