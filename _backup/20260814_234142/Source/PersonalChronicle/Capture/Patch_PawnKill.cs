using System;
using System.Collections.Generic;
using HarmonyLib;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// Captures colonist deaths and chronicle-colonist kills (P2).
    /// Postfix on Pawn.Kill(DamageInfo?, Hediff).
    ///
    /// Patch 档案:
    ///   Patch Target      : Pawn.Kill(DamageInfo?, Hediff)
    ///   Patch Type        : Postfix (read-only, never skips the original)
    ///   Reason            : (1) archive chronicle-victim deaths with killer weapon
    ///                       / killer Subject edges; (2) record kills by chronicle
    ///                       colonists of external humanlikes (raiders) for combat log
    ///   Alternative       : Prefix (rejected: need post-kill Dead confirmation)
    ///   Compatibility Risk: [HarmonyAfter] JellyCreative.IsekaiLeveling; moderate
    ///   Execution Order   : after ISEKAI leveling; otherwise no order contract
    ///   Failure Behavior  : catch → Log.Warning, original unaffected
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill), new Type[] { typeof(DamageInfo?), typeof(Hediff) })]
    [HarmonyAfter("JellyCreative.IsekaiLeveling")]
    public static class Patch_PawnKill
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            try
            {
                if (__instance == null || !__instance.Dead)
                {
                    return;
                }

                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }

                Pawn instigator = GetInstigatorPawn(dinfo);
                Thing weapon = GetKillerWeapon(dinfo, instigator);

                bool victimIsArchive = IsArchiveVictim(__instance);

                if (victimIsArchive)
                {
                    string deathCauseKey = null;
                    if (dinfo.HasValue)
                    {
                        deathCauseKey = dinfo.Value.Def != null ? dinfo.Value.Def.defName : null;
                    }
                    else if (exactCulprit != null && exactCulprit.def != null)
                    {
                        deathCauseKey = exactCulprit.def.defName;
                    }

                    Dictionary<string, string> extraParams = null;
                    if (instigator != null)
                    {
                        extraParams = new Dictionary<string, string>
                        {
                            { ChronicleEventParams.Killer, instigator.LabelShort }
                        };
                    }
                    // P2: pass killer pawn for Subject edge + battle participants.
                    service.OnPawnDied(__instance, deathCauseKey, weapon, extraParams, instigator);
                    return;
                }

                // P2: external humanlike killed → record the kill for the combat log.
                // RimWorld 1.6 often passes a null/Projectile Instigator for melee /
                // forwarded kills, so we must NOT gate the kill on a resolvable pawn.
                // Attribute to the chronicle colonist when resolvable; otherwise the
                // kill is still recorded under an "unknown killer" bucket so the
                // combat log is never empty due to an unresolvable instigator.
                // Consume the damage ledger for this victim to attribute the kill to
                // the top damager (A) and record the finishing instigator (B) as assist.
                List<Pawn> assistLookup = Patch_PawnTakeDamage.ConsumeTopDamagers(__instance);
                // v6.8: 推断战斗风格（近战/远程）与补刀伤害，供个人战斗维度累加。
                float finishingDamage = dinfo.HasValue ? dinfo.Value.Amount : 0f;
                bool isMelee = IsMeleeWeapon(weapon);
                service.OnKillRecorded(instigator, __instance, weapon, assistLookup, finishingDamage, isMelee);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture, "Pawn.Kill patch failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Chronicle archive victims: player-faction humanlikes or colony prisoners
        /// (same gate as pre-P2 death capture).
        /// </summary>
        private static bool IsArchiveVictim(Pawn pawn)
        {
            if (pawn == null || pawn.Faction == null)
            {
                return false;
            }
            return pawn.Faction.IsPlayer || pawn.IsPrisonerOfColony;
        }

        /// <summary>
        /// External target (raider, tribe, mechanoid, animal, quest creature, etc.).
        /// 绝对宽松：任何非玩家方/非囚犯的死亡都视为可记录战斗履历的外部击杀目标，
        /// 不再限制种族（Humanlike）。faction 为空（野生动物/任务怪等）也按外部目标处理。
        /// </summary>
        private static bool IsExternalVictim(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }
            if (pawn.Faction != null && (pawn.Faction.IsPlayer || pawn.IsPrisonerOfColony))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// DamageInfo.Instigator is not consistently a Pawn. For ranged kills
        /// vanilla normally stores the Projectile; the pawn is its Launcher.
        /// Some modded weapons expose a Verb caster through CompEquippable.
        /// </summary>
        private static Pawn GetInstigatorPawn(DamageInfo? dinfo)
        {
            if (!dinfo.HasValue)
            {
                return null;
            }
            Thing instigator = dinfo.Value.Instigator;
            Pawn pawn = instigator as Pawn;
            if (pawn != null)
            {
                return pawn;
            }
            Projectile projectile = instigator as Projectile;
            if (projectile != null)
            {
                return projectile.Launcher as Pawn;
            }
            if (instigator != null)
            {
                CompEquippable equippable = instigator.TryGetComp<CompEquippable>();
                if (equippable != null && equippable.PrimaryVerb != null)
                {
                    return equippable.PrimaryVerb.CasterPawn;
                }
            }
            return null;
        }

        private static Thing GetKillerWeapon(DamageInfo? dinfo, Pawn instigator)
        {
            if (!dinfo.HasValue)
            {
                return null;
            }
            if (instigator != null && instigator.equipment != null)
            {
                return instigator.equipment.Primary;
            }
            return dinfo.Value.Instigator as ThingWithComps;
        }

        /// <summary>
        /// v6.8: 推断击杀武器是否为近战（供个人战斗风格维度）。优先取武器主 Verb 的
        /// <see cref="Verb.IsMeleeAttack"/>（已反射核验存在于 RimWorld 1.6 Assembly-CSharp）；
        /// 无武器（环境致死/徒手）按远程处理（不计入近战风格）。
        /// </summary>
        private static bool IsMeleeWeapon(Thing weapon)
        {
            ThingWithComps twc = weapon as ThingWithComps;
            if (twc == null)
            {
                return false;
            }
            CompEquippable eq = twc.TryGetComp<CompEquippable>();
            if (eq != null && eq.PrimaryVerb != null)
            {
                return eq.PrimaryVerb.IsMeleeAttack;
            }
            return false;
        }
    }
}
