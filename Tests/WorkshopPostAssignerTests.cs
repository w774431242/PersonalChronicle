using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 岗位自动分配（CHG-20261003-POSTS · Phase 1）离线单测。
    ///
    /// <para>钉死的契约：① 技能/能力门槛不足则出局 ② 评分 = 该人在岗位所承接的每个原版
    /// 工作类型上的档位之和 ③ 专岗（MaxHolders=1）取最高分 ④ <b>同分必须取候选人索引更小者</b>
    /// （稳定性 —— 否则岗位会随集合增删来回跳人，这是本工程反复踩过的坑）
    /// ⑤ 空输入不抛异常、返回 0。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopPostAssignerTests
    {
        private static WorkshopPostAssigner.Candidate Candidate(string id, int craft, int smith)
        {
            var c = new WorkshopPostAssigner.Candidate { Id = id };
            c.Skills["Crafting"] = craft;
            c.Skills["Smithing"] = smith;
            c.Capacities["Manipulation"] = true;
            c.Capacities["Moving"] = true;
            return c;
        }

        private static WorkshopPostAssigner.PostSpec Post(string name, int maxHolders,
            params string[] workTypes)
        {
            var p = new WorkshopPostAssigner.PostSpec { DefName = name, MaxHolders = maxHolders };
            p.CoveredWorkTypeDefNames.AddRange(workTypes);
            return p;
        }

        [Test]
        public void Assign_SinglePost_PicksHighestWorkPriority()
        {
            var low = Candidate("low", 5, 0);
            low.WorkPriorities["Smithing"] = 1;      // 1 档（最高优先）
            var mid = Candidate("mid", 5, 0);
            mid.WorkPriorities["Smithing"] = 3;
            var cands = new List<WorkshopPostAssigner.Candidate> { low, mid };

            var posts = new List<WorkshopPostAssigner.PostSpec> { Post("WP_Smith", 1, "Smithing") };
            int n = WorkshopPostAssigner.Assign(cands, posts, out Dictionary<string, string> result);

            Assert.AreEqual(1, n);
            Assert.AreEqual("low", result["WP_Smith"], "工作档位 1 的人最适合该专岗");
            Assert.AreEqual("WP_Smith", low.CurrentPostDefName);
            Assert.IsEmpty(mid.CurrentPostDefName);
        }

        [Test]
        public void Assign_EqualScores_KeepsCandidateOrder_Stability()
        {
            // 回归钉子：同分时不得因 List.Sort 不稳定而换人。
            var a = Candidate("a", 5, 0);
            a.WorkPriorities["Smithing"] = 2;
            var b = Candidate("b", 5, 0);
            b.WorkPriorities["Smithing"] = 2;
            var cands = new List<WorkshopPostAssigner.Candidate> { a, b };

            var posts = new List<WorkshopPostAssigner.PostSpec> { Post("WP_Smith", 1, "Smithing") };
            WorkshopPostAssigner.Assign(cands, posts, out Dictionary<string, string> first);
            Assert.AreEqual("a", first["WP_Smith"]);

            // 候选人集合在**前面**插入一个同分者 ⇒ 原本的 a 仍应拿到（索引相对序不变则结果不变）。
            var c0 = Candidate("c0", 5, 0);
            c0.WorkPriorities["Smithing"] = 2;
            var cands2 = new List<WorkshopPostAssigner.Candidate> { c0, a, b };
            WorkshopPostAssigner.Assign(cands2, posts, out Dictionary<string, string> second);
            Assert.AreEqual("c0", second["WP_Smith"], "同分取索引更小者（新增者在前）");
        }

        [Test]
        public void Assign_SkillGate_ExcludesInsufficientCandidate()
        {
            // 显式建对象：门槛技能 = Smithing，weak 只有 2（不足 5），strong 有 9。
            var weak = Candidate("weak", 2, 2);
            weak.WorkPriorities["Smithing"] = 1;      // 技能不足但工作档位最高
            var strong = Candidate("strong", 9, 9);
            strong.WorkPriorities["Smithing"] = 3;
            var cands = new List<WorkshopPostAssigner.Candidate> { weak, strong };

            var post = Post("WP_Smith", 1, "Smithing");
            post.SkillDefNames.Add("Smithing");
            post.MinSkillLevel = 5;
            var posts = new List<WorkshopPostAssigner.PostSpec> { post };
            WorkshopPostAssigner.Assign(cands, posts, out Dictionary<string, string> result);

            Assert.AreEqual("strong", result["WP_Smith"], "技能不足者出局，即使工作档位更高");
        }

        [Test]
        public void Assign_CapacityGate_ExcludesIncapableCandidate()
        {
            var crippled = Candidate("crippled", 9, 0);
            crippled.Capacities["Moving"] = false;
            crippled.WorkPriorities["Hauling"] = 1;
            var able = Candidate("able", 3, 0);
            able.WorkPriorities["Hauling"] = 4;
            var cands = new List<WorkshopPostAssigner.Candidate> { crippled, able };

            var post = Post("WP_Haul", 1, "Hauling");
            post.RequiredCapacityDefNames.Add("Moving");
            var posts = new List<WorkshopPostAssigner.PostSpec> { post };
            WorkshopPostAssigner.Assign(cands, posts, out Dictionary<string, string> result);

            Assert.AreEqual("able", result["WP_Haul"], "能力不足者出局");
        }

        [Test]
        public void Assign_MaxHolders_TakesTopN()
        {
            var cands = new List<WorkshopPostAssigner.Candidate>();
            for (int i = 0; i < 4; i++)
            {
                var c = Candidate("c" + i, 5, 0);
                // ⚠ 原版档位 1=最高、4=最低 ⇒ 想让 c0 最高必须用**小**档位。
                c.WorkPriorities["Crafting"] = i + 1;
                cands.Add(c);
            }
            var posts = new List<WorkshopPostAssigner.PostSpec> { Post("WP_Multi", 2, "Crafting") };
            int n = WorkshopPostAssigner.Assign(cands, posts, out Dictionary<string, string> result);

            Assert.AreEqual(2, n, "MaxHolders=2 ⇒ 只取前 2 人");
            Assert.AreEqual("c0,c1", result["WP_Multi"], "取分数最高的两人且保持稳定序");
        }

        [Test]
        public void Assign_EmptyOrNull_ReturnsZero_AndEmptyResult_NoThrow()
        {
            Dictionary<string, string> result;
            Assert.AreEqual(0, WorkshopPostAssigner.Assign(null, null, out result));
            Assert.IsEmpty(result);
            Assert.AreEqual(0, WorkshopPostAssigner.Assign(
                new List<WorkshopPostAssigner.Candidate>(), null, out result));
            Assert.AreEqual(0, WorkshopPostAssigner.Assign(
                new List<WorkshopPostAssigner.Candidate> { Candidate("x", 1, 1) },
                new List<WorkshopPostAssigner.PostSpec>(), out result));
        }

        [Test]
        public void Assign_DefaultIsOnePostPerPerson_NoPersonHoardsEveryPost()
        {
            // 回归钉子（2026-10-03 实机）：3 人 9 岗时，最会干活的那位曾独拿 7 个岗
            // —— MaxHolders 只管"每岗几人"，此前**没有**"每人几岗"约束。
            var cands = new List<WorkshopPostAssigner.Candidate>();
            for (int i = 0; i < 3; i++)
            {
                var c = Candidate("c" + i, 5, 5);
                // 三个人的工作档位**完全相同**（都是最优）⇒ 旧实现会让第一个人被所有岗认领
                c.WorkPriorities["Crafting"] = 1;
                c.WorkPriorities["Smithing"] = 1;
                c.WorkPriorities["Construction"] = 1;
                cands.Add(c);
            }
            var posts = new List<WorkshopPostAssigner.PostSpec>();
            for (int i = 0; i < 9; i++)
            {
                posts.Add(Post("WP_" + i, 1, "Crafting", "Smithing", "Construction"));
            }
            WorkshopPostAssigner.Assign(cands, posts, out Dictionary<string, string> result);

            foreach (WorkshopPostAssigner.Candidate c in cands)
            {
                Assert.LessOrEqual(c.AssignedPostCount, 1,
                    c.Id + " 最多只能担任 1 个主岗（一人多岗是例外而非常态）");
            }
            // 3 人 ⇒ 最多填满 3 个岗，其余 6 岗应显示"暂无合适人选"
            int staffed = 0;
            foreach (var kv in result)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    staffed++;
                }
            }
            Assert.AreEqual(3, staffed, "9 岗 3 人 ⇒ 只应填满 3 岗");
        }

        [Test]
        public void Assign_ExplicitMaxPostsAllowsPartTimePosts()
        {
            // 需要兼岗时可显式放宽（默认 1 不变）
            var cands = new List<WorkshopPostAssigner.Candidate>
            {
                Candidate("only", 9, 9)
            };
            cands[0].WorkPriorities["Crafting"] = 1;
            cands[0].WorkPriorities["Smithing"] = 1;
            var posts = new List<WorkshopPostAssigner.PostSpec>
            {
                Post("WP_A", 1, "Crafting"),
                Post("WP_B", 1, "Smithing")
            };
            WorkshopPostAssigner.Assign(cands, posts, 2, out Dictionary<string, string> result);

            Assert.AreEqual(2, cands[0].AssignedPostCount, "显式传 2 ⇒ 允许兼 2 岗");
            Assert.AreEqual("only", result["WP_A"]);
            Assert.AreEqual("only", result["WP_B"]);
        }    }
}