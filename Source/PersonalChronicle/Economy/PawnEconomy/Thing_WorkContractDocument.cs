using System.Collections.Generic;
using System.Text;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**实体工作合同纸**（真实 Thing，可选中、可检查、可保存）。
    ///
    /// <para><b>只存身份，不存第二份可变账</b>（ARCH 席④）：本类字段 = 合同身份（<see cref="ContractId"/>）、
    /// 文档实例身份（<see cref="DocumentInstanceId"/> / <see cref="Generation"/>）、呈递授权人
    /// （<see cref="AuthorizedHolderKey"/>）与签发机构（<see cref="IssuerInstitutionKey"/>）。
    /// **条款/进度/金额一律从权威合同快照查询** —— 存第二份余额或 CompletedJobs 必然与账本分叉。</para>
    ///
    /// <para><b>纸 ≠ 债权</b>：<see cref="AuthorizedHolderKey"/> 只决定"谁能合法呈递这张纸"，
    /// 付款对象（PayeeKey）始终由合同裁定 ⇒ 偷纸/捡纸者拿不到钱（REQ-LaborVisual-014）。</para>
    ///
    /// <para><b>代次</b>：补发递增 <see cref="Generation"/> 并换发新实例 ID，旧纸随之失效但**不被销毁**
    ///（可显示"已作废"），且**不得**按普通纪念纸清理（未付款/对账中/有效代次的纸受保护）。</para>
    /// </summary>
    public sealed class Thing_WorkContractDocument : ThingWithComps
    {
        /// <summary>合同身份（权威 ID；`contract:...`）。</summary>
        public string ContractId;
        /// <summary>文档实例身份（`contractdoc:{ContractId}|g{Generation}`）—— 与合同上的当前有效实例比对即可判旧纸。</summary>
        public string DocumentInstanceId;
        /// <summary>签发代次（≥1；0 = 数据损坏 ⇒ 按无效纸处理，不给任何权利）。</summary>
        public int Generation;
        /// <summary>呈递授权人（承接时的持约人 PawnKey）。**只决定呈递权**，不决定付款对象。</summary>
        public string AuthorizedHolderKey;
        /// <summary>签发机构（劳务站稳定 ID）。</summary>
        public string IssuerInstitutionKey;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ContractId, "pcContractId");
            Scribe_Values.Look(ref DocumentInstanceId, "pcDocInstance");
            Scribe_Values.Look(ref Generation, "pcDocGeneration", 0);
            Scribe_Values.Look(ref AuthorizedHolderKey, "pcAuthorizedHolder");
            Scribe_Values.Look(ref IssuerInstitutionKey, "pcIssuerInstitution");
        }

        /// <summary>该纸是否**结构上**完整（身份齐备）。false ⇒ 不给任何权利、不参与呈递。</summary>
        public bool IsStructurallyValid
            => !string.IsNullOrEmpty(ContractId) && !string.IsNullOrEmpty(DocumentInstanceId) && Generation > 0;

        /// <summary>
        /// 该纸对某 Pawn 是否**当前有效**（可合法呈递 / 可领款）。判据全部来自权威合同快照：
        /// 结构完整 ∧ 授权持有人匹配 ∧ 代次与实例与合同当前记录**逐字一致**。
        /// 任何一项不符 ⇒ 无效（fail-closed：旧纸、复制纸、伪造纸一律不给付款资格）。
        /// </summary>
        internal bool IsValidFor(string pawnKey, WorkContract contract)
        {
            if (!IsStructurallyValid || contract == null) return false;
            if (!string.Equals(contract.ContractId, ContractId, System.StringComparison.Ordinal)) return false;
            if (!string.IsNullOrEmpty(AuthorizedHolderKey)
                && !string.Equals(AuthorizedHolderKey, pawnKey, System.StringComparison.Ordinal)) return false;
            if (contract.DocumentGeneration != Generation) return false;
            return string.Equals(contract.DocumentInstanceId, DocumentInstanceId, System.StringComparison.Ordinal);
        }

        /// <summary>呈递授权人是否为该 Pawn（不校验代次；用于"这是不是我的纸"的快速判定）。</summary>
        public bool Authorizes(string pawnKey)
            => !string.IsNullOrEmpty(pawnKey)
               && (string.IsNullOrEmpty(AuthorizedHolderKey)
                   || string.Equals(AuthorizedHolderKey, pawnKey, System.StringComparison.Ordinal));

        /// <summary>
        /// 检查文本（只读）。**全部数字来自权威快照**；查不到合同 ⇒ 如实说"合同记录不存在"，
        /// 绝不显示编造的条款（REQ-LaborVisual-004/022）。
        /// </summary>
        public override string GetInspectString()
        {
            var text = new StringBuilder();
            string baseText = base.GetInspectString();
            if (!string.IsNullOrEmpty(baseText)) text.Append(baseText).Append('\n');
            var economy = GameComponent_PawnEconomy.Current;
            text.Append(T("PC_PEA.LaborVisual.Doc.ShortCode")).Append(": ")
                .Append(LaborFlowRules.ShortCode(ContractId ?? string.Empty));
            if (!IsStructurallyValid)
            {
                text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.Corrupt"));
                return text.ToString();
            }
            var contract = economy?.QueryContractById(ContractId);
            if (contract == null)
            {
                text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.ContractMissing"));
                return text.ToString();
            }
            bool current = contract.DocumentGeneration == Generation
                && string.Equals(contract.DocumentInstanceId, DocumentInstanceId, System.StringComparison.Ordinal);
            text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.Generation")).Append(": g").Append(Generation);
            if (!current)
            {
                // 旧代次/被补发：如实标注"已补发作废"，并明确它**没有支付或继承权利**。
                text.Append(" · ").Append(T("PC_PEA.LaborVisual.Doc.Superseded"))
                    .Append(" (g").Append(contract.DocumentGeneration).Append(')');
            }
            text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.Holder")).Append(": ")
                .Append(string.IsNullOrEmpty(AuthorizedHolderKey) ? T("PC_PEA.LaborVisual.Doc.NoHolder") : LabelFor(AuthorizedHolderKey));
            text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.WorkType")).Append(": ").Append(WorkLabel(contract));
            text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.Wage")).Append(": ")
                .Append(EconomyLabels.FormatSilver(contract.WagePerJobMinor));
            text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.Progress")).Append(": ")
                .Append(contract.CompletedJobs.ToString()).Append('/').Append(contract.MaxJobs.ToString());
            text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.Due")).Append(": ")
                .Append(DateAtGameTick(contract.ExpiresTick));
            var deliveryTotals = economy?.QueryLaborDeliveryTotals(ContractId);
            if (deliveryTotals != null)
                text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.Delivery")).Append(": ").Append(deliveryTotals.Delivered)
                    .Append(" · ").Append(T("PC_PEA.LaborVisual.Doc.Remaining")).Append(": ").Append(deliveryTotals.Remaining);
            var flow = economy?.QueryLaborWorkflow(ContractId);
            text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.Stage")).Append(": ")
                .Append(LaborVisualText.StageLabel(flow?.Stage ?? LaborFlowStage.None));
            if (flow != null && flow.Hold != FlowHold.None)
                text.Append(" · ").Append(LaborVisualText.HoldLabel(flow.Hold))
                    .Append(" (").Append(LaborVisualText.ReasonLabel(flow.ReasonCode)).Append(')');
            if (flow != null)
                text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.NextStation")).Append(": ")
                    .Append(economy.LaborStationLabel(flow.CurrentStationKey)).Append(" · ").Append(LaborVisualText.NextActionLabel(flow.NextAction));
            var submission = economy?.QueryLatestSubmission(ContractId);
            long paid = economy?.QueryPaidMinorFor(ContractId) ?? 0L;
            text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.Paid")).Append(": ")
                .Append(EconomyLabels.FormatSilver(paid));
            text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.Unpaid")).Append(": ")
                .Append(EconomyLabels.FormatSilver(economy?.QueryUnpaidMinorFor(ContractId) ?? 0L));
            if (submission != null)
                text.Append('\n').Append(T("PC_PEA.LaborVisual.Doc.LastSubmission")).Append(": ")
                    .Append(DateAtGameTick(submission.AcceptedTick));
            text.Append('\n').Append(current
                ? T("PC_PEA.LaborVisual.Doc.Valid")
                : T("PC_PEA.LaborVisual.Doc.InvalidNoRights"));
            return text.ToString();
        }

        private string DateAtGameTick(int tick)
        {
            var heldMap = MapHeld;
            var location = heldMap != null && heldMap.Tile.Valid && Find.WorldGrid != null
                ? Find.WorldGrid.LongLatOf(heldMap.Tile) : UnityEngine.Vector2.zero;
            return GenDate.DateFullStringAt((long)GenTicks.TicksAbs - GenTicks.TicksGame + tick, location);
        }

        private static string WorkLabel(WorkContract contract)
        {
            if (WorkPriceRules.Parse(contract.PositionKey, out string work, out _, out string order) && order != null)
                return EconomyLabels.TranslatedOrId("PC_PEA.Order." + order, order);
            return work ?? contract.PositionKey;
        }

        private static string LabelFor(string pawnKey)
        {
            var pawn = GameComponent_PawnEconomy.Current?.QueryPawnByKey(pawnKey);
            return pawn != null ? pawn.LabelShortCap : pawnKey;
        }

        /// <summary>整键回落翻译（缺键显示稳定 ID，绝不空白；GOV-010 / F4）。</summary>
        private static string T(string key) => EconomyLabels.TranslatedOrId(key, key.Substring("PC_PEA.".Length));

        /// <summary>仅用于**定位**的 gizmo：把镜头移向该纸当前所在（地面/载体/持有者）。找不到就**如实说**，不跳同名他人。</summary>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;
            yield return new Command_Action
            {
                defaultLabel = T("PC_PEA.LaborVisual.Doc.Locate"),
                defaultDesc = T("PC_PEA.LaborVisual.Doc.LocateDesc"),
                icon = def?.uiIcon,
                action = () =>
                {
                    // Native held-position traversal covers Pawn_InventoryTracker, carry and nested holders.
                    if (MapHeld != null && PositionHeld.IsValid)
                    { CameraJumper.TryJump(new GlobalTargetInfo(PositionHeld, MapHeld)); return; }
                    Messages.Message(T("PC_PEA.LaborVisual.Doc.LocateFailed"), MessageTypeDefOf.RejectInput, false);
                }
            };
        }
    }
}
