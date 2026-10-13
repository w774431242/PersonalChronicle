using System;
using System.Diagnostics;
using System.Globalization;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>B7 performance instrumentation: instance counters for the planning loop, so the tick
    /// cost of the economy can be reviewed without a profiler. Counters are plain fields on the
    /// component (no static mutable state) and the log line is bounded to once per PerfLogTicks.</summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        private const int PerfLogTicks = 600;
        private long perfTicks, perfSubjects, perfSourceErrors, perfSlowTicks;
        private double perfLastMs, perfMaxMs, perfTotalMs;
        private int perfLastLogTick = -PerfLogTicks;

        private void RecordPlanningTick(int now, long startedTicks, int planned, int errors)
        {
            double ms = (Stopwatch.GetTimestamp() - startedTicks) * 1000d / Stopwatch.Frequency;
            perfTicks++; perfSubjects += planned; perfSourceErrors += errors;
            perfLastMs = ms; perfTotalMs += ms; if (ms > perfMaxMs) perfMaxMs = ms;
            if (ms > 2d) perfSlowTicks++;
            if (now - perfLastLogTick < PerfLogTicks) return;
            perfLastLogTick = now;
            ChronicleLog.Info("ECON-PERF", "planning " + Summary());
        }
        /// <summary>One reviewable line: tick count, subjects, average and worst planning cost in ms,
        /// slow ticks above the 2 ms budget and accumulated source errors.</summary>
        internal string Summary()
        {
            double average = perfTicks == 0 ? 0d : perfTotalMs / perfTicks;
            return string.Format(CultureInfo.InvariantCulture,
                "ticks={0} subjects={1} avgMs={2:0.###} maxMs={3:0.###} slow>2ms={4} sourceErrors={5}",
                perfTicks, perfSubjects, average, perfMaxMs, perfSlowTicks, perfSourceErrors);
        }
    }
}