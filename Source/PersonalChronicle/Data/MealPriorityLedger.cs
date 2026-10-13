using System.Collections.Generic;
using PersonalChronicle.Domain.Meal;
using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// CHG-20261004-MEALSCHEDULE-B · 「吃饭」工作档位的<b>归属台账</b>。
    ///
    /// <para><b>要解决的问题</b>：<c>PC_MealBreak</c> 这一格位现在有<b>两个写入方</b> ——
    /// ① PC 三餐作息规则（餐段边界自动写档）；② 玩家在 Work 表的「用餐」列手动排班。
    /// 玩家口径（2026-10-04 决策）：<b>玩家手动优先 —— 玩家改过就 PC 不再动</b>。
    /// 这与车间既有裁决（<c>WorkshopWorkDemotionLedger</c> + <c>WorkshopTakeover</c> 的
    /// 「当前值仍等于我写的值才认领，否则视为玩家已改动」）<b>同一套语义</b>，
    /// 因此复用同样的台账式判定，而不是另立规则（GOV-005）。</para>
    ///
    /// <para><b>为什么必须落盘</b>：若 lastWritten 只存内存，读档后 PC 无法区分
    /// 「这个 3 是我上次写的」与「这个 3 是玩家自己排的」⇒ 会把玩家手动排班误判成自己的，
    /// 或者反过来该清零时不清零（非餐段仍有档位 ⇒ 空闲就去吃饭）。
    /// 落盘后读档行为与不读档一致（GATE-SAVE）。</para>
    ///
    /// <para><b>数据边界</b>：本类<b>只做判定与记录，不触碰任何 Pawn / Def / UI</b>，
    /// 因此判定体是纯函数（可离线单测）；实际 <c>SetPriority</c> 由
    /// <c>MapComponent_MealSchedule</c> 执行（GOV-003：台账不写 pawn）。</para>
    /// </summary>
    public sealed class MealPriorityLedger : IExposable
    {
        /// <summary>
        /// 存档序列化（加性可选字段：旧档缺键 ⇒ 集合保持初始值，无需迁移）。
        /// </summary>
        public void ExposeData()
        {
            Scribe_Collections.Look(ref PlayerOwnedPawnIds, "playerOwned", LookMode.Value);
            Scribe_Collections.Look(ref LastWrittenByPawnId, "lastWritten", LookMode.Value, LookMode.Value);
            if (PlayerOwnedPawnIds == null)
            {
                PlayerOwnedPawnIds = new HashSet<int>();
            }
            if (LastWrittenByPawnId == null)
            {
                LastWrittenByPawnId = new Dictionary<int, int>();
            }
        }

        /// <summary>玩家已手动接管该殖民者的「用餐」格位（key=pawn.thingIDNumber）。一旦成立即不再自动写。</summary>
        public HashSet<int> PlayerOwnedPawnIds = new HashSet<int>();

        /// <summary>PC 上次写入的档位（key=pawn.thingIDNumber）；-1 = 从未写过。</summary>
        public Dictionary<int, int> LastWrittenByPawnId = new Dictionary<int, int>();

        /// <summary>从未写入过时的哨兵值。</summary>
        public const int NeverWritten = -1;

        /// <summary>
        /// 判定：这次该不该由 PC 写入，以及写入后如何记账。
        /// </summary>
        /// <param name="pawnId">殖民者 thingIDNumber（存档内稳定）。</param>
        /// <param name="currentPriority">该 pawn 当前的实际档位（<c>GetPriority</c> 读回值）。</param>
        /// <param name="desiredPriority">PC 本次想写的档位（0 = 离段清零）。</param>
        /// <returns>是否应当执行写入。</returns>
        public bool ShouldWrite(int pawnId, int currentPriority, int desiredPriority)
        {
            if (pawnId == 0)
            {
                return false;
            }
            // 玩家已接管 ⇒ 永不自动写（即使玩家后来又改回 PC 的值也不夺回，
            // 与车间「PC 不再拥有」一致：撤销由玩家自己用下面的界面完成）。
            if (PlayerOwnedPawnIds.Contains(pawnId))
            {
                return false;
            }

            int last;
            if (!LastWrittenByPawnId.TryGetValue(pawnId, out last) || last == NeverWritten)
            {
                // 从未写过：只有「当前非 0 且不等于我想要的」才判为玩家手动排过。
                // desired > 0 且 current == 0 ⇒ 干净 ⇒ PC 接管。
                // desired == 0（离段）且 current > 0 ⇒ 无法区分来源 ⇒ 按玩家所有处理（保守，绝不误清）。
                if (currentPriority != 0 && currentPriority != desiredPriority)
                {
                    PlayerOwnedPawnIds.Add(pawnId);
                    LastWrittenByPawnId[pawnId] = NeverWritten;
                    return false;
                }
                if (currentPriority == desiredPriority)
                {
                    LastWrittenByPawnId[pawnId] = desiredPriority;   // 已达期望，记为 PC 所有
                    return false;
                }
                return true;   // 需要写
            }

            // 写过：当前值与我上次写的不同 ⇒ 玩家中间动过 ⇒ 交还控制权。
            if (currentPriority != last)
            {
                PlayerOwnedPawnIds.Add(pawnId);
                LastWrittenByPawnId[pawnId] = NeverWritten;
                return false;
            }

            return currentPriority != desiredPriority;   // 幂等：已等于期望则不写
        }

        /// <summary>写成功后记账（只在真正调用 SetPriority 之后调用）。</summary>
        public void RecordWrite(int pawnId, int writtenPriority)
        {
            LastWrittenByPawnId[pawnId] = writtenPriority;
        }

        /// <summary>
        /// 该 pawn 的「用餐」格位是否归玩家所有（UI 提示用：可显示"已由你手动接管"）。
        /// </summary>
        public bool IsPlayerOwned(int pawnId)
        {
            return pawnId != 0 && PlayerOwnedPawnIds.Contains(pawnId);
        }

        /// <summary>
        /// 清理已不存在（死亡/移除）的 pawn 条目，防止字典无界增长（PERF-001 有界性）。
        /// </summary>
        /// <param name="alivePawnIds">当前仍存在的 pawn id 集合（由调用方一次遍历产出）。</param>
        /// <returns>被清理的条目数（诊断用）。</returns>
        public int Prune(HashSet<int> alivePawnIds)
        {
            int removed = 0;
            if (PlayerOwnedPawnIds != null)
            {
                removed += PruneSet(PlayerOwnedPawnIds, alivePawnIds);
            }
            if (LastWrittenByPawnId != null)
            {
                List<int> dead = null;
                foreach (KeyValuePair<int, int> pair in LastWrittenByPawnId)
                {
                    if (!alivePawnIds.Contains(pair.Key))
                    {
                        if (dead == null)
                        {
                            dead = new List<int>(4);
                        }
                        dead.Add(pair.Key);
                    }
                }
                if (dead != null)
                {
                    for (int i = 0; i < dead.Count; i++)
                    {
                        LastWrittenByPawnId.Remove(dead[i]);
                    }
                    removed += dead.Count;
                }
            }
            return removed;
        }

        private static int PruneSet(HashSet<int> set, HashSet<int> alive)
        {
            List<int> dead = null;
            foreach (int id in set)
            {
                if (!alive.Contains(id))
                {
                    if (dead == null)
                    {
                        dead = new List<int>(4);
                    }
                    dead.Add(id);
                }
            }
            if (dead == null)
            {
                return 0;
            }
            for (int i = 0; i < dead.Count; i++)
            {
                set.Remove(dead[i]);
            }
            return dead.Count;
        }

        /// <summary>存档键洁净化：复用车间台账的实现，避免非法 XML 字符毁档（2026-10-02 事故教训）。</summary>
        public static string SanitizeKey(string pawnStableId)
        {
            return Domain.WorkshopWorkDemotionLedger.SanitizeKey(pawnStableId);
        }

        /// <summary>便于设置页/调试读取的常量：吃饭时段 Def 名（避免各处硬编码）。</summary>
        public static string MealAssignmentDefName
        {
            get { return MealScheduleDefaults.MealTimeAssignmentDefName; }
        }
    }
}