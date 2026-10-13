using System;
using System.Collections.Generic;
using PersonalChronicle.Automation;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// 工作合同闸门：把三个授权来源（合同 / 车间班次托管 / 生存白名单）合成一份**每个 pawn 的已授权工种集合**，
    /// 交给 <c>Capture\Patch_PawnWorkSettings_CacheGivers</c> 在作业表重建时过滤。CHG-20261012-WORKGATE。
    ///
    /// <para><b>为什么在这里（Economy 层）</b>：合同的权威在 <see cref="GameComponent_PawnEconomy"/>；
    /// 放同层可零新增依赖边（Capture→Economy 与 Economy→Data 都是既有边）。</para>
    ///
    /// <para><b>不缓存跨帧结果</b>：闸门只在**作业表重建**时被调用（原版 <c>workGiversDirty</c> 驱动，
    /// 不是每 tick）；每次重建算一次集合（≤ 工种数），随后对全部 giver 做 O(1) 查表。合同/班次/设置变化后
    /// 由 <see cref="InvalidateAll"/> 触发重建（最多滞后到下一次维护槽 ≈ 250 tick）。</para>
    ///
    /// <para><b>关闭即原版</b>：设置项 <c>EnableWorkContractGate</c> 为 false 时 <see cref="AuthorizedWorkTypes"/>
    /// 返回 null，补丁直接不做任何过滤（零行为变化，回滚路径）。</para>
    /// </summary>
    public static class WorkAuthorityService
    {
        /// <summary>主动维护的时刻（非权威、零持久化）；由真正 GameComponentTick 驱动。</summary>
        private static int lastInvalidateTick = -1;
        private static Game cachedGame;
        private static int cachedRevision = -1;

        /// <summary>UI 缓存：**每 tick 一张 per-pawn 表**（tick 变化即清空重算；可从权威重建，非权威状态）。
        /// 不用单槽缓存：原版面板按"列=工种 × 行=人"遍历，相邻调用会来回换人 ⇒ 单槽命中率≈0。</summary>
        private static int cachedTick = -1;
        private static Dictionary<Pawn, HashSet<string>> cachedByPawn;

        /// <summary>闸门总开关（设置缺失时按"关"处理，避免设置尚未加载时误拦）。</summary>
        public static bool Enabled
        {
            get
            {
                ChronicleSettings settings = PersonalChronicleMod.Settings;
                return settings != null && settings.EnableWorkContractGate;
            }
        }

        /// <summary>托管名单只是候选；实际授权必须属于该 pawn 当前值勤与岗位。班餐独立托管。</summary>
        private static bool ShiftManaged(Pawn pawn, string workTypeDefName)
        {
            if (pawn == null || pawn.Map == null || string.IsNullOrEmpty(workTypeDefName)) return false;
            if (workTypeDefName == PersonalChronicle.Domain.Meal.MealScheduleDefaults.MealWorkTypeDefName) return true;
            var manager = MapComponent_WorkshopManager.Get(pawn.Map);
            var type = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeDefName);
            return manager != null && type != null && manager.AuthorizesScheduledWork(pawn, type);
        }

        /// <summary>合同以外的独立授权；订单限界不能覆盖玩家白名单或该 Pawn 实际排班。</summary>
        internal static bool HasIndependentAuthority(Pawn pawn, string workTypeDefName)
        {
            var settings = PersonalChronicleMod.Settings;
            if (settings != null && WorkAuthority.IsWhitelisted(workTypeDefName, settings.WorkGateWhitelist)) return true;
            return ShiftManaged(pawn, workTypeDefName);
        }

        /// <summary>精确订单的候选 giver 白名单；工种授权不能顺带开放该工种内无薪的另一订单/自动劳动。</summary>
        public static bool GiverMatchesOrder(string order, Type giverType)
        {
            if (giverType == null) return false;
            switch (order)
            {
                case "Mine": return typeof(WorkGiver_Miner).IsAssignableFrom(giverType);
                case "ChopWood":
                case "CutPlants":
                case "Harvest": return typeof(WorkGiver_PlantsCut).IsAssignableFrom(giverType);
                case "Hunt": return typeof(WorkGiver_HunterHunt).IsAssignableFrom(giverType);
                case "Slaughter": return typeof(WorkGiver_Slaughter).IsAssignableFrom(giverType);
                case "Tame": return typeof(WorkGiver_Tame).IsAssignableFrom(giverType);
                case "Haul": return typeof(WorkGiver_HaulGeneral).IsAssignableFrom(giverType);
                default: return false;
            }
        }

        public static bool IsGiverAuthorizedForScope(Pawn pawn, WorkGiver giver)
        {
            if (!Enabled || pawn == null) return true;
            string name = giver?.def?.workType?.defName;
            if (string.IsNullOrEmpty(name) || HasIndependentAuthority(pawn, name)) return true;
            var contract = GameComponent_PawnEconomy.Current?.QueryActiveContract(pawn.GetUniqueLoadID());
            if (contract == null || !WorkPriceRules.Parse(contract.PositionKey, out _, out _, out string scope)
                || string.IsNullOrEmpty(scope)) return true; // 旧合同的工种覆盖口径不重写
            return GiverMatchesOrder(scope, giver.GetType());
        }

        private static void EnsureGameContext()
        {
            Game game = Verse.Current.Game;
            if (ReferenceEquals(cachedGame, game)) return;
            cachedGame = game;
            lastInvalidateTick = -1;
            cachedTick = -1;
            cachedRevision = -1;
            cachedByPawn?.Clear();
        }

        /// <summary>不能从重建回调自失效：dirty=false 时原版永远不调用该回调。真实 tick 主动推进维护。</summary>
        public static void Tick(int now)
        {
            EnsureGameContext();
            if (!Enabled || !WorkAuthority.RefreshDue(now, lastInvalidateTick)) return;
            lastInvalidateTick = now;
            InvalidateAll();
        }

        /// <summary>
        /// 该 pawn 当前被授权的工种集合；<b>返回 null = 不过滤</b>（闸门关闭 / 无法判定 ⇒ fail-open）。
        /// </summary>
        public static HashSet<string> AuthorizedWorkTypes(Pawn pawn)
        {
            if (pawn == null || pawn.workSettings == null) return null;
            if (!Enabled) return null;

            var allowed = new HashSet<string>();
            // ① 生存白名单
            ChroniqueWhitelist(allowed);
            // ② 当前 Pawn 的实际排班（固定岗位 / 班次 / 轮班 / 设备映射）。
            // 自动分配的 map-wide 候选仅含 shift.TaskType，不能代表实际排班全集。
            Map map = pawn.Map;
            if (map != null)
            {
                List<WorkTypeDef> types = DefDatabase<WorkTypeDef>.AllDefsListForReading;
                for (int i = 0; i < types.Count; i++)
                {
                    string name = types[i]?.defName;
                    if (ShiftManaged(pawn, name)) allowed.Add(name);
                }
            }
            // ③ 劳务站合同（Active 且未过期；粒度 = 该 pawn × 工种）
            GameComponent_PawnEconomy runtime = GameComponent_PawnEconomy.Current;
            if (runtime != null)
            {
                try { runtime.CollectWorkAuthorityWorkTypes(pawn, allowed); }
                catch (Exception ex) { Log.ErrorOnce("[PC] 工作授权读取合同失败（本次按无合同处理）：" + ex.Message, 0x5A17E1); }
            }
            return allowed;
        }

        /// <summary>UI 用（工作面板格位/悬停，**每帧每格**会被问到）：同 tick 同 pawn 复用一次计算结果，
        /// 避免"每格都建一个 HashSet"的 OnGUI 分配。缓存按 tick / Game / 设置版本失效。</summary>
        public static bool IsAuthorizedNow(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn == null || workType == null || !Enabled) return true;
            EnsureGameContext();
            int tick = GenTicks.TicksGame;
            int revision = WorkAuthorityInvalidator.Revision;
            if (cachedTick != tick || cachedRevision != revision)
            {
                // 原版工作面板是"列=工种 × 行=人"的循环 ⇒ **相邻调用会来回换人**；单槽缓存命中率≈0
                //（PERF 席实测估算 ≈130k 对象/s @60fps）。改为"每 tick 一张 per-pawn 表"：
                // 命中 O(1)、每帧重建次数降到 O(在场殖民者)（≈250→12 次/帧）。
                cachedTick = tick;
                cachedRevision = revision;
                if (cachedByPawn == null) cachedByPawn = new Dictionary<Pawn, HashSet<string>>();
                else cachedByPawn.Clear();
            }
            if (cachedByPawn == null) cachedByPawn = new Dictionary<Pawn, HashSet<string>>();
            if (!cachedByPawn.TryGetValue(pawn, out HashSet<string> set))
            {
                set = AuthorizedWorkTypes(pawn);
                cachedByPawn[pawn] = set;
            }
            return set == null || set.Contains(workType.defName);
        }

        private static void ChroniqueWhitelist(HashSet<string> into)
        {
            ChronicleSettings settings = PersonalChronicleMod.Settings;
            if (settings == null) return;
            List<string> whitelist = settings.WorkGateWhitelist;
            if (whitelist == null) return;
            for (int i = 0; i < whitelist.Count; i++)
                if (!string.IsNullOrEmpty(whitelist[i])) into.Add(whitelist[i]);
        }

        /// <summary>呈现用：某工种对该 pawn 的授权来源（供设置页/工具提示说明"为什么这格是开的"）。</summary>
        public static WorkAuthoritySource SourceFor(Pawn pawn, WorkTypeDef workType)
        {
            if (workType == null) return WorkAuthoritySource.None;
            if (!Enabled) return WorkAuthoritySource.Whitelist;   // 关闭 = 一切按原版放行，呈现上按"不受限"处理
            ChronicleSettings settings = PersonalChronicleMod.Settings;
            bool whitelisted = settings != null && WorkAuthority.IsWhitelisted(workType.defName, settings.WorkGateWhitelist);
            bool shift = ShiftManaged(pawn, workType.defName);
            bool contract = false;
            GameComponent_PawnEconomy runtime = GameComponent_PawnEconomy.Current;
            if (pawn != null && runtime != null)
            {
                try
                {
                    var set = new HashSet<string>();
                    runtime.CollectWorkAuthorityWorkTypes(pawn, set);
                    contract = set.Contains(workType.defName);
                }
                catch (Exception) { contract = false; }
            }
            return WorkAuthority.Resolve(contract, shift, whitelisted);
        }

        /// <summary>合同 / 班次 / 设置变化后：让已缓存的有序作业表重新构建（**唯一的失效入口**）。
        /// 代价有界：每张地图的自由殖民者各一次 dirty 标记，真正的重建发生在下一次取表。</summary>
        public static void InvalidateAll()
        {
            WorkAuthorityInvalidator.InvalidateAll();
            cachedByPawn?.Clear();
            cachedTick = -1;
        }
    }
}
