using NUnit.Framework;
using PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 总控中心「接入状态」三态判定的离线单测。
    ///
    /// <para>只测 <see cref="SystemCenterReadModelProvider.ResolveWiring"/> 这一纯函数
    /// （不触碰 DefDatabase / Provider）：状态文案与徽标配色都挂在它的结果上，
    /// 一旦判定顺序漂移（例如「预留」被 Provider 有无覆盖），表现就是观测面开始说谎
    /// —— 把未接入的中心标成「运行中」—— 属于必须钉死的语义。</para>
    /// </summary>
    [TestFixture]
    public sealed class SystemCenterWiringTests
    {
        [Test]
        public void ResolveWiring_ReservedWins_EvenWithProvider()
        {
            Assert.AreEqual(SystemCenterWiring.Reserved,
                SystemCenterReadModelProvider.ResolveWiring(true, true),
                "预留中心即便有 Provider 也按「预留」显示（真不一致由启动期校验报警）");
            Assert.AreEqual(SystemCenterWiring.Reserved,
                SystemCenterReadModelProvider.ResolveWiring(true, false));
        }

        [Test]
        public void ResolveWiring_NonReservedWithProvider_IsRunning()
        {
            Assert.AreEqual(SystemCenterWiring.Running,
                SystemCenterReadModelProvider.ResolveWiring(false, true));
        }

        [Test]
        public void ResolveWiring_NonReservedWithoutProvider_IsNotWired()
        {
            Assert.AreEqual(SystemCenterWiring.NotWired,
                SystemCenterReadModelProvider.ResolveWiring(false, false),
                "模块已存在但未接入中心框架 = 数据未接入（合法状态，不是缺陷）");
        }

        [Test]
        public void CardView_DefaultWiring_DoesNotClaimRunning()
        {
            SystemCenterCardView card = new SystemCenterCardView();
            Assert.AreNotEqual(SystemCenterWiring.Running, card.Wiring,
                "默认状态不得是「运行中」：漏赋值时宁可说未接入，也不能凭空宣称在跑");
        }
    }
}
