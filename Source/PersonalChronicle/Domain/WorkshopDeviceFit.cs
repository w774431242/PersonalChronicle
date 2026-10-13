using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 「工序设备匹配度」求值结果（纯数据，供管理面板概览页渲染）。
    /// </summary>
    public struct WorkshopDeviceFit
    {
        /// <summary>工序是否声明了期望设备（<c>false</c> ⇒ 缺失项无意义，UI 应显示"未声明"）。</summary>
        public bool HasExpected;

        /// <summary>与期望 defName 相符的台数。</summary>
        public int Matched;

        /// <summary>不属于本工序期望设备的台数（"多余"）。</summary>
        public int Extra;

        /// <summary>期望但缺失的 defName 列表（永不 null，无缺失时为空表）。</summary>
        public List<string> MissingDefNames;
    }

    /// <summary>
    /// 车间「设备匹配度」判定（**纯函数，可离线单测**）。
    ///
    /// <para>语义：把车间类型声明的期望设备 defName 与区内实际设备 defName 比对，输出
    /// 匹配 / 多余 / 缺失。**只比较 defName 字符串**，不反射类型、不读 DefDatabase，
    /// 因此可在无 Unity/Verse 宿主的单测里直接跑（与 <c>EquipmentStateResolver</c> 同范式）。</para>
    ///
    /// <para>未声明期望设备（<paramref name="expectedDefNames"/> 为 null 或空）是**合法状态**
    /// ——调试台/质检台/维修机等原生锚点尚未落地——此时全部设备计为"多余"且
    /// <see cref="WorkshopDeviceFit.HasExpected"/> 为 false，由 UI 如实说明而非报"缺失"。</para>
    /// </summary>
    public static class WorkshopDeviceFitEvaluator
    {
        public static WorkshopDeviceFit Evaluate(List<string> expectedDefNames, IList<string> actualDefNames)
        {
            return Evaluate(expectedDefNames, actualDefNames, null, null);
        }

        /// <summary>等价设备满足同一要求；可接受的辅助设备不额外形成必需项。</summary>
        public static WorkshopDeviceFit Evaluate(List<string> expectedDefNames, IList<string> actualDefNames,
            Func<string, string> canonicalName, IList<string> acceptedDefNames)
        {
            var fit = new WorkshopDeviceFit { MissingDefNames = new List<string>() };
            if (expectedDefNames == null || expectedDefNames.Count == 0)
            {
                // 未声明期望设备：不产生缺失项，全部计入"多余"（UI 会显示"未声明"而非误导性计数）。
                fit.Extra = actualDefNames != null ? actualDefNames.Count : 0;
                return fit;
            }

            fit.HasExpected = true;
            Func<string, string> normalize = canonicalName ?? (name => name);
            var required = new HashSet<string>();
            foreach (string name in expectedDefNames)
                if (name != null) required.Add(normalize(name));
            var accepted = new HashSet<string>(required);
            if (acceptedDefNames != null)
                foreach (string name in acceptedDefNames)
                    if (name != null) accepted.Add(normalize(name));
            var present = new HashSet<string>();
            if (actualDefNames != null)
            {
                for (int i = 0; i < actualDefNames.Count; i++)
                {
                    string defName = actualDefNames[i];
                    string identity = defName != null ? normalize(defName) : null;
                    if (identity != null && accepted.Contains(identity))
                    {
                        fit.Matched++;
                        present.Add(identity);
                    }
                    else
                    {
                        fit.Extra++;
                    }
                }
            }
            // 期望清单里同一 defName 写重复时只报一次缺失（2026-09-11 体检：原实现会显示
            // "缺失 2：[台, 台]"）。
            var missingSeen = new HashSet<string>();
            for (int i = 0; i < expectedDefNames.Count; i++)
            {
                string defName = expectedDefNames[i];
                string identity = defName != null ? normalize(defName) : null;
                if (identity != null && !present.Contains(identity) && missingSeen.Add(identity))
                {
                    fit.MissingDefNames.Add(defName);
                }
            }
            return fit;
        }
    }
}
