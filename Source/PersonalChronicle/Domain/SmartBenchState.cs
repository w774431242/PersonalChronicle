namespace PersonalChronicle.Domain
{
    public static class SmartBenchState
    {
        public const string Waiting = "SmartBenchWaiting";

        public static string KeyOf(bool broken, bool powered, bool hasBill, bool working)
        {
            if (broken) return EquipmentStateResolver.KeyFault;
            if (!powered) return EquipmentStateResolver.KeyOff;
            if (working) return EquipmentStateResolver.KeyRun;
            return hasBill ? Waiting : EquipmentStateResolver.KeyIdle;
        }
    }
}
