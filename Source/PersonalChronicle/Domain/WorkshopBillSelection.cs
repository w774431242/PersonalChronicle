using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>选择最近的可执行工单；不可用的近设备不能挡住更远的可用设备。</summary>
    public static class WorkshopBillSelection
    {
        public static TJob NearestExecutable<TDevice, TJob>(IList<TDevice> devices,
            Func<TDevice, int> distance, Func<TDevice, TJob> tryMake, Action<TJob> discard)
            where TDevice : class where TJob : class
        {
            TJob best = null;
            int bestDistance = int.MaxValue;
            foreach (var device in devices)
            {
                if (device == null) continue;
                int d = distance(device);
                if (best != null && d >= bestDistance) continue;
                var candidate = tryMake(device);
                if (candidate == null) continue;
                if (best != null) discard(best);
                best = candidate;
                bestDistance = d;
            }
            return best;
        }
    }
}
