using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// v2.4 制造类技能「整件结算制」缓存器（方案 A，PM 拍板 2026-08-24）。
    ///
    /// 背景：原版制造经验为 tick 制——<c>Toils_Recipe.DoRecipeWork</c> 的 work toil
    /// 每 tick 调 <c>SkillRecord.Learn(0.1 / recipe.workSkillLearnFactor / …, false, false)</c>，
    /// <c>FinishRecipeAndStartStoringProduct</c> 完成时还有一次 GetSkill(workSkill).Learn。
    /// 本缓存器配合 <see cref="Capture.Patch_SkillRecord_Learn"/> 的 Prefix：制造
    /// （<c>JobDefOf.DoBill</c>）期间的 Learn 调用被拦截（跳过原版写入、零职业 XP 分流），
    /// 原始经验增量累加到此；制造完成（<c>Bill_Production.Notify_IterationCompleted</c>）
    /// 时 <c>Flush</c> 一次性注入 <c>pawn.skills.Learn(workSkill, total)</c>——
    /// 原版 Passion / 学习速率公式对总量自动生效，再经 SkillRecord.Learn Postfix 一次性
    /// 分流职业 XP。产出导向、无双重计数；制造中途取消则缓存随 job 变化重置丢弃。
    ///
    /// v2.4.1 修复（2026-08-27 玩家反馈延伸）：原版手术配方（Recipe_Surgery 等）
    /// 的 workSkill=Medicine 且执行 job=DoBill，导致 Warmup() 将 Medicine 纳入
    /// 制造技能集 → Prefix 拦截手术 Learn 调用 → 医疗经验丢失。
    /// 根因升级：原黑名单（仅排除 Medicine）为被动反应式，任何 mod 引入非制造
    /// workSkill 都会重演同类事故。v2.4.1 改为白名单 ManufacturingSkillNames
    /// （仅 Cooking/Crafting），从根源杜绝非制造技能被误拦截。
    ///
    /// 单 Pawn 单槽（同一时刻只做一个 job）；Entry 持有 job 引用，job 变化即视为新批次。
    /// </summary>
    public static class CraftXpSettlement
    {
        private sealed class Entry
        {
            public Job Job;
            public float RawXp;
        }

        private static readonly Dictionary<Pawn, Entry> Map = new Dictionary<Pawn, Entry>();

        /// <summary>
        /// 制造技能白名单命中集（defName 集）。Prefix 制造技能首滤用：
        /// 仅白名单内技能（Cooking/Crafting）走整件拦截；其余技能零成本放行，
        /// 避免高频路径上不必要的 pawn 反射与 job 查询。null 表示尚未构建（Warmup 前）。
        /// </summary>
        private static HashSet<string> CraftSkillNames;

        /// <summary>
        /// 制造技能白名单：只有这些技能才被识别为"制造"并走整件结算拦截。
        /// 采用白名单而非黑名单，从根源杜绝"非制造技能被误纳入制造集"的同类 bug
        /// （v2.4.1 升级：原黑名单 Medicine 被动式，任何 mod 引入非制造 workSkill
        /// 都会重演手术不加医疗经验的事故。白名单只认已知制造技能）。
        /// Cooking/Crafting 是原版唯一走 DoBill 的制造语义 workSkill。
        /// </summary>
        private static readonly HashSet<string> ManufacturingSkillNames = new HashSet<string>
        {
            "Cooking",
            "Crafting"
        };

        /// <summary>
        /// 幂等预热制造技能集。[StaticConstructorOnStartup] 阶段 DefDatabase 已填充，
        /// 调用时机安全；Prefix 首次命中时若仍为 null 会触发懒构建兜底。
        /// v2.4.1：只收集白名单内的 workSkill，避免非制造技能（如 Medicine）
        /// 被误纳入制造集导致 Prefix 误拦截（手术不加医疗经验 bug 根因）。
        /// </summary>
        public static void Warmup()
        {
            if (CraftSkillNames != null)
            {
                return;
            }
            var set = new HashSet<string>();
            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (recipe.workSkill != null && !string.IsNullOrEmpty(recipe.workSkill.defName))
                {
                    // 仅纳入白名单内的制造技能；非制造技能（医疗/研究/种植等）一律放行
                    if (ManufacturingSkillNames.Contains(recipe.workSkill.defName))
                    {
                        set.Add(recipe.workSkill.defName);
                    }
                }
            }
            CraftSkillNames = set;
        }

        /// <summary>
        /// 是否为制造技能（原版所有配方 workSkill 并集）。非制造技能首滤。
        /// 缓存未建时懒构建（游戏运行期 DefDatabase 已可用，安全）。
        /// </summary>
        public static bool IsCraftSkill(SkillDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.defName))
            {
                return false;
            }
            if (CraftSkillNames == null)
            {
                Warmup();
            }
            return CraftSkillNames.Contains(def.defName);
        }

        /// <summary>
        /// 完成结算注入期间置 true：Patch_SkillRecord_Learn 前缀据此放行该注入调用
        /// （不再拦截缓存，让原版公式真正落到技能与职业 XP）。
        /// </summary>
        [System.ThreadStatic]
        public static bool IsSettling;

        /// <summary>
        /// 尝试拦截缓存一次制造经验。仅当 pawn 正处制造 job（<c>JobDefOf.DoBill</c>）
        /// 时缓存原始增量并返回 true；同 job 内累加；job 变化（取消/换 job）则重置为新 job。
        /// </summary>
        public static bool TryCache(Pawn pawn, float xp)
        {
            if (pawn == null || xp <= 0f)
            {
                return false;
            }
            Job cur = pawn.CurJob;
            if (cur == null || cur.def != JobDefOf.DoBill)
            {
                return false;
            }
            if (!Map.TryGetValue(pawn, out Entry e) || e.Job != cur)
            {
                Map[pawn] = e = new Entry { Job = cur };
            }
            e.RawXp += xp;
            return true;
        }

        /// <summary>取走该 pawn 全部缓存制造经验并清空；无则返回 0。</summary>
        public static float Flush(Pawn pawn)
        {
            if (pawn != null && Map.TryGetValue(pawn, out Entry e))
            {
                Map.Remove(pawn);
                return e.RawXp;
            }
            return 0f;
        }
    }
}
