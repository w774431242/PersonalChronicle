using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Care;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Care;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M8 完整闭环（2026-10-10，`CHG-20261010-ECON-M8-FULL`）：审计要求"统一选择活跃授权，并检查真实执行者
    /// 与授权关系，避免错误执行者、旧授权占据扣费对象"。本 fixture 钉住三件事：
    /// ① 唯一活跃选择器的三重过滤（未过期 / 有额度 / 执行者相符）；
    /// ② 证据核验不再"只要存在同类型授权"；
    /// ③ 证据定键式只有一个生产者（应用层与结算侧同源）。
    /// </summary>
    [TestFixture]
    public sealed class CareAuthorisationSelectionTests
    {
        private static CreateCareRequest Request(string id, string beneficiary, string payer, string executor,
            long cap, int created, int expires, ServiceKind kind = ServiceKind.Treatment)
            => new CreateCareRequest
            {
                AuthorizationId = id, Kind = kind, BeneficiaryKey = beneficiary, BeneficiaryIsAdult = true,
                GuardianKey = null, ExecutorKey = executor, PayerKey = payer,
                MaxAmountMinor = cap, CreatedTick = created, ExpiresTick = expires
            };

        [Test] public void OnlyALiveGrantWithBudgetAndTheMatchingExecutorCanBeChosenForCharging()
        {
            var data = new CareSaveData();
            var service = new CareService(data);
            int now = 1000;
            Assert.That(service.Create(Request("care:a", "Human_child", "Human_parent", "Human_nanny", 100, now, now + 500)),
                Is.EqualTo(CareCode.Applied));

            Assert.That(service.SelectChargeable(ServiceKind.Treatment, "Human_child", now + 10, "Human_nanny"),
                Is.Not.Null, "a live grant with budget and the matching executor must be selectable");
            Assert.That(service.SelectChargeable(ServiceKind.Treatment, "Human_child", now + 10, "Human_stranger"),
                Is.Null, "a wrong executor must never occupy the charge target");
            Assert.That(service.SelectChargeable(ServiceKind.Treatment, "Human_child", now + 10, "Human_child"),
                Is.Null, "the beneficiary can never execute against their own grant");
            Assert.That(service.SelectChargeable(ServiceKind.Treatment, "Human_child", now + 600, "Human_nanny"),
                Is.Null, "an expired grant is never selectable");
            Assert.That(service.SelectChargeable(ServiceKind.Treatment, "Human_other", now + 10, "Human_nanny"),
                Is.Null, "another beneficiary never borrows the grant");

            Assert.That(service.Charge("care:a", "Human_parent", 100, now + 20), Is.EqualTo(CareCode.Applied));
            Assert.That(service.SelectChargeable(ServiceKind.Treatment, "Human_child", now + 30, "Human_nanny"),
                Is.Null, "an exhausted grant is never selectable again");
        }

        [Test] public void AnUnnamedExecutorGrantStaysOpenToAnyNonBeneficiary()
        {
            // 未指定执行者 = "不限执行者"（与 CareRules.CanExecute 同一纪律）：不得因此变成"谁都不可用"。
            var data = new CareSaveData();
            var service = new CareService(data);
            int now = 10;
            Assert.That(service.Create(Request("care:open", "Human_child", "Human_parent", null, 50, now, now + 100)),
                Is.EqualTo(CareCode.Applied));
            Assert.That(service.SelectChargeable(ServiceKind.Treatment, "Human_child", now + 1, "Human_anyone"),
                Is.Not.Null, "an unnamed executor means any non-beneficiary");
            Assert.That(service.SelectChargeable(ServiceKind.Treatment, "Human_child", now + 1, "Human_child"),
                Is.Null, "but never the beneficiary themselves");
        }

        [Test] public void CareEvidenceNeedsALiveGrantAndUsesTheSingleKeyProducer()
        {
            var data = new CareSaveData();
            var service = new CareService(data);
            int now = 100;
            Assert.That(service.AcceptCareAct(ServiceKind.Treatment, "Human_doctor", "Human_patient", now),
                Is.EqualTo(CareCode.AuthorizationUnknown), "no grant at all must refuse the evidence");

            Assert.That(service.Create(Request("care:b", "Human_patient", "Human_patient", "Human_doctor", 50, now, now + 10)),
                Is.EqualTo(CareCode.Applied));
            Assert.That(service.AcceptCareAct(ServiceKind.Treatment, "Human_doctor", "Human_patient", now),
                Is.EqualTo(CareCode.Applied));
            Assert.That(service.AcceptCareAct(ServiceKind.Treatment, "Human_doctor", "Human_patient", now),
                Is.EqualTo(CareCode.Replay), "the same act must never be recorded twice");
            Assert.That(service.AcceptCareAct(ServiceKind.Treatment, "Human_stranger", "Human_patient", now),
                Is.EqualTo(CareCode.AuthorizationUnknown), "a wrong executor can never record evidence against this grant");
            Assert.That(service.AcceptCareAct(ServiceKind.Childcare, "Human_doctor", "Human_patient", now),
                Is.EqualTo(CareCode.AuthorizationUnknown), "evidence is scoped to its own service kind");
            Assert.That(service.AcceptCareAct(ServiceKind.Treatment, "Human_doctor", "Human_patient", now + 50),
                Is.EqualTo(CareCode.AuthorizationUnknown), "an expired grant can never authorize new evidence");

            // 定键式单一生产者：应用层静态入口产出的键必须与记录时用的完全一致（两处副本曾是漂移点）。
            Assert.That(CareService.EvidenceIdOf(ServiceKind.Treatment, "Human_patient", "Human_doctor", now),
                Is.EqualTo("care-evidence:Treatment:Human_patient|Human_doctor|" + now.ToString()));
        }
    }
}
