using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// Partial of <see cref="BuiltinPopulationProvider"/> —— **派生指标 / 戒律 / 勋章榜**
    /// 采集（预览 ④ 派生属性 · 本 Mod、② 信仰的 Precept、⑦ 荣誉的勋章构成）。
    ///
    /// <para><b>诚实性</b>：每一项都必须有**真实口径**（可复算）。算不出的（时序类、汇率类、
    /// 战役胜负）不是"留空"，而是**不产出**并在 UI 侧如实标注无口径 —— 绝不填样例数字。</para>
    ///
    /// <para><b>成本</b>：逐人一次活读（对象数 N × 每人的需要/技能/装备/健康读取），
    /// 与既有 <c>Collect</c> 同阶，且整体在 300 ticks 节流内（BuildOverview 调用频率）。</para>
    /// </summary>
    public sealed partial class BuiltinPopulationProvider
    {
        /// <summary>情绪风险阈值（原版精神崩溃常用警戒线）。</summary>
        private const float MoodRiskThreshold = 0.35f;

        /// <summary>温饱达标线（原版饥饿从 0.3 起降心情）。</summary>
        private const float FedThreshold = 0.3f;

        /// <summary>技能覆盖的等级门槛（≥ 该级视为"有人能挑大梁"）。</summary>
        private const int SkillCoverLevel = 8;

        /// <summary>勋章榜/戒律榜展示上限。</summary>
        private const int TopListLimit = 5;

        /// <summary>
        /// ④ 派生指标（预览那 12 张卡里**口径真实可算**的部分，逐项可复算）。
        /// 不可算的部分（产出/工时累计曲线、单点依赖的时间维度、银币汇率折算）不出现在列表里
        /// —— UI 侧另有说明行，避免用户以为"漏了"。
        /// </summary>
        private void FillDerived(PopulationOverviewSnapshot snapshot)
        {
            IReadOnlyList<ArchiveObject> objects = queries.GetObjectsOfCategory(ArchiveCategoryKeys.Pawn);
            if (objects == null || objects.Count == 0)
            {
                return;
            }
            int live = 0;
            int moodRisk = 0;
            int equipped = 0;
            int wounded = 0;
            int bedded = 0;
            int fed = 0;
            long producedTotal = 0L;
            long producedTop = 0L;
            HashSet<SkillDef> coveredSkills = new HashSet<SkillDef>();

            for (int i = 0; i < objects.Count; i++)
            {
                PawnObject pawn = objects[i] as PawnObject;
                if (pawn == null)
                {
                    continue;
                }
                if (pawn.Production != null)
                {
                    long quantity = pawn.Production.TotalQuantity;
                    if (quantity > 0L)
                    {
                        producedTotal += quantity;
                        if (quantity > producedTop)
                        {
                            producedTop = quantity;
                        }
                    }
                }
                if (!queries.IsCurrentlyEnlisted(pawn.StableId))
                {
                    continue;
                }
                Pawn livePawn = queries.GetLivePawn(pawn.StableId);
                if (livePawn == null)
                {
                    continue;
                }
                live++;
                if (livePawn.needs != null && livePawn.needs.mood != null
                    && livePawn.needs.mood.CurLevel < MoodRiskThreshold)
                {
                    moodRisk++;
                }
                bool armed = livePawn.equipment != null && livePawn.equipment.Primary != null;
                bool armored = livePawn.apparel != null && livePawn.apparel.WornApparelCount > 0;
                if (armed && armored)
                {
                    equipped++;
                }
                if (livePawn.health != null && livePawn.health.hediffSet != null
                    && livePawn.health.hediffSet.hediffs.Count > 0)
                {
                    wounded++;
                }
                if (livePawn.ownership != null && livePawn.ownership.OwnedBed != null)
                {
                    bedded++;
                }
                if (livePawn.needs != null && livePawn.needs.food != null
                    && livePawn.needs.food.CurLevel >= FedThreshold)
                {
                    fed++;
                }
                CollectCoveredSkills(livePawn, coveredSkills);
            }

            snapshot.DerivedReady = live > 0;
            if (live <= 0)
            {
                return;
            }
            int skillTotal = DefDatabase<SkillDef>.DefCount;
            // 输出顺序 = UI 分组顺序（产出 0-1 / 保障 2-4 / 风险 5-6 / 技能 7）：
            // 顺序即契约，UI 按固定索引插分组标题，**不要在中间插行**而不改 Roster 的索引表。
            AddDerivedRow(snapshot, "OutputPerCapita",
                (producedTotal / (double)live).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                -1f);
            AddDerivedRow(snapshot, "TopDependency",
                producedTotal > 0 ? (producedTop / (float)producedTotal).ToString("P0",
                    System.Globalization.CultureInfo.InvariantCulture) : DashText(),
                producedTotal > 0 ? producedTop / (float)producedTotal : -1f);
            AddDerivedRow(snapshot, "Equipped", Ratio(live, equipped), equipped / (float)live);
            AddDerivedRow(snapshot, "Bedded", Ratio(live, bedded), bedded / (float)live);
            AddDerivedRow(snapshot, "Fed", Ratio(live, fed), fed / (float)live);
            AddDerivedRow(snapshot, "MoodRisk", Ratio(live, moodRisk), moodRisk / (float)live);
            AddDerivedRow(snapshot, "Wounded", Ratio(live, wounded), wounded / (float)live);
            AddDerivedRow(snapshot, "SkillCover",
                coveredSkills.Count + " / " + skillTotal,
                skillTotal > 0 ? coveredSkills.Count / (float)skillTotal : -1f);
        }

        private static void CollectCoveredSkills(Pawn pawn, HashSet<SkillDef> covered)
        {
            if (pawn.skills == null || pawn.skills.skills == null)
            {
                return;
            }
            List<SkillRecord> skills = pawn.skills.skills;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRecord record = skills[i];
                if (record == null || record.TotallyDisabled || record.def == null)
                {
                    continue;
                }
                if (record.Level >= SkillCoverLevel)
                {
                    covered.Add(record.def);
                }
            }
        }

        private static string Ratio(int total, int part)
        {
            return total > 0 ? part + " / " + total : DashText();
        }

        /// <summary>缺失值占位（与工程既有约定一致：符号不进 Keyed）。</summary>
        private static string DashText()
        {
            return "—";
        }

        private static void AddDerivedRow(PopulationOverviewSnapshot snapshot, string key,
            string valueText, float share01)
        {
            snapshot.Derived.Add(new PopulationCountRow
            {
                Label = ("PersonalChronicle.UI.MasterControl.Population.Derived." + key).Translate().ToString(),
                ValueText = valueText,
                Share01 = share01
            });
        }

        /// <summary>
        /// ② 信仰的**戒律榜**：统计全体有理念者的 Precept（按 def 名聚合，取前 N）。
        /// 只在启用 Ideology 时执行；读取失败由面板级失败隔离兜住（不拖垮其它面板）。
        /// </summary>
        private void FillPrecepts(PopulationOverviewSnapshot snapshot)
        {
            if (!ModsConfig.IdeologyActive)
            {
                return;
            }
            IReadOnlyList<ArchiveObject> objects = queries.GetObjectsOfCategory(ArchiveCategoryKeys.Pawn);
            if (objects == null || objects.Count == 0)
            {
                return;
            }
            Dictionary<string, int> counts = new Dictionary<string, int>(System.StringComparer.Ordinal);
            for (int i = 0; i < objects.Count; i++)
            {
                PawnObject pawn = objects[i] as PawnObject;
                if (pawn == null || !queries.IsCurrentlyEnlisted(pawn.StableId))
                {
                    continue;
                }
                Pawn live = queries.GetLivePawn(pawn.StableId);
                if (live == null)
                {
                    continue;
                }
                // ⚠ 2026-09-15（审计 COMP-006 修复）：DLC 类型 `Ideo`/`Precept` 收口进
                // `Compatibility/Dlc/IdeologyAdapter`（只回传 defName 计数，Core 不见 DLC 类型）。
                Compatibility.Dlc.IdeologyAdapter.AccumulatePreceptsOf(live, counts);
            }
            FillTopRows(snapshot.Precepts, counts, TopListLimit, localized: true);
        }

        /// <summary>⑦ 荣誉的**勋章榜**：按 <c>MedalDef</c> 聚合全体档案的已授予勋章，取前 N。</summary>
        private void FillMedalTop(PopulationOverviewSnapshot snapshot)
        {
            IReadOnlyList<ArchiveObject> objects = queries.GetObjectsOfCategory(ArchiveCategoryKeys.Pawn);
            if (objects == null || objects.Count == 0)
            {
                return;
            }
            Dictionary<string, int> counts = new Dictionary<string, int>(System.StringComparer.Ordinal);
            for (int i = 0; i < objects.Count; i++)
            {
                PawnObject pawn = objects[i] as PawnObject;
                if (pawn == null || pawn.GrantedMedals == null)
                {
                    continue;
                }
                for (int m = 0; m < pawn.GrantedMedals.Count; m++)
                {
                    string defName = pawn.GrantedMedals[m];
                    if (string.IsNullOrEmpty(defName))
                    {
                        continue;
                    }
                    int seen;
                    counts.TryGetValue(defName, out seen);
                    counts[defName] = seen + 1;
                }
            }
            FillTopRows(snapshot.MedalTop, counts, TopListLimit, localized: false);
        }

        /// <summary>
        /// 把 defName → 计数 的字典转成「榜」并写入 <paramref name="target"/>（按数量降序）。
        /// <paramref name="localized"/> = true 时标签取 <c>def.LabelCap</c>（Precept），
        /// false 时取 <c>MedalDef.LabelCap</c>（勋章）。
        /// </summary>
        private static void FillTopRows(List<PopulationCountRow> target,
            Dictionary<string, int> counts, int limit, bool localized)
        {
            target.Clear();
            if (counts.Count == 0)
            {
                return;
            }
            List<KeyValuePair<string, int>> rows =
                new List<KeyValuePair<string, int>>(counts);
            rows.Sort((a, b) => b.Value != a.Value
                ? b.Value.CompareTo(a.Value)
                : string.CompareOrdinal(a.Key, b.Key));
            int max = rows[0].Value;
            int take = Mathf.Min(limit, rows.Count);
            for (int i = 0; i < take; i++)
            {
                string label = LabelOf(rows[i].Key, localized);
                target.Add(new PopulationCountRow
                {
                    Label = label,
                    ValueText = rows[i].Value.ToString(),
                    Share01 = max > 0 ? rows[i].Value / (float)max : 0f
                });
            }
        }

        private static string LabelOf(string defName, bool localized)
        {
            if (!localized)
            {
                Def medal = DefDatabase<MedalDef>.GetNamedSilentFail(defName);
                if (medal != null)
                {
                    return medal.LabelCap.ToString();
                }
            }
            Def precept = DefDatabase<PreceptDef>.GetNamedSilentFail(defName);
            if (precept != null)
            {
                return precept.LabelCap.ToString();
            }
            return defName;
        }
    }
}
