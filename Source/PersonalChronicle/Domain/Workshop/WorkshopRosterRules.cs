using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 名册判定：谁属于哪个班组 / 某人在某小时是否在编（**纯函数，零 Verse 依赖**）。
    ///
    /// <para><b>模块边界（GOV-004 · 2026-10-03 三模块拆分 · 名册侧）</b>：
    /// 本类只回答"编制归属"这一类<b>只读</b>问题，<b>不写任何引擎状态</b>、
    /// 不认识 Pawn / Zone / JobDriver / UI。引擎侧负责把 Pawn 投影成这里的
    /// <see cref="RosterEntry"/> / <see cref="CrewRoster"/>，再把判定结果翻译回去。</para>
    ///
    /// <para><b>为什么值得拆出来</b>：拆分前名册判定（谁在编、谁该上岗、候选池怎么解析）
    /// 全部埋在 MapComponent_WorkshopManager.Schedule.cs 里，<b>与 Verse 类型纠缠在
    /// 同一方法体内</b> =&gt; 无法离线单测 —— 这正是"同一缺陷连续三次只能靠实机发现"的
    /// 结构性根因（判定与 Verse 对象图耦合 =&gt; 构造不出最小复现）。
    /// 拆成纯函数后，"多班次班组被并集写坏 21 小时 Work"这类问题在单测里一行即可钉死。</para>
    ///
    /// <para><b>标识口径</b>：本层用 int 作人员标识（引擎侧投影自 Pawn.thingIDNumber），
    /// <b>不引入 Verse 类型</b>；这也让单测无需构造 Pawn。</para>
    /// </summary>
    public static class WorkshopRosterRules
    {
        /// <summary>名册条目（人员标识 + 可派工标记；<b>无引擎类型</b>）。</summary>
        public struct RosterEntry
        {
            /// <summary>人员稳定标识（引擎侧 = Pawn.thingIDNumber；负值 = 无效条目）。</summary>
            public int PawnId;

            /// <summary>是否可参与派工（在编/在岗可用；由引擎侧投影，判定逻辑在本层）。</summary>
            public bool Eligible;

            public RosterEntry(int pawnId, bool eligible)
            {
                PawnId = pawnId;
                Eligible = eligible;
            }
        }

        /// <summary>班组编制（成员标识 + 被指派的班次标识；<b>无引擎类型</b>）。</summary>
        public sealed class CrewRoster
        {
            /// <summary>班组内成员标识（可能含无效/重复项，判定时跳过）。</summary>
            public readonly List<int> MemberIds = new List<int>();

            /// <summary>该班组被<b>显式指派</b>的班次标识（空 = 仅按倒班表轮转）。</summary>
            public readonly List<int> AssignedShiftIds = new List<int>();

            /// <summary>岗位→班次<b>固定绑定</b>（GOV-009 稳定标识；负值 = 未绑定）。</summary>
            public int FixedShiftId = -1;

            public int Headcount
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < MemberIds.Count; i++)
                    {
                        if (MemberIds[i] >= 0)
                        {
                            n++;
                        }
                    }
                    return n;
                }
            }
        }

        /// <summary>某人员命中的班组序号；<c>-1</c> = 未编入任何班组。</summary>
        public static int CrewIndexOf(IList<CrewRoster> crews, int pawnId)
        {
            if (crews == null || pawnId < 0)
            {
                return -1;
            }
            for (int c = 0; c < crews.Count; c++)
            {
                CrewRoster crew = crews[c];
                if (crew == null)
                {
                    continue;
                }
                for (int i = 0; i < crew.MemberIds.Count; i++)
                {
                    if (crew.MemberIds[i] == pawnId)
                    {
                        return c;
                    }
                }
            }
            return -1;
        }

        /// <summary>该班组是否被显式指派了此班次。</summary>
        public static bool IsShiftAssigned(CrewRoster crew, int shiftId)
        {
            return crew != null && shiftId >= 0 && crew.AssignedShiftIds != null
                && crew.AssignedShiftIds.Contains(shiftId);
        }
        /// <summary>
        /// <b>岗位→班次的固定绑定解析</b>（修成因 A 的核心纯函数）。
        ///
        /// <para>语义：一个人被指派多个班次时，若采用"固定绑定"策略，则他的作息时段
        /// 必须<b>永久绑定同一个班次</b>，而不是每天轮换 —— 否则 Work 小时在日与日之间
        /// 翻转约 12 小时，睡眠时段随之剧烈跳变（用户实机确认的成因 A）。</para>
        ///
        /// <para><b>取法（三级降级，全部确定性 =&gt; 同一存档反复求值恒等）</b>：</para>
        /// <list type="number">
        /// <item>该班组只被指派 <b>1 个</b>班次 =&gt; 返回它（<b>常见情形，零新增存储</b>）；</item>
        /// <item>已持久化的 <see cref="CrewRoster.FixedShiftId"/> 仍指向某个被指派班次 =&gt; 返回它
        /// （此前绑定的选择，跨存档保持）；</item>
        /// <item>都没有 =&gt; 按 <b>班次标识升序取第一个</b>（GOV-009：只认稳定 ID，
        /// 不认显示顺序/文本）。返回值需由调用方<b>回写</b> FixedShiftId 以便持久化。</item>
        /// </list>
        /// </summary>
        /// <param name="crew">班组编制（可为 null）。</param>
        /// <param name="availableShiftIds">
        /// 该班组在本车间可被指派的班次标识（<b>已按稳定 ID 升序</b>；调用方负责排序，
        /// 以便"取第一个"在多存档/多语言下完全一致）。
        /// </param>
        /// <returns>应绑定的班次标识；<c>-1</c> = 无可用班次（无计划）。</returns>
        public static int ResolveFixedShiftId(CrewRoster crew, IList<int> availableShiftIds)
        {
            if (crew == null || availableShiftIds == null || availableShiftIds.Count == 0)
            {
                return -1;
            }
            // ① 唯一被指派班次 => 天然固定，无需存储。
            if (availableShiftIds.Count == 1)
            {
                return availableShiftIds[0];
            }
            // ② 已有绑定且仍有效 => 沿用（跨存档稳定）。
            if (crew.FixedShiftId >= 0 && availableShiftIds.Contains(crew.FixedShiftId))
            {
                return crew.FixedShiftId;
            }
            // ③ 兜底：稳定 ID 最小者（调用方已排序 => 取首元素即确定性结果）。
            return availableShiftIds[0];
        }

        /// <summary>
        /// 清除「一人多组」历史脏数据（<b>纯逻辑版</b>；每人保留最先编入的班组）。
        ///
        /// <para>引擎侧 Zone_Workshop.DedupeCrewMemberships 在读档时调用本函数，
        /// 从而把"一人一组"不变式的判定与引擎对象图解耦（可离线单测）。</para>
        /// </summary>
        public static void DedupeMemberships(IList<CrewRoster> crews)
        {
            if (crews == null)
            {
                return;
            }
            var seen = new HashSet<int>();
            for (int c = 0; c < crews.Count; c++)
            {
                CrewRoster crew = crews[c];
                if (crew == null)
                {
                    continue;
                }
                for (int i = crew.MemberIds.Count - 1; i >= 0; i--)
                {
                    int id = crew.MemberIds[i];
                    if (id < 0 || !seen.Add(id))
                    {
                        crew.MemberIds.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>该人员当前是否<b>在编</b>（命中任一班组）。</summary>
        public static bool IsOnRoster(IList<CrewRoster> crews, int pawnId)
        {
            return CrewIndexOf(crews, pawnId) >= 0;
        }

        /// <summary>
        /// 候选池解析：把"倒班表轮转"与"显式指派"合成一份人员标识列表
        /// （<b>纯逻辑，无引擎类型</b>；去重且保持确定性顺序）。
        /// </summary>
        /// <param name="rotatedIds">倒班表轮转出的成员标识（可为 null = 无）。</param>
        /// <param name="crew">班组编制（用于取显式指派并集）。</param>
        /// <param name="result">输出：合并去重后的成员标识列表。</param>
        /// <returns>候选池是否非空。</returns>
        public static bool ResolveCandidatePool(IList<int> rotatedIds, CrewRoster crew, List<int> result)
        {
            if (result == null)
            {
                return false;
            }
            result.Clear();
            if (rotatedIds != null)
            {
                for (int i = 0; i < rotatedIds.Count; i++)
                {
                    AddUnique(result, rotatedIds[i]);
                }
            }
            // 显式指派并集：与倒班表叠加（二者都满足者优先），故取并集而非交集。
            if (crew != null && crew.MemberIds != null)
            {
                for (int i = 0; i < crew.MemberIds.Count; i++)
                {
                    AddUnique(result, crew.MemberIds[i]);
                }
            }
            return result.Count > 0;
        }

        private static void AddUnique(List<int> list, int value)
        {
            if (value >= 0 && !list.Contains(value))
            {
                list.Add(value);
            }
        }
    }
}