using Verse;

namespace PersonalChronicle.Domain.Honor
{
    /// <summary>
    /// P7 已授予职称记录（append-only 历史，青铜→银→金逐级追加；显示最高档由 UI 处理）。
    /// 写于 QualificationEvaluator 判定 Qualified 后（默认自动授予 D-T1）。
    /// </summary>
    public sealed class GrantedTitle : IExposable
    {
        public string TitleDefName;
        public string QualificationDefName;
        public long GrantedTick;

        public GrantedTitle()
        {
        }

        public GrantedTitle(string titleDefName, string qualificationDefName, long grantedTick)
        {
            TitleDefName = titleDefName;
            QualificationDefName = qualificationDefName;
            GrantedTick = grantedTick;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref TitleDefName, "titleDefName");
            Scribe_Values.Look(ref QualificationDefName, "qualificationDefName");
            Scribe_Values.Look(ref GrantedTick, "grantedTick", 0L);
        }
    }
}
