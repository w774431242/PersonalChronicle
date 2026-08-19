using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>Partial of ArchiveMainTabWindow 鈥?PawnDetail view drawing (BUG-BASE-01 refactor).</summary>
    public sealed partial class ArchiveMainTabWindow
    {
        private void DrawPawnOverview(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            if (!(cachedDetailObject is PawnObject pawn))
            {
                return;
            }

            // v4.6.1: contribution-archive layout (matches contribution-archive-overview.html).
            // 0) Cover header: portrait + name + role description + stamp + verdict.
            // 1) Ledger header: 3-cell榨取总账 (output value / work hours / weekly yield).
            // 2) Output ledger: ProgressBar rows by production type.
            // 3) Health residual: 4 StatCells + 3 dim bars + depreciation event log + verdict.
            // 4) Medal wall (勋章墙): highest reached tier per series (§6.9).
            y = DrawCoverHeader(rect, y, pawn, service);
            y = DrawLedger(rect, y + UITheme.BlockGap, pawn);
            y = DrawOutputLedger(rect, y + UITheme.BlockGap);
            y = DrawHealthValuation(rect, y + UITheme.BlockGap, cachedHealth);
            y = DrawMedalWall(rect, y + UITheme.BlockGap, cachedMedals, service);
        }

        // ---- v4.4 Overview derived draw helpers ----

        private const float LifeTimelineNodeSize = 18f;















        /// <summary>
        /// P2: overview KPI uses the same combat caches as CombatLog tab.
        /// </summary>






        /// <summary>
        /// Renders the last <paramref name="maxRows"/> place visits (newest first).
        /// </summary>








        // v4.6.8: abbreviate large silver values with K/M/B magnitude suffixes
        // (e.g. 1840 → "1.8K", 2_300_000 → "2.3M"). Keeps the unit sub-label intact.














        // P3-2: Pawn/Weapon stat panels were identical — merged into one.




        // ---- Detail: live-read tabs (Skills / Health / Relations) --------------







        private struct RelationRowView
        {
            public string OtherLabel;
            public string RelationLabel;
            public string StatusLabel;
        }

        /// <summary>
        /// v4.6: merge live direct relations with archived snapshots so the Social
        /// tab shows initial ties (spouse/parent/friend at join time) even for dead
        /// or departed pawns. Live state wins when both sources describe the same pair.
        /// </summary>








        // ---- Detail: production tab (LD-1) -----------------------------------



        // ---- Detail: live tabs (LD-2/3/4) ------------------------------------

        /// <summary>
        /// v4.0 Career: intensity summary + work ledger + production + skill archive.
        /// </summary>




        /// <summary>
        /// Shared intensity hero card used by both Overview (v4.5.2) and Career tab.
        /// </summary>


        // ---- 健康残值 · 资产折旧 (window renders only; derivation in ReadModels) ----




        // ---- Medal wall (勋章墙, v1.1.4) ----
        // §6.9: 同 SeriesKey 只画最高已达档位；未授予但当前达标的也显示（灰态+进度）。
        // 窗口只消费 ReadModel 快照，不在此做任何判定/排序。

        private const float MedalWallCardH = 84f;








        // ---- 榨取总账 / 产出核销 (contribution-archive layout, v4.6.1) ----

        /// <summary>
        /// v4.6.2: Cover header (matches contribution-archive-overview.html .cover block).
        /// Portrait + name + role description + in-service stamp + one-line verdict.
        /// All visual goes through UITheme tokens; portrait is drawn via the native
        /// PortraitsCache (3D colonist render). Falls back to a placeholder box if
        /// no live pawn is resolvable.
        /// </summary>






        /// <summary>
        /// v4.6.4: tier medal inline row (.tier-inline in the design spec).
        /// Renders a square medal (tier display code on tier-coloured fill) beside
        /// the tier title, an "预估/实际" tag and the projected daily hours. Never
        /// called when the tier is undefined — the row collapses to zero height.
        /// </summary>












        /// <summary>
        /// v4.14: net ledger silver = realised market value − work-hours × cost
        /// rate. Kept in one place so the "净收益" cell and any tooltip agree.
        /// </summary>



















        private static string FormatHours(double hours)
        {
            return "PersonalChronicle.UI.Hours".Translate(hours.ToString("0.0")).ToString();
        }







        /// <summary>
        /// v3.1 P3 Social: significant relation snapshots + social events + co-occurrence.
        /// </summary>




        /// <summary>
        /// Returns the (col, row) grid slots for a given relation count. Slots are
        /// returned in the order they should be filled (importance-ranked by caller),
        /// importance-ranked by caller), so position 0 is always the most prominent
        /// tie. Slots are exact integer offsets — DrawSocialNetwork multiplies them
        /// by colSpacing / rowSpacing so every node lands on a grid intersection.
        /// All relation cards share the SAME size — only positions change by tier:
        /// spouses occupy the symmetric left/right anchor (the "extension centre"),
        /// parents sit above, children below, siblings on the sides and
        /// friends/rivals/others on the outer ring.
        /// </summary>
        private static (int col, int row)[] GridSlotsFor(List<SocialNodeView> nodes)
        {
            if (nodes.Count == 0)
            {
                return System.Array.Empty<(int, int)>();
            }

            // Slot pools, one per relation tier. When a pool is exhausted the
            // remaining nodes fall back to the outer ring; same size everywhere.
            var spouseSlots = new (int, int)[]
            {
                (-1, 0), ( 1, 0) // 夫妻对称左右
            };
            var parentSlots = new (int, int)[]
            {
                ( 0, -1), (-1, -1), ( 1, -1), // 父母辈往上
                (-2, -1), ( 2, -1)
            };
            var childSlots = new (int, int)[]
            {
                ( 0, 1), (-1, 1), ( 1, 1), // 子女辈往下
                (-2, 1), ( 2, 1)
            };
            var siblingSlots = new (int, int)[]
            {
                (-2, 0), ( 2, 0) // 平辈左右
            };
            var outerSlots = new (int, int)[]
            {
                (-1, -2), ( 1, -2), (-1, 2), ( 1, 2),
                (-2, -2), ( 2, -2), (-2, 2), ( 2, 2),
                ( 0, -2), ( 0, 2), (-3, 0), ( 3, 0)
            };

            var result = new (int col, int row)[nodes.Count];
            var used = new HashSet<(int, int)>();
            int[] cursors = new int[5]; // spouse / parent / child / sibling / outer
            for (int i = 0; i < nodes.Count; i++)
            {
                SocialRelationTier tier = SocialRelationTierOf(nodes[i].RelationDefName);
                (int col, int row) slot;
                switch (tier)
                {
                    case SocialRelationTier.Spouse:
                        slot = TakeNext(spouseSlots, ref cursors[0], used);
                        break;
                    case SocialRelationTier.Parent:
                        slot = TakeNext(parentSlots, ref cursors[1], used);
                        break;
                    case SocialRelationTier.Child:
                        slot = TakeNext(childSlots, ref cursors[2], used);
                        break;
                    case SocialRelationTier.Sibling:
                        slot = TakeNext(siblingSlots, ref cursors[3], used);
                        break;
                    default:
                        slot = TakeNext(outerSlots, ref cursors[4], used);
                        break;
                }
                result[i] = slot;
            }
            return result;
        }

        private static (int col, int row) TakeNext(
            (int col, int row)[] pool, ref int cursor, HashSet<(int, int)> used)
        {
            // 优先用池内未被占用的槽位；池耗尽则在外圈兜底，保证不重叠。
            for (int k = cursor; k < pool.Length; k++)
            {
                if (used.Add((pool[k].col, pool[k].row)))
                {
                    cursor = k + 1;
                    return pool[k];
                }
            }
            cursor = pool.Length;
            for (int ring = 1; ring < 8; ring++)
            {
                for (int dc = -ring; dc <= ring; dc++)
                {
                    for (int dr = -ring; dr <= ring; dr++)
                    {
                        if (Mathf.Abs(dc) != ring && Mathf.Abs(dr) != ring)
                        {
                            continue;
                        }
                        if (used.Add((dc, dr)))
                        {
                            return (dc, dr);
                        }
                    }
                }
            }
            return (0, 0);
        }

        private enum SocialRelationTier
        {
            Other,
            Spouse,
            Parent,
            Child,
            Sibling
        }


        /// <summary>
        /// Draws an orthogonal Z-shaped link between two card centres. The line
        /// starts at card A's midpoint and ends at card B's midpoint — NOT from
        /// the card edges. Both vertical axes run along card A's X midpoint
        /// (center.x) and card B's X midpoint (nodeCenter.x); the two vertical
        /// segments are joined by one horizontal segment at the mid-height.
        /// A small 5f chamfer at each 90° turn keeps the corner smooth.
        /// </summary>
        // Orthogonal link geometry now lives in ArchivePanelBase (BUG-BASE-01 refactor).

    }
}
