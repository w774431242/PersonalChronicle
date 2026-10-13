using System.Collections.Generic;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间专属工位 —— **只提权（映射类型 → 最高档 1）+ 旧档迁移还原**
    /// （源自 `CHG-20261003-WORKSTATION`，后经 `CHG-20261005-NODEMOTE` 收窄）。
    ///
    /// <para><b>⚠ 2026-10-05 重大变更（`CHG-20261005-NODEMOTE`，玩家裁定）</b>：
    /// 原「**降档通道**」（把其余所有已勾选工作统一压到最低档 4、带台账与「一键全解除」出口）
    /// **已整体移除**。玩家原话：「不需要这个，只需要人员被移除车间体系，然后恢复显示原版的工作面板，
    /// 玩家可以手动进行调整，避免多余」。`EXC-2026-015` 的 Status 已改为 **Superseded**。</para>
    ///
    /// <para><b>现在本切片只做三件事</b>：</para>
    /// <list type="number">
    /// <item><b>提权</b>（<see cref="AlignKeptWorkTypes"/>）：车间相关映射类型 → 最高档 1，
    /// 由 `EXC-2026-013` 授权，**不动任何其它工作**（其余工作保持玩家原档位）；</item>
    /// <item><b>状态装配</b>（<see cref="SyncWorkStationForZone"/>）：为 L4 状态行提供
    /// 「已对齐 / 跳过 / 在册 / 保留项数 / 需手动优先级」读数；</item>
    /// <item><b>旧档迁移</b>（<see cref="RestoreAllDemotedWork"/>）：由 <c>WorkshopManageView.Refresh</c>
    /// 幂等调用，把**旧存档里已被压到 4 档**的项还原 —— 防原出口删除后这些项永久卡住。
    /// **除迁移外不得再挂任何 UI 出口**（禁止把已删的「一键全解除」重新接回）。</item>
    /// </list>
    ///
    /// <para><b>仍然有效的两条硬红线</b>：</para>
    /// <list type="bullet">
    /// <item>绝不调用 <c>Disable</c> / <c>SetPriority(0)</c>（该禁令的唯一例外是接管通道，见 `EXC-2026-016`）；</item>
    /// <item>不写 tick / 绘制路径 / WorkGiver —— 一律事件驱动，
    /// 复用 <see cref="SyncVanillaWorkSettings"/> 的触发纪律。</item>
    /// </list>
    ///
    /// <para><b>守卫（镜像原版 <c>Pawn_WorkSettings</c>，与既有 EnableWorkTypes 同规）</b>：
    /// <c>workSettings != null</c> → <c>EnableAndInitializeIfNotAlreadyInitialized()</c> →
    /// 跳过 <c>pawn.WorkTypeIsDisabled(type)</c>（原版对 disabled 类型写非 0 会 <c>Log.Error</c>）。</para>
    ///
    /// <para><b>简单模式降级（<c>!Find.PlaySettings.useWorkPriorities</c>）</b>：引擎在该模式下
    /// <b>不读档位</b>（<c>GetPriority</c> 对任何已启用工作恒返回 3）⇒ 档位写入无观测效果，
    /// 故只做 Enable（把 0 档补成默认档）、不做提权，并在 L4 状态行告知「需开启手动优先级」。</para>
    /// </summary>
    public partial class MapComponent_WorkshopManager
    {
        /// <summary>
        /// 车间专属工位状态快照（供 L4 状态行消费；<b>运行态，不落盘</b>）。
        /// </summary>
        public struct WorkStationStatus
        {
            /// <summary>本次成功对齐（提到 1 档 / 降到 4 档）的人数。</summary>
            public int alignedPawns;

            /// <summary>因 <c>WorkTypeIsDisabled</c>（技能缺失 / 背景禁用）被跳过的人数。</summary>
            public int skippedPawns;

            /// <summary>车间内实际参与对齐的排班人数（分母口径）。</summary>
            public int scopedPawns;

            /// <summary>台账中可一键解除的项数。</summary>
            public int ledgerEntries;

            /// <summary>本车间最终生效的保留工作类型数（车间相关 = 提到 1 档的那批）。</summary>
            public int keptWorkTypes;

            /// <summary>玩家处于简单模式（降档通道整条跳过）。</summary>
            public bool needsManualPriorities;
        }

        /// <summary>最近一次同步的结果（L4 状态行读它；仅在同步事件里赋值，绘制路径只读）。</summary>
        public WorkStationStatus LastWorkStationStatus;
        // ---- 复用既有 WorkEnable 的工作区（private 字段跨 partial 共享）----------------
        // workSyncScope : 本车间排班相关 pawn（已去重）
        // workSyncTypes : 本车间"车间相关工作"的 WorkTypeDef 保留集（已去重、保序）

        /// <summary>降档通道的临时工作区（成员级复用，避免每次同步 new List）。</summary>
        private readonly List<WorkTypeDef> demoteAllTypes = new List<WorkTypeDef>();

        /// <summary>设备反查得到的工序行 defName 去重集合（成员级复用）。</summary>
        private readonly HashSet<string> deviceProcessSeen = new HashSet<string>();

        /// <summary>已告警过的未解析工序行 defName（每 defName 只告警一次，COMP-011）。</summary>
        private readonly HashSet<string> unresolvedProcessWarned = new HashSet<string>();

        /// <summary>已告警过的未知岗位键（每键只告警一次）。</summary>
        private readonly HashSet<string> unknownTaskKeyWarned = new HashSet<string>();

        /// <summary>降档时遍历的候选工作类型（复用成员级列表，构造一次）。</summary>
        private List<WorkTypeDef> DemotableWorkTypes()
        {
            if (demoteAllTypes.Count == 0)
            {
                demoteAllTypes.AddRange(DefDatabase<WorkTypeDef>.AllDefsListForReading);
            }
            return demoteAllTypes;
        }

        /// <summary>defName → 工序行 Def（缺失静默跳过，每个 defName 记一次诊断）。</summary>
        private WorkshopProcessDef ResolveProcess(string defName)
        {
            WorkshopProcessDef def = DefDatabase<WorkshopProcessDef>.GetNamedSilentFail(defName);
            if (def == null && !string.IsNullOrEmpty(defName) && unresolvedProcessWarned.Add(defName))
            {
                Log.Warning("[PersonalChronicle] Workshop process standard not found (skipped): "
                    + defName);
            }
            return def;
        }
        /// <summary>
        /// 收集「车间相关工作」保留集 = ① 岗位映射 ② 工序标准表 ③ 设备通道，取并集去重。
        ///
        /// <para><b>为什么并集而非覆盖</b>：三条通道语义互补 —— 岗位映射是"这个车间干什么"、
        /// 工序表是"这道工序的标准分工"、设备通道是"这里实际摆着什么机器"。
        /// 任一为空都不影响其余（安全默认：全空 ⇒ 保留集只有值勤类型）。</para>
        ///
        /// <para><b>纪律</b>：本方法只做 Def → Def 的解析与去重，<b>不写任何 pawn 状态</b>。</para>
        /// </summary>
        private void CollectKeptWorkTypes(Zone_Workshop zone)
        {
            workSyncTypes.Clear();
            deviceProcessSeen.Clear();

            WorkshopTypeDef typeDef = zone != null ? zone.WorkshopType : null;

            // ⚠ 「未指定车间类型」是**允许的中间态**（玩家刚框出车间区、还没选类型时，
            //    岗位页会整片显示"尚未实现"）。此时 ① ② 两条通道都没有映射数据可用，
            //    但**不能因此崩溃**：本方法由管理面板的 Refresh / 排班变更直接调用，
            //    抛异常会让整个面板无法打开（2026-10-04 实机 NRE，堆栈：
            //    CollectKeptWorkTypes [0x00051] ← SyncWorkStationForZone ← Refresh）。
            //    与 WorkEnable.EnableWorkTypes 的同款提前返回同口径（③ 段与
            //    OffShift / Dialog_WorkshopShiftEditor / AutoAssignService 也都有守卫，
            //    只有这里漏了 —— 故① ② 合并到同一次判空，杜绝再漏）。
            if (typeDef != null)
            {
                // ① 岗位映射（EXC-012 / EXC-013 授权面，行为不变）：按本车间各班次的岗位取并集。
                for (int i = 0; i < shifts.Count; i++)
                {
                    WorkshopShift shift = shifts[i];
                    if (shift == null || shift.TargetZone != zone)
                    {
                        continue;
                    }
                    AddWorkTypeNames(typeDef.WorkTypeDefNamesOf(shift.TaskType));
                }

                // ② 工序/岗位标准表（CHG-20261003 新增，标准化细）。
                List<string> keys = typeDef.ProcessKeys();
                for (int i = 0; i < keys.Count; i++)
                {
                    AddProcessWorkTypes(ResolveProcess(keys[i]));
                }
            }

            // ③ 设备通道（CHG-20261003 新增）：车间内实际有工单的工作台 → 工序 → 工作类型。
            CollectDeviceChannelWorkTypes(zone, typeDef);
        }

        /// <summary>把一批 defName 并入保留集（解析失败静默跳过 + 每名只告警一次）。</summary>
        private void AddWorkTypeNames(List<string> defNames)
        {
            if (defNames == null)
            {
                return;
            }
            for (int i = 0; i < defNames.Count; i++)
            {
                WorkTypeDef mapped = ResolveWorkType(defNames[i]);
                if (mapped != null && !workSyncTypes.Contains(mapped))
                {
                    workSyncTypes.Add(mapped);
                }
            }
        }

        /// <summary>把一个工序行的 WorkTypeDef 并入保留集。</summary>
        private void AddProcessWorkTypes(WorkshopProcessDef process)
        {
            if (process == null)
            {
                return;
            }
            string taskKey = process.taskTypeKey;
            if (!string.IsNullOrEmpty(taskKey) && !WorkshopTaskTypeKeys.IsKnown(taskKey)
                && unknownTaskKeyWarned.Add(taskKey))
            {
                Log.Warning("[PersonalChronicle] Unknown workshop task type key (treated as Production): "
                    + taskKey);
            }
            AddWorkTypeNames(process.WorkTypeNames());
        }
        /// <summary>
        /// 设备通道（L1）：车间内所有<b>持有 <c>Bill_Production</c> 的 Thing</b> → 设备 defName
        /// → 反查工序行（<see cref="WorkshopProcessDef.expectedDeviceDefNames"/> 与
        /// <see cref="WorkshopTypeDef.deviceWorkTypeDefNames"/>）→ 工作类型<b>并集</b>。
        ///
        /// <para><b>用户裁决</b>：设备进出车间<b>也要</b>触发，<b>不要求先开工单</b>。因此这里的
        /// 口径是「车间里存在该设备」而非「该设备当前有工单」—— 故用设备清单
        /// （<see cref="WorkshopStats.ScanDevices"/>，与设备页/概览同源）判定"在车间内"，
        /// 再用 <c>IBillGiver</c> 过滤出"能出活的工位"。</para>
        ///
        /// <para><b>并集为空 ⇒ 什么都不做</b>（不写、不降档），这是"降级到现有按类型映射"的语义：
        /// 车间类型没声明 <c>deviceWorkTypeDefNames</c>、且没有任何工序行以该设备为锚点时，
        /// 设备通道不贡献任何保留项。</para>
        ///
        /// <para><b>性能</b>：仅在同步事件里跑一次，O(设备数 × 工序行数)；工序行集合在
        /// <see cref="WorkshopProcessCatalog"/> 内按 defName 建表，避免逐设备全表扫。
        /// 绝不进 tick / 绘制路径。</para>
        /// </summary>
        private void CollectDeviceChannelWorkTypes(Zone_Workshop zone, WorkshopTypeDef typeDef)
        {
            // 车间类型显式声明的设备通道（可选字段）：非空即并入。
            if (typeDef != null)
            {
                AddWorkTypeNames(typeDef.DeviceWorkTypeDefNames());
            }
            if (zone == null || zone.Map == null)
            {
                return;
            }
            List<Thing> devices = WorkshopStats.ScanDevices(zone);
            if (devices == null || devices.Count == 0)
            {
                return;
            }
            for (int i = 0; i < devices.Count; i++)
            {
                Thing device = devices[i];
                if (device == null || device.Destroyed || device.def == null)
                {
                    continue;
                }
                // 口径（用户裁决）：只认"能出活的工位"= 实现 IBillGiver 且持有 BillStack 的设备，
                // **不要求当前已有工单** —— 设备搬进车间就该被算进来（Bill_Production 是 BillStack
                // 里可能出现的工单类型；此处只借 IBillGiver 判定"这是台机器"）。
                // 自家传送带/农机等非 IBillGiver 设备由 deviceWorkTypeDefNames 显式声明覆盖。
                if (!(device is IBillGiver giver) || giver.BillStack == null)
                {
                    continue;
                }
                AddProcessWorkTypesForDevice(device.def.defName);
            }
        }

        /// <summary>设备 defName → 工序行 → 工作类型并集（每工序行只处理一次）。</summary>
        private void AddProcessWorkTypesForDevice(string deviceDefName)
        {
            if (string.IsNullOrEmpty(deviceDefName))
            {
                return;
            }
            List<WorkshopProcessDef> rows = WorkshopProcessCatalog.ProcessesForDevice(deviceDefName);
            for (int i = 0; i < rows.Count; i++)
            {
                WorkshopProcessDef row = rows[i];
                if (row == null || row.defName == null || !deviceProcessSeen.Add(row.defName))
                {
                    continue;
                }
                AddProcessWorkTypes(row);
            }
        }

        // ---- 降档执行（EXC-2026-015 的唯一写入点）-------------------------------------


        /// <summary>
        /// 对单个 pawn 执行「车间专属工位」：**车间相关工作 → 最高档 1**（只提权，不降任何东西）。
        ///
        /// <para>⚠ <b>CHG-20261005-NODEMOTE</b>：本方法原名 <c>DemoteOtherWorkTypes</c> ——
        /// 因为它在提权之后还会把"其余已勾选工作"降到最低档 4（原 ② 段）。玩家 2026-10-05 裁定
        /// 「不需要降档，只需要人员移出车间后恢复原版面板、手动调整」⇒ **② 段已删除**，
        /// 方法随之改名以反映真实职责（只对齐保留集，不降档）。</para>
        ///
        /// <para><b>为什么 ① 段必须保留</b>：它是"车间工作优先"的**唯一全 scope 路径**
        /// （不依赖 <c>EnableWorkTypes</c> 的班次门控）⇒ 删掉会让车间优先退化成只对在班者生效。</para>
        ///
        /// <para><b>返回值口径（L4 状态行）</b>：<c>true</c> = 本次至少产生了一处写入（已对齐）；
        /// <c>false</c> = 无写入 —— 要么已是目标档（幂等），要么全被
        /// <c>WorkTypeIsDisabled</c> 守卫跳过（计为"跳过"）。</para>
        /// </summary>
        private bool AlignKeptWorkTypes(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead)
            {
                return false;
            }
            Pawn_WorkSettings settings = pawn.workSettings;
            if (settings == null)
            {
                return false;
            }
            settings.EnableAndInitializeIfNotAlreadyInitialized();

            // ① 保留集提到最高档 1（EXC-2026-013 口径：0 → 1 含重开、2/3/4 → 1）。
            int aligned = 0;
            bool skipped = false;
            PlaySettings play = Find.PlaySettings;
            bool prioritiesEffective = play != null && play.useWorkPriorities;
            for (int i = 0; i < workSyncTypes.Count; i++)
            {
                WorkTypeDef type = workSyncTypes[i];
                if (type == null)
                {
                    continue;
                }
                // 原版守卫：disabled（技能缺失/背景禁用）类型写非 0 会 Log.Error。
                if (pawn.WorkTypeIsDisabled(type))
                {
                    skipped = true;
                    continue;
                }
                if (!prioritiesEffective)
                {
                    // 简单模式：档位不被引擎读取 ⇒ 只保证启用（不做无意义写入）。
                    if (settings.GetPriority(type) <= 0)
                    {
                        settings.SetPriority(type, Pawn_WorkSettings.DefaultPriority);
                        aligned++;
                    }
                    continue;
                }
                if (settings.GetPriority(type) != TopWorkPriority)
                {
                    settings.SetPriority(type, TopWorkPriority);
                    aligned++;
                }
            }

            // ② 【已删除】CHG-20261005-NODEMOTE：原为「其余已勾选工作降到最低档 4」。
            //    玩家裁定移除降档 ⇒ 本段连同 DemoteNonKeptWorkTypes 一并删除。
            //    效果：其余工作保持玩家原来的档位；车间工作仍是最高档 1（上面的 ① 段）。
            if (skipped)
            {
                workSkipPawns++;
            }
            return aligned > 0;
        }
        // ⚠ CHG-20261005-NODEMOTE（2026-10-05 玩家批准 A 方案）：**本方法已删除**。
        //
        // 原为「其余已勾选工作降到最低档 4」的核心循环（EXC-2026-015 的唯一写入点，
        // 含三条判定：属保留集跳过 / WorkTypeIsDisabled 跳过 / 未勾选跳过，以及三餐类型豁免）。
        //
        // 删除理由（玩家裁定）：「不需要这个，只需要人员被移除车间体系后恢复显示原版工作面板，
        // 玩家可以手动进行调整，避免多余」⇒ 降档通道整体移除，其余工作保持玩家原档位。
        //
        // **保留的相关资产（勿误删）**：
        //   · RestoreAllDemotedWork / RestorePawnDemotions —— **旧档一次性迁移专用**
        //     （由 WorkshopManageView.Refresh 调用，把旧档里被压到 4 的项还原回去）；
        //   · ChronicleGameComponent.DemotionLedger 字段 —— 保留不删（不动 Scribe 键结构），仅停止写入；
        //   · CurrentDemotionLedger() —— 迁移路径仍需要它。
        //
        // ✔ 核对结果（2026-10-05）：`DemotableWorkTypes()` **仍有调用方**
        //   （MapComponent_WorkshopManager.WorkTakeover.cs:191，接管候选集构建）
        //   ⇒ **不是死代码、勿删**。（首版注释误记为"已无调用方"，此处更正。）
        // ---- 一键全解除（只还原台账里的项，绝不碰玩家自己改的档位）---------------------

        /// <summary>
        /// 最近一次 <see cref="RestoreAllDemotedWork"/> 实际还原的项数（**运行态**，不入档）。
        ///
        /// <para>⚠ CHG-20261005-NODEMOTE：原先供「一键全解除」的 UI 提示读取；该按钮已随降档通道删除
        /// ⇒ 现在**只有迁移路径会写它**，暂无读取者。保留是因为它是迁移是否真的还原了东西的
        /// 唯一可观测字段（排障时可在 Watch 里看），**不是死代码但有此风险认知**。</para>
        /// </summary>
        public int LastRestoreCount { get; private set; }

        /// <summary>
        /// 把 PC 降过的档位还原回玩家原来的值。
        ///
        /// <para>⚠ <b>CHG-20261005-NODEMOTE（2026-10-05 玩家批准 A 方案）后的唯一用途 =
        /// 旧档一次性迁移</b>：降档通道与「一键全解除」按钮均已移除，本方法不再有 UI 出口，
        /// 只由 <c>WorkshopManageView.Refresh</c> 在每次开面板时调用一次 ——
        /// 幂等且自清理（还原后即从台账移除；台账为空则立刻返回）⇒ 不需要"已迁移"标记。
        /// 目的：防旧档里"被压到 4 档"的项永久卡住（原出口已消失）。</para>
        ///
        /// <para><b>只认台账，绝不越界</b>：遍历台账 (<b>pawn, workTypeDefName, 原档位</b>)，
        /// 台账里没有的项一律不碰 —— 玩家自己排的档位 100% 安全。这是 EXC-2026-015 的核心承诺，
        /// 也是"绝不改动 PC 未动过的项"的实现方式。</para>
        ///
        /// <para><b>两道自愈闸门（防止把 A 档记录用到 B 档）</b>：</para>
        /// <list type="number">
        /// <item><b>pawn 必须仍然有效</b>：解析不到 / 已死 / 已离殖民地 ⇒ 丢弃该 pawn 全部记录
        /// （不还原 —— 没有可还原的对象）。新 pawn 复用旧 ID 的情形由此被挡住。</item>
        /// <item><b>当前值必须仍是 PC 写的最低档</b>：若玩家在 PC 降档之后手动把它改成了 1/2/3
        /// ⇒ <b>跳过还原</b>（玩家的新意图优先），但仍丢弃台账记录（PC 已不再拥有这一项）。
        /// 若当前是 0（玩家关掉了）⇒ 同样跳过，绝不"顺手打开"。</item>
        /// </list>
        ///
        /// <para><b>失败隔离</b>：单个 pawn 出异常不影响其余（逐 pawn try/catch，记一次诊断）。</para>
        /// </summary>
        public int RestoreAllDemotedWork()
        {
            WorkshopWorkDemotionLedger ledger = CurrentDemotionLedger();
            LastRestoreCount = 0;
            if (ledger == null || ledger.pawns == null || ledger.pawns.Count == 0)
            {
                return 0;
            }
            // 先取快照再逐条删：边遍历边改集合会漏项。
            List<WorkshopWorkDemotionLedger.PawnRecord> snapshot =
                new List<WorkshopWorkDemotionLedger.PawnRecord>(ledger.pawns);
            for (int i = 0; i < snapshot.Count; i++)
            {
                WorkshopWorkDemotionLedger.PawnRecord rec = snapshot[i];
                if (rec == null || rec.entries == null)
                {
                    continue;
                }
                try
                {
                    RestorePawnDemotions(rec);
                }
                catch (System.Exception ex)
                {
                    Log.Warning("[PersonalChronicle] Restore workshop work demotion failed (pawn "
                        + rec.pawnId + "): " + ex.Message);
                }
            }
            return LastRestoreCount;
        }
        /// <summary>还原单个 pawn 的全部记录；返回实际还原项数。失效记录一律丢弃。</summary>
        private int RestorePawnDemotions(WorkshopWorkDemotionLedger.PawnRecord rec)
        {
            int pawnId = rec.pawnId;
            Pawn pawn = FindPawnByStableId(pawnId);
            WorkshopWorkDemotionLedger ledger = CurrentDemotionLedger();
            if (pawn == null)
            {
                // 闸门①：pawn 已死 / 离殖民地 / ID 被新 pawn 复用而无法确认 ⇒ 记录失效，丢弃。
                // 绝不"按 ID 硬写"—— 那正是"把 A 档记录应用到 B 档"的成因。
                ledger?.DiscardPawn(pawnId);
                return 0;
            }
            Pawn_WorkSettings settings = pawn.workSettings;
            int restored = 0;
            List<WorkshopWorkDemotionLedger.Entry> entries =
                new List<WorkshopWorkDemotionLedger.Entry>(rec.entries);
            for (int i = 0; i < entries.Count; i++)
            {
                WorkshopWorkDemotionLedger.Entry e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.workTypeDefName))
                {
                    continue;
                }
                WorkTypeDef type = ResolveWorkType(e.workTypeDefName);
                if (type == null)
                {
                    // Def 已被移除（Mod 卸载等）⇒ 无法还原，丢弃记录。
                    ledger?.Discard(pawnId, e.workTypeDefName);
                    continue;
                }
                // 无 workSettings / 类型已被禁用 ⇒ 无法安全写，丢弃记录（不猜）。
                if (settings == null || pawn.WorkTypeIsDisabled(type))
                {
                    ledger?.Discard(pawnId, e.workTypeDefName);
                    continue;
                }
                // 闸门②：玩家已改动（不再是 PC 写的最低档）⇒ 不还原，保留玩家的新值。
                if (settings.GetPriority(type) != Pawn_WorkSettings.LowestPriority)
                {
                    ledger?.Discard(pawnId, e.workTypeDefName);
                    continue;
                }
                int target = e.originalPriority;
                // 越界防护：合法区间 1..4；旧档 / 手工改档可能写出越界值。
                if (target < 1 || target > Pawn_WorkSettings.LowestPriority)
                {
                    ledger?.Discard(pawnId, e.workTypeDefName);
                    continue;
                }
                settings.SetPriority(type, target);
                restored++;
                ledger?.Discard(pawnId, e.workTypeDefName);
            }
            LastRestoreCount += restored;
            return restored;
        }
        // ---- 台账 / pawn 解析 ---------------------------------------------------

        /// <summary>取当前 colonies 的降档台账（无 Game 时返回 null，调用方判空）。</summary>
        private static WorkshopWorkDemotionLedger CurrentDemotionLedger()
        {
            Game game = Current.Game;
            if (game == null)
            {
                return null;
            }
            ChronicleGameComponent component = game.GetComponent<ChronicleGameComponent>();
            return component != null ? component.DemotionLedger : null;
        }

        /// <summary>
        /// pawn 稳定标识 → 活着的玩家 pawn。找不到 / 已死 / 非玩家 faction ⇒ null。
        ///
        /// <para>用 <see cref="PawnsFinder.AllMapsWorldAndTemporary_Alive"/>（同
        /// <c>EnableManualWorkPriorities</c> 的取法），刻意<b>不</b>缓存：还原是低频事件，
        /// 缓存只会引入"用了旧 pawn 引用"的陈旧风险。</para>
        /// </summary>
        private static Pawn FindPawnByStableId(int pawnId)
        {
            if (pawnId <= 0)
            {
                return null;
            }
            List<Pawn> all = PawnsFinder.AllMapsWorldAndTemporary_Alive;
            if (all == null)
            {
                return null;
            }
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p == null || p.Destroyed || p.Dead || p.Faction != Faction.OfPlayer)
                {
                    continue;
                }
                if (p.thingIDNumber == pawnId)
                {
                    return p;
                }
            }
            return null;
        }
        /// <summary>本轮同步里被 <c>WorkTypeIsDisabled</c> 挡住的人数（内部累加器）。</summary>
        private int workSkipPawns;

        /// <summary>
        /// 「车间专属工位」同步入口（<b>事件驱动，唯一调用方是 <see cref="SyncVanillaWorkSettings"/></b>）。
        ///
        /// <para><b>为什么仍走既有 SyncVanillaWorkSettings 而不新增 tick 钩子</b>：EXC-012 / EXC-013
        /// 已把「写入原版玩家级状态」收敛到排班/名册/开面板对账三个事件上；本通道只是在这三个
        /// 事件里<b>多做一件事</b>，不新增触发面 ⇒ 不违反"禁 tick / 绘制路径 / WorkGiver 内写"。</para>
        ///
        /// <para><b>顺序很重要</b>：① 先 Enable（EXC-012/013，可能把 0 档打开）→ ② 再提保留集到 1
        /// → ③ 最后降其余。<b>顺序反了会把刚 Enable 的类型又降到 4</b>。</para>
        /// </summary>
        private void SyncWorkStationForZone(Zone_Workshop zone)
        {
            workSkipPawns = 0;
            PlaySettings play = Find.PlaySettings;
            bool needsManual = play == null || !play.useWorkPriorities;
            if (zone == null)
            {
                // ⚠ CHG-20261005-NODEMOTE-AUDIT：原先此分支还填 ledgerEntries（降档台账项数），
                //   但该字段自降档移除后**已无任何读取者**（读数行同步删掉了那一项）⇒ 死赋值，已删。
                LastWorkStationStatus = new WorkStationStatus
                {
                    needsManualPriorities = needsManual
                };
                return;
            }
            // ⚠ CHG-20261005-NODEMOTE（2026-10-05 玩家批准「A 方案」）：**降档通道已移除**。
            //
            // 原实现此处对成员执行 DemoteOtherWorkTypes（车间相关→1、其余已勾选→4），
            // 即 EXC-2026-015 授权的"自动降档"。玩家裁定：「不需要这个，只需要人员被移除车间
            // 体系后恢复原版工作面板，手动调整，避免多余」⇒ 删链。
            //
            // **功能不丢的取证依据**（删前逐条核对，两处都不依赖降档）：
            //   · 「0 档补勾选 + 映射类型提到最高档 1」= EnableWorkTypes（SyncVanillaWorkSettings）；
            //   · 「在班者被离班链写 0 后补回」= RestoreOnShiftWorkTypes（tick 每小时，只补 0）。
            // ⇒ 删后"车间相关工作优先"仍成立；唯一失去的是"其余工作被压到最低档 4"这条强约束，
            //   而那正是玩家要移除的多余部分。
            //
            // 台账字段（ChronicleGameComponent.DemotionLedger）**保留不删**（不动 Scribe 键结构），
            // 仅停止写入；RestoreAllDemotedWork 保留，专供**旧档一次性迁移**（见其注释）。
            CollectKeptWorkTypes(zone);
            // ⚠ **修正说明（首次实施时过度删除，已恢复）**：原打算整段删掉调用循环，但
            //   DemoteOtherWorkTypes 的 ① 段并非降档 —— 它是「**映射类型提到最高档 1**」
            //   （车间工作优先的唯一全 scope 路径，且不依赖 EnableWorkTypes 的班次门控）
            //   ⇒ 整段删除会让"车间优先"退化成只对在班者生效，与既有设计不符。
            //   故**恢复调用**，仅剥掉 ② 段（降档），方法改名为 AlignKeptWorkTypes。
            int aligned = 0;
            for (int i = 0; i < workSyncScope.Count; i++)
            {
                if (AlignKeptWorkTypes(workSyncScope[i]))
                {
                    aligned++;
                }
            }
            LastWorkStationStatus = new WorkStationStatus
            {
                alignedPawns = aligned,
                skippedPawns = workSkipPawns,
                scopedPawns = workSyncScope.Count,
                // ledgerEntries 不再填（降档台账已停用）⇒ 读数行同步不显示该项。
                keptWorkTypes = workSyncTypes.Count,
                needsManualPriorities = needsManual
            };
        }

        /// <summary>台账剪枝谓词：pawn 仍活着、仍属玩家、且 workSettings 可用。</summary>
        private static bool IsPawnStillRelevant(int pawnId)
        {
            Pawn pawn = FindPawnByStableId(pawnId);
            return pawn != null && pawn.workSettings != null;
        }

        // ---- L1 脏标记消费（写入发生在 tick 之外）-------------------------------------

        /// <summary>
        /// 若设备/工单指纹已变化，则对本图各车间重放一次「车间专属工位」同步。
        ///
        /// <para><b>为什么写入不在 tick 里</b>：<see cref="MapComponent_WorkshopManager"/>
        /// 的 <c>MapComponentTick</c> 只做<b>只读</b>指纹比对并置 <c>deviceChannelDirty</c>；
        /// 本方法是<b>唯一</b>消费该标记并真正调 <c>SetPriority</c> 的地方，调用点是
        /// <b>玩家可见的事件</b>（打开车间管理面板 / 排班变更），与 EXC-2026-012 / 013
        /// 的触发纪律一致。tick 内<b>零</b> <c>SetPriority</c> 调用（F6）。</para>
        ///
        /// <para><b>调用方</b>：<c>WorkshopManageView.Refresh</c>（开面板/刷新）与
        /// <c>OnRosterChanged</c>（排班类变更）。二者本来就会调
        /// <see cref="SyncVanillaWorkSettings"/>，本方法只是在其之前把"设备变了"这件事
        /// 转成一次显式重放，<b>不新增触发面</b>。</para>
        ///
        /// <para><b>幂等</b>：无条件重放是安全的 —— 已是目标档即跳过；台账首记优先。</para>
        /// </summary>
        public void SyncWorkStationIfDirty()
        {
            if (!deviceChannelDirty)
            {
                return;
            }
            deviceChannelDirty = false;
            List<Zone_Workshop> zones = AllWorkshopZones();
            for (int i = 0; i < zones.Count; i++)
            {
                SyncVanillaWorkSettings(zones[i]);
            }
        }

        /// <summary>设备/工单指纹是否处于"已变化未消费"状态（诊断 / 单测用）。</summary>
        public bool HasPendingDeviceChannelChange
        {
            get { return deviceChannelDirty; }
        }

    }
}