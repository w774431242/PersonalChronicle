using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// 制造中心 ⑦ 数据层契约（Def 注册表 + Runtime 对象快照）—— 数据构建部分。
    ///
    /// <para>口径：全部读**真实原版数据** —— `DefDatabase` 计数与节选、`map.listerBuildings` 上的
    /// 真实 Thing（含 CompPowerTrader / CompBreakdownable / CompQuality / BillStack）、
    /// `map.powerNetManager` 上的真实电网。`stableId` 一律 `<defName>:<thingIDNumber>`（数据层契约）。</para>
    /// </summary>
    public sealed partial class BuiltinManufacturingProvider : IManufacturingDataLayerDataProvider
    {
        /// <summary>ThingDef 节选（与预览同一批，均为稳定 defName）。</summary>
        private static readonly string[] SampleThingDefs =
        {
            "Steel", "WoodLog", "Cloth", "ComponentIndustrial", "Potatoes",
            "TableMachining", "TableSmithy", "HandTailorBench", "ElectricStove", "FabricationBench"
        };

        private const int MaxThingRows = 8;
        private const int MaxRecipeRows = 6;
        private const int MaxPowerNets = 4;

        public ManufacturingDataLayerSnapshot BuildDataLayer(Map map)
        {
            ManufacturingDataLayerSnapshot snapshot = new ManufacturingDataLayerSnapshot();
            try
            {
                int thingDefCount = SafeDefCount<ThingDef>();
                int recipeDefCount = SafeDefCount<RecipeDef>();
                snapshot.DefsSub = T2("PersonalChronicle.UI.DataLayer.Defs.Sub",
                    thingDefCount, recipeDefCount);

                // QualityCategory 枚举顺序（7 档，键用 name —— 与数据层契约一致）
                foreach (QualityCategory quality in (QualityCategory[])Enum.GetValues(typeof(QualityCategory)))
                {
                    snapshot.QualityChips.Add(((int)quality).ToString(CultureInfo.InvariantCulture)
                        + " " + quality.ToString());
                }

                AppendDefRows(snapshot);
                AppendThings(snapshot, map);
                AppendPowerNets(snapshot, map);
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Provider, "data layer snapshot failed: " + ex);
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }

        private static int SafeDefCount<T>() where T : Def
        {
            try
            {
                List<T> defs = DefDatabase<T>.AllDefsListForReading;
                return defs != null ? defs.Count : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static void AppendDefRows(ManufacturingDataLayerSnapshot snapshot)
        {
            for (int i = 0; i < SampleThingDefs.Length; i++)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(SampleThingDefs[i]);
                if (def == null)
                {
                    continue;
                }
                snapshot.ThingDefs.Add(new DefRowView
                {
                    Name = def.defName,
                    Detail = string.IsNullOrEmpty(def.label) ? def.defName : def.label,
                    Meta = def.thingClass != null ? def.thingClass.Name : "—",
                    Extra = T1("PersonalChronicle.UI.DataLayer.Stack", def.stackLimit)
                });
            }

            List<RecipeDef> recipes;
            try
            {
                recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            }
            catch (Exception)
            {
                return;
            }
            if (recipes == null)
            {
                return;
            }
            int added = 0;
            for (int i = 0; i < recipes.Count && added < MaxRecipeRows; i++)
            {
                RecipeDef recipe = recipes[i];
                if (recipe?.ingredients == null || recipe.products == null || recipe.products.Count == 0)
                {
                    continue;
                }
                snapshot.Recipes.Add(new DefRowView
                {
                    Name = recipe.defName,
                    Detail = JoinCounts(recipe.ingredients) + " → " + JoinThingCounts(recipe.products),
                    Meta = T1("PersonalChronicle.UI.DataLayer.Work", (int)recipe.workAmount),
                    Extra = HasRatedProduct(recipe)
                        ? T("PersonalChronicle.UI.DataLayer.Quality")
                        : (recipe.workSpeedStat != null ? recipe.workSpeedStat.defName : "—")
                });
                added++;
            }
        }

        /// <summary>产物是否可评品质（用 CompQuality 存在性判定，不依赖可能改名的 Def 字段）。</summary>
        private static bool HasRatedProduct(RecipeDef recipe)
        {
            if (recipe?.products == null || recipe.products.Count == 0)
            {
                return false;
            }
            ThingDef product = recipe.products[0]?.thingDef;
            return product != null && product.HasComp(typeof(CompQuality));
        }

        private static string JoinCounts(List<IngredientCount> ingredients)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < ingredients.Count; i++)
            {
                IngredientCount ingredient = ingredients[i];
                if (ingredient == null)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append(" + ");
                }
                builder.Append(ingredient.filter != null && ingredient.filter.AnyAllowedDef != null
                    ? ingredient.filter.AnyAllowedDef.defName
                    : "?");
                builder.Append('×').Append(Mathf.CeilToInt(ingredient.GetBaseCount()));
            }
            return builder.Length > 0 ? builder.ToString() : "—";
        }

        private static string JoinThingCounts(List<ThingDefCountClass> products)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < products.Count; i++)
            {
                ThingDefCountClass product = products[i];
                if (product?.thingDef == null)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append(" + ");
                }
                builder.Append(product.thingDef.defName).Append('×').Append(product.count);
            }
            return builder.Length > 0 ? builder.ToString() : "—";
        }

        private static void AppendThings(ManufacturingDataLayerSnapshot snapshot, Map map)
        {
            long revision = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            snapshot.ThingsSub = TS("PersonalChronicle.UI.DataLayer.Things.Sub", revision.ToString(CultureInfo.InvariantCulture));

            snapshot.ThingsHeader.Add(T("PersonalChronicle.UI.DataLayer.Col.StableId"));
            snapshot.ThingsHeader.Add(T("PersonalChronicle.UI.DataLayer.Col.Class"));
            snapshot.ThingsHeader.Add(T("PersonalChronicle.UI.DataLayer.Col.Power"));
            snapshot.ThingsHeader.Add(T("PersonalChronicle.UI.DataLayer.Col.Breakdown"));
            snapshot.ThingsHeader.Add(T("PersonalChronicle.UI.DataLayer.Col.Quality"));
            snapshot.ThingsHeader.Add(T("PersonalChronicle.UI.DataLayer.Col.Bills"));

            List<Building> buildings = map.listerBuildings != null
                ? map.listerBuildings.allBuildingsColonist
                : null;
            if (buildings == null)
            {
                return;
            }
            for (int i = 0; i < buildings.Count && snapshot.Things.Count < MaxThingRows; i++)
            {
                Building building = buildings[i];
                IBillGiver giver = building as IBillGiver;
                if (giver == null)
                {
                    continue;
                }
                CompPowerTrader power = building.TryGetComp<CompPowerTrader>();
                CompBreakdownable breakdown = building.TryGetComp<CompBreakdownable>();
                CompQuality quality = building.TryGetComp<CompQuality>();
                BillStack stack = giver.BillStack;

                ThingRowView row = new ThingRowView();
                // stableId 契约：<defName>:<thingIDNumber>
                row.Cells.Add(building.def.defName + ":" + building.thingIDNumber.ToString(CultureInfo.InvariantCulture));
                row.Cells.Add(building.def.thingClass != null ? building.def.thingClass.Name : "—");
                row.Cells.Add(power == null
                    ? "—"
                    : (power.PowerOn ? T("PersonalChronicle.UI.DataLayer.On") : T("PersonalChronicle.UI.DataLayer.Off"))
                      + " " + Mathf.RoundToInt(Mathf.Abs(power.PowerOutput)).ToString(CultureInfo.InvariantCulture) + "W");
                row.Cells.Add(breakdown == null
                    ? "—"
                    : (breakdown.BrokenDown
                        ? T("PersonalChronicle.UI.DataLayer.Broken")
                        : T("PersonalChronicle.UI.DataLayer.Ok")));
                row.Cells.Add(quality != null ? quality.Quality.ToString() : "—");
                row.Cells.Add((stack != null ? stack.Count : 0).ToString(CultureInfo.InvariantCulture));
                snapshot.Things.Add(row);
            }
        }

        private static void AppendPowerNets(ManufacturingDataLayerSnapshot snapshot, Map map)
        {
            List<PowerNet> nets = null;
            try
            {
                if (map.powerNetManager != null)
                {
                    nets = map.powerNetManager.AllNetsListForReading;
                }
            }
            catch (Exception)
            {
                nets = null;
            }
            if (nets == null)
            {
                return;
            }
            for (int i = 0; i < nets.Count && snapshot.PowerNets.Count < MaxPowerNets; i++)
            {
                PowerNet net = nets[i];
                if (net == null)
                {
                    continue;
                }
                // 只展示电网的两个权威读数（原版 PowerNet）：**净收支 W** 与 **已储 Wd**。
                // ⛔ 单位必须走 PowerGridReading：`CurrentEnergyGainRate()` 是 Wd/tick 不是 W，
                //    直接 RoundToInt 会恒为 0（实机 `0W · 0Wd` 的根因）。
                snapshot.PowerNets.Add(
                    Mathf.RoundToInt(PowerGridReading.NetWatts(net)).ToString(CultureInfo.InvariantCulture) + "W · "
                    + Mathf.RoundToInt(PowerGridReading.StoredWattDays(net)).ToString(CultureInfo.InvariantCulture) + "Wd");
            }
        }

    }
}
