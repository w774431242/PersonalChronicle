using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Verse;

namespace PersonalChronicle
{
    /// <summary>
    /// 轻量性能探针（默认关闭，Settings 里打开）。
    ///
    /// 存在理由：体检报告里的百分号是静态估算，无法回答"实测提升了多少"。
    /// 本探针把关键入口的真实耗时打进 Player.log，供 before / after 对比。
    ///
    /// 用法（成对调用）：
    /// <code>
    ///   long t0 = PerfProbe.Start();      // 未启用 → 返回 0
    ///   ...被测代码...
    ///   PerfProbe.End("reconcile", t0);   // t0 == 0 → 直接返回
    /// </code>
    ///
    /// 关闭时的开销 = 一次属性读取 + 一次比较，**零分配、零 Stopwatch 调用**。
    /// 开启后每 <see cref="ReportIntervalTicks"/> 汇总输出一次（次数 / 总毫秒 / 平均 / 最大单次），
    /// 输出后统计清零，因此每行都是"本轮"数据。
    ///
    /// ⚠️ key 必须是字面量（拼字符串会抵消节流意义）。
    /// </summary>
    public static class PerfProbe
    {
        /// <summary>汇总输出周期（tick）：3600 = 60 秒。</summary>
        private const int ReportIntervalTicks = 3600;

        private sealed class Stat
        {
            public long Count;
            public double TotalMs;
            public double MaxMs;
        }

        private static readonly Dictionary<string, Stat> stats = new Dictionary<string, Stat>();
        private static readonly StringBuilder sb = new StringBuilder();
        private static int lastReportTick = int.MinValue;

        private static bool Enabled =>
            PersonalChronicleMod.Settings != null && PersonalChronicleMod.Settings.EnablePerfProbe;

        /// <summary>开始计时；未启用返回 0（<see cref="End"/> 见 0 直接返回）。</summary>
        public static long Start()
        {
            return Enabled ? Stopwatch.GetTimestamp() : 0L;
        }

        /// <summary>结束计时并累计。key 用字面量。</summary>
        public static void End(string key, long startTs)
        {
            if (startTs == 0L)
            {
                return;
            }
            double ms = (Stopwatch.GetTimestamp() - startTs) * 1000.0 / Stopwatch.Frequency;
            if (!stats.TryGetValue(key, out Stat s))
            {
                s = new Stat();
                stats.Add(key, s);
            }
            s.Count++;
            s.TotalMs += ms;
            if (ms > s.MaxMs)
            {
                s.MaxMs = ms;
            }
        }

        /// <summary>
        /// 由 <c>ChronicleGameComponent.GameComponentTick</c> 最开头驱动（须在
        /// recording gate 之前，否则关闭录制时永远不输出）。
        /// </summary>
        public static void Tick()
        {
            if (!Enabled)
            {
                if (stats.Count > 0)
                {
                    stats.Clear();
                }
                return;
            }
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (now - lastReportTick < ReportIntervalTicks)
            {
                return;
            }
            lastReportTick = now;
            Report(now);
        }

        private static void Report(int now)
        {
            if (stats.Count == 0)
            {
                return;
            }
            sb.Length = 0;
            sb.Append("[PersonalChronicle][Perf] t=").Append(now);
            foreach (KeyValuePair<string, Stat> kv in stats)
            {
                Stat s = kv.Value;
                if (s.Count <= 0)
                {
                    continue;
                }
                sb.Append(" | ").Append(kv.Key)
                  .Append(" n=").Append(s.Count)
                  .Append(" total=").Append(s.TotalMs.ToString("0.00"))
                  .Append("ms avg=").Append((s.TotalMs / s.Count).ToString("0.000"))
                  .Append("ms max=").Append(s.MaxMs.ToString("0.000")).Append("ms");
            }
            Log.Message(sb.ToString());
            stats.Clear();
        }
    }
}
