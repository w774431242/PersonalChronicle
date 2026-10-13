using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 名册/岗位绑定纯逻辑离线单测（CHG-20261003-WORKSHOP-SPLIT · 名册模块）。
    ///
    /// <para><b>钉死的核心契约</b>：① 岗位→班次的固定绑定在<b>多次求值下恒定</b>
    /// （这是修成因 A 的关键：绑定若会漂移，作息表就会毫无征兆地跳到另一个班次）；
    /// ② 只被指派 1 个班次时<b>天然固定</b>（零新增存储）；
    /// ③ 绑定一旦持久化就<b>跨存档保持</b>（不被后续重算覆盖）；
    /// ④ 「一人一组」不变式；⑤ 候选池 = 轮转 ∪ 显式指派。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopRosterRulesTests
    {
        private static WorkshopRosterRules.CrewRoster Crew(int fixedId, params int[] members)
        {
            var c = new WorkshopRosterRules.CrewRoster { FixedShiftId = fixedId };
            c.MemberIds.AddRange(members);
            return c;
        }

        [Test]
        public void SingleAssignedShift_IsNaturallyFixed_NoStorageNeeded()
        {
            // 常见情形：班组只被指派一个班次 => 天然固定。
            var crew = Crew(-1, 101);
            Assert.AreEqual(7, WorkshopRosterRules.ResolveFixedShiftId(crew, new List<int> { 7 }),
                "只被指派 1 个班次时，绑定就是那一个（无需持久化）");
        }

        [Test]
        public void MultipleShifts_ResolutionIsDeterministic_AcrossRepeatedCalls()
        {
            // 成因 A 的核心：绑定必须稳定，否则作息会在日与日之间跳。
            var crew = Crew(-1, 101);
            var ids = new List<int> { 3, 9, 5 };   // 已按稳定 ID 升序
            int first = WorkshopRosterRules.ResolveFixedShiftId(crew, ids);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(first, WorkshopRosterRules.ResolveFixedShiftId(crew, ids),
                    "第 " + i + " 次求值必须与第一次相同（绑定漂移 = 作息跳变）");
            }
            Assert.AreEqual(3, first, "兜底取稳定 ID 最小者（GOV-009：不认显示顺序/文本）");
        }

        [Test]
        public void PersistedBinding_SurvivesAndIsNotRecomputed()
        {
            // 玩家/系统此前绑定到 9 ⇒ 后续重算必须沿用，不得被"取最小 ID"覆盖。
            var crew = Crew(9, 101);
            var ids = new List<int> { 3, 9, 5 };
            Assert.AreEqual(9, WorkshopRosterRules.ResolveFixedShiftId(crew, ids),
                "已持久化的绑定必须优先沿用（跨存档稳定）");
        }

        [Test]
        public void StaleBinding_IsIgnored_AndRecomputed()
        {
            // 绑定指向的班次已被删除（ids 不含 9）⇒ 必须重算，不得返回过期 ID。
            var crew = Crew(9, 101);
            var ids = new List<int> { 3, 5 };
            int resolved = WorkshopRosterRules.ResolveFixedShiftId(crew, ids);
            Assert.AreEqual(3, resolved, "过期绑定必须被忽略并重算");
            Assert.IsFalse(ids.Contains(resolved) == false, "结果必须仍指向可用班次");
        }

        [Test]
        public void NoShifts_ReturnsMinusOne_NoPlan()
        {
            var crew = Crew(-1, 101);
            Assert.AreEqual(-1, WorkshopRosterRules.ResolveFixedShiftId(crew, new List<int>()),
                "无可用班次 = 无计划 ⇒ 返回 -1（调用方据此保持玩家手排不动）");
        }

        [Test]
        public void DedupeMemberships_KeepsFirstCrewOnly()
        {
            // 一人同时挂在 A/B 两组是历史脏数据（实机 2026-09-25）。
            var a = Crew(-1, 1, 2);
            var b = Crew(-1, 2, 3);
            var crews = new List<WorkshopRosterRules.CrewRoster> { a, b };
            WorkshopRosterRules.DedupeMemberships(crews);
            CollectionAssert.AreEqual(new[] { 1, 2 }, a.MemberIds, "A 组保留最先编入者");
            CollectionAssert.AreEqual(new[] { 3 }, b.MemberIds, "B 组移除重复的 2");
        }

        [Test]
        public void CandidatePool_IsUnionOfRotationAndExplicitAssignment()
        {
            // 倒班轮转出 {1,2}；显式指派并入 {2,5} ⇒ 并集 {1,2,5}（去重保序）。
            var crew = Crew(-1, 5, 6);
            var result = new List<int>();
            bool ok = WorkshopRosterRules.ResolveCandidatePool(
                new List<int> { 1, 2 }, crew, result);
            Assert.IsTrue(ok);
            CollectionAssert.AreEqual(new[] { 1, 2, 5, 6 }, result,
                "候选池 = 轮转 ∪ 班组全员（显式指派叠加）");
        }

        [Test]
        public void CrewIndexOf_FindsCrew_AndMinusOneWhenAbsent()
        {
            var crews = new List<WorkshopRosterRules.CrewRoster>
            {
                Crew(-1, 10, 11), Crew(-1, 20)
            };
            Assert.AreEqual(0, WorkshopRosterRules.CrewIndexOf(crews, 11));
            Assert.AreEqual(1, WorkshopRosterRules.CrewIndexOf(crews, 20));
            Assert.AreEqual(-1, WorkshopRosterRules.CrewIndexOf(crews, 99), "未编入 = -1");
            Assert.IsTrue(WorkshopRosterRules.IsOnRoster(crews, 10));
            Assert.IsFalse(WorkshopRosterRules.IsOnRoster(crews, 99));
        }
    }
}