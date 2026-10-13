using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Data.PawnEconomy
{
    internal sealed class SettlementLedgerLoad
    {
        public FundedSettlementLedger Ledger { get; }
        public string Fault { get; }
        public string ArchiveHash { get; }
        public bool PreserveRaw { get; }
        public SettlementLedgerLoad(FundedSettlementLedger ledger, string fault = null, bool preserveRaw = false, string archiveHash = null)
        { Ledger = ledger; Fault = fault; PreserveRaw = preserveRaw; ArchiveHash = archiveHash; }
    }
    internal static class SettlementLedgerCodec
    {
        public const int CurrentSchema = 1;
        public static string Encode(FundedSettlementLedger ledger, string fault, string archiveHash)
        {
            var root = new XElement("SettlementLedger", new XAttribute("schema", CurrentSchema), new XAttribute("version", ledger.Version),
                fault == null ? null : new XAttribute("fault", fault), archiveHash == null ? null : new XAttribute("archive", archiveHash),
                ledger.ReadEntries().Select(row => new XElement("Command", new XAttribute("id", row.Intent.CommandId),
                    new XAttribute("expected", row.Intent.ExpectedVersion), new XAttribute("kind", row.Intent.Kind),
                    new XAttribute("amount", row.Intent.AmountMinor), new XAttribute("phase", row.Phase),
                    row.Reason == null ? null : new XAttribute("reason", row.Reason),
                    row.Intent.Movement == null ? new object[] { new XAttribute("claim", row.Intent.ClaimId), new XAttribute("target", row.Intent.TargetAccount),
                        row.Intent.PayerAccount == null ? null : new XAttribute("payer", row.Intent.PayerAccount) }
                        : new object[] { new XAttribute("reserveVersion", row.Intent.Movement.ExpectedVersion), new XAttribute("party", row.Intent.Movement.CounterpartyId),
                            new XAttribute("source", row.Intent.Movement.SourceThingId), new XAttribute("count", row.Intent.Movement.SilverCount) })));
            // FA-01：零余额账户（设计 §4 EnrollPawn）没有资金条目 ⇒ 必须单独落盘，否则读档后新 Pawn
            // 又会退回「账户未知」并卡住自主求职。附加元素：旧档没有它照常加载（缺失 = 无登记）。
            foreach (string id in ledger.ReadEnrolledAccounts()) root.Add(new XElement("Account", new XAttribute("id", id)));
            return root.ToString(SaveOptions.DisableFormatting);
        }
        private static void Fields(XElement element, params string[] names)
        {
            if (element.HasElements || element.Attributes().Any(x => x.Name.NamespaceName.Length != 0 || !names.Contains(x.Name.LocalName))
                || !string.IsNullOrWhiteSpace(element.Value)) throw new FormatException("UnknownSettlementField");
        }
        public static SettlementLedgerLoad Decode(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return new SettlementLedgerLoad(new FundedSettlementLedger());
            try
            {
                using (var reader = XmlReader.Create(new StringReader(payload), new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16000000 }))
                {
                    var root = XElement.Load(reader);
                    if (root.Name != "SettlementLedger") throw new FormatException("InvalidSettlementRoot");
                    if ((int)root.Attribute("schema") != CurrentSchema) return new SettlementLedgerLoad(new FundedSettlementLedger(), "UnsupportedSettlementSchema", true);
                    if (root.Attributes().Any(x => x.Name.NamespaceName.Length != 0 || !new[] { "schema", "version", "fault", "archive" }.Contains(x.Name.LocalName))
                        || root.Elements().Any(x => x.Name != "Command" && x.Name != "Account") || root.Nodes().OfType<XText>().Any(x => !string.IsNullOrWhiteSpace(x.Value)))
                        throw new FormatException("UnknownSettlementField");
                    var nodes = root.Elements("Command").Take(FundedSettlementLedger.MaximumEntries + 1).ToArray();
                    if (nodes.Length > FundedSettlementLedger.MaximumEntries) throw new FormatException("SettlementCapacity");
                    var rows = new List<SettlementEntry>();
                    foreach (var node in nodes)
                    {
                        var kind = (SettlementKind)Enum.Parse(typeof(SettlementKind), (string)node.Attribute("kind"));
                        if (!Enum.IsDefined(typeof(SettlementKind), kind)) throw new FormatException("UnknownSettlementKind");
                        string id = (string)node.Attribute("id"); long expected = (long)node.Attribute("expected");
                        SettlementIntent intent;
                        if (kind == SettlementKind.LegacyAllocation || kind == SettlementKind.WageTransfer || kind == SettlementKind.Purchase || kind == SettlementKind.PublicTransfer)
                        {
                            Fields(node, "id", "expected", "kind", "amount", "phase", "reason", "claim", "target", "payer");
                            intent = new SettlementIntent(id, expected, (string)node.Attribute("claim"), (string)node.Attribute("target"),
                                (long)node.Attribute("amount"), kind, (string)node.Attribute("payer"));
                        }
                        else
                        {
                            Fields(node, "id", "expected", "kind", "amount", "phase", "reason", "reserveVersion", "party", "source", "count");
                            intent = new SettlementIntent(id, expected, new ReserveMovementIntent(id, kind == SettlementKind.Deposit ? ReserveMovementKind.Deposit
                                : ReserveMovementKind.Withdrawal, (long)node.Attribute("reserveVersion"), (string)node.Attribute("party"),
                                (string)node.Attribute("source"), (int)node.Attribute("count")));
                            if (intent.AmountMinor != (long)node.Attribute("amount")) throw new FormatException("SettlementAmountMismatch");
                        }
                        rows.Add(new SettlementEntry(intent, (SettlementPhase)Enum.Parse(typeof(SettlementPhase), (string)node.Attribute("phase")), (string)node.Attribute("reason")));
                    }
                    // FA-01：零余额账户（EnrollPawn）。缺省元素 = 旧档无登记（合法），不因此判坏档。
                    var accountNodes = root.Elements("Account").Take(FundedSettlementLedger.MaximumEnrolledAccounts + 1).ToArray();
                    if (accountNodes.Length > FundedSettlementLedger.MaximumEnrolledAccounts) throw new FormatException("EnrollmentCapacity");
                    var enrollments = new List<string>();
                    foreach (var account in accountNodes)
                    {
                        Fields(account, "id");
                        string accountId = (string)account.Attribute("id");
                        if (string.IsNullOrEmpty(accountId)) throw new FormatException("InvalidEnrolledAccount");
                        enrollments.Add(accountId);
                    }
                    var ledger = FundedSettlementLedger.Restore(rows, enrollments);
                    if (ledger.Version != (long)root.Attribute("version")) throw new FormatException("SettlementVersionMismatch");
                    return new SettlementLedgerLoad(ledger, (string)root.Attribute("fault"), false, (string)root.Attribute("archive"));
                }
            }
            catch (Exception error) when (error is XmlException || error is FormatException || error is ArgumentException
                || error is OverflowException || error is InvalidOperationException || error is NullReferenceException)
            { return new SettlementLedgerLoad(new FundedSettlementLedger(), "InvalidSettlementPayload", true); }
        }
    }
}
