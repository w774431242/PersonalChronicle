using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 「接管原版工作」判定 + 接管台账（<c>CHG-20261003-TAKEOVER</c> · Phase 2）——纯逻辑、可离线单测。
    ///
    /// <para><b>为什么不复用降档台账</b>（<c>WorkshopWorkDemotionLedger</c>）：
    /// 降档把档位写成 <b>4</b>，其还原闸门是「当前值仍为 4 才还原」；而接管把档位写成 <b>0</b>，
    /// 会被降档闸门判为"玩家已改动"而**丢弃且永不还原**。两者语义互斥，必须分账。</para>
    ///
    /// <para><b>安全设计（EXC-2026-016）</b>：接管是<b>不可逆风险很高</b>的写入
    /// （原版工作被勾掉 ⇒ 该殖民者彻底做不了这件事），因此本类只做<b>判定</b>，
    /// 真正写入由 Automation 层经 Application 门面执行，且必须先过两道闸门：
    /// ① 能力对等（见 <see cref="IsTakeoverAllowed"/>）；② 强制兜底（见 <see cref="MandatoryKeep"/>）。</para>
    ///
    /// <para>⚠ <b>CHG-20261005-REDLINE（2026-10-05 玩家口径「硬红线移除。有专属岗位。」）</b>：
    /// <see cref="MandatoryKeep"/> 的语义已从"**永不接管**"改为"**必须有在岗的专属岗位承接**"
    /// —— 玩家的论证成立：既然有物料/仓储/医务/安保这些**专属岗位**承接它们，
    /// 就不该无条件禁掉。详见该字段的注释。</para>
    /// </summary>
    public static class WorkshopTakeover
    {
        /// <summary>原版合法档位下界：1（最高优先）。0 = 未勾选。</summary>
        public const int MinPriority = 1;

        /// <summary>原版最低档（Pawn_WorkSettings.LowestPriority = 4，1.6 实证）。</summary>
        public const int LowestPriority = 4;

        /// <summary>还原闸门：这条接管记录现在是否仍可安全还原。</summary>
        /// <param name="currentPriority">该工作类型当前在 pawn 上的档位。</param>
        /// <param name="recordedOriginal">台账记录的、PC 动手之前的档位。</param>
        /// <returns>
        /// true = 可还原。两个条件都要满足：当前仍为 0（玩家没自己改过），
        /// 且原值合法（1..4，越界即丢弃、绝不硬写）。
        /// </returns>
        public static bool ShouldRestore(int currentPriority, int recordedOriginal)
        {
            if (currentPriority != 0)
            {
                return false;   // 玩家已改动 => PC 不再拥有这一项
            }
            return recordedOriginal >= MinPriority && recordedOriginal <= LowestPriority;
        }

        /// <summary>
        /// 敏感工作类型 defName（**需在岗专属岗位才允许接管**）。
        ///
        /// <para><b>原先的语义（已废止）</b>：搬运 / 清洁 / 医疗 / 灭火一旦被取消勾选，
        /// 殖民地会出现「无人送料 ⇒ 产线停摆」「无人清理」「伤病无人治」「火灾失控」
        /// ⇒ 因此"无论如何不得接管"。</para>
        ///
        /// <para>⚠ <b>CHG-20261005-REDLINE（2026-10-05 玩家口径「硬红线移除。有专属岗位。」）</b>：
        /// 那条"一票否决"的论证有个漏洞 —— 它默认"接管后无人替代"，但**岗位体系已经提供了
        /// 专属岗位**（物料 / 仓储 / 医务 / 安保）。只要**确实有人担任该岗位**，
        /// 接管（清零他人）之后这些活仍有人做。故本表语义改为：
        /// <b>敏感项要求「该工作类型在岗有专属岗位持有者」才允许接管</b>。</para>
        ///
        /// <para>⚠ <b>但这仍不足以单独保证安全</b>：接管是"**按人**清零"，若连
        /// <b>岗位持有者本人</b>也一起清零，就退化成"所有人都不能搬运"。
        /// ⇒ 调用方（<c>MapComponent_WorkshopManager.TryTakeOverVanillaWork</c>）
        /// **必须**在写入时豁免岗位持有者，两处合起来才成立。</para>
        /// </summary>
        public static readonly string[] MandatoryKeep =
        {
            "Hauling", "Cleaning", "Doctor", "Firefighter"
        };

        private static readonly HashSet<string> KeepSet = new HashSet<string>(MandatoryKeep);

        /// <summary>该工作类型是否属于<b>敏感项</b>（接管需在岗专属岗位）。</summary>
        public static bool IsMandatoryKeep(string workTypeDefName)
        {
            return !string.IsNullOrEmpty(workTypeDefName) && KeepSet.Contains(workTypeDefName);
        }

        /// <summary>
        /// 能力对等闸门：该工作类型<b>是否允许接管</b>（原签名，等价于"无在岗岗位"的最保守口径）。
        /// <para>保留是为兼容既有调用与单测；新代码请用三参重载。</para>
        /// </summary>
        public static bool IsTakeoverAllowed(string workTypeDefName, bool deliveredByPost)
        {
            return IsTakeoverAllowed(workTypeDefName, deliveredByPost, false);
        }

        /// <summary>
        /// 能力对等闸门：该工作类型<b>是否允许接管</b>（CHG-20261005-REDLINE 起为三参）。
        /// </summary>
        /// <param name="workTypeDefName">原版工作类型 defName。</param>
        /// <param name="deliveredByPost">
        /// 是否存在 <b>implementedWorkTypeDefNames</b>（真的实现了 Job）的岗位承接它
        /// （由 Automation 层算好后传入）。
        /// </param>
        /// <param name="staffedPostCovers">
        /// <b>该工作类型是否有在岗的专属岗位持有者</b>：
        /// 存在岗位承接它 <b>且</b> 该岗位在本车间确实有承担者
        /// （由 Automation 层用岗位分配结果算好后传入）。
        /// <para>这是 <see cref="MandatoryKeep"/> 敏感项的**唯一放行条件**。</para>
        /// </param>
        /// <returns>
        /// true = 允许接管。false = 拒绝：<b>敏感项且无在岗专属岗位</b>，或 <b>车间尚不能独立交付</b>。
        /// </returns>
        public static bool IsTakeoverAllowed(string workTypeDefName, bool deliveredByPost,
            bool staffedPostCovers)
        {
            if (string.IsNullOrEmpty(workTypeDefName))
            {
                // ⚠ fail-closed（2026-10-03 单测钉子）：空 defName 不走"闸门② = true ⇒ 允许"，
                //   否则一次脏数据就会让"接管一个不存在的工作类型"被放行。
                return false;
            }
            if (IsMandatoryKeep(workTypeDefName))
            {
                // CHG-20261005-REDLINE：不再是"一票否决"，而是"**必须有在岗的专属岗位承接**"。
                // 注意这里**刻意不看** deliveredByPost —— 敏感项关心的不是"PC 有没有实现 Job"
                // （PC 至今没有搬运/清洁/医疗/灭火的 Job），而是"**有没有人在岗位上做这件事**"。
                return staffedPostCovers;
            }
            return deliveredByPost;   // 闸门②：能力对等（非敏感项）
        }

        /// <summary>
        /// 强制兜底：接管后本图"可工作的非车间工人"数量。
        ///
        /// <para><b>为什么要它</b>：若所有可工作殖民者都被派进车间且原版工作全被接管，
        /// 殖民地会<b>无人可用</b>（连"取消勾选"这个自救动作都做不出来，因为没人能去做）。
        /// 因此接管前的最后一道检查：<b>接管后仍须至少剩 <paramref name="minFreeWorkers"/> 名
        /// 未被接管的自由人</b>，否则拒绝。</para>
        /// </summary>
        /// <param name="totalEligible">本图可工作（未倒地未崩溃）的玩家殖民者数。</param>
        /// <param name="crewMembers">被排进车间班组的人数（去重后）。</param>
        /// <param name="minFreeWorkers">必须保留的自由人下限（默认 1）。</param>
        /// <returns>接管后剩余的自由人数量。</returns>
        public static int FreeWorkersAfter(int totalEligible, int crewMembers, int minFreeWorkers)
        {
            int free = totalEligible - crewMembers;
            return free < 0 ? 0 : free;
        }

        /// <summary>强制兜底是否通过（接管后自由人 ≥ 下限）。</summary>
        public static bool PassesFreeWorkerFloor(int totalEligible, int crewMembers, int minFreeWorkers)
        {
            return FreeWorkersAfter(totalEligible, crewMembers, minFreeWorkers) >= minFreeWorkers;
        }
    }
}
