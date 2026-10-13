using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 「近 8 季滚动桶」共用工具（段 0 = 最老、段 7 = 当前季）。
    ///
    /// <para><b>唯一实现</b>：<see cref="ProductionAccumulator"/>（产出市值桶）与
    /// <see cref="WorkTimeAccumulator"/>（工时桶）共用同一套推进/归一逻辑 ——
    /// 禁止「同功能两处实现」（组件绘制规范反劣质黑名单第 10 条）。</para>
    ///
    /// <para><b>旧档兼容（GATE-SAVE）</b>：存档无此键 ⇒ 空列表 ⇒ <see cref="EnsureBuffer"/>
    /// 归一为 8 个 0 + 锚点 −1，无需 Migration；历史数据不可追溯 ⇒ 如实画空态。</para>
    /// </summary>
    public static class QuadrumBuckets
    {
        /// <summary>滚动桶槽数（近 8 季）。</summary>
        public const int Slots = 8;

        /// <summary>新建全 0 桶。</summary>
        public static List<float> NewBuffer()
        {
            return new List<float>(new float[Slots]);
        }

        /// <summary>桶长度/引用归一（存档载入后与旧档默认值都要过一遍）。</summary>
        public static void EnsureBuffer(List<float> buckets)
        {
            if (buckets == null)
            {
                buckets = new List<float>(Slots);
            }
            while (buckets.Count < Slots)
            {
                buckets.Add(0f);
            }
            while (buckets.Count > Slots)
            {
                buckets.RemoveAt(buckets.Count - 1);
            }
        }

        /// <summary>
        /// 将滚动桶**对齐**到指定绝对季度序号（幂等、可安全重复调用）。
        /// 写入路径与读取路径（快照构建期）都应调用：只在对齐写入时，
        /// 一个闲置了 n 季的档案会把 n 季前的高度留在末段 ⇒ 曲线「停在过去」。
        /// </summary>
        public static void Advance(List<float> buckets, ref long anchor, long quadrumIndex)
        {
            EnsureBuffer(buckets);
            if (anchor < 0L)
            {
                anchor = quadrumIndex;
                return;
            }
            long shift = quadrumIndex - anchor;
            if (shift <= 0L)
            {
                return;
            }
            if (shift >= Slots)
            {
                for (int i = 0; i < Slots; i++)
                {
                    buckets[i] = 0f;
                }
            }
            else
            {
                int n = (int)shift;
                for (int i = 0; i < Slots - n; i++)
                {
                    buckets[i] = buckets[i + n];
                }
                for (int i = Slots - n; i < Slots; i++)
                {
                    buckets[i] = 0f;
                }
            }
            anchor = quadrumIndex;
        }
    }
}
