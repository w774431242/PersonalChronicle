namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>设备卡身份快照：只承载资源路径与预格式化文本，不持有建筑或 Unity 资产。</summary>
    public sealed class ProductionDeviceCardView
    {
        public string TexturePath = string.Empty;
        public string TierText = string.Empty;
        public string EquipmentLabel = string.Empty;
        public string SpeedText = string.Empty;
        public string Tooltip = string.Empty;
    }

    /// <summary>生产设备等级；T2/T3 仅提供接入契约，不生成设备或样例数据。</summary>
    public enum ProductionDeviceTier { Unspecified = 0, T1 = 1, T2 = 2, T3 = 3 }

    internal static class ProductionDeviceCardTier
    {
        internal static string TranslationKey(ProductionDeviceTier tier)
        {
            switch (tier)
            {
                case ProductionDeviceTier.T1: return "PersonalChronicle.ProductionCard.T1";
                case ProductionDeviceTier.T2: return "PersonalChronicle.ProductionCard.T2";
                case ProductionDeviceTier.T3: return "PersonalChronicle.ProductionCard.T3";
                default: return null;
            }
        }
    }
}
