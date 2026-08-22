using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// Tracks damage dealt to pawns so that, on death, we can attribute the kill
    /// to the pawn who dealt the MOST damage (the real "owner" of the kill) rather
    /// than whoever landed the finishing blow. The finishing instigator is recorded
    /// separately as an "assist" when it differs from the top damager.
    ///
    /// Design notes:
    ///   - Prefix on Pawn.TakeDamage: accumulate per-victim damage into a transient
    ///     map. We only care about damage from Pawn instigators (range/melee weapons
    ///     carried by a pawn), so environmental / non-pawn damage is ignored for the
    ///     attribution but the victim is still registered (so a later pawn hit flips
    ///     the lead).
    ///   - The accumulator is cleared when the victim dies (read + clear in Kill).
    ///   - Map is keyed by victim Pawn with a ConditionalWeakTable-like lifetime:
    ///     we use a plain Dictionary and prune null victim entries on read to avoid
    ///     leaking. RimWorld pawns are long-lived enough that this is safe.
    /// </summary>
    public static class Patch_PawnTakeDamage
    {
        /// <summary>
        /// Ledger size at/above which <see cref="PruneStale"/> actually scans. Below this
        /// the scan is skipped, so the common case (a handful of wounded pawns) stays O(1).
        /// </summary>
        private const int PruneThreshold = 64;

        /// <summary>Default number of assist candidates returned by <see cref="ConsumeTopDamagers"/>.</summary>
        private const int DefaultTopDamagers = 3;

        // victim -> (damager -> cumulative damage)
        private static readonly Dictionary<Pawn, Dictionary<Pawn, float>> DamageLedger =
            new Dictionary<Pawn, Dictionary<Pawn, float>>();

        /// <summary>
        /// Minimum ticks between failure warnings from the TakeDamage prefix. The
        /// method runs on every bullet/fire tick, so a persistent exception must be
        /// surfaced once per cooldown rather than flooding Player.log (BASE-009).
        /// </summary>
        private const long AssistWarningCooldownTicks = 6000L; // ~100s at 60 ticks/s

        /// <summary>Last tick a failure warning was emitted; long.MinValue = never.</summary>
        private static long _lastAssistWarningTick = long.MinValue;

        /// <summary>
        /// Drops every accumulated entry. MUST be called when a save is loaded or the
        /// player returns to the main menu: this ledger is static, so without an explicit
        /// reset it would keep Pawn references from the previous session alive and could
        /// mis-attribute an assist to a pawn that belongs to a different save.
        /// </summary>
        public static void Reset()
        {
            DamageLedger.Clear();
        }

        /// <summary>
        /// Records damage from a pawn instigator against a victim. Returns nothing;
        /// queried via <see cref="ConsumeTopDamagers"/> at kill time.
        /// </summary>
        public static void NoteDamage(Pawn victim, Pawn instigator, float amount)
        {
            if (victim == null || instigator == null || amount <= 0f)
            {
                return;
            }
            // Lifetime guard: a dead victim is about to be consumed by the kill patch;
            // a non-tracked/destroyed victim's stale entry is pruned to avoid leaks.
            if (victim.Dead || victim.Destroyed)
            {
                return;
            }
            PruneStale();
            if (!DamageLedger.TryGetValue(victim, out Dictionary<Pawn, float> ledger))
            {
                ledger = new Dictionary<Pawn, float>();
                DamageLedger[victim] = ledger;
            }
            if (!ledger.ContainsKey(instigator))
            {
                ledger[instigator] = 0f;
            }
            ledger[instigator] += amount;
        }

        /// <summary>
        /// Removes ledger entries for victims that are no longer valid (dead,
        /// destroyed, or whose entry has an empty sub-ledger). Called opportunistically
        /// from <see cref="NoteDamage"/> so the map cannot grow unbounded across a long
        /// colony where many pawns take damage but survive.
        /// </summary>
        private static void PruneStale()
        {
            if (DamageLedger.Count < PruneThreshold)
            {
                return;
            }
            List<Pawn> stale = null;
            foreach (KeyValuePair<Pawn, Dictionary<Pawn, float>> kv in DamageLedger)
            {
                Pawn v = kv.Key;
                if (v == null || v.Dead || v.Destroyed || kv.Value == null || kv.Value.Count == 0)
                {
                    stale ??= new List<Pawn>();
                    stale.Add(v);
                }
            }
            if (stale != null)
            {
                foreach (Pawn v in stale)
                {
                    if (v != null)
                    {
                        DamageLedger.Remove(v);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the top damagers for a victim, sorted descending by damage,
        /// and clears the ledger entry for that victim. Call once at kill time.
        /// Only chronicle-colonist damagers are returned (external damagers are
        /// irrelevant for assist attribution within the player's archive).
        /// </summary>
        public static List<Pawn> ConsumeTopDamagers(Pawn victim, int max = DefaultTopDamagers)
        {
            List<Pawn> result = new List<Pawn>();
            if (victim == null || !DamageLedger.TryGetValue(victim, out Dictionary<Pawn, float> ledger))
            {
                return result;
            }
            DamageLedger.Remove(victim);
            if (ledger.Count == 0)
            {
                return result;
            }
            // Explicit loop instead of LINQ (matches the rest of the codebase) and a
            // deterministic tie-breaker (thingIDNumber) so equal-damage assist order —
            // which is persisted into the kill event — doesn't depend on Dictionary order.
            List<KeyValuePair<Pawn, float>> entries = new List<KeyValuePair<Pawn, float>>(ledger.Count);
            foreach (KeyValuePair<Pawn, float> kv in ledger)
            {
                if (kv.Key != null)
                {
                    entries.Add(kv);
                }
            }
            entries.Sort((a, b) =>
            {
                int byDmg = b.Value.CompareTo(a.Value);
                if (byDmg != 0) return byDmg;
                int aid = a.Key.thingIDNumber;
                int bid = b.Key.thingIDNumber;
                return aid != bid ? aid.CompareTo(bid) : 0;
            });
            for (int i = 0; i < entries.Count && i < max; i++)
            {
                result.Add(entries[i].Key);
            }
            return result;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.TakeDamage))]
        public static void Prefix(Pawn __instance, DamageInfo dinfo)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }
                // RimWorld already resolves projectile damage to the launcher pawn before
                // TakeDamage runs, so DamageInfo.Instigator is the pawn we want whenever a
                // pawn is responsible. Anything else (turret, fire, cold, a collapsing roof)
                // is not an assist candidate and is simply skipped.
                Pawn instigator = dinfo.Instigator as Pawn;
                if (instigator == null)
                {
                    return;
                }
                NoteDamage(__instance, instigator, dinfo.Amount);
            }
            catch (Exception ex)
            {
                // Pawn.TakeDamage is one of the hottest methods in the game (every bullet,
                // every fire tick), so log at most once per cooldown. A persistent failure
                // still surfaces in Player.log for diagnosis (BASE-009 / COMP-009) without
                // flooding thousands of lines per raid. Assist data is cosmetic; dropping
                // one sample never touches the vanilla damage pipeline.
                long now = Find.TickManager != null ? Find.TickManager.TicksGame : long.MinValue;
                if (_lastAssistWarningTick == long.MinValue
                    || now == long.MinValue
                    || now - _lastAssistWarningTick >= AssistWarningCooldownTicks)
                {
                    _lastAssistWarningTick = now;
                    ChronicleLog.Warning(ChronicleLog.Category.Capture, "[Assist] Pawn.TakeDamage assist capture failed: " + ex.Message);
                }
            }
        }
    }
}
