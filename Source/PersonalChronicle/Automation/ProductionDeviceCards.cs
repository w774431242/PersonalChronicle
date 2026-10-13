using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>各等级生产设备的卡片声明，与配方/生产实现分离，可供未来 T2/T3 复用。</summary>
    public sealed class ProductionDeviceCardExtension : DefModExtension
    {
        public ProductionDeviceTier tier;
        /// <summary>完整单图路径（不带 .png）；建议使用北向资源，也允许专用设备卡图。</summary>
        public string texturePath;
    }

    internal static class ProductionDeviceCards
    {
        internal static bool TryResolve(ThingDef def, out ProductionDeviceCardExtension card)
        {
            card = def?.GetModExtension<ProductionDeviceCardExtension>();
            if (card == null || ProductionDeviceCardTier.TranslationKey(card.tier) == null
                || string.IsNullOrWhiteSpace(card.texturePath))
            {
                card = null;
                return false;
            }
            return true;
        }

        internal static ProductionDeviceCardView Build(Thing thing)
        {
            if (thing == null || !TryResolve(thing.def, out ProductionDeviceCardExtension card)) return null;
            string tier = ProductionDeviceCardTier.TranslationKey(card.tier).Translate().ToString();
            string label = thing.def.LabelCap.ToString();
            string speed = "PersonalChronicle.ProductionCard.Speed".Translate(
                (NamedArgument)thing.GetStatValue(StatDefOf.WorkTableWorkSpeedFactor)
                    .ToString("0.##", CultureInfo.InvariantCulture)).ToString();
            return new ProductionDeviceCardView
            {
                TexturePath = card.texturePath,
                TierText = tier,
                EquipmentLabel = label,
                SpeedText = speed,
                Tooltip = tier + " · " + label + "\n" + speed
            };
        }
    }
}
