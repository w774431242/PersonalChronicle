using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using MapUI = Verse.UI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        private sealed class LaborCarryPresentation
        {
            internal Pawn Pawn;
            internal Thing Paper, Product;
        }
        private readonly Dictionary<int, LaborCarryPresentation> laborCarryPresentation = new Dictionary<int, LaborCarryPresentation>();

        // Sample from real held Things at the bounded tick boundary; GUI never queries economic ledgers.
        private void CacheLaborCarryPresentation(Pawn pawn)
        {
            if (pawn == null) return;
            var paper = QueryLaborHeldPaper(pawn);
            var product = QueryLaborHeldProduct(pawn);
            if (paper == null && product == null) { laborCarryPresentation.Remove(pawn.thingIDNumber); return; }
            if (!laborCarryPresentation.ContainsKey(pawn.thingIDNumber) && laborCarryPresentation.Count >= 64)
            {
                int remove = -1;
                foreach (var pair in laborCarryPresentation) { remove = pair.Key; break; }
                if (remove >= 0) laborCarryPresentation.Remove(remove);
            }
            laborCarryPresentation[pawn.thingIDNumber] = new LaborCarryPresentation { Pawn = pawn, Paper = paper, Product = product };
        }

        private void DrawLaborCarryPresentation()
        {
            if (!WorldRendererUtility.DrawingMap || Find.CurrentMap == null) return;
            foreach (var display in laborCarryPresentation.Values)
            {
                var pawn = display.Pawn;
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != Find.CurrentMap || pawn.Position.Fogged(pawn.Map)) continue;
                string job = pawn.CurJob?.def?.defName;
                if (job != LaborCollectJobDefName && job != LaborSubmitJobDefName && job != LaborVisitJobDefName) continue;
                var point = MapUI.MapToUIPosition(pawn.DrawPos);
                if (point.x < 20 || point.x > MapUI.screenWidth - 20 || point.y < 20 || point.y > MapUI.screenHeight - 20) continue;
                DrawLaborHeldIcon(pawn, display.Paper, new Rect(point.x - 22, point.y + 2, 20, 20));
                DrawLaborHeldIcon(pawn, display.Product, new Rect(point.x + 2, point.y + 2, 20, 20));
            }
        }

        private static void DrawLaborHeldIcon(Pawn pawn, Thing item, Rect rect)
        {
            if (item == null || item.Destroyed || item.Spawned || item.MapHeld != pawn.Map || item.PositionHeld != pawn.Position) return;
            Widgets.ThingIcon(rect, item);
        }
    }
}
