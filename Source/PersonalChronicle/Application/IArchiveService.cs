using System.Collections.Generic;
using PersonalChronicle.Api;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Service surface consumed by the UI layer. Extends the read-only
    /// <see cref="IArchiveQueryService"/> with the write entry points; the
    /// implementation satisfies both so an external mod can depend only on the
    /// narrower query contract.
    ///
    /// All read queries return read-only snapshots or live read-only views;
    /// write entry points are the only way the capture layer feeds the archive.
    ///
    /// v0.2 methods (GetAllRecords / GetActiveRecords / GetArchivedRecords /
    /// GetEventsFor / OnColonistJoined / OnPawnDied) are preserved unchanged —
    /// v2.1 additions are appended, never breaking existing signatures.
    /// </summary>
    public interface IArchiveService : IArchiveQueryService
    {
        // ---- v0.2 write surface (unchanged) ----

        void OnColonistJoined(Pawn pawn);
        void OnColonistJoined(Pawn pawn, PawnRole role);
        void OnPawnDied(Pawn pawn, string deathCauseKey);

        // ---- read queries are inherited from IArchiveQueryService ----
        // (GetRecentEvents / GetAllEvents / GetCategoryBehavior / GetWorkPriorities /
        // GetWorkTimeStats / GetSkillArchive / GetProductionSummary / GetLiveLocation /
        // GetCurrentHolder / GetActiveBattle / GetProductionEvents / live stats /
        // GetHomeViewMode are all declared on IArchiveQueryService and surfaced here
        // through inheritance — do NOT re-declare to avoid CS0108 hide warnings.)

        // ---- v2.1 Phase 1 capture write entries ----

        /// <summary>
        /// Records a colonist death, optionally linking the killer's weapon as a
        /// Subject edge. <paramref name="weapon"/> may be null (unarmed kill) —
        /// behavior then matches the v0.2 two-argument overload exactly.
        ///
        /// v0.3: <paramref name="extraParams"/> carries language-independent
        /// identity snapshots to be merged into the event's Params, e.g.
        /// ["killer"] = killer pawn LabelShort. Both <paramref name="weapon"/>
        /// and <paramref name="extraParams"/> are optional — existing call sites
        /// keep compiling unchanged.
        /// </summary>
        void OnPawnDied(Pawn pawn, string deathCauseKey, Thing weapon = null, Dictionary<string, string> extraParams = null);

        /// <summary>
        /// v3.1 P2: same as the four-argument death write, plus a live killer
        /// pawn for Subject edges (kill graph + battle participants).
        /// </summary>
        void OnPawnDied(Pawn pawn, string deathCauseKey, Thing weapon, Dictionary<string, string> extraParams, Pawn killer);

        /// <summary>
        /// v3.1 P2: records a kill by a chronicle colonist of a non-archived
        /// victim (e.g. raider). Creates a Death-type event with Primary=victim
        /// and Subject=killer (+ weapon/battle), so GetEventsFor(killer) lists it.
        /// When the victim is itself a chronicle colonist, use
        /// <see cref="OnPawnDied"/> instead (that path already adds the killer edge).
        /// </summary>
        /// <summary>
        /// assistLookup: optional, ordered by damage dealt (descending). The top
        /// entry is treated as the primary kill contributor when it differs from
        /// the finishing instigator, who is then recorded as an assist.
        /// </summary>
        void OnKillRecorded(Pawn killer, Pawn victim, Thing weapon = null, List<Pawn> assistLookup = null);

        /// <summary>
        /// v6.8 重载：在击杀记录的同时累加个人战斗维度（生涯伤害 / 战斗风格近战·远程）。
        /// <paramref name="finishingDamage"/> 为补刀时的 DamageInfo.Amount（作为生涯伤害近似累加）；
        /// <paramref name="isMelee"/> 由击杀武器 PrimaryVerb.IsMelee 推断（true=近战击杀 / false=远程击杀）。
        /// 非 chronicle 殖民者击杀（killer 为 null 或未知凶手桶）不累加，与基础 OnKillRecorded 幂等路径一致。
        /// </summary>
        void OnKillRecorded(Pawn killer, Pawn victim, Thing weapon, List<Pawn> assistLookup, float finishingDamage, bool isMelee);

        /// <summary>
        /// v3.1 P3: significant social relation formed or ended (lover/spouse/family).
        /// Writes a Social ChronicleEvent + updates PawnObject.Relations snapshots
        /// for both parties when they are chronicle-relevant.
        /// </summary>
        void OnRelationChanged(Pawn a, Pawn b, PawnRelationDef relationDef, bool formed);

        /// <summary>
        /// Records a crafted/built thing (weapon, apparel, equipment...).
        /// <paramref name="product"/> is the finished Thing; the worker becomes a
        /// Subject edge when it is a chronicle-relevant pawn.
        /// </summary>
        void OnThingCrafted(Thing product, Pawn worker);

        /// <summary>
        /// P1 CAREER-001 BE-005：记录一次"制造产出物品"职业事实（ItemProduced）。
        /// 仅捕获事实（人物/时间/物品Def/品质/相关技能），不含任何评价字段。
        /// <paramref name="product"/> 为成品 Thing；<paramref name="recipe"/> 为配方
        /// （可为 null，此时技能留空）；<paramref name="worker"/> 为制造者。
        /// 调用方须保证 <paramref name="worker"/> 为玩家派系 chronicle 相关 humanlike，
        /// 否则本方法直接返回（不重复记录、不污染）。
        /// </summary>
        void RecordCareerProduced(Thing product, RecipeDef recipe, Pawn worker);

        /// <summary>
        /// P1 CAREER-005：记录一次"完成一座建筑"职业事实（ConstructionCompleted）。
        /// 由 <c>Patch_GenConstruct</c>（Postfix on Frame.CompleteConstruction）调用；
        /// <paramref name="builtStableId"/> = builtDef.defName + ":" + frame.thingIDNumber
        /// （与 OnThingBuilt 同键）。只记事实，建筑不入装备归档（与 OnThingBuilt 分工）。
        /// </summary>
        void RecordCareerConstruction(Pawn worker, ThingDef builtDef, string builtStableId, long tick);

        /// <summary>
        /// P1 CAREER-005：记录一次"完成一项研究"职业事实（ResearchCompleted）。
        /// 由 <c>Patch_ResearchFinishProject</c>（Postfix on ResearchManager.FinishProject）
        /// 调用；同项目防重复（只记一次）。研究者为 null（dev 强制完成等）时由调用方过滤。
        /// </summary>
        void RecordCareerResearch(Pawn researcher, string researchDefName, long tick);

        /// <summary>
        /// P1 CAREER-005：WorkCompleted 量子派生。每累计 2500 工作tick（=游戏 1 小时）
        /// 记 1 条 WorkCompleted 事实（对齐 UI 每事件=1h 语义）。无状态推导
        /// （TotalWorkTicks / quantum 与 RecordCountByType 差额），幂等、零新增 Save schema。
        /// 由工时采样器每次采样后调用。
        /// </summary>
        void RecordWorkCompletedQuantum(string pawnStableId, long tick);

        /// <summary>
        /// V2.1 职业 XP 采集（原版链路）：记录一次原版技能经验注入 → 职业技能 XP。
        /// 由 <c>Patch_SkillRecord_Learn</c>（Postfix on SkillRecord.Learn）调用。
        /// <paramref name="actualXp"/> 为已含 LearnRateFactor（Passion/学习速率）的增量；
        /// <paramref name="vanillaSkillName"/> 为原版 SkillDef.defName（经
        /// ProfessionalSkillDef.sourceSkills 匹配职业技能）。只累加、永不减少
        /// （无每日上限、无衰退）。调用方须保证 <paramref name="pawn"/> 为玩家派系
        /// chronicle 相关 humanlike。
        /// </summary>
        /// <param name="countPractice">
        /// v2.3 整件结算：制造完成一次性注入（Patch_BillProduction → pawn.skills.Learn）时传
        /// <c>false</c>，跳过 practice 计数（制造实践由 <see cref="RecordCraftCompleted"/> 独家
        /// 记录，避免一次完成重复计实践）；其他技能来源保持 <c>true</c>。
        /// </param>
        void RecordSkillLearn(Pawn pawn, string vanillaSkillName, float actualXp, long tick, bool countPractice = true);

        /// <summary>
        /// V2.3 制造完成实践统计（v2.1 的自造 XP 结算已由「整件结算制」取代）：
        /// 工作台完成一次制造迭代 → recipe.workSkill 映射的职业技能记一次实践
        /// （practiceCount / practiceCountBySkill / lastPracticeTick）。
        /// 由 <c>Patch_BillProduction</c>（Postfix on Bill_Production.Notify_IterationCompleted，
        /// 所有工作台/简易制作点制造完成的统一回调）调用。制造 XP 走整件结算——
        /// Patch_BillProduction 先 Flush <c>CraftXpSettlement</c> 缓存 → 原生
        /// pawn.skills.Learn(workSkill, total) 一次性注入 → 原版公式自动生效 →
        /// <see cref="RecordSkillLearn"/> 分流职业 XP。只累加、永不减少（append-only 铁律）。
        /// 调用方须保证 <paramref name="worker"/> 为玩家派系 chronicle 相关 humanlike。
        /// </summary>
        void RecordCraftCompleted(Pawn worker, RecipeDef recipe, long tick);

        /// <summary>
        /// v1.1.4 损耗宫格：记录该人物消耗了一份可摄入物（进食/饮用/用药/成瘾品），
        /// 按 <c>ThingDef.BaseMarketValue</c> 计价累加到 <see cref="ConsumptionAccumulator"/>。
        /// 来自 <c>Thing.Ingested</c> 捕获（高频，不写事件流）。<paramref name="eater"/> 需为
        /// 玩家派系、chronicle 相关的 humanlike 殖民者。
        /// </summary>
        void OnThingConsumed(Pawn eater, Thing food);

        /// <summary>
        /// v1.1.4 劳模住所/工坊检测（方案 A）：记录该殖民者在一台工作台完成了一次制造迭代。
        /// 来源 <c>Bill_Production.Notify_IterationCompleted</c> 捕获（低频，不写事件流），
        /// 在数据层聚合 <see cref="WorkplaceSnapshot"/>（Building_WorkTable.def.defName 稳定键
        /// + 工坊所在房间角色 RoomRoleDef.defName + UseCount/LastUsedTick）。
        /// 只存 defName 稳定键 —— 玩家手动改名后 UI 实时解析 <c>DefDatabase.LabelCap</c> 正确显示新名。
        /// </summary>
        void OnWorkplaceUsed(Pawn worker, Building_WorkTable workbench);

        /// <summary>
        /// v1.1.6 切换档案记录的「当前工作场所」：把该殖民者的 WorkplaceSnapshot 改指选中的工坊
        /// （不实际指挥殖民者做工，仅改档案记录，安全可逆）。供六宫格工坊卡弹窗「设为当前」。
        /// </summary>
        void SetCurrentWorkplace(string pawnStableId, string buildingDefName, string buildingStableId,
            string roomRoleDefName, int mapIndex, Verse.IntVec3 cell);

        /// <summary>
        /// v1.1.4 建筑别名（旧 per-pawn 兼容）：为指定殖民者的工作场所设置自定义别名
        /// （<see cref="WorkplaceSnapshot.CustomName"/>）。新语义已由
        /// <see cref="SetBuildingAlias"/>（工坊实例全局共享）取代；本方法保留兼容。
        /// </summary>
        void SetWorkplaceCustomName(string pawnStableId, string customName);

        /// <summary>
        /// v1.1.4 工坊实例全局别名：key = <c>defName:thingIDNumber</c>（BuildingStableId）。
        /// 设置/清除某台工坊的自定义名，任何使用该工坊的劳模档案共享显示此名。
        /// <paramref name="customName"/> 为 null/空时清除别名、回落默认名（LabelCap 实时解析）。
        /// </summary>
        void SetBuildingAlias(string buildingStableId, string customName);

        /// <summary>
        /// v1.1.4 房间类型别名：key = RoomRoleDef.defName（如 Bedroom）。
        /// 设置/清除某房间类型的自定义显示名（如「员工宿舍」），mod 内部展示层覆盖，
        /// 不改原版 Def。<paramref name="customName"/> 为 null/空时清除别名、回落 RoomRoleDef.LabelCap。
        /// </summary>
        void SetRoomRoleAlias(string roomRoleDefName, string customName);

        /// <summary>
        /// v1.1.4 房间级改名（集中于 ITab，无需家具）：key = <c>pawnStableId:RoomRoleDefName</c>。
        /// 设置「某殖民者的某类型房间」的自定义显示名（如「Dweeb 的卧室」→「员工宿舍」），
        /// 粒度到个人+类型，互不干扰。原版显示由 <c>Patch_RoomRoleLabel</c> 按房间拥有者查表覆盖。
        /// </summary>
        void SetRoomName(string pawnStableId, string roomRoleDefName, string customName);

        /// <summary>
        /// v1.1.4 房间级自定义名读取（null = 未改名）。
        /// </summary>
        string GetRoomName(string pawnStableId, string roomRoleDefName);

        /// <summary>
        /// v1.1.4 房间级类型名替换（底层 Role 不变）：key = <c>pawnStableId:RoomRoleDefName</c>。
        /// 设置「某殖民者的某类型房间」的类型显示名（如「工作间」→「工坊类型」），
        /// 粒度到个人+类型，互不干扰。仅 UI 替换，游戏逻辑不受影响。
        /// </summary>
        void SetRoomTypeName(string pawnStableId, string roomRoleDefName, string customName);

        /// <summary>
        /// v1.1.4 房间级类型名替换读取（null = 未替换）。
        /// </summary>
        string GetRoomTypeName(string pawnStableId, string roomRoleDefName);

        /// <summary>
        /// Records a finished construction. <paramref name="builtDef"/> is the
        /// completed building's ThingDef; <paramref name="builtStableId"/> is the
        /// caller-supplied stable identity (defName:thingIDNumber of the frame).
        /// </summary>
        void OnThingBuilt(ThingDef builtDef, string builtStableId, Pawn worker);

        /// <summary>
        /// v4.9: records an equipment thing's decommission (退役仪式) — a read-only
        /// "death record" captured at destroy time. Never prevents the destroy.
        /// <paramref name="lastHolder"/> is optional (may be null when the destroy
        /// has no clear holder context, e.g. deterioration in storage).
        /// </summary>
        void OnThingDestroyed(Thing thing, Pawn lastHolder = null);

        /// <summary>
        /// Records a battle-grade incident start. The caller (capture layer) is
        /// responsible for the data-driven filter (IncidentBattleExtension);
        /// this service only persists the fact.
        /// </summary>
        void OnBattleStarted(IncidentDef incidentDef);

        /// <summary>
        /// v4.11 P0: links the freshly-spawned raid Lord(s) on the map to an active
        /// battle and snapshots the raid force size (<see cref="BattleObject.RaidCount"/>)
        /// plus the runtime <see cref="BattleObject.RemainingRaidCount"/> countdown.
        /// Called from <see cref="OnBattleStarted"/> once the BattleObject exists and
        /// the raid workers have already generated their Lords (IncidentWorker.TryExecute
        /// calls TryExecuteWorker synchronously before its postfix runs).
        /// </summary>
        void LinkRaidLords(BattleObject battle);

        /// <summary>
        /// v4.11 P0: one raid pawn left the map (death / capture / exit / downed) for
        /// the Lord identified by <paramref name="lordLoadId"/>. <paramref name="remainingPawns"/>
        /// is the Lord's authoritative <c>ownedPawns.Count</c> after the loss (read in
        /// the capture patch, since Notify_PawnLost already removed the pawn). When it
        /// reaches zero the battle is finalized (EndTick written). Called from the
        /// Lord.Notify_PawnLost capture patch. No-op for Lords not linked to any battle.
        /// </summary>
        void OnRaidPawnGone(int lordLoadId, int remainingPawns);

        // ---- persisted state (write side) ----
        // Read side of the home view mode (GetHomeViewMode) and all live stats are
        // already declared on IArchiveQueryService; only the write entry remains here.

        /// <summary>
        /// v4.0 home view mode persisted in the game component.
        /// </summary>
        void SetHomeViewMode(int mode);

        // ---- v4.1 unified event bridge (legacy ↔ IArchiveEventSink) ----

        /// <summary>
        /// v4.1 bridge: records an event through the unified <see cref="IArchiveEventSink"/>
        /// contract. This is the single connection point between the rich legacy
        /// write methods (OnKillRecorded / OnPawnDied / ...) and the unified event
        /// sink — callers that already hold a fully-formed <see cref="ArchiveEventInput"/>
        /// (e.g. a third-party mod) should use <c>PersonalChronicleApi.TryGet</c> →
        /// <see cref="IArchiveEventSink.TryRecord"/> directly; this method is the
        /// equivalent on the legacy surface and delegates to it. Never throws on
        /// bad input; returns a <see cref="CaptureResult"/>.
        /// </summary>
        PersonalChronicle.Api.CaptureResult RecordEvent(PersonalChronicle.Api.ArchiveEventInput input);

        /// <summary>
        /// P5~P7 职业资格自动授予：判定 Qualified → 自动授予职称 → 回写 CareerEvent(TitleGranted)。
        /// 与勋章授勋同节流（reconcile 调用），内部自行做 recording gate、活读扫描、
        /// 去重写入 CareerData.GrantedTitles 与 MarkChanged。返回本次新授予职称的 (Pawn, TitleDef)。
        /// </summary>
        List<KeyValuePair<Pawn, PersonalChronicle.Domain.Qualification.ProfessionalTitleDef>> RunQualification(ChronicleGameComponent component);

        // ---- REQ-MVS-013 物品级产消读数（2026-09-20，CHG-MVS-ITEMFLOW-001）----

        /// <summary>
        /// 记一笔物品级产消（Capture 层只读钩子调用；失败静默降级，不影响原版流程）。
        /// <paramref name="production"/> true = 生产、false = 消费。
        /// </summary>
        void RecordItemFlow(string defName, int qty, bool production);

        /// <summary>
        /// 读某物品近两季的日均产/消速率（件/日）。<c>false</c> = 无账本/未建立
        /// （调用方画 `—`，**不得填 0**）；<c>true</c> 时数值可能为 0（真值：近期无产出）。
        /// </summary>
        bool TryGetItemFlowRate(string defName, bool production, out float perDay);

        /// <summary>
        /// 记一笔**原料流向**（2026-09-20，`REQ-MVS-018`）：material 被 product 的生产消耗了 qty 件。
        /// 由 <c>Patch_ItemFlowIngredients</c> 在 <c>GenRecipe.MakeRecipeProducts</c> 的只读
        /// Prefix 里调用（该路径覆盖全部配方；采掘/收获/建造耗材不经此路）。
        /// </summary>
        void RecordMaterialFlow(string material, string product, int qty);

        /// <summary>
        /// 读某物品作为**原料**时近两季流向各产物的件数（降序）。
        /// <c>false</c> = 无流向记录 —— ⚠ **不等于"没有流向"**（只覆盖配方消耗路径），
        /// 调用方必须按"无数据"处理（画 `—` / 退回其它判据），不得当作 0。
        /// </summary>
        bool TryGetMaterialFlows(string material,
            System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>> result);
    }
}
