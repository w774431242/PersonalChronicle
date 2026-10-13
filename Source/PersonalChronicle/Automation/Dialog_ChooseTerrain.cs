using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 改造地形选择窗口（农机「改造地形」互动键）。
    ///
    /// 候选 = <b>所有可由玩家建造的 TerrainDef</b>（按 <c>designationCategory != null</c> 判定，
    /// 原版与第三方地形 mod 一视同仁，零白名单）+ 农机默认沃土 SoilRich（非玩家可建但允许选回）。
    /// 未研究解锁的地形不列出；无肥力地形（fertility ≤ 0）不列出。
    ///
    /// 选中后交给 <see cref="CompAutoAgriculture.SetTargetTerrain"/>：改造从设备中心
    /// 一圈圈重新铺开（圈层机制复用），可无限次更换目标。
    /// </summary>
    public class Dialog_ChooseTerrain : Dialog_DefPicker<TerrainDef>
    {
        private const float StatH = 19f;
        private const int StatRows = 4;
        private const string DefaultTerrainDefName = "SoilRich";

        private readonly CompAutoAgriculture comp;

        public Dialog_ChooseTerrain(CompAutoAgriculture comp)
            : base(comp != null ? comp.TargetTerrain : null)
        {
            this.comp = comp;
        }

        protected override Vector2 PickerSize => new Vector2(720f, 540f);

        /// <summary>⚠️ 在基类构造器内调用：只准用静态数据（comp 此时还未赋值）。</summary>
        protected override List<TerrainDef> Collect()
        {
            return DefDatabase<TerrainDef>.AllDefsListForReading
                .Where(IsPickable)
                .OrderBy(d => d.label)
                .ToList();
        }

        protected override string SearchText(TerrainDef element) => element.label;

        protected override bool PassesFilter(TerrainDef element) => true;

        protected override TaggedString TitleText => Tr("PersonalChronicle.Automation.Farm.TerrainTitle");

        protected override TaggedString HintText => Tr("PersonalChronicle.Automation.Farm.TerrainHint");

        protected override TaggedString NoMatchText => Tr("PersonalChronicle.Automation.Farm.TerrainNoMatch");

        protected override void OnPicked(TerrainDef element)
        {
            comp?.SetTargetTerrain(element);
        }

        protected override void DrawDetails(TerrainDef def, Rect rect)
        {
            float blockH = StatH * StatRows;
            GameFont font = Text.Font;
            try
            {
                Text.Font = GameFont.Tiny;
                DrawStatRow(rect, 0, StatH, Tr("PersonalChronicle.Automation.Farm.StatFertility"), Num(def.fertility));
                DrawStatRow(rect, 1, StatH, Tr("PersonalChronicle.Automation.Farm.StatBeauty"), Num(def.GetStatValueAbstract(StatDefOf.Beauty)));
                DrawStatRow(rect, 2, StatH, Tr("PersonalChronicle.Automation.Farm.StatPathCost"),
                    def.pathCost > 0 ? (TaggedString)def.pathCost.ToString() : Tr("PersonalChronicle.Automation.Farm.StatNone"));
                DrawStatRow(rect, 3, StatH, Tr("PersonalChronicle.Automation.Farm.StatCost"), CostText(def));
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.LabelScrollable(
                    new Rect(rect.x, rect.y + blockH + 6f, rect.width, rect.yMax - rect.y - blockH - 6f),
                    def.description ?? string.Empty,
                    ref descScroll);
            }
            finally
            {
                Text.Font = font;
            }
        }

        /// <summary>
        /// 可选地形：玩家可建造（原版「地板」分类与第三方地形 mod 同一判定，零白名单）
        /// 或农机默认沃土；未研究解锁的排除；<b>无肥力地形排除</b>（肥力 0 的石板/
        /// 金属地板等对农田毫无意义，且原版播种本就要求肥力 > 0）。
        /// 水面/基座等不可建造地形天然被滤掉。
        /// </summary>
        private static bool IsPickable(TerrainDef def)
        {
            if (def == null)
            {
                return false;
            }
            if (def.defName == DefaultTerrainDefName)
            {
                return true;
            }
            if (def.designationCategory == null)
            {
                return false;
            }
            if (def.fertility <= 0f)
            {
                return false;
            }
            return ResearchUnlocked(def.researchPrerequisites);
        }

        private static TaggedString CostText(TerrainDef def)
        {
            List<ThingDefCountClass> cost = def.costList;
            if (cost == null || cost.Count == 0)
            {
                return Tr("PersonalChronicle.Automation.Farm.StatNone");
            }
            string text = string.Empty;
            for (int i = 0; i < cost.Count; i++)
            {
                if (cost[i] == null || cost[i].thingDef == null || cost[i].count <= 0)
                {
                    continue;
                }
                text += (text.Length > 0 ? " / " : string.Empty)
                    + cost[i].thingDef.LabelCap + " ×" + cost[i].count;
            }
            return text.Length > 0 ? (TaggedString)text : Tr("PersonalChronicle.Automation.Farm.StatNone");
        }
    }
}
