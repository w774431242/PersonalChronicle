using PersonalChronicle.Application.Effects;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Workshop;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application.Workshop
{
    /// <summary>
    /// 车间 + 专业「成品效果」结算服务（落地管线 Step 5b/5c）。
    ///
    /// <para><b>归位（2026-09-21 审计修复 · CHG-SHOP-AUDIT-001 · A-2）</b>：本类承载的
    /// 业务逻辑原直接写在 <c>Capture/Patch_GenRecipe</c> 的 Postfix 内（约 200 行），
    /// 违反归位铁律「Capture 只做钩子 → 调 Application，不放业务」。现下沉到 Application：
    /// Patch 只保留 null/派系守卫 + 转调（记录产役履历、记录物品流、调本服务）。</para>
    ///
    /// <para><b>取值路径</b>：车间加成走 <see cref="WorkshopBonusAccess"/>（Application 契约，
    /// 实现由 Automation 注入）—— 本类不再引用 Automation 任何类型（A-1 同步闭合）。
    /// 概率/品质判定走 <see cref="WorkshopProductionRules"/>（Domain 纯函数，可离线单测）。</para>
    ///
    /// <para><b>性能</b>：每次产出一件成品调用一次；车间加成一次委托调用（实现侧字典命中），
    /// 零解析、零遍历。</para>
    /// </summary>
    public static class WorkshopProductionEffects
    {
        /// <summary>
        /// 对一件刚产出的成品结算全部车间/专业效果：
        /// ① 品质偏置（专业能力 + 车间偏移）② 耐久/精度登记
        /// ③ 额外产出 ④ 主料返还 ⑤ 劣质回炉。
        /// 任何环节数据缺失 → 该项零影响（不猜值、不编造）。
        /// </summary>
        public static void Apply(Thing product, RecipeDef recipeDef, Pawn worker)
        {
            if (product == null || product.def == null || worker == null || worker.Map == null)
            {
                return;
            }
            WorkshopEffectiveBonus bonus = WorkshopBonusAccess.WorkingBonusOf(worker);
            ApplyQualityBias(product, recipeDef, worker, bonus.qualityOffset);
            ApplyProductBonuses(product, recipeDef, worker, bonus);
        }

        /// <summary>
        /// 成品品质偏置：原版 PostProcessProduct 内部已按 GenerateQualityCreatedByPawn 设好品质；
        /// 本方法在其后按「专业能力偏置 + 车间品质偏移」重算并覆盖品质档位。
        /// 范围精确（仅制造产物），无品质组件 / 总偏移为 0 / 目标档未变均零影响。
        /// </summary>
        private static void ApplyQualityBias(Thing product, RecipeDef recipeDef, Pawn worker, int workshopLevels)
        {
            CompQuality compQuality = product.TryGetComp<CompQuality>();
            if (compQuality == null)
            {
                return;
            }
            int levels = ProfessionalEffectService.GetQualityBiasLevels(worker, recipeDef);
            int total = levels + workshopLevels;
            if (total == 0)
            {
                return;
            }
            QualityCategory current = compQuality.Quality;
            int targetIndex = ProfessionalEffectResolver.ClampQuality((int)current, total);
            QualityCategory target = (QualityCategory)targetIndex;
            if (target == current)
            {
                return;
            }
            compQuality.SetQuality(target, null);
            if (Prefs.DevMode)
            {
                ChronicleLog.Info(ChronicleLog.Category.Capture,
                    "[CAREER] QualityBias applied: pawn=" + worker.GetUniqueLoadID()
                    + " recipe=" + (recipeDef != null ? recipeDef.defName : "null")
                    + " " + current + " -> " + target
                    + " (career=" + levels + " workshop=" + workshopLevels + ")");
            }
        }

        /// <summary>产出量 / 耗材 / 良品率 / 材质加成四项（全部读在岗已解析的缓存，零解析）。</summary>
        private static void ApplyProductBonuses(Thing product, RecipeDef recipeDef, Pawn worker, WorkshopEffectiveBonus bonus)
        {
            // ① 精度 / 耐久：登记到成品注册表，由 MaxHitPoints / Accuracy* 的 StatPart 在求值时取用。
            if (WorkshopProductionRules.ShouldRegisterProductBonus(bonus.maxHpFactor, bonus.accuracyFactor))
            {
                WorkshopBonusAccess.RegisterProductBonus(product.thingIDNumber, bonus.maxHpFactor, bonus.accuracyFactor);
            }

            // ② 产出量 ×1.20 ⇒ 20% 概率额外产出一件同规格成品（期望值 = 因子）。
            if (WorkshopProductionRules.ShouldSpawnExtraProduct(bonus.yieldFactor, Rand.Value))
            {
                SpawnExtraProduct(product, worker, bonus);
            }

            // ③ 耗材 ×0.90 ⇒ 10% 概率返还一份主料（期望值 = 1 − 因子）。
            if (WorkshopProductionRules.ShouldRefundIngredient(bonus.stuffCostFactor, Rand.Value))
            {
                RefundIngredient(recipeDef, worker);
            }

            // ④ 良品率（SPC 全检语义）：消除劣质尾 —— 造出 Awful/Poor 时按概率回炉提升到 Normal。
            //    原版没有"报废品"概念，故以"劣质档被拦截"来兑现"报废率下降"。
            CompQuality quality = product.TryGetComp<CompQuality>();
            if (quality != null)
            {
                int target = WorkshopProductionRules.SalvageQualityTargetIfTriggered(
                    (int)quality.Quality, bonus.goodRateFactor, Rand.Value);
                if (target >= 0)
                {
                    quality.SetQuality((QualityCategory)target, null);
                }
            }
        }

        /// <summary>
        /// 额外产出一件与成品同 def / 同材质 / 同品质的成品。
        /// <para>2026-09-11 体检：额外产出**同样登记**耐久/精度（否则同一批两件货只有一件吃加成）。</para>
        /// </summary>
        private static void SpawnExtraProduct(Thing product, Pawn worker, WorkshopEffectiveBonus bonus)
        {
            Thing extra = ThingMaker.MakeThing(product.def, product.Stuff);
            if (extra == null)
            {
                return;
            }
            CompQuality srcQuality = product.TryGetComp<CompQuality>();
            CompQuality dstQuality = extra.TryGetComp<CompQuality>();
            if (srcQuality != null && dstQuality != null)
            {
                dstQuality.SetQuality(srcQuality.Quality, null);
            }
            if (WorkshopProductionRules.ShouldRegisterProductBonus(bonus.maxHpFactor, bonus.accuracyFactor))
            {
                WorkshopBonusAccess.RegisterProductBonus(extra.thingIDNumber, bonus.maxHpFactor, bonus.accuracyFactor);
            }
            GenPlace.TryPlaceThing(extra, worker.Position, worker.Map, ThingPlaceMode.Near);
        }

        /// <summary>返还一份配方主料（1 个，落在工人脚下）。</summary>
        private static void RefundIngredient(RecipeDef recipeDef, Pawn worker)
        {
            if (recipeDef == null || recipeDef.ingredients == null || recipeDef.ingredients.Count == 0)
            {
                return;
            }
            IngredientCount first = recipeDef.ingredients[0];
            if (first == null || first.filter == null)
            {
                return;
            }
            ThingDef stuffDef = null;
            foreach (ThingDef allowed in first.filter.AllowedThingDefs)
            {
                stuffDef = allowed;
                break;
            }
            if (stuffDef == null)
            {
                return;
            }
            Thing refund = ThingMaker.MakeThing(stuffDef, GenStuff.DefaultStuffFor(stuffDef));
            if (refund == null)
            {
                return;
            }
            refund.stackCount = 1;
            GenPlace.TryPlaceThing(refund, worker.Position, worker.Map, ThingPlaceMode.Near);
        }
    }
}
