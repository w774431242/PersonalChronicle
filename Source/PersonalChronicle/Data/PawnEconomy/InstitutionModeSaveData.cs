using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using PersonalChronicle.Domain.PawnEconomy.Governance;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    internal sealed class InstitutionModeSaveData : IExposable
    {
        private const string EncodingId = "base64-utf8-v1";
        private string wire, encoding, cutoverRaw, originalHash;
        private long sourceRevision, ledgerVersion;
        private long recoveryProvenRevision = -1;
        /// <summary>FA-23：最近一次被**显式重新核验**解除的故障码（持久审计痕迹，可选属性）。</summary>
        private string reverifiedFault;
        private bool frozen, preserveRaw;
        private LegacySettlementArchive cutover;
        public InstitutionModeState State = new InstitutionModeState();
        public bool Frozen => frozen;
        public bool PreserveRaw => preserveRaw;
        /// <summary>
        /// 「完整游戏存档与恢复验收」（B2.4.3）的**真实证据版本**（<c>-1</c> = 尚未证明）。
        /// 由 <see cref="MarkSaveRecoveryProven"/> 在一次干净的存档→恢复往返之后写入；
        /// 它取代了原先的硬编码 <c>false</c>（那会让制度永远停在 Preparing，见变更标注 2026-10-09）。
        /// </summary>
        public long RecoveryProvenRevision => recoveryProvenRevision;
        /// <summary>本次会话是否由**存档恢复**而来。会话内标志、**不落盘**（新开档恒 false）——
        /// 「恢复能力」只能由真的恢复过证明，不能被设置或加载标志冒充。</summary>
        public bool LoadedFromSave { get; private set; }
        /// <summary>
        /// 记录一次**已证明的存档恢复**。判据不在此处（在 Economy 宿主，那里才有 audit/托管账本）；
        /// 本方法只做幂等守门：未冻结/raw 保留/版本回退一律拒绝，重复调用保留最大已证明版本。
        /// </summary>
        public void MarkSaveRecoveryProven(long revision)
        {
            if (!frozen || preserveRaw || revision < 0 || revision <= recoveryProvenRevision) return;
            recoveryProvenRevision = revision;
        }
        /// <summary>
        /// FA-23：显式重新核验解除故障的唯一写入口（判据在 Economy 宿主：只有实时故障源全部消失时才会调用）。
        /// 与"切换设置"严格区分 —— 设置路径永不调用本方法，因此切换开关不可能清故障。
        /// 解除后 phase 落到 Preparing，由同一次 Observe 依真实 readiness 重算；revision 单调 +1 即持久审计痕迹，
        /// 被解除的故障码一并落盘（可选属性，旧档缺失即 null）。
        /// </summary>
        public bool ClearFaultAfterReverification(string clearedFault)
        {
            if (preserveRaw || State.Phase != InstitutionPhase.Faulted || string.IsNullOrEmpty(clearedFault)
                || State.Revision == long.MaxValue) return false;
            reverifiedFault = clearedFault;
            State = new InstitutionModeState(InstitutionPhase.Preparing, State.Desired, State.Revision + 1, null);
            return true;
        }
        /// <summary>最近一次被显式重新核验解除的故障码（null = 从未解除过）。只读展示用。</summary>
        public string ReverifiedFault => reverifiedFault;
        public long SourceRevision => sourceRevision;
        public long LedgerVersionAtFreeze => ledgerVersion;
        public string OriginalHash => originalHash;
        public LegacySettlementArchive ReadCutover() => frozen && !preserveRaw ? cutover ?? (cutover = new LegacySettlementArchive(cutoverRaw, "FrozenAtInstitutionRequest")) : null;
        public void Freeze(string raw, string originalArchiveHash, long revision, long newLedgerVersion)
        {
            if (frozen || preserveRaw) return;
            if (revision < 0 || newLedgerVersion < 0) throw new ArgumentOutOfRangeException();
            cutoverRaw = raw; originalHash = originalArchiveHash; sourceRevision = revision; ledgerVersion = newLedgerVersion;
            frozen = true; cutover = null;
        }
        private string Encode()
            => new XElement("InstitutionMode", new XAttribute("schema", 1), new XAttribute("phase", State.Phase),
                new XAttribute("desired", State.Desired), new XAttribute("revision", State.Revision),
                // 可选属性：未证明时不写（旧档读回即默认 -1），证明后随档持久化。
                recoveryProvenRevision < 0 ? null : new XAttribute("recoveryProvenRevision", recoveryProvenRevision),
                reverifiedFault == null ? null : new XAttribute("reverifiedFault", reverifiedFault),
                State.Fault == null ? null : new XAttribute("fault", State.Fault),
                !frozen ? null : new XElement("FrozenLegacy", new XAttribute("sourceRevision", sourceRevision),
                    new XAttribute("ledgerVersion", ledgerVersion), originalHash == null ? null : new XAttribute("originalHash", originalHash),
                    new XElement("Raw", cutoverRaw == null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(cutoverRaw))))).ToString(SaveOptions.DisableFormatting);
        private static void KnownFields(XElement node, params string[] names)
        {
            if (node.Attributes().Any(x => x.Name.NamespaceName.Length != 0 || !names.Contains(x.Name.LocalName))
                || node.Nodes().OfType<XText>().Any(x => !string.IsNullOrWhiteSpace(x.Value))) throw new FormatException("UnknownModeField");
        }
        private void Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) { State = new InstitutionModeState(); return; }
            using (var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 96000000 }))
            {
                var root = XElement.Load(reader);
                if (root.Name != "InstitutionMode" || (int)root.Attribute("schema") != 1) throw new FormatException("UnsupportedModeSchema");
                KnownFields(root, "schema", "phase", "desired", "revision", "fault", "recoveryProvenRevision", "reverifiedFault");
                if (root.Elements().Any(x => x.Name != "FrozenLegacy") || root.Elements().Count() > 1) throw new FormatException("UnknownModeElement");
                var state = new InstitutionModeState((InstitutionPhase)Enum.Parse(typeof(InstitutionPhase), (string)root.Attribute("phase")),
                    (bool)root.Attribute("desired"), (long)root.Attribute("revision"), (string)root.Attribute("fault"));
                // 可选属性：旧档（无此属性）读出 -1 = 未证明，行为与修复前一致（仍受阻于该 gate）。
                recoveryProvenRevision = (long?)root.Attribute("recoveryProvenRevision") ?? -1;
                reverifiedFault = (string)root.Attribute("reverifiedFault");
                var snapshot = root.Element("FrozenLegacy");
                if (snapshot != null)
                {
                    KnownFields(snapshot, "sourceRevision", "ledgerVersion", "originalHash");
                    if (snapshot.Elements().Count() != 1 || snapshot.Element("Raw") == null || snapshot.Element("Raw").HasAttributes
                        || snapshot.Element("Raw").HasElements) throw new FormatException("InvalidFrozenLegacy");
                    sourceRevision = (long)snapshot.Attribute("sourceRevision"); ledgerVersion = (long)snapshot.Attribute("ledgerVersion");
                    if (sourceRevision < 0 || ledgerVersion < 0) throw new FormatException("InvalidFreezeVersion");
                    originalHash = (string)snapshot.Attribute("originalHash");
                    string raw = snapshot.Element("Raw").Value;
                    cutoverRaw = raw.Length == 0 ? null : new UTF8Encoding(false, true).GetString(Convert.FromBase64String(raw)); frozen = true;
                }
                if (state.Phase == InstitutionPhase.Active && !frozen) throw new FormatException("ActiveWithoutFrozenLegacy");
                // Readiness is a session capability. It is deliberately absent from Save.
                State = state.Phase == InstitutionPhase.Active ? new InstitutionModeState(InstitutionPhase.Preparing, state.Desired,
                    state.Revision == long.MaxValue ? state.Revision : state.Revision + 1) : state;
            }
        }
        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && !preserveRaw)
            { wire = Convert.ToBase64String(Encoding.UTF8.GetBytes(Encode())); encoding = EncodingId; }
            Scribe_Values.Look(ref wire, "payload"); Scribe_Values.Look(ref encoding, "encoding");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                frozen = false; preserveRaw = false; cutover = null; cutoverRaw = null; originalHash = null; sourceRevision = ledgerVersion = 0;
                recoveryProvenRevision = -1;
                reverifiedFault = null;
                // 走到这里就说明本次会话确实从存档恢复过 —— 这是「恢复能力」唯一的证据来源。
                LoadedFromSave = true;
                try
                {
                    if (encoding != null && encoding != EncodingId) throw new FormatException("UnsupportedModeEncoding");
                    Decode(encoding == null ? wire : wire == null ? null : new UTF8Encoding(false, true).GetString(Convert.FromBase64String(wire)));
                }
                catch (Exception error) when (error is XmlException || error is FormatException || error is ArgumentException
                    || error is OverflowException || error is InvalidOperationException || error is NullReferenceException)
                {
                    preserveRaw = true; State = new InstitutionModeState(InstitutionPhase.Faulted, false, 0, "UnknownInstitutionSave");
                }
            }
        }
    }
}
