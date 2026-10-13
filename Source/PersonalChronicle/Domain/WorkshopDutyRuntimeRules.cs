namespace PersonalChronicle.Domain
{
    /// <summary>运行时边界：排班与管制作息同时允许，才可执行车间岗位。</summary>
    public static class WorkshopDutyRuntimeRules
    {
        public const int WaitRetryTicks = 300;
        public const int BoundaryCheckTicks = 60;

        public static bool CanWork(bool scheduled, string assignment)
        {
            if (!scheduled) return false;
            return assignment == null || assignment == "Work" || assignment == "Anything";
        }

        public static bool ShouldInterrupt(bool managedJob, bool playerForced, bool emergency,
            bool scheduled, string assignment)
        {
            return managedJob && !playerForced && !emergency && !CanWork(scheduled, assignment);
        }
    }
}
