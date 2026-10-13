using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    public sealed class SmartBenchExtension : DefModExtension
    {
        public ThingDef prototype;
        public string texturePath;
    }

    /// <summary>只在启动期接线；工单仍由原版 WorkGiver_DoBill 和 JobDriver_DoBill 执行。</summary>
    public static class SmartBenchBootstrap
    {
        public static void Initialize()
        {
            WorkshopDeviceIdentity.Rebuild();
            int count = 0;
            foreach (ThingDef target in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                SmartBenchExtension extension = target.GetModExtension<SmartBenchExtension>();
                if (extension == null) continue;
                try
                {
                    if (extension.prototype == null || extension.prototype == target)
                        throw new InvalidOperationException("Missing/invalid prototype: " + target.defName);
                    int workGivers = Bind(target, extension.prototype,
                        CollectPrototypeRecipes(extension.prototype, DefDatabase<RecipeDef>.AllDefsListForReading),
                        DefDatabase<WorkGiverDef>.AllDefsListForReading);
                    if (workGivers == 0 || target.AllRecipes.Count == 0)
                        throw new InvalidOperationException("No recipes or work giver: " + target.defName);
                    UseCustomGraphicsWhenComplete(target, extension.texturePath);
                    count++;
                }
                catch (Exception ex)
                {
                    Log.Error("[PersonalChronicle] T1 binding failed for " + target.defName + ": " + ex);
                }
            }
            WorkshopProcessCatalog.Invalidate();
            Log.Message("[PersonalChronicle] T1 manual equipment initialized: " + count);
        }

        /// <summary>同时读取当前声明和已生成 RecipeDef，避免原型早期 AllRecipes 缓存遗漏后生成的配方。</summary>
        internal static List<RecipeDef> CollectPrototypeRecipes(ThingDef prototype, IList<RecipeDef> allRecipes)
        {
            var result = new List<RecipeDef>();
            var seen = new HashSet<RecipeDef>();
            foreach (RecipeDef recipe in prototype.AllRecipes)
                if (recipe != null && seen.Add(recipe)) result.Add(recipe);
            if (prototype.recipes != null)
                foreach (RecipeDef recipe in prototype.recipes)
                    if (recipe != null && seen.Add(recipe)) result.Add(recipe);
            foreach (RecipeDef recipe in allRecipes)
                if (recipe != null && recipe.recipeUsers != null && recipe.recipeUsers.Contains(prototype)
                    && seen.Add(recipe)) result.Add(recipe);
            return result;
        }

        /// <summary>保留配方对象及其技能/研究条件；独立列表，不改写原台或全局 recipeUsers。</summary>
        internal static int Bind(ThingDef target, ThingDef prototype, IList<RecipeDef> recipes,
            IList<WorkGiverDef> workGivers)
        {
            var merged = new List<RecipeDef>();
            var seen = new HashSet<RecipeDef>();
            foreach (RecipeDef recipe in target.AllRecipes)
                if (recipe != null && seen.Add(recipe)) merged.Add(recipe);
            foreach (RecipeDef recipe in recipes)
                if (recipe != null && seen.Add(recipe)) merged.Add(recipe);
            target.recipes = merged;
            // AllRecipes 可在启动早期被查询；同步已存在的公开缓存列表，防止空配方缓存残留。
            List<RecipeDef> cached = target.AllRecipes;
            cached.Clear();
            cached.AddRange(merged);

            int attached = 0;
            foreach (WorkGiverDef giver in workGivers)
            {
                if (giver.fixedBillGiverDefs == null || !giver.fixedBillGiverDefs.Contains(prototype)
                    || giver.giverClass == null || !typeof(WorkGiver_DoBill).IsAssignableFrom(giver.giverClass)) continue;
                // 复制而非修改可能由 XML 继承共享的列表。
                if (!giver.fixedBillGiverDefs.Contains(target))
                {
                    giver.fixedBillGiverDefs = new List<ThingDef>(giver.fixedBillGiverDefs) { target };
                }
                attached++;
            }
            return attached;
        }

        private static void UseCustomGraphicsWhenComplete(ThingDef target, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            foreach (string direction in new[] { "north", "east", "south", "west" })
                if (ContentFinder<Texture2D>.Get(path + "_" + direction, false) == null) return;
            var graphic = new GraphicData();
            graphic.CopyFrom(target.graphicData);
            graphic.texPath = path;
            graphic.graphicClass = typeof(Graphic_Multi);
            graphic.shaderType = ShaderTypeDefOf.Cutout;
            graphic.drawSize = new Vector2(3.5f, 1.5f);
            graphic.drawRotated = false;
            graphic.allowFlip = false;
            graphic.color = Color.white;
            graphic.colorTwo = Color.white;
            graphic.shadowData = null;
            graphic.maskPath = null;
            ApplyResolvedGraphic(target, graphic, graphic.Graphic,
                (Texture2D)graphic.Graphic.MatAt(target.defaultPlacingRot).mainTexture);
            RegisterGraphic(target);
            RefreshBlueprintGraphic(target.blueprintDef, graphic);
            RefreshBlueprintGraphic(target.installBlueprintDef, graphic);
        }

        /// <summary>PostLoad 已缓存 graphic 和图标；仅替换 graphicData 无法更新实际绘制。</summary>
        internal static void ApplyResolvedGraphic(ThingDef target, GraphicData data, Graphic resolved, Texture2D icon)
        {
            target.graphicData = data;
            target.graphic = resolved;
            target.uiIcon = icon;
            target.uiIconMaterial = null;
            target.uiIconColor = Color.white;
            target.uiIconColorTwo = Color.white;
            target.uiIconAngle = 0f;
        }

        internal static GraphicData PrepareBlueprintGraphic(GraphicData existing, GraphicData building)
        {
            var data = new GraphicData();
            // 保留蓝图的 EdgeDetect、蓝色与 renderQueue，不把建成设备的着色器复制过去。
            data.CopyFrom(existing);
            data.texPath = building.texPath;
            data.maskPath = null;
            data.graphicClass = building.graphicClass;
            data.drawSize = building.drawSize;
            data.drawRotated = building.drawRotated;
            data.allowFlip = building.allowFlip;
            data.shadowData = null;
            return data;
        }

        private static void RefreshBlueprintGraphic(ThingDef blueprint, GraphicData building)
        {
            if (blueprint == null || blueprint.graphicData == null) return;
            GraphicData data = PrepareBlueprintGraphic(blueprint.graphicData, building);
            ApplyResolvedGraphic(blueprint, data, data.Graphic,
                (Texture2D)data.Graphic.MatAt(blueprint.defaultPlacingRot).mainTexture);
            RegisterGraphic(blueprint);
        }

        private static void RegisterGraphic(ThingDef target)
        {
            if (target.drawerType == DrawerType.RealtimeOnly) return;
            // ChronicleStartup 位于原版静态图集 Bake 之前，可将新图加入队列。
            TextureAtlasGroup group = target.category.ToAtlasGroup();
            target.graphic.TryInsertIntoAtlas(group);
            if (group == TextureAtlasGroup.Building && target.Minifiable)
                target.graphic.TryInsertIntoAtlas(TextureAtlasGroup.Item);
        }
    }
}
