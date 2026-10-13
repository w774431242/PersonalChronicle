using NUnit.Framework;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Care;

namespace PersonalChronicle.Tests
{
    /// <summary>U6-A4：照护授权到期裁剪（250 tick 调用点）的可证伪边界 —— 判据必须与
    /// <c>CareAuthorization.IsExpired</c>（`now &gt; ExpiresTick`）逐字一致，否则"读时过期、表里不清"
    /// 会让持久化表被历史行填满并挤压有效授权。</summary>
    [TestFixture]
    public sealed class CareExpiryTrimTests
    {
        private static CareAuthorization Grant(string id, int created, int expires, long max = 1000, long charged = 0)
            => new CareAuthorization(id, ServiceKind.Treatment, "pawn:A", null, null, "pawn:A", max, created, expires, 1, charged);

        [Test] public void OnlyExpiredRowsAreTrimmedAndTheBoundaryIsExclusive()
        {
            var data = new CareSaveData();
            Assert.That(data.WriteAuthorization(Grant("care:expired", 100, 200)), Is.True);
            Assert.That(data.WriteAuthorization(Grant("care:boundary", 100, 300)), Is.True);
            Assert.That(data.WriteAuthorization(Grant("care:live", 100, 1000)), Is.True);
            Assert.That(data.AuthorizationCount, Is.EqualTo(3));

            // 恰好在到期 tick 上：IsExpired 是 `now > ExpiresTick` ⇒ 等值**不算**过期，必须保留
            Assert.That(data.TrimExpired(300), Is.EqualTo(1), "只删真正过期的那一行");
            Assert.That(data.ReadAuthorization("care:expired"), Is.Null);
            Assert.That(data.ReadAuthorization("care:boundary"), Is.Not.Null, "等值边界不得被裁掉（与 IsExpired 逐字一致）");
            Assert.That(data.ReadAuthorization("care:live"), Is.Not.Null);
            Assert.That(data.AuthorizationCount, Is.EqualTo(2));
        }

        [Test] public void TrimmingIsIdempotentAndNeverTouchesLiveRowsOnATickRollback()
        {
            var data = new CareSaveData();
            Assert.That(data.WriteAuthorization(Grant("care:a", 500, 600)), Is.True);
            Assert.That(data.TrimExpired(1000), Is.EqualTo(1));
            Assert.That(data.TrimExpired(1000), Is.EqualTo(0), "第二次调用零删除（幂等）");
            // 反向对照：tick 回退（读档/时间回退）不得让**任何**行过期
            Assert.That(data.WriteAuthorization(Grant("care:b", 500, 600)), Is.True);
            Assert.That(data.TrimExpired(10), Is.EqualTo(0), "时间回退不产生过期");
            Assert.That(data.AuthorizationCount, Is.EqualTo(1));
        }

        [Test] public void ChargedRowsAreTrimmedByTheSameTimeRule()
        {
            // 已扣费的过期授权同样应被清掉：它是历史凭据而非活跃额度（额度只影响新扣费，判定见 CanCharge）
            var data = new CareSaveData();
            Assert.That(data.WriteAuthorization(Grant("care:paid", 100, 200, max: 1000, charged: 400)), Is.True);
            Assert.That(data.TrimExpired(500), Is.EqualTo(1));
            Assert.That(data.AuthorizationCount, Is.Zero);
        }
    }
}
