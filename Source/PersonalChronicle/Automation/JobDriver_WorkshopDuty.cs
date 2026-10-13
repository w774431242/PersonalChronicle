using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    /// <summary>无可执行岗位任务时短暂待命，结束后由原版工作链重新找活。</summary>
    public class JobDriver_WorkshopDuty : JobDriver
    {
        private int dutyStartTick = -1;
        private MapComponent_WorkshopManager Manager => MapComponent_WorkshopManager.Get(pawn.Map);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Manager == null || !Manager.CanExecuteDuty(pawn));
            AddFinishAction(condition => FinishDuty());
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            Toil wait = ToilMaker.MakeToil("WorkshopAwaitExecutableWork");
            wait.initAction = BeginDuty;
            wait.preTickActions = new List<System.Action> { BeginDuty };
            wait.defaultCompleteMode = ToilCompleteMode.Delay;
            wait.defaultDuration = WorkshopDutyRuntimeRules.WaitRetryTicks;
            yield return wait;
        }

        private void BeginDuty()
        {
            if (dutyStartTick >= 0) return;
            dutyStartTick = Find.TickManager?.TicksGame ?? 0;
            Manager?.NotifyDutyStarted(pawn, Manager.DutyZoneFor(pawn));
        }

        private void FinishDuty()
        {
            Manager?.NotifyDutyEnded(pawn);
            if (dutyStartTick < 0 || pawn == null || PersonalChronicleMod.WorkTimeCaptureService == null) return;
            long now = Find.TickManager?.TicksGame ?? dutyStartTick;
            long ticks = now - dutyStartTick;
            if (ticks <= 0) return;
            // 等待归待命；实际加工由原版工作采样记录，不能重复计入生产工时。
            PersonalChronicleMod.WorkTimeCaptureService.RecordSample(new WorkTimeSample(
                pawn.GetUniqueLoadID(), "PC_WorkshopStandby", ticks, now,
                "PersonalChronicle.Automation.WorkshopDuty"));
        }
    }
}
