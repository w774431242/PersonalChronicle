using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// 勋章授予服务（阶段一：阈值类 Pawn 勋章写入链路）。
    ///
    /// 判定引擎 <see cref="MedalAwardEvaluator"/> 是纯逻辑、零副作用；本服务是
    /// 它唯一的"写入方"——把 NewAwards 持久化到 <see cref="PawnObject.GrantedMedals"/>
    /// （append-only，去重），并触发 <see cref="ChronicleGameComponent.MarkChanged"/>
    /// 使 Read Model 失效重建。
    ///
    /// 只对活着的归档殖民者判定（对齐 reconcile 活读语义）；死亡者不追授，
    /// 生涯台账冻结语义一致。触发频率由调用方（reconcile 节流）控制。
    /// </summary>
    public static class MedalAwardService
    {
        /// <summary>
        /// 对当前存活归档殖民者执行一次授勋判定与写入。
        /// </summary>
        /// <returns>本次新授予的勋章 Def（按授予顺序），调用方可据此弹公告；无新授予返回空列表。</returns>
        public static List<MedalDef> Run(ChronicleGameComponent component)
        {
            List<MedalDef> newAwards = new List<MedalDef>();
            if (component == null || component.Objects == null)
            {
                return newAwards;
            }

            // Recording gate 与工作采样/reconcile 共享同一开关。
            if (PersonalChronicleMod.Settings == null || !PersonalChronicleMod.Settings.EnableRecording)
            {
                return newAwards;
            }

            List<ColonyMember> live = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < live.Count; i++)
            {
                ColonyMember member = live[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                Pawn pawn = member.Pawn;
                if (pawn.GetUniqueLoadID() == null)
                {
                    continue;
                }

                IReadOnlyList<PawnRecord> records = component.GetRecordsFor(pawn);
                if (records == null || records.Count == 0)
                {
                    // 尚未建档（reconcile 确认窗口未关），下次再判。
                    continue;
                }
                PawnObject pawnObject = records[0] as PawnObject;
                if (pawnObject == null || pawnObject.IsArchived)
                {
                    continue;
                }

                MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(pawnObject);
                List<MedalEvaluation> pending = result.NewAwards;
                if (pending == null || pending.Count == 0)
                {
                    continue;
                }

                for (int j = 0; j < pending.Count; j++)
                {
                    MedalEvaluation eval = pending[j];
                    if (eval == null || eval.Def == null)
                    {
                        continue;
                    }
                    pawnObject.AddGrantedMedal(eval.Def.defName);
                    // P8 闭环：授勋回写 CareerEvent(MedalGranted) 事实（不写评价数值）。
                    WriteMedalGrantedEvent(pawnObject, eval.Def, pawn);
                    newAwards.Add(eval.Def);
                    // T3 金质公告：NewAwards 仅含"未授予"判定，写入即首次，恰好弹一次。
                    AnnounceGold(pawn, eval.Def);
                }
                component.MarkChanged();
            }

            return newAwards;
        }

        /// <summary>
        /// 手动授勋入口（玩家在档案馆 Pawn 详情页主动授予）。复用与自动授勋（<see cref="Run"/>）
        /// 完全相同的写入/公告链路，保证语义一致，避免逻辑分叉。
        ///
        /// 校验：仅当 <see cref="MedalAwardEvaluator"/> 判定该勋章对当前人物为
        /// "达标且未授予"（IsNewAward=true）时才写入；已授予或尚未达标者被拦截，
        /// 返回 false，调用方据此给出反馈而不重复写入。
        /// </summary>
        /// <param name="pawn">受勋殖民者（活读实例）。</param>
        /// <param name="def">要授予的勋章 Def（须为 Threshold + Pawn 类，与判定范围一致）。</param>
        /// <param name="component">持久化组件，用于取 PawnObject 与触发 MarkChanged。</param>
        /// <returns>true=本次成功授予并写入；false=被校验拦截（已授予/未达标/入参非法）。</returns>
        public static bool AwardManual(Pawn pawn, MedalDef def, ChronicleGameComponent component)
        {
            if (pawn == null || def == null || component == null)
            {
                return false;
            }
            if (def.kind != MedalKind.Threshold || def.ownerType != MedalOwner.Pawn)
            {
                // 手动授勋仅覆盖阈值类 Pawn 勋章（与自动授勋判定范围对齐）。
                return false;
            }

            IReadOnlyList<PawnRecord> records = component.GetRecordsFor(pawn);
            if (records == null || records.Count == 0)
            {
                return false;
            }
            PawnObject pawnObject = records[0] as PawnObject;
            if (pawnObject == null || pawnObject.IsArchived)
            {
                // 档案已冻结（死亡/离队）不追授，与自动授勋语义一致。
                return false;
            }

            // 复用判定引擎校验：只有 IsNewAward（达标且未授予）才允许手动写入。
            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(pawnObject);
            MedalEvaluation match = null;
            for (int i = 0; i < result.Items.Count; i++)
            {
                if (result.Items[i] != null && result.Items[i].Def == def)
                {
                    match = result.Items[i];
                    break;
                }
            }
            if (match == null || !match.IsNewAward)
            {
                return false;
            }

            pawnObject.AddGrantedMedal(def.defName);
            // P8 闭环：手动授勋同样回写 CareerEvent(MedalGranted) 事实。
            WriteMedalGrantedEvent(pawnObject, def, pawn);
            AnnounceGold(pawn, def);
            component.MarkChanged();
            return true;
        }

        /// <summary>
        /// 金质勋章授勋公告（架构方案 §6.9 公告 Letter，仅首次、恰好一次）。
        /// 标题 = 勋章 Label 翻译（如「劳动模范·金质」）；正文 = UI.Medal.Gold.Letter
        /// （{0} = 人物名，{1} = 无材质称号名）；LookTargets 指向受勋者。
        /// 签名经引擎反射核验（1.6：ReceiveLetter(TaggedString, TaggedString, LetterDef,
        /// LookTargets, Faction, Quest, List&lt;ThingDef&gt;, string, int, bool)）。
        /// </summary>
        private static void AnnounceGold(Pawn pawn, MedalDef def)
        {
            if (def == null || def.tier != MedalTier.Gold || pawn == null)
            {
                return;
            }

            string label = MedalTranslationKeys.Label(def.defName).Translate().ToString();
            string seriesName = MedalTranslationKeys.SeriesName(MedalDef.SeriesKeyOf(def.defName)).Translate();
            string text = MedalTranslationKeys.GoldLetter().Translate(pawn.LabelShort, seriesName).ToString();

            Find.LetterStack.ReceiveLetter(
                label,
                text,
                LetterDefOf.PositiveEvent,
                new LookTargets(pawn),
                null,
                null,
                null,
                null,
                0,
                true);
        }

        /// <summary>
        /// P8 闭环：授勋回写 CareerEvent(MedalGranted) 事实（仅事实，不写评价数值）。
        /// 与 TitleGranted 同源模式；null-safe，失败仅跳过不污染主链路。
        /// </summary>
        private static void WriteMedalGrantedEvent(PawnObject pawnObject, MedalDef def, Pawn pawn)
        {
            if (pawnObject == null || def == null || pawnObject.CareerData == null)
            {
                return;
            }
            try
            {
                long tick = Find.TickManager.TicksGame;
                string pawnId = pawn != null ? pawn.GetUniqueLoadID() : (pawnObject.StableId ?? string.Empty);
                pawnObject.CareerData.Events.Add(new PersonalChronicle.Domain.Career.CareerEvent(
                    pawnId + ":" + tick + ":medal:" + def.defName,
                    pawnId,
                    tick,
                    PersonalChronicle.Domain.Career.CareerEventType.MedalGranted,
                    def.defName,
                    null,
                    null,
                    null,
                    1,
                    null));
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to write medal granted event: " + ex.Message);
            }
        }
    }
}
