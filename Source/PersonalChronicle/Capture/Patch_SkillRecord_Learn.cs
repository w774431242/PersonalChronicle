using System;
using System.Reflection;
using HarmonyLib;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// 职业 XP 采集点（v2.5，跨 Mod 兼容重构）：
    ///
    /// v2.4 及之前采用 Prefix 拦截制造（JobDefOf.DoBill）期间的 SkillRecord.Learn 并 return false
    /// 跳过原方法、缓存到 CraftXpSettlement、完成时一次性注入——"整件结算制"。
    /// 但 return false 跳过原方法会与任何同样 patch SkillRecord.Learn 的 Mod（技能加速/训练类）
    /// 在制造期间产生**真实语义冲突**（其 Prefix 不执行、原方法被跳过）。
    ///
    /// v2.5 改为：本补丁仅作 **Postfix 纯分流**，**绝不拦截或跳过原方法**（Prefix 已移除，
    /// CraftXpSettlement 整件结算缓存已废弃）。原版 SkillRecord.Learn 始终正常执行（制造期间
    /// 与原版完全等价、不干预平衡）；其真实经验增量（含 Passion/学习速率，由 LearnRateFactor
    /// 还原）经本 Postfix 分流为职业 XP。
    ///
    /// 因 SkillRecord.Learn 是原版全部技能经验的唯一最终注入点，Postfix 可 100% 覆盖所有来源
    /// （制造/研究/战斗/种植/医疗/社交），append-only 累加、无双重计数，与现有非制造来源行为一致。
    /// 制造中途取消亦按已发生劳动获得部分职业 XP（比整件结算的"中途取消不获"更合理）。
    ///
    /// 性能：非制造来源经首道 ProfessionalSkillSourceCache 空查快速放行；制造来源每 tick 一次
    /// 真实经验分流（与每 tick 0.1 原版 Learn 同频），低频可接受。阈值 MinProfessionalXpThreshold
    /// 过滤每 tick 散碎微额（如 CE 战斗/社交 0.0x）；practiceCount 由 RecordSkillLearn 内
    /// xp >= 1f 阈值自然过滤微额，不会翻倍。
    ///
    /// Patch 档案：
    ///   Target  : Verse.SkillRecord.Learn(float, bool, bool)
    ///   Type    : Postfix（仅分流职业 XP，不修改原方法返回值、不跳过原方法）
    ///   Reason  : 职业技能 XP 采集（跨 Mod 兼容，零拦截）
    ///   Risk    : 低——Postfix 不跳过原方法，不与其他 patch Learn 的 Mod 冲突；签名漂移有探测
    /// </summary>
    // 治理登记（2026-09-15 审计补登）：本 patch 属 EXC-2026-001（捕获层 Harmony 例外 · E2）**清单 #17**。
    // 前提 = COMP-004「原生机制无法完整捕获」：技能 XP 学习是职业成长的唯一权威信号，
    // RimWorld 未暴露可订阅的学习事件 ⇒ 必须 Postfix 钩取。
    // 替代方案被否：GameComponent 轮询 SkillRecord 等级差分 —— 无法定位单次学习边界，且为热路径全扫（PERF-001）。
    [HarmonyPatch(typeof(SkillRecord), "Learn", new Type[] { typeof(float), typeof(bool), typeof(bool) })]
    public static class Patch_SkillRecord_Learn
    {
        /// <summary>
        /// 签名探测（仿 Patch_GenRecipe 范式）：[HarmonyPatch] 在目标签名漂移时会静默跳过，
        /// 故自行探测一次并在启动时明确报错，避免职业 XP 采集无声失效。
        /// v2.2：探测真实签名 (float, bool, bool)。
        /// </summary>
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        /// <summary>
        /// SkillRecord.pawn 为私有字段（1.6 反射实证），静态缓存 FieldInfo 避免高频反射查找。
        /// 字段漂移时为 null → pawn 获取失败 → 采集静默跳过（有签名探测兜底）。
        /// </summary>
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(SkillRecord), "pawn");

        /// <summary>
        /// 非制造技能经验的最小采集阈值。低于此值的微量增量（如 CE 战斗/社交每 tick 0.0x）
        /// 不进入职业 XP 计算，避免高并发场景下大量无意义处理。
        /// </summary>
        private const float MinProfessionalXpThreshold = 0.05f;

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(
                typeof(SkillRecord),
                "Learn",
                new Type[] { typeof(float), typeof(bool), typeof(bool) }) != null;
            if (!found)
            {
                ChronicleLog.Error(ChronicleLog.Category.Capture,
                    "SkillRecord.Learn(float,bool,bool) target signature changed; professional XP capture silently disabled - update Patch_SkillRecord_Learn");
            }
            return found;
        }

        public static void Postfix(SkillRecord __instance, float xp, bool direct, bool ignoreLearnRate)
        {
            try
            {
                if (__instance == null || xp <= 0f)
                {
                    return;
                }
                SkillDef def = __instance.def;
                if (def == null || string.IsNullOrEmpty(def.defName))
                {
                    return;
                }
                // 首道过滤：该原版技能无任何职业技能映射 → 零成本返回（高频路径）
                if (ProfessionalSkillSourceCache.Get(def.defName) == null)
                {
                    return;
                }
                // pawn 私有字段反射（v2.2：真实签名无 pawn 参数，从 SkillRecord.pawn 读取）
                Pawn pawn = PawnField != null ? PawnField.GetValue(__instance) as Pawn : null;
                if (pawn == null)
                {
                    return;
                }
                // 仅当前殖民地人口（殖民者/奴隶/囚犯）进入职业 XP 计算；
                // 避免袭击者/野生动物/访客/友军等非目标 pawn 触发后续完整处理链。
                if (!ChronicleColonistScanner.TryClassifyCurrent(pawn, out _))
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                // 原版实际经验（含 Passion/学习速率；入口参数 xp 为原始增量快照，与原方法内部修改互不影响）。
                float actualXp = xp;
                try
                {
                    actualXp = xp * __instance.LearnRateFactor(direct);
                }
                catch (Exception)
                {
                    actualXp = xp; // LearnRateFactor 异常时退回原始值，不阻断采集
                }
                // 微额经验过滤：非制造来源的每 tick 微量增量（如 CE 战斗/社交 0.0x）低于阈值时不进入职业 XP。
                if (actualXp < MinProfessionalXpThreshold)
                {
                    return;
                }
                // 逐 tick 真实经验分流（制造/研究/战斗/种植等统一路径）。
                // practiceCount 由 RecordSkillLearn 内 xp >= 1f 阈值自然过滤微额，不会翻倍。
                service.RecordSkillLearn(pawn, def.defName, actualXp, Find.TickManager.TicksGame, true);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture,
                    "SkillRecord.Learn patch failed: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Startup hook：预热职业技能源映射缓存 + 触发一次签名探测。
    /// [StaticConstructorOnStartup] 时 DefDatabase 已填充，缓存构建时机安全。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patch_SkillRecord_LearnStartup
    {
        static Patch_SkillRecord_LearnStartup()
        {
            ProfessionalSkillSourceCache.Warmup();
            _ = Patch_SkillRecord_Learn.TargetMethodExists;
        }
    }
}
