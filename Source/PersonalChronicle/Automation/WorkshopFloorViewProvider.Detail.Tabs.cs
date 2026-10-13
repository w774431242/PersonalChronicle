using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    public sealed partial class WorkshopFloorViewProvider
    {
        /// <summary>
        /// ⑦ 生产列表（**本机专属**，预览 §5c-Q L15）：数据源 = 本机 <c>BillStack</c>；
        /// 行 = 每条 <c>Bill</c>（配方 / 重复模式 / 挂起态），**第一条未挂起 = 执行中**（原版语义）。
        /// <para>可写：调序 / 挂起 / 移出 / 追加 —— UI 只回传「动作 + 行下标 + 稳定 ID」，
        /// 实际写入由 <see cref="WorkshopBillActions"/> 按原版 API 执行。</para>
        /// </summary>
        private static FloorDetailSectionView BuildBillQueueTab(IBillGiver bench, Thing thing)
        {
            FloorDetailSectionView section = new FloorDetailSectionView
            {
                Caption = T("PersonalChronicle.UI.Floor.Detail.Queue.Caption"),
                RenderKind = FloorDetailRenderKinds.BillQueue,
                BenchStableId = thing.def.defName + ":" + thing.thingIDNumber.ToString(CultureInfo.InvariantCulture),
                SourceKind = SourceApi
            };

            // 批次 2（2026-09-23）：进度取数**一次**（只有"当前执行中"那条用得上；与俯视图同源，
            //   不重复实现 workLeft ÷ totalWork）。
            float jobProgress = WorkshopDeviceReader.ResolveProgress(FindOperator(thing), thing);

            BillStack stack = bench != null ? bench.BillStack : null;
            if (stack == null || stack.Count == 0)
            {
                section.EmptyHint = T("PersonalChronicle.UI.Floor.Detail.Queue.Empty");
            }
            else
            {
                bool activeAssigned = false;
                for (int i = 0; i < stack.Count; i++)
                {
                    Bill bill = stack[i];
                    if (bill == null)
                    {
                        continue;
                    }
                    RecipeDef recipe = bill.recipe;
                    bool active = !bill.suspended && !activeAssigned;
                    if (active)
                    {
                        activeAssigned = true;
                    }
                    section.BillRows.Add(new FloorBillRowView
                    {
                        RecipeLabel = recipe != null ? recipe.LabelCap.ToString() : "—",
                        RecipeDefName = recipe != null ? recipe.defName : "—",
                        ModeText = BuildBillModeText(bill),
                        Suspended = bill.suspended,
                        Active = active,
                        // S3-5d（2026-09-23）：本工单指定的生产人员（原版 `PawnRestriction` 只读属性 ⇒ 直读安全）
                        AssignedName = bill.PawnRestriction != null
                            ? bill.PawnRestriction.LabelShort
                            : string.Empty,
                        // 批次 2（2026-09-23）：**进度条 + 独立状态列**（预览要求）——
                        //   进度只有"当前执行中"那条有真实值（与俯视图同源）；其余如实 -1（不画 0%）。
                        Progress01 = active ? jobProgress : -1f,
                        StateText = BuildBillStateText(active, bill.suspended, active ? jobProgress : -1f)
                    });
                }
            }

            List<RecipeDef> recipes = new List<RecipeDef>();
            WorkshopBillActions.CollectRecipes(thing, recipes);
            for (int i = 0; i < recipes.Count; i++)
            {
                section.RecipeDefNames.Add(recipes[i].defName);
                section.RecipeLabels.Add(recipes[i].LabelCap.ToString());
            }
            return section;
        }

        /// <summary>
        /// 批次 2（2026-09-23）：列表行**状态列**文案 —— 预览要求列表有独立状态列
        /// （`运行中 64%` / `等待` / `已挂起`）；原实现把状态塞进副行，与重复模式混在一起。
        /// 进度取不到（`&lt; 0`）时如实只说「运行中」，不编百分比。
        /// </summary>
        private static string BuildBillStateText(bool active, bool suspended, float progress01)
        {
            if (suspended)
            {
                return T("PersonalChronicle.UI.Floor.Detail.Queue.Suspended");
            }
            if (!active)
            {
                return T("PersonalChronicle.UI.Floor.Detail.Queue.StateWaiting");
            }
            return progress01 >= 0f
                ? T1("PersonalChronicle.UI.Floor.Detail.Queue.StateRunning",
                    Mathf.RoundToInt(Mathf.Clamp01(progress01) * 100f))
                : T("PersonalChronicle.UI.Floor.Detail.Queue.StateRunningNoProgress");
        }

        /// <summary>重复模式文案（原版三档：Forever / RepeatCount / TargetCount）。</summary>
        private static string BuildBillModeText(Bill bill)
        {
            if (bill is Bill_Production production)
            {
                if (production.repeatMode == BillRepeatModeDefOf.Forever)
                {
                    return T("PersonalChronicle.UI.Floor.Detail.Queue.ModeForever");
                }
                if (production.repeatMode == BillRepeatModeDefOf.TargetCount)
                {
                    return T1("PersonalChronicle.UI.Floor.Detail.Queue.ModeTarget", production.targetCount);
                }
                return T1("PersonalChronicle.UI.Floor.Detail.Queue.ModeRepeat", production.targetCount);
            }
            return "—";
        }

        /// <summary>
        /// 页签④品质（新预览拆回的独立页签）：<c>QualityCategory</c> 全枚举 0–6，**高亮当前档**。
        /// 数据全部来自原版 <see cref="CompQuality"/> ⇒ 每行 <see cref="SourceApi"/>。
        /// </summary>
        private static FloorDetailSectionView BuildQualTab(Thing thing)
        {
            FloorDetailSectionView section = new FloorDetailSectionView
            {
                Caption = T("PersonalChronicle.UI.Floor.Detail.Qual.Caption")
            };

            CompQuality quality = thing.TryGetComp<CompQuality>();
            if (quality == null || thing.TryGetComp<CompQuality>() == null)
            {
                section.EmptyHint = T("PersonalChronicle.UI.Floor.Detail.Qual.Empty");
                return section;
            }

            QualityCategory current = quality.Quality;
            // 枚举 0–6（Awful..Legendary）逐档列出，当前档取语气色 1（好）
            string[] catKeys =
            {
                "Awful", "Poor", "Normal", "Good", "Excellent", "Masterwork", "Legendary"
            };
            for (int i = 0; i < catKeys.Length; i++)
            {
                QualityCategory category = (QualityCategory)i;
                bool isCurrent = category == current;
                FloorDetailRowView row = Row(
                    "PersonalChronicle.UI.Floor.Detail.Qual.Cat." + catKeys[i],
                    isCurrent
                        ? T("PersonalChronicle.UI.Floor.Detail.Qual.Current")
                        : string.Empty, 0);
                row.Tone = isCurrent ? 1 : 0;
                row.SourceKind = SourceApi;
                section.Rows.Add(row);
            }
            return section;
        }


        private static void AddRow(FloorDetailSectionView section, string key, string value,
            int tone, int sourceKind, string labelOverride = null)
        {
            FloorDetailRowView row;
            if (!string.IsNullOrEmpty(labelOverride))
            {
                // 显式标签（已是本地化文本，如技能名）——不走 Keyed
                row = new FloorDetailRowView
                {
                    Label = labelOverride,
                    Value = value,
                    Tone = tone,
                    SourceKind = sourceKind
                };
            }
            else
            {
                row = Row(key, value, tone);
                row.SourceKind = sourceKind;
            }
            section.Rows.Add(row);
        }

        /// <summary>页签①运行：状态时序 + 电气读数（全部原版 Comp 直读）。</summary>
    }
}
