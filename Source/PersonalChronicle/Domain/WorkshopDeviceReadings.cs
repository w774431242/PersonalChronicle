using System;
using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 一台机台的**运行读数**（状态 / 功率 / 工单 / 作业者 / 进度）。
    ///
    /// <para><b>只给键与数值，不给文案</b>：状态是<b>键</b>（交
    /// <see cref="PersonalChronicle.Application.EquipmentStateText.TranslationKeyOf"/> 翻译），
    /// 工单是<b>计数</b>（文案由 UI 按语言拼）—— 与 <see cref="EquipmentState"/>/<c>MedalMetricKeys</c>
    /// 同一分工，Domain 不持有用户可见文本。</para>
    /// </summary>
    internal struct WorkshopDeviceReading
    {
        /// <summary>状态键（run / idle / fault / off）。</summary>
        internal string StateKey;

        /// <summary>功率文本（"480W"；无 <c>CompPowerTrader</c> 或输出 ≈ 0 时为空）。</summary>
        internal string PowerText;

        /// <summary>工单数（0 = 无单；文案由 UI 拼）。</summary>
        internal int BillCount;

        /// <summary>是否有**现在就该做**的工单（<c>BillStack.AnyShouldDoNow</c>）—— 状态派生的输入之一。</summary>
        internal bool HasWork;

        /// <summary>正在做这台活的 pawn 名（空 = 无人在做）。</summary>
        internal string OperatorText;

        /// <summary>进度 0..1；<b>&lt; 0 = 不画进度条</b>（无人在做 / 取不到配方）。</summary>
        internal float Progress01;

        /// <summary>当前工单的配方名（<c>RecipeDef.LabelCap</c>；无工单 = 空）。</summary>
        internal string RecipeLabel;

        /// <summary>当前工单的目标数（<c>Bill_Production.targetCount</c>）；<b>-1 = ∞ / 非计数模式 / 无工单</b>。</summary>
        internal int RecipeTarget;

        /// <summary>当前工单配方的**对应技能**（<c>RecipeDef.workSkill</c>，原版字段）；无工单 = null。</summary>
        internal SkillDef WorkSkill;

        /// <summary>
        /// 原版工单**指定的生产人员**（<c>Bill.PawnRestriction</c>）；未指定 = 空。
        ///
        /// <para>⚠️ 这是**原版**的"这台设备由谁做"（工单详情里的工人限制），与 PC 侧的
        /// 车间排班（班组/固定班/轮岗）是两回事 —— 玩家在设备上指定了人，卡上必须看得见。</para>
        /// </summary>
        internal string AssignedText;
    }

    /// <summary>
    /// 机台读数的**唯一读取器**。
    ///
    /// <para><b>⛔ 为什么要有它（2026-09-13）</b>：同一件事原本有<b>两份实现</b> ——
    /// <c>WorkshopFloorViewProvider.BuildDevice</c>（俯视图/详情，10 字段）与
    /// <c>WorkshopLayoutCanvas.BuildDeviceEntries</c>（自由画布设备卡，5 字段）。
    /// 后果是**画布卡参数贫瘠**（缺工单/作业者/进度）、且两边的功率口径已经开始漂移
    /// （一边抑制 0W、一边不抑制）。读数收口到这里，两边只做各自的**排版**。</para>
    ///
    /// <para><b>进度是近似值</b>（预览原话）：分子 = <c>JobDriver_DoBill.workLeft</c> 剩余，
    /// 分母 = <c>RecipeDef.WorkAmountTotal</c> 额定工时 —— 分子受技能/品质修正，因此只作"大致进度"读。</para>
    /// </summary>
    internal static class WorkshopDeviceReader
    {
        /// <summary>功率视为"无输出"的下限（W）：0.5 以下不显示，避免一排 `0W` 噪声。</summary>
        private const float MinReportedWatts = 0.5f;

        /// <summary>读一台机台（<paramref name="activeWorker"/> 由 <see cref="CollectActiveWorkers"/> 提供，可为 null）。</summary>
        internal static WorkshopDeviceReading Read(Thing thing, Pawn activeWorker)
        {
            WorkshopDeviceReading reading = new WorkshopDeviceReading
            {
                StateKey = EquipmentStateResolver.KeyOff,
                PowerText = string.Empty,
                OperatorText = string.Empty,
                Progress01 = -1f
            };
            if (thing == null)
            {
                return reading;
            }

            CompBreakdownable breakdown = thing.TryGetComp<CompBreakdownable>();
            bool broken = breakdown != null && breakdown.BrokenDown;
            CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
            bool powerOn = power == null || power.PowerOn;

            IBillGiver giver = thing as IBillGiver;
            BillStack stack = giver != null ? giver.BillStack : null;
            reading.BillCount = stack != null ? stack.Count : 0;
            reading.HasWork = stack != null && stack.AnyShouldDoNow;

            // 当前工单（卡片"生产"行的数据）：配方名 / 目标数 / 配方对应技能 —— 全部原版直读。
            Bill active = FindActiveBill(stack);
            RecipeDef activeRecipe = active != null ? active.recipe : null;
            if (activeRecipe != null)
            {
                reading.RecipeLabel = activeRecipe.LabelCap.ToString();
                reading.WorkSkill = activeRecipe.workSkill;
                Bill_Production production = active as Bill_Production;
                // `repeatMode / targetCount` 只在 Bill_Production 上（基类 Bill 无此成员，已核实）。
                reading.RecipeTarget = production != null
                    && production.repeatMode == BillRepeatModeDefOf.RepeatCount
                    ? production.targetCount
                    : -1;
            }

            reading.StateKey = EquipmentStateResolver.KeyOf(
                EquipmentStateResolver.Resolve(broken, powerOn, reading.HasWork));

            // 原版语义：PowerOutput 负 = 耗电、正 = 发电（已反编译核实；没有 PowerConsumption 成员）。
            if (power != null)
            {
                float watts = Mathf.Abs(power.PowerOutput);
                if (watts >= MinReportedWatts)
                {
                    reading.PowerText = Mathf.RoundToInt(watts).ToString(CultureInfo.InvariantCulture) + "W";
                }
            }

            // 原版「指定生产人员」：`pawnRestriction` 是私有字段，对外只有只读属性 `PawnRestriction`
            // （已反编译核实）—— 玩家在工单里指定了人，这里必须读出来。
            if (active != null && active.PawnRestriction != null)
            {
                reading.AssignedText = active.PawnRestriction.LabelShort;
            }

            if (activeWorker != null)
            {
                reading.OperatorText = activeWorker.LabelShort;
            }
            reading.Progress01 = ResolveProgress(activeWorker, thing);
            return reading;
        }

        /// <summary>
        /// 当前工单：优先**现在就该做**的那张（<c>ShouldDoNow</c>），否则队列首张。
        /// （从 `WorkshopFloorViewProvider.Detail` 迁入 —— 设备详情与设备卡共用一份口径。）
        /// </summary>
        internal static Bill FindActiveBill(BillStack stack)
        {
            if (stack == null)
            {
                return null;
            }
            for (int i = 0; i < stack.Count; i++)
            {
                Bill bill = stack[i];
                if (bill != null && bill.ShouldDoNow())
                {
                    return bill;
                }
            }
            return stack.Count > 0 ? stack[0] : null;
        }

        /// <summary>
        /// 近似进度：分子 = 该台正在执行的 <c>JobDriver_DoBill.workLeft</c> 剩余；
        /// 分母 = <c>RecipeDef.WorkAmountTotal</c> 额定工时。取不到（无人在做 / 配方缺失）→ -1（不画条）。
        /// </summary>
        internal static float ResolveProgress(Pawn worker, Thing bench)
        {
            if (worker == null || bench == null)
            {
                return -1f;
            }
            try
            {
                JobDriver_DoBill driver = worker.jobs?.curDriver as JobDriver_DoBill;
                if (driver == null || driver.BillGiver == null || (Thing)driver.BillGiver != bench)
                {
                    return -1f;
                }
                Bill bill = driver.job?.bill;
                RecipeDef recipe = bill != null ? bill.recipe : null;
                float total = recipe != null ? recipe.WorkAmountTotal(null) : 0f;
                if (total <= 1f)
                {
                    return -1f;
                }
                return Mathf.Clamp01(1f - driver.workLeft / total);
            }
            catch (Exception)
            {
                return -1f;
            }
        }

        /// <summary>正在执行 DoBill 的殖民者：设备 → 人（用于操作员文字与进度）。</summary>
        internal static Dictionary<Thing, Pawn> CollectActiveWorkers(Map map)
        {
            Dictionary<Thing, Pawn> result = new Dictionary<Thing, Pawn>();
            if (map == null || map.mapPawns == null)
            {
                return result;
            }
            IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
            if (spawned == null)
            {
                return result;
            }
            for (int i = 0; i < spawned.Count; i++)
            {
                Pawn pawn = spawned[i];
                if (pawn == null || pawn.Dead || pawn.jobs == null)
                {
                    continue;
                }
                JobDriver_DoBill driver = pawn.jobs.curDriver as JobDriver_DoBill;
                Thing bench = driver?.BillGiver as Thing;
                if (bench != null && !result.ContainsKey(bench))
                {
                    result[bench] = pawn;
                }
            }
            return result;
        }
    }
}
