using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy;

namespace PersonalChronicle.Tests
{
    /// <summary>CHG-20261012-ECON-CONTRACTIZE · B1（P1-2）：「工作合同」表**行载荷契约**的离线锁定。
    ///
    /// <para><b>为什么要锁</b>：合同行有两型（Posted 待承接 / Active 承接中），二者共用同一张表的列集。
    /// 格数一旦分叉就是"整列静默串列"或"撤销控件凭空消失"—— 两者都不抛异常、编译与单测也不报错，
    /// 只在屏幕上错位（同一族缺陷在本工程已复现多次）。这里把三条契约钉死：
    /// ① 两型格数 = <see cref="VanillaContractRow.ColumnCount"/>（与读模型列声明同源）；
    /// ② 撤销载荷只落在 Posted 行，且与 <c>Action</c>/<c>Verb</c> 配套（缺一画不出按钮或画出无文案按钮）；
    /// ③ 末列（控件格）恒空 —— 它由呈现层画按钮，写文本会被按钮压住。</para></summary>
    [TestFixture]
    public sealed class VanillaContractRowTests
    {
        private static EconomyRow Posted()
            => VanillaContractRow.Compose("contract:posted", "待承接", "割除", 0, 8, 0L, 0L, 500L,
                "0.00", "0.00", "待承接 · 割除 · 0/8", "合同号: contract:posted", "待承接 contract:posted", "撤销");

        private static EconomyRow Active()
            => VanillaContractRow.Compose("contract:a", "小明", "割除", 3, 8, 1500L, 0L, 500L,
                "15.00", "0.00", "小明 · 割除 · 3/8", "合同号: contract:a", "小明 contract:a", null);

        [Test] public void BothRowKindsCarryExactlyTheDeclaredColumnCount()
        {
            Assert.That(VanillaContractRow.ColumnCount, Is.EqualTo(8), "列集 = 承接人/工种/已完成/总件数/已付/未付/每件/操作");
            Assert.That(Posted().Cells, Has.Length.EqualTo(VanillaContractRow.ColumnCount));
            Assert.That(Active().Cells, Has.Length.EqualTo(VanillaContractRow.ColumnCount));
            Assert.That(Posted().CellSorts, Has.Length.EqualTo(VanillaContractRow.ColumnCount),
                "排序数值必须与列同序同长（缺项会按 0 参与排序，静默给出错误次序）");
            Assert.That(Active().CellSorts, Has.Length.EqualTo(VanillaContractRow.ColumnCount));
            Assert.That(Posted().Cells[VanillaContractRow.ActionColumnIndex], Is.Empty,
                "控件格的文本恒空（按钮由呈现层画）");
            Assert.That(Active().Cells[VanillaContractRow.ActionColumnIndex], Is.Empty);
        }

        [Test] public void OnlyThePostedRowCarriesTheRevocationPayload()
        {
            var posted = Posted();
            Assert.That(posted.Revoke, Is.EqualTo("contract:posted"), "撤销载荷 = 合同号（稳定 ID，不是显示文本）");
            Assert.That(posted.Action, Is.EqualTo("撤销"));
            Assert.That(posted.Verb, Is.EqualTo(VanillaContractRow.RevokeVerb));
            var active = Active();
            Assert.That(active.Revoke, Is.Null, "承接中的合同不可从这张表撤销（解约是另一条线）");
            Assert.That(active.Action, Is.Null, "无撤销载荷就不该有按钮文案（否则画出点了没反应的按钮，F4）");
            Assert.That(active.Verb, Is.Null);
        }

        [Test] public void RowKeySearchAndSortsStayUsableForBothKinds()
        {
            foreach (var row in new[] { Posted(), Active() })
            {
                Assert.That(row.Key, Is.Not.Null.And.Not.Empty, "行键 = 合同号（展开态按它寻址）");
                Assert.That(row.Search, Does.Contain(row.Key), "搜索键必须含合同号（占位符承诺搜索承接人 / 合同号）");
                Assert.That(row.Detail, Is.Not.Null.And.Not.Empty, "展开详情（合同号/签订/到期/时段/岗位键）");
                Assert.That(row.Cells[0], Is.Not.Null.And.Not.Empty, "承接人列：Posted 显示待承接，Active 显示人名");
            }
            var posted = Posted();
            Assert.That(posted.Cells[2], Is.EqualTo("0"), "Posted 已完成 = 0（如实）");
            Assert.That(posted.Cells[3], Is.EqualTo("8"));
            Assert.That(posted.Cells[6], Is.EqualTo("5.00"), "每件薪资按银两位显示");
            Assert.That(posted.CellSorts[3], Is.EqualTo(8L));
            Assert.That(Active().CellSorts[4], Is.EqualTo(1500L), "已付列按**数值**排序（不是显示文本）");
        }
    }
}
