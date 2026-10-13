using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Automation;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Governance;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        // P0-4（2026-10-10）：整键回落 —— 缺键显示标识本身，绝不把 PC_PEA.* 裸键打上屏。
        private static string T(string key) => EconomyLabels.TranslatedOrId("PC_PEA." + key, key);
        /// <summary>P2-3：**带位置参数**的整键翻译（唯一入口）。替代 1.6 已 <c>[Obsolete]</c> 的原版
        /// <c>Translate(params object[])</c> —— 后者缺键时会把带 <c>{0}</c> 的裸键直接打上屏。</summary>
        private static string TF(string key, params object[] args)
            => EconomyLabels.Formatted("PC_PEA." + key, key, args);
        /// <summary>F6-A：说明性长句行 —— 无按钮，呈现层按行种标记用次要色绘制，与数据行区分。</summary>
        private static EconomyRow Hint(string text) => new EconomyRow(text, verb: EconomyWindowLayout.HintRowVerb);
        private static string Money(long value) => (value / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        private string PawnName(string key) => PawnByKey(key)?.LabelShortCap.ToString() ?? key ?? "—";
        public EconomyView Query(Map map, string focusKey, int tab)
        {
            RefreshPawns();
            var rows = new List<EconomyRow>();
            var actors = map?.mapPawns.FreeColonistsSpawned.Where(BasicPawn).OrderBy(p => p.thingIDNumber)
                .Select(p => new EconomyRow(p.LabelShortCap, key: p.GetUniqueLoadID())).ToList() ?? new List<EconomyRow>();
            var mode = QueryInstitutionMode();
            string status = T("Mode.Phase." + mode.Phase);
            var selected = anchors.FirstOrDefault(a => a.Spawned && a.GetUniqueLoadID() == focusKey && a.Map == map);
            var institution = selected == null ? null : Institution(selected);
            rows.Add(Hint(T("Mode.Explanation")));
            if (mode.HasOutstandingRights) rows.Add(Hint(T("Mode.Outstanding")));
            if (mode.Phase == PersonalChronicle.Domain.PawnEconomy.Governance.InstitutionPhase.Faulted)
                rows.Add(new EconomyRow(ReverifyHint(mode), T("Mode.Reverify"), "Reverify"));
            if (mode.Fault != null) rows.Add(Hint(T("Mode.FaultHint") + " · " + T("Mode.FaultCode") + ": " + mode.Fault));
            if (mode.Desired || mode.Phase != PersonalChronicle.Domain.PawnEconomy.Governance.InstitutionPhase.Off)
                foreach (string blocker in mode.Blockers)
                    rows.Add(Hint(EconomyLabels.GateBlocker(blocker) + " " + EconomyLabels.GateStep(blocker)));
            if (!State.Ready) rows.Add(Hint(T("FaultHint")));
            else switch (tab)
            {
                case 0: BuildInstitutions(map, selected, institution, rows, actors); break;
                case 1: BuildJobs(map, selected, institution, rows, actors); break;
                case 2: BuildStock(map, selected, institution, rows, actors); break;
                case 3: BuildAccounts(map, selected, institution, rows, actors); break;
                case 4: BuildLedger(map, selected, institution, rows, actors); break;
                default: BuildReports(map, selected, institution, rows, actors); break;
            }
            if (!LegacyFeesAllowed)
                // F6-A：`Hint` 不是动作动词，而是**行种标记**（说明性长句，无按钮）—— 必须一起保留，
                // 否则它会被这里剥成纯文本行、呈现层就再也分不出"说明"与"数据"。
                rows = rows.Select(row => row.Verb == "Focus" || row.Verb == EconomyInstitutionCards.InstitutionRowVerb
                    || row.Verb == "Reverify"
                    || row.Verb == EconomyWindowLayout.HintRowVerb
                    || row.Verb?.StartsWith("Treasury.", StringComparison.Ordinal) == true ? row : new EconomyRow(row.Text)).ToList();
            return new EconomyView(State.Revision, mode.NewCommitmentsAllowed, false, status, selected?.LabelCap.ToString() ?? T("NoFocus"), rows, actors);
        }
        private void BuildInstitutions(Map map, Building_EconomyInstitution selected, EconomyInstitution institution, List<EconomyRow> rows, List<EconomyRow> actors)
        {
            // FA-02（经理侧审计 2026-10-10；B2 重写）：首页机构行只输出**仍然成立**的事实
            //（类型 · 建筑名 · 制度阶段），行动词 `InstitutionCard` 仅为**行种标记**（呈现层据此画卡片）。
            // 政策深链与旧 `EconomyInstitution.Open` 读数一并退役 —— 状态来源只剩制度阶段（见 InstitutionSummary）。
            var mode = QueryInstitutionMode();
            var institutions = QueryInstitutions(map.uniqueID);
            if (institutions.Count == 0) rows.Add(Hint(T("Mode.InstitutionNone")));
            else
            {
                // B2：机构行只带**行种标记**（`InstitutionCard`，呈现层据此画卡片），不再挂动作文案与政策深链；
                // 「制度未开放 ⇒ 不支付」的提示行（Mode.OpenNotPayment）随政策页退役删除（制度阶段已在每行摘要里）。
                for (int i = 0; i < institutions.Count; i++)
                    rows.Add(new EconomyRow(InstitutionSummary(institutions[i], mode), null,
                        EconomyInstitutionCards.InstitutionRowVerb, key: institutions[i].AnchorKey));
            }
            // 旧制度记录独立成「历史对账」区：只展示归档事实，不参与任何当前状态判定，也不提供写入口
            // （旧写入路径由 LegacyFeesAllowed=false 关闭）。Focus = 把该旧记录选中给旧页签查看，与营业状态无关。
            // UI 收紧（2026-10-10）：只有**真正存在旧记录**的 anchor 才进本区。旧记录在新体系下不再产生
            // （Register 受 LegacyFeesAllowed=false 关闭），原「有 anchor 就列出、无记录则显示无归档记录」
            // 会让正常存档长期只看到一片空集噪音 ⇒ 空集时整个区（含标题句）都不出现。
            var legacyAnchors = anchors.Where(a => a.Spawned && a.Map == map && Institution(a) != null).ToList();
            if (legacyAnchors.Count > 0)
            {
                rows.Add(Hint(T("Mode.LegacyReadOnly")));
                for (int i = 0; i < legacyAnchors.Count; i++)
                {
                    var legacyAnchor = legacyAnchors[i];
                    var legacyModel = Institution(legacyAnchor);
                    string legacyText = legacyAnchor.LabelCap + " · " + T("Mode.LegacyArchive") + " · "
                        + (legacyModel.Open ? T("OpenState") : T("ClosedState"));
                    // F4：旧记录仍只给 `Focus`（选中给旧页签查看），不提供任何写入口。
                    rows.Add(new EconomyRow(legacyText, T("Configure"), "Focus", key: legacyAnchor.GetUniqueLoadID()));
                }
            }
            // FA-02 / UI 门 P2（2026-10-10 复核）：本块是**旧记录**的归档事实展示。原先它按"可编辑"生成
            // 按钮行，但 `LegacyFeesAllowed => false` 的动词白名单会把动作全部剥掉，只留下孤立标签
            // （例如「价格: 0.00」与「价格」两行同屏）⇒ 现在只保留**带值事实**，纯动作行整行删除。
            if (institution != null)
            {
                rows.Add(new EconomyRow(T("Hours") + $": {institution.StartHour:00}:00–{institution.EndHour:00}:00"));
                if (institution.InstitutionKind == InstitutionKind.Shop || institution.InstitutionKind == InstitutionKind.ExamCenter)
                    rows.Add(new EconomyRow(T("Price") + ": " + Money(institution.Price)));
                if (institution.InstitutionKind == InstitutionKind.WorkshopOffice)
                {
                    rows.Add(new EconomyRow(T("Wage") + ": " + Money(institution.Wage)));
                    rows.Add(new EconomyRow(T("Capacity") + ": " + institution.Capacity));
                    rows.Add(new EconomyRow(T("RequireExam") + ": " + institution.RequiresExam));
                }
                if (institution.InstitutionKind == InstitutionKind.ExamCenter)
                    rows.Add(new EconomyRow(T("Doctor") + ": " + PawnName(institution.DoctorKey)));
            }
        }
        private void BuildJobs(Map map, Building_EconomyInstitution selected, EconomyInstitution institution, List<EconomyRow> rows, List<EconomyRow> actors)
        {
            foreach (var p in State.All<EconomyPosition>().Where(p => p.MapId == map?.uniqueID && p.Open))
                rows.Add(new EconomyRow((Zone(p)?.label ?? T("MissingZone")) + " · " + T("Wage") + " " + Money(p.Wage)
                    + $" · {p.StartHour:00}–{p.EndHour:00} · {State.All<EconomyContract>().Count(c => c.Active && c.PositionId == p.Id)}/{p.Capacity}"
                    + (p.RequiresExam ? " · " + T("RequireExam") : ""), T("Withdraw"), "Withdraw", p.Id));
            foreach (var c in State.All<EconomyContract>().Where(c => c.Active && State.Find<EconomyPosition>(c.PositionId)?.MapId == map?.uniqueID))
                rows.Add(new EconomyRow(PawnName(c.PawnKey) + " · " + T("Contract") + " #" + c.Id, T("Terminate"), "Terminate", c.Id));
        }
        private void BuildStock(Map map, Building_EconomyInstitution selected, EconomyInstitution institution, List<EconomyRow> rows, List<EconomyRow> actors)
        {
            if (selected?.def?.GetModExtension<EconomyInstitutionExtension>()?.kind == InstitutionKind.Shop)
            {
                // 2026-10-10（用户裁决）：每件存货显示**该件物品自身价值**，与结算走同一函数（显示价 == 结算价）。
                // B2：政策兜底价整体退役 ⇒ 不再有第二价源（价值不可得的件按 0 显示，且在商店侧不可购 —— 见 EconomyConsumption）。
                foreach (var item in ShopInventory(selected))
                    rows.Add(new EconomyRow(item.LabelCap + " · " + Money(ItemValuation.UnitPriceMinor(item.MarketValue))));
                rows.Add(Hint(T("Public.StockHint")));
            }
            else rows.Add(Hint(T("SelectShop")));
        }
        private void BuildAccounts(Map map, Building_EconomyInstitution selected, EconomyInstitution institution, List<EconomyRow> rows, List<EconomyRow> actors)
        {
            BuildTreasuryRows(selected, rows);
        }
        private void BuildLedger(Map map, Building_EconomyInstitution selected, EconomyInstitution institution, List<EconomyRow> rows, List<EconomyRow> actors)
        {
            AddCurrentSettlementRows(rows);
            if (State.All<EconomyReceipt>().Any()) rows.Add(Hint(T("Mode.LegacyReadOnly")));
            foreach (var r in State.All<EconomyReceipt>().OrderBy(r => r.Paid).ThenByDescending(r => r.Id))
                rows.Add(new EconomyRow("#" + r.Id + " · " + T("Receipt." + r.Type) + " · " + PawnName(r.SubjectKey) + " · " + Money(r.Amount)
                    + " · " + (r.Paid ? T("Paid") : T("Unpaid"))));
            foreach (var o in State.All<EconomyOrder>().Where(o => o.Pending))
                rows.Add(new EconomyRow("#" + o.Id + " · " + PawnName(o.ClientKey) + " · " + T("Receipt." + o.OrderKind) + " · " + o.Progress,
                    T("Cancel"), "Cancel", o.Id));
        }
        private void AddCurrentSettlementRows(List<EconomyRow> rows)
        {
            foreach (var row in settlement.ReadEntries().Reverse().Take(64))
            {
                string source = row.Intent.Kind == SettlementKind.Deposit ? PawnName(row.Intent.Movement.CounterpartyId)
                    : SettlementLedgerNames.IsPublic(row.Intent.PayerAccount ?? SettlementLedgerNames.Treasury)
                        ? PublicAccountName(row.Intent.PayerAccount ?? SettlementLedgerNames.Treasury) : PawnName(row.Intent.PayerAccount);
                string target = row.Intent.Kind == SettlementKind.Withdrawal ? PawnName(row.Intent.Movement.CounterpartyId)
                    : row.Intent.Kind == SettlementKind.Deposit ? PublicAccountName(SettlementLedgerNames.Treasury)
                        : SettlementLedgerNames.IsPublic(row.Intent.TargetAccount) ? PublicAccountName(row.Intent.TargetAccount) : PawnName(row.Intent.TargetAccount);
                rows.Add(new EconomyRow(T("Public.Kind." + row.Intent.Kind) + " · " + Money(row.Intent.AmountMinor)
                    + " · " + source + " → " + target
                    + " · " + T("Public.Phase." + row.Phase)));
            }
        }
        private void BuildReports(Map map, Building_EconomyInstitution selected, EconomyInstitution institution, List<EconomyRow> rows, List<EconomyRow> actors)
        {
            foreach (var r in State.All<EconomyReport>().OrderByDescending(r => r.Id))
                rows.Add(new EconomyRow(PawnName(r.PawnKey) + " · " + T("Doctor") + " " + PawnName(r.DoctorKey)
                    + " · " + T("Capabilities") + $" {r.Moving:P0}/{r.Manipulation:P0}/{r.Consciousness:P0}"
                    + " · " + (Find.TickManager.TicksGame < r.ExpiresTick ? T("Valid") : T("Expired"))
                    + (r.VisibleConditions.Count == 0 ? "" : " · " + string.Join(", ", r.VisibleConditions.Select(name => DefDatabase<HediffDef>.GetNamedSilentFail(name)?.LabelCap.ToString() ?? name)))));
            rows.Add(Hint(T("ReportHint")));
        }
        public bool Execute(string verb, long id, string key, string actorKey, int revision)
        {
            if (!LegacyFeesAllowed) return false;
            if (!State.Ready || revision != State.Revision) return false;
            if (verb == "Enable")
            {
                bool enabled = !State.Enabled;
                if (!Service.Enable(enabled)) return false;
                if (!enabled) foreach (var c in State.All<EconomyContract>().Where(c => c.Active).ToList()) EndContract(c);
                return true;
            }
            if (verb == "Autonomy")
            {
                if (!BasicPawn(PawnByKey(key))) return false;
                var person = Service.Person(key); person.Autonomy = !person.Autonomy;
                if (!person.Autonomy)
                    foreach (var o in State.All<EconomyOrder>().Where(o => o.Pending && o.ClientKey == key).ToList()) Service.Cancel(o, "AutonomyDisabled");
                State.Revision++; return true;
            }
            if (verb == "Terminate") { var c = State.Find<EconomyContract>(id); if (c == null) return false; EndContract(c); return true; }
            if (verb == "Withdraw")
            {
                var p = State.Find<EconomyPosition>(id); if (p == null) return false; p.Open = false;
                foreach (var c in State.All<EconomyContract>().Where(c => c.Active && c.PositionId == id).ToList()) EndContract(c);
                State.Revision++; return true;
            }
            if (verb == "Cancel") return Service.Cancel(State.Find<EconomyOrder>(id), "PlayerCancelled");
            var institution = State.Find<EconomyInstitution>(id); var anchor = Anchor(id);
            if (institution == null || anchor == null) return false;
            if (verb == "Open") { institution.Open = !institution.Open; if (!institution.Open) CancelInstitution(id, "InstitutionClosed"); }
            else if (verb == "Hours")
            { institution.StartHour = institution.StartHour == 6 ? 18 : institution.StartHour == 18 ? 0 : 6; institution.EndHour = institution.StartHour == 6 ? 18 : institution.StartHour == 18 ? 6 : 0; }
            else if (verb == "PriceUp") institution.Price = Math.Min(100000, institution.Price + 1000);
            else if (verb == "PriceDown") institution.Price = Math.Max(0, institution.Price - 1000);
            else if (verb == "WageUp") institution.Wage = Math.Min(10000, institution.Wage + 500);
            else if (verb == "WageDown") institution.Wage = Math.Max(500, institution.Wage - 500);
            else if (verb == "Capacity") institution.Capacity = institution.Capacity % 4 + 1;
            else if (verb == "RequireExam") institution.RequiresExam = !institution.RequiresExam;
            else if (verb == "Publish") { if (!PublishOffer(institution, anchor, key)) return false; }
            else if (verb == "Doctor") { if (!AssignExaminer(institution, actorKey)) return false; }
            else if (verb == "Exam") { if (!RequestExam(institution, anchor, actorKey)) return false; }
            else if (verb == "Stock") { if (!StockItem(institution, anchor, key, actorKey)) return false; }
            else if (verb == "Settle")
            { if (institution.InstitutionKind != InstitutionKind.SettlementOffice) return false; Service.SettlePayables(); }
            else return false;
            institution.Revision++; State.Revision++; return true;
        }
        private bool PublishOffer(EconomyInstitution institution, Building_EconomyInstitution anchor, string key)
        {
            long id = institution.Id;
            if (institution.InstitutionKind != InstitutionKind.WorkshopOffice || !int.TryParse(key, out int zoneId)
                || State.All<EconomyPosition>().Count() >= 256 || State.All<EconomyPosition>().Any(p => p.Open && p.MapId == institution.MapId && p.ZoneId == zoneId)) return false;
            var zone = anchor.Map.zoneManager.AllZones.OfType<Zone_Workshop>().FirstOrDefault(z => z.ID == zoneId);
            if (!WorkshopZoneUtility.IsAlive(zone) || zone.WorkshopType == null) return false;
            State.Add(new EconomyPosition { InstitutionId = id, MapId = institution.MapId, ZoneId = zoneId,
                StartHour = institution.StartHour, EndHour = institution.EndHour, Capacity = institution.Capacity,
                Wage = institution.Wage, RequiresExam = institution.RequiresExam });
            return true;
        }
        private bool AssignExaminer(EconomyInstitution institution, string actorKey)
        {
            long id = institution.Id;
            if (institution.InstitutionKind != InstitutionKind.ExamCenter
                || State.All<EconomyOrder>().Any(o => o.Pending && o.InstitutionId == id)) return false;
            string prior = institution.DoctorKey; institution.DoctorKey = actorKey;
            if (!DoctorValid(institution)) { institution.DoctorKey = prior; return false; }
            return true;
        }
        private bool RequestExam(EconomyInstitution institution, Building_EconomyInstitution anchor, string actorKey)
        {
            long id = institution.Id;
            var pawn = PawnByKey(actorKey);
            if (institution.InstitutionKind != InstitutionKind.ExamCenter || !State.Enabled || !BasicPawn(pawn) || !DoctorValid(institution)
                || pawn.Map != anchor.Map || pawn.GetUniqueLoadID() == institution.DoctorKey || !Open(institution)) return false;
            var order = Service.Prepare(EconomyOrderKind.Examination, actorKey, institution, actorKey, institution.Price,
                Find.TickManager.TicksGame, provider: institution.DoctorKey);
            if (order == null) return false;
            Service.Person(actorKey).NextDecisionTick = 0;
            return true;
        }
        private bool StockItem(EconomyInstitution institution, Building_EconomyInstitution anchor, string key, string actorKey)
        {
            long id = institution.Id;
            var pawn = PawnByKey(actorKey);
            var item = anchor.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).OfType<Apparel>().FirstOrDefault(a => a.GetUniqueLoadID() == key);
            if (institution.InstitutionKind != InstitutionKind.Shop || !BasicPawn(pawn) || pawn.Map != anchor.Map || item == null
                || anchor.Stock.Count >= Building_EconomyInstitution.StockCapacity || State.All<EconomyOwnership>().Any(o => o.AssetKey == key)
                || !pawn.CanReserveAndReach(item, PathEndMode.ClosestTouch, Danger.None) || !pawn.CanReach(anchor, PathEndMode.Touch, Danger.None)
                || pawn.CurJob?.playerForced == true || item.IsForbidden(pawn)) return false;
            var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("PC_PEA_Stock"), item, anchor); job.count = 1;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            return true;
        }

        /// <summary>
        /// 机构摘要（B2：政策净删后只剩**仍然成立**的事实）：类型 · 建筑名 · **制度阶段**。
        ///
        /// <para>此前这一行还拼「机构配置（未配置/营业/停业）+ 营业时段 + 四类能力可用性」—— 三者全部
        /// 建立在 <c>InstitutionPolicy</c> 之上，随政策退役。**不保留**它们的理由不是"少写点字"，而是
        /// 继续显示就必须发明数据（M5：未知不当零 / GOV-010：不给玩家看假读数）。制度阶段保留：
        /// 它是制度开关的读数，与政策无关，而且是"为什么现在不能花钱"的唯一权威答案。</para>
        /// </summary>
        private string InstitutionSummary(InstitutionRef reference, InstitutionModeSnapshot mode)
            => EconomyLabels.KindLabel(reference.Kind) + " · " + reference.Anchor.LabelCap
               + " · " + T("Mode.PhaseLabel") + ": " + T("Mode.Phase." + mode.Phase);

        /// <summary>FA-23：故障态下的核验提示 —— 必须给出**具体故障对象**与"上次显式核验解除了什么"。</summary>
        private string ReverifyHint(InstitutionModeSnapshot mode)
        {
            var text = new StringBuilder(T("Mode.ReverifyHint"));
            text.Append(" · ").Append(T("Mode.FaultCode")).Append(": ").Append(mode.Fault ?? "—");
            string cleared = institutionMode.ReverifiedFault;
            if (!string.IsNullOrEmpty(cleared))
                text.Append(" · ").Append(T("Mode.Reverified")).Append(": ").Append(cleared);
            return text.ToString();
        }
    }
}
