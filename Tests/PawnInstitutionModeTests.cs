using System;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Governance;
using PersonalChronicle.Automation;
using PersonalChronicle.Data;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Governance;
using PersonalChronicle.Economy.PawnEconomy;
using PersonalChronicle.Settings;
using Verse;

namespace PersonalChronicle.Tests
{
    public sealed partial class PawnSilverReserveTests
    {
        private InstitutionModeSaveData RoundTripMode(InstitutionModeSaveData data, Action<XElement> edit = null)
        {
            string path = PathForTest(); Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref data, "mode"); Scribe.saver.FinalizeSaving();
            if (edit != null) { var file = XDocument.Load(path); edit(file.Root.Element("mode")); file.Save(path); }
            InstitutionModeSaveData loaded = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "mode"); Scribe.loader.FinalizeLoading();
            Assert.That(loaded, Is.Not.Null); return loaded;
        }
        // CHG-20261010-ECON-DEFAULT-ON（D2=A，用户需求「经济系统默认启用」）：本测试取代 B2.4.1 的
        // `B241_SettingsDefaultFalseAndActualScribePreservesExplicitIntent` —— 契约的**默认值**由
        // false 改为 true（开箱即用）。契约的**另一半**逐字保留并加强：Scribe 必须保真玩家显式选择，
        // 且缺键时回落字段初值。默认值翻转为 true 后，"显式关闭不被默认覆盖"才是真正的风险面，
        // 故断言方向随之翻转（原测试验显式 true，本测试验显式 false）。
        [Test] public void CHG20261010_SettingsDefaultTrueAndScribePreservesExplicitIntent()
        {
            var settings = new ChronicleSettings(); Assert.That(settings.EnablePawnEconomicInstitution, Is.True);
            settings.EnablePawnEconomicInstitution = false; string path = PathForTest();
            Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref settings, "settings"); Scribe.saver.FinalizeSaving();
            ChronicleSettings loaded = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "settings"); Scribe.loader.FinalizeLoading();
            Assert.That(loaded, Is.Not.Null); Assert.That(loaded.EnablePawnEconomicInstitution, Is.False);
            var xml = XDocument.Load(path); xml.Root.Element("settings").Element("enablePawnEconomicInstitution").Remove(); xml.Save(path);
            loaded = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "settings"); Scribe.loader.FinalizeLoading();
            Assert.That(loaded.EnablePawnEconomicInstitution, Is.True);
        }
        [Test] public void B241_ModeSaveMissingWireDefaultsOffWithoutFrozenRights()
        {
            string path = PathForTest(); new XDocument(new XElement("savegame", new XElement("mode"))).Save(path);
            InstitutionModeSaveData loaded = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "mode"); Scribe.loader.FinalizeLoading();
            Assert.That(loaded.State.Phase, Is.EqualTo(InstitutionPhase.Off)); Assert.That(loaded.State.Desired, Is.False); Assert.That(loaded.Frozen, Is.False);
        }
        [Test] public void B241_LoadedActiveBecomesPreparingAndCannotKeepRuntimeReadiness()
        {
            var data = new InstitutionModeSaveData(); data.Freeze("", "original-hash", 1, 2);
            data.State = new InstitutionModeState(InstitutionPhase.Active, true, 9);
            var loaded = RoundTripMode(data); Assert.That(loaded.State.Phase, Is.EqualTo(InstitutionPhase.Preparing));
            Assert.That(loaded.State.Desired, Is.True); Assert.That(loaded.State.Revision, Is.EqualTo(10));
            var runtime = new GameComponent_PawnEconomy(null); SetField(runtime, "institutionMode", loaded);
            Assert.That(runtime.QueryInstitutionMode().NewCommitmentsAllowed, Is.False); Assert.That(runtime.QueryInstitutionMode().LegacyFeesAllowed, Is.False);
            // A contradictory saved Active+fault must preserve the faulted wire, not erase the fault during revalidation.
            var contradictory = RoundTripMode(data, node =>
            {
                var root = XElement.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(node.Element("payload").Value)));
                root.SetAttributeValue("fault", "deficit"); WriteWireText(node, "payload", root.ToString(SaveOptions.DisableFormatting));
            });
            Assert.That(contradictory.State.Phase, Is.EqualTo(InstitutionPhase.Faulted)); Assert.That(contradictory.PreserveRaw, Is.True);
        }
        [Test] public void B241_FrozenLatestLedgerPreservesDifferentOriginalAndIsIdempotent()
        {
            var original = LegacyRights(); var component = new GameComponent_PawnEconomy(null);
            var funds = Field<PawnSettlementSaveHost>(component, "settlement"); string before = PawnEconomySaveData.Encode(original);
            funds.CaptureLegacy(before, "BeforeLegacyRecovery");
            original.All<EconomyReceipt>().First(x => x.Type == "Wage").Paid = true; original.Revision++;
            Field<PawnEconomySaveData>(component, "save").State = original;
            component.ApplyInstitutionModeSetting(true); var data = Field<InstitutionModeSaveData>(component, "institutionMode");
            Assert.That(data.Frozen, Is.True); Assert.That(data.ReadCutover().Raw, Is.Not.EqualTo(before)); Assert.That(funds.ReadLegacyArchive().Raw, Is.EqualTo(before));
            Assert.That(data.ReadCutover().Report.UnpaidWagesMinor, Is.Zero); Assert.That(funds.ReadLegacyArchive().Report.UnpaidWagesMinor, Is.EqualTo(500));
            string latest = data.ReadCutover().Raw; component.ApplyInstitutionModeSetting(false); component.ApplyInstitutionModeSetting(true);
            Assert.That(data.ReadCutover().Raw, Is.EqualTo(latest)); Assert.That(component.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Draining));
            Assert.That(funds.ReadAccount("Treasury").BalanceMinor, Is.Zero);
            var loaded = RoundTripMode(data); Assert.That(loaded.ReadCutover().Raw, Is.EqualTo(latest)); Assert.That(loaded.OriginalHash, Is.EqualTo(funds.ReadLegacyArchive().Hash));
        }
        [Test] public void B241_EmptyLegacyAndZeroLedgerExemptsTheRecoveryGate()
        {
            // CHG-20261012-INSTGATE-EXEMPT（2026-10-12 用户裁决 + EXC-2026-031）：断言语义按新口径订正 ——
            // `SaveRecoveryVerified` 的证明对象是"制度开启时**已存在**的旧账/账本内容"。空旧账 + 冻结时账本 0 条
            // ⇒ 没有可被恢复保护的既得资金 ⇒ 该门判 **N/A**，不再要求"先存读档一次"（原断言编码的是旧口径）。
            var component = new GameComponent_PawnEconomy(null); component.ApplyInstitutionModeSetting(true);
            var view = component.QueryInstitutionMode();
            Assert.That(view.Blockers, Does.Not.Contain("SaveAcceptance"), "无旧账可恢复 ⇒ 该门不适用，不列在待完成项");
            Assert.That(view.Phase, Is.EqualTo(InstitutionPhase.Active), "其余六门成立 ⇒ 制度开放（新殖民地可直接接合同）");
            Assert.That(view.NewCommitmentsAllowed, Is.True);
            // ★ 牙齿：本档**没有任何恢复证据**（从未读档往返）却已开放 ⇒ 使制度 Active 的只能是"豁免面"判定，
            // 而不是被伪造/被顺带写入的证据（撤掉 `nothingToRecover` 即回 Preparing，见下一用例的反证）。
            Assert.That(Field<InstitutionModeSaveData>(component, "institutionMode").RecoveryProvenRevision, Is.EqualTo(-1),
                "不得为让门变绿而写伪证据：本档仍应无恢复证明");
            // 封印不放宽：旧收费路径仍恒 false（与本轮门禁收窄无关）。
            Assert.That(view.LegacyFeesAllowed, Is.False);
            component.ApplyInstitutionModeSetting(false); Assert.That(component.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Off));
        }
        [Test] public void SaveRecoveryGateStaysClosedWithoutARealRoundTripAndOpensAfterOne()
        {
            // 回归锁（2026-10-09，CHG-20261009-ECON-SAVEACCEPT；2026-10-12 按豁免面订正前置夹具）：
            // 该 gate 原为**硬编码 false** ⇒ 制度永远停在 Preparing，任何设置开关都推不动。
            // 现由**真实存档恢复证据**驱动 —— 有可恢复内容的档未往返仍阻塞；一次干净恢复即解锁并可进 Active。
            // ① 有旧账（启用且注资的信封 + 记录 + 未付工资）⇒ **不放宽**：
            var legacy = new GameComponent_PawnEconomy(null);
            Field<PawnEconomySaveData>(legacy, "save").State = LegacyRights();
            legacy.ApplyInstitutionModeSetting(true);
            Assert.That(legacy.QueryInstitutionMode().Blockers, Does.Contain("SaveAcceptance"),
                "有旧账的档仍必须完成一次真实往返证明（豁免面不含该档）");
            Assert.That(legacy.QueryInstitutionMode().NewCommitmentsAllowed, Is.False);
            // ② 旧账为空但**冻结时账本已有条目**（一次真实实物入库）⇒ 同样不豁免（豁免面界定）：
            var funded = new GameComponent_PawnEconomy(null);
            SetField(funded, "settlement", HostFunded(200));
            funded.ApplyInstitutionModeSetting(true);
            Assert.That(funded.QueryInstitutionMode().Blockers, Is.EquivalentTo(new[] { "SaveAcceptance" }),
                "冻结时账本非空 ⇒ 仍欠一次存档→恢复往返");
            Assert.That(funded.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Preparing));
            Assert.That(funded.QueryInstitutionMode().NewCommitmentsAllowed, Is.False);

            // 一次**真实** Scribe 往返 = 真的恢复过一次（走实际存读档管线，不是模拟标志）。
            var component = new GameComponent_PawnEconomy(null); component.ApplyInstitutionModeSetting(true);
            var data = Field<InstitutionModeSaveData>(component, "institutionMode");
            Assert.That(data.Frozen, Is.True, "the fixture must freeze the legacy ledger first");
            var loaded = RoundTripMode(data);
            Assert.That(loaded.LoadedFromSave, Is.True, "a Scribe round trip must mark the session as recovered from save");

            // 把恢复出的制度节点装回宿主 = 读档后首次观察（宿主其余节点保持冻结时的状态）。
            SetField(component, "institutionMode", loaded);
            component.ApplyInstitutionModeSetting(true);
            var view = component.QueryInstitutionMode();
            Assert.That(view.Blockers, Is.Empty, "a clean recovery must satisfy the save/recovery gate");
            Assert.That(view.Phase, Is.EqualTo(InstitutionPhase.Active), "with every gate proven the institution reaches Active");
            Assert.That(view.NewCommitmentsAllowed, Is.True, "Active is what finally authorises new commitments");
            Assert.That(loaded.RecoveryProvenRevision, Is.GreaterThanOrEqualTo(0), "the proven revision must persist for the next save");

            // 证据必须随档持久化：再次往返后仍然成立（不依赖"本次会话恰好恢复过"）。
            var persisted = RoundTripMode(loaded);
            Assert.That(persisted.RecoveryProvenRevision, Is.GreaterThanOrEqualTo(0), "the proof must survive a second save");
            Assert.That(persisted.Frozen, Is.True);
        }

        [Test] public void SaveProofIsRejectedWhenTheLedgerIsNotFrozenOrTheRevisionGoesBackwards()
        {
            // 幂等与守门：未冻结 / 版本回退 一律不得写入证明（否则 gate 会被一条无证据的调用撬开）。
            var unfrozen = new InstitutionModeSaveData();
            unfrozen.MarkSaveRecoveryProven(3);
            Assert.That(unfrozen.RecoveryProvenRevision, Is.EqualTo(-1), "an unfrozen ledger must not record a proof");
            var frozen = new InstitutionModeSaveData(); frozen.Freeze("", "hash", 1, 2);
            frozen.MarkSaveRecoveryProven(5); frozen.MarkSaveRecoveryProven(2);
            Assert.That(frozen.RecoveryProvenRevision, Is.EqualTo(5), "only the highest proven revision is kept");
        }

        [Test] public void B241_SwitchOffKeepsDrainingForOldRightsButNotForRetainedFunds()
        {
            var component = new GameComponent_PawnEconomy(null); Field<PawnEconomySaveData>(component, "save").State = LegacyRights();
            component.ApplyInstitutionModeSetting(true); component.ApplyInstitutionModeSetting(false);
            Assert.That(component.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Draining)); Assert.That(component.QueryInstitutionMode().HasOutstandingRights, Is.True);
            // FA-03（2026-10-10 审计）：**正常托管余额不是未完成事项** —— 只有资金、无旧权益、无在途承诺时，
            // 关闭后必须离开 Active 且**不得**因余额非零而卡在 Draining（原断言编码的正是该缺陷）。
            var another = new GameComponent_PawnEconomy(null); SetField(another, "settlement", HostFunded());
            another.ApplyInstitutionModeSetting(true); another.ApplyInstitutionModeSetting(false);
            Assert.That(another.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Off));
            Assert.That(another.QueryInstitutionMode().HasOutstandingRights, Is.False, "余额与储备不算未完成事项");
            another.ApplyInstitutionModeSetting(true);
            Assert.That(another.QueryInstitutionMode().Phase, Is.Not.EqualTo(InstitutionPhase.Draining),
                "余额保留条件下重新开启不得停留在 Draining（其余门仍由各自证据决定）");
            Assert.That(Field<PawnSettlementSaveHost>(another, "settlement").ReadAccount("Treasury").BalanceMinor, Is.EqualTo(10000));
        }
        [Test] public void B241_LegacyRevisionChangeAfterFreezeFaultsAndCannotBeToggledAway()
        {
            var component = new GameComponent_PawnEconomy(null); Field<PawnEconomySaveData>(component, "save").State = LegacyRights();
            component.ApplyInstitutionModeSetting(true); Field<PawnEconomySaveData>(component, "save").State.Revision++;
            component.ApplyInstitutionModeSetting(true); Assert.That(component.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Faulted));
            component.ApplyInstitutionModeSetting(false); Assert.That(component.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Faulted));
        }
        [TestCase("Enable")][TestCase("Settle")][TestCase("Cancel")][TestCase("Autonomy")][TestCase("Publish")]
        public void B241_LegacyCommandsNeverMutateOrMintRegardlessHistoricalEnabled(string verb)
        {
            var component = new GameComponent_PawnEconomy(null); var state = LegacyRights(); Field<PawnEconomySaveData>(component, "save").State = state;
            string before = PawnEconomySaveData.Encode(state); Assert.That(component.Execute(verb, 3, "pawn", "pawn", state.Revision), Is.False);
            component.CancelJob(3); component.Pulse(3, null, false); Assert.That(component.OrderActive(3), Is.False); Assert.That(component.JobOrderValid(3), Is.False);
            Assert.That(component.DoctorJob(null, null), Is.Null); Assert.That(component.Clinics(null), Is.Empty);
            Assert.That(PawnEconomySaveData.Encode(state), Is.EqualTo(before));
        }
        [Test] public void B241_ProductionHookCannotPayLegacyContractOrCreateReceipt()
        {
            var component = new GameComponent_PawnEconomy(null); var state = LegacyRights(); Field<PawnEconomySaveData>(component, "save").State = state;
            string before = PawnEconomySaveData.Encode(state);
            typeof(GameComponent_PawnEconomy).GetMethod("OnProduction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(component, new object[] { null, null });
            Assert.That(PawnEconomySaveData.Encode(state), Is.EqualTo(before));
        }
        [Test] public void B241_ReadonlyModeQueryIsCachedAndDoesNotFreezeOpenOrChangeLedger()
        {
            var component = new GameComponent_PawnEconomy(null); var view = component.QueryInstitutionMode();
            Assert.That(component.QueryInstitutionMode(), Is.SameAs(view)); Assert.That(view.Phase, Is.EqualTo(InstitutionPhase.Off));
            Assert.That(Field<InstitutionModeSaveData>(component, "institutionMode").Frozen, Is.False);
            Assert.That(Field<PawnEconomySaveData>(component, "save").State.Records, Is.Empty);
            Assert.That(Field<PawnSettlementSaveHost>(component, "settlement").Version, Is.Zero);
        }
        [TestCase("<InstitutionMode schema=\"99\"><Future value=\"保留\" /></InstitutionMode>")]
        [TestCase("<broken>")]
        [TestCase("<InstitutionMode schema=\"1\" phase=\"777\" desired=\"true\" revision=\"0\" />")]
        [TestCase("<InstitutionMode schema=\"1\" phase=\"Off\" desired=\"false\" revision=\"0\" extra=\"保留\" />")]
        [TestCase("<!DOCTYPE x [<!ENTITY e 'x'>]><InstitutionMode schema=\"1\">&e;</InstitutionMode>")]
        public void B241_UnknownModePayloadIsPreservedFaultedAndCannotFreezeOrActivate(string raw)
        {
            var loaded = RoundTripMode(new InstitutionModeSaveData(), node => WriteWireText(node, "payload", raw));
            Assert.That(loaded.State.Phase, Is.EqualTo(InstitutionPhase.Faulted)); Assert.That(loaded.PreserveRaw, Is.True);
            loaded.Freeze("replacement", "hash", 0, 0); Assert.That(loaded.ReadCutover(), Is.Null);
            var again = RoundTripMode(loaded); Assert.That(Field<string>(again, "wire"), Is.EqualTo(Field<string>(loaded, "wire")));
            var runtime = new GameComponent_PawnEconomy(null); SetField(runtime, "institutionMode", again); runtime.ApplyInstitutionModeSetting(true);
            Assert.That(runtime.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Faulted)); Assert.That(runtime.QueryInstitutionMode().NewCommitmentsAllowed, Is.False);
        }
        [TestCase("future-v9", "future-wire")][TestCase("base64-utf8-v1", "%%%")]
        public void B241_UnknownModeEncodingPreservesExactWire(string encoding, string wire)
        {
            var data = RoundTripMode(new InstitutionModeSaveData(), node => { node.Element("encoding").Value = encoding; node.Element("payload").Value = wire; });
            var again = RoundTripMode(data); Assert.That(Field<string>(again, "wire"), Is.EqualTo(wire)); Assert.That(Field<string>(again, "encoding"), Is.EqualTo(encoding));
            Assert.That(again.State.Phase, Is.EqualTo(InstitutionPhase.Faulted));
        }
        [Test] public void B241_FrozenUnknownLegacyTextRetainsMixedNewlinesAndChinese()
        {
            const string raw = "未知权益\nLF\rCR\r\nCRLF<&>"; var data = new InstitutionModeSaveData(); data.Freeze(raw, "origin", 0, 0);
            var loaded = RoundTripMode(data); Assert.That(loaded.ReadCutover().Raw, Is.EqualTo(raw));
            Assert.That(RoundTripMode(loaded).ReadCutover().Raw, Is.EqualTo(raw)); Assert.That(loaded.ReadCutover().Fault, Is.Not.Null);
        }
        [Test] public void B241_WholeComponentKeepsFrozenModeAndOldPendingOrderOnReload()
        {
            var component = new GameComponent_PawnEconomy(null); Field<PawnEconomySaveData>(component, "save").State = LegacyRights(); component.ApplyInstitutionModeSetting(true);
            string snapshot = Field<InstitutionModeSaveData>(component, "institutionMode").ReadCutover().Raw; string path = PathForTest();
            Scribe.saver.InitSaving(path, "savegame"); component.ExposeData(); Scribe.saver.FinalizeSaving();
            var loaded = new GameComponent_PawnEconomy(null); Scribe.loader.InitLoading(path); loaded.ExposeData(); Scribe.loader.FinalizeLoading();
            Scribe.mode = LoadSaveMode.PostLoadInit; loaded.ExposeData(); Scribe.mode = LoadSaveMode.Inactive;
            Assert.That(Field<InstitutionModeSaveData>(loaded, "institutionMode").ReadCutover().Raw, Is.EqualTo(snapshot));
            Assert.That(Field<PawnEconomySaveData>(loaded, "save").State.All<EconomyOrder>().Single().Status, Is.EqualTo(EconomyOrderStatus.Active));
            loaded.ApplyInstitutionModeSetting(false); Assert.That(loaded.QueryInstitutionMode().Phase, Is.EqualTo(InstitutionPhase.Draining));
            Assert.That(Field<PawnEconomySaveData>(loaded, "save").State.All<EconomyAccount>().Single().Reserved, Is.EqualTo(600));
        }
        [Test] public void B241_OneEconomicToggleExistsOnlyInEconomicSettingsTab()
        {
            Assert.That(SettingsCatalog.RowsOf(SettingsTab.Economy).Count(x => x.Kind == SettingKind.Toggle), Is.EqualTo(1));
            Assert.That(SettingsCatalog.RowsOf(SettingsTab.Economy).Single().Id, Is.EqualTo(SettingId.EconomicInstitution));
            foreach (SettingsTab tab in new[] { SettingsTab.Archive, SettingsTab.Workshop, SettingsTab.Ui, SettingsTab.Diag })
                Assert.That(SettingsCatalog.RowsOf(tab).Any(x => x.Id == SettingId.EconomicInstitution), Is.False);
        }
        [Test] public void B241_LegacyEconomicSlotIsPausedWithoutDeletingItsIdentity()
        {
            var slot = new WorkshopSlot { EconomicContractId = 99 };
            Assert.That(slot.Matches(null, 12), Is.False); Assert.That(slot.EconomicContractId, Is.EqualTo(99));
            string path = PathForTest(); Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref slot, "slot"); Scribe.saver.FinalizeSaving();
            WorkshopSlot loaded = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "slot"); Scribe.loader.FinalizeLoading();
            Assert.That(loaded.EconomicContractId, Is.EqualTo(99));
        }
    }
    [TestFixture]
    public sealed class PawnInstitutionModeRulesTests
    {
        private static InstitutionReadiness Ready(int missing = -1, string fault = null) => new InstitutionReadiness(missing != 0, missing != 1, missing != 2,
            missing != 3, missing != 4, missing != 5, missing != 6, fault);
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)][TestCase(6)]
        public void EveryMissingCapabilityIndividuallyBlocksActive(int missing)
        {
            var service = new InstitutionModeService(new InstitutionModeState()); service.Observe(true, Ready(missing), false);
            Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Preparing)); Assert.That(service.Read(Ready(missing), false).NewCommitmentsAllowed, Is.False);
        }
        [Test] public void FullyVerifiedPureFixtureCanActivateButNeverEnablesLegacyRoute()
        {
            var service = new InstitutionModeService(new InstitutionModeState()); service.Observe(true, Ready(), false);
            Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Active)); Assert.That(service.Read(Ready(), false).NewCommitmentsAllowed, Is.True);
            Assert.That(service.Read(Ready(), false).LegacyFeesAllowed, Is.False);
        }
        [Test] public void DisabledIntentCannotActivateEvenWithAllCapabilities()
        { var service = new InstitutionModeService(new InstitutionModeState()); service.Observe(false, Ready(), false); Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Off)); }
        [Test] public void OutstandingRightsDrainBeforeReenableAndFinishOnlyWithClearEvidence()
        {
            var service = new InstitutionModeService(new InstitutionModeState()); service.Observe(true, Ready(), false);
            service.Observe(false, Ready(), true); Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Draining));
            service.Observe(true, Ready(), true); Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Draining));
            service.Observe(true, Ready(), false); Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Active));
            service.Observe(false, Ready(), false); Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Off));
        }
        [Test] public void LostCapabilityClosesActiveImmediatelyAndDrainsExistingRights()
        {
            var service = new InstitutionModeService(new InstitutionModeState()); service.Observe(true, Ready(), false);
            Assert.That(service.Read(Ready(3), true).NewCommitmentsAllowed, Is.False);
            service.Observe(true, Ready(3), true); Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Draining));
        }
        [Test] public void FaultIsStickyAcrossToggleAndReadinessRepair()
        {
            var service = new InstitutionModeService(new InstitutionModeState()); service.Observe(true, Ready(fault: "deficit"), true);
            service.Observe(false, Ready(), false); service.Observe(true, Ready(), false);
            Assert.That(service.State.Phase, Is.EqualTo(InstitutionPhase.Faulted)); Assert.That(service.State.Fault, Is.EqualTo("deficit"));
        }
        [Test] public void RepeatedObservationIsIdempotentAndRevisionExhaustionCannotWrap()
        {
            var service = new InstitutionModeService(new InstitutionModeState()); service.Observe(true, Ready(3), false); var original = service.State;
            service.Observe(true, Ready(3), false); Assert.That(service.State, Is.SameAs(original));
            var exhausted = new InstitutionModeService(new InstitutionModeState(InstitutionPhase.Off, false, long.MaxValue)); exhausted.Observe(true, Ready(), false);
            Assert.That(exhausted.State.Phase, Is.EqualTo(InstitutionPhase.Faulted)); Assert.That(exhausted.State.Revision, Is.EqualTo(long.MaxValue));
        }
    }
}
