using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed class EconomyInstitutionExtension : DefModExtension { public InstitutionKind kind; }
    public sealed partial class Building_EconomyInstitution : Building, IThingHolder
    {
        private ThingOwner<Thing> stock;
        public ThingOwner<Thing> Stock => stock ?? (stock = new ThingOwner<Thing>(this, false, LookMode.Deep));
        /// <summary>CHG-20261012-ECON-CONTRACTIZE · B2（用户裁决⑥「服务价迁体检中心建筑字段」）：
        /// 照护服务**单价**（minor；按次）。0 = **未挂牌** ⇒ 不收费、不结算（M5「未知不当零」：
        /// 调用方读到 null 必须拒绝，而不是拿 0 当免费）。字段落在建筑上，与既有
        /// <see cref="Building_EconomyStorage"/> 先例同型（public 字段 + Scribe_Values + 旧档缺键 = 0）。
        /// 仅对 <c>InstitutionKind.ExamCenter</c> 生效（其它机构不显示该 gizmo）。</summary>
        public long ServicePricePerUnitMinor;
        /// <summary>服务价调整步长（银 → minor）：±1 银/次。</summary>
        private const long ServicePriceStepMinor = 100L * 1L;
        /// <summary>服务价上限（minor）：10 000 银/次 —— 只做越界保护，不构成玩法约束。</summary>
        private const long ServicePriceMaximumMinor = 100L * 100000L;
        /// <summary>CHG-20261011-LABOR-VISUAL：**交付接收区**（独立于衣物货架 <see cref="Stock"/>）——
        /// 只接收已核验通过的真实产物，不是商店库存：容量独立、类型不限（任何可交付的 Thing）。
        /// 与衣物库存**共用 holder 会让"柜台收货"变成"上架在售"**（语义与权属都错）。</summary>
        private ThingOwner<Thing> delivery;
        public ThingOwner<Thing> DeliveryContainer => delivery ?? (delivery = new ThingOwner<Thing>(this, false, LookMode.Deep));
        /// <summary>交付接收容量（唯一来源；入库门、工作台 KPI 与预检共用）。</summary>
        public const int DeliveryCapacity = 256;
        /// <summary>该柜台还能接收的交付件数（**取物前预检**用；不发起任何 Reserve）。</summary>
        public int DeliveryRoom => DeliveryCapacity - DeliveryContainer.Count;
        /// <summary>接收一件已核验的交付物。**容量预检在前**；失败 ⇒ 调用方保留原位置与未提交量，不吞物。</summary>
        public bool AcceptDelivery(Thing thing)
        {
            if (thing == null || thing.Destroyed || thing.stackCount <= 0) return false;
            if (DeliveryContainer.Count >= DeliveryCapacity) return false;
            return DeliveryContainer.TryAddOrTransfer(thing, thing.stackCount, false) > 0;
        }
        internal int AcceptLaborDelivery(Thing thing, int quantity)
        {
            if (thing == null || thing.Destroyed || quantity <= 0 || DeliveryRoom <= 0) return 0;
            return DeliveryContainer.TryAddOrTransfer(thing, System.Math.Min(quantity, thing.stackCount), false);
        }
        public ThingOwner GetDirectlyHeldThings() => Stock;
        public void GetChildHolders(List<IThingHolder> holders)
        {
            // CA-17：**交付接收区必须作为子持有器挂进链里**（原版范式同 Pawn 的
            // `outChildren.Add(inventory)`）。修前只把两个容器**内**的物件的子持有器追加进去 ——
            // 普通木材这类"没有子持有器的物品"**根本不在递归清单里**（真实 API 探针复现），
            // 于是保存/销毁/清单遍历都会漏掉已验收入库的交付物。
            // ⚠ `ThingOwner` 本身**不实现 IThingHolder**（Verse.ThingOwner 只实现 IExposable/IList），
            // 因此需要一个真实持有器对象做适配（见 DeliveryHolderAdapter）。
            holders.Add(DeliveryHolderAdapter);
            ThingOwnerUtility.AppendThingHoldersFromThings(holders, Stock);
        }

        private DeliveryHolder deliveryHolder;
        private DeliveryHolder DeliveryHolderAdapter => deliveryHolder ?? (deliveryHolder = new DeliveryHolder(this));

        /// <summary>交付接收区的子持有器适配（CA-17）：把接收区挂进原版持有器递归链。</summary>
        private sealed class DeliveryHolder : IThingHolder
        {
            private readonly Building_EconomyInstitution owner;
            public DeliveryHolder(Building_EconomyInstitution owner) { this.owner = owner; }
            /// <summary>父持有器 = 柜台本体（原版递归遍历/销毁链路的一致性要求）。</summary>
            public IThingHolder ParentHolder => owner;
            public ThingOwner GetDirectlyHeldThings() => owner.DeliveryContainer;
            public void GetChildHolders(List<IThingHolder> outChildren)
                => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, owner.DeliveryContainer);
        }
        public override void ExposeData()
        {
            base.ExposeData(); Scribe_Deep.Look(ref stock, "economicStock", this);
            // 旧档缺 `servicePricePerUnit` ⇒ 0 = 未挂牌（显式传默认值：行为不依赖 default(long) 的隐含语义）。
            Scribe_Values.Look(ref ServicePricePerUnitMinor, "servicePricePerUnit", 0L);
            // CHG-20261011-LABOR-VISUAL：旧档缺该节点 ⇒ 空接收区（自然语义）。
            Scribe_Deep.Look(ref delivery, "laborDelivery", this);
            if (stock == null) stock = new ThingOwner<Thing>(this, false, LookMode.Deep);
            if (delivery == null) delivery = new ThingOwner<Thing>(this, false, LookMode.Deep);
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        { base.SpawnSetup(map, respawningAfterLoad); GameComponent_PawnEconomy.Current?.Register(this); }
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            GameComponent_PawnEconomy.Current?.Unregister(this);
            if (Spawned)
            {
                Stock.TryDropAll(Position, Map, ThingPlaceMode.Near);
                // 交付接收区**必须同样落地** —— 否则拆毁柜台会静默吞掉玩家已验收入库的真实产物（§9.2）。
                DeliveryContainer.TryDropAll(Position, Map, ThingPlaceMode.Near);
            }
            base.DeSpawn(mode);
        }
        /// <summary>
        /// CA-13/REQ-017：机构建筑**只读**显示本站当前在办的劳务流程（阶段 + 下一步）。
        /// 与账户页/合同纸同源（`ReadOpenFlowsAtStation`），**不写任何状态**、不发薪、不推进阶段；
        /// 显示上限 3 条（有界，避免刷屏），其余以计数呈现。
        /// </summary>
        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            var flows = GameComponent_PawnEconomy.Current?.ReadOpenFlowsAtStation(GetUniqueLoadID());
            if (flows == null || flows.Count == 0) return text;
            var sb = new System.Text.StringBuilder(text);
            sb.Append('\n').Append(EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Station.OpenCount", "StationOpenCount"))
                .Append(": ").Append(flows.Count);
            bool institutionBlocked = false;
            for (int i = 0; i < flows.Count && i < 3; i++)
            {
                var flow = flows[i];
                // 2026-10-12 实机反馈：新殖民地未完成一次存档往返时制度停在 Preparing，劳务站此前只说
                // 「制度尚未开放新的用工承诺」，玩家无从得知要做什么 ⇒ 记下这一条，收尾时补一行**可执行的下一步**。
                if (flow.ReasonCode == "InstitutionNotReady") institutionBlocked = true;
                sb.Append('\n').Append("· ").Append(LaborVisualText.StageLabel(flow.Stage));
                string next = LaborVisualText.NextActionLabel(flow.NextAction);
                if (!string.IsNullOrEmpty(next)) sb.Append(" · ").Append(next);
                if (flow.Hold != FlowHold.None)
                    sb.Append(" · ").Append(LaborVisualText.HoldLabel(flow.Hold))
                        .Append(" (").Append(LaborVisualText.ReasonLabel(flow.ReasonCode)).Append(')');
            }
            AppendInstitutionGateLine(sb, institutionBlocked);
            return sb.ToString();
        }

        /// <summary>制度门未开时，把「缺哪一步」写在**被挡住的地方**（与经济页 blocker 行同源同键，
        /// 见 <see cref="EconomyLabels.GateStep"/>）。只读当前制度快照：不写状态、不改阶段、不触发规划。
        /// 门已开放或没有任何待办门时不追加任何文本（不刷噪音）。</summary>
        private static void AppendInstitutionGateLine(System.Text.StringBuilder sb, bool institutionBlocked)
        {
            if (!institutionBlocked || sb == null) return;
            string step = GameComponent_PawnEconomy.FirstGateStep();
            if (!string.IsNullOrEmpty(step)) sb.Append('\n').Append(step);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;
            // CA-10：**对账处置入口** —— 只在确有需要人工裁决的流程时出现（平时不占位）。
            // 玩家必须先核验物理事实（柜台接收区/本人背包/批次台账），再选择"继续办理"或"按损失核销剩余"；
            // 两个动作都不改金额、不改债权，只改流程状态与批次核销。
            var reconcileFlows = GameComponent_PawnEconomy.Current?.ReadReconciliationFlowsAtStation(this);
            if (reconcileFlows != null && reconcileFlows.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Reconcile.Label", "Reconcile")
                        + " (" + reconcileFlows.Count + ")",
                    defaultDesc = EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Reconcile.Desc", "ReconcileDesc"),
                    icon = def?.uiIcon,
                    action = OpenLaborReconciliationMenu
                };
            }
            // REQ-021：**诊断快照入口**（只读、零副作用）。玩家/支持人员可复制这行文本到回报里，
            // 从而把「我看到的」变成「可复现的事实」；不写任何状态、不调 Scribe、不改账。
            yield return new Command_Action
            {
                defaultLabel = EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Diag.Label", "LaborDiag"),
                defaultDesc = EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Diag.Desc", "LaborDiagDesc"),
                icon = def?.uiIcon,
                action = () =>
                {
                    string snapshot = GameComponent_PawnEconomy.Current?.BuildLaborDiagnostics() ?? string.Empty;
                    if (string.IsNullOrEmpty(snapshot)) return;
                    Log.Message("[PC][LABORFLOW-DIAG] " + snapshot);
                    Find.WindowStack.Add(new Dialog_MessageBox(snapshot,
                        EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Diag.Close", "Close"), null, null, null,
                        EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Diag.Title", "LaborDiagTitle")));
                }
            };
            // P2-3：机构主按钮的两条文案走**整键回落**（缺键显示稳定 ID，绝不把 PC_PEA.* 裸键打上屏）。
            yield return new Command_Action
            {
                defaultLabel = EconomyLabels.TranslatedOrId("PC_PEA.Open", "Open"),
                defaultDesc = EconomyLabels.TranslatedOrId("PC_PEA.OpenDesc", "OpenDesc"),
                // 2026-10-10（实机截图定位）：`Command.icon == null` 时原版 `Command.DrawIcon` 直接回落
                // `BaseContent.BadTex`（品红底 + 红叉）—— 该占位图同时用于"未提供图标"与"贴图丢失"，
                // 因此看着像故障。本按钮此前从未设 icon ⇒ 5 台柜台的「经济机构」一律显示红叉。
                // 改用该 Def 自己的 uiIcon（与柜台工作台窗口头部同一张图）；Def 未声明时为 null ⇒ 回落原占位。
                icon = def?.uiIcon,
                // P0-6（2026-10-10）：按机构类型打开**自己的柜台工作台**（单台机构一个窗口）；
                // 缺 <c>EconomyInstitutionExtension</c>（未声明 kind）时回落到原聚合窗口 ——
                // 一个坏 Def 不能把按钮变成"点了没反应"。（声明了 kind 的 Def 一律开工作台；
                // 枚举值越界由 QueryWorkspace 的 default 分支按体检中心口径渲染，不静默失败。）
                action = () =>
                {
                    var kind = def?.GetModExtension<EconomyInstitutionExtension>()?.kind;
                    if (kind.HasValue) { Find.WindowStack.Add(new Window_InstitutionWorkspace(Map, this)); return; }
                    // 末批（删聚合窗口）：未声明 <c>kind</c> 的 Def 是**数据错误**（5 台柜台定义都声明了它）。
                    // 旧实现回落到聚合窗口 —— 那个窗口已删除，而"点了没反应"违反 F4 ⇒ 改为**如实报错**：
                    // 按钮照旧可点，点下去明确告诉玩家这栋建筑缺少机构类型声明（一行提示而非静默失败）。
                    Messages.Message(EconomyLabels.TranslatedOrId("PC_PEA.KindMissing", "KindMissing"),
                        MessageTypeDefOf.RejectInput, false);
                }
            };
            // CHG-20261012-ECON-CONTRACTIZE · B2（用户裁决⑥「服务价迁体检中心建筑字段」）：政策页退役后
            // 这里成为服务价**唯一写路径**。只对体检中心显示（服务结算只发生在体检中心）。
            // 0 = **未挂牌**（Help 里写明"不收费、不结算"）—— 不是"免费"；调用方读到 null 必须拒绝（M5）。
            if (def?.GetModExtension<EconomyInstitutionExtension>()?.kind == InstitutionKind.ExamCenter)
                yield return new Command_Action
                {
                    defaultLabel = EconomyLabels.TranslatedOrId("PC_PEA.Policy.ServicePrice", "ServicePrice"),
                    defaultDesc = EconomyLabels.Formatted("PC_PEA.Policy.ServicePriceHelp", "ServicePriceHelp",
                        EconomyLabels.FormatSilver(ServicePricePerUnitMinor)),
                    icon = def?.uiIcon,
                    action = () => Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                    {
                        ServicePriceOption("PC_PEA.Policy.ServicePriceUp", ServicePriceStepMinor),
                        ServicePriceOption("PC_PEA.Policy.ServicePriceDown", -ServicePriceStepMinor),
                        ServicePriceOption("PC_PEA.Policy.ServicePriceClear", 0L)
                    }))
                };
        }
        /// <summary>服务价调整菜单项：label 走**整键回落**（缺键显示稳定 ID，绝不空白）；改动当场
        /// <c>Messages</c> 回报（点了没反应等于没改，F4）；已到界不动时**不刷假消息**。</summary>
        private FloatMenuOption ServicePriceOption(string key, long delta)
            => new FloatMenuOption(EconomyLabels.TranslatedOrId(key, key.Substring("PC_PEA.".Length)), () =>
            {
                long before = ServicePricePerUnitMinor;
                long next = delta == 0L ? 0L : before + delta;
                if (next < 0L) next = 0L;
                if (next > ServicePriceMaximumMinor) next = ServicePriceMaximumMinor;
                if (next == before) return;
                ServicePricePerUnitMinor = next;
                Messages.Message(EconomyLabels.Formatted("PC_PEA.Policy.ServicePriceChanged", "ServicePriceChanged",
                    EconomyLabels.FormatSilver(next)), MessageTypeDefOf.NeutralEvent, false);
            });
        /// <summary>单个柜台的**在售容量上限**（唯一来源，2026-10-10）：入库门（<c>Stock</c> 动词校验）、
        /// 搬运入库（本类 <see cref="Store"/>）与柜台工作台 KPI 显示共用同一个数字 —— 此前三处各写 64，
        /// 改一处不生效。注意它与"列表显示上限"（<c>Take(64)</c>）语义不同，不要合并。</summary>
        public const int StockCapacity = 64;
        public bool Store(Pawn pawn)
        {
            var item = pawn.carryTracker.CarriedThing as Apparel;
            return item != null && Stock.Count < StockCapacity && Stock.TryAddOrTransfer(item, 1, false) == 1;
        }
    }
    public sealed class JobGiver_EconomicDemand : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            var economy = GameComponent_PawnEconomy.Current;
            if (economy?.HasPendingLaborFulfillment(pawn) == true)
                return LaborFlowRules.FulfillmentPriority(GameComponent_PawnEconomy.SafeFulfillmentDecision(pawn), true,
                    pawn.timetable?.CurrentAssignment == TimeAssignmentDefOf.Work);
            return GameComponent_PawnEconomy.SafeDecision(pawn) ? 6f : 0f;
        }
        protected override Job TryGiveJob(Pawn pawn) => GameComponent_PawnEconomy.Current?.Plan(pawn);
    }
    public sealed class WorkGiver_EconomicExam : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) => GameComponent_PawnEconomy.Current?.Clinics(pawn) ?? Enumerable.Empty<Thing>();
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => t is Building_EconomyInstitution
            && pawn.CanReserve(t, 2) && GameComponent_PawnEconomy.Current?.DoctorJob(pawn, t) != null;
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) => GameComponent_PawnEconomy.Current?.DoctorJob(pawn, t);
    }
    public sealed class JobDriver_EconomicVisit : JobDriver
    {
        private GameComponent_PawnEconomy Economy => GameComponent_PawnEconomy.Current;
        private bool Doctor => job.def.defName == "PC_PEA_PerformExam";
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            bool reserved = pawn.Reserve(job.targetA, job, 2, -1, null, errorOnFailed);
            if (!reserved) Economy?.CancelJob(job.count);
            return reserved;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.AddFinishAction(condition => Economy?.CancelJob(job.count));
            this.FailOn(() => Economy == null || !Economy.JobOrderValid(job.count));
            this.FailOn(() => pawn.Drafted || pawn.Downed || pawn.InMentalState
                || pawn.needs.food?.CurLevelPercentage < .35f || pawn.needs.rest?.CurLevelPercentage < .25f);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var service = ToilMaker.MakeToil("EconomicInstitutionService");
            service.defaultCompleteMode = ToilCompleteMode.Never;
            service.tickAction = () => {
                if (Economy.IsExam(job.count)) Economy.Pulse(job.count, pawn, Doctor);
                else if (Economy.FinishVisit(job.count, pawn)) ReadyForNextToil();
                else { EndJobWith(JobCondition.Incompletable); return; }
                if (!Economy.OrderActive(job.count)) ReadyForNextToil();
            };
            yield return service;
            if (Economy.IsPurchase(job.count))
            {
                yield return Toils_General.Wait(60);
                var wear = ToilMaker.MakeToil("WearPurchasedGarment");
                wear.initAction = () => Economy.WearAcquired(pawn);
                wear.defaultCompleteMode = ToilCompleteMode.Instant; yield return wear;
            }
        }
    }
    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**到劳务站办理签约**（REQ-LaborVisual-002/003）。
    /// Toil 顺序 = 真实寻路 → 洽谈停留（90t，意向阶段）→ **SignCommit**（原子复验签约）→ 签领停留（30t，发纸）。
    /// 基本需求/征召/倒地一律中断（暂停不抹掉意图：流程行仍在，恢复后重新规划）。
    /// </summary>
    public sealed class JobDriver_LaborVisit : JobDriver_LaborCounter
    {
        protected override bool IsSubmission => false;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (var approach in ApproachCounter()) yield return approach;
            yield return ServiceDelay("LaborNegotiate", LaborFlowRules.NegotiateTicks, () => Economy.LaborArrivedAtOffice(pawn));
            var sign = ToilMaker.MakeToil("LaborSignCommit");
            sign.defaultCompleteMode = ToilCompleteMode.Instant;
            sign.initAction = () => { if (!Economy.LaborSignCommit(pawn, job.targetA.Thing)) EndJobWith(JobCondition.Incompletable); };
            yield return sign;
            yield return ServiceDelay("LaborIssueDocument", LaborFlowRules.IssueTicks, () =>
            { if (!Economy.LaborIssueDocument(pawn)) EndJobWith(JobCondition.Incompletable); });
        }
    }

    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**到工资结算处交验**（REQ-LaborVisual-010）。
    /// Toil 顺序 = 真实寻路 → 核验停留（90t）→ **SubmissionCommit**（接收回执 + 授予付款资格）。
    /// 付款**不在此发生**（资格成立后由既有账本结算）；实物未带够 ⇒ 完成本次并退避，绝不伪付。
    /// </summary>
    public sealed class JobDriver_LaborSubmit : JobDriver_LaborCounter
    {
        protected override bool IsSubmission => true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (var approach in ApproachCounter()) yield return approach;
            yield return ServiceDelay("LaborVerifySubmission", LaborFlowRules.VerifyTicks, () => Economy.LaborVerificationStarted(pawn));
            var submit = ToilMaker.MakeToil("LaborSubmissionCommit");
            submit.defaultCompleteMode = ToilCompleteMode.Instant;
            submit.initAction = () => { if (!Economy.LaborSubmit(pawn, job.targetA.Thing)) EndJobWith(JobCondition.Incompletable); };
            yield return submit;
        }
    }

    /// <summary>
    /// CHG-20261011-LABOR-VISUAL 返修（CA-07）：**自主收集真实产物**（REQ-LaborVisual-007/010）。
    ///
    /// <para>Toil 顺序 = 容量守卫 → 真实寻路到该**真实实例** → 取入**本人库存**（跨 Job 保留；用
    /// carryTracker 会在 Job 结束时原地掉落）。每趟一个 Job，多趟由规划层重复下发 ——
    /// 中断、被打断、物品被搬走都只是"这趟没取到"，不会伪造交付（批次仍按剩余量记账）。</para>
    /// </summary>
    public sealed class JobDriver_LaborCollectOutputs : JobDriver
    {
        private string collectedDefName;
        private int inventoryBefore;
        public override void ExposeData()
        { base.ExposeData(); Scribe_Values.Look(ref collectedDefName, "laborCollectedDef"); Scribe_Values.Look(ref inventoryBefore, "laborInventoryBefore", 0); }
        public override string GetReport() => EconomyLabels.TranslatedOrId(job.targetA.Thing is Thing_WorkContractDocument
            ? "PC_PEA.LaborVisual.Report.CollectDocument" : "PC_PEA.LaborVisual.Report.Collecting", "Collecting");
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed)) return true;
            GameComponent_PawnEconomy.Current?.LaborInterrupted(pawn, JobCondition.Incompletable, job.targetA.Thing);
            return false;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnForbidden(TargetIndex.A);
            this.FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            this.AddFinishAction(condition => GameComponent_PawnEconomy.Current?.LaborInterrupted(pawn, condition, job.targetA.Thing));
            this.FailOn(() => pawn.Drafted || pawn.Downed || pawn.InMentalState
                || pawn.needs?.food?.CurLevelPercentage < .35f || pawn.needs?.rest?.CurLevelPercentage < .25f);
            // 容量守卫：`Toils_Haul.TakeToInventory` 本身不抛，但"手上一件都拿不下"时整趟白跑 ——
            // 在这里提前结束（规划层下次会重新评估），避免站着空转。
            var guard = ToilMaker.MakeToil("LaborCollectCapacityGuard");
            guard.defaultCompleteMode = ToilCompleteMode.Instant;
            guard.initAction = () =>
            {
                var thing = job.targetA.Thing;
                collectedDefName = thing?.def?.defName;
                inventoryBefore = GameComponent_PawnEconomy.LaborInventoryCount(pawn, collectedDefName);
                if (thing?.def == null || job.count <= 0
                    || pawn.inventory?.innerContainer == null || !pawn.inventory.innerContainer.CanAcceptAnyOf(thing, false))
                    EndJobWith(JobCondition.Incompletable);
            };
            // CA-13：阶段由**真实 Job 事件**推进（开始搬运 ⇒ CollectingOutputs），不在绘制/规划路径写状态。
            var announce = ToilMaker.MakeToil("LaborCollectStageAnnounce");
            announce.defaultCompleteMode = ToilCompleteMode.Instant;
            announce.initAction = () => { if (!(job.targetA.Thing is Thing_WorkContractDocument)) GameComponent_PawnEconomy.Current?.NoteLaborCollectingStarted(pawn); };
            yield return announce;
            yield return guard;
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            // 取入**库存**（不是携带器）：Job 结束时不会原地掉落，下一次规划才能把货带到柜台。
            var collect = ToilMaker.MakeToil("LaborTakeRegisteredInventory");
            collect.defaultCompleteMode = ToilCompleteMode.Instant;
            collect.initAction = () =>
            {
                if (GameComponent_PawnEconomy.Current?.LaborTakeInventory(pawn, job.targetA.Thing,
                    job.count, "job:" + job.loadID) != true) EndJobWith(JobCondition.Incompletable);
            };
            yield return collect;
        }
    }

    public sealed class JobDriver_EconomicStock : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed)
            && pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var finish = ToilMaker.MakeToil("EconomicStockTransfer");
            finish.initAction = () => {
                if (!(job.targetB.Thing is Building_EconomyInstitution shop) || !shop.Store(pawn)) EndJobWith(JobCondition.Incompletable);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant; yield return finish;
        }
    }
    public sealed class JobDriver_EconomicWear : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => !job.targetA.Thing.Spawned
            || pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() => pawn.Drafted || pawn.Downed || pawn.InMentalState);
            if (job.targetA.Thing.Spawned) yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(60);
            var finish = ToilMaker.MakeToil("EconomicWearOwned");
            finish.initAction = () => GameComponent_PawnEconomy.Current?.WearOwned(pawn, job.targetA.Thing as Apparel);
            finish.defaultCompleteMode = ToilCompleteMode.Instant; yield return finish;
        }
    }
}
