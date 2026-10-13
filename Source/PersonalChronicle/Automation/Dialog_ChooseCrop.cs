using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 作物选择窗口（农机「配置农场」→「选择作物」）。
    ///
    /// 布局与交互骨架在 <see cref="Dialog_DefPicker{T}"/>（搜索框 + 图标网格 + 右侧详情），
    /// 本类只提供作物数据：可播种植物全集、生长属性行、选中回调。
    /// </summary>
    public class Dialog_ChooseCrop : Dialog_DefPicker<ThingDef>
    {
        private const float StatH = 19f;
        private const int StatRows = 7;

        private readonly Action<ThingDef> onChosen;

        public Dialog_ChooseCrop(ThingDef current, Action<ThingDef> onChosen)
            : base(current)
        {
            this.onChosen = onChosen;
        }

        protected override Vector2 PickerSize => new Vector2(760f, 540f);

        protected override List<ThingDef> Collect()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(IsSowableCrop)
                .OrderBy(d => d.label)
                .ToList();
        }

        protected override string SearchText(ThingDef element) => element.label;

        protected override bool PassesFilter(ThingDef element) => true;

        protected override TaggedString TitleText => Tr("PersonalChronicle.Automation.Farm.SearchCrop");

        protected override TaggedString HintText => Tr("PersonalChronicle.Automation.Farm.CropHint");

        protected override TaggedString NoMatchText => Tr("PersonalChronicle.Automation.Farm.NoMatch");

        protected override void OnPicked(ThingDef element)
        {
            onChosen?.Invoke(element);
        }

        protected override void DrawDetails(ThingDef def, Rect rect)
        {
            PlantProperties p = def.plant;
            float blockH = StatH * StatRows;
            GameFont font = Text.Font;
            try
            {
                Text.Font = GameFont.Tiny;
                DrawStatRow(rect, 0, StatH, Tr("PersonalChronicle.Automation.Farm.GrowDays"), Tr("PersonalChronicle.Automation.Farm.StatDays", Num(p.growDays)));
                DrawStatRow(rect, 1, StatH, Tr("PersonalChronicle.Automation.Farm.FertilityMin"), Percent(p.fertilityMin));
                DrawStatRow(rect, 2, StatH, Tr("PersonalChronicle.Automation.Farm.FertilitySens"),
                    p.fertilitySensitivity > 0f ? Percent(p.fertilitySensitivity) : Tr("PersonalChronicle.Automation.Farm.StatNone"));
                DrawStatRow(rect, 3, StatH, Tr("PersonalChronicle.Automation.Farm.MinGlow"), Percent(p.growMinGlow));
                DrawStatRow(rect, 4, StatH, Tr("PersonalChronicle.Automation.Farm.Harvest"), HarvestText(p));
                DrawStatRow(rect, 5, StatH, Tr("PersonalChronicle.Automation.Farm.SowSkill"),
                    p.sowMinSkill > 0 ? Tr("PersonalChronicle.Automation.Farm.StatSkill", p.sowMinSkill.ToString()) : Tr("PersonalChronicle.Automation.Farm.StatNone"));
                DrawStatRow(rect, 6, StatH, Tr("PersonalChronicle.Automation.Farm.TempRange"),
                    Tr("PersonalChronicle.Automation.Farm.StatTemp", Num(p.minOptimalGrowthTemperature), Num(p.maxOptimalGrowthTemperature)));
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

        private static bool IsSowableCrop(ThingDef def)
        {
            PlantProperties p = def.plant;
            return p != null && p.Sowable && !p.mustBeWildToSow && ResearchUnlocked(p.sowResearchPrerequisites);
        }

        private static TaggedString HarvestText(PlantProperties p)
        {
            if (p.harvestedThingDef == null || p.harvestYield <= 0f)
            {
                return Tr("PersonalChronicle.Automation.Farm.StatNone");
            }
            return Tr("PersonalChronicle.Automation.Farm.StatYield", p.harvestedThingDef.LabelCap, Num(p.harvestYield));
        }

        private static TaggedString Percent(float value) => Tr("PersonalChronicle.Automation.Farm.StatPercent", Mathf.RoundToInt(value * 100f).ToString());
    }
}
