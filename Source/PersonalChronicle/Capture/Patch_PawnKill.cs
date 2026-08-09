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
                if (__instance.RaceProps == null || !__instance.RaceProps.Humanlike)
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
                bool killerIsChronicle = instigator != null
                    && ChronicleColonistScanner.TryClassifyCurrent(instigator, out _);

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

                // P2: external humanlike killed by a chronicle colonist → kill log only.
                if (killerIsChronicle)
                {
                    service.OnKillRecorded(instigator, __instance, weapon);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("PersonalChronicle: Pawn.Kill patch failed: " + ex.Message);
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
    }
}
